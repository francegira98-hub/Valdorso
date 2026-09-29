using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Valdorso.Interazione
{
    /// <summary>
    /// La base di tutto ciò con cui si interagisce premendo E: porte, panche, letti, bacheche, campane, lanterne.
    /// Ogni oggetto dice che cosa si può fare ("Accendi la lanterna"), da quanto lontano e da dove guardarlo.
    /// Il giocatore chiede, il server controlla (distanza, stato) e decide: nessun client cambia il mondo da solo.
    /// Ha bisogno di un NetworkIdentity sullo stesso oggetto o su un oggetto sopra di lui (per esempio sulla casa).
    /// </summary>
    public abstract class Interagibile : NetworkBehaviour
    {
        [Header("Interazione")]
        [Tooltip("Il nome dell'oggetto con l'articolo, per la scritta a schermo (es. la lanterna, la porta)")]
        [SerializeField] protected string nome = "l'oggetto";

        [Tooltip("Distanza massima in metri dal petto del personaggio al punto dell'oggetto")]
        [SerializeField] float distanza = 2.2f;

        [Tooltip("Il punto da guardare per interagire (es. la maniglia). Vuoto = il centro dell'oggetto")]
        [SerializeField] Transform punto;

        static readonly List<Interagibile> tutti = new List<Interagibile>();

        /// <summary>Tutti gli oggetti interagibili attivi in questo momento (sul client: solo quelli già arrivati dal server).</summary>
        public static IReadOnlyList<Interagibile> Tutti => tutti;

        Vector3 centroLocale;

        public float Distanza => distanza;
        public string Nome => nome;

        /// <summary>Il punto del mondo dove sta l'oggetto per chi lo guarda.</summary>
        public Vector3 Punto => punto != null ? punto.position : transform.TransformPoint(centroLocale);

        protected virtual void Awake()
        {
            // Il centro si misura una volta sola: dal collider se c'è, altrimenti dalle mesh visibili.
            Bounds b;
            Collider c = GetComponentInChildren<Collider>();
            Renderer r = GetComponentInChildren<Renderer>();
            if (c != null) b = c.bounds;
            else if (r != null) b = r.bounds;
            else b = new Bounds(transform.position, Vector3.zero);
            centroLocale = transform.InverseTransformPoint(b.center);
        }

        protected virtual void OnEnable() => tutti.Add(this);
        protected virtual void OnDisable() => tutti.Remove(this);

        /// <summary>
        /// La scritta accanto al tasto E (es. "Spegni la lanterna"). La chiede il client di chi guarda,
        /// quindi deve leggere solo dati già sincronizzati.
        /// </summary>
        public abstract string Azione(GameObject chi);

        /// <summary>Se in questo momento chi guarda può usare l'oggetto. Lo chiedono sia il client sia il server.</summary>
        public virtual bool PuoInteragire(GameObject chi) => true;

        /// <summary>
        /// Cosa succede quando qualcuno interagisce. Lo chiama solo il server, dopo i controlli dell'Interattore
        /// (chi lo scrive nelle classi figlie gli mette sopra [Server]).
        /// </summary>
        public abstract void Interagisci(GameObject chi);
    }
}
