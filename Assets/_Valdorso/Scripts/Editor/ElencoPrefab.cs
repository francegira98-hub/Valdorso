using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Strumenti per conoscere i prefab dei pacchetti, da incollare nella chat con Claude.
    /// 1) Elenca i prefab della cartella selezionata: nomi e misure (larghezza × altezza × profondità in metri).
    /// 2) Descrivi i perni dei pezzi del kit: per muri, pavimenti, tetti, porte e finestre dice dove comincia
    ///    e dove finisce il pezzo rispetto al suo perno (il punto da cui Unity lo posa), per montare le case da codice.
    /// Uso: selezionare una cartella nella colonna di DESTRA della finestra Project (l'Inspector deve mostrarne il nome),
    /// poi il menu Valdorso → Prove. Funziona solo nell'editor, non entra nel gioco.
    /// </summary>
    public static class ElencoPrefab
    {
        // Parti del nome che riconoscono i pezzi del kit delle case (senza badare alle maiuscole).
        static readonly string[] PezziDelKit =
        {
            "wall", "floor", "roof", "barn", "planksCorner", "chimney", "door", "window", "bridge", "houseAdd", "fence"
        };

        [MenuItem("Valdorso/Prove/Elenca i prefab della cartella selezionata")]
        static void Elenca()
        {
            if (!CartellaSelezionata(out string cartella)) return;

            var righe = new List<string>();
            foreach (GameObject prefab in PrefabDi(cartella))
            {
                Vector3 misura = Riquadro(prefab, out _).size;
                righe.Add(string.Format(CultureInfo.InvariantCulture, "{0}\t{1:0.#} x {2:0.#} x {3:0.#}",
                    prefab.name, misura.x, misura.y, misura.z));
            }
            Consegna(righe, $"Prefab in {cartella} ({righe.Count}), misure in metri larghezza x altezza x profondita:", cartella);
        }

        [MenuItem("Valdorso/Prove/Descrivi i perni dei pezzi del kit")]
        static void DescriviPerni()
        {
            if (!CartellaSelezionata(out string cartella)) return;

            var righe = new List<string>();
            foreach (GameObject prefab in PrefabDi(cartella))
            {
                if (prefab.name.Contains("Splines")) continue; // staccionate già montate dalla demo
                if (!EDelKit(prefab.name)) continue;

                Bounds b = Riquadro(prefab, out int mesh);
                Vector3 scala = prefab.transform.localScale;
                Vector3 rot = prefab.transform.localEulerAngles;
                righe.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}\tmisura {1:0.##} x {2:0.##} x {3:0.##}\tX da {4:0.##} a {5:0.##}\tY da {6:0.##} a {7:0.##}\tZ da {8:0.##} a {9:0.##}\tmesh {10}\tscala {11:0.##},{12:0.##},{13:0.##}\trot {14:0},{15:0},{16:0}",
                    prefab.name, b.size.x, b.size.y, b.size.z,
                    b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z,
                    mesh, scala.x, scala.y, scala.z, rot.x, rot.y, rot.z));
            }
            Consegna(righe, $"Perni dei pezzi del kit in {cartella} ({righe.Count}); X larghezza, Y altezza, Z profondita, in metri rispetto al perno:", cartella);
        }

        // ---------------------------------------------------------------- supporto

        static bool CartellaSelezionata(out string cartella)
        {
            cartella = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(cartella) || !AssetDatabase.IsValidFolder(cartella))
            {
                EditorUtility.DisplayDialog("Valdorso",
                    "Seleziona prima una cartella nella colonna di destra della finestra Project (l'Inspector deve mostrarne il nome).", "OK");
                return false;
            }
            return true;
        }

        static bool EDelKit(string nome)
        {
            foreach (string parte in PezziDelKit)
                if (nome.IndexOf(parte, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static IEnumerable<GameObject> PrefabDi(string cartella)
        {
            string[] guid = AssetDatabase.FindAssets("t:Prefab", new[] { cartella });
            try
            {
                for (int i = 0; i < guid.Length; i++)
                {
                    if (i % 50 == 0) EditorUtility.DisplayProgressBar("Valdorso", "Misuro i prefab...", (float)i / guid.Length);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid[i]));
                    if (prefab != null) yield return prefab;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void Consegna(List<string> righe, string intestazione, string cartella)
        {
            righe.Sort(System.StringComparer.OrdinalIgnoreCase);
            var testo = new StringBuilder();
            testo.Append(intestazione).Append('\n');
            foreach (string r in righe) testo.Append(r).Append('\n');
            EditorGUIUtility.systemCopyBuffer = testo.ToString();
            Debug.Log($"[Valdorso] {righe.Count} prefab di {cartella} copiati negli appunti.");
            EditorUtility.DisplayDialog("Valdorso", $"{righe.Count} prefab copiati negli appunti: incollali nella chat (Ctrl+V).", "OK");
        }

        /// <summary>
        /// Il riquadro che contiene tutte le mesh del prefab, misurato come se il prefab fosse posato
        /// nel punto 0,0,0 senza rotazione (la sua scala resta), senza metterlo nella scena.
        /// </summary>
        static Bounds Riquadro(GameObject prefab, out int quanteMesh)
        {
            quanteMesh = 0;
            bool trovato = false;
            var riquadro = new Bounds();
            Matrix4x4 posato = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                quanteMesh++;
                Bounds b = mf.sharedMesh.bounds;
                Matrix4x4 m = posato * mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var angolo = new Vector3(
                        (k & 1) == 0 ? b.min.x : b.max.x,
                        (k & 2) == 0 ? b.min.y : b.max.y,
                        (k & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = m.MultiplyPoint3x4(angolo);
                    if (!trovato) { riquadro = new Bounds(p, Vector3.zero); trovato = true; }
                    else riquadro.Encapsulate(p);
                }
            }
            return trovato ? riquadro : new Bounds();
        }
    }
}