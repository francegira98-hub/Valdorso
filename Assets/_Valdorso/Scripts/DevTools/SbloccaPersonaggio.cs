using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Valdorso.DevTools
{
    /// <summary>
    /// Sblocca il personaggio: F8 lo riporta al punto di partenza (davanti al tempio).
    /// Strumento di sviluppo: nasce da solo all'avvio del gioco, non va messo in nessuna scena.
    /// Più avanti diventerà un comando dello staff e un "sono bloccato" con attesa per tutti.
    /// Il movimento è deciso dal PC di chi gioca, quindi il server riceve la nuova posizione da solo.
    /// </summary>
    public class SbloccaPersonaggio : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Nasci()
        {
            var go = new GameObject("SbloccaPersonaggio");
            DontDestroyOnLoad(go);
            go.AddComponent<SbloccaPersonaggio>();
        }

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.f8Key.wasPressedThisFrame) return;
            if (NetworkClient.localPlayer == null) return;

            GameObject partenza = GameObject.Find("PuntoDiPartenza");
            if (partenza == null)
            {
                Debug.LogWarning("[Valdorso] Nessun PuntoDiPartenza in questa scena.");
                return;
            }

            Transform t = NetworkClient.localPlayer.transform;
            var cc = t.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;   // il CharacterController va spento per spostare il personaggio di colpo
            t.SetPositionAndRotation(partenza.transform.position, partenza.transform.rotation);
            if (cc != null) cc.enabled = true;
            Debug.Log("[Valdorso] Personaggio riportato al punto di partenza (F8).");
        }
    }
}
