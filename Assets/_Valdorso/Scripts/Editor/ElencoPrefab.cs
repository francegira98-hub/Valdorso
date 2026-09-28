using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Elenca i prefab di una cartella con le loro misure (larghezza × altezza × profondità in metri)
    /// e copia l'elenco negli appunti, da incollare nella chat con Claude.
    /// Uso: selezionare una cartella nella finestra Project, poi Valdorso → Prove → Elenca i prefab della cartella.
    /// Funziona solo nell'editor, non entra nel gioco.
    /// </summary>
    public static class ElencoPrefab
    {
        [MenuItem("Valdorso/Prove/Elenca i prefab della cartella selezionata")]
        static void Elenca()
        {
            string cartella = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(cartella) || !AssetDatabase.IsValidFolder(cartella))
            {
                EditorUtility.DisplayDialog("Valdorso", "Seleziona prima una cartella nella finestra Project.", "OK");
                return;
            }

            var righe = new List<string>();
            string[] guid = AssetDatabase.FindAssets("t:Prefab", new[] { cartella });
            for (int i = 0; i < guid.Length; i++)
            {
                if (i % 50 == 0) EditorUtility.DisplayProgressBar("Valdorso", "Misuro i prefab...", (float)i / guid.Length);
                string percorso = AssetDatabase.GUIDToAssetPath(guid[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(percorso);
                if (prefab == null) continue;
                Vector3 misura = Misura(prefab);
                righe.Add(string.Format(CultureInfo.InvariantCulture, "{0}\t{1:0.#} x {2:0.#} x {3:0.#}",
                    prefab.name, misura.x, misura.y, misura.z));
            }
            EditorUtility.ClearProgressBar();
            righe.Sort(System.StringComparer.OrdinalIgnoreCase);

            var testo = new StringBuilder();
            testo.Append("Prefab in ").Append(cartella).Append(" (").Append(righe.Count).Append("), misure in metri larghezza x altezza x profondita:\n");
            foreach (string r in righe) testo.Append(r).Append('\n');
            EditorGUIUtility.systemCopyBuffer = testo.ToString();
            Debug.Log($"[Valdorso] {righe.Count} prefab di {cartella} copiati negli appunti.");
            EditorUtility.DisplayDialog("Valdorso", $"{righe.Count} prefab copiati negli appunti: incollali nella chat (Ctrl+V).", "OK");
        }

        /// <summary>Le misure del prefab: il riquadro che contiene tutte le sue mesh, senza metterlo nella scena.</summary>
        static Vector3 Misura(GameObject prefab)
        {
            bool trovato = false;
            var riquadro = new Bounds();
            Matrix4x4 radice = prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                Matrix4x4 m = radice * mf.transform.localToWorldMatrix;
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
            return trovato ? riquadro.size : Vector3.zero;
        }
    }
}
