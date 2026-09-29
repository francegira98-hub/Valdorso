using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Valdorso.Creatures;
using Valdorso.UI;

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
        SuggerimentoInterazione suggerimento;
        Interagibile bersaglio;
        double prossimaLocale;
        double prossimaServer;

        Vector3 Petto => transform.position + Vector3.up * altezzaPetto;

        void Awake()
        {
            creature = GetComponent<Creature>();
        }

        public override void OnStartLocalPlayer()
        {
            suggerimento = SuggerimentoInterazione.Crea();
        }

        void OnDisable()
        {
            bersaglio = null;
            if (suggerimento != null) suggerimento.Nascondi();
        }

        void OnDestroy()
        {
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
            CmdInteragisci(bersaglio.netIdentity, bersaglio.ComponentIndex);
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
                Vector3 p = o.Punto;
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

        /// <summary>Vero se tra il petto e l'oggetto c'è qualcos'altro (un muro, il terreno).</summary>
        bool Nascosto(Vector3 da, Vector3 a, Interagibile o)
        {
            if (!Physics.Linecast(da, a, out RaycastHit hit, ostacoli, QueryTriggerInteraction.Ignore)) return false;
            Transform t = hit.collider.transform;
            if (t.IsChildOf(transform)) return false;        // il proprio corpo
            if (t.IsChildOf(o.transform)) return false;      // l'oggetto stesso
            return true;
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

            if (Vector3.Distance(Petto, oggetto.Punto) > oggetto.Distanza + margineServer) return;
            if (!oggetto.PuoInteragire(gameObject)) return;

            prossimaServer = adesso + attesa;
            oggetto.Interagisci(gameObject);
        }
    }
}