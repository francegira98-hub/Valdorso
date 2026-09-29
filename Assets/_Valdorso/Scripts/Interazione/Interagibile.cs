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

        /// <summary>Il centro dell'oggetto nelle sue coordinate (segue l'oggetto quando ruota, come una porta).</summary>
        protected Vector3 CentroLocale => centroLocale;

        public float Distanza => distanza;
        public string Nome => nome;

        /// <summary>Il punto del mondo dove sta l'oggetto per chi lo guarda.</summary>
        public virtual Vector3 Punto => punto != null ? punto.position : transform.TransformPoint(centroLocale);

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

        /// <summary>
        /// Vero per le azioni che riguardano solo chi le fa e non cambiano il mondo (leggere una bacheca):
        /// allora l'Interattore chiama UsaLocale sul PC di chi gioca, senza passare dal server.
        /// </summary>
        public virtual bool Locale => false;

        /// <summary>L'azione solo locale (vedi Locale).</summary>
        public virtual void UsaLocale(GameObject chi) { }

        /// <summary>
        /// Vero se questo collider non deve "nascondere" l'oggetto a chi lo guarda (es. il muro in cui sta una finestra:
        /// il suo collider è una scatola piena, ma dalla stanza la finestra si vede lo stesso).
        /// </summary>
        public virtual bool IgnoraOstacolo(Collider c) => false;

        /// <summary>Se in questo momento chi guarda può usare l'oggetto. Lo chiedono sia il client sia il server.</summary>
        public virtual bool PuoInteragire(GameObject chi) => true;

        /// <summary>
        /// Cosa succede quando qualcuno interagisce. Lo chiama solo il server, dopo i controlli dell'Interattore
        /// (chi lo scrive nelle classi figlie gli mette sopra [Server]).
        /// </summary>
        public abstract void Interagisci(GameObject chi);
    }
}