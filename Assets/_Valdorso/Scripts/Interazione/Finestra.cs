using Mirror;
using UnityEngine;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Una finestra con le sue imposte: con E si aprono o si chiudono tutte insieme, per tutti.
    /// Ogni anta gira intorno al suo perno (il cardine), da "Chiusa Y" ad "Aperta Y" in rotazione locale.
    /// Lo stato lo tiene il server; ogni PC fa girare le ante con dolcezza.
    /// Più avanti: di notte le imposte chiuse non lasciano uscire la luce, e i ladri potranno forzarle.
    /// </summary>
    public class Finestra : Interagibile
    {
        [System.Serializable]
        public class Anta
        {
            [Tooltip("L'anta da far girare (il suo perno deve stare sul cardine)")]
            public Transform perno;
            [Tooltip("Rotazione Y locale dell'anta chiusa")]
            public float chiusaY = 0f;
            [Tooltip("Rotazione Y locale dell'anta aperta")]
            public float apertaY = 100f;
        }

        [Header("Finestra")]
        [SerializeField] Anta[] ante = new Anta[0];

        [Tooltip("Aperta quando il mondo si apre")]
        [SerializeField] bool apertaAllInizio = true;

        [Tooltip("Velocità delle ante in gradi al secondo")]
        [SerializeField] float velocita = 200f;

        [SyncVar] bool aperta;

        protected override void Awake()
        {
            base.Awake();
            if (nome == "l'oggetto") nome = "la finestra";
        }

        public override void OnStartServer()
        {
            aperta = apertaAllInizio;
            Metti(aperta);
        }

        // Chi entra nel mondo trova subito le imposte come sono, senza vederle girare
        public override void OnStartClient() => Metti(aperta);

        void Metti(bool a)
        {
            foreach (Anta anta in ante)
                if (anta.perno != null) anta.perno.localRotation = Rotazione(anta, a);
        }

        static Quaternion Rotazione(Anta anta, bool a) => Quaternion.Euler(0f, a ? anta.apertaY : anta.chiusaY, 0f);

        void Update()
        {
            foreach (Anta anta in ante)
            {
                if (anta.perno == null) continue;
                Quaternion voluta = Rotazione(anta, aperta);
                if (anta.perno.localRotation != voluta)
                    anta.perno.localRotation = Quaternion.RotateTowards(anta.perno.localRotation, voluta, velocita * Time.deltaTime);
            }
        }

        // Il muro in cui sta la finestra non la nasconde: il suo collider è pieno, ma dal vano la finestra si vede
        public override bool IgnoraOstacolo(Collider c) =>
            transform.parent != null && c.transform.IsChildOf(transform.parent);

        public override string Azione(GameObject chi) => (aperta ? "Chiudi " : "Apri ") + nome;

        [Server]
        public override void Interagisci(GameObject chi) => aperta = !aperta;
    }
}