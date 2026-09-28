using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Semina la natura nella valle già creata: alberi del terreno (bosco, macchie sui monti, alberi isolati,
    /// cespugli), erba come dettagli del terreno e rocce come oggetti della scena, seguendo le regole della mappa
    /// (fitto nel bosco, radure vicino al villaggio, niente sulle strade e nell'acqua).
    /// Menu di Unity: Valdorso → Valle → Semina la natura. Si può rilanciare: toglie la natura di prima e la rifà.
    /// Funziona solo nell'editor, non entra nel gioco.
    /// </summary>
    public static partial class ValleBuilder
    {
        const string RicettaNaturaPath = Cartella + "/RicettaNatura.asset";
        const int RisErba = 256;          // celle d'erba per lato di tessera (una ogni 2 m circa)
        const int ErbaPerPatch = 16;

        [MenuItem("Valdorso/Valle/Semina la natura")]
        static void SeminaNatura()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK");
                return;
            }
            RicettaValle ricettaValle = AssetDatabase.LoadAssetAtPath<RicettaValle>(RicettaPath);
            Terrain[] terreni = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            GameObject radice = GameObject.Find("Valle");
            if (ricettaValle == null || terreni.Length == 0 || radice == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri la scena Valle (e, se non esiste, crea prima la valle).", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Valdorso",
                    "Seminare la natura? Alberi, erba e rocce seminati prima verranno tolti e rifatti.", "Semina", "Annulla"))
                return;

            RicettaNatura r = PreparaRicettaNatura(out string rapportoPrefab);
            if (r.alberiBosco.Count == 0)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo gli alberi del bosco: controlla la ricetta " + RicettaNaturaPath + ".", "OK");
                Selection.activeObject = r;
                return;
            }

            try
            {
                var campi = new Campi(ricettaValle);
                var caso = new System.Random(r.seme);
                float o1 = (float)caso.NextDouble() * 1000f, o2 = (float)caso.NextDouble() * 1000f;

                // ---------- Alberi ----------
                var prototipi = new List<GameObject>();
                int Indice(GameObject g)
                {
                    int i = prototipi.IndexOf(g);
                    if (i < 0) { prototipi.Add(g); i = prototipi.Count - 1; }
                    return i;
                }
                var alberiPerTerreno = new Dictionary<Terrain, List<TreeInstance>>();
                foreach (Terrain t in terreni) alberiPerTerreno[t] = new List<TreeInstance>();
                int nBosco = 0, nMonte = 0, nPrato = 0, nCespugli = 0;

                const float cella = 6f;
                int celle = Mathf.CeilToInt((ValleMappa.Lato + 2f * ValleMappa.Anello) / cella);
                for (int iz = 0; iz < celle; iz++)
                {
                    if (iz % 20 == 0) EditorUtility.DisplayProgressBar("Valdorso", "Pianto gli alberi...", 0.3f + 0.3f * iz / celle);
                    for (int ix = 0; ix < celle; ix++)
                    {
                        float x = ValleMappa.Origine + (ix + (float)caso.NextDouble()) * cella;
                        float z = ValleMappa.Origine + (iz + (float)caso.NextDouble()) * cella;
                        double tiro = caso.NextDouble();
                        Terrain t = TerrenoIn(terreni, x, z, out Vector2 n);
                        if (t == null) continue;
                        if (!LiberoPerAlberi(campi, x, z, 14f)) continue;
                        float ripidezza = t.terrainData.GetSteepness(n.x, n.y);

                        float bosco = Bosco(campi, x, z);
                        float monte = campi.MacchieMonte(x, z);
                        float pBosco = bosco * cella * cella / r.boscoMetriPerAlbero;
                        float pMonte = monte * cella * cella / r.monteMetriPerAlbero;
                        float pPrato = campi.DistanzaValle(x, z) < -30f ? r.probabilitaAlberoPrato * (1f - bosco) * (1f - monte) : 0f;
                        bool bordoBosco = bosco > 0.05f && bosco < 0.5f;
                        float dFiume = campi.DistanzaFiume(x, z);
                        bool rivaFiume = dFiume > 14f && dFiume < 24f && campi.DistanzaValle(x, z) < -20f;

                        GameObject scelto = null;
                        float scalaMin = 0.85f, scalaMax = 1.2f;
                        if (tiro < pBosco && ripidezza < 35f && r.alberiBosco.Count > 0)
                        {
                            scelto = r.alberiBosco[caso.Next(r.alberiBosco.Count)];
                            nBosco++;
                        }
                        else if (tiro < pBosco + pMonte && ripidezza < 40f)
                        {
                            var lista = r.alberiMonte.Count > 0 ? r.alberiMonte : r.alberiBosco;
                            scelto = lista[caso.Next(lista.Count)];
                            scalaMin = 0.8f; scalaMax = 1.15f;
                            nMonte++;
                        }
                        else if (tiro < pBosco + pMonte + pPrato && ripidezza < 25f)
                        {
                            var lista = r.alberiPrato.Count > 0 ? r.alberiPrato : r.alberiBosco;
                            scelto = lista[caso.Next(lista.Count)];
                            scalaMin = 1.0f; scalaMax = 1.3f;
                            nPrato++;
                        }
                        else if ((bordoBosco || rivaFiume) && tiro > 0.85f && ripidezza < 30f && r.cespugli.Count > 0)
                        {
                            scelto = r.cespugli[caso.Next(r.cespugli.Count)];
                            scalaMin = 0.8f; scalaMax = 1.2f;
                            nCespugli++;
                        }
                        if (scelto == null) continue;

                        float scala = Mathf.Lerp(scalaMin, scalaMax, (float)caso.NextDouble());
                        alberiPerTerreno[t].Add(new TreeInstance
                        {
                            position = new Vector3(n.x, 0f, n.y),
                            widthScale = scala,
                            heightScale = scala * Mathf.Lerp(0.95f, 1.08f, (float)caso.NextDouble()),
                            rotation = (float)caso.NextDouble() * Mathf.PI * 2f,
                            color = Color.white,
                            lightmapColor = Color.white,
                            prototypeIndex = Indice(scelto)
                        });
                    }
                }

                var protoAlberi = new TreePrototype[prototipi.Count];
                for (int i = 0; i < prototipi.Count; i++) protoAlberi[i] = new TreePrototype { prefab = prototipi[i] };

                // ---------- Erba (solo nelle 9 tessere della valle: l'anello si guarda da lontano) ----------
                var erbe = new List<GameObject>();
                var ruoloErba = new List<int>(); // 0 prato, 1 bosco, 2 monte
                void AggiungiErbe(List<GameObject> lista, int ruolo)
                {
                    foreach (GameObject g in lista)
                        if (g != null && !erbe.Contains(g)) { erbe.Add(g); ruoloErba.Add(ruolo); }
                }
                AggiungiErbe(r.erbaPrato, 0);
                AggiungiErbe(r.erbaBosco, 1);
                AggiungiErbe(r.erbaMonte, 2);
                var protoErba = new List<DetailPrototype>();
                var erbaValida = new List<int>();
                for (int i = 0; i < erbe.Count; i++)
                {
                    var d = new DetailPrototype
                    {
                        prototype = erbe[i],
                        usePrototypeMesh = true,
                        useInstancing = true,
                        renderMode = DetailRenderMode.VertexLit,
                        minWidth = 0.8f,
                        maxWidth = 1.2f,
                        minHeight = 0.8f,
                        maxHeight = 1.3f,
                        noiseSpread = 0.3f,
                        healthyColor = Color.white,
                        dryColor = new Color(0.92f, 0.9f, 0.8f)
                    };
                    if (d.Validate(out string errore)) { protoErba.Add(d); erbaValida.Add(i); }
                    else Debug.LogWarning("[Valdorso] Erba scartata " + erbe[i].name + ": " + errore);
                }

                int tessereFatte = 0;
                foreach (Terrain t in terreni)
                {
                    EditorUtility.DisplayProgressBar("Valdorso", $"Semino la tessera {tessereFatte + 1} di {terreni.Length}...", 0.6f + 0.3f * tessereFatte / terreni.Length);
                    tessereFatte++;
                    TerrainData dati = t.terrainData;
                    dati.treePrototypes = protoAlberi;
                    dati.SetTreeInstances(alberiPerTerreno[t].ToArray(), true);

                    Vector3 o = t.transform.position;
                    bool inValle = o.x >= -1f && o.z >= -1f && o.x < ValleMappa.Lato - 1f && o.z < ValleMappa.Lato - 1f;
                    dati.SetDetailResolution(RisErba, ErbaPerPatch);
                    dati.detailPrototypes = inValle ? protoErba.ToArray() : new DetailPrototype[0];
                    if (inValle)
                    {
                        var mappe = new int[protoErba.Count][,];
                        for (int k = 0; k < mappe.Length; k++) mappe[k] = new int[RisErba, RisErba];
                        float passo = ValleMappa.LatoTessera / RisErba;
                        for (int cz = 0; cz < RisErba; cz++)
                        {
                            for (int cx = 0; cx < RisErba; cx++)
                            {
                                float x = o.x + (cx + 0.5f) * passo, z = o.z + (cz + 0.5f) * passo;
                                float ripidezza = dati.GetSteepness((cx + 0.5f) / RisErba, (cz + 0.5f) / RisErba);
                                ErbaInCella(campi, r, x, z, ripidezza, o1, o2, ruoloErba, erbaValida, mappe, cx, cz);
                            }
                        }
                        for (int k = 0; k < mappe.Length; k++) dati.SetDetailLayer(0, 0, k, mappe[k]);
                    }

                    t.treeDistance = 2000f;
                    t.treeBillboardDistance = 220f;
                    t.treeCrossFadeLength = 30f;
                    t.treeMaximumFullLODCount = 200;
                    t.detailObjectDistance = r.distanzaErba;
                    t.detailObjectDensity = 1f;
                    EditorUtility.SetDirty(dati);
                    EditorUtility.SetDirty(t);
                }

                // ---------- Rocce ----------
                EditorUtility.DisplayProgressBar("Valdorso", "Poso le rocce...", 0.93f);
                Transform vecchia = radice.transform.Find("Natura");
                if (vecchia != null) Object.DestroyImmediate(vecchia.gameObject);
                var natura = new GameObject("Natura");
                natura.transform.SetParent(radice.transform);
                var rocce = new GameObject("Rocce");
                rocce.transform.SetParent(natura.transform);

                int rValle = PosaRocce(campi, terreni, caso, r.rocceValle, r.rocceValleNumero, rocce.transform, "Valle", 0.7f, 1.2f,
                    (x, z, rip) => campi.DistanzaValle(x, z) < -40f && Bosco(campi, x, z) < 0.2f && rip < 25f && LiberoPerAlberi(campi, x, z, 10f));
                int rBosco = PosaRocce(campi, terreni, caso, r.rocceBosco, r.rocceBoscoNumero, rocce.transform, "Bosco", 0.8f, 1.4f,
                    (x, z, rip) => Bosco(campi, x, z) > 0.6f && rip < 35f && LiberoPerAlberi(campi, x, z, 12f));
                int rFiume = PosaRocce(campi, terreni, caso, r.rocceFiume, r.rocceFiumeNumero, rocce.transform, "Fiume", 0.6f, 1.0f,
                    (x, z, rip) => { float d = campi.DistanzaFiume(x, z); return d > 7f && d < 16f && campi.DistanzaValle(x, z) < -20f && campi.DistanzaStrade(x, z) > 5f; });
                int rMonte = PosaRocce(campi, terreni, caso, r.rocceMonte, r.rocceMonteNumero, rocce.transform, "Monte", 1.0f, 2.2f,
                    (x, z, rip) => campi.DistanzaValle(x, z) > -60f && rip > 15f && rip < 50f && campi.DistanzaStrade(x, z) > 8f);

                EditorSceneManager.MarkSceneDirty(radice.scene);
                EditorSceneManager.SaveScene(radice.scene);
                AssetDatabase.SaveAssets();

                Debug.Log($"[Valdorso] Natura seminata: alberi {nBosco} nel bosco, {nMonte} sui monti, {nPrato} nei prati, {nCespugli} cespugli " +
                          $"({prototipi.Count} tipi); erba {protoErba.Count} tipi nelle 9 tessere della valle; " +
                          $"rocce {rValle} nei prati, {rBosco} nel bosco, {rFiume} lungo il fiume, {rMonte} sui monti.\n" + rapportoPrefab);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------------------------------------------------------------- Regole

        static float Bosco(Campi campi, float x, float z)
        {
            float f = Liscio((10f - campi.DistanzaBosco(x, z)) / 25f);
            float e = Ellisse(new Vector2(x, z), ValleMappa.Boschetto, ValleMappa.BoschettoRaggi);
            return Mathf.Max(f, 1f - Liscio((e - 0.8f) / 0.35f));
        }

        /// <summary>Niente alberi e rocce nell'acqua, sulle strade, nel villaggio e nella radura delle rovine.</summary>
        static bool LiberoPerAlberi(Campi campi, float x, float z, float margineFiume)
        {
            var p = new Vector2(x, z);
            if (campi.DistanzaFiume(x, z) < margineFiume) return false;
            if (Ellisse(p, ValleMappa.Lago, ValleMappa.LagoRaggi) < 1.15f) return false;
            if (campi.DistanzaStrade(x, z) < 6f) return false;
            if (Vector2.Distance(p, ValleMappa.Villaggio) < 120f) return false;
            foreach (var luogo in ValleMappa.Luoghi)
                if ((luogo.nome == "Rovine_Antichi" || luogo.nome == "Ingresso_Cripta" || luogo.nome.StartsWith("Ingresso_Grotta")) &&
                    Vector2.Distance(p, luogo.posizione) < 25f)
                    return false;
            return true;
        }

        static void ErbaInCella(Campi campi, RicettaNatura r, float x, float z, float ripidezza, float o1, float o2,
            List<int> ruoloErba, List<int> erbaValida, int[][,] mappe, int cx, int cz)
        {
            var p = new Vector2(x, z);
            if (campi.DistanzaFiume(x, z) < 7.5f) return;
            if (Ellisse(p, ValleMappa.Lago, ValleMappa.LagoRaggi) < 1.04f) return;
            if (ripidezza > 32f) return;
            float dStrada = campi.DistanzaStrade(x, z);
            if (dStrada < 2.5f) return;
            float fattore = dStrada < 4f ? 0.4f : 1f;
            if (Vector2.Distance(p, ValleMappa.Villaggio) < 45f) fattore *= 0.3f;

            float bosco = Bosco(campi, x, z);
            bool suiMonti = campi.DistanzaValle(x, z) > -40f;
            int ruolo = bosco > 0.5f ? 1 : (suiMonti ? 2 : 0);
            if (ruolo == 2 && campi.Quota(x, z) > 250f) return;

            // Quali erbe crescono qui: chiazze larghe dello stesso tipo, come nei prati veri.
            var candidate = new List<int>();
            for (int k = 0; k < erbaValida.Count; k++) if (ruoloErba[erbaValida[k]] == ruolo) candidate.Add(k);
            if (candidate.Count == 0) return;
            float chiazza = Mathf.PerlinNoise(x / 35f + o1, z / 35f + o2);
            int scelta = candidate[Mathf.Min(candidate.Count - 1, (int)(chiazza * candidate.Count))];
            float folto = Mathf.Clamp01(Mathf.PerlinNoise(x / 18f + o2, z / 18f + o1) * 1.4f - 0.1f);

            // Le erbe di Meadow sono fili singoli: servono in tanti. Ogni cella ha un'erba principale e, dove c'è, una seconda a metà.
            int quanti;
            if (ruolo == 0) quanti = Mathf.RoundToInt(r.erbaPratoDensita * (0.6f + 0.4f * folto) * fattore);
            else if (ruolo == 1) quanti = Mathf.RoundToInt(4f * folto * fattore);
            else quanti = Mathf.RoundToInt(6f * folto * fattore * (1f - Liscio((ripidezza - 22f) / 10f)));
            if (quanti <= 0) return;
            mappe[scelta][cz, cx] = Mathf.Min(255, quanti);
            if (candidate.Count > 1)
            {
                float altra = Mathf.PerlinNoise(x / 13f + o1 * 0.5f, z / 13f + o2 * 0.5f);
                int seconda = candidate[Mathf.Min(candidate.Count - 1, (int)(altra * candidate.Count))];
                if (seconda != scelta) mappe[seconda][cz, cx] = Mathf.Min(255, quanti / 2);
            }
        }

        static Terrain TerrenoIn(Terrain[] terreni, float x, float z, out Vector2 normalizzato)
        {
            foreach (Terrain t in terreni)
            {
                Vector3 o = t.transform.position;
                Vector3 s = t.terrainData.size;
                if (x >= o.x && x < o.x + s.x && z >= o.z && z < o.z + s.z)
                {
                    normalizzato = new Vector2((x - o.x) / s.x, (z - o.z) / s.z);
                    return t;
                }
            }
            normalizzato = Vector2.zero;
            return null;
        }

        static int PosaRocce(Campi campi, Terrain[] terreni, System.Random caso, List<GameObject> prefab, int numero, Transform genitore,
            string nomeGruppo, float scalaMin, float scalaMax, System.Func<float, float, float, bool> adatto)
        {
            if (prefab.Count == 0 || numero <= 0) return 0;
            var gruppo = new GameObject(nomeGruppo);
            gruppo.transform.SetParent(genitore);
            int posate = 0;
            float lato = ValleMappa.Lato + 2f * ValleMappa.Anello;
            // Molti tentativi: alcune zone (la riva del fiume) sono strisce sottili sulla mappa.
            for (int tentativo = 0; tentativo < numero * 400 && posate < numero; tentativo++)
            {
                float x = ValleMappa.Origine + (float)caso.NextDouble() * lato;
                float z = ValleMappa.Origine + (float)caso.NextDouble() * lato;
                Terrain t = TerrenoIn(terreni, x, z, out Vector2 n);
                if (t == null) continue;
                float ripidezza = t.terrainData.GetSteepness(n.x, n.y);
                if (!adatto(x, z, ripidezza)) continue;

                GameObject scelto = prefab[caso.Next(prefab.Count)];
                var roccia = (GameObject)PrefabUtility.InstantiatePrefab(scelto, gruppo.transform);
                float scala = Mathf.Lerp(scalaMin, scalaMax, (float)caso.NextDouble());
                float y = t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y;
                roccia.transform.position = new Vector3(x, y - 0.25f * scala, z);
                roccia.transform.rotation = Quaternion.Euler(((float)caso.NextDouble() - 0.5f) * 12f, (float)caso.NextDouble() * 360f, ((float)caso.NextDouble() - 0.5f) * 12f);
                roccia.transform.localScale = Vector3.one * scala;
                posate++;
            }
            return posate;
        }

        // ---------------------------------------------------------------- Ricetta della natura

        static RicettaNatura PreparaRicettaNatura(out string rapporto)
        {
            RicettaNatura r = AssetDatabase.LoadAssetAtPath<RicettaNatura>(RicettaNaturaPath);
            bool nuova = r == null;
            if (nuova)
            {
                r = ScriptableObject.CreateInstance<RicettaNatura>();
                AssetDatabase.CreateAsset(r, RicettaNaturaPath);
            }

            // Tutti i prefab dei pacchetti NatureManufacture, per nome (le copie "VS_" sono per un altro programma e si saltano).
            var tutti = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/NatureManufacture Assets" }))
            {
                string percorso = AssetDatabase.GUIDToAssetPath(guid);
                string nome = Path.GetFileNameWithoutExtension(percorso);
                if (nome.StartsWith("VS_", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!tutti.ContainsKey(nome)) tutti[nome] = percorso;
            }

            void Riempi(List<GameObject> lista, IEnumerable<string> nomi)
            {
                if (lista.Count > 0) return; // già scelta (anche a mano): non si tocca
                foreach (string nome in nomi)
                    if (tutti.TryGetValue(nome, out string percorso))
                    {
                        var g = AssetDatabase.LoadAssetAtPath<GameObject>(percorso);
                        if (g != null && !lista.Contains(g)) lista.Add(g);
                    }
            }
            IEnumerable<string> Serie(string prima, int da, int a, string dopo = "", string formato = "00")
            {
                for (int i = da; i <= a; i++) yield return prima + i.ToString(formato) + dopo;
            }
            IEnumerable<string> InCartella(string pezzoPercorso, System.Func<string, bool> filtro)
            {
                foreach (var coppia in tutti)
                    if (coppia.Value.Replace('\\', '/').Contains(pezzoPercorso) && filtro(coppia.Key.ToLowerInvariant()))
                        yield return coppia.Key;
            }
            bool NonPezzo(string n) => !(n.Contains("branch") || n.Contains("cone") || n.Contains("log") || n.Contains("stump") ||
                                         n.Contains("root") || n.Contains("leaf") || n.Contains("leaves") || n.Contains("bush") || n.Contains("particle") ||
                                         n.Contains("plant") || n.EndsWith("_static"));

            var bosco = new List<string>(Serie("prefab_beech_tree_", 1, 9));
            bosco.AddRange(new[] { "Prefab_Forest_pine_tree_00_A", "Prefab_Forest_pine_tree_00_B", "Prefab_Forest_pine_tree_00_C", "Prefab_Forest_pine_tree_00_D" });
            Riempi(r.alberiBosco, bosco);
            Riempi(r.alberiMonte, InCartella("/Mountain Environment/Pine_trees/", NonPezzo));
            Riempi(r.alberiPrato, InCartella("/Meadow Environment/", n => NonPezzo(n) &&
                (n.Contains("tree") || n.Contains("oak") || n.Contains("maple") || n.Contains("birch") || n.Contains("linden"))));
            Riempi(r.alberiPrato, new[] { "prefab_beech_tree_00_A", "prefab_beech_tree_00_B", "prefab_beech_tree_00_C", "prefab_beech_tree_00_D" });
            Riempi(r.cespugli, new[] { "Prefab_hazel_00_bush", "prefab_maple_bush_01", "prefab_maple_bush_02", "prefab_maple_bush_03", "prefab_maple_bush_04" });

            var valle = new List<string>(Serie("prefab_m_rock_", 1, 3, "_grass_top"));
            valle.AddRange(Serie("prefab_s_rock_", 1, 6, "_grass_top"));
            valle.AddRange(Serie("prefab_Rock_", 1, 7, "_grass_top"));
            Riempi(r.rocceValle, valle);
            var roccebosco = new List<string>(Serie("Prefab_ground_rock_", 1, 3, "_moss"));
            roccebosco.AddRange(new[] { "Prefab_Big_rock_01_moss", "Prefab_Big_rock_02_moss", "Prefab_ground_rock_01_needles", "Prefab_ground_rock_02_needles" });
            Riempi(r.rocceBosco, roccebosco);
            var monte = new List<string>(Serie("Prefab_mountain_rock_big_01_", 1, 5, "", "0"));
            monte.AddRange(Serie("Prefab_mountain_rock_big_02_", 1, 8, "", "0"));
            monte.AddRange(Serie("Prefab_mountain_rock_small_01_", 1, 5, "", "0"));
            Riempi(r.rocceMonte, monte);
            Riempi(r.rocceFiume, Serie("Prefab_flat_rock_", 1, 4, "_moss"));

            Riempi(r.erbaPrato, new[] { "prefab_Terrain_grass_meadow_01_1", "prefab_Terrain_grass_meadow_01_3", "prefab_Terrain_grass_meadow_01_5",
                                        "prefab_Terrain_grass_meadow_02_2", "prefab_Terrain_grass_meadow_02_4", "prefab_Terrain_grass_meadow_03_1" });
            Riempi(r.erbaBosco, new[] { "prefab_Unity_Terrain_grass_01_1", "prefab_Unity_Terrain_grass_01_3", "prefab_Unity_Terrain_grass_02_1" });
            Riempi(r.erbaMonte, new[] { "Prefab_grass_02_A_1_Unity_Terrain", "Prefab_grass_03_A_1_Unity_Terrain" });

            EditorUtility.SetDirty(r);
            AssetDatabase.SaveAssets();

            var sb = new StringBuilder("Prefab usati (si cambiano nella ricetta " + RicettaNaturaPath + "):");
            void Elenca(string titolo, List<GameObject> lista)
            {
                sb.Append("\n  ").Append(titolo).Append(" (").Append(lista.Count).Append("): ");
                for (int i = 0; i < lista.Count; i++) sb.Append(i > 0 ? ", " : "").Append(lista[i] != null ? lista[i].name : "(vuoto)");
            }
            Elenca("Alberi del bosco", r.alberiBosco);
            Elenca("Alberi dei monti", r.alberiMonte);
            Elenca("Alberi dei prati", r.alberiPrato);
            Elenca("Cespugli", r.cespugli);
            Elenca("Rocce dei prati", r.rocceValle);
            Elenca("Rocce del bosco", r.rocceBosco);
            Elenca("Rocce dei monti", r.rocceMonte);
            Elenca("Rocce del fiume", r.rocceFiume);
            Elenca("Erba dei prati", r.erbaPrato);
            Elenca("Erba del bosco", r.erbaBosco);
            Elenca("Erba dei monti", r.erbaMonte);
            rapporto = sb.ToString();
            return r;
        }
    }
}