using Mirror;
using UnityEngine;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Panche, sedie, sgabelli, ceppi e letti: con E ci si siede o ci si sdraia.
    /// I posti si calcolano da soli dalla forma dell'oggetto (una panca lunga ne ha due o tre),
    /// il server ricorda chi occupa quale posto. Se l'orientamento non torna con un modello,
    /// si corregge "Verso Y" (di quanti gradi ruotare la direzione in cui si guarda) e "Spostamento".
    /// </summary>
    public class Sedile : Interagibile
    {
        public enum TipoSeduta { Siedi, Sdraiati }

        [Header("Seduta")]
        [SerializeField] TipoSeduta tipo = TipoSeduta.Siedi;

        [Tooltip("La scritta accanto alla E (es. Siediti sulla panca, Sdraiati sul letto)")]
        [SerializeField] string azione = "Siediti";

        [Tooltip("Quanti posti: 0 = li calcola dalla lunghezza (uno ogni Passo metri)")]
        [SerializeField] int postiMassimi = 0;

        [Tooltip("Spazio per persona sulle panche, in metri")]
        [SerializeField] float passo = 0.65f;

        [Tooltip("Direzione in cui si guarda da seduti, in gradi rispetto all'avanti del modello")]
        [SerializeField] float versoY = 0f;

        [Tooltip("Altezza dei piedi del personaggio sopra la base dell'oggetto (0 per sedersi, circa 0.5 per un letto)")]
        [SerializeField] float altezza = 0f;

        [Tooltip("Correzione fine del posto: x a destra, y in alto, z in avanti (rispetto a chi è seduto)")]
        [SerializeField] Vector3 spostamento = new Vector3(0f, 0f, 0.25f);

        [Tooltip("Posti messi a mano (oggetti vuoti: dove vanno i piedi, con l'asse blu verso dove si guarda). " +
                 "Se ce ne sono, si usano questi al posto dei posti calcolati: servono per i modelli con più sedute fuse insieme")]
        [SerializeField] Transform[] postiManuali = new Transform[0];

        readonly SyncList<uint> occupanti = new SyncList<uint>();

        Bounds formaLocale;
        int numeroPosti = 1;

        public TipoSeduta Tipo => tipo;

        /// <summary>
        /// Il punto da guardare è in mezzo ai posti, all'altezza della seduta (non il centro del modello):
        /// così un tavolo con piatti e boccali sopra non "nasconde" le sue panche.
        /// </summary>
        public override Vector3 Punto
        {
            get
            {
                if (numeroPosti <= 0) return base.Punto;
                Vector3 somma = Vector3.zero;
                for (int i = 0; i < numeroPosti; i++)
                {
                    Posto(i, out Vector3 p, out _);
                    somma += p;
                }
                return somma / numeroPosti + Vector3.up * (tipo == TipoSeduta.Sdraiati ? 0.2f : 0.45f);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            formaLocale = MisuraForma();
            Vector3 f = Avanti();
            Vector3 l = Vector3.Cross(Vector3.up, f);
            float lunghezza = Mathf.Abs(l.x) > Mathf.Abs(l.z) ? formaLocale.size.x : formaLocale.size.z;
            numeroPosti = postiManuali.Length > 0 ? postiManuali.Length
                : postiMassimi > 0 ? postiMassimi
                : tipo == TipoSeduta.Sdraiati ? 1
                : Mathf.Max(1, Mathf.FloorToInt(lunghezza / Mathf.Max(0.3f, passo)));
        }

        public override void OnStartServer()
        {
            occupanti.Clear();
            for (int i = 0; i < numeroPosti; i++) occupanti.Add(0);
        }

        public override string Azione(GameObject chi) => azione;

        public override bool PuoInteragire(GameObject chi)
        {
            Postura p = chi.GetComponent<Postura>();
            if (p == null || !p.InPiedi) return false;
            return PostoLiberoPiuVicino(chi.transform.position) >= 0;
        }

        [Server]
        public override void Interagisci(GameObject chi)
        {
            Postura p = chi.GetComponent<Postura>();
            if (p == null) return;
            int i = PostoLiberoPiuVicino(chi.transform.position);
            if (i >= 0) p.ServerSiedi(this, i);
        }

        int PostoLiberoPiuVicino(Vector3 da)
        {
            int migliore = -1;
            float dMin = float.MaxValue;
            for (int i = 0; i < numeroPosti; i++)
            {
                if (i < occupanti.Count && occupanti[i] != 0) continue;
                Posto(i, out Vector3 pos, out _);
                float d = (pos - da).sqrMagnitude;
                if (d < dMin) { dMin = d; migliore = i; }
            }
            return migliore;
        }

        [Server]
        public bool ServerOccupa(int i, uint chi)
        {
            if (i < 0 || i >= occupanti.Count || occupanti[i] != 0) return false;
            occupanti[i] = chi;
            return true;
        }

        [Server]
        public void ServerLibera(int i, uint chi)
        {
            if (i >= 0 && i < occupanti.Count && occupanti[i] == chi) occupanti[i] = 0;
        }

        /// <summary>Dove vanno i piedi del personaggio sul posto i e verso dove guarda.</summary>
        public void Posto(int i, out Vector3 posizione, out Quaternion rotazione)
        {
            if (i >= 0 && i < postiManuali.Length && postiManuali[i] != null)
            {
                Transform m = postiManuali[i];
                rotazione = Quaternion.Euler(0f, m.eulerAngles.y, 0f);
                posizione = m.position + rotazione * spostamento;
                return;
            }

            Vector3 f = Avanti();
            Vector3 l = Vector3.Cross(Vector3.up, f);
            float lunghezza = Mathf.Abs(l.x) > Mathf.Abs(l.z) ? formaLocale.size.x : formaLocale.size.z;
            float laterale = numeroPosti > 1 ? (i + 0.5f) / numeroPosti * lunghezza - lunghezza / 2f : 0f;

            Vector3 c = formaLocale.center;
            Vector3 locale = new Vector3(c.x, formaLocale.min.y + altezza, c.z)
                             + l * (laterale + spostamento.x) + Vector3.up * spostamento.y + f * spostamento.z;
            posizione = transform.TransformPoint(locale);
            rotazione = transform.rotation * Quaternion.LookRotation(f, Vector3.up);
        }

        /// <summary>
        /// Il punto della seduta (o del materasso) sotto il posto i: lo si trova "tastando" dall'alto i collider del mobile.
        /// Serve a Postura per appoggiare il bacino proprio lì, qualunque sia l'animazione e la corporatura.
        /// </summary>
        public Vector3 Superficie(int i)
        {
            Posto(i, out Vector3 pos, out Quaternion rot);
            Vector3 centro = pos - rot * spostamento; // il centro del posto, senza la correzione dei piedi
            var raggio = new Ray(centro + Vector3.up * 3f, Vector3.down);
            float migliore = float.MinValue;
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                if (c.Raycast(raggio, out RaycastHit hit, 5f) && hit.point.y > migliore) migliore = hit.point.y;
            }
            // Nessun collider sotto, oppure un collider "a scatola" che copre anche lo schienale (troppo alto per sedersi):
            // allora si usa un'altezza normale, da terra
            float terra = transform.TransformPoint(formaLocale.min).y;
            float massimo = tipo == TipoSeduta.Sdraiati ? 1.0f : 0.7f;
            if (migliore == float.MinValue || migliore - terra > massimo)
                return new Vector3(centro.x, terra + (tipo == TipoSeduta.Sdraiati ? 0.6f : 0.45f), centro.z);
            return new Vector3(centro.x, migliore, centro.z);
        }

        static readonly Collider[] ingombri = new Collider[16];

        /// <summary>
        /// Dove si sta in piedi accanto al posto i: lì si arriva camminando prima di sedersi, e lì ci si rimette
        /// in piedi alzandosi. Si prova davanti, poi ai lati, poi dietro, e si sceglie il primo punto libero
        /// (niente muri, finestre o mobili): così non si finisce mai dentro una parete.
        /// </summary>
        public Vector3 Uscita(int i)
        {
            Posto(i, out Vector3 pos, out Quaternion rot);
            Vector3 centro = pos - rot * spostamento;
            Vector3 avanti = rot * Vector3.forward, destra = rot * Vector3.right;
            Vector3[] prove = tipo == TipoSeduta.Sdraiati
                ? new[] { destra * 0.85f, -destra * 0.85f, avanti * 1.35f, -avanti * 1.35f }  // si scende di fianco al letto
                : new[] { avanti * 0.45f, destra * 0.6f, -destra * 0.6f, -avanti * 0.6f };   // vicino: meno strada da fare seduti

            foreach (Vector3 p in prove)
            {
                Vector3 punto = centro + p;
                if (!Terra(punto, centro.y, out Vector3 aTerra)) continue;
                if (Libero(aTerra)) return aTerra;
            }
            // Nessun lato libero: davanti, come prima
            Terra(centro + prove[0], centro.y, out Vector3 ripiego);
            return ripiego;
        }

        /// <summary>Il pavimento sotto un punto (senza contare il mobile stesso).</summary>
        bool Terra(Vector3 punto, float quota, out Vector3 aTerra)
        {
            Vector3 alto = new Vector3(punto.x, quota + 1.2f, punto.z);
            RaycastHit[] colpi = Physics.RaycastAll(alto, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            float migliore = float.MinValue;
            foreach (RaycastHit h in colpi)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.point.y > migliore && h.point.y < quota + 0.6f) migliore = h.point.y; // niente davanzali o tavoli alti
            }
            aTerra = new Vector3(punto.x, migliore == float.MinValue ? transform.TransformPoint(formaLocale.min).y : migliore, punto.z);
            return migliore != float.MinValue;
        }

        /// <summary>Vero se una persona in piedi ci sta (una capsula larga 60 cm e alta 1,7 m), senza toccare niente.</summary>
        bool Libero(Vector3 piedi)
        {
            int n = Physics.OverlapCapsuleNonAlloc(piedi + Vector3.up * 0.35f, piedi + Vector3.up * 1.4f, 0.28f,
                ingombri, ~0, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < n; k++)
            {
                Transform t = ingombri[k].transform;
                if (t.IsChildOf(transform)) continue;                            // il mobile stesso (il bordo del letto)
                if (t.GetComponentInParent<Valdorso.Creatures.Creature>() != null) continue; // le persone si spostano
                return false;
            }
            return true;
        }

        Vector3 Avanti() => Quaternion.Euler(0f, versoY, 0f) * Vector3.forward;

        /// <summary>La forma dell'oggetto nelle sue coordinate, misurata dalle mesh (non dipende da come è girato).</summary>
        Bounds MisuraForma()
        {
            bool prima = true;
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds m = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 angolo = m.center + Vector3.Scale(m.extents,
                        new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 p = transform.InverseTransformPoint(mf.transform.TransformPoint(angolo));
                    if (prima) { b = new Bounds(p, Vector3.zero); prima = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

#if UNITY_EDITOR
        // Nella scena, con l'oggetto selezionato: una pallina verde per ogni posto e una freccia per dove si guarda
        void OnDrawGizmosSelected()
        {
            formaLocale = MisuraForma();
            Vector3 f = Avanti();
            Vector3 l = Vector3.Cross(Vector3.up, f);
            float lunghezza = Mathf.Abs(l.x) > Mathf.Abs(l.z) ? formaLocale.size.x : formaLocale.size.z;
            numeroPosti = postiManuali.Length > 0 ? postiManuali.Length
                : postiMassimi > 0 ? postiMassimi : tipo == TipoSeduta.Sdraiati ? 1
                : Mathf.Max(1, Mathf.FloorToInt(lunghezza / Mathf.Max(0.3f, passo)));
            Gizmos.color = Color.green;
            for (int i = 0; i < numeroPosti; i++)
            {
                Posto(i, out Vector3 p, out Quaternion r);
                Gizmos.DrawSphere(p, 0.08f);
                Gizmos.DrawLine(p, p + r * Vector3.forward * 0.5f);
            }
        }
#endif
    }
}