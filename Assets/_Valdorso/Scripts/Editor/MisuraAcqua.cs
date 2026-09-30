using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Valle → Misura l'acqua: "tasta" il lago e il fiume di R.A.M (gli oggetti sul livello Water
    /// con una mesh) in una griglia di celle da 2 m e scrive le quote del pelo dell'acqua in AcqueValle
    /// (oggetto "Acque" nella scena, lo crea se manca). Serve al nuoto. Da rilanciare se si cambia l'acqua.
    /// </summary>
    public static class MisuraAcqua
    {
        const float Cella = 2f;

        [MenuItem("Valdorso/Valle/Misura l'acqua")]
        static void Misura()
        {
            int livelloAcqua = LayerMask.NameToLayer("Water");
            var celle = new Dictionary<Vector2Int, float>();
            string rapporto = "";

            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.gameObject.layer != livelloAcqua || mf.sharedMesh == null) continue;

                // Un collider provvisorio, solo per tastare la forma; poi si toglie
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                Physics.SyncTransforms();
                Bounds b = mc.bounds;

                int trovate = 0;
                float minY = float.MaxValue, maxY = float.MinValue;
                int x0 = Mathf.FloorToInt(b.min.x / Cella), x1 = Mathf.CeilToInt(b.max.x / Cella);
                int z0 = Mathf.FloorToInt(b.min.z / Cella), z1 = Mathf.CeilToInt(b.max.z / Cella);
                float alto = b.max.y + 10f, lunghezza = b.size.y + 20f;
                for (int ix = x0; ix <= x1; ix++)
                    for (int iz = z0; iz <= z1; iz++)
                    {
                        var da = new Vector3((ix + 0.5f) * Cella, alto, (iz + 0.5f) * Cella);
                        if (!mc.Raycast(new Ray(da, Vector3.down), out RaycastHit h, lunghezza)) continue;
                        var k = new Vector2Int(ix, iz);
                        if (!celle.TryGetValue(k, out float y) || h.point.y > y) celle[k] = h.point.y;
                        trovate++;
                        minY = Mathf.Min(minY, h.point.y);
                        maxY = Mathf.Max(maxY, h.point.y);
                    }
                Object.DestroyImmediate(mc);
                rapporto += $"{mf.name}: {trovate} celle, pelo dell'acqua da {minY:0.0} a {maxY:0.0} m\n";
            }

            if (celle.Count == 0)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non ho trovato acqua: apri la scena Valle (lago e fiume sul livello Water).", "OK");
                return;
            }

            AcqueValle acque = Object.FindAnyObjectByType<AcqueValle>();
            if (acque == null)
            {
                var go = new GameObject("Acque");
                Undo.RegisterCreatedObjectUndo(go, "Acque");
                acque = go.AddComponent<AcqueValle>();
            }
            Undo.RecordObject(acque, "Misura l'acqua");
            acque.ScriviCelle(Cella, celle);
            EditorUtility.SetDirty(acque);
            EditorSceneManager.MarkSceneDirty(acque.gameObject.scene);

            Debug.Log("[Valdorso] Acqua misurata:\n" + rapporto + $"Totale {celle.Count} celle da {Cella} m.");
            EditorUtility.DisplayDialog("Valdorso", "Acqua misurata.\n\n" + rapporto + $"\nTotale {celle.Count} celle da {Cella} m.\n\nRicorda: Ctrl+S sulla scena.", "OK");
        }
    }
}
