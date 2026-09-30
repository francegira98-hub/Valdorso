using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Prove → Trova i materiali in più: cerca nella scena aperta gli oggetti che hanno più materiali
    /// che parti della mesh (l'avviso giallo "Material count in the shared material list is higher than sub mesh count").
    /// Li elenca in Console (clic sulla riga per trovarli) e chiede se toglierli.
    /// Il materiale in più non si vede comunque: Unity lo disegna una seconda volta sopra, e costa solo prestazioni.
    /// </summary>
    public static class MaterialiInPiu
    {
        [MenuItem("Valdorso/Prove/Trova i materiali in più")]
        static void Trova()
        {
            var trovati = new List<Renderer>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Mesh m = null;
                if (r is MeshRenderer) m = r.GetComponent<MeshFilter>()?.sharedMesh;
                else if (r is SkinnedMeshRenderer s) m = s.sharedMesh;
                if (m == null) continue;
                if (r.sharedMaterials.Length > m.subMeshCount)
                {
                    trovati.Add(r);
                    Debug.Log($"[Valdorso] {Percorso(r.transform)}: {r.sharedMaterials.Length} materiali, la mesh \"{m.name}\" ha {m.subMeshCount} parti.", r);
                }
            }

            if (trovati.Count == 0)
            {
                EditorUtility.DisplayDialog("Valdorso", "Nessun oggetto con materiali in più in questa scena.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Valdorso", $"Trovati {trovati.Count} oggetti con materiali in più (elenco in Console).\n\n" +
                    "Tolgo i materiali in più? Non cambia l'aspetto: quelli in più venivano solo disegnati due volte.", "Togli", "Lascia così"))
                return;

            foreach (Renderer r in trovati)
            {
                Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
                Undo.RecordObject(r, "Togli i materiali in più");
                var tenuti = new Material[m.subMeshCount];
                System.Array.Copy(r.sharedMaterials, tenuti, m.subMeshCount);
                r.sharedMaterials = tenuti;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            EditorSceneManager.MarkSceneDirty(trovati[0].gameObject.scene);
            EditorUtility.DisplayDialog("Valdorso", $"Sistemati {trovati.Count} oggetti.\n\nRicorda: Ctrl+S sulla scena.", "OK");
        }

        static string Percorso(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
