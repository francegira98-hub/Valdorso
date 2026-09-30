using Mirror;
using UnityEngine;
using Valdorso.Creatures;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Una scala a pioli: con E ci si aggancia, W sale, S scende, E o Spazio fanno staccare.
    /// In cima si scende da soli sul piano (se c'è), in fondo si rimettono i piedi a terra.
    /// Si può anche prendere dall'alto: guardandola dal soppalco la scritta diventa "Scendi dalla scala".
    /// Una persona alla volta. La forma (piede, cima, lato da cui si sale) la misura "Rendi vivo il villaggio"
    /// leggendo il modello; se non torna, si corregge qui a mano.
    /// Il movimento sui pioli lo fa Postura, sul PC di chi gioca.
    /// </summary>
    public class Scala : Interagibile
    {
        [Header("Scala (misurata da Rendi vivo il villaggio)")]
        [Tooltip("Il piede della scala, al centro tra i montanti, nelle coordinate dell'oggetto")]
        [SerializeField] Vector3 bassoLocale;
        [Tooltip("La cima della scala, al centro tra i montanti")]
        [SerializeField] Vector3 altoLocale = Vector3.up * 3f;
        [Tooltip("Il lato da cui si sale (orizzontale): chi sale guarda nel verso opposto")]
        [SerializeField] Vector3 fuoriLocale = Vector3.forward;

        [Header("Chi sale")]
        [Tooltip("Distanza dei piedi dal centro della scala, verso fuori (m)")]
        [SerializeField] float distanzaPiedi = 0.3f;
        [Tooltip("Quanto sotto la cima si fermano i piedi (m lungo la scala): le mani restano sui pioli")]
        [SerializeField] float margineCima = 1.0f;

        [SyncVar] uint occupante;

        bool uscitaMisurata;
        bool haUscitaAlta;
        Vector3 uscitaAlta;

        public Vector3 Basso => transform.TransformPoint(bassoLocale);
        public Vector3 Alto => transform.TransformPoint(altoLocale);
        public Vector3 Asse => (Alto - Basso).normalized;
        public float Lunghezza => Vector3.Distance(Basso, Alto);

        /// <summary>Verso fuori, orizzontale.</summary>
        public Vector3 Fuori
        {
            get
            {
                Vector3 f = transform.TransformDirection(fuoriLocale);
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
            }
        }

        /// <summary>Fin dove arrivano i piedi, in metri lungo la scala.</summary>
        public float PiediMassimi => Mathf.Max(0f, Lunghezza - margineCima);

        /// <summary>Dove stanno i piedi di chi è sulla scala a 't' metri dal piede.</summary>
        public Vector3 PiediA(float t) => Basso + Asse * Mathf.Clamp(t, 0f, PiediMassimi) + Fuori * distanzaPiedi;

        /// <summary>Girati verso la scala.</summary>
        public Quaternion Verso => Quaternion.LookRotation(-Fuori, Vector3.up);

        /// <summary>Dove si rimettono i piedi a terra scendendo dal basso.</summary>
        public Vector3 UscitaBassa
        {
            get
            {
                Vector3 p = Basso + Fuori * (distanzaPiedi + 0.35f);
                return Terreno(p + Vector3.up * 1.2f, 2.5f, out Vector3 t) ? t : p;
            }
        }

        /// <summary>Il pavimento in cima (il soppalco), se c'è.</summary>
        public bool UscitaAlta(out Vector3 punto)
        {
            if (!uscitaMisurata) MisuraUscitaAlta();
            punto = uscitaAlta;
            return haUscitaAlta;
        }

        void MisuraUscitaAlta()
        {
            uscitaMisurata = true;
            haUscitaAlta = false;
            Vector3 alto = Alto;
            Vector3 da = alto - Fuori * 0.7f;
            da.y = alto.y + 0.5f;
            // Il pavimento più alto sotto la cima: in piano, almeno 20 cm sotto la cima (la scala sporge),
            // non più di 1,8 m sotto (niente pavimento del piano terra), senza contare la scala e le persone
            float migliore = float.MinValue;
            foreach (RaycastHit h in Physics.RaycastAll(da, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<Creature>() != null) continue;
                if (h.normal.y < 0.7f) continue;
                if (h.point.y < alto.y - 1.8f || h.point.y > alto.y - 0.2f) continue;
                if (h.point.y > migliore) { migliore = h.point.y; uscitaAlta = h.point; haUscitaAlta = true; }
            }
        }

        /// <summary>Il primo pavimento sotto un punto, senza contare la scala stessa e le persone.</summary>
        bool Terreno(Vector3 da, float lunghezza, out Vector3 punto)
        {
            punto = da;
            RaycastHit[] colpi = Physics.RaycastAll(da, Vector3.down, lunghezza, ~0, QueryTriggerInteraction.Ignore);
            float migliore = float.MinValue;
            foreach (RaycastHit h in colpi)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<Creature>() != null) continue;
                if (h.point.y > migliore) { migliore = h.point.y; punto = h.point; }
            }
            return migliore > float.MinValue;
        }

        /// <summary>Vero se chi guarda è più vicino alla cima che al piede (sta sul soppalco).</summary>
        public bool DallAlto(Vector3 piedi)
        {
            if (!UscitaAlta(out Vector3 su)) return false;
            return (piedi - su).sqrMagnitude < (piedi - Basso).sqrMagnitude;
        }

        /// <summary>Il punto della scala più vicino al petto: così la si prende sia dal basso sia dall'alto.</summary>
        public override Vector3 PuntoPer(Vector3 petto)
        {
            Vector3 a = Basso, b = Alto;
            float t = Vector3.Dot(petto - a, (b - a).normalized);
            return a + (b - a).normalized * Mathf.Clamp(t, 0.3f, Lunghezza - 0.2f);
        }

        public override string Azione(GameObject chi) =>
            DallAlto(chi.transform.position) ? "Scendi dalla scala" : "Sali sulla scala";

        public override bool PuoInteragire(GameObject chi)
        {
            if (occupante != 0) return false;
            Postura p = chi.GetComponent<Postura>();
            return p != null && p.InPiedi && !p.Occupato;
        }

        [Server]
        public override void Interagisci(GameObject chi)
        {
            Postura p = chi.GetComponent<Postura>();
            if (p != null) p.ServerSaliScala(this);
        }

        /// <summary>Nella scena, con la scala selezionata: la linea dei pioli, il lato da cui si sale, dove vanno i piedi.</summary>
        void OnDrawGizmosSelected()
        {
            Vector3 b = Basso, a = Alto, f = Fuori;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(b, a);                               // la scala
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(b + Vector3.up, b + Vector3.up + f); // da che parte si sale
            Gizmos.DrawSphere(b + Vector3.up + f, 0.06f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(PiediA(0f), 0.12f);             // piedi in basso
            Gizmos.DrawWireSphere(PiediA(PiediMassimi), 0.12f);   // piedi in cima
            if (Application.isPlaying && UscitaAlta(out Vector3 su))
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(su, 0.2f);                  // dove si scende sul soppalco
            }
        }

        [Server]
        public bool ServerOccupa(uint chi)
        {
            if (occupante != 0 && occupante != chi) return false;
            occupante = chi;
            return true;
        }

        [Server]
        public void ServerLibera(uint chi)
        {
            if (occupante == chi) occupante = 0;
        }
    }
}