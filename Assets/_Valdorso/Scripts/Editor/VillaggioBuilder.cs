using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Costruisce il villaggio della valle dalla PiantaVillaggio: spiana i lotti con un raccordo morbido,
    /// dipinge piazza (ciottoli), strade e aie (terra battuta), toglie l'erba sotto edifici e strade,
    /// e monta gli edifici con il kit modulare di Hivemind (regole dei perni verificate con le case di prova).
    /// Menu: Valdorso → Villaggio → Costruisci il villaggio, con la scena Valle aperta. Si può rilanciare:
    /// gli edifici vengono tolti e rifatti, spianatura e colori tornano uguali.
    /// Modifica le tessere del terreno: se qualcosa va storto, si tornano indietro con Git.
    /// </summary>
    public static class VillaggioBuilder
    {
        const string CartellaKit = "Assets/HIVEMIND";
        const float Modulo = 6f;
        const float AltezzaPiano = 2.5f;
        const float Raccordo = 5f;        // metri di pendio morbido intorno a un lotto spianato
        // Area toccata dallo strumento, in coordinate del villaggio: villaggio, mulino, innesti delle strade e ponte
        static readonly Rect AreaLavoro = Rect.MinMaxRect(-180f, -180f, 180f, 290f);
        static readonly Vector2 CentroPonte = new Vector2(875f, 700f);  // nel mondo
        const float RaggioPonte = 30f;     // intorno al ponte si cancellano le vecchie strade (restano i raccordi nuovi)
        const float RotPonte = 104f;       // di traverso alla corrente
        const float PianoPonte = 4.35f;    // quanto stanno le tavole sopra il perno del ponte
        const float AlzaPonte = 0f;        // di quanto le tavole stanno sopra la riva più bassa (si può ritoccare)
        const float DistanzaRiva = 12.5f;  // dove si misurano le due rive, dal centro del ponte lungo il suo asse

        // Misurati a ogni costruzione: altezza delle tavole (uguale alla riva più bassa + AlzaPonte) e perno del ponte
        static float pianoTavole, pernoPonte;
        const float RaggioCancella = 128f; // entro questa distanza si cancellano vecchie strade e vecchia macchia di terra

        /// <summary>
        /// Le strade della prima mappa della valle, già dipinte sul terreno da Crea la valle: vicino al villaggio
        /// vengono cancellate (tornano prato) e sostituite da quelle della pianta. Coordinate del mondo (est, nord).
        /// </summary>
        static readonly Vector2[][] StradeVecchie =
        {
            new[] { new Vector2(1150, 270), new Vector2(900, 420) },
            new[] { new Vector2(830, 470), new Vector2(880, 575), new Vector2(875, 685) },
            new[] { new Vector2(910, 460), new Vector2(1075, 550) },
            new[] { new Vector2(865, 715), new Vector2(800, 830) },
            new[] { new Vector2(855, 705), new Vector2(650, 725) },
        };

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        static readonly List<string> mancanti = new List<string>();

        [MenuItem("Valdorso/Villaggio/Costruisci il villaggio")]
        static void Costruisci()
        {
            GameObject valle = GameObject.Find("/Valle"); // la barra: solo l'oggetto Valle in cima alla scena, non i gruppi omonimi (Natura/Rocce/Valle)
            Terrain[] terreni = Terrain.activeTerrains;
            if (valle == null || terreni.Length == 0)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Valle (Assets/_Valdorso/Scenes/Valle).", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Valdorso",
                "Costruire il villaggio?\n\nSpiana i lotti, dipinge piazza e strade, toglie l'erba sotto edifici e strade " +
                "e monta gli edifici. Gli edifici costruiti prima vengono tolti e rifatti.", "Costruisci", "Annulla"))
                return;

            cache.Clear();
            mancanti.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("Valdorso", "Misuro le quote dei lotti...", 0.05f);
                MisuraPonte();
                var quote = new Dictionary<string, float>();
                foreach (PiantaVillaggio.Lotto l in PiantaVillaggio.Lotti) quote[l.id] = QuotaMedia(l.area);

                EditorUtility.DisplayProgressBar("Valdorso", "Spiano i lotti...", 0.15f);
                foreach (Terrain t in terreni) Spiana(t, quote);

                EditorUtility.DisplayProgressBar("Valdorso", "Dipingo piazza e strade...", 0.45f);
                int dipinte = 0;
                foreach (Terrain t in terreni) if (Dipingi(t)) dipinte++;

                EditorUtility.DisplayProgressBar("Valdorso", "Tolgo l'erba sotto edifici e strade...", 0.65f);
                foreach (Terrain t in terreni) TogliErba(t);

                EditorUtility.DisplayProgressBar("Valdorso", "Monto gli edifici...", 0.8f);
                GameObject villaggio = MontaEdifici(valle.transform, quote, out int pezzi);

                foreach (Terrain t in terreni) EditorUtility.SetDirty(t.terrainData);
                EditorSceneManager.MarkSceneDirty(valle.scene);
                Selection.activeGameObject = villaggio;
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

                if (mancanti.Count > 0)
                    Debug.LogWarning("[Valdorso] Pezzi non trovati in " + CartellaKit + ": " + string.Join(", ", mancanti));
                string rapporto = $"{PiantaVillaggio.Lotti.Length} lotti spianati, colori su {dipinte} tessere, " +
                                  $"{PiantaVillaggio.Edifici.Length} edifici e {PiantaVillaggio.Oggetti.Count} oggetti, il ponte ({pezzi} pezzi).";
                Debug.Log("[Valdorso] Villaggio costruito: " + rapporto);
                EditorUtility.DisplayDialog("Valdorso", "Villaggio costruito.\n" + rapporto + "\n\nRicorda: File → Save Project e Ctrl+S sulla scena.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ terreno

        /// <summary>Quota media del terreno nel lotto (centro e quattro punti interni), in metri.</summary>
        static float QuotaMedia(Rect area)
        {
            Vector2[] punti =
            {
                area.center,
                new Vector2(area.xMin + 1f, area.yMin + 1f), new Vector2(area.xMax - 1f, area.yMin + 1f),
                new Vector2(area.xMin + 1f, area.yMax - 1f), new Vector2(area.xMax - 1f, area.yMax - 1f)
            };
            float somma = 0f;
            foreach (Vector2 p in punti) somma += Altezza(PiantaVillaggio.AlMondo(p));
            return somma / punti.Length;
        }

        static float Altezza(Vector2 mondo)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 pos = t.GetPosition();
                Vector3 dim = t.terrainData.size;
                if (mondo.x >= pos.x && mondo.x <= pos.x + dim.x && mondo.y >= pos.z && mondo.y <= pos.z + dim.z)
                    return pos.y + t.SampleHeight(new Vector3(mondo.x, 0f, mondo.y));
            }
            return 0f;
        }

        /// <summary>Porta i punti di altezza dei lotti alla loro quota, con un raccordo morbido tutto intorno.</summary>
        static void Spiana(Terrain t, Dictionary<string, float> quote)
        {
            TerrainData d = t.terrainData;
            int ris = d.heightmapResolution;
            if (!Zona(t, ris - 1, 0f, out int x0, out int y0, out int w, out int h)) return;

            float[,] alt = d.GetHeights(x0, y0, w, h);
            Vector3 pos = t.GetPosition();
            Vector3 dim = d.size;
            bool cambiato = false;

            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    Vector2 mondo = new Vector2(pos.x + (x0 + i) / (float)(ris - 1) * dim.x, pos.z + (y0 + j) / (float)(ris - 1) * dim.z);
                    Vector2 p = mondo - ValleMappa.Villaggio;

                    // Testate del ponte: il terreno alle due estremità si porta all'altezza delle tavole
                    float pesoPonte = PesoTestate(mondo, out bool est);
                    if (pesoPonte > 0f)
                    {
                        alt[j, i] = Mathf.Lerp(alt[j, i], (pianoTavole - pos.y) / dim.y, pesoPonte);
                        cambiato = true;
                    }

                    foreach (PiantaVillaggio.Lotto l in PiantaVillaggio.Lotti)
                    {
                        float dist = DistanzaRett(p, l.area);
                        if (dist >= Raccordo) continue;
                        float peso = 1f - Liscio(dist / Raccordo);
                        float obiettivo = (quote[l.id] - pos.y) / dim.y;
                        alt[j, i] = Mathf.Lerp(alt[j, i], obiettivo, peso);
                        cambiato = true;
                    }
                }
            if (cambiato) d.SetHeights(x0, y0, alt);
        }

        /// <summary>
        /// Misura le due rive a DistanzaRiva dal centro del ponte: le tavole vanno all'altezza della riva più bassa
        /// (+ AlzaPonte), il perno del ponte PianoPonte sotto, e le testate portano la riva più alta alla stessa altezza.
        /// Rilanciando si rimisura sulle testate già fatte, quindi il ponte resta dov'è.
        /// </summary>
        static void MisuraPonte()
        {
            Vector2 asse = new Vector2(Mathf.Sin(RotPonte * Mathf.Deg2Rad), Mathf.Cos(RotPonte * Mathf.Deg2Rad));
            float est = Altezza(CentroPonte + asse * DistanzaRiva), ovest = Altezza(CentroPonte - asse * DistanzaRiva);
            pianoTavole = Mathf.Min(est, ovest) + AlzaPonte;
            pernoPonte = pianoTavole - PianoPonte;
            Debug.Log($"[Valdorso] Ponte: riva est {est:0.00} m, riva ovest {ovest:0.00} m, tavole a {pianoTavole:0.00} m, perno a {pernoPonte:0.00} m.");
        }

        /// <summary>
        /// Quanto un punto del mondo appartiene alle testate del ponte (0-1): due rampe lungo l'asse del ponte,
        /// da 6,5 a 11 m dal centro, larghe 7 m, che sfumano in 10 m verso la strada e in 7 m ai lati
        /// (pendii dolci, non muri di terra). Verso l'acqua non si tocca nulla, così le rive restano come sono.
        /// est = la testata verso est (quella del villaggio).
        /// </summary>
        static float PesoTestate(Vector2 mondo, out bool est)
        {
            Vector2 d = mondo - CentroPonte;
            Vector2 asse = new Vector2(Mathf.Sin(RotPonte * Mathf.Deg2Rad), Mathf.Cos(RotPonte * Mathf.Deg2Rad));
            Vector2 lato = new Vector2(asse.y, -asse.x);
            float lungo = Vector2.Dot(d, asse);
            est = lungo > 0f;
            float u = Mathf.Abs(lungo), v = Mathf.Abs(Vector2.Dot(d, lato));
            if (u < 6.5f) return 0f;
            float pu = u <= 11f ? 1f : 1f - Liscio((u - 11f) / 10f);
            float pv = v <= 3.5f ? 1f : 1f - Liscio((v - 3.5f) / 7f);
            return pu * pv;
        }

        /// <summary>Piazza a ciottoli, strade e aie in terra battuta, terra sotto gli edifici.</summary>
        static bool Dipingi(Terrain t)
        {
            TerrainData d = t.terrainData;
            int terra = IndiceLayer(d, "Valle_Terra"), ciottoli = IndiceLayer(d, "Valle_Ciottoli"), prato = IndiceLayer(d, "Valle_Prato");
            if (terra < 0) return false;
            int ris = d.alphamapResolution;
            if (!Zona(t, ris, 0.5f, out int x0, out int y0, out int w, out int h)) return false;

            float[,,] a = d.GetAlphamaps(x0, y0, w, h);
            Vector3 pos = t.GetPosition();
            Vector3 dim = d.size;
            int strati = d.alphamapLayers;
            var obiettivo = new float[strati];

            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    Vector2 mondo = new Vector2(pos.x + (x0 + i + 0.5f) / ris * dim.x, pos.z + (y0 + j + 0.5f) / ris * dim.z);
                    Vector2 p = mondo - ValleMappa.Villaggio;

                    // Prima si cancellano vecchie strade e vecchia macchia di terra intorno al centro (tornano prato)
                    float cancella = 0f;
                    float dCentro = p.magnitude;
                    if (prato >= 0 && dCentro < RaggioCancella)
                    {
                        foreach (Vector2[] vecchia in StradeVecchie)
                            cancella = Mathf.Max(cancella, 1f - Liscio((DistanzaLinea(mondo, vecchia) - 1.8f) / 9f));
                        cancella = Mathf.Max(cancella, 1f - Liscio((dCentro - 35f) / 30f));
                        cancella *= 1f - Liscio((dCentro - (RaggioCancella - 18f)) / 18f);
                    }
                    float dPonte = Vector2.Distance(mondo, CentroPonte);
                    if (prato >= 0 && dPonte < RaggioPonte)
                    {
                        float vecchie = 0f;
                        foreach (Vector2[] vecchia in StradeVecchie)
                            vecchie = Mathf.Max(vecchie, 1f - Liscio((DistanzaLinea(mondo, vecchia) - 1.8f) / 9f));
                        cancella = Mathf.Max(cancella, vecchie * (1f - Liscio((dPonte - (RaggioPonte - 10f)) / 10f)));
                    }

                    float quantoTerra = 0f, quantoCiottoli = 0f;

                    foreach (PiantaVillaggio.Lotto l in PiantaVillaggio.Lotti)
                    {
                        float dist = DistanzaRett(p, l.area);
                        if (l.tipo == PiantaVillaggio.Tipo.Piazza)
                            quantoCiottoli = Mathf.Max(quantoCiottoli, 1f - Liscio((dist + 1.5f) / 3f));
                        else if (l.tipo == PiantaVillaggio.Tipo.Fabbro || l.tipo == PiantaVillaggio.Tipo.Addestramento ||
                                 l.tipo == PiantaVillaggio.Tipo.Orti || l.tipo == PiantaVillaggio.Tipo.Fienile)
                            quantoTerra = Mathf.Max(quantoTerra, 0.7f * (1f - Liscio(dist / 2f)));
                    }
                    foreach (PiantaVillaggio.Edificio e in PiantaVillaggio.Edifici)
                        quantoTerra = Mathf.Max(quantoTerra, 1f - Liscio((DistanzaRett(p, e.Impronta) - 1f) / 2f));
                    foreach (var strada in PiantaVillaggio.Strade)
                    {
                        float dist = DistanzaLinea(p, strada.punti);
                        quantoTerra = Mathf.Max(quantoTerra, 0.9f * (1f - Liscio((dist - strada.larghezza * 0.5f + 1f) / 2f)));
                    }

                    if (cancella <= 0f && quantoTerra <= 0f && quantoCiottoli <= 0f) continue;
                    for (int k = 0; k < strati; k++) obiettivo[k] = a[j, i, k];
                    if (prato >= 0) Mescola(obiettivo, prato, cancella);
                    Mescola(obiettivo, terra, quantoTerra);
                    if (ciottoli >= 0) Mescola(obiettivo, ciottoli, quantoCiottoli * 0.75f);
                    for (int k = 0; k < strati; k++) a[j, i, k] = obiettivo[k];
                }
            d.SetAlphamaps(x0, y0, a);
            return true;
        }

        /// <summary>Niente erba sotto edifici, piazza e strade; erba rada nelle aie.</summary>
        static void TogliErba(Terrain t)
        {
            TerrainData d = t.terrainData;
            if (d.detailPrototypes.Length == 0) return;
            int ris = d.detailResolution;
            if (!Zona(t, ris, 0.5f, out int x0, out int y0, out int w, out int h)) return;

            Vector3 pos = t.GetPosition();
            Vector3 dim = d.size;
            var fattore = new float[h, w];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    Vector2 mondo = new Vector2(pos.x + (x0 + i + 0.5f) / ris * dim.x, pos.z + (y0 + j + 0.5f) / ris * dim.z);
                    Vector2 p = mondo - ValleMappa.Villaggio;
                    float f = 1f;
                    foreach (PiantaVillaggio.Lotto l in PiantaVillaggio.Lotti)
                    {
                        if (!l.area.Contains(p)) continue;
                        if (l.tipo == PiantaVillaggio.Tipo.Piazza) f = 0f;
                        else if (l.tipo == PiantaVillaggio.Tipo.Fabbro || l.tipo == PiantaVillaggio.Tipo.Addestramento ||
                                 l.tipo == PiantaVillaggio.Tipo.Orti || l.tipo == PiantaVillaggio.Tipo.Fienile) f = Mathf.Min(f, 0.25f);
                        else f = Mathf.Min(f, 0.6f);
                    }
                    foreach (PiantaVillaggio.Edificio e in PiantaVillaggio.Edifici)
                        if (DistanzaRett(p, e.Impronta) < 1.5f) f = 0f;
                    foreach (var strada in PiantaVillaggio.Strade)
                        if (DistanzaLinea(p, strada.punti) < strada.larghezza * 0.5f + 0.5f) f = 0f;
                    fattore[j, i] = f;
                }

            for (int k = 0; k < d.detailPrototypes.Length; k++)
            {
                int[,] m = d.GetDetailLayer(x0, y0, w, h, k);
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        m[j, i] = Mathf.RoundToInt(m[j, i] * fattore[j, i]);
                d.SetDetailLayer(x0, y0, k, m);
            }
        }

        /// <summary>
        /// La parte della mappa (di altezze, colori o erba) della tessera che cade nel villaggio.
        /// risoluzione = celle per lato (per le altezze: punti - 1); mezzaCella = 0,5 per le mappe a celle.
        /// </summary>
        static bool Zona(Terrain t, int risoluzione, float mezzaCella, out int x0, out int y0, out int w, out int h)
        {
            Vector3 pos = t.GetPosition();
            Vector3 dim = t.terrainData.size;
            Vector2 c = ValleMappa.Villaggio;
            float xMin = c.x + AreaLavoro.xMin, xMax = c.x + AreaLavoro.xMax;
            float zMin = c.y + AreaLavoro.yMin, zMax = c.y + AreaLavoro.yMax;
            int limite = mezzaCella > 0f ? risoluzione - 1 : risoluzione;
            x0 = Mathf.Clamp(Mathf.FloorToInt((xMin - pos.x) / dim.x * risoluzione), 0, limite);
            y0 = Mathf.Clamp(Mathf.FloorToInt((zMin - pos.z) / dim.z * risoluzione), 0, limite);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((xMax - pos.x) / dim.x * risoluzione), 0, limite);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((zMax - pos.z) / dim.z * risoluzione), 0, limite);
            w = x1 - x0 + 1;
            h = y1 - y0 + 1;
            bool dentro = xMax >= pos.x && xMin <= pos.x + dim.x && zMax >= pos.z && zMin <= pos.z + dim.z;
            return dentro && w > 0 && h > 0;
        }

        static int IndiceLayer(TerrainData d, string nome)
        {
            TerrainLayer[] layers = d.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
                if (layers[i] != null && layers[i].name == nome) return i;
            return -1;
        }

        static void Mescola(float[] w, int indice, float quanto)
        {
            if (quanto <= 0f) return;
            quanto = Mathf.Clamp01(quanto);
            for (int k = 0; k < w.Length; k++) w[k] = w[k] * (1f - quanto) + (k == indice ? quanto : 0f);
        }

        // ------------------------------------------------------------------ edifici

        static GameObject MontaEdifici(Transform valle, Dictionary<string, float> quote, out int pezzi)
        {
            // Via ogni vecchio "Villaggio" della scena, ovunque sia (anche avanzi finiti in altri gruppi)
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == "Villaggio" && t.gameObject.scene == valle.gameObject.scene)
                    Object.DestroyImmediate(t.gameObject);

            // Il segnaposto dell'arrivo va davanti alla porta del tempio (x 2, y 20 dal centro del villaggio)
            Transform arrivo = valle.Find("Segnaposti/Arrivo_Tempio");
            if (arrivo != null)
            {
                Vector2 punto = PiantaVillaggio.AlMondo(PiantaVillaggio.ArrivoTempio);
                arrivo.position = new Vector3(punto.x, Altezza(punto) + 0.5f, punto.y);
            }

            var villaggio = new GameObject("Villaggio");
            villaggio.transform.SetParent(valle, false);
            villaggio.transform.position = new Vector3(ValleMappa.Villaggio.x, 0f, ValleMappa.Villaggio.y);

            pezzi = 0;
            foreach (PiantaVillaggio.Edificio e in PiantaVillaggio.Edifici)
            {
                var edificio = new GameObject(e.nome).transform;
                edificio.SetParent(villaggio.transform, false);
                edificio.localPosition = new Vector3(e.angolo.x, quote[e.lotto] + 0.02f, e.angolo.y);
                edificio.localRotation = Quaternion.Euler(0f, e.facciata, 0f);
                bool camino = e.moduli >= 2 && e.lotto != "tempio";
                int soppalco = PiantaVillaggio.ModuliSoppalco(e);
                pezzi += MontaCasa(edificio, e.moduli, e.piani, camino, soppalco);
                pezzi += Arreda(edificio, e, soppalco);
                if (e.lotto.StartsWith("casa")) pezzi += AttorniCasa(villaggio.transform, e);
            }

            foreach (PiantaVillaggio.Oggetto o in PiantaVillaggio.Oggetti)
                if (PosaCentrato(villaggio.transform, o, Altezza(PiantaVillaggio.AlMondo(o.posizione)))) pezzi++;

            if (PosaPonte(villaggio.transform)) pezzi++;

            return villaggio;
        }

        /// <summary>Monta una casa lunga 'moduli' × 6 m e profonda 6 m: porta e finestra grande in facciata (z = 0).</summary>
        static int MontaCasa(Transform casa, int moduli, int piani, bool camino, int soppalco)
        {
            int n = 0;
            float L = moduli * Modulo, P = Modulo;
            for (int p = 0; p < piani; p++)
            {
                float y = p * AltezzaPiano;
                bool terra = p == 0;

                // Pavimento: al primo piano solo sui moduli del soppalco, gli altri restano a doppia altezza
                int conPavimento = terra ? moduli : soppalco;
                for (int i = 0; i < conPavimento; i++)
                    n += Pezzo(casa, "SM_floor6m_a1", new Vector3(i * Modulo, y, 0f), 0f);

                // Facciata (z = 0): al piano terra, al centro, porta e finestra grande
                int moduloPorta = moduli / 2;
                for (int i = 0; i < moduli; i++)
                {
                    float x = i * Modulo;
                    if (terra && i == moduloPorta)
                    {
                        n += Pezzo(casa, "SM_wallDoor2m_a1", new Vector3(x, y, 0f), 0f);
                        n += Pezzo(casa, "SM_DoorA_a1", new Vector3(x + 0.45f, y, 0f), 90f); // porta aperta verso l'interno
                        n += Pezzo(casa, "SM_wallWindow4m_a1", new Vector3(x + 2f, y, 0f), 0f);
                    }
                    else
                    {
                        n += Pezzo(casa, "SM_wallWindow3m_a1", new Vector3(x, y, 0f), 0f);
                        n += Pezzo(casa, "SM_wall3m_a1", new Vector3(x + 3f, y, 0f), 0f);
                    }
                }

                // Retro (z = -6)
                for (int i = 0; i < moduli; i++)
                {
                    float x = i * Modulo;
                    n += Pezzo(casa, "SM_wall3m_a1", new Vector3(x + 3f, y, -P), 180f);
                    n += Pezzo(casa, "SM_wallWindow3m_a1", new Vector3(x + 6f, y, -P), 180f);
                }

                // Fianchi
                n += Pezzo(casa, "SM_wall3m_a1", new Vector3(0f, y, 0f), 90f);
                n += Pezzo(casa, "SM_wallWindow3m_a1", new Vector3(0f, y, -3f), 90f);
                n += Pezzo(casa, "SM_wall3m_a1", new Vector3(L, y, -P), 270f);
                n += Pezzo(casa, "SM_wallWindow3m_a1", new Vector3(L, y, -3f), 270f);

                // Travi d'angolo
                n += Pezzo(casa, "SM_planksCorner_a1", new Vector3(0f, y, 0f), 0f);
                n += Pezzo(casa, "SM_planksCorner_a1", new Vector3(L, y, 0f), 270f);
                n += Pezzo(casa, "SM_planksCorner_a1", new Vector3(L, y, -P), 180f);
                n += Pezzo(casa, "SM_planksCorner_a1", new Vector3(0f, y, -P), 90f);
            }

            float yTetto = piani * AltezzaPiano;

            if (camino) n += Pezzo(casa, "SM_chimneyA_a1", new Vector3(L - 2.5f, yTetto + 2.6f, -3f), 0f);

            if (moduli == 1)
            {
                // Tetto intero a quattro falde: niente timpani, altrimenti spuntano fuori dal tetto
                n += Pezzo(casa, "SM_roofTop6mx6m_a1", new Vector3(0f, yTetto, 0f), 0f);
            }
            else
            {
                n += Pezzo(casa, "SM_wallTopRoof6m_a1", new Vector3(0f, yTetto, 0f), 90f);
                n += Pezzo(casa, "SM_wallTopRoof6m_a1", new Vector3(L, yTetto, -P), 270f);
                n += Pezzo(casa, "SM_roofEnd6m_a1", new Vector3(0f, yTetto, 0f), 0f);
                for (int i = 1; i < moduli - 1; i++)
                    n += Pezzo(casa, "SM_roofMiddle6m_a1", new Vector3(i * Modulo, yTetto, 0f), 0f);
                n += Pezzo(casa, "SM_roofEnd6m_a1", new Vector3(L, yTetto, -P), 180f);
            }
            return n;
        }

        /// <summary>
        /// Gli interni: gli arredi della pianta e, se c'è un soppalco, il parapetto sul bordo e la scala a pioli
        /// per salire (appoggiata al bordo, nel modulo a doppia altezza).
        /// </summary>
        static int Arreda(Transform edificio, PiantaVillaggio.Edificio e, int soppalco)
        {
            int n = 0;
            const float pavimento = 0.03f;   // spessore del pavimento del kit sopra il perno

            foreach (PiantaVillaggio.Arredo ar in PiantaVillaggio.ArrediDi(e))
            {
                var o = new PiantaVillaggio.Oggetto(ar.prefab, ar.x, ar.z, ar.rotY);
                if (PosaCentrato(edificio, o, ar.piano * AltezzaPiano + pavimento + ar.alzata)) n++;
            }

            if (soppalco > 0 && soppalco < e.moduli)
            {
                float bordo = soppalco * Modulo;
                float yPiano = AltezzaPiano + pavimento;
                foreach (float z in new[] { -1.05f, -2.65f, -5.3f })
                    if (PosaCentrato(edificio, new PiantaVillaggio.Oggetto("SM_fenceB_a1", bordo, z, 0f), yPiano)) n++;
                if (PosaCentrato(edificio, new PiantaVillaggio.Oggetto("SM_ladderA_a1", bordo + 0.2f, -4f, 90f), pavimento)) n++;
            }
            return n;
        }

        /// <summary>Intorno a una casa dei coloni: panca accanto alla porta, legna sul fianco, barile e secchio.</summary>
        static int AttorniCasa(Transform villaggio, PiantaVillaggio.Edificio e)
        {
            int n = 0;
            float xPorta = (e.moduli / 2) * Modulo;
            n += PosaQui(villaggio, "SM_benchA_a1", e.AlVillaggio(xPorta + 4f, 0.9f), e.facciata + 90f);
            n += PosaQui(villaggio, "SM_firewoodSetA_a1", e.AlVillaggio(e.Lunghezza + 1f, -3f), e.facciata);
            n += PosaQui(villaggio, "SM_barrelClosedA_a1", e.AlVillaggio(-0.9f, -1.2f), e.facciata);
            n += PosaQui(villaggio, "SM_WoodWaterBucket_UV", e.AlVillaggio(-0.7f, 0.8f), e.facciata);
            return n;
        }

        static int PosaQui(Transform villaggio, string prefab, Vector2 punto, float rotY)
        {
            var o = new PiantaVillaggio.Oggetto(prefab, punto.x, punto.y, rotY);
            return PosaCentrato(villaggio, o, Altezza(PiantaVillaggio.AlMondo(punto))) ? 1 : 0;
        }

        /// <summary>
        /// Il ponte sul fiume a nord del villaggio, lungo 14 m e girato di traverso alla corrente (104°).
        /// Altezza sistemata a mano da Fra il 28/09 (perno a 63,90); le strade arrivano alle sue due teste.
        /// </summary>
        static bool PosaPonte(Transform villaggio)
        {
            Vector2 locale = CentroPonte - ValleMappa.Villaggio;
            var o = new PiantaVillaggio.Oggetto("SM_Bridge_a1", locale.x, locale.y, RotPonte, pernoY: pernoPonte, xzEsatti: true);
            return PosaCentrato(villaggio, o, 0f);
        }

        static int Pezzo(Transform casa, string nomePrefab, Vector3 posizione, float rotY)
        {
            GameObject prefab = Carica(nomePrefab);
            if (prefab == null) return 0;
            var pezzo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, casa);
            pezzo.transform.localPosition = posizione;
            pezzo.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            return 1;
        }

        /// <summary>
        /// Posa un pezzo singolo con il centro della sua base nel punto dato, appoggiato al terreno.
        /// Misura la forma dalle mesh (non dai Renderer), perché alcuni pezzi "fusi" di Hivemind
        /// hanno la forma lontanissima dal loro perno.
        /// </summary>
        static bool PosaCentrato(Transform villaggio, PiantaVillaggio.Oggetto o, float quota)
        {
            GameObject prefab = Carica(o.prefab);
            if (prefab == null) return false;
            var pezzo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, villaggio);
            pezzo.transform.localPosition = new Vector3(o.posizione.x, quota, o.posizione.y);
            pezzo.transform.localRotation = Quaternion.Euler(0f, o.rotY, 0f);

            if (o.PernoEsatto && o.xzEsatti)
            {
                pezzo.transform.localPosition = new Vector3(o.posizione.x, o.pernoY, o.posizione.y);
                return true;
            }

            if (RiquadroMesh(pezzo.transform, out Bounds b))
            {
                Vector3 voluto = villaggio.TransformPoint(new Vector3(o.posizione.x, quota, o.posizione.y));
                pezzo.transform.position += new Vector3(voluto.x - b.center.x, voluto.y - b.min.y, voluto.z - b.center.z);
                if (o.PernoEsatto)
                {
                    Vector3 lp = pezzo.transform.localPosition;
                    pezzo.transform.localPosition = new Vector3(lp.x, o.pernoY, lp.z);
                }
            }
            return true;
        }

        /// <summary>Il riquadro nel mondo che contiene tutte le mesh di un oggetto e dei suoi figli.</summary>
        static bool RiquadroMesh(Transform radice, out Bounds riquadro)
        {
            bool trovato = false;
            riquadro = new Bounds();
            foreach (MeshFilter mf in radice.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds m = mf.sharedMesh.bounds;
                Matrix4x4 mat = mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 angolo = new Vector3(
                        (k & 1) == 0 ? m.min.x : m.max.x,
                        (k & 2) == 0 ? m.min.y : m.max.y,
                        (k & 4) == 0 ? m.min.z : m.max.z);
                    Vector3 p = mat.MultiplyPoint3x4(angolo);
                    if (!trovato) { riquadro = new Bounds(p, Vector3.zero); trovato = true; }
                    else riquadro.Encapsulate(p);
                }
            }
            return trovato;
        }

        static GameObject Carica(string nome)
        {
            if (cache.TryGetValue(nome, out GameObject trovato)) return trovato;
            GameObject prefab = null;
            foreach (string guid in AssetDatabase.FindAssets(nome + " t:Prefab", new[] { CartellaKit }))
            {
                string percorso = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(percorso) != nome) continue;
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(percorso);
                break;
            }
            if (prefab == null && !mancanti.Contains(nome)) mancanti.Add(nome);
            cache[nome] = prefab;
            return prefab;
        }

        // ------------------------------------------------------------------ geometria

        static float Liscio(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Distanza di un punto da un rettangolo (0 se è dentro).</summary>
        static float DistanzaRett(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float DistanzaLinea(Vector2 p, Vector2[] punti)
        {
            float migliore = float.MaxValue;
            for (int i = 0; i + 1 < punti.Length; i++)
            {
                Vector2 a = punti[i], b = punti[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                migliore = Mathf.Min(migliore, Vector2.Distance(p, a + ab * t));
            }
            return migliore;
        }
    }
}