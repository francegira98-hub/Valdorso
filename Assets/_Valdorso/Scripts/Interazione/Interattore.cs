using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Valdorso.Creatures;
using Valdorso.UI;
using Valdorso.World;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Va sul prefab del giocatore. Sul PC di chi gioca cerca l'oggetto interagibile più adatto
    /// (vicino, davanti alla telecamera, non dietro un muro), mostra la scritta "E  Accendi la lanterna"
    /// e alla pressione di E chiede al server di usarlo. Il server ricontrolla distanza e stato,
    /// poi fa agire l'oggetto: nessun client può aprire una porta dall'altra parte della valle.
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class Interattore : NetworkBehaviour
    {
        [Tooltip("Quanto largo è lo sguardo: gradi massimi tra il centro dello schermo e l'oggetto")]
        [SerializeField] float angoloMassimo = 55f;

        [Tooltip("Altezza del petto dal piede del personaggio, da dove si misura la distanza")]
        [SerializeField] float altezzaPetto = 1.2f;

        [Tooltip("Tolleranza del server sulla distanza (il personaggio si muove mentre il messaggio viaggia)")]
        [SerializeField] float margineServer = 0.8f;

        [Tooltip("Secondi minimi tra un'interazione e l'altra")]
        [SerializeField] float attesa = 0.3f;

        [Tooltip("Cosa ferma lo sguardo (muri, terreno). Di solito Everything")]
        [SerializeField] LayerMask ostacoli = ~0;

        Creature creature;
        Postura postura;
        SuggerimentoInterazione suggerimento;
        Interagibile bersaglio;
        double prossimaLocale;
        SafeZone luogoAttuale;
        float prossimoControlloLuogo;
        double prossimaServer;

        Vector3 Petto => transform.position + Vector3.up * altezzaPetto;

        void Awake()
        {
            creature = GetComponent<Creature>();
            postura = GetComponent<Postura>();
        }

        public override void OnStartLocalPlayer()
        {
            suggerimento = SuggerimentoInterazione.Crea();
        }

        void OnDisable()
        {
            bersaglio = null;
            if (suggerimento != null) suggerimento.Nascondi();
            if (isLocalPlayer) PannelloLettura.Chiudi();
        }

        void OnDestroy()
        {
            if (isLocalPlayer) PannelloLettura.Chiudi();
            if (suggerimento != null) Destroy(suggerimento.gameObject);
        }

        void Update()
        {
            if (!isLocalPlayer || suggerimento == null) return;

            if (creature.IsDead || PauseMenu.IsPaused || SettingsWindow.IsOpen)
            {
                bersaglio = null;
                suggerimento.Nascondi();
                return;
            }

            ControllaLuogoSicuro();

            // Mentre si legge una bacheca: E chiude, e allontanarsi chiude da solo
            if (PannelloLettura.Aperto)
            {
                bersaglio = null;
                if (PannelloLettura.Fonte == null || Vector3.Distance(Petto, PannelloLettura.Fonte.position) > 4f)
                {
                    PannelloLettura.Chiudi();
                    return;
                }
                suggerimento.Nascondi(); // i tasti li mostra il foglio stesso, in fondo
                Keyboard k = Keyboard.current;
                if (k != null && k.rKey.wasPressedThisFrame && PannelloLettura.HaAzione)
                    PannelloLettura.EseguiAzione();
                else if (k != null && k.eKey.wasPressedThisFrame)
                {
                    prossimaLocale = Time.timeAsDouble + attesa;
                    PannelloLettura.Chiudi();
                }
                return;
            }

            // Da seduti o sdraiati (o mentre si va verso il posto) l'unica cosa da fare con E è alzarsi
            if (postura != null && postura.Occupato)
            {
                bersaglio = null;
                suggerimento.Mostra(postura.SuScala ? "Lasciati andare" : "Alzati");
                Keyboard tastiera = Keyboard.current;
                if (tastiera != null && tastiera.eKey.wasPressedThisFrame && Time.timeAsDouble >= prossimaLocale)
                {
                    prossimaLocale = Time.timeAsDouble + attesa;
                    postura.ChiediDiAlzarti();
                }
                return;
            }

            bersaglio = Scegli();
            if (bersaglio == null)
            {
                suggerimento.Nascondi();
                return;
            }
            suggerimento.Mostra(bersaglio.Azione(gameObject));

            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.eKey.wasPressedThisFrame) return;
            if (Time.timeAsDouble < prossimaLocale) return;

            prossimaLocale = Time.timeAsDouble + attesa;
            if (bersaglio.Locale) bersaglio.UsaLocale(gameObject);
            else CmdInteragisci(bersaglio.netIdentity, bersaglio.ComponentIndex);
        }

        /// <summary>Quattro volte al secondo: se si entra o si esce da un luogo sicuro, lo dice in alto sullo schermo.</summary>
        void ControllaLuogoSicuro()
        {
            if (Time.time < prossimoControlloLuogo) return;
            prossimoControlloLuogo = Time.time + 0.25f;
            SafeZone qui = SafeZone.At(transform.position);
            if (qui == luogoAttuale) return;
            if (qui != null) suggerimento.Avviso("Al sicuro: " + qui.PlaceName, "Da qui si lascia la valle subito, senza attesa");
            else if (luogoAttuale != null) suggerimento.Avviso("Lasci " + luogoAttuale.PlaceName, "");
            luogoAttuale = qui;
        }

        /// <summary>
        /// L'oggetto migliore tra quelli a portata: conta soprattutto quanto è vicino al centro dello schermo,
        /// poi quanto è vicino al personaggio.
        /// </summary>
        Interagibile Scegli()
        {
            Camera cam = Camera.main;
            if (cam == null) return null;

            Vector3 petto = Petto;
            Interagibile migliore = null;
            float punteggioMigliore = float.MaxValue;

            foreach (Interagibile o in Interagibile.Tutti)
            {
                if (o == null) continue;
                Vector3 p = o.PuntoPer(petto);
                float d = Vector3.Distance(petto, p);
                if (d > o.Distanza) continue;

                float angolo = Vector3.Angle(cam.transform.forward, p - cam.transform.position);
                if (angolo > angoloMassimo) continue;
                if (!o.PuoInteragire(gameObject)) continue;
                if (Nascosto(petto, p, o)) continue;

                float punteggio = angolo + d * 8f;
                if (punteggio < punteggioMigliore)
                {
                    punteggioMigliore = punteggio;
                    migliore = o;
                }
            }
            return migliore;
        }

        static readonly RaycastHit[] colpi = new RaycastHit[16];

        /// <summary>
        /// Vero se tra il petto e l'oggetto c'è qualcosa di grande (un muro, il terreno, un armadio).
        /// Gli oggetti piccoli, come piatti, boccali e candele appoggiati su un tavolo, non nascondono niente.
        /// </summary>
        bool Nascosto(Vector3 da, Vector3 a, Interagibile o)
        {
            Vector3 dir = a - da;
            float lunghezza = dir.magnitude;
            if (lunghezza < 0.01f) return false;
            int n = Physics.RaycastNonAlloc(da, dir / lunghezza, colpi, lunghezza, ostacoli, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = colpi[i].collider;
                Transform t = c.transform;
                if (t.IsChildOf(transform)) continue;       // il proprio corpo
                if (t.IsChildOf(o.transform)) continue;     // l'oggetto stesso
                if (o.IgnoraOstacolo(c)) continue;          // ciò che l'oggetto dice di ignorare (il muro della finestra)
                if (c.bounds.size.magnitude < 0.6f) continue; // un oggetto piccolo appoggiato
                return true;
            }
            return false;
        }

        [Command]
        void CmdInteragisci(NetworkIdentity identita, byte indice)
        {
            double adesso = Time.timeAsDouble;
            if (adesso < prossimaServer - 0.05) return;
            if (creature.IsDead || identita == null) return;

            NetworkBehaviour[] componenti = identita.NetworkBehaviours;
            if (componenti == null || indice >= componenti.Length) return;
            if (!(componenti[indice] is Interagibile oggetto) || !oggetto.isActiveAndEnabled) return;

            if (Vector3.Distance(Petto, oggetto.PuntoPer(Petto)) > oggetto.Distanza + margineServer) return;
            if (!oggetto.PuoInteragire(gameObject)) return;

            prossimaServer = adesso + attesa;
            oggetto.Interagisci(gameObject);
        }
    }
}