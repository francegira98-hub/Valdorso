using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Crea la valle dalla mappa (ValleMappa) e dalla ricetta (RicettaValle):
    /// 3 × 3 tessere di terreno da 500 m collegate tra loro, con monti ai bordi (più alti a nord),
    /// la conca, la collina dell'Orso, il letto del fiume, la conca del lago, lo spiazzo del villaggio,
    /// le texture di prato, sottobosco, terra, roccia e ciottoli, e i segnaposti dei luoghi.
    /// Menu di Unity: Valdorso → Valle → Crea la valle.
    /// Attenzione: ricostruire cancella i ritocchi fatti a mano sul terreno.
    /// Funziona solo nell'editor, non entra nel gioco.
    /// </summary>
    public static class ValleBuilder
    {
        const string CartellaMondo = "Assets/_Valdorso/Mondo";
        const string Cartella = CartellaMondo + "/Valle";
        const string CartellaTerreno = Cartella + "/Terreno";
        const string CartellaLayer = Cartella + "/Layer";
        const string AtmosferaPath = Cartella + "/Atmosfera_Valle.asset";
        const string RicettaPath = Cartella + "/RicettaValle.asset";
        const string ScenePath = "Assets/_Valdorso/Scenes/Valle.unity";

        const int RisAltezze = 513;   // punti di altezza per lato di tessera (circa uno al metro)
        const int RisTexture = 512;   // punti di colore per lato di tessera della valle
        const int RisTextureAnello = 256; // l'anello di montagne si guarda da lontano: basta meno dettaglio
        const float AltezzaTerreno = 800f; // altezza massima del terreno, con le cime dell'anello

        // I cinque ruoli delle texture.
        const int PRATO = 0, SOTTOBOSCO = 1, TERRA = 2, ROCCIA = 3, CIOTTOLI = 4;
        static readonly string[] NomiRuoli = { "Prato", "Sottobosco", "Terra", "Roccia", "Ciottoli" };

        // Nomi dei Terrain Layer da cercare, in ordine di preferenza (dai pacchetti NatureManufacture).
        static readonly string[][] NomiCercati =
        {
            new[] { "layer_T_ground_meadow_grass_01", "Terrain Layer_Grass_1", "Terrain_Layer_Grass_01" },
            new[] { "layer_T_forest_ground_leaves_01", "Terrain_Layer5_Leaves", "Terrain_Layer_Leaves", "layer_T_forest_ground_needles_01" },
            new[] { "layer_T_ground_meadow_soil_01", "Terrain Layer_soil_01", "Terrain_Layer2_Soil" },
            new[] { "Terrain Layer_rocks", "Terrain_Layer_Rocks", "Terrain Layer_soil_rocky_02", "Terrain_Layer8_Stones" },
            new[] { "Terrain_Layer_River_Rocks", "Terrain_Layer_Small_Pebbles", "Terrain_Layer_Rock_Pebbles", "Terrain Layer_stones" }
        };

        [MenuItem("Valdorso/Valle/Crea la valle")]
        static void Crea()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK");
                return;
            }
            bool esiste = File.Exists(ScenePath) || AssetDatabase.IsValidFolder(CartellaTerreno);
            if (esiste && !EditorUtility.DisplayDialog("Valdorso",
                    "La valle esiste già. Ricostruirla da zero?\n\nI ritocchi fatti a mano sul terreno andranno persi.",
                    "Ricostruisci", "Annulla"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreaCartella("Assets/_Valdorso", "Mondo");
            CreaCartella(CartellaMondo, "Valle");
            CreaCartella(Cartella, "Terreno");
            CreaCartella(Cartella, "Layer");

            RicettaValle ricetta = PreparaRicetta(out string rapporto);
            if (ricetta.prato == null)
            {
                EditorUtility.DisplayDialog("Valdorso",
                    "Non trovo una texture per il prato. Seleziona la ricetta in " + RicettaPath +
                    ", trascina un Terrain Layer d'erba nel campo Prato e riprova.", "OK");
                Selection.activeObject = ricetta;
                return;
            }

            try
            {
                var campi = new Campi(ricetta);

                Scene scena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var radice = new GameObject("Valle");

                // Il sole: basso da sud-ovest, luce calda.
                var soleGo = new GameObject("Sole");
                soleGo.transform.SetParent(radice.transform);
                soleGo.transform.rotation = Quaternion.Euler(40f, 30f, 0f);
                var sole = soleGo.AddComponent<Light>();
                sole.type = LightType.Directional;
                sole.intensity = 1.2f;
                sole.color = new Color(1f, 0.95f, 0.87f);
                sole.shadows = LightShadows.Soft;
                RenderSettings.sun = sole;

                // L'atmosfera: il "filtro" dell'immagine (tonemapping, colori, un po' di bagliore).
                var atmosferaGo = new GameObject("Atmosfera");
                atmosferaGo.transform.SetParent(radice.transform);
                Volume volume = atmosferaGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = CreaAtmosfera();

                TerrainLayer[] layers = CompattaLayer(ricetta, out int[] ruolo);

                int n = ValleMappa.TesserePerLato;
                var tessere = new Terrain[n, n];
                var terreni = new GameObject("Terreno");
                terreni.transform.SetParent(radice.transform);
                int fatte = 0;
                for (int j = 0; j < n; j++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        EditorUtility.DisplayProgressBar("Valdorso", $"Modello la tessera {fatte + 1} di {n * n}...", 0.3f + 0.65f * fatte / (n * n));
                        TerrainData dati = CreaTessera(i, j, campi, layers, ruolo);
                        GameObject go = Terrain.CreateTerrainGameObject(dati);
                        go.name = $"Tessera_{i}_{j}";
                        go.transform.SetParent(terreni.transform);
                        go.transform.position = new Vector3(ValleMappa.Origine + i * ValleMappa.LatoTessera, 0f, ValleMappa.Origine + j * ValleMappa.LatoTessera);
                        Terrain t = go.GetComponent<Terrain>();
                        t.drawInstanced = true;
                        t.heightmapPixelError = 4f;
                        t.basemapDistance = 1000f;
                        t.groupingID = 1;
                        t.allowAutoConnect = true;
                        tessere[i, j] = t;
                        fatte++;
                    }
                }

                // Le tessere si "tengono per mano": niente cuciture visibili tra una e l'altra.
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                        tessere[i, j].SetNeighbors(
                            i > 0 ? tessere[i - 1, j] : null,
                            j < n - 1 ? tessere[i, j + 1] : null,
                            i < n - 1 ? tessere[i + 1, j] : null,
                            j > 0 ? tessere[i, j - 1] : null);

                // Telecamera panoramica dal passo (solo nell'editor: EditorOnly non entra nelle build).
                var panoramaGo = new GameObject("Telecamera_Panorama");
                panoramaGo.tag = "EditorOnly";
                panoramaGo.transform.SetParent(radice.transform);
                float xp = 1650f, zp = -120f;
                panoramaGo.transform.position = new Vector3(xp, campi.Altezza(xp, zp) + 140f, zp);
                panoramaGo.transform.LookAt(new Vector3(700f, 90f, 800f));
                Camera panorama = panoramaGo.AddComponent<Camera>();
                panorama.fieldOfView = 50f;
                panorama.nearClipPlane = 0.5f;
                panorama.farClipPlane = 5000f;
                panoramaGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

                // Segnaposti dei luoghi, con un'etichetta visibile nella finestra Scene.
                var segni = new GameObject("Segnaposti");
                segni.transform.SetParent(radice.transform);
                Texture2D icona = EditorGUIUtility.IconContent("sv_label_1").image as Texture2D;
                foreach (var luogo in ValleMappa.Luoghi)
                {
                    var g = new GameObject(luogo.nome);
                    g.transform.SetParent(segni.transform);
                    float x = luogo.posizione.x, z = luogo.posizione.y;
                    g.transform.position = new Vector3(x, campi.Altezza(x, z) + 0.5f, z);
                    if (icona != null) EditorGUIUtility.SetIconForObject(g, icona);
                }

                EditorUtility.DisplayProgressBar("Valdorso", "Salvo la scena...", 0.97f);
                EditorSceneManager.SaveScene(scena, ScenePath);
                AssetDatabase.SaveAssets();

                SceneView vista = SceneView.lastActiveSceneView;
                if (vista != null)
                {
                    vista.LookAt(new Vector3(750f, 150f, 700f), Quaternion.Euler(55f, 0f, 0f), 1500f);
                    vista.Repaint();
                }

                Debug.Log($"[Valdorso] Valle creata: {n * n} tessere da 500 m (valle 3 × 3 e anello di montagne) in {ScenePath}.\n" + rapporto);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------------------------------------------------------------- Ricetta e texture

        static RicettaValle PreparaRicetta(out string rapporto)
        {
            RicettaValle r = AssetDatabase.LoadAssetAtPath<RicettaValle>(RicettaPath);
            if (r == null)
            {
                r = ScriptableObject.CreateInstance<RicettaValle>();
                AssetDatabase.CreateAsset(r, RicettaPath);
            }

            Dictionary<string, string> disponibili = ElencoTerrainLayer();
            var sb = new StringBuilder("Texture usate (si cambiano nella ricetta " + RicettaPath + "):");
            TerrainLayer[] attuali = { r.prato, r.sottobosco, r.terra, r.roccia, r.ciottoli };
            for (int k = 0; k < attuali.Length; k++)
            {
                bool trovata = false;
                if (attuali[k] == null)
                {
                    attuali[k] = Cerca(disponibili, NomiCercati[k]);
                    trovata = attuali[k] != null;
                }
                string percorso = attuali[k] != null ? AssetDatabase.GetAssetPath(attuali[k]) : "(nessuna: uso il prato)";
                sb.Append("\n  ").Append(NomiRuoli[k]).Append(": ").Append(percorso).Append(trovata ? "  [scelta ora per nome]" : "");
            }
            r.prato = attuali[PRATO];
            r.sottobosco = attuali[SOTTOBOSCO];
            r.terra = attuali[TERRA];
            r.roccia = attuali[ROCCIA];
            r.ciottoli = attuali[CIOTTOLI];
            EditorUtility.SetDirty(r);
            AssetDatabase.SaveAssets();
            rapporto = sb.ToString();
            return r;
        }

        /// <summary>Tutti i Terrain Layer del progetto, per nome (senza le copie create da Unity e quelle delle scene demo).</summary>
        static Dictionary<string, string> ElencoTerrainLayer()
        {
            var elenco = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer"))
            {
                string percorso = AssetDatabase.GUIDToAssetPath(guid);
                if (percorso.Contains("_TerrainAutoUpgrade") || percorso.Contains("Floating_Island")) continue;
                string nome = Path.GetFileNameWithoutExtension(percorso);
                if (!elenco.ContainsKey(nome)) elenco[nome] = percorso;
            }
            return elenco;
        }

        static TerrainLayer Cerca(Dictionary<string, string> elenco, string[] nomi)
        {
            foreach (string nome in nomi)
                if (elenco.TryGetValue(nome, out string percorso))
                    return AssetDatabase.LoadAssetAtPath<TerrainLayer>(percorso);
            return null;
        }

        /// <summary>Toglie i doppioni: se un ruolo non ha texture usa quella del prato.</summary>
        static TerrainLayer[] CompattaLayer(RicettaValle r, out int[] ruolo)
        {
            TerrainLayer[] originali = { r.prato, r.sottobosco, r.terra, r.roccia, r.ciottoli };
            Color[] tinte = { r.tintaPrato, r.tintaSottobosco, r.tintaTerra, r.tintaRoccia, r.tintaCiottoli };
            var perRuolo = new TerrainLayer[originali.Length];
            for (int k = 0; k < originali.Length; k++)
                perRuolo[k] = CopiaLayer(originali[k] != null ? originali[k] : r.prato, NomiRuoli[k], tinte[k]);
            var unici = new List<TerrainLayer>();
            ruolo = new int[perRuolo.Length];
            for (int k = 0; k < perRuolo.Length; k++)
            {
                TerrainLayer l = perRuolo[k] != null ? perRuolo[k] : r.prato;
                int indice = unici.IndexOf(l);
                if (indice < 0)
                {
                    unici.Add(l);
                    indice = unici.Count - 1;
                }
                ruolo[k] = indice;
            }
            return unici.ToArray();
        }

        /// <summary>
        /// Una copia nostra del Terrain Layer del pacchetto: stesse immagini, niente metallo,
        /// e una tinta per scurire. Così i file dei pacchetti restano come sono.
        /// </summary>
        static TerrainLayer CopiaLayer(TerrainLayer origine, string nome, Color tinta)
        {
            string percorso = $"{CartellaLayer}/Valle_{nome}.terrainlayer";
            TerrainLayer copia = AssetDatabase.LoadAssetAtPath<TerrainLayer>(percorso);
            bool nuova = copia == null;
            if (nuova) copia = new TerrainLayer();

            copia.diffuseTexture = origine.diffuseTexture;
            copia.normalMapTexture = origine.normalMapTexture;
            copia.maskMapTexture = origine.maskMapTexture;
            copia.normalScale = origine.normalScale;
            copia.tileSize = origine.tileSize;
            copia.tileOffset = origine.tileOffset;
            copia.smoothness = origine.smoothness;
            copia.specular = Color.black;
            copia.metallic = 0f;
            copia.diffuseRemapMin = Vector4.zero;
            copia.diffuseRemapMax = new Vector4(tinta.r, tinta.g, tinta.b, 1f);
            // Nella mask map il canale rosso è il metallo: lo spegniamo del tutto.
            Vector4 minimo = origine.maskMapRemapMin, massimo = origine.maskMapRemapMax;
            minimo.x = 0f;
            massimo.x = 0f;
            copia.maskMapRemapMin = minimo;
            copia.maskMapRemapMax = massimo;

            if (nuova) AssetDatabase.CreateAsset(copia, percorso);
            else EditorUtility.SetDirty(copia);
            return copia;
        }

        static VolumeProfile CreaAtmosfera()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(AtmosferaPath) != null) AssetDatabase.DeleteAsset(AtmosferaPath);
            var profilo = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profilo, AtmosferaPath);

            Tonemapping tonemapping = profilo.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments colori = profilo.Add<ColorAdjustments>(true);
            colori.postExposure.Override(0.1f);
            colori.contrast.Override(10f);
            colori.saturation.Override(5f);

            Bloom bagliore = profilo.Add<Bloom>(true);
            bagliore.threshold.Override(1.1f);
            bagliore.intensity.Override(0.3f);

            foreach (VolumeComponent componente in profilo.components) AssetDatabase.AddObjectToAsset(componente, profilo);
            EditorUtility.SetDirty(profilo);
            return profilo;
        }

        // ---------------------------------------------------------------- Una tessera

        static TerrainData CreaTessera(int i, int j, Campi campi, TerrainLayer[] layers, int[] ruolo)
        {
            string percorso = $"{CartellaTerreno}/Tessera_{i}_{j}.asset";
            if (File.Exists(percorso)) AssetDatabase.DeleteAsset(percorso);

            var dati = new TerrainData();
            dati.heightmapResolution = RisAltezze;
            int n = ValleMappa.TesserePerLato;
            bool anello = i == 0 || j == 0 || i == n - 1 || j == n - 1;
            int risTexture = anello ? RisTextureAnello : RisTexture;
            dati.size = new Vector3(ValleMappa.LatoTessera, AltezzaTerreno, ValleMappa.LatoTessera);
            dati.alphamapResolution = risTexture;
            dati.baseMapResolution = 1024;
            AssetDatabase.CreateAsset(dati, percorso);
            dati.terrainLayers = layers;

            float ox = ValleMappa.Origine + i * ValleMappa.LatoTessera, oz = ValleMappa.Origine + j * ValleMappa.LatoTessera;

            // Altezze: la tabella di Unity è [nord, est], con valori da 0 a 1.
            var altezze = new float[RisAltezze, RisAltezze];
            float passoA = ValleMappa.LatoTessera / (RisAltezze - 1);
            for (int zi = 0; zi < RisAltezze; zi++)
                for (int xi = 0; xi < RisAltezze; xi++)
                    altezze[zi, xi] = Mathf.Clamp01(campi.Altezza(ox + xi * passoA, oz + zi * passoA) / AltezzaTerreno);
            dati.SetHeights(0, 0, altezze);

            // Colori: per ogni punto quanto c'è di prato, sottobosco, terra, roccia e ciottoli.
            var mappe = new float[risTexture, risTexture, layers.Length];
            var pesi = new float[5];
            float passoT = ValleMappa.LatoTessera / risTexture;
            for (int az = 0; az < risTexture; az++)
            {
                for (int ax = 0; ax < risTexture; ax++)
                {
                    float x = ox + (ax + 0.5f) * passoT, z = oz + (az + 0.5f) * passoT;
                    float ripidezza = dati.GetSteepness((ax + 0.5f) / risTexture, (az + 0.5f) / risTexture);
                    campi.Pesi(x, z, ripidezza, pesi);
                    for (int k = 0; k < 5; k++) mappe[az, ax, ruolo[k]] += pesi[k];
                }
            }
            dati.SetAlphamaps(0, 0, mappe);
            EditorUtility.SetDirty(dati);
            return dati;
        }

        static void CreaCartella(string genitore, string nome)
        {
            if (!AssetDatabase.IsValidFolder(genitore + "/" + nome)) AssetDatabase.CreateFolder(genitore, nome);
        }

        // ---------------------------------------------------------------- Le regole del terreno

        /// <summary>
        /// Tutto ciò che serve per sapere quanto è alto e di che colore è ogni punto della valle.
        /// Le distanze da fiume, strade, bosco e bordi della conca si calcolano una volta sola
        /// su una griglia di 2 m, poi si leggono in fretta.
        /// </summary>
        class Campi
        {
            readonly RicettaValle r;
            readonly Griglia valle = new Griglia(), bosco = new Griglia(), fiumeD = new Griglia(), fiumeT = new Griglia(), strade = new Griglia();
            readonly float[] superficieFiume;
            readonly float[] o = new float[12];

            const float NucleoFiume = 6f;      // metà larghezza del letto (fiume largo 12 m)
            const float FondoFiume = 2.2f;     // profondità dell'acqua al centro

            public Campi(RicettaValle ricetta)
            {
                r = ricetta;
                var caso = new System.Random(r.seme);
                for (int k = 0; k < o.Length; k++) o[k] = (float)caso.NextDouble() * 1000f;

                // Il fiume: punti, lunghezze e livello dell'acqua che scende sempre verso il lago.
                List<Vector2> fiume = ValleMappa.CampionaFiume(50);
                var lunghezze = new float[fiume.Count];
                for (int k = 1; k < fiume.Count; k++) lunghezze[k] = lunghezze[k - 1] + Vector2.Distance(fiume[k - 1], fiume[k]);
                float totale = lunghezze[fiume.Count - 1];
                superficieFiume = new float[fiume.Count];
                float minimo = float.MaxValue;
                for (int k = 0; k < fiume.Count; k++)
                {
                    minimo = Mathf.Min(minimo, Fondo(fiume[k].x, fiume[k].y) - 1.5f);
                    superficieFiume[k] = Mathf.Max(minimo, r.livelloLago);
                }

                Vector2 fiumeMin = fiume[0], fiumeMax = fiume[0];
                foreach (Vector2 q in fiume)
                {
                    fiumeMin = Vector2.Min(fiumeMin, q);
                    fiumeMax = Vector2.Max(fiumeMax, q);
                }

                for (int iz = 0; iz < Griglia.N; iz++)
                {
                    if (iz % 50 == 0) EditorUtility.DisplayProgressBar("Valdorso", "Traccio fiume, strade e bosco sulla mappa...", 0.3f * iz / Griglia.N);
                    for (int ix = 0; ix < Griglia.N; ix++)
                    {
                        var p = new Vector2(Griglia.Origine + ix * Griglia.Passo, Griglia.Origine + iz * Griglia.Passo);
                        valle.Scrivi(ix, iz, DistanzaConSegno(p, ValleMappa.Valle));
                        bosco.Scrivi(ix, iz, DistanzaConSegno(p, ValleMappa.Bosco));

                        float migliore = float.MaxValue, lungo = 0f;
                        // Lontano dal fiume non serve cercare il punto più vicino (risparmia tempo).
                        bool vicino = p.x > fiumeMin.x - 300f && p.x < fiumeMax.x + 300f && p.y > fiumeMin.y - 300f && p.y < fiumeMax.y + 300f;
                        if (!vicino) migliore = 1000f;
                        for (int k = 1; vicino && k < fiume.Count; k++)
                        {
                            float d = DistanzaSegmento(p, fiume[k - 1], fiume[k], out float t);
                            if (d < migliore)
                            {
                                migliore = d;
                                lungo = lunghezze[k - 1] + t * (lunghezze[k] - lunghezze[k - 1]);
                            }
                        }
                        fiumeD.Scrivi(ix, iz, migliore);
                        fiumeT.Scrivi(ix, iz, lungo / totale);

                        float ds = float.MaxValue;
                        foreach (Vector2[] strada in ValleMappa.Strade)
                            for (int k = 1; k < strada.Length; k++)
                                ds = Mathf.Min(ds, DistanzaSegmento(p, strada[k - 1], strada[k], out _));
                        strade.Scrivi(ix, iz, ds);
                    }
                }
            }

            /// <summary>Il fondo della valle senza rilievi: sale piano da sud-ovest a nord-est.</summary>
            float Fondo(float x, float z) => r.fondoValle + r.pendenzaVersoLago * 0.5f * (x / ValleMappa.Lato + z / ValleMappa.Lato);

            float Rumore(float x, float z, float scala, int k) => Mathf.PerlinNoise(x / scala + o[k], z / scala + o[k + 1]);

            float SuperficieFiume(float t)
            {
                float f = Mathf.Clamp01(t) * (superficieFiume.Length - 1);
                int a = Mathf.Min((int)f, superficieFiume.Length - 2);
                return Mathf.Lerp(superficieFiume[a], superficieFiume[a + 1], f - a);
            }

            /// <summary>L'altezza del terreno in metri nel punto (est, nord).</summary>
            public float Altezza(float x, float z)
            {
                var p = new Vector2(x, z);
                float dFiume = fiumeD.Leggi(x, z);
                float eLago = Ellisse(p, ValleMappa.Lago, ValleMappa.LagoRaggi);

                // Fondo con dolci ondulazioni, più calme vicino all'acqua.
                float calma = Liscio((dFiume - 10f) / 60f) * Liscio((eLago - 1.1f) / 0.8f);
                float h = Fondo(x, z) + ((Rumore(x, z, 180f, 0) - 0.5f) * 4f + (Rumore(x, z, 520f, 2) - 0.5f) * 6f) * calma;

                // Monti: salgono fuori dalla conca (con i piedi un po' dentro), più alti a nord, con creste.
                float m = Liscio((valle.Leggi(x, z) + 100f) / 210f);
                if (m > 0f)
                {
                    float massimo = Mathf.Lerp(r.monteSud, r.monteNord, z / ValleMappa.Lato);
                    float cresta = 1f - Mathf.Abs(Rumore(x, z, 220f, 4) * 2f - 1f);
                    float ruvido = (Rumore(x, z, 55f, 6) - 0.5f) * 24f;
                    // Nell'anello, lontano dalla valle, le montagne crescono in cime e creste più grandi.
                    float oltre = Liscio((valle.Leggi(x, z) - 80f) / 420f);
                    float picchi = 1f - Mathf.Abs(Rumore(x, z, 380f, 10) * 2f - 1f);
                    // Piccole creste e canaloni che spezzano i pendii lisci.
                    float dettaglio = (1f - Mathf.Abs(Rumore(x, z, 28f, 0) * 2f - 1f)) * 10f;
                    h += m * (massimo * (0.65f + 0.5f * cresta) * (1f + 0.8f * oltre * (0.5f + picchi)) + (ruvido + dettaglio) * m);
                }

                // La collina dell'Orso.
                float eCollina = Ellisse(p, ValleMappa.Collina, ValleMappa.CollinaRaggi);
                if (eCollina < 1f) h += r.collina * 0.5f * (1f + Mathf.Cos(Mathf.PI * eCollina));

                // Lo spiazzo piano del villaggio.
                float dVillaggio = Vector2.Distance(p, ValleMappa.Villaggio);
                float piano = 1f - Liscio((dVillaggio - 70f) / 50f);
                if (piano > 0f) h = Mathf.Lerp(h, Fondo(ValleMappa.Villaggio.x, ValleMappa.Villaggio.y) + 0.8f, piano);

                // Il letto del fiume: più largo dove scava più a fondo (valle a V nei monti).
                float superficie = SuperficieFiume(fiumeT.Leggi(x, z));
                if (dFiume < NucleoFiume)
                {
                    float q = dFiume / NucleoFiume;
                    h = Mathf.Min(h, superficie - FondoFiume * (1f - q * q));
                }
                else
                {
                    float riva = Mathf.Min(12f + 0.7f * Mathf.Max(0f, h - superficie), 180f);
                    if (dFiume < NucleoFiume + riva)
                        h = Mathf.Min(h, Mathf.Lerp(superficie + 0.4f, h, Liscio((dFiume - NucleoFiume) / riva)));
                }

                // La conca del lago con la sua spiaggia.
                if (eLago < 1f)
                    h = Mathf.Min(h, r.livelloLago + 0.3f - (r.profonditaLago + 0.3f) * (1f - eLago * eLago));
                else if (eLago < 1.5f)
                    h = Mathf.Min(h, Mathf.Lerp(r.livelloLago + 0.3f, h, Liscio((eLago - 1f) / 0.5f)));

                return Mathf.Clamp(h, 0f, AltezzaTerreno);
            }

            /// <summary>Quanto c'è di ogni texture nel punto: i cinque pesi sommano sempre a 1.</summary>
            public void Pesi(float x, float z, float ripidezza, float[] w)
            {
                var p = new Vector2(x, z);
                for (int k = 0; k < w.Length; k++) w[k] = 0f;
                w[PRATO] = 1f;

                // Sottobosco nel bosco grande e nel boschetto.
                float f = Liscio((15f - bosco.Leggi(x, z)) / 30f);
                f = Mathf.Max(f, 1f - Liscio((Ellisse(p, ValleMappa.Boschetto, ValleMappa.BoschettoRaggi) - 0.85f) / 0.3f));
                Mescola(w, SOTTOBOSCO, f);

                // Chiazze di terra grandi e rade nei prati, e terra calpestata ai lati delle strade.
                float dentroValle = 1f - Liscio((valle.Leggi(x, z) + 60f) / 80f);
                float chiazza = Liscio((Rumore(x, z, 110f, 8) - 0.66f) / 0.14f) * Liscio((Rumore(x, z, 23f, 10) - 0.35f) / 0.3f);
                Mescola(w, TERRA, chiazza * 0.45f * dentroValle);
                Mescola(w, TERRA, (1f - Liscio((strade.Leggi(x, z) - 3f) / 7f)) * 0.3f * dentroValle);

                // Villaggio e strade in terra battuta.
                Mescola(w, TERRA, (1f - Liscio((Vector2.Distance(p, ValleMappa.Villaggio) - 35f) / 30f)) * 0.75f);
                Mescola(w, TERRA, 1f - Liscio((strade.Leggi(x, z) - 1.8f) / 1.6f));

                // Ciottoli nel letto del fiume e sulla riva del lago.
                Mescola(w, CIOTTOLI, 1f - Liscio((fiumeD.Leggi(x, z) - (NucleoFiume + 1.5f)) / 3f));
                Mescola(w, CIOTTOLI, 1f - Liscio((Ellisse(p, ValleMappa.Lago, ValleMappa.LagoRaggi) - 1.02f) / 0.08f));

                // Pendii bassi dei monti: macchie di sottobosco (dove un giorno ci saranno abeti e pini).
                float quota = Altezza(x, z) - Fondo(x, z);
                float suiMonti = Liscio((valle.Leggi(x, z) + 40f) / 100f);
                float macchie = Liscio((Rumore(x, z, 150f, 2) - 0.45f) / 0.2f) * (1f - Liscio((quota - 230f) / 80f));
                Mescola(w, SOTTOBOSCO, macchie * suiMonti * 0.85f);

                // Roccia: sui pendii ripidi sempre, in alto anche dove è meno ripido. Sotto resta l'erba.
                float ripido = Liscio((ripidezza - 27f) / 13f);
                float alto = Liscio((quota - 240f) / 120f) * 0.9f;
                Mescola(w, ROCCIA, Mathf.Max(ripido, alto));
            }

            static void Mescola(float[] w, int indice, float quanto)
            {
                quanto = Mathf.Clamp01(quanto);
                if (quanto <= 0f) return;
                for (int k = 0; k < w.Length; k++) w[k] *= 1f - quanto;
                w[indice] += quanto;
            }
        }

        // ---------------------------------------------------------------- Geometria

        /// <summary>Una tabella di numeri ogni 2 m su tutta la valle, letta con interpolazione.</summary>
        class Griglia
        {
            public const float Origine = ValleMappa.Origine;                   // da -500 m...
            public const float Passo = 2f;
            public const int N = (int)((ValleMappa.Lato + 2f * ValleMappa.Anello) / Passo) + 1; // ...a 2000 m, ogni 2 m
            readonly float[] v = new float[N * N];

            public void Scrivi(int ix, int iz, float valore) => v[iz * N + ix] = valore;

            public float Leggi(float x, float z)
            {
                float gx = Mathf.Clamp((x - Origine) / Passo, 0f, N - 1.001f);
                float gz = Mathf.Clamp((z - Origine) / Passo, 0f, N - 1.001f);
                int x0 = (int)gx, z0 = (int)gz;
                float fx = gx - x0, fz = gz - z0;
                float a = Mathf.Lerp(v[z0 * N + x0], v[z0 * N + x0 + 1], fx);
                float b = Mathf.Lerp(v[(z0 + 1) * N + x0], v[(z0 + 1) * N + x0 + 1], fx);
                return Mathf.Lerp(a, b, fz);
            }
        }

        static float Liscio(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float Ellisse(Vector2 p, Vector2 centro, Vector2 raggi)
        {
            float dx = (p.x - centro.x) / raggi.x, dz = (p.y - centro.y) / raggi.y;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float DistanzaSegmento(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Distanza dal contorno: negativa dentro la forma, positiva fuori.</summary>
        static float DistanzaConSegno(Vector2 p, Vector2[] forma)
        {
            float d = float.MaxValue;
            bool dentro = false;
            for (int i = 0, j = forma.Length - 1; i < forma.Length; j = i++)
            {
                d = Mathf.Min(d, DistanzaSegmento(p, forma[j], forma[i], out _));
                if ((forma[i].y > p.y) != (forma[j].y > p.y) &&
                    p.x < (forma[j].x - forma[i].x) * (p.y - forma[i].y) / (forma[j].y - forma[i].y) + forma[i].x)
                    dentro = !dentro;
            }
            return dentro ? -d : d;
        }
    }
}