using System;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using Valdorso.Network;
using Valdorso.Server;
using Valdorso.UI;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Una bacheca con i fogli appesi: gli incarichi della Gilda o i proclami del Balivo.
    /// Il server legge gli avvisi dall'archivio (AvvisiStore) e li manda a tutti; ogni PC appende un foglio
    /// per ogni avviso, con il titolo scritto a mano e il sigillo di ceralacca del colore del grado.
    /// Si guarda un foglio e con E si legge; gli incarichi si accettano dal foglio (tasto R) finché ci sono posti:
    /// quando sono pieni, il foglio viene tolto. I fogli ingialliscono man mano che si avvicina la scadenza.
    /// </summary>
    public class BachecaAvvisi : Interagibile
    {
        public enum TipoBacheca { Incarichi, Proclami }

        [Serializable]
        public struct AvvisoRete
        {
            public string id, titolo, firma, testo, ricompensa, creato, scadenza;
            public byte grado, posti, occupati;
        }

        [Header("Bacheca")]
        [SerializeField] TipoBacheca tipo = TipoBacheca.Incarichi;

        [Tooltip("Il nome dell'archivio sul server (file Bacheche/<nome>.json)")]
        [SerializeField] string archivio = "Gilda";

        [Tooltip("Il titolo in cima ai fogli letti (es. Bacheca della Gilda)")]
        [SerializeField] string titoloBacheca = "Bacheca della Gilda";

        [Tooltip("Il centro del pannello su cui si appendono i fogli, nelle coordinate della bacheca")]
        [SerializeField] Vector3 centroPannello = new Vector3(0f, 1.5f, 0.06f);

        [Tooltip("Larghezza e altezza del pannello (m)")]
        [SerializeField] Vector2 misuraPannello = new Vector2(1.6f, 1.1f);

        readonly SyncList<AvvisoRete> avvisi = new SyncList<AvvisoRete>();

        static readonly List<BachecaAvvisi> tutte = new List<BachecaAvvisi>();
        static Material carta, ceralacca;

        readonly List<Transform> fogli = new List<Transform>();
        int guardato = -1;

        const float LarghezzaFoglio = 0.21f, AltezzaFoglio = 0.29f;

        /// <summary>I gradi degli incarichi (da allineare ai ranghi della Gilda quando saranno decisi).</summary>
        public static readonly string[] NomiGradi = { "Comune", "Bronzo", "Argento", "Oro" };

        protected override void Awake()
        {
            base.Awake();
            if (nome == "l'oggetto") nome = "la bacheca";
        }

        protected override void OnEnable() { base.OnEnable(); tutte.Add(this); }
        protected override void OnDisable() { base.OnDisable(); tutte.Remove(this); }

        public override void OnStartServer() => ServerRicarica();

        bool daRifare;

        public override void OnStartClient()
        {
            // Quando la lista cambia, i fogli si rifanno una volta sola, alla fine del fotogramma
            avvisi.OnChange += (op, i, vecchio) => daRifare = true;
            RifaiFogli();
        }

        void LateUpdate()
        {
            if (!daRifare) return;
            daRifare = false;
            RifaiFogli();
        }

        // ---------------------------------------------------------------- lettura

        public override bool Locale => true;

        public override string Azione(GameObject chi)
        {
            guardato = FoglioGuardato();
            if (guardato < 0 || guardato >= avvisi.Count) return "Guarda " + nome;
            return "Leggi: " + avvisi[guardato].titolo;
        }

        /// <summary>
        /// La bacheca si può usare solo guardandola davvero: lo sguardo deve cadere sul pannello (con un po' di margine),
        /// non basta averla di lato mentre si guarda una finestra.
        /// </summary>
        public override bool PuoInteragire(GameObject chi)
        {
            Camera cam = Camera.main;
            if (cam == null) return true; // sul server non c'è telecamera: decidono gli altri controlli
            var piano = new Plane(transform.forward, transform.TransformPoint(centroPannello));
            var raggio = new Ray(cam.transform.position, cam.transform.forward);
            if (!piano.Raycast(raggio, out float d)) return false;
            Vector3 locale = transform.InverseTransformPoint(raggio.GetPoint(d)) - centroPannello;
            const float Margine = 0.35f;
            return Mathf.Abs(locale.x) <= misuraPannello.x / 2f + Margine && Mathf.Abs(locale.y) <= misuraPannello.y / 2f + Margine;
        }

        public override void UsaLocale(GameObject chi)
        {
            if (guardato < 0 || guardato >= avvisi.Count)
            {
                PannelloLettura.Apri(titoloBacheca, tipo == TipoBacheca.Incarichi
                    ? "<i>Nessun incarico, per ora. Ripassa più tardi: la Gilda appende i fogli man mano che arrivano le richieste.</i>"
                    : "<i>Nessun proclama, per ora.</i>", transform);
                return;
            }

            AvvisoRete a = avvisi[guardato];
            string testo = "";
            if (!string.IsNullOrEmpty(a.firma)) testo += $"<i>{a.firma}</i>\n\n";
            testo += a.testo;
            if (tipo == TipoBacheca.Incarichi)
            {
                testo += $"\n\n<b>Grado:</b> {NomiGradi[Mathf.Clamp(a.grado, 0, 3)]}";
                if (!string.IsNullOrEmpty(a.ricompensa)) testo += $"\n<b>Ricompensa:</b> {a.ricompensa}";
                if (a.posti > 0) testo += $"\n<b>Posti:</b> {a.occupati} su {a.posti}";
            }
            if (!string.IsNullOrEmpty(a.scadenza) && DateTime.TryParse(a.scadenza, out DateTime s))
                testo += $"\n<b>Entro il</b> {s.ToLocalTime():d MMMM yyyy}";

            string id = a.id;
            if (tipo == TipoBacheca.Incarichi)
                PannelloLettura.Apri(a.titolo, testo, transform, "Accetta l'incarico", () => CmdAccetta(id));
            else
                PannelloLettura.Apri(a.titolo, testo, transform);
        }

        public override void Interagisci(GameObject chi) { } // leggere è solo locale

        /// <summary>Quale foglio sta al centro dello sguardo: si guarda dove la linea dello schermo incontra il pannello.</summary>
        int FoglioGuardato()
        {
            if (fogli.Count == 0) return -1;
            Camera cam = Camera.main;
            if (cam == null) return 0;
            var piano = new Plane(transform.forward, transform.TransformPoint(centroPannello));
            var raggio = new Ray(cam.transform.position, cam.transform.forward);
            Vector3 punto = piano.Raycast(raggio, out float d) ? raggio.GetPoint(d) : transform.TransformPoint(centroPannello);

            int migliore = 0;
            float dMin = float.MaxValue;
            for (int i = 0; i < fogli.Count; i++)
            {
                if (fogli[i] == null) continue;
                float dd = (fogli[i].position - punto).sqrMagnitude;
                if (dd < dMin) { dMin = dd; migliore = i; }
            }
            return migliore;
        }

        // ---------------------------------------------------------------- server

        /// <summary>Rilegge l'archivio e rimanda gli avvisi visibili (non scaduti, con posti liberi).</summary>
        [Server]
        public void ServerRicarica()
        {
            ArchivioAvvisi arch = AvvisiStore.Carica(archivio);
            avvisi.Clear();
            foreach (AvvisoRecord r in arch.avvisi)
            {
                if (r.Scaduto || r.Pieno) continue;
                avvisi.Add(new AvvisoRete
                {
                    id = r.id,
                    titolo = r.titolo,
                    firma = r.firma,
                    testo = r.testo,
                    ricompensa = r.ricompensa,
                    creato = r.creato,
                    scadenza = r.scadenza,
                    grado = (byte)Mathf.Clamp(r.grado, 0, 3),
                    posti = (byte)Mathf.Clamp(r.posti, 0, 255),
                    occupati = (byte)Mathf.Clamp(r.accettatoDa.Count, 0, 255),
                });
            }
        }

        /// <summary>Per la finestra del server: rilegge tutte le bacheche della scena.</summary>
        public static void ServerRicaricaTutte()
        {
            if (!NetworkServer.active) return;
            foreach (BachecaAvvisi b in tutte) if (b != null) b.ServerRicarica();
        }

        [Command(requiresAuthority = false)]
        void CmdAccetta(string id, NetworkConnectionToClient mittente = null)
        {
            if (tipo != TipoBacheca.Incarichi || mittente == null || mittente.identity == null) return;
            if (Vector3.Distance(mittente.identity.transform.position, Punto) > Distanza + 2f) return;
            if (!ValdorsoNetworkManager.TryGetCharacter(mittente, out string personaggio, out _)) return;

            ArchivioAvvisi arch = AvvisiStore.Carica(archivio);
            AvvisoRecord r = arch.avvisi.Find(x => x.id == id);
            string esito;
            if (r == null || r.Scaduto) esito = "Questo foglio non è più valido.";
            else if (r.accettatoDa.Contains(personaggio)) esito = "Hai già accettato questo incarico.";
            else if (r.Pieno) esito = "Troppo tardi: i posti sono già presi.";
            else
            {
                r.accettatoDa.Add(personaggio);
                AvvisiStore.Salva(archivio, arch);
                esito = r.Pieno
                    ? "Hai accettato l'incarico. Eri l'ultimo: il foglio viene tolto dalla bacheca."
                    : $"Hai accettato l'incarico. Posti: {r.accettatoDa.Count} su {r.posti}.";
                ServerRicarica();
            }
            TargetEsito(mittente, esito);
        }

        [TargetRpc]
        void TargetEsito(NetworkConnection destinatario, string esito) => PannelloLettura.MostraEsito(esito);

        // ---------------------------------------------------------------- i fogli

        void RifaiFogli()
        {
            foreach (Transform f in fogli) if (f != null) Destroy(f.gameObject);
            fogli.Clear();
            if (carta == null) PreparaMateriali();

            // Quanti fogli stanno nel pannello: colonne e righe con un po' di spazio attorno
            int colonne = Mathf.Max(1, Mathf.FloorToInt(misuraPannello.x / (LarghezzaFoglio + 0.06f)));
            int righe = Mathf.Max(1, Mathf.FloorToInt(misuraPannello.y / (AltezzaFoglio + 0.06f)));
            int quanti = Mathf.Min(avvisi.Count, colonne * righe);
            var caso = new System.Random(archivio.GetHashCode());

            for (int i = 0; i < quanti; i++)
            {
                int c = i % colonne, r = i / colonne;
                float x = (c + 0.5f) / colonne * misuraPannello.x - misuraPannello.x / 2f;
                float y = misuraPannello.y / 2f - (r + 0.5f) / righe * misuraPannello.y;
                float storto = (float)(caso.NextDouble() * 8.0 - 4.0); // un po' storti, come appesi a mano
                fogli.Add(CreaFoglio(avvisi[i], centroPannello + new Vector3(x, y, 0.005f), storto));
            }
        }

        Transform CreaFoglio(AvvisoRete a, Vector3 posizioneLocale, float storto)
        {
            var foglio = new GameObject("Foglio: " + a.titolo).transform;
            foglio.SetParent(transform, false);
            foglio.localPosition = posizioneLocale;
            foglio.localRotation = Quaternion.Euler(0f, 0f, storto);

            // La carta (un Quad guarda verso -Z: lo si gira verso chi legge)
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(foglio, false);
            q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            q.transform.localScale = new Vector3(LarghezzaFoglio, AltezzaFoglio, 1f);
            var rq = q.GetComponent<MeshRenderer>();
            rq.sharedMaterial = carta;
            rq.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var blocco = new MaterialPropertyBlock();
            blocco.SetColor("_BaseColor", ColoreCarta(a));
            rq.SetPropertyBlock(blocco);

            // Il titolo scritto a mano, in inchiostro scuro
            var go = new GameObject("Titolo", typeof(TextMeshPro));
            go.transform.SetParent(foglio, false);
            go.transform.localPosition = new Vector3(0f, AltezzaFoglio * 0.18f, 0.002f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var t = go.GetComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = new Vector2(LarghezzaFoglio * 0.86f, AltezzaFoglio * 0.5f);
            if (UIKit.TextFont != null) t.font = UIKit.TextFont;
            t.text = a.titolo;
            t.color = new Color(0.22f, 0.14f, 0.08f);
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.15f;
            t.fontSizeMax = 1.6f; // si adatta: titoli corti grandi, titoli lunghi su due righe
            t.fontStyle = FontStyles.Bold;

            // Il grado (o "Proclama") scritto sopra il sigillo
            var gg = new GameObject("Grado", typeof(TextMeshPro));
            gg.transform.SetParent(foglio, false);
            gg.transform.localPosition = new Vector3(0f, -AltezzaFoglio * 0.13f, 0.002f);
            gg.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tg = gg.GetComponent<TextMeshPro>();
            tg.rectTransform.sizeDelta = new Vector2(LarghezzaFoglio * 0.8f, AltezzaFoglio * 0.12f);
            if (UIKit.TextFont != null) tg.font = UIKit.TextFont;
            tg.text = tipo == TipoBacheca.Incarichi ? "Grado: " + NomiGradi[Mathf.Clamp(a.grado, 0, 3)] : "Proclama";
            tg.color = new Color(0.30f, 0.20f, 0.12f);
            tg.alignment = TextAlignmentOptions.Center;
            tg.fontStyle = FontStyles.Italic;
            tg.enableAutoSizing = true;
            tg.fontSizeMin = 0.1f;
            tg.fontSizeMax = 0.9f;

            // Il sigillo di ceralacca in basso
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(foglio, false);
            s.transform.localPosition = new Vector3(0f, -AltezzaFoglio * 0.33f, 0.008f);
            s.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            s.transform.localScale = new Vector3(0.07f, 0.007f, 0.07f); // un bel bottone di ceralacca, in rilievo
            var rs = s.GetComponent<MeshRenderer>();
            rs.sharedMaterial = ceralacca;
            var bs = new MaterialPropertyBlock();
            int grado = tipo == TipoBacheca.Proclami ? 0 : a.grado;
            bs.SetColor("_BaseColor", ColoreSigillo(grado));
            bs.SetFloat("_Metallic", grado >= 2 ? 0.85f : grado == 1 ? 0.6f : 0f); // bronzo, argento e oro luccicano
            rs.SetPropertyBlock(bs);

            return foglio;
        }

        /// <summary>Carta chiara appena appesa, sempre più gialla man mano che si avvicina la scadenza.</summary>
        static Color ColoreCarta(AvvisoRete a)
        {
            var nuova = new Color(0.88f, 0.83f, 0.70f);
            var vecchia = new Color(0.74f, 0.62f, 0.40f);
            float eta = 0f;
            if (DateTime.TryParse(a.creato, out DateTime c))
            {
                if (DateTime.TryParse(a.scadenza, out DateTime s) && s > c)
                    eta = (float)((DateTime.UtcNow - c.ToUniversalTime()).TotalHours / (s - c).TotalHours);
                else
                    eta = (float)((DateTime.UtcNow - c.ToUniversalTime()).TotalDays / 30.0); // senza scadenza: ingiallisce in un mese
            }
            return Color.Lerp(nuova, vecchia, Mathf.Clamp01(eta));
        }

        static Color ColoreSigillo(int grado)
        {
            switch (grado)
            {
                case 1: return new Color(0.80f, 0.45f, 0.18f); // bronzo
                case 2: return new Color(0.88f, 0.90f, 0.95f); // argento
                case 3: return new Color(1.00f, 0.78f, 0.22f); // oro
                default: return new Color(0.75f, 0.06f, 0.05f); // ceralacca rossa
            }
        }

        static void PreparaMateriali()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            carta = new Material(lit) { name = "Carta (bacheca)" };
            carta.SetFloat("_Smoothness", 0.1f);
            ceralacca = new Material(lit) { name = "Ceralacca (bacheca)" };
            ceralacca.SetFloat("_Smoothness", 0.75f);
        }

#if UNITY_EDITOR
        // Nella scena, con la bacheca selezionata: il rettangolo del pannello dove vanno i fogli
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(centroPannello, new Vector3(misuraPannello.x, misuraPannello.y, 0.01f));
        }
#endif
    }
}