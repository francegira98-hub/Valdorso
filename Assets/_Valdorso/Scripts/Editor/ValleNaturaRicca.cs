using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Arricchisce la natura della valle, sopra quella di "Semina la natura" (proposta di Claude, 28/09, chiesta da Fra):
    /// fiori a chiazze, trifogli e margherite nei prati; erbe alte e canne lungo il fiume e il lago; felci, mughetti,
    /// mirtilli ed edera nel bosco; erica e mirtilli rossi sui monti (tutti dettagli del terreno);
    /// e come oggetti: tronchi caduti, ceppi, radici, funghi e rami nel bosco; sassi, massi bagnati e salici sulle rive;
    /// cespugli, formicai, cataste di legna, rastrelliere del fieno, farfalle e api nei prati;
    /// alberi tra le case e intorno al villaggio, un frutteto, siepi, aiuole di fiori ed erba alta lungo i recinti.
    /// Menu: Valdorso → Valle → Arricchisci la natura. Ordine: Semina la natura, Costruisci il villaggio,
    /// poi Arricchisci la natura (per ultimo). Si può rilanciare: toglie l'arricchimento di prima e lo rifà.
    /// Solo editor.
    /// </summary>
    public static partial class ValleBuilder
    {
        const string GruppoRicco = "Arricchimento";

        // Dettagli nuovi: chiave → nome del prefab (versioni "Terrain" dei pacchetti, fatte per i dettagli del terreno)
        static readonly (string chiave, string prefab, float min, float max)[] DettagliRicchi =
        {
            ("fiore", "prefab_Terrain_flower_chamomile_01_cross_1", 0.8f, 1.2f),
            ("fiore", "prefab_Terrain_flower_cornflower_01_cross_1", 0.8f, 1.2f),
            ("fiore", "prefab_Terrain_flower_common_poppy_01_cross_1", 0.8f, 1.2f),
            ("fiore", "prefab_Terrain_flower_common_Saint_John's_wort_01_cross_1", 0.9f, 1.3f),
            ("fiore", "prefab_Terrain_flower_common_chicory_01_cross_1", 0.8f, 1.1f),
            ("trifoglio", "prefab_detail_meadow_clover_01", 0.8f, 1.2f),
            ("margherita", "prefab_detail_meadow_daisy_01", 0.8f, 1.2f),
            ("riva", "prefab_Terrain_grass_meadow_01_cross_1", 0.9f, 1.3f),
            ("riva", "prefab_Terrain_flower_goldenrod_01_cross_1", 0.8f, 1.2f),
            ("riva", "prefab_Terrain_flower_sunroot_01_cross_1", 0.7f, 1.1f),
            ("felce", "prefab_Unity_Terrain_fern_01_2", 0.8f, 1.4f),
            ("felce", "prefab_Unity_Terrain_fern_01_3", 0.8f, 1.4f),
            ("sottobosco", "prefab_Unity_Terrain_lily_valley_01_1", 0.8f, 1.2f),
            ("sottobosco", "Prefab_billberry_03_Unity_Terrain", 0.8f, 1.3f),
            ("monte", "Prefab_heath_A_01_Unity_Terrain", 0.8f, 1.3f),
            ("monte", "Prefab_lingonberry_01_Unity_Terrain", 0.8f, 1.3f),
        };

        static Dictionary<string, GameObject> prefabNatura;
        static readonly List<string> mancantiNatura = new List<string>();

        [MenuItem("Valdorso/Valle/Arricchisci la natura")]
        static void ArricchisciNatura()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK"); return; }
            RicettaValle ricettaValle = AssetDatabase.LoadAssetAtPath<RicettaValle>(RicettaPath);
            Terrain[] terreni = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            GameObject radice = GameObject.Find("/Valle");
            Transform natura = radice != null ? radice.transform.Find("Natura") : null;
            if (ricettaValle == null || terreni.Length == 0 || radice == null || natura == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri la scena Valle con la natura già seminata (Valdorso → Valle → Semina la natura).", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Valdorso",
                "Arricchire la natura?\n\nAggiunge fiori, erbe, felci, sassi, tronchi, funghi, salici, alberi e siepi intorno al villaggio. " +
                "L'arricchimento di prima viene tolto e rifatto. Va lanciato dopo Costruisci il villaggio.", "Arricchisci", "Annulla"))
                return;

            CaricaPrefabNatura();
            mancantiNatura.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("Valdorso", "Preparo le mappe della valle...", 0.05f);
                var campi = new Campi(ricettaValle);
                var caso = new System.Random(412);
                float o1 = (float)caso.NextDouble() * 1000f, o2 = (float)caso.NextDouble() * 1000f;

                int tessere = DettagliNuovi(terreni, campi, o1, o2);

                EditorUtility.DisplayProgressBar("Valdorso", "Poso tronchi, sassi, salici e alberi...", 0.7f);
                Transform vecchio = natura.Find(GruppoRicco);
                if (vecchio != null) Object.DestroyImmediate(vecchio.gameObject);
                var gruppo = new GameObject(GruppoRicco).transform;
                gruppo.SetParent(natura, false);

                var conti = new List<string>();
                OggettiBosco(campi, terreni, caso, gruppo, conti);
                OggettiFiume(campi, terreni, caso, gruppo, conti);
                OggettiPrati(campi, terreni, caso, gruppo, conti);
                OggettiVillaggio(campi, terreni, caso, gruppo, conti);

                foreach (Terrain t in terreni) EditorUtility.SetDirty(t.terrainData);
                EditorSceneManager.MarkSceneDirty(radice.scene);
                if (mancantiNatura.Count > 0)
                    Debug.LogWarning("[Valdorso] Prefab non trovati per l'arricchimento: " + string.Join(", ", mancantiNatura));
                Debug.Log($"[Valdorso] Natura arricchita: dettagli nuovi su {tessere} tessere; " + string.Join(", ", conti) + ".");
                EditorUtility.DisplayDialog("Valdorso", "Natura arricchita.\n" + string.Join("\n", conti) +
                    "\n\nRicorda: Ctrl+S sulla scena e File → Save Project.", "OK");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        // ------------------------------------------------------------------ dettagli del terreno

        static int DettagliNuovi(Terrain[] terreni, Campi campi, float o1, float o2)
        {
            // I prototipi nuovi (validi) e il loro gruppo
            var nuovi = new List<DetailPrototype>();
            var chiavi = new List<string>();
            var prefabNuovi = new HashSet<GameObject>();
            foreach (var d in DettagliRicchi)
            {
                GameObject g = PrefabNatura(d.prefab);
                if (g == null) continue;
                var p = new DetailPrototype
                {
                    prototype = g,
                    usePrototypeMesh = true,
                    useInstancing = true,
                    renderMode = DetailRenderMode.VertexLit,
                    minWidth = d.min,
                    maxWidth = d.max,
                    minHeight = d.min,
                    maxHeight = d.max,
                    noiseSpread = 0.4f,
                    healthyColor = Color.white,
                    dryColor = new Color(0.93f, 0.9f, 0.82f)
                };
                if (p.Validate(out string errore)) { nuovi.Add(p); chiavi.Add(d.chiave); prefabNuovi.Add(g); }
                else Debug.LogWarning("[Valdorso] Dettaglio scartato " + d.prefab + ": " + errore);
            }
            int Scegli(string chiave, float v)
            {
                var c = new List<int>();
                for (int i = 0; i < chiavi.Count; i++) if (chiavi[i] == chiave) c.Add(i);
                if (c.Count == 0) return -1;
                return c[Mathf.Clamp((int)(v * c.Count), 0, c.Count - 1)];
            }

            // Recinti del villaggio (per l'erba alta lungo le staccionate)
            var recinti = new List<(Vector2 a, Vector2 b)>();
            foreach (PiantaVillaggio.Oggetto o in PiantaVillaggio.Oggetti)
            {
                if (!o.prefab.Contains("fence")) continue;
                float lungo = o.prefab.Contains("fenceB") ? 1.7f : 3.3f;
                Vector2 dir = new Vector2(Mathf.Sin(o.rotY * Mathf.Deg2Rad), Mathf.Cos(o.rotY * Mathf.Deg2Rad));
                Vector2 c = PiantaVillaggio.AlMondo(o.posizione);
                recinti.Add((c - dir * lungo * 0.5f, c + dir * lungo * 0.5f));
            }

            int fatte = 0;
            int nT = 0;
            foreach (Terrain t in terreni)
            {
                nT++;
                TerrainData dati = t.terrainData;
                DetailPrototype[] vecchi = dati.detailPrototypes;
                if (vecchi.Length == 0) continue; // l'anello di montagne non ha erba
                EditorUtility.DisplayProgressBar("Valdorso", $"Fiori ed erbe, tessera {nT} di {terreni.Length}...", 0.1f + 0.55f * nT / terreni.Length);

                int ris = dati.detailResolution;
                // Si tengono i prototipi della semina (non quelli di un arricchimento precedente), con le loro mappe
                var tenuti = new List<DetailPrototype>();
                var mappeTenute = new List<int[,]>();
                for (int k = 0; k < vecchi.Length; k++)
                {
                    if (vecchi[k].prototype != null && prefabNuovi.Contains(vecchi[k].prototype)) continue;
                    tenuti.Add(vecchi[k]);
                    mappeTenute.Add(dati.GetDetailLayer(0, 0, ris, ris, k));
                }

                var mappe = new int[nuovi.Count][,];
                for (int k = 0; k < mappe.Length; k++) mappe[k] = new int[ris, ris];
                Vector3 o = t.transform.position;
                float passo = dati.size.x / ris;

                for (int cz = 0; cz < ris; cz++)
                    for (int cx = 0; cx < ris; cx++)
                    {
                        float x = o.x + (cx + 0.5f) * passo, z = o.z + (cz + 0.5f) * passo;
                        var p = new Vector2(x, z);
                        float rip = dati.GetSteepness((cx + 0.5f) / ris, (cz + 0.5f) / ris);
                        float caso1 = Hash(cx + (int)o.x, cz + (int)o.z);

                        float dFiume = campi.DistanzaFiume(x, z);
                        float lago = Ellisse(p, ValleMappa.Lago, ValleMappa.LagoRaggi);
                        if (dFiume < 7f || lago < 1.02f) continue;
                        if (campi.DistanzaStrade(x, z) < 2.5f) continue;

                        // Nel villaggio: aiuole intorno alle case, erba alta lungo i recinti, prato con margherite
                        Vector2 pv = p - ValleMappa.Villaggio;
                        if (pv.sqrMagnitude < 190f * 190f)
                        {
                            int stato = StatoVillaggio(pv, recinti, p);
                            if (stato == 0) continue;                           // strada, edificio, piazza, orto, aia
                            if (stato == 2)                                      // aiuola vicino a una casa
                            {
                                if (caso1 > 0.35f) Metti(mappe, Scegli("fiore", Hash(cx, cz * 7)), cx, cz, 2 + (int)(caso1 * 3f));
                                continue;
                            }
                            if (stato == 3)                                      // lungo un recinto
                            {
                                Metti(mappe, Scegli("riva", 0f), cx, cz, 3 + (int)(caso1 * 3f));
                                continue;
                            }
                            if (stato == 4)                                      // prato del villaggio
                            {
                                if (Mathf.PerlinNoise(x / 12f + o1, z / 12f + o2) > 0.62f) Metti(mappe, Scegli("margherita", 0f), cx, cz, 2);
                                else if (Mathf.PerlinNoise(x / 16f + o2, z / 16f + o1) > 0.64f) Metti(mappe, Scegli("trifoglio", 0f), cx, cz, 2);
                                continue;
                            }
                        }

                        // Rive del fiume e del lago: erbe alte, verga d'oro, girasoli selvatici
                        bool riva = (dFiume < 13f && campi.DistanzaValle(x, z) < -15f) || (lago < 1.12f);
                        if (riva && rip < 30f)
                        {
                            float m = Mathf.PerlinNoise(x / 9f + o1, z / 9f + o2);
                            if (m > 0.35f) Metti(mappe, Scegli("riva", Mathf.PerlinNoise(x / 20f + o2, z / 20f)), cx, cz, 2 + (int)(m * 6f));
                            continue;
                        }

                        float bosco = Bosco(campi, x, z);
                        bool suiMonti = campi.DistanzaValle(x, z) > -40f;
                        if (bosco > 0.5f)
                        {
                            if (rip > 35f) continue;
                            float f = Mathf.PerlinNoise(x / 14f + o1, z / 14f + o2);
                            if (f > 0.45f) Metti(mappe, Scegli("felce", Mathf.PerlinNoise(x / 30f, z / 30f + o1)), cx, cz, 1 + (int)((f - 0.45f) * 9f));
                            float s = Mathf.PerlinNoise(x / 22f + o2, z / 22f + o1);
                            if (s > 0.62f) Metti(mappe, Scegli("sottobosco", Mathf.PerlinNoise(x / 40f + o2, z / 40f)), cx, cz, 2 + (int)((s - 0.62f) * 10f));
                        }
                        else if (suiMonti)
                        {
                            if (rip > 30f || campi.Quota(x, z) > 250f) continue;
                            float h = Mathf.PerlinNoise(x / 16f + o2, z / 16f + o1);
                            if (h > 0.55f) Metti(mappe, Scegli("monte", Mathf.PerlinNoise(x / 35f, z / 35f + o2)), cx, cz, 2 + (int)((h - 0.55f) * 8f));
                        }
                        else
                        {
                            if (rip > 28f) continue;
                            // Prati: chiazze di fiori dello stesso tipo, trifogli e margherite, qualche fiore sparso
                            float fiori = Mathf.PerlinNoise(x / 40f + o1, z / 40f + o2);
                            if (fiori > 0.6f)
                                Metti(mappe, Scegli("fiore", Mathf.PerlinNoise(x / 90f + o2, z / 90f + o1)), cx, cz, 1 + (int)((fiori - 0.6f) * 15f));
                            else if (caso1 > 0.93f)
                                Metti(mappe, Scegli("fiore", Hash(cz, cx)), cx, cz, 1);
                            if (Mathf.PerlinNoise(x / 20f + o2, z / 20f) > 0.66f) Metti(mappe, Scegli("trifoglio", 0f), cx, cz, 2);
                            if (Mathf.PerlinNoise(x / 15f, z / 15f + o1) > 0.7f) Metti(mappe, Scegli("margherita", 0f), cx, cz, 2);
                        }
                    }

                var tutti = new List<DetailPrototype>(tenuti);
                tutti.AddRange(nuovi);
                dati.detailPrototypes = tutti.ToArray();
                for (int k = 0; k < mappeTenute.Count; k++) dati.SetDetailLayer(0, 0, k, mappeTenute[k]);
                for (int k = 0; k < mappe.Length; k++) dati.SetDetailLayer(0, 0, tenuti.Count + k, mappe[k]);
                EditorUtility.SetDirty(dati);
                fatte++;
            }
            return fatte;
        }

        static void Metti(int[][,] mappe, int indice, int cx, int cz, int quanti)
        {
            if (indice < 0 || quanti <= 0) return;
            mappe[indice][cz, cx] = Mathf.Clamp(mappe[indice][cz, cx] + quanti, 0, 255);
        }

        /// <summary>
        /// Cosa c'è in un punto del villaggio (coordinate del villaggio): 0 niente dettagli (strade, edifici, piazza, orti, aie),
        /// 2 aiuola (0,8-2,2 m da una casa), 3 lungo un recinto, 4 prato, 1 fuori da tutto.
        /// </summary>
        static int StatoVillaggio(Vector2 pv, List<(Vector2 a, Vector2 b)> recinti, Vector2 mondo)
        {
            foreach (var s in PiantaVillaggio.Strade)
                if (DistPolilinea(pv, s.punti) < s.larghezza * 0.5f + 0.6f) return 0;
            float vicinoCasa = float.MaxValue;
            foreach (PiantaVillaggio.Edificio e in PiantaVillaggio.Edifici)
            {
                float d = DistRettangolo(pv, e.Impronta);
                if (d < 0.8f) return 0;
                vicinoCasa = Mathf.Min(vicinoCasa, d);
            }
            foreach (PiantaVillaggio.Lotto l in PiantaVillaggio.Lotti)
            {
                if (!l.area.Contains(pv)) continue;
                if (l.tipo == PiantaVillaggio.Tipo.Piazza || l.tipo == PiantaVillaggio.Tipo.Orti || l.tipo == PiantaVillaggio.Tipo.Addestramento ||
                    l.tipo == PiantaVillaggio.Tipo.Fabbro || l.tipo == PiantaVillaggio.Tipo.Fienile || l.tipo == PiantaVillaggio.Tipo.Torre)
                    return 0;
            }
            foreach (var r in recinti)
                if (DistPolilinea(mondo, new[] { r.a, r.b }) < 0.7f) return 3;
            if (vicinoCasa < 2.2f) return 2;
            return pv.magnitude < 110f ? 4 : 1;
        }

        // ------------------------------------------------------------------ oggetti

        static void OggettiBosco(Campi campi, Terrain[] terreni, System.Random caso, Transform gruppo, List<string> conti)
        {
            var g = Sotto(gruppo, "Bosco");
            bool NelBosco(float x, float z, float rip) => Bosco(campi, x, z) > 0.6f && rip < 32f && LiberoPerAlberi(campi, x, z, 12f);
            int n = 0;
            n += Sparsi(campi, terreni, caso, g, Lista("prefab_dead_log_01_Moss", "prefab_dead_log_02_Moss", "prefab_dead_log_03_Leaves",
                "prefab_dead_log_04_Leaves", "Prefab_Forest_pine_04_log", "Prefab_Forest_pine_08_Log", "Prefab_Forest_Pine_09_log", "SM_EuropeanBeech_Log_01"),
                120, NelBosco, 0.9f, 1.3f, 0.05f, 6f);
            n += Sparsi(campi, terreni, caso, g, Lista("prefab_beech_forest_stump_01_1_Moss", "prefab_beech_forest_stump_01_2", "prefab_beech_forest_stump_01_3",
                "prefab_Beech_forest_stump_02_01", "prefab_Beech_forest_stump_02_02", "prefab_Beech_forest_stump_02_03", "Prefab_pine_stump_01_moss",
                "Prefab_pine_stump_03_moss", "SM_EuropeanBeech_Stump_01"), 110, NelBosco, 0.9f, 1.2f, 0.1f, 4f);
            n += Sparsi(campi, terreni, caso, g, Lista("prefab_beech_old_roots_01_Moss", "prefab_beech_old_roots_02_Moss", "prefab_beech_old_roots_01_Leaves",
                "Prefab_Pine_roots_02", "Prefab_Pine_roots_04"), 45, NelBosco, 0.9f, 1.2f, 0.15f, 3f);
            n += Sparsi(campi, terreni, caso, g, Lista("prefab_Beech_mushroom_04A", "prefab_Beech_mushroom_01", "Prefab_mushroom_fly_amanita_03",
                "Prefab_mushroom_fly_amanita_02", "Prefab_mushroom_pennybun_02", "prefab_Mushroom_Lactarius_03", "Prefab_mushroom_saffron_milk_cap_01",
                "Prefab_mushroom_scarletina_bolete_03", "prefab_Mushroom_Russula_01"), 200, NelBosco, 1f, 1.5f, 0.02f, 3f);
            n += Sparsi(campi, terreni, caso, g, Lista("prefab_detail_branches_01", "prefab_detail_branches_02", "prefab_detail_branches_03",
                "prefab_beech_dry_bough_01", "prefab_beech_dry_bough_03", "Prefab_Pine_broken_branch_10", "SM_LeaveDerbis_M_01", "SM_LeaveDerbis_M_02"),
                160, NelBosco, 0.9f, 1.2f, 0.03f, 4f);
            conti.Add($"bosco {n} (tronchi, ceppi, radici, funghi, rami)");
        }

        static void OggettiFiume(Campi campi, Terrain[] terreni, System.Random caso, Transform gruppo, List<string> conti)
        {
            var g = Sotto(gruppo, "Rive");
            bool Riva(float x, float z, float lo, float hi)
            {
                float d = campi.DistanzaFiume(x, z);
                if (d < lo || d > hi || campi.DistanzaValle(x, z) > -15f || campi.DistanzaStrade(x, z) < 4f) return false;
                return !OccupatoVillaggio(new Vector2(x, z), 3f);
            }
            bool Sponda(float x, float z, float lo, float hi)
            {
                float e = Ellisse(new Vector2(x, z), ValleMappa.Lago, ValleMappa.LagoRaggi);
                return e > lo && e < hi && campi.DistanzaStrade(x, z) > 4f;
            }
            var sassi = Lista("Prefab_river_stone_04", "Prefab_river_stone_05", "Prefab_river_stone_06", "Prefab_river_stone_07",
                "Prefab_river_stone_08", "Prefab_river_stone_09", "Prefab_river_stone_10", "Prefab_river_stone_11");
            var massi = Lista("Prefab_Test_River_stone_01_Wet", "Prefab_Test_River_stone_01_Wet2");
            var salici = Lista("prefab_grey_willow_01", "prefab_grey_willow_02", "prefab_grey_willow_03", "prefab_grey_willow_04");
            int n = 0;
            n += Sparsi(campi, terreni, caso, g, sassi, 320, (x, z, r) => Riva(x, z, 5.8f, 8f), 1f, 2.2f, 0.05f, 10f);
            n += Sparsi(campi, terreni, caso, g, massi, 55, (x, z, r) => Riva(x, z, 4.5f, 6.8f), 0.7f, 1.3f, 0.35f, 12f);
            n += Sparsi(campi, terreni, caso, g, salici, 90, (x, z, r) => Riva(x, z, 8.5f, 15f) && r < 25f, 0.9f, 1.4f, 0.1f, 3f);
            n += Sparsi(campi, terreni, caso, g, sassi, 140, (x, z, r) => Sponda(x, z, 0.99f, 1.05f), 1f, 2.2f, 0.05f, 10f);
            n += Sparsi(campi, terreni, caso, g, salici, 40, (x, z, r) => Sponda(x, z, 1.07f, 1.22f) && r < 25f, 0.9f, 1.4f, 0.1f, 3f);
            conti.Add($"rive {n} (sassi, massi bagnati, salici)");
        }

        static void OggettiPrati(Campi campi, Terrain[] terreni, System.Random caso, Transform gruppo, List<string> conti)
        {
            var g = Sotto(gruppo, "Prati");
            bool Prato(float x, float z, float rip) =>
                campi.DistanzaValle(x, z) < -40f && Bosco(campi, x, z) < 0.15f && rip < 20f && LiberoPerAlberi(campi, x, z, 14f) &&
                Vector2.Distance(new Vector2(x, z), ValleMappa.Villaggio) > 100f;
            bool Margine(float x, float z, float rip)
            {
                float b = Bosco(campi, x, z);
                return b > 0.1f && b < 0.45f && rip < 20f && LiberoPerAlberi(campi, x, z, 14f);
            }
            int n = 0;
            n += Sparsi(campi, terreni, caso, g, Lista("Prefab_hazel_04_cluster", "Prefab_hazel_03", "prefab_maple_bush_01", "prefab_maple_bush_02",
                "prefab_beech_plant_01", "prefab_beech_plant_02", "Prefab_Forest_black_cherry_03", "Prefab_Forest_black_cherry_05"),
                70, Prato, 0.9f, 1.3f, 0.1f, 3f);
            n += Sparsi(campi, terreni, caso, g, Lista("Prefab_Anthill_ants_01", "Prefab_Anthill_ants_02", "Prefab_Anthill"), 25, Prato, 0.9f, 1.3f, 0.08f, 3f);
            n += Sparsi(campi, terreni, caso, g, Lista("Prefab_Log_Pile_1", "Prefab_Log_Pile_2", "prefab_Trunks"), 14, Margine, 1f, 1f, 0.05f, 2f);
            n += Sparsi(campi, terreni, caso, g, Lista("prefab_stand_for_hay"), 5,
                (x, z, r) => Ellisse(new Vector2(x, z), ValleMappa.Pascoli, ValleMappa.PascoliRaggi) < 1f && r < 15f && campi.DistanzaStrade(x, z) > 6f,
                1f, 1f, 0.05f, 2f);
            n += Sparsi(campi, terreni, caso, g, Lista("prefab_Butterfly_Single"), 24, Prato, 1f, 1f, -0.6f, 0f);
            conti.Add($"prati {n} (cespugli, formicai, cataste, fieno, farfalle)");
        }

        static void OggettiVillaggio(Campi campi, Terrain[] terreni, System.Random caso, Transform gruppo, List<string> conti)
        {
            var g = Sotto(gruppo, "Villaggio");
            var alberi = Lista("prefab_01_Poplar", "prefab_02_Poplar", "prefab_beech_tree_01", "prefab_beech_tree_02", "prefab_beech_tree_03");
            var piccoli = Lista("prefab_beech_tree_00_C", "prefab_beech_tree_00_D");
            var siepe = Lista("Prefab_hazel_02", "Prefab_hazel_03", "Prefab_hazel_04_cluster", "prefab_maple_bush_01", "prefab_beech_plant_01", "prefab_beech_plant_02");
            var posti = new List<Vector2>();
            bool Lontano(Vector2 p, float minimo)
            {
                foreach (Vector2 q in posti) if ((q - p).sqrMagnitude < minimo * minimo) return false;
                return true;
            }
            int n = 0;

            // Alberi tra le case (15-100 m dal centro) e nel cerchio che la prima semina aveva lasciato vuoto (100-165 m)
            for (int giro = 0; giro < 2; giro++)
            {
                int voluti = giro == 0 ? 32 : 55;
                float rMin = giro == 0 ? 15f : 100f, rMax = giro == 0 ? 100f : 165f, spazio = giro == 0 ? 9f : 13f;
                for (int t = 0; t < voluti * 300 && voluti > 0; t++)
                {
                    float ang = (float)caso.NextDouble() * Mathf.PI * 2f;
                    float rag = Mathf.Sqrt(Mathf.Lerp(rMin * rMin, rMax * rMax, (float)caso.NextDouble()));
                    Vector2 p = ValleMappa.Villaggio + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rag;
                    if (OccupatoVillaggio(p, 4f) || campi.DistanzaStrade(p.x, p.y) < 7f || campi.DistanzaFiume(p.x, p.y) < 15f) continue;
                    if (!Lontano(p, spazio)) continue;
                    if (Posa(terreni, g, alberi[caso.Next(alberi.Count)], p, caso, 0.9f, 1.25f, 0.1f, 2f)) { posti.Add(p); n++; voluti--; }
                }
            }

            // Frutteto a sud-ovest delle case (3 file da 4)
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 4; c++)
                {
                    Vector2 locale = new Vector2(-42f + c * 5.5f + (float)(caso.NextDouble() - 0.5), -51f - r * 4.5f + (float)(caso.NextDouble() - 0.5));
                    if (Posa(terreni, g, piccoli[caso.Next(piccoli.Count)], PiantaVillaggio.AlMondo(locale), caso, 0.9f, 1.1f, 0.1f, 2f)) n++;
                }

            // Siepi: dietro le case dei coloni, fuori dal camposanto e dal cortile del Balivo
            foreach (PiantaVillaggio.Edificio e in PiantaVillaggio.Edifici)
            {
                if (!e.lotto.StartsWith("casa")) continue;
                foreach (Vector2 punto in new[] { e.AlVillaggio(-1.4f, -7.4f), e.AlVillaggio(e.Lunghezza + 1.4f, -7.4f), e.AlVillaggio(e.Lunghezza * 0.5f, -7.8f) })
                    if (Posa(terreni, g, siepe[caso.Next(siepe.Count)], PiantaVillaggio.AlMondo(punto), caso, 0.8f, 1.1f, 0.1f, 2f)) n++;
            }
            for (float y = 26f; y <= 42f; y += 3.5f)
                if (Posa(terreni, g, siepe[caso.Next(siepe.Count)], PiantaVillaggio.AlMondo(new Vector2(-35.8f, y)), caso, 0.8f, 1.1f, 0.1f, 2f)) n++;
            for (float y = -6f; y <= 14f; y += 3.5f)
                if (Posa(terreni, g, siepe[caso.Next(siepe.Count)], PiantaVillaggio.AlMondo(new Vector2(47.6f, y)), caso, 0.8f, 1.1f, 0.1f, 2f)) n++;

            // Api sugli orti e sulle aiuole
            var api = Lista("prefab_Bees_Particle");
            if (api.Count > 0)
                foreach (Vector2 locale in new[] { new Vector2(-32f, -19.5f), new Vector2(-37f, -18f), new Vector2(-8f, -36f), new Vector2(3f, -38f) })
                    if (Posa(terreni, g, api[0], PiantaVillaggio.AlMondo(locale), caso, 1f, 1f, -0.8f, 0f)) n++;

            conti.Add($"villaggio {n} (alberi, frutteto, siepi, api)");
        }

        // ------------------------------------------------------------------ supporto

        /// <summary>Posa fino a 'numero' pezzi a caso nella valle dove 'adatto' dice sì. affonda = metri sotto terra (negativo: sollevato).</summary>
        static int Sparsi(Campi campi, Terrain[] terreni, System.Random caso, Transform g, List<GameObject> prefab, int numero,
            System.Func<float, float, float, bool> adatto, float scalaMin, float scalaMax, float affonda, float inclina)
        {
            if (prefab.Count == 0) return 0;
            int posati = 0;
            float lato = ValleMappa.Lato + 2f * ValleMappa.Anello;
            for (int t = 0; t < numero * 500 && posati < numero; t++)
            {
                float x = ValleMappa.Origine + (float)caso.NextDouble() * lato;
                float z = ValleMappa.Origine + (float)caso.NextDouble() * lato;
                Terrain ter = TerrenoIn(terreni, x, z, out Vector2 n);
                if (ter == null) continue;
                if (!adatto(x, z, ter.terrainData.GetSteepness(n.x, n.y))) continue;
                if (Posa(terreni, g, prefab[caso.Next(prefab.Count)], new Vector2(x, z), caso, scalaMin, scalaMax, affonda, inclina)) posati++;
            }
            return posati;
        }

        static bool Posa(Terrain[] terreni, Transform g, GameObject prefab, Vector2 p, System.Random caso, float scalaMin, float scalaMax, float affonda, float inclina)
        {
            Terrain ter = TerrenoIn(terreni, p.x, p.y, out _);
            if (ter == null || prefab == null) return false;
            var pezzo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
            float scala = Mathf.Lerp(scalaMin, scalaMax, (float)caso.NextDouble());
            float y = ter.SampleHeight(new Vector3(p.x, 0f, p.y)) + ter.transform.position.y;
            pezzo.transform.position = new Vector3(p.x, y - affonda * scala, p.y);
            pezzo.transform.rotation = Quaternion.Euler(((float)caso.NextDouble() - 0.5f) * inclina, (float)caso.NextDouble() * 360f,
                                                        ((float)caso.NextDouble() - 0.5f) * inclina);
            pezzo.transform.localScale = Vector3.one * scala;
            return true;
        }

        static Transform Sotto(Transform padre, string nome)
        {
            var t = new GameObject(nome).transform;
            t.SetParent(padre, false);
            return t;
        }

        /// <summary>Vero se il punto del mondo cade su un lotto, un edificio o una strada del villaggio (con un margine in metri).</summary>
        static bool OccupatoVillaggio(Vector2 mondo, float margine)
        {
            Vector2 pv = mondo - ValleMappa.Villaggio;
            if (pv.sqrMagnitude > 320f * 320f) return false;
            foreach (PiantaVillaggio.Lotto l in PiantaVillaggio.Lotti)
                if (DistRettangolo(pv, l.area) < margine) return true;
            foreach (PiantaVillaggio.Edificio e in PiantaVillaggio.Edifici)
                if (DistRettangolo(pv, e.Impronta) < margine + 1f) return true;
            foreach (var s in PiantaVillaggio.Strade)
                if (DistPolilinea(pv, s.punti) < s.larghezza * 0.5f + margine) return true;
            foreach (PiantaVillaggio.Oggetto o in PiantaVillaggio.Oggetti)
                if ((o.posizione - pv).sqrMagnitude < (margine + 1.5f) * (margine + 1.5f)) return true;
            return false;
        }

        static void CaricaPrefabNatura()
        {
            prefabNatura = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string cartella in new[] { "Assets/NatureManufacture Assets", "Assets/HIVEMIND" })
            {
                if (!AssetDatabase.IsValidFolder(cartella)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { cartella }))
                {
                    string percorso = AssetDatabase.GUIDToAssetPath(guid);
                    string nome = Path.GetFileNameWithoutExtension(percorso);
                    if (nome.StartsWith("VS_", System.StringComparison.OrdinalIgnoreCase) || prefabNatura.ContainsKey(nome)) continue;
                    prefabNatura[nome] = null;
                    prefabNatura[nome] = AssetDatabase.LoadAssetAtPath<GameObject>(percorso);
                }
            }
        }

        static GameObject PrefabNatura(string nome)
        {
            if (prefabNatura != null && prefabNatura.TryGetValue(nome, out GameObject g) && g != null) return g;
            if (!mancantiNatura.Contains(nome)) mancantiNatura.Add(nome);
            return null;
        }

        static List<GameObject> Lista(params string[] nomi)
        {
            var l = new List<GameObject>();
            foreach (string n in nomi)
            {
                GameObject g = PrefabNatura(n);
                if (g != null) l.Add(g);
            }
            return l;
        }

        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float DistRettangolo(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float DistPolilinea(Vector2 p, Vector2[] punti)
        {
            float m = float.MaxValue;
            for (int i = 0; i + 1 < punti.Length; i++)
            {
                Vector2 a = punti[i], ab = punti[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                m = Mathf.Min(m, Vector2.Distance(p, a + ab * t));
            }
            return m;
        }
    }
}
