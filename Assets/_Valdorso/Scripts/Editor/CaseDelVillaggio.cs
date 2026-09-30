using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Villaggio → Aggiungi le case del villaggio (6.7a, pianta approvata da Fra il 01/10).
    /// Gli edifici chiave (tempio, Balivo, locanda, Gilda, fabbro, mulino, case dei coloni) restano quelli del costruttore
    /// del villaggio, con gli interni. Questo strumento aggiunge intorno le case "già pronte" dei pacchetti Hivemind
    /// (Modular Medieval Town, House Forge, il kit del villaggio): si vedono complete da fuori ma non si entra.
    /// Le posa lungo le strade del villaggio (PiantaVillaggio.Strade), affacciate sulla strada, una accanto all'altra:
    /// - vicino alla piazza case di pietra e da città, più in là case di legno, ai margini case di paglia;
    /// - mai sopra un lotto della pianta (edifici chiave, orti, camposanto, lotti liberi per crescere), una strada,
    ///   l'acqua o un pendio troppo ripido;
    /// - il terreno sotto ogni casa viene spianato con un raccordo morbido; alberi e rocce lì sotto vengono tolti.
    /// Aggiunge anche i lampioni lungo le strade. Si può rilanciare: rifà tutto da capo (con lo stesso seme, stesse case).
    /// Se una casa guarda dalla parte sbagliata, si corregge il suo verso nella tabella Davanti qui sotto.
    /// </summary>
    public static class CaseDelVillaggio
    {
        const string CartellaKit = "Assets/HIVEMIND";
        const string NomeRadice = "Case del villaggio";
        const int Seme = 312;                 // l'anno della valle: stesso seme, stesso villaggio
        const float Scarto = 2f;              // metri liberi tra una casa e l'altra
        const float DallaStrada = 2.5f;       // metri tra il bordo della strada e la facciata
        const float RaggioMassimo = 165f;     // oltre questa distanza dalla piazza non si costruisce
        const float SecondaFila = 0f;         // seconda fila spenta (01/10, Fra: le case dietro non hanno una via); tornerà con i vicoli veri
        const float Vicolo = 5f;              // il vicolo tra la prima e la seconda fila
        const float PendioMassimo = 4f;       // dislivello massimo sotto una casa (m)
        const float Raccordo = 4f;            // raccordo del terreno spianato
        const float PassoLampioni = 24f;

        // Le case per anello: vicino alla piazza, nel villaggio, ai margini
        static readonly string[] Centro =
        {
            "SM_House01", "SM_House02", "SM_House03", "SM_House04", "SM_Merged_House_01", "SM_Merged_House_02",
            "SM_Merged_House_03", "SM_Merged_House_05", "SM_Merged_House_09", "PF_StoneHouse01_JustBuilding", "PF_StoneHouse02"
        };
        static readonly string[] Villaggio =
        {
            "PF_WoodenHouse01_JustBuilding", "PF_WoodenHouse02", "PF_WoodenHouse03", "PF_House_A", "PF_House_B", "PF_House_C",
            "SM_Merged_House_04", "SM_Merged_House_06", "SM_Merged_House_07", "SM_Merged_House_08", "SM_Merged_House_10"
        };
        static readonly string[] Margini =
        {
            "PF_PrimitiveHouse01", "PF_PrimitiveHouse02", "PF_PrimitiveHouse03", "PF_PrimitiveHouse04", "PF_WoodenHouse02", "PF_House_B"
        };

        /// <summary>
        /// Da che parte ha la porta ogni modello, in gradi rispetto al suo +Z (0 = la porta guarda verso +Z).
        /// Si ritocca guardando la scena se una casa dà le spalle alla strada.
        /// </summary>
        static readonly Dictionary<string, float> Davanti = new Dictionary<string, float>
        {
            // Le case di città (tetto arancione, Modular Medieval Town) hanno la porta sul retro del modello (segnalato da Fra il 01/10)
            { "SM_House01", 180f }, { "SM_House02", 180f }, { "SM_House03", 180f }, { "SM_House04", 180f },
            { "SM_Merged_House_01", 180f }, { "SM_Merged_House_02", 180f }, { "SM_Merged_House_03", 180f },
            { "SM_Merged_House_04", 180f }, { "SM_Merged_House_05", 180f }, { "SM_Merged_House_06", 180f },
            { "SM_Merged_House_07", 180f }, { "SM_Merged_House_08", 180f }, { "SM_Merged_House_09", 180f },
            { "SM_Merged_House_10", 180f },
        };

        static Vector2[] fiume; // il corso del fiume, in coordinate del villaggio

        const string FileVersi = "Assets/_Valdorso/Mondo/Valle/VersiDelleCase.json";

        [System.Serializable] class Verso { public string modello; public float gradi; }
        [System.Serializable] class Versi { public List<Verso> elenco = new List<Verso>(); }

        /// <summary>Il verso della porta di un modello: quello scritto con "Gira questo modello", altrimenti la tabella Davanti.</summary>
        static float VersoDi(string modello)
        {
            foreach (Verso v in LeggiVersi().elenco) if (v.modello == modello) return v.gradi;
            return Davanti.TryGetValue(modello, out float d) ? d : 0f;
        }

        static Versi LeggiVersi()
        {
            if (!File.Exists(FileVersi)) return new Versi();
            return JsonUtility.FromJson<Versi>(File.ReadAllText(FileVersi)) ?? new Versi();
        }

        /// <summary>
        /// Valdorso → Villaggio → Gira questo modello di 90° (Ctrl+Alt+G): si seleziona nella scena una casa che guarda
        /// dalla parte sbagliata e si lancia finché la porta non guarda la strada. Gira subito tutte le case dello stesso
        /// modello e si ricorda il verso (VersiDelleCase.json), così rilanciando lo strumento restano giuste.
        /// </summary>
        [MenuItem("Valdorso/Villaggio/Gira questo modello di 90° %&g")]
        static void GiraModello()
        {
            GameObject scelta = Selection.activeGameObject;
            GameObject radice = scelta != null ? PrefabUtility.GetOutermostPrefabInstanceRoot(scelta) : null;
            GameObject sorgente = radice != null ? PrefabUtility.GetCorrespondingObjectFromSource(radice) : null;
            if (sorgente == null || radice.transform.parent == null || radice.transform.parent.parent == null
                || radice.transform.parent.parent.name != NomeRadice)
            {
                EditorUtility.DisplayDialog("Valdorso", "Seleziona nella scena una delle case del villaggio (sotto \"" + NomeRadice + "\").", "OK");
                return;
            }
            string modello = sorgente.name;
            Versi versi = LeggiVersi();
            Verso v = versi.elenco.Find(x => x.modello == modello);
            if (v == null) { v = new Verso { modello = modello, gradi = Davanti.TryGetValue(modello, out float d) ? d : 0f }; versi.elenco.Add(v); }
            v.gradi = Mathf.Repeat(v.gradi + 90f, 360f);
            File.WriteAllText(FileVersi, JsonUtility.ToJson(versi, true));
            AssetDatabase.ImportAsset(FileVersi);

            // Gira subito tutte le case di quel modello, attorno al loro centro
            int n = 0;
            foreach (Transform casa in radice.transform.parent)
            {
                GameObject src = PrefabUtility.GetCorrespondingObjectFromSource(casa.gameObject);
                if (src == null || src.name != modello) continue;
                Undo.RecordObject(casa, "Gira il modello");
                Vector3 centro = Riquadro(casa, out Bounds b) ? b.center : casa.position;
                casa.RotateAround(new Vector3(centro.x, casa.position.y, centro.z), Vector3.up, 90f);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(radice.scene);
            Debug.Log($"[Valdorso] {modello}: girato di 90° ({n} case), verso ricordato {v.gradi:0}°.");
        }

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        static readonly List<string> mancanti = new List<string>();

        struct Scatola
        {
            public Vector2 centro, asseX, asseZ;
            public float mezzaX, mezzaZ;
            public bool Contiene(Vector2 p, float margine)
            {
                Vector2 d = p - centro;
                return Mathf.Abs(Vector2.Dot(d, asseX)) <= mezzaX + margine && Mathf.Abs(Vector2.Dot(d, asseZ)) <= mezzaZ + margine;
            }
            public IEnumerable<Vector2> Punti(float margine)
            {
                for (int i = -2; i <= 2; i++)
                    for (int j = -2; j <= 2; j++)
                        yield return centro + asseX * ((mezzaX + margine) * i / 2f) + asseZ * ((mezzaZ + margine) * j / 2f);
            }
        }

        [MenuItem("Valdorso/Villaggio/Aggiungi le case del villaggio")]
        static void Aggiungi()
        {
            GameObject valle = GameObject.Find("/Valle");
            if (valle == null || Terrain.activeTerrains.Length == 0)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Valle.", "OK");
                return;
            }
            cache.Clear();
            mancanti.Clear();
            var caso = new System.Random(Seme);
            var punti = ValleMappa.CampionaFiume(8);
            fiume = new Vector2[punti.Count];
            for (int i = 0; i < punti.Count; i++) fiume[i] = punti[i] - ValleMappa.Villaggio;

            Transform vecchio = valle.transform.Find(NomeRadice);
            if (vecchio != null) Undo.DestroyObjectImmediate(vecchio.gameObject);
            var radice = new GameObject(NomeRadice).transform;
            Undo.RegisterCreatedObjectUndo(radice.gameObject, NomeRadice);
            radice.SetParent(valle.transform, false);
            var gruppoCase = Gruppo(radice, "Case");
            var gruppoLampioni = Gruppo(radice, "Lampioni");

            var posate = new List<Scatola>();
            var conteggio = new Dictionary<string, int>();
            int rifiutate = 0;

            // ---- Le case, strada per strada, da una parte e dall'altra
            foreach (var strada in PiantaVillaggio.Strade)
            {
                if (strada.punti.Length < 2 || strada.punti[0].magnitude > 160f) continue; // solo le strade del villaggio
                foreach (int lato in new[] { 1, -1 })
                {
                    float lunghezza = Lunghezza(strada.punti);
                    float s = 14f; // si comincia un po' lontano dalla piazza
                    while (s < lunghezza - 4f)
                    {
                        Vector2 p = PuntoA(strada.punti, s, out Vector2 dir);
                        Vector2 normale = new Vector2(-dir.y, dir.x) * lato;
                        float daPiazza = p.magnitude;
                        if (daPiazza > RaggioMassimo) break;

                        string[] scelta = daPiazza < 70f ? Centro : daPiazza < 130f ? Villaggio : Margini;
                        string nome = scelta[caso.Next(scelta.Length)];
                        GameObject prefab = Carica(nome);
                        if (prefab == null) { s += 6f; continue; }

                        // Misura del modello dritto: larghezza lungo la strada, profondità verso l'interno
                        Vector3 dim = Misura(prefab);
                        float larga = dim.x, profonda = dim.z;
                        Vector2 centro = p + normale * (strada.larghezza * 0.5f + DallaStrada + profonda * 0.5f) + dir * (larga * 0.5f);
                        var box = new Scatola { centro = centro, asseX = dir, asseZ = normale, mezzaX = larga * 0.5f, mezzaZ = profonda * 0.5f };

                        string motivo = Controlla(box, posate);
                        if (motivo != null) { rifiutate++; s += 4f; continue; }

                        // La porta verso la strada: il +Z del modello guarda verso -normale
                        Vector2 guarda = -normale;
                        float yaw = Mathf.Atan2(guarda.x, guarda.y) * Mathf.Rad2Deg + VersoDi(nome);
                        float quota = Spiana(box);
                        TogliNatura(valle.transform, box);
                        Posa(gruppoCase, prefab, box.centro, quota - 0.25f, yaw);

                        posate.Add(box);
                        conteggio[nome] = conteggio.TryGetValue(nome, out int c) ? c + 1 : 1;

                        // Vicino alla piazza il villaggio è più fitto: dietro, oltre un vicolo, una seconda casa
                        if (daPiazza < SecondaFila)
                        {
                            string nome2 = Villaggio[caso.Next(Villaggio.Length)];
                            GameObject prefab2 = Carica(nome2);
                            if (prefab2 != null)
                            {
                                Vector3 dim2 = Misura(prefab2);
                                Vector2 centro2 = box.centro + normale * (profonda * 0.5f + Vicolo + dim2.z * 0.5f);
                                var box2 = new Scatola { centro = centro2, asseX = dir, asseZ = normale, mezzaX = dim2.x * 0.5f, mezzaZ = dim2.z * 0.5f };
                                if (Controlla(box2, posate) == null)
                                {
                                    // Guarda verso il vicolo, cioè verso la strada come la casa davanti
                                    float yaw2 = Mathf.Atan2(guarda.x, guarda.y) * Mathf.Rad2Deg + VersoDi(nome2);
                                    float quota2 = Spiana(box2);
                                    TogliNatura(valle.transform, box2);
                                    Posa(gruppoCase, prefab2, box2.centro, quota2 - 0.25f, yaw2);
                                    posate.Add(box2);
                                    conteggio[nome2] = conteggio.TryGetValue(nome2, out int c2) ? c2 + 1 : 1;
                                }
                            }
                        }
                        s += larga + Scarto;
                    }
                }
            }

            // ---- I lampioni, lungo le strade, alternati da una parte e dall'altra
            int lampioni = 0;
            GameObject lampione = Carica("SM_street_lamp_01");
            if (lampione != null)
                foreach (var strada in PiantaVillaggio.Strade)
                {
                    if (strada.punti.Length < 2 || strada.punti[0].magnitude > 160f) continue;
                    float lunghezza = Lunghezza(strada.punti);
                    int k = 0;
                    for (float s = 10f; s < lunghezza; s += PassoLampioni, k++)
                    {
                        Vector2 p = PuntoA(strada.punti, s, out Vector2 dir);
                        if (p.magnitude > RaggioMassimo) break;
                        Vector2 normale = new Vector2(-dir.y, dir.x) * (k % 2 == 0 ? 1 : -1);
                        Vector2 punto = p + normale * (strada.larghezza * 0.5f + 0.8f);
                        bool occupato = false;
                        foreach (Scatola b in posate) if (b.Contiene(punto, 0.5f)) { occupato = true; break; }
                        if (occupato) continue;
                        float yaw = Mathf.Atan2(-normale.x, -normale.y) * Mathf.Rad2Deg;
                        Transform l = Posa(gruppoLampioni, lampione, punto, Altezza(punto), yaw);
                        var luce = new GameObject("Luce del lampione").AddComponent<Light>();
                        luce.transform.SetParent(l, false);
                        luce.transform.localPosition = new Vector3(0f, 3.1f, 0f);
                        luce.type = LightType.Point;
                        luce.color = new Color(1f, 0.7f, 0.4f);
                        luce.intensity = 1.5f;
                        luce.range = 9f;
                        luce.shadows = LightShadows.None;
                        lampioni++;
                    }
                }

            EditorSceneManager.MarkSceneDirty(valle.scene);
            int totale = 0;
            string elenco = "";
            foreach (var kv in conteggio) { totale += kv.Value; elenco += $"\n  {kv.Key}: {kv.Value}"; }
            string rapporto = $"{totale} case posate, {lampioni} lampioni ({rifiutate} posti scartati perché occupati).";
            if (mancanti.Count > 0) rapporto += "\nNon trovati: " + string.Join(", ", mancanti);
            Debug.Log("[Valdorso] Case del villaggio: " + rapporto + elenco);
            EditorUtility.DisplayDialog("Valdorso", "Case del villaggio aggiunte.\n\n" + rapporto + "\n\nL'elenco per modello è in Console.\nRicorda: Ctrl+S sulla scena.", "OK");
        }

        // ------------------------------------------------------------------ controlli

        /// <summary>Null se la casa ci sta; altrimenti il motivo per cui no.</summary>
        static string Controlla(Scatola box, List<Scatola> posate)
        {
            if (box.centro.magnitude > RaggioMassimo) return "lontana";
            foreach (Vector2 q in box.Punti(1.5f))
            {
                foreach (PiantaVillaggio.Lotto l in PiantaVillaggio.Lotti)
                    if (Espandi(l.area, 2f).Contains(q)) return "lotto " + l.id;
                foreach (PiantaVillaggio.Edificio e in PiantaVillaggio.Edifici)
                    if (Espandi(e.Impronta, 2f).Contains(q)) return "edificio";
                foreach (var strada in PiantaVillaggio.Strade)
                    if (DistanzaLinea(q, strada.punti) < strada.larghezza * 0.5f + 0.5f) return "strada";
                foreach (Scatola b in posate) if (b.Contiene(q, Scarto * 0.5f)) return "casa";
                if (fiume != null && DistanzaLinea(q, fiume) < 16f) return "fiume";
            }
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector2 q in box.Punti(0f)) { float h = Altezza(q); min = Mathf.Min(min, h); max = Mathf.Max(max, h); }
            if (max - min > PendioMassimo) return "pendio";
            return null;
        }

        static Rect Espandi(Rect r, float m) => Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);

        // ------------------------------------------------------------------ terreno e natura

        /// <summary>Spiana il terreno sotto la casa alla quota media, con un raccordo morbido intorno. Restituisce la quota.</summary>
        static float Spiana(Scatola box)
        {
            float somma = 0f; int n = 0;
            foreach (Vector2 q in box.Punti(0f)) { somma += Altezza(q); n++; }
            float quota = somma / n;
            float raggio = Mathf.Sqrt(box.mezzaX * box.mezzaX + box.mezzaZ * box.mezzaZ) + Raccordo + 2f;
            Vector2 mondoCentro = PiantaVillaggio.AlMondo(box.centro);

            foreach (Terrain t in Terrain.activeTerrains)
            {
                TerrainData d = t.terrainData;
                Vector3 pos = t.GetPosition(), dimT = d.size;
                int res = d.heightmapResolution;
                float passo = dimT.x / (res - 1);
                int x0 = Mathf.Clamp(Mathf.FloorToInt((mondoCentro.x - raggio - pos.x) / passo), 0, res - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((mondoCentro.x + raggio - pos.x) / passo), 0, res - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((mondoCentro.y - raggio - pos.z) / passo), 0, res - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((mondoCentro.y + raggio - pos.z) / passo), 0, res - 1);
                if (x1 <= x0 || z1 <= z0) continue;
                Undo.RegisterCompleteObjectUndo(d, "Spiana le case");
                float[,] h = d.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
                for (int j = 0; j <= z1 - z0; j++)
                    for (int i = 0; i <= x1 - x0; i++)
                    {
                        var mondo = new Vector2(pos.x + (x0 + i) * passo, pos.z + (z0 + j) * passo);
                        Vector2 q = mondo - ValleMappa.Villaggio;
                        Vector2 dq = q - box.centro;
                        float fx = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(dq, box.asseX)) - box.mezzaX - 1f);
                        float fz = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(dq, box.asseZ)) - box.mezzaZ - 1f);
                        float fuori = Mathf.Sqrt(fx * fx + fz * fz);
                        if (fuori >= Raccordo) continue;
                        float peso = 1f - Liscio(fuori / Raccordo);
                        h[j, i] = Mathf.Lerp(h[j, i], (quota - pos.y) / dimT.y, peso);
                    }
                d.SetHeights(x0, z0, h);
            }
            return quota;
        }

        static void TogliNatura(Transform valle, Scatola box)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                TerrainData d = t.terrainData;
                Vector3 pos = t.GetPosition(), dim = d.size;
                var restano = new List<TreeInstance>();
                bool tolto = false;
                foreach (TreeInstance albero in d.treeInstances)
                {
                    Vector3 w = pos + Vector3.Scale(albero.position, dim);
                    if (box.Contiene(new Vector2(w.x, w.z) - ValleMappa.Villaggio, 3f)) { tolto = true; continue; }
                    restano.Add(albero);
                }
                if (tolto)
                {
                    Undo.RecordObject(d, "Alberi sotto le case");
                    d.treeInstances = restano.ToArray();
                }
            }
            Transform natura = valle.Find("Natura");
            if (natura == null) return;
            var via = new List<GameObject>();
            foreach (Transform t in natura.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                if (box.Contiene(new Vector2(t.position.x, t.position.z) - ValleMappa.Villaggio, 2f)) via.Add(t.gameObject);
            }
            foreach (GameObject g in via) Undo.DestroyObjectImmediate(g);
        }

        // ------------------------------------------------------------------ pezzi

        static Transform Gruppo(Transform radice, string nome)
        {
            var g = new GameObject(nome).transform;
            g.SetParent(radice, false);
            return g;
        }

        /// <summary>Posa un pezzo con il centro della base in 'punto' (coordinate del villaggio) e la base alla quota data.</summary>
        static Transform Posa(Transform genitore, GameObject prefab, Vector2 punto, float quota, float gradiY)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, genitore);
            Vector2 mondo = PiantaVillaggio.AlMondo(punto);
            go.transform.rotation = Quaternion.Euler(0f, gradiY, 0f);
            go.transform.position = new Vector3(mondo.x, quota, mondo.y);
            if (Riquadro(go.transform, out Bounds b))
                go.transform.position += new Vector3(mondo.x - b.center.x, quota - b.min.y, mondo.y - b.center.z);
            return go.transform;
        }

        /// <summary>Larghezza (x), altezza (y) e profondità (z) del modello dritto.</summary>
        static Vector3 Misura(GameObject prefab)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Vector3 dim = Riquadro(go.transform, out Bounds b) ? b.size : new Vector3(8f, 8f, 8f);
            Object.DestroyImmediate(go);
            return dim;
        }

        static bool Riquadro(Transform radice, out Bounds riquadro)
        {
            bool trovato = false;
            riquadro = new Bounds();
            foreach (Renderer r in radice.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!trovato) { riquadro = r.bounds; trovato = true; }
                else riquadro.Encapsulate(r.bounds);
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
                if (percorso.Contains("HDRP")) { prefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(percorso); continue; } // meglio la versione URP, se c'è
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(percorso);
                break;
            }
            if (prefab == null && !mancanti.Contains(nome)) mancanti.Add(nome);
            cache[nome] = prefab;
            return prefab;
        }

        // ------------------------------------------------------------------ geometria

        static float Altezza(Vector2 locale)
        {
            Vector2 m = PiantaVillaggio.AlMondo(locale);
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 pos = t.GetPosition(), dim = t.terrainData.size;
                if (m.x >= pos.x && m.x <= pos.x + dim.x && m.y >= pos.z && m.y <= pos.z + dim.z)
                    return pos.y + t.SampleHeight(new Vector3(m.x, 0f, m.y));
            }
            return 0f;
        }

        static float Lunghezza(Vector2[] punti)
        {
            float l = 0f;
            for (int i = 0; i + 1 < punti.Length; i++) l += Vector2.Distance(punti[i], punti[i + 1]);
            return l;
        }

        static Vector2 PuntoA(Vector2[] punti, float s, out Vector2 dir)
        {
            for (int i = 0; i + 1 < punti.Length; i++)
            {
                float l = Vector2.Distance(punti[i], punti[i + 1]);
                dir = (punti[i + 1] - punti[i]).normalized;
                if (s <= l) return punti[i] + dir * s;
                s -= l;
            }
            dir = (punti[punti.Length - 1] - punti[punti.Length - 2]).normalized;
            return punti[punti.Length - 1];
        }

        static float DistanzaLinea(Vector2 p, Vector2[] punti)
        {
            float d = float.MaxValue;
            for (int i = 0; i + 1 < punti.Length; i++)
            {
                Vector2 a = punti[i], b = punti[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, Vector2.Distance(p, a + ab * t));
            }
            return d;
        }

        static float Liscio(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}