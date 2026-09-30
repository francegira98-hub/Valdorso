using System.Reflection;
using Mirror;
using StarterAssets;
using UnityEngine;
using UnityEngine.Rendering;
using Valdorso.Combat;
using Valdorso.Creatures;
using Valdorso.Interazione;
using Valdorso.Movement;
using Valdorso.Stats;
using Valdorso.World;

namespace Valdorso.Movement
{
    /// <summary>
    /// Nuoto in superficie. Va sul prefab del giocatore.
    /// Dove l'acqua è alta (più del petto) il personaggio smette di camminare e nuota a galla, con la testa fuori:
    /// si muove con i soliti tasti, più piano, e con Maiuscolo più svelto. Dove si tocca, si torna a camminare.
    /// Nuotare consuma stamina (stare fermi a galla meno); a stamina finita si annaspa: si va un po' sotto
    /// e si perde salute, finché non si torna a riva o non si riprende fiato.
    /// Il movimento lo fa il PC di chi gioca (usando il ThirdPersonController senza gravità, così la telecamera
    /// resta la solita); il server tiene stamina e salute e dice a tutti chi sta nuotando.
    /// Dove sta l'acqua lo sa AcqueValle (menu Valdorso → Valle → Misura l'acqua).
    /// </summary>
    [DefaultExecutionOrder(-90)] // prima del ThirdPersonController: gli diciamo noi quanto salire o scendere
    [RequireComponent(typeof(Creature))]
    public class Nuoto : NetworkBehaviour
    {
        [Header("Quando si nuota")]
        [Tooltip("Si comincia a nuotare dove l'acqua è più profonda di così (m)")]
        [SerializeField] float profonditaNuoto = 1.35f;
        [Tooltip("Si torna a camminare dove l'acqua è meno profonda di così (m)")]
        [SerializeField] float profonditaCammino = 1.15f;

        [Header("A galla")]
        [Tooltip("Di quanto la testa sta sopra il pelo dell'acqua (m)")]
        [SerializeField] float testaFuori = 0.12f;
        [Tooltip("Da sfiniti la testa va un po' sotto (m sopra il pelo, negativo = sotto)")]
        [SerializeField] float testaSfiniti = -0.05f;

        [Header("Velocità (m/s)")]
        [SerializeField] float velocita = 1.6f;
        [SerializeField] float velocitaSvelta = 2.6f;

        [Header("Stamina e salute (al secondo)")]
        [SerializeField] float staminaFermi = 0.4f;
        [SerializeField] float staminaNuotando = 1.2f;
        [SerializeField] float staminaSvelti = 3f;
        [Tooltip("Salute persa ogni 2 secondi quando si annaspa")]
        [SerializeField] float dannoAnnaspare = 5f;

        public float Velocita => velocita;

        [SyncVar(hook = nameof(QuandoCambiaNuoto))] bool nuota;
        [SyncVar] bool sfinito;

        /// <summary>Vero mentre si nuota.</summary>
        public bool Nuota => nuota || nuotaLocale;

        static readonly FieldInfo velocitaVerticale =
            typeof(ThirdPersonController).GetField("_verticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);

        Creature creature;
        CreatureAnimator creatureAnimator;
        ThirdPersonController mover;
        StarterAssetsInputs input;
        Postura postura;
        Behaviour[] daFermare;
        bool[] eranoAccesi;

        // Sul PC di chi gioca
        bool nuotaLocale;
        float gravitaNormale, velocitaNormale, velocitaSveltaNormale;
        float testaRelativa = -1f; // altezza della testa sopra i piedi nella posa del nuoto, misurata
        Transform testa;

        // Su tutti: per l'animazione
        Vector3 ultimaPosizione;
        float velocitaAnim;

        // Sul server
        float debito;
        float prossimoDanno;
        Vector3 ultimaPosServer;

        void Awake()
        {
            creature = GetComponent<Creature>();
            creatureAnimator = GetComponent<CreatureAnimator>();
            mover = GetComponent<ThirdPersonController>();
            input = GetComponent<StarterAssetsInputs>();
            postura = GetComponent<Postura>();
            daFermare = new Behaviour[] { GetComponent<DodgeRoll>(), GetComponent<MeleeCombat>(), GetComponent<ObstacleTraversal>() };
            eranoAccesi = new bool[daFermare.Length];
        }

        void Update()
        {
            AnimaNuoto();
            if (isLocalPlayer) AggiornaLocale();
            if (isServer) AggiornaServer();
        }

        // ---------- PC di chi gioca ----------

        void AggiornaLocale()
        {
            if (mover == null || AcqueValle.Istanza == null) return;
            if (creature.IsDead || (postura != null && postura.Occupato))
            {
                if (nuotaLocale) Esci();
                return;
            }

            Vector3 p = transform.position;
            bool acqua = AcqueValle.Istanza.Superficie(p, out float pelo);
            float fondo = acqua ? Fondo(p, pelo) : float.MinValue;
            float profondita = acqua ? pelo - fondo : 0f;

            if (!nuotaLocale)
            {
                // Si comincia a nuotare dove l'acqua è alta e il petto è già sotto
                if (acqua && profondita > profonditaNuoto && p.y + 1.1f < pelo) Entra();
                else return;
            }
            else if (!acqua || profondita < profonditaCammino)
            {
                Esci();
                return;
            }

            // A galla: la testa appena fuori dall'acqua (o un po' sotto, da sfiniti)
            if (testa == null)
            {
                Animator anim = GetComponent<Animator>();
                testa = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            }
            if (testa != null)
            {
                float misura = testa.position.y - p.y;
                testaRelativa = testaRelativa < 0f ? misura : Mathf.Lerp(testaRelativa, misura, Time.deltaTime * 2f);
            }
            float sopra = sfinito ? testaSfiniti : testaFuori;
            float voluta = pelo + sopra - (testaRelativa > 0f ? testaRelativa : 1.5f);
            voluta = Mathf.Max(voluta, fondo); // mai sotto il fondo
            float v = Mathf.Clamp((voluta - p.y) / 0.25f, -3f, 3f);
            velocitaVerticale?.SetValue(mover, v);

            if (input != null) input.jump = false;
            mover.SprintSpeed = sfinito ? velocita : velocitaSvelta;
        }

        /// <summary>Il fondo sotto il pelo dell'acqua (terreno, sassi, ponti), senza contare le persone.</summary>
        float Fondo(Vector3 p, float pelo)
        {
            var da = new Vector3(p.x, pelo + 0.5f, p.z);
            int maschera = mover.GroundLayers;
            return Physics.Raycast(da, Vector3.down, out RaycastHit h, 60f, maschera, QueryTriggerInteraction.Ignore)
                ? h.point.y : pelo - 60f;
        }

        void Entra()
        {
            nuotaLocale = true;
            gravitaNormale = mover.Gravity;
            velocitaNormale = mover.MoveSpeed;
            velocitaSveltaNormale = mover.SprintSpeed;
            mover.Gravity = 0f;
            mover.MoveSpeed = velocita;
            mover.SprintSpeed = velocitaSvelta;
            velocitaVerticale?.SetValue(mover, 0f);
            for (int i = 0; i < daFermare.Length; i++)
            {
                eranoAccesi[i] = daFermare[i] != null && daFermare[i].enabled;
                if (daFermare[i] != null) daFermare[i].enabled = false;
            }
            if (creatureAnimator != null) creatureAnimator.ImpostaNuoto(true);
            CmdNuota(true);
        }

        void Esci()
        {
            if (!nuotaLocale) return;
            nuotaLocale = false;
            if (mover != null)
            {
                mover.Gravity = gravitaNormale;
                mover.MoveSpeed = velocitaNormale;
                mover.SprintSpeed = velocitaSveltaNormale;
            }
            for (int i = 0; i < daFermare.Length; i++)
                if (daFermare[i] != null) daFermare[i].enabled = eranoAccesi[i];
            if (creatureAnimator != null) creatureAnimator.ImpostaNuoto(false);
            if (NetworkClient.isConnected) CmdNuota(false);
        }

        public override void OnStartLocalPlayer() => RenderPipelineManager.beginCameraRendering += TelecameraSopraAcqua;

        public override void OnStopLocalPlayer() => RenderPipelineManager.beginCameraRendering -= TelecameraSopraAcqua;

        void OnDestroy() => RenderPipelineManager.beginCameraRendering -= TelecameraSopraAcqua;

        [Tooltip("La telecamera resta almeno così sopra il pelo dell'acqua (m): sotto, l'acqua sembra un velo sottile")]
        [SerializeField] float telecameraSopra = 0.3f;

        /// <summary>
        /// Appena prima di disegnare: se la telecamera di chi gioca è finita sotto il pelo dell'acqua
        /// (camminando nel fiume, guardando dal basso), la si alza fin sopra. Sott'acqua si nuoterà alla v0.7.
        /// </summary>
        void TelecameraSopraAcqua(ScriptableRenderContext contesto, Camera cam)
        {
            if (cam == null || cam != Camera.main || AcqueValle.Istanza == null) return;
            Vector3 p = cam.transform.position;
            if (!AcqueValle.Istanza.Superficie(p, out float pelo)) return;
            if (p.y < pelo + telecameraSopra) cam.transform.position = new Vector3(p.x, pelo + telecameraSopra, p.z);
        }

        public override void OnStartClient()
        {
            // Chi entra nel mondo mentre qualcuno sta già nuotando lo vede nuotare
            if (nuota && creatureAnimator != null) creatureAnimator.ImpostaNuoto(true);
        }

        void OnDisable()
        {
            if (isLocalPlayer) Esci();
        }

        [Command]
        void CmdNuota(bool si)
        {
            nuota = si && !creature.IsDead;
            if (!nuota) { sfinito = false; debito = 0f; }
            ultimaPosServer = transform.position;
            prossimoDanno = Time.time + 2f;
        }

        // ---------- Server ----------

        [Server]
        void AggiornaServer()
        {
            if (!nuota) return;
            if (creature.IsDead) { nuota = false; sfinito = false; return; }

            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            Vector3 spostamento = p - ultimaPosServer;
            spostamento.y = 0f;
            ultimaPosServer = p;
            float vel = dt > 0f ? spostamento.magnitude / dt : 0f;

            float consumo = vel < 0.3f ? staminaFermi : vel > (velocita + velocitaSvelta) * 0.5f ? staminaSvelti : staminaNuotando;
            debito += consumo * dt;
            while (debito >= 1f)
            {
                if (creature.Stats.TrySpend(VitalType.Stamina, 1f)) { debito -= 1f; sfinito = false; }
                else { sfinito = true; debito = 0f; break; }
            }

            if (sfinito && Time.time >= prossimoDanno)
            {
                prossimoDanno = Time.time + 2f;
                creature.ReceiveDamage(DamageInfo.Create(dannoAnnaspare, DamageType.Pure, null, "l'acqua"));
            }
        }

        // ---------- Tutti ----------

        void QuandoCambiaNuoto(bool prima, bool adesso)
        {
            // Chi gioca decide da sé quando esce dall'acqua: il ritardo del server non lo rimette a nuotare
            if (isLocalPlayer && adesso && !nuotaLocale) return;
            if (creatureAnimator != null) creatureAnimator.ImpostaNuoto(adesso);
        }

        /// <summary>La bracciata segue la velocità: ferma a galla (0) o nuotando (1).</summary>
        void AnimaNuoto()
        {
            Vector3 p = transform.position;
            if (!Nuota)
            {
                ultimaPosizione = p;
                if (velocitaAnim != 0f && creatureAnimator != null) { velocitaAnim = 0f; creatureAnimator.ImpostaVelocitaNuoto(0f); }
                return;
            }
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 d = p - ultimaPosizione;
            d.y = 0f;
            ultimaPosizione = p;
            float voluta = Mathf.Clamp01(d.magnitude / dt / Mathf.Max(0.1f, velocita));
            velocitaAnim = Mathf.MoveTowards(velocitaAnim, voluta, 3f * dt);
            if (creatureAnimator != null) creatureAnimator.ImpostaVelocitaNuoto(velocitaAnim);
        }
    }
}