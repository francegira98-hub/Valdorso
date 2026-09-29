using System.Collections;
using System.Collections.Generic;
using Mirror;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using Valdorso.Combat;
using Valdorso.Creatures;
using Valdorso.Movement;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Va sul prefab del giocatore: se è in piedi, seduto o sdraiato, e su quale posto.
    /// Come ci si siede: il server riserva il posto; il personaggio cammina fino accanto al mobile (in piedi,
    /// con i passi); arrivato, lo dice al server, che solo allora lo mette seduto o sdraiato per tutti;
    /// mentre la posa arriva, il bacino si appoggia proprio sulla seduta o sul materasso.
    /// Ci si alza con E, muovendosi o saltando: si scende dal lato libero. Morendo o uscendo il posto si libera.
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class Postura : NetworkBehaviour
    {
        public enum Posa : byte { InPiedi, Seduto, Sdraiato }

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");

        [Tooltip("Velocità dei passi per raggiungere il mobile (m/s)")]
        [SerializeField] float velocitaAvvicinamento = 1.6f;

        [Tooltip("Secondi della posa che arriva (e del bacino che si appoggia)")]
        [SerializeField] float durataPosa = 0.65f;

        [Tooltip("Secondi per rimettersi in piedi accanto al mobile")]
        [SerializeField] float durataAlzarsi = 0.45f;

        [Tooltip("Di quanto il bacino sta sopra la seduta (m): lo scheletro ha le anche un po' dentro il corpo")]
        [SerializeField] float bacinoSopraSeduta = 0.1f;

        [Tooltip("Di quanto il bacino sta sopra il materasso da sdraiati (m)")]
        [SerializeField] float bacinoSopraLetto = 0.12f;

        [Tooltip("Da sdraiati la telecamera si allontana fino a questa distanza, per vedere bene il personaggio")]
        [SerializeField] float distanzaTelecameraSdraiati = 5.5f;

        [SyncVar(hook = nameof(QuandoCambiaPosa))]
        Posa posa;

        public Posa Attuale => posa;
        public bool InPiedi => posa == Posa.InPiedi;

        /// <summary>Vero da quando si va verso un posto a quando ci si è rialzati: niente altre interazioni.</summary>
        public bool Occupato => bloccato || posa != Posa.InPiedi;

        Creature creature;
        CreatureAnimator creatureAnimator;
        CharacterController controller;
        StarterAssetsInputs input;
        ThirdPersonController mover;
        Behaviour[] daFermare;
        bool[] eranoAccesi;
        bool bloccato;
        Coroutine spostamento;
        CinemachineThirdPersonFollow telecamera;
        float distanzaNormale = -1f;

        // Dove stanno le anche rispetto ai piedi in ogni posa, per questo corpo: si misura la prima volta
        readonly Dictionary<Posa, Vector3> ancheNellaPosa = new Dictionary<Posa, Vector3>();

        // Solo sul server
        Sedile sedile;
        int posto = -1;
        bool inArrivo;
        double prossimaRichiesta;

        void Awake()
        {
            creature = GetComponent<Creature>();
            creatureAnimator = GetComponent<CreatureAnimator>();
            controller = GetComponent<CharacterController>();
            input = GetComponent<StarterAssetsInputs>();
            mover = GetComponent<ThirdPersonController>();
            daFermare = new Behaviour[]
            {
                mover, GetComponent<DodgeRoll>(), GetComponent<MeleeCombat>(), GetComponent<ObstacleTraversal>()
            };
            eranoAccesi = new bool[daFermare.Length];
        }

        public override void OnStartServer() => creature.Died += QuandoMuore;
        public override void OnStopServer()
        {
            creature.Died -= QuandoMuore;
            LiberaPosto();
        }

        public override void OnStartClient() => QuandoCambiaPosa(Posa.InPiedi, posa);

        public override void OnStartLocalPlayer()
        {
            // La telecamera che segue il personaggio (PlayerFollowCamera, dentro il prefab del giocatore)
            telecamera = GetComponentInChildren<CinemachineThirdPersonFollow>(true);
            if (telecamera != null) distanzaNormale = telecamera.CameraDistance;
        }

        void Update()
        {
            if (!isLocalPlayer) return;

            // Da sdraiati la telecamera si allontana piano, e torna com'era quando ci si alza
            if (telecamera != null && distanzaNormale > 0f)
            {
                float voluta = posa == Posa.Sdraiato ? distanzaTelecameraSdraiati : distanzaNormale;
                telecamera.CameraDistance = Mathf.MoveTowards(telecamera.CameraDistance, voluta, 4f * Time.deltaTime);
            }

            if (!bloccato || input == null) return;
            // Muoversi o saltare fa alzare, come premere E
            if (input.move.sqrMagnitude > 0.25f || input.jump)
            {
                input.jump = false;
                ChiediDiAlzarti();
            }
        }

        /// <summary>Sul PC di chi gioca: chiede al server di alzarsi (o di lasciar perdere il posto verso cui si va).</summary>
        public void ChiediDiAlzarti()
        {
            if (!isLocalPlayer || !Occupato) return;
            CmdAlzati();
        }

        // ---------- Server ----------

        /// <summary>Riserva il posto e manda il personaggio a camminare fino accanto al mobile.</summary>
        [Server]
        public bool ServerSiedi(Sedile s, int indice)
        {
            if (posa != Posa.InPiedi || sedile != null || creature.IsDead) return false;
            if (!s.ServerOccupa(indice, netId)) return false;

            sedile = s;
            posto = indice;
            inArrivo = true;
            TargetAvvicinati(s.Uscita(indice), s, indice);
            return true;
        }

        /// <summary>Il personaggio è arrivato accanto al mobile: ora si siede (o si sdraia) per tutti.</summary>
        [Command]
        void CmdArrivato()
        {
            if (!inArrivo || sedile == null || creature.IsDead) return;
            inArrivo = false;
            sedile.Posto(posto, out Vector3 pos, out Quaternion rot);
            posa = sedile.Tipo == Sedile.TipoSeduta.Sdraiati ? Posa.Sdraiato : Posa.Seduto;
            Vector3 bacino = sedile.Superficie(posto) + Vector3.up * (posa == Posa.Sdraiato ? bacinoSopraLetto : bacinoSopraSeduta);
            TargetSiediti(pos, rot, bacino);
        }

        [Command]
        void CmdAlzati()
        {
            double adesso = Time.timeAsDouble;
            if (adesso < prossimaRichiesta) return;
            prossimaRichiesta = adesso + 0.3;
            ServerAlzati();
        }

        [Server]
        void ServerAlzati()
        {
            if (posa == Posa.InPiedi && sedile == null) return;
            Vector3 uscita = sedile != null ? sedile.Uscita(posto) : transform.position;
            bool eraSeduto = posa != Posa.InPiedi;
            LiberaPosto();
            posa = Posa.InPiedi;
            TargetAlzati(uscita, eraSeduto);
        }

        [Server]
        void LiberaPosto()
        {
            if (sedile != null && posto >= 0) sedile.ServerLibera(posto, netId);
            sedile = null;
            posto = -1;
            inArrivo = false;
        }

        [Server]
        void QuandoMuore()
        {
            if (posa == Posa.InPiedi && sedile == null) return;
            LiberaPosto();
            posa = Posa.InPiedi;
            TargetAlzati(transform.position, false); // si resta dove si è: l'animazione di morte fa il resto
        }

        // ---------- PC di chi gioca ----------

        [TargetRpc]
        void TargetAvvicinati(Vector3 accanto, Sedile s, int indice)
        {
            Blocca();
            if (spostamento != null) StopCoroutine(spostamento);
            spostamento = StartCoroutine(AvvicinatiE(accanto, s, indice));
        }

        /// <summary>Cammina (con i passi dell'animazione) fino accanto al mobile, girato verso il posto, poi lo dice al server.</summary>
        IEnumerator AvvicinatiE(Vector3 accanto, Sedile s, int indice)
        {
            Animator anim = GetComponent<Animator>();
            Vector3 da = transform.position;
            Vector3 verso = accanto - da;
            verso.y = 0f;
            float distanza = verso.magnitude;

            if (distanza > 0.05f)
            {
                float durata = Mathf.Clamp(distanza / velocitaAvvicinamento, 0.2f, 2f);
                Quaternion daRot = transform.rotation;
                Quaternion camminando = Quaternion.LookRotation(verso, Vector3.up);
                float t = 0f;
                while (t < 1f && bloccato)
                {
                    t += Time.deltaTime / durata;
                    float k = Mathf.Clamp01(t);
                    transform.SetPositionAndRotation(Vector3.Lerp(da, accanto, k),
                        Quaternion.Slerp(daRot, camminando, Mathf.Clamp01(t * 3f)));
                    if (anim != null) { anim.SetFloat(SpeedHash, distanza / durata); anim.SetFloat(MotionSpeedHash, 1f); }
                    yield return null;
                }
            }
            if (anim != null) anim.SetFloat(SpeedHash, 0f);
            if (!bloccato) yield break;

            // Girati verso il posto, poi ci si siede
            if (s != null)
            {
                s.Posto(indice, out _, out Quaternion rotPosto);
                Quaternion daRot = transform.rotation;
                float g = 0f;
                while (g < 1f && bloccato)
                {
                    g += Time.deltaTime / 0.2f;
                    transform.rotation = Quaternion.Slerp(daRot, rotPosto, Mathf.SmoothStep(0f, 1f, g));
                    yield return null;
                }
            }
            if (bloccato) CmdArrivato();
        }

        [TargetRpc]
        void TargetSiediti(Vector3 pos, Quaternion rot, Vector3 bacino)
        {
            if (spostamento != null) StopCoroutine(spostamento);
            spostamento = StartCoroutine(SiediE(pos, rot, bacino));
        }

        /// <summary>
        /// Mentre la posa arriva, porta il bacino dello scheletro proprio sulla seduta o sul materasso, in un solo
        /// movimento. La prima volta per ogni posa lo scarto tra anche e piedi si misura; poi si ricorda.
        /// </summary>
        IEnumerator SiediE(Vector3 pos, Quaternion rot, Vector3 bacino)
        {
            Animator anim = GetComponent<Animator>();
            Transform anche = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (anche == null) { yield return Sposta(pos, rot, durataPosa); yield break; }

            Posa questa = posa;
            Vector3 da = transform.position;
            Quaternion daRot = transform.rotation;
            bool ricordata = ancheNellaPosa.TryGetValue(questa, out Vector3 anchePosa);

            float t = 0f;
            while (t < 1f && bloccato)
            {
                t += Time.deltaTime / durataPosa;
                float k = Mathf.SmoothStep(0f, 1f, t);
                Quaternion r = Quaternion.Slerp(daRot, rot, k);
                Vector3 scarto = ricordata ? rot * anchePosa : anche.position - transform.position;
                transform.SetPositionAndRotation(Vector3.Lerp(da, bacino - scarto, k), r);
                yield return null;
            }

            // Rifinitura dolce mentre la posa si assesta, poi si ricorda lo scarto per le prossime volte
            float c = 0f;
            while (c < 0.5f && bloccato)
            {
                c += Time.deltaTime;
                transform.position += (bacino - anche.position) * Mathf.Min(1f, Time.deltaTime * 6f);
                yield return null;
            }
            if (bloccato) ancheNellaPosa[questa] = Quaternion.Inverse(transform.rotation) * (anche.position - transform.position);
        }

        [TargetRpc]
        void TargetAlzati(Vector3 uscita, bool eraSeduto)
        {
            if (spostamento != null) StopCoroutine(spostamento);
            spostamento = StartCoroutine(AlzatiE(uscita, eraSeduto));
        }

        IEnumerator AlzatiE(Vector3 uscita, bool eraSeduto)
        {
            Animator anim = GetComponent<Animator>();
            if (anim != null) anim.SetFloat(SpeedHash, 0f);
            if (!creature.IsDead && eraSeduto) yield return Sposta(uscita, transform.rotation, durataAlzarsi);
            Sblocca();
        }

        IEnumerator Sposta(Vector3 pos, Quaternion rot, float durata)
        {
            Vector3 da = transform.position;
            Quaternion daRot = transform.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, durata);
                float k = Mathf.SmoothStep(0f, 1f, t);
                transform.SetPositionAndRotation(Vector3.Lerp(da, pos, k), Quaternion.Slerp(daRot, rot, k));
                yield return null;
            }
            transform.SetPositionAndRotation(pos, rot);
        }

        void Blocca()
        {
            if (bloccato) return;
            bloccato = true;
            for (int i = 0; i < daFermare.Length; i++)
            {
                eranoAccesi[i] = daFermare[i] != null && daFermare[i].enabled;
                if (daFermare[i] != null) daFermare[i].enabled = false;
            }
            if (controller != null) controller.enabled = false;
            if (input != null) { input.move = Vector2.zero; input.jump = false; input.sprint = false; }
        }

        void Sblocca()
        {
            if (!bloccato) return;
            bloccato = false;
            if (controller != null) controller.enabled = true;
            for (int i = 0; i < daFermare.Length; i++)
            {
                if (daFermare[i] == null) continue;
                // Da morti il movimento lo riaccende chi gestisce la morte (LocalPlayerSetup), non noi
                if (creature.IsDead && daFermare[i] == mover) continue;
                daFermare[i].enabled = eranoAccesi[i];
            }
        }

        // ---------- Tutti ----------

        void QuandoCambiaPosa(Posa prima, Posa adesso)
        {
            if (creatureAnimator != null)
                creatureAnimator.ImpostaPosa(adesso == Posa.Seduto, adesso == Posa.Sdraiato);
        }
    }
}