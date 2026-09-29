using Mirror;
using UnityEngine;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Una lanterna, una torcia o un braciere che si accende e si spegne con E.
    /// Lo stato lo tiene il server e lo vedono tutti: se la spegni tu, si spegne anche per gli altri.
    /// Accende e spegne le luci e gli oggetti della fiamma che trova dentro di sé (o quelli indicati).
    /// </summary>
    public class Lanterna : Interagibile
    {
        [Header("Lanterna")]
        [Tooltip("Accesa quando il mondo si apre")]
        [SerializeField] bool accesaAllInizio = true;

        [Tooltip("Le luci da accendere e spegnere. Vuoto = tutte le luci dentro questo oggetto")]
        [SerializeField] Light[] luci;

        [Tooltip("Oggetti visibili solo da accesa (fiamma, particelle, vetro luminoso). Facoltativo")]
        [SerializeField] GameObject[] fiamme;

        [SyncVar(hook = nameof(QuandoCambia))]
        bool accesa;

        protected override void Awake()
        {
            base.Awake();
            if (luci == null || luci.Length == 0) luci = GetComponentsInChildren<Light>(true);
            if (nome == "l'oggetto") nome = "la lanterna";
        }

        public override void OnStartServer()
        {
            accesa = accesaAllInizio;
            Applica(accesa);
        }

        public override void OnStartClient() => Applica(accesa);

        public override string Azione(GameObject chi) => (accesa ? "Spegni " : "Accendi ") + nome;

        [Server]
        public override void Interagisci(GameObject chi)
        {
            accesa = !accesa;
            Applica(accesa); // così cambia anche sul server dedicato (in Host rifarlo due volte non fa danni)
        }

        void QuandoCambia(bool prima, bool adesso) => Applica(adesso);

        void Applica(bool si)
        {
            foreach (Light l in luci) if (l != null) l.enabled = si;
            if (fiamme == null) return;
            foreach (GameObject f in fiamme) if (f != null) f.SetActive(si);
        }
    }
}