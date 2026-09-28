using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Valdorso.WorldEvents;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Prepara la scena Valle per essere la scena di gioco (passo 6.5.2): aggiunge gli oggetti che aveva TestRete
    /// (Mondo con il registro degli eventi, punto di partenza, telecamera principale degli Starter Assets, sistema degli
    /// eventi dell'interfaccia), mette il punto di partenza davanti alla porta del tempio rivolto verso la piazza,
    /// spegne la telecamera panoramica (serve solo per guardare la valle nell'editor) e aggiunge la scena alle Build Settings.
    /// Menu: Valdorso → Valle → Prepara la valle per il gioco, con la scena Valle aperta. Si può rilanciare.
    /// La scena online del NetworkManager (nella scena Menu) si cambia a mano.
    /// </summary>
    public static class ValleGioco
    {
        [MenuItem("Valdorso/Valle/Prepara la valle per il gioco")]
        static void Prepara()
        {
            Scene scena = SceneManager.GetActiveScene();
            GameObject valle = GameObject.Find("/Valle");
            Transform arrivo = valle != null ? valle.transform.Find("Segnaposti/Arrivo_Tempio") : null;
            if (scena.name != "Valle" || valle == null || arrivo == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri la scena Valle (con il villaggio costruito) e riprova.", "OK");
                return;
            }
            var fatto = new List<string>();

            // Mondo: il registro di tutto ciò che accade (vive solo sul server)
            GameObject mondo = GameObject.Find("/Mondo");
            if (mondo == null) { mondo = new GameObject("Mondo"); fatto.Add("creato Mondo"); }
            if (mondo.GetComponent<WorldEventLog>() == null) { mondo.AddComponent<WorldEventLog>(); fatto.Add("aggiunto WorldEventLog"); }

            // Punto di partenza: davanti alla porta del tempio, rivolto a sud verso la piazza
            GameObject partenza = GameObject.Find("/PuntoDiPartenza");
            if (partenza == null) { partenza = new GameObject("PuntoDiPartenza"); fatto.Add("creato PuntoDiPartenza"); }
            if (partenza.GetComponent<NetworkStartPosition>() == null) partenza.AddComponent<NetworkStartPosition>();
            Vector3 p = arrivo.position;
            float y = p.y;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 o = t.GetPosition(), s = t.terrainData.size;
                if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z) { y = o.y + t.SampleHeight(p); break; }
            }
            partenza.transform.SetPositionAndRotation(new Vector3(p.x, y + 0.2f, p.z), Quaternion.Euler(0f, 180f, 0f));
            fatto.Add($"punto di partenza a {p.x:0.0}, {y + 0.2f:0.00}, {p.z:0.0}");

            // Telecamera principale (con Cinemachine) e sistema degli eventi dell'interfaccia, dagli Starter Assets
            if (GameObject.FindWithTag("MainCamera") == null)
                fatto.Add(Istanzia("MainCamera") ? "aggiunta MainCamera" : "MainCamera NON trovata");
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
                fatto.Add(Istanzia("UI_EventSystem") ? "aggiunto UI_EventSystem" : "UI_EventSystem NON trovato");

            // La telecamera panoramica serve solo nell'editor: in gioco disegnerebbe la valle una seconda volta
            Transform panorama = valle.transform.Find("Telecamera_Panorama");
            if (panorama != null && panorama.gameObject.activeSelf)
            {
                panorama.gameObject.SetActive(false);
                fatto.Add("spenta Telecamera_Panorama (riaccendila quando vuoi guardare la valle nella finestra Game)");
            }

            // La scena nelle Build Settings
            var elenco = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!elenco.Exists(s => s.path == scena.path))
            {
                elenco.Add(new EditorBuildSettingsScene(scena.path, true));
                EditorBuildSettings.scenes = elenco.ToArray();
                fatto.Add("Valle aggiunta alle Build Settings");
            }

            EditorSceneManager.MarkSceneDirty(scena);
            string rapporto = string.Join("\n", fatto);
            Debug.Log("[Valdorso] Valle pronta per il gioco:\n" + rapporto);
            EditorUtility.DisplayDialog("Valdorso", "Valle pronta per il gioco.\n\n" + rapporto +
                "\n\nOra: Ctrl+S, poi nella scena Menu il campo Online Scene del NetworkManager = Valle.", "OK");
        }

        /// <summary>Mette nella scena il prefab con quel nome esatto (cercato in tutto Assets), collegato al prefab.</summary>
        static bool Istanzia(string nome)
        {
            foreach (string guid in AssetDatabase.FindAssets(nome + " t:Prefab"))
            {
                string percorso = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(percorso) != nome) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(percorso);
                if (prefab == null) continue;
                PrefabUtility.InstantiatePrefab(prefab);
                return true;
            }
            return false;
        }
    }
}
