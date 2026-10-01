using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Valle → Erba vera (più fitta): rende l'erba un prato vero (prova di bellezza, 6.7, passo 2e).
    /// Prima l'erba stava su celle di quasi 2 m (256 per tessera) con 3-4 fili per metro quadro; vicino al villaggio
    /// ancora meno (ValleNatura la riduceva al 30% entro 45 m). Questo strumento:
    /// - rende la griglia due volte più fine (512 per tessera, celle di circa 1 m), così i bordi intorno a case e strade
    ///   non sono più a scalini;
    /// - moltiplica i fili d'erba (i tipi con "grass" nel nome) per PiuErba, e ancora per PiuErbaVillaggio vicino al villaggio;
    /// - lascia fiori, felci, trifogli e il resto con lo stesso numero di prima, solo distribuiti sulla griglia nuova;
    /// - dove prima non c'era erba (case, strade, fiume, lago) non ne mette.
    /// Si usa una volta sola, sulla griglia vecchia da 256: se la tessera è già a 512 la salta.
    /// Per tornare indietro: git restore Assets/_Valdorso/Mondo/Valle/Terreno (rimette le tessere dell'ultimo commit).
    /// Se si rilancia Semina la natura (che rimette 256), dopo va rilanciato anche questo. Solo editor.
    /// </summary>
    public static class ErbaVera
    {
        const int RisVecchia = 256;
        const int RisNuova = 512;
        const int PerBlocco = 32;
        const float PiuErba = 3f;             // fili d'erba: tre volte tanti
        const float PiuErbaVillaggio = 2.5f;  // vicino al villaggio ancora di più (prima era ridotta al 30%)
        const float RaggioVillaggio = 45f;

        [MenuItem("Valdorso/Valle/Erba vera (più fitta)")]
        static void Fai()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK");
                return;
            }
            Terrain[] terreni = Terrain.activeTerrains;
            if (terreni.Length == 0 || GameObject.Find("/Valle") == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Valle.", "OK");
                return;
            }

            int fatte = 0, saltate = 0;
            long prima = 0, dopo = 0;
            var caso = new System.Random(312);
            int f = RisNuova / RisVecchia;
            try
            {
                for (int ti = 0; ti < terreni.Length; ti++)
                {
                    Terrain t = terreni[ti];
                    TerrainData d = t.terrainData;
                    DetailPrototype[] proto = d.detailPrototypes;
                    if (proto.Length == 0) continue;
                    if (d.detailResolution != RisVecchia) { saltate++; continue; }
                    EditorUtility.DisplayProgressBar("Erba vera", "Tessera " + t.name + "...", (float)ti / terreni.Length);

                    int n = proto.Length;
                    var vecchie = new int[n][,];
                    for (int k = 0; k < n; k++)
                    {
                        vecchie[k] = d.GetDetailLayer(0, 0, RisVecchia, RisVecchia, k);
                        foreach (int v in vecchie[k]) prima += v;
                    }

                    d.SetDetailResolution(RisNuova, PerBlocco);
                    Vector3 o = t.transform.position;
                    float passo = d.size.x / RisNuova;

                    for (int k = 0; k < n; k++)
                    {
                        bool erba = Erba(proto[k]);
                        int[,] vecchia = vecchie[k];
                        var nuova = new int[RisNuova, RisNuova];
                        for (int z = 0; z < RisNuova; z++)
                        {
                            for (int x = 0; x < RisNuova; x++)
                            {
                                if (vecchia[z / f, x / f] == 0) continue;   // dove non c'era erba non se ne mette
                                float valore = Bilineare(vecchia, (x + 0.5f) / f - 0.5f, (z + 0.5f) / f - 0.5f) / (f * f);
                                if (erba)
                                {
                                    valore *= PiuErba;
                                    var punto = new Vector2(o.x + (x + 0.5f) * passo, o.z + (z + 0.5f) * passo);
                                    if (Vector2.Distance(punto, ValleMappa.Villaggio) < RaggioVillaggio) valore *= PiuErbaVillaggio;
                                }
                                int intero = (int)valore;
                                if (caso.NextDouble() < valore - intero) intero++;
                                intero = Mathf.Min(255, intero);
                                nuova[z, x] = intero;
                                dopo += intero;
                            }
                        }
                        d.SetDetailLayer(0, 0, k, nuova);
                    }
                    EditorUtility.SetDirty(d);
                    fatte++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Valdorso] Erba vera: " + fatte + " tessere fatte, " + saltate + " già fatte prima e saltate; fili e fiori da "
                      + prima + " a " + dopo + ". Ora Ctrl+S sulla scena e prova in Play guardando gli fps (Stats).");
        }

        // (Il 30/09 qui c'era "Erba più alta e varia": tolto, perché con i fili del pacchetto già alti li portava fino a 3 m.
        //  Le altezze giuste le mette "Erba: conta i fili".)

        // Correzione (30/09): i terreni erano in "CoverageMode", dove i numeri delle mappe sono una copertura da 0 a 255
        // (15 = 6% di terreno coperto), mentre tutti i nostri strumenti li scrivono come numero di fili per cella.
        // Per questo l'erba era rada. Qui si passa a "InstanceCountMode" rileggendo e riscrivendo le mappe così come sono.
        // Altezze: dalla diagnosi i fili del pacchetto sono alti di natura 0,2-1,6 m; ogni tipo viene portato
        // tra AltezzaPrato e AltezzaPratoMax metri veri (erba alla caviglia e al ginocchio), fiori e felci restano com'erano.
        const float AltezzaPrato = 0.35f, AltezzaPratoMax = 0.85f;

        [MenuItem("Valdorso/Valle/Erba: conta i fili (correzione)")]
        static void ContaIFili()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK");
                return;
            }
            int cambiate = 0, gia = 0, tipi = 0;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                TerrainData d = t.terrainData;
                DetailPrototype[] proto = d.detailPrototypes;
                if (proto.Length == 0) continue;

                if (d.detailScatterMode != DetailScatterMode.InstanceCountMode)
                {
                    int r = d.detailResolution;
                    var mappe = new int[proto.Length][,];
                    for (int k = 0; k < proto.Length; k++) mappe[k] = d.GetDetailLayer(0, 0, r, r, k);
                    d.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                    for (int k = 0; k < proto.Length; k++) d.SetDetailLayer(0, 0, k, mappe[k]);
                    cambiate++;
                }
                else gia++;

                for (int k = 0; k < proto.Length; k++)
                {
                    if (!Erba(proto[k]) || proto[k].prototype == null) continue;
                    float base_ = AltezzaMesh(proto[k].prototype);
                    if (base_ <= 0.01f) continue;
                    proto[k].minHeight = Mathf.Clamp(AltezzaPrato / base_, 0.2f, 3f);
                    proto[k].maxHeight = Mathf.Clamp(AltezzaPratoMax / base_, proto[k].minHeight, 3f);
                    proto[k].minWidth = 0.9f;
                    proto[k].maxWidth = 1.3f;
                    if (cambiate + gia == 1) tipi++;
                }
                d.detailPrototypes = proto;
                EditorUtility.SetDirty(d);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Valdorso] Erba, conta i fili: " + cambiate + " tessere passate a InstanceCountMode, " + gia
                      + " lo erano già; " + tipi + " tipi d'erba portati a " + AltezzaPrato + "-" + AltezzaPratoMax + " m. Ora Ctrl+S.");
        }

        static float AltezzaMesh(GameObject prefab)
        {
            float alto = 0f;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) alto = Mathf.Max(alto, mf.sharedMesh.bounds.size.y * mf.transform.lossyScale.y);
            return alto;
        }

        // ---------- Strade, piazza e case libere dall'erba ----------
        // Con l'erba finalmente fitta si è visto che copriva le strade: prima la pulizia era a celle di 2 m e stretta.
        // Qui: niente erba sulla carreggiata (con il bordo frastagliato), erba più rada e consumata per un metro e mezzo
        // ai lati, niente erba sulla piazza e sotto le case "già pronte" del villaggio. Si può rilanciare.
        const float MezzaStradaValle = 2.5f;   // strade di terra della valle: 5 m
        const float BordoConsumato = 1.5f;

        [MenuItem("Valdorso/Valle/Erba: libera strade e case")]
        static void LiberaStrade()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK");
                return;
            }
            // Le strade in coordinate del mondo, con la loro mezza larghezza
            var strade = new List<(float meta, Vector2[] punti)>();
            foreach (var s in PiantaVillaggio.Strade)
            {
                var punti = new Vector2[s.punti.Length];
                for (int i = 0; i < punti.Length; i++) punti[i] = PiantaVillaggio.AlMondo(s.punti[i]);
                strade.Add((s.larghezza * 0.5f, punti));
            }
            foreach (Vector2[] s in ValleMappa.Strade) strade.Add((MezzaStradaValle, s));

            var vuoti = new List<Rect>();
            PiantaVillaggio.Lotto piazza = PiantaVillaggio.TrovaLotto("piazza");
            if (piazza != null)
                vuoti.Add(new Rect(piazza.area.position + ValleMappa.Villaggio, piazza.area.size));
            GameObject case_ = GameObject.Find("/Valle/Case del villaggio/Case");
            if (case_ != null)
            {
                foreach (Transform casa in case_.transform)
                {
                    Bounds b = default;
                    bool trovato = false;
                    foreach (Renderer r in casa.GetComponentsInChildren<Renderer>())
                    {
                        if (!trovato) { b = r.bounds; trovato = true; }
                        else b.Encapsulate(r.bounds);
                    }
                    if (trovato) vuoti.Add(Rect.MinMaxRect(b.min.x - 0.5f, b.min.z - 0.5f, b.max.x + 0.5f, b.max.z + 0.5f));
                }
            }

            int tessere = 0;
            long tolti = 0;
            try
            {
                Terrain[] terreni = Terrain.activeTerrains;
                for (int ti = 0; ti < terreni.Length; ti++)
                {
                    Terrain t = terreni[ti];
                    TerrainData d = t.terrainData;
                    int n = d.detailPrototypes.Length;
                    if (n == 0) continue;
                    EditorUtility.DisplayProgressBar("Erba: libera strade e case", t.name, (float)ti / terreni.Length);
                    Vector3 o = t.transform.position;
                    Vector3 m = d.size;
                    var zona = Rect.MinMaxRect(o.x - 8f, o.z - 8f, o.x + m.x + 8f, o.z + m.z + 8f);

                    // Solo le strade che passano per questa tessera
                    var qui = new List<(float meta, Vector2[] punti)>();
                    foreach (var s in strade)
                        foreach (Vector2 p in s.punti)
                            if (zona.Contains(p)) { qui.Add(s); break; }
                    var vuotiQui = new List<Rect>();
                    foreach (Rect v in vuoti) if (v.Overlaps(zona)) vuotiQui.Add(v);
                    if (qui.Count == 0 && vuotiQui.Count == 0) continue;

                    int r = d.detailResolution;
                    float passo = m.x / r;
                    var mappe = new int[n][,];
                    for (int k = 0; k < n; k++) mappe[k] = d.GetDetailLayer(0, 0, r, r, k);
                    for (int z = 0; z < r; z++)
                    {
                        for (int x = 0; x < r; x++)
                        {
                            var p = new Vector2(o.x + (x + 0.5f) * passo, o.z + (z + 0.5f) * passo);
                            float f = 1f;
                            foreach (Rect v in vuotiQui) if (v.Contains(p)) { f = 0f; break; }
                            if (f > 0f)
                            {
                                foreach (var s in qui)
                                {
                                    float dist = DistanzaLinea(p, s.punti);
                                    float meta = s.meta * (0.85f + 0.3f * Mathf.PerlinNoise(p.x / 7f, p.y / 7f));
                                    if (dist < meta) { f = 0f; break; }
                                    if (dist < meta + BordoConsumato) f = Mathf.Min(f, Mathf.Lerp(0.35f, 1f, (dist - meta) / BordoConsumato));
                                }
                            }
                            if (f >= 1f) continue;
                            for (int k = 0; k < n; k++)
                            {
                                int prima = mappe[k][z, x];
                                if (prima == 0) continue;
                                int dopo = Mathf.RoundToInt(prima * f);
                                tolti += prima - dopo;
                                mappe[k][z, x] = dopo;
                            }
                        }
                    }
                    for (int k = 0; k < n; k++) d.SetDetailLayer(0, 0, k, mappe[k]);
                    EditorUtility.SetDirty(d);
                    tessere++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Valdorso] Erba: libera strade e case: " + tessere + " tessere, " + tolti + " fili tolti da strade, piazza e "
                      + (vuoti.Count - 1) + " case. Ora Ctrl+S.");
        }

        static float DistanzaLinea(Vector2 p, Vector2[] punti)
        {
            float migliore = float.MaxValue;
            for (int i = 0; i < punti.Length - 1; i++)
            {
                Vector2 a = punti[i], ab = punti[i + 1] - a;
                float q = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                migliore = Mathf.Min(migliore, Vector2.Distance(p, a + q * ab));
            }
            return migliore;
        }

        // ---------- Niente sfarfallio ----------
        // Erba e foglie sono fatte di tanti fili sottili: senza anti-aliasing tremolano quando ci si muove.
        // L'anti-aliasing temporale (TAA) mescola i fotogrammi e li rende fermi e morbidi.
        [MenuItem("Valdorso/Luce/Niente sfarfallio (TAA)")]
        static void NienteSfarfallio()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK");
                return;
            }
            var nomi = new List<string>();
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var dati = c.GetUniversalAdditionalCameraData();
                if (dati == null) continue;
                Undo.RecordObject(dati, "Niente sfarfallio");
                dati.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                dati.antialiasingQuality = AntialiasingQuality.High;
                EditorUtility.SetDirty(dati);
                nomi.Add(c.name);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Valdorso] Niente sfarfallio: TAA alta sulle telecamere " + string.Join(", ", nomi) + ". Ora Ctrl+S.");
        }

        [MenuItem("Valdorso/Prove/Diagnosi dell'erba")]
        static void Diagnosi()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var t = new System.Text.StringBuilder();
            t.AppendLine("DIAGNOSI DELL'ERBA");
            t.AppendLine("Qualita " + QualitySettings.names[QualitySettings.GetQualityLevel()] + ": regole del terreno " + QualitySettings.terrainQualityOverrides
                         + ", densita erba " + QualitySettings.terrainDetailDensityScale.ToString(ci)
                         + ", distanza erba " + QualitySettings.terrainDetailDistance.ToString(ci));
            Terrain tr = null;
            foreach (Terrain x in Terrain.activeTerrains) if (x.terrainData.detailPrototypes.Length > 0) { tr = x; break; }
            if (tr == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Nessuna tessera con l'erba: apri la scena Valle.", "OK");
                return;
            }
            TerrainData d = tr.terrainData;
            t.AppendLine("Tessera " + tr.name + ": disegna alberi ed erba " + tr.drawTreesAndFoliage + ", terreno instanced " + tr.drawInstanced
                         + ", distanza erba " + tr.detailObjectDistance.ToString(ci) + ", densita " + tr.detailObjectDensity.ToString(ci)
                         + ", risoluzione " + d.detailResolution + ", modo " + d.detailScatterMode);
            DetailPrototype[] proto = d.detailPrototypes;
            for (int k = 0; k < proto.Length; k++)
            {
                DetailPrototype p = proto[k];
                bool valido = p.Validate(out string errore);
                string nome = p.prototype != null ? p.prototype.name : (p.prototypeTexture != null ? p.prototypeTexture.name : "-");
                t.AppendLine(k + ". " + nome + ": mesh " + p.usePrototypeMesh + ", instancing " + p.useInstancing + ", " + p.renderMode
                             + ", altezza " + p.minHeight.ToString("0.##", ci) + "-" + p.maxHeight.ToString("0.##", ci)
                             + ", valido " + valido + (valido ? "" : " (" + errore + ")"));
                if (p.prototype == null) continue;
                foreach (Renderer r in p.prototype.GetComponentsInChildren<Renderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    string misura = mf != null && mf.sharedMesh != null
                        ? Vector3.Scale(mf.sharedMesh.bounds.size, r.transform.lossyScale).ToString("0.00") : "?";
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) { t.AppendLine("     materiale mancante"); continue; }
                        t.AppendLine("     " + r.name + " misura " + misura + ", materiale " + m.name + ", shader " + m.shader.name
                                     + ", supportato " + m.shader.isSupported + ", instancing " + m.enableInstancing);
                    }
                }
            }
            EditorGUIUtility.systemCopyBuffer = t.ToString();
            Debug.Log("[Valdorso] Diagnosi dell'erba copiata negli appunti.");
            EditorUtility.DisplayDialog("Valdorso", "Diagnosi dell'erba copiata negli appunti: incollala nella chat (Ctrl+V).", "OK");
        }

        /// <summary>L'erba vera e propria: i tipi con "grass" nel nome (i fiori e il resto no).</summary>
        static bool Erba(DetailPrototype p)
        {
            string nome = p.prototype != null ? p.prototype.name : (p.prototypeTexture != null ? p.prototypeTexture.name : "");
            return nome.ToLowerInvariant().Contains("grass");
        }

        /// <summary>Valore morbido tra le celle vecchie vicine, così la griglia nuova non ha scalini.</summary>
        static float Bilineare(int[,] m, float x, float z)
        {
            int r = m.GetLength(0);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, r - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(z), 0, r - 1);
            int x1 = Mathf.Min(x0 + 1, r - 1);
            int z1 = Mathf.Min(z0 + 1, r - 1);
            float tx = Mathf.Clamp01(x - x0);
            float tz = Mathf.Clamp01(z - z0);
            float a = Mathf.Lerp(m[z0, x0], m[z0, x1], tx);
            float b = Mathf.Lerp(m[z1, x0], m[z1, x1], tx);
            return Mathf.Lerp(a, b, tz);
        }
    }
}