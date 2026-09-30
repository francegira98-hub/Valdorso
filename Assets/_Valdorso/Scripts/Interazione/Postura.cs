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
    /// Scala a pioli: il server la riserva, poi sul PC di chi gioca W sale e S scende; in cima si passa sul piano,
    /// in fondo si rimettono i piedi a terra; E o Spazio fanno staccare (a metà si cade).
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class Postura : NetworkBehaviour
    {
        public enum Posa : byte { InPiedi, Seduto, Sdraiato, Scala }

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

        [Header("Scala a pioli")]
        [Tooltip("Velocità lungo la scala (m/s). La usa anche il menu delle animazioni per la velocità dello stato Scala")]
        [SerializeField] float velocitaScala = 0.9f;

        [Tooltip("Correzione dell'altezza del corpo sulla scala (m), se i piedi non poggiano sui pioli")]
        [SerializeField] float scartoScala = 0f;

        public float VelocitaScala => velocitaScala;

        [SyncVar(hook = nameof(QuandoCambiaPosa))]
        Posa posa;

        public Posa Attuale => posa;
        public bool InPiedi => posa == Posa.InPiedi;

        /// <summary>Vero mentre si è sulla scala a pioli (anche prima che il server lo confermi a tutti).</summary>
        public bool SuScala => posa == Posa.Scala || suScalaLocale;

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

        // Scala a pioli, sul PC di chi gioca e (l'animazione) su tutti
        bool suScalaLocale;
        bool vuoleStaccarsi;
        float ultimaYScala;
        float velocitaAnimScala;
        float correzioneScala;      // di quanto abbassare il corpo perché il piede più basso poggi sul piolo
        bool correzioneScalaNota;   // misurata in una salita precedente: si riparte da lì

        // Dove stanno le anche rispetto ai piedi in ogni posa, per questo corpo: si misura la prima volta
        readonly Dictionary<Posa, Vector3> ancheNellaPosa = new Dictionary<Posa, Vector3>();

        // Solo sul server
        Sedile sedile;
        int posto = -1;
        Scala scala;
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
            AnimaScala();
            if (!isLocalPlayer) return;

            // Da sdraiati la telecamera si allontana piano, e torna com'era quando ci si alza
            if (telecamera != null && distanzaNormale > 0f)
            {
                float voluta = posa == Posa.Sdraiato ? distanzaTelecameraSdraiati : distanzaNormale;
                telecamera.CameraDistance = Mathf.MoveTowards(telecamera.CameraDistance, voluta, 4f * Time.deltaTime);
            }

            if (!bloccato || input == null || suScalaLocale) return;
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
            if (suScalaLocale) { vuoleStaccarsi = true; return; } // sulla scala: E fa staccare
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
            bool eraSeduto = posa == Posa.Seduto || posa == Posa.Sdraiato;
            LiberaPosto();
            posa = Posa.InPiedi;
            TargetAlzati(uscita, eraSeduto);
        }

        [Server]
        void LiberaPosto()
        {
            if (sedile != null && posto >= 0) sedile.ServerLibera(posto, netId);
            if (scala != null) scala.ServerLibera(netId);
            scala = null;
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

        // ---------- Scala a pioli ----------

        /// <summary>Riserva la scala e manda il personaggio sui pioli.</summary>
        [Server]
        public bool ServerSaliScala(Scala s)
        {
            if (posa != Posa.InPiedi || sedile != null || scala != null || creature.IsDead) return false;
            if (!s.ServerOccupa(netId)) return false;
            scala = s;
            posa = Posa.Scala;
            TargetScala(s);
            return true;
        }

        /// <summary>Chi gioca è sceso dalla scala (in cima, in fondo o lasciandosi andare).</summary>
        [Command]
        void CmdLasciaScala()
        {
            if (posa != Posa.Scala) return;
            LiberaPosto();
            posa = Posa.InPiedi;
        }

        [TargetRpc]
        void TargetScala(Scala s)
        {
            if (s == null) { CmdLasciaScala(); return; }
            Blocca();
            suScalaLocale = true;
            vuoleStaccarsi = false;
            if (creatureAnimator != null) creatureAnimator.ImpostaScala(true);
            if (spostamento != null) StopCoroutine(spostamento);
            spostamento = StartCoroutine(ScalaE(s));
        }

        /// <summary>Sui pioli: W sale, S scende; in cima si passa sul piano, in fondo si torna a terra.</summary>
        IEnumerator ScalaE(Scala s)
        {
            Animator anim = GetComponent<Animator>();
            Transform piedeS = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            Transform piedeD = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightFoot) : null;
            if (!correzioneScalaNota) correzioneScala = 0f;

            float t = s.DallAlto(transform.position) ? s.PiediMassimi : 0f;
            yield return Sposta(s.PiediA(t) + Vector3.up * (scartoScala + correzioneScala), s.Verso, t > 0f ? 0.6f : 0.35f);

            while (suScalaLocale && !creature.IsDead)
            {
                if (input != null && input.jump) { input.jump = false; vuoleStaccarsi = true; }
                if (vuoleStaccarsi)
                {
                    vuoleStaccarsi = false;
                    IniziaUscitaScala();
                    if (t < 0.4f) yield return Sposta(s.UscitaBassa, s.Verso, 0.35f);
                    // ci si lascia andare: il corpo resta dov'era (si toglie l'abbassamento della scala), poi si cade
                    else yield return Sposta(transform.position + s.Fuori * 0.35f - Vector3.up * correzioneScala, s.Verso, 0.25f);
                    break;
                }

                float dir = input != null ? input.move.y : 0f;
                float passo = Mathf.Abs(dir) > 0.2f ? Mathf.Sign(dir) * velocitaScala * Time.deltaTime : 0f;

                // In cima, salendo ancora: ci si tira su e si passa sul piano
                if (passo > 0f && t >= s.PiediMassimi - 0.001f && s.UscitaAlta(out Vector3 cima))
                {
                    IniziaUscitaScala();
                    Vector3 qui = transform.position;
                    yield return Sposta(new Vector3(qui.x, cima.y + 0.05f, qui.z), s.Verso, 0.45f);
                    yield return Sposta(cima + Vector3.up * 0.02f, s.Verso, 0.4f);
                    break;
                }
                // In fondo, scendendo ancora: piedi a terra
                if (passo < 0f && t <= 0.001f)
                {
                    IniziaUscitaScala();
                    yield return Sposta(s.UscitaBassa, s.Verso, 0.35f);
                    break;
                }

                t = Mathf.Clamp(t + passo, 0f, s.PiediMassimi);

                // La clip di Mixamo tiene il corpo più in alto dei piedi del personaggio: si misura il piede più basso
                // e si abbassa il corpo piano piano finché poggia sul piolo (vale per ogni corporatura, si ricorda)
                if (piedeS != null && piedeD != null)
                {
                    float piede = Mathf.Min(piedeS.position.y, piedeD.position.y) - transform.position.y;
                    float voluta = -(piede - 0.08f); // l'osso del piede sta circa 8 cm sopra la suola
                    correzioneScala = Mathf.Lerp(correzioneScala, Mathf.Clamp(voluta, -1.5f, 0.5f), Time.deltaTime * 2f);
                    correzioneScalaNota = true;
                }
                transform.SetPositionAndRotation(s.PiediA(t) + Vector3.up * (scartoScala + correzioneScala), s.Verso);
                yield return null;
            }
            FineScala();
        }

        /// <summary>
        /// Si sta per scendere dalla scala: l'animazione dei pioli lascia il posto a quella in piedi mentre il
        /// personaggio si sposta, così il corpo (che sulla scala è disegnato più in alto dei piedi) non fa su e giù.
        /// Anche il server lo sa subito, così gli altri lo vedono scendere nello stesso momento.
        /// </summary>
        void IniziaUscitaScala()
        {
            if (creatureAnimator != null) creatureAnimator.ImpostaScala(false);
            CmdLasciaScala();
        }

        void FineScala()
        {
            if (!suScalaLocale) return;
            suScalaLocale = false;
            if (creatureAnimator != null) creatureAnimator.ImpostaScala(false);
            Sblocca();
            CmdLasciaScala();
        }

        /// <summary>
        /// Su tutti i PC: la velocità dell'animazione dei pioli segue quanto il personaggio sale o scende
        /// (avanti salendo, all'indietro scendendo, ferma quando si sta fermi).
        /// </summary>
        void AnimaScala()
        {
            if (creatureAnimator == null) return;
            float y = transform.position.y;
            if (!SuScala)
            {
                ultimaYScala = y;
                if (velocitaAnimScala != 0f) { velocitaAnimScala = 0f; creatureAnimator.ImpostaVelocitaScala(0f); }
                return;
            }
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float voluta = Mathf.Clamp((y - ultimaYScala) / dt / Mathf.Max(0.1f, velocitaScala), -1f, 1f);
            ultimaYScala = y;
            velocitaAnimScala = Mathf.MoveTowards(velocitaAnimScala, voluta, 8f * dt);
            creatureAnimator.ImpostaVelocitaScala(velocitaAnimScala);
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
            if (suScalaLocale)
            {
                suScalaLocale = false;
                if (creatureAnimator != null) creatureAnimator.ImpostaScala(false);
            }
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
            if (creatureAnimator == null) return;
            creatureAnimator.ImpostaPosa(adesso == Posa.Seduto, adesso == Posa.Sdraiato);
            // Chi gioca decide da sé quando scende dalla scala: il ritardo del server non lo rimette sui pioli
            if (!(isLocalPlayer && adesso == Posa.Scala && !suScalaLocale))
                creatureAnimator.ImpostaScala(adesso == Posa.Scala);
        }
    }
}