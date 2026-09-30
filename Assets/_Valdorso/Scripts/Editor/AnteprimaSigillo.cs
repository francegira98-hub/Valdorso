using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valdorso.World;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Il velo del sigillo si costruisce seguendo il terreno e non viene salvato nella scena (pesa, e il gioco lo rifà
    /// da solo all'avvio). Questo piccolo aiuto lo ricostruisce anche nell'editor ogni volta che si apre una scena,
    /// così nella scheda Scene si vede sempre, senza dover rilanciare "Alza il sigillo della Corona".
    /// </summary>
    [InitializeOnLoad]
    static class AnteprimaSigillo
    {
        static AnteprimaSigillo()
        {
            EditorSceneManager.sceneOpened += (scena, modo) => Ricostruisci();
            EditorApplication.delayCall += Ricostruisci; // anche dopo ogni compilazione
        }

        static void Ricostruisci()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (SigilloCorona s in Object.FindObjectsByType<SigilloCorona>(FindObjectsSortMode.None))
            {
                if (s.GetComponent<MeshFilter>()?.sharedMesh != null) continue;
                s.Costruisci();
            }
        }
    }
}
