using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Valle → Alza il sigillo della Corona: mette il velo di luce nella gola del passo,
    /// oltre la palizzata di Aurelia del posto di guardia. Crea (se manca) il materiale con lo shader Valdorso/Sigillo,
    /// l'oggetto con NetworkIdentity e SigilloCorona, e costruisce il velo nell'editor per vederlo subito.
    /// Si può rilanciare: rimette l'oggetto al suo posto e rifà il velo.
    /// </summary>
    public static class SigilloBuilder
    {
        const float XSigillo = 1758f;   // 13 m oltre la palizzata di Aurelia (x 1745)
        const float ZStrada = 96.5f;
        const string Materiale = "Assets/_Valdorso/Mondo/Valle/Sigillo.mat";
        const string Nome = "Sigillo della Corona";

        [MenuItem("Valdorso/Valle/Alza il sigillo della Corona")]
        static void Alza()
        {
            GameObject valle = GameObject.Find("/Valle");
            if (valle == null || Terrain.activeTerrains.Length == 0)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Valle.", "OK");
                return;
            }

            Shader shader = Shader.Find("Valdorso/Sigillo");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo lo shader Valdorso/Sigillo: controlla che Sigillo.shader sia nel progetto e senza errori.", "OK");
                return;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Materiale);
            if (mat == null)
            {
                mat = new Material(shader) { name = "Sigillo" };
                AssetDatabase.CreateAsset(mat, Materiale);
            }
            else mat.shader = shader;
            // I valori scelti dopo la prima prova (01/10): velo che si vede, rune più piccole e in poche fasce
            mat.SetColor("_ColoreRune", new Color(1f, 0.78f, 0.32f));
            mat.SetColor("_ColoreVelo", new Color(0.85f, 0.88f, 1f));
            mat.SetFloat("_Opacita", 0.55f);
            mat.SetFloat("_Intensita", 3.5f);
            mat.SetFloat("_Rune", 0.22f);
            mat.SetFloat("_Cella", 0.9f);
            mat.SetFloat("_Salita", 0.25f);
            EditorUtility.SetDirty(mat);

            Transform esistente = valle.transform.Find(Nome);
            GameObject go = esistente != null ? esistente.gameObject : new GameObject(Nome);
            if (esistente == null)
            {
                Undo.RegisterCreatedObjectUndo(go, Nome);
                go.transform.SetParent(valle.transform, false);
            }
            go.transform.SetPositionAndRotation(new Vector3(XSigillo, 0f, ZStrada), Quaternion.identity);
            go.transform.localScale = Vector3.one;
            if (go.GetComponent<NetworkIdentity>() == null) go.AddComponent<NetworkIdentity>();
            var sigillo = go.GetComponent<SigilloCorona>();
            if (sigillo == null) sigillo = go.AddComponent<SigilloCorona>();
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            Physics.SyncTransforms();
            sigillo.Costruisci();

            EditorSceneManager.MarkSceneDirty(valle.scene);
            Bounds b = r.bounds;
            string rapporto = $"Velo largo {b.size.z:0} m e alto {b.size.y:0} m, a x {XSigillo:0}.";
            Debug.Log("[Valdorso] Sigillo della Corona alzato. " + rapporto);
            EditorUtility.DisplayDialog("Valdorso", "Sigillo della Corona alzato.\n\n" + rapporto + "\n\nRicorda: Ctrl+S sulla scena.", "OK");
        }
    }
}