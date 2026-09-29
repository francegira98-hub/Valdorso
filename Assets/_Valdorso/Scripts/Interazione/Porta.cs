using Mirror;
using UnityEngine;
using Valdorso.Creatures;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Una porta vera: con E si apre e si chiude, per tutti. Ruota intorno al suo perno, che nei pezzi del kit
    /// di Hivemind sta sul cardine. Lo stato lo tiene il server; ogni PC fa girare la porta con dolcezza.
    /// Non si chiude addosso a qualcuno: se c'è una persona sulla soglia, la scritta "Chiudi" non compare.
    /// </summary>
    public class Porta : Interagibile
    {
        [Header("Porta")]
        [Tooltip("Rotazione Y (locale) della porta chiusa")]
        [SerializeField] float angoloChiusa = 0f;

        [Tooltip("Rotazione Y (locale) della porta aperta")]
        [SerializeField] float angoloAperta = 90f;

        [Tooltip("Aperta quando il mondo si apre")]
        [SerializeField] bool apertaAllInizio = false;

        [Tooltip("Velocità dell'anta in gradi al secondo")]
        [SerializeField] float velocita = 150f;

        [SyncVar] bool aperta;

        static readonly Collider[] trovati = new Collider[16];

        protected override void Awake()
        {
            base.Awake();
            if (nome == "l'oggetto") nome = "la porta";
        }

        public override void OnStartServer()
        {
            aperta = apertaAllInizio;
            transform.localRotation = Rotazione(aperta);
        }

        // Chi entra nel mondo trova subito la porta com'è, senza vederla girare
        public override void OnStartClient() => transform.localRotation = Rotazione(aperta);

        Quaternion Rotazione(bool a) => Quaternion.Euler(0f, a ? angoloAperta : angoloChiusa, 0f);

        void Update()
        {
            Quaternion voluta = Rotazione(aperta);
            if (transform.localRotation != voluta)
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, voluta, velocita * Time.deltaTime);
        }

        public override string Azione(GameObject chi) => (aperta ? "Chiudi " : "Apri ") + nome;

        public override bool PuoInteragire(GameObject chi) => !(aperta && QualcunoSullaSoglia());

        [Server]
        public override void Interagisci(GameObject chi)
        {
            if (aperta && QualcunoSullaSoglia()) return;
            aperta = !aperta;
        }

        /// <summary>
        /// Vero se nel punto dove starebbe la porta chiusa c'è una persona (giocatore o creatura):
        /// chiudere la porta la chiuderebbe dentro l'anta.
        /// </summary>
        bool QualcunoSullaSoglia()
        {
            // Dal cardine al bordo libero dell'anta, nella posizione da chiusa
            Vector3 versoCentro = CentroLocale;
            versoCentro.y = 0f;
            float larghezza = versoCentro.magnitude * 2f;
            if (larghezza < 0.1f) return false;

            Quaternion base_ = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 direzione = base_ * Rotazione(false) * versoCentro.normalized;
            Vector3 cardine = transform.position + Vector3.up * 0.9f;
            Vector3 bordo = cardine + direzione * larghezza;

            int n = Physics.OverlapCapsuleNonAlloc(cardine + direzione * 0.15f, bordo, 0.35f, trovati,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (trovati[i].transform.IsChildOf(transform)) continue;
                if (trovati[i].GetComponentInParent<Creature>() != null) return true;
            }
            return false;
        }
    }
}