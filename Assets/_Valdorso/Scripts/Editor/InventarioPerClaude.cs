using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Prove → Inventario per Claude: scrive in un file di testo quello che Claude non può vedere
    /// (i pacchetti sono esclusi da GitHub), così lavora sui nomi veri invece di tirare a indovinare.
    /// Contiene: cartelle dei pacchetti con quanti prefab, materiali e texture hanno; tutti i file che parlano di
    /// erba, rocce, monti, strade, fango, ciottoli, lastricati, scale (con la risoluzione delle texture);
    /// impostazioni dei terreni (strati, erba e distanze) e quanta erba c'è vicino al punto di partenza;
    /// le ricette della valle; le impostazioni della grafica; gli oggetti principali della scena.
    /// Il file finisce in Logs/Inventario_Claude.txt (cartella esclusa da GitHub) e si apre la cartella:
    /// basta trascinarlo nella chat. Va lanciato con la scena Valle aperta. Solo editor.
    /// </summary>
    public static class InventarioPerClaude
    {
        static readonly string[] Parole =
        {
            "grass", "erba", "meadow", "mountain", "rock", "cliff", "boulder", "scree", "road", "path", "dirt", "mud",
            "gravel", "cobble", "pavement", "paving", "stonepattern", "stone_floor", "pebble", "puddle", "decal",
            "stair", "step", "ladder", "ground", "soil", "moss"
        };
        static readonly string[] Pacchetti = { "Assets/NatureManufacture Assets", "Assets/Hivemind" };
        const string RicettaNaturaPath = "Assets/_Valdorso/Mondo/Valle/RicettaNatura.asset";
        const string RicettaVallePath = "Assets/_Valdorso/Mondo/Valle/RicettaValle.asset";
        const int MaxRighe = 4000;
        static readonly CultureInfo ci = CultureInfo.InvariantCulture;

        class Conta { public int prefab, materiali, texture, mesh, strati, shader; }

        [MenuItem("Valdorso/Prove/Inventario per Claude")]
        static void Scrivi()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK");
                return;
            }
            var t = new StringBuilder();
            try
            {
                t.AppendLine("INVENTARIO PER CLAUDE - " + System.DateTime.Now.ToString("dd/MM/yyyy HH:mm", ci));
                t.AppendLine("Unity " + Application.unityVersion + ", scena aperta: "
                             + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

                string[] tutti = AssetDatabase.GetAllAssetPaths();
                Cartelle(t, tutti);
                FileUtili(t, tutti);
                Terreni(t);
                Ricette(t);
                Grafica(t);
                Scena(t);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string cartella = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(cartella);
            string file = Path.Combine(cartella, "Inventario_Claude.txt");
            File.WriteAllText(file, t.ToString(), new UTF8Encoding(false));
            EditorUtility.RevealInFinder(file);
            Debug.Log("[Valdorso] Inventario per Claude scritto in " + file + ": trascinalo nella chat.");
        }

        // 1. Le cartelle, con quanti file di ogni tipo contengono (anche nelle sottocartelle)
        static void Cartelle(StringBuilder t, string[] tutti)
        {
            t.AppendLine();
            t.AppendLine("== 1. CARTELLE (Assets fino a 3 livelli, pacchetti fino a 4) ==");
            t.AppendLine("formato: cartella  [prefab / materiali / texture / modelli / strati terreno / shader]");
            var conte = new SortedDictionary<string, Conta>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string p in tutti)
            {
                if (!p.StartsWith("Assets/") || AssetDatabase.IsValidFolder(p)) continue;
                string tipo = Tipo(p);
                if (tipo == null) continue;
                string[] pezzi = p.Split('/');
                string cammino = "Assets";
                for (int i = 1; i < pezzi.Length - 1 && i <= 4; i++)
                {
                    cammino += "/" + pezzi[i];
                    if (!conte.TryGetValue(cammino, out Conta c)) { c = new Conta(); conte[cammino] = c; }
                    switch (tipo)
                    {
                        case "prefab": c.prefab++; break;
                        case "materiale": c.materiali++; break;
                        case "texture": c.texture++; break;
                        case "modello": c.mesh++; break;
                        case "strato": c.strati++; break;
                        case "shader": c.shader++; break;
                    }
                }
            }
            foreach (var kv in conte)
            {
                int livello = kv.Key.Split('/').Length - 1;
                bool pacchetto = false;
                foreach (string pk in Pacchetti) if (kv.Key.StartsWith(pk)) pacchetto = true;
                if (livello > (pacchetto ? 4 : 3)) continue;
                Conta c = kv.Value;
                t.AppendLine(new string(' ', (livello - 1) * 2) + kv.Key.Substring(kv.Key.LastIndexOf('/') + 1)
                             + $"  [{c.prefab} / {c.materiali} / {c.texture} / {c.mesh} / {c.strati} / {c.shader}]");
            }
        }

        // 2. I file che servono per erba, monti, strade e piazza
        static void FileUtili(StringBuilder t, string[] tutti)
        {
            t.AppendLine();
            t.AppendLine("== 2. FILE CON PAROLE UTILI (" + string.Join(", ", Parole) + ") ==");
            t.AppendLine("formato: tipo  percorso  [risoluzione della texture]");
            var righe = new List<string>();
            for (int i = 0; i < tutti.Length; i++)
            {
                string p = tutti[i];
                if (!p.StartsWith("Assets/") || p.StartsWith("Assets/_Valdorso/Scripts")) continue;
                if (i % 500 == 0) EditorUtility.DisplayProgressBar("Inventario per Claude", "Cerco i file utili...", (float)i / tutti.Length);
                string tipo = Tipo(p);
                if (tipo == null) continue;
                string minuscolo = p.ToLowerInvariant();
                bool utile = false;
                foreach (string parola in Parole) if (minuscolo.Contains(parola)) { utile = true; break; }
                if (!utile) continue;
                string extra = "";
                if (tipo == "texture" && AssetImporter.GetAtPath(p) is TextureImporter ti)
                {
                    ti.GetSourceTextureWidthAndHeight(out int w, out int h);
                    extra = "  " + w + "x" + h;
                }
                righe.Add(tipo + "  " + p + extra);
            }
            righe.Sort(System.StringComparer.OrdinalIgnoreCase);
            if (righe.Count > MaxRighe)
            {
                t.AppendLine("(trovati " + righe.Count + ", elencati i primi " + MaxRighe + ")");
                righe.RemoveRange(MaxRighe, righe.Count - MaxRighe);
            }
            foreach (string r in righe) t.AppendLine(r);
        }

        // 3. I terreni della scena aperta
        static void Terreni(StringBuilder t)
        {
            t.AppendLine();
            t.AppendLine("== 3. TERRENI DELLA SCENA ==");
            Terrain[] terreni = Terrain.activeTerrains;
            if (terreni.Length == 0) { t.AppendLine("(nessun terreno: aprire la scena Valle)"); return; }
            System.Array.Sort(terreni, (a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (Terrain tr in terreni)
            {
                TerrainData d = tr.terrainData;
                t.AppendLine(string.Format(ci,
                    "{0}: pos {1}, misura {2}, altezze {3}, erba {4} (per blocco {5}), distanza erba {6}, densita erba {7:0.##}, " +
                    "distanza alberi {8}, basemap {9}, errore pixel {10}, materiale {11}, strati {12}, tipi d'erba {13}",
                    tr.name, tr.transform.position, d.size, d.heightmapResolution, d.detailResolution,
                    d.detailResolutionPerPatch, tr.detailObjectDistance, tr.detailObjectDensity, tr.treeDistance,
                    tr.basemapDistance, tr.heightmapPixelError,
                    tr.materialTemplate != null ? tr.materialTemplate.name + " (" + tr.materialTemplate.shader.name + ")" : "di serie",
                    d.terrainLayers.Length, d.detailPrototypes.Length));
            }

            TerrainData primo = terreni[0].terrainData;
            t.AppendLine();
            t.AppendLine("Strati del terreno (dal primo, " + terreni[0].name + "):");
            foreach (TerrainLayer s in primo.terrainLayers)
            {
                if (s == null) { t.AppendLine("  (vuoto)"); continue; }
                t.AppendLine(string.Format(ci, "  {0}: colore {1} {2}, normale {3} {4}, maschera {5} {6}, ripetizione {7} m, metallico {8}, lucido {9}",
                    s.name, Nome(s.diffuseTexture), Misura(s.diffuseTexture), Nome(s.normalMapTexture), Misura(s.normalMapTexture),
                    Nome(s.maskMapTexture), Misura(s.maskMapTexture), s.tileSize, s.metallic, s.smoothness));
            }
            t.AppendLine("Tipi d'erba (dal primo):");
            DetailPrototype[] proto = primo.detailPrototypes;
            for (int i = 0; i < proto.Length; i++)
            {
                DetailPrototype p = proto[i];
                string nome = p.usePrototypeMesh ? Nome(p.prototype) : Nome(p.prototypeTexture);
                t.AppendLine(string.Format(ci, "  {0}. {1}: {2}, instancing {3}, larghezza {4:0.##}-{5:0.##}, altezza {6:0.##}-{7:0.##}, densita {8:0.##}",
                    i, nome, p.renderMode, p.useInstancing ? "si" : "no", p.minWidth, p.maxWidth, p.minHeight, p.maxHeight, p.density));
            }

            // Quanta erba c'è nel terreno del punto di partenza
            GameObject punto = GameObject.Find("/PuntoDiPartenza");
            if (punto == null) return;
            Vector3 pp = punto.transform.position;
            foreach (Terrain tr in terreni)
            {
                Vector3 o = tr.transform.position;
                Vector3 m = tr.terrainData.size;
                if (pp.x < o.x || pp.x >= o.x + m.x || pp.z < o.z || pp.z >= o.z + m.z) continue;
                TerrainData d = tr.terrainData;
                int r = d.detailResolution;
                t.AppendLine();
                t.AppendLine(string.Format(ci, "Erba nel terreno del punto di partenza ({0}), cella di {1:0.##} m:", tr.name, m.x / r));
                for (int i = 0; i < d.detailPrototypes.Length; i++)
                {
                    int[,] strato = d.GetDetailLayer(0, 0, r, r, i);
                    long somma = 0;
                    int piene = 0;
                    foreach (int v in strato) { somma += v; if (v > 0) piene++; }
                    t.AppendLine(string.Format(ci, "  tipo {0}: totale {1}, celle con erba {2:0.#}%, media nelle celle piene {3:0.#}",
                        i, somma, 100f * piene / (r * r), piene > 0 ? (float)somma / piene : 0f));
                }
                break;
            }
        }

        // 4. Le ricette della valle (tutti i numeri e i pacchetti scelti)
        static void Ricette(StringBuilder t)
        {
            foreach (string percorso in new[] { RicettaNaturaPath, RicettaVallePath })
            {
                t.AppendLine();
                t.AppendLine("== 4. RICETTA " + percorso + " ==");
                Object o = AssetDatabase.LoadAssetAtPath<Object>(percorso);
                if (o == null) { t.AppendLine("(non trovata)"); continue; }
                var so = new SerializedObject(o);
                SerializedProperty p = so.GetIterator();
                bool entra = true;
                while (p.NextVisible(entra))
                {
                    entra = true;
                    if (p.name == "m_Script" || p.propertyType == SerializedPropertyType.ArraySize) continue;
                    t.AppendLine(new string(' ', p.depth * 2) + p.displayName + ": " + Valore(p));
                }
            }
        }

        // 5. La grafica
        static void Grafica(StringBuilder t)
        {
            t.AppendLine();
            t.AppendLine("== 5. GRAFICA ==");
            t.AppendLine("Qualita: " + QualitySettings.names[QualitySettings.GetQualityLevel()]
                         + ", lod bias " + QualitySettings.lodBias.ToString(ci)
                         + ", vsync " + QualitySettings.vSyncCount);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                t.AppendLine(string.Format(ci, "URP {0}: scala {1}, MSAA {2}, HDR {3}, ombre fino a {4} m, ombra del sole {5}, colore {6}",
                    urp.name, urp.renderScale, urp.msaaSampleCount, urp.supportsHDR ? "si" : "no",
                    urp.shadowDistance, urp.mainLightShadowmapResolution, urp.colorGradingMode));
            }
            t.AppendLine(string.Format(ci, "Cielo {0}, luce ambiente {1}, nebbia {2} {3} {4:0.####}",
                RenderSettings.skybox != null ? RenderSettings.skybox.name : "nessuno", RenderSettings.ambientMode,
                RenderSettings.fog ? "accesa" : "spenta", RenderSettings.fogMode, RenderSettings.fogDensity));
            if (RenderSettings.sun != null)
                t.AppendLine(string.Format(ci, "Sole {0}: rotazione {1}, intensita {2}, colore {3}",
                    RenderSettings.sun.name, RenderSettings.sun.transform.eulerAngles, RenderSettings.sun.intensity, RenderSettings.sun.color));
        }

        // 6. Gli oggetti principali della scena
        static void Scena(StringBuilder t)
        {
            t.AppendLine();
            t.AppendLine("== 6. SCENA: figli di /Valle (e i loro figli, fino a 40) ==");
            GameObject valle = GameObject.Find("/Valle");
            if (valle == null) { t.AppendLine("(manca /Valle: aprire la scena Valle)"); return; }
            foreach (Transform figlio in valle.transform)
            {
                t.AppendLine(figlio.name + " (" + figlio.childCount + " figli)");
                int n = 0;
                foreach (Transform nipote in figlio)
                {
                    if (n++ >= 40) { t.AppendLine("  ..."); break; }
                    t.AppendLine("  " + nipote.name + (nipote.childCount > 0 ? " (" + nipote.childCount + ")" : ""));
                }
            }
        }

        static string Tipo(string p)
        {
            string e = Path.GetExtension(p).ToLowerInvariant();
            switch (e)
            {
                case ".prefab": return "prefab";
                case ".mat": return "materiale";
                case ".png": case ".jpg": case ".jpeg": case ".tga": case ".psd": case ".tif": case ".tiff": case ".exr": case ".hdr": return "texture";
                case ".fbx": case ".obj": case ".blend": return "modello";
                case ".terrainlayer": return "strato";
                case ".shader": case ".shadergraph": return "shader";
                default: return null;
            }
        }

        static string Nome(Object o) => o != null ? o.name : "-";

        static string Misura(Texture tex) => tex != null ? tex.width + "x" + tex.height : "";

        static string Valore(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.intValue.ToString(ci);
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.####", ci);
                case SerializedPropertyType.Boolean: return p.boolValue ? "si" : "no";
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum:
                    return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length
                        ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString(ci);
                case SerializedPropertyType.Color: return p.colorValue.ToString();
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString();
                case SerializedPropertyType.ObjectReference: return p.objectReferenceValue != null ? p.objectReferenceValue.name : "(vuoto)";
                case SerializedPropertyType.Generic: return p.isArray ? "[" + p.arraySize + "]" : "";
                default: return "";
            }
        }
    }
}
