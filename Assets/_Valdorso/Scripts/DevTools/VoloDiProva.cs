using System.Reflection;
using Mirror;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Valdorso.DevTools
{
    /// <summary>
    /// Volo di prova, per girare il mondo veloce mentre si sviluppa. Si mette da solo, non serve aggiungerlo a niente.
    /// F9 accende e spegne. In volo si passa attraverso tutto:
    /// W A S D per muoversi dove guarda la telecamera, Spazio per salire, C per scendere, Maiuscolo per andare veloce,
    /// la rotellina del mouse cambia la velocità.
    /// Funziona solo nell'editor e nelle build di sviluppo (Development Build): nel gioco vero non esiste.
    /// </summary>
    [DefaultExecutionOrder(10000)] // dopo tutto il resto: niente tremolii con la telecamera
    public class VoloDiProva : MonoBehaviour
    {
        public static bool Attivo { get; private set; }

        static readonly FieldInfo campoYaw = typeof(ThirdPersonController).GetField("_cinemachineTargetYaw", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo campoPitch = typeof(ThirdPersonController).GetField("_cinemachineTargetPitch", BindingFlags.NonPublic | BindingFlags.Instance);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Installa()
        {
            if (!Debug.isDebugBuild || Application.isBatchMode) return;
            var go = new GameObject("Volo di prova") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<VoloDiProva>();
        }

        float velocita = 12f;
        float yaw, pitch;
        GameObject io;
        CharacterController cc;
        ThirdPersonController mover;
        StarterAssetsInputs input;
        Behaviour[] daSpegnere;
        bool[] eranoAccesi;
        bool ccEraAcceso;
        CinemachineBrain cervello;
        bool cervelloEraAcceso;

        void LateUpdate()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            GameObject locale = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
            if (Attivo && locale != io) Spegni(); // il personaggio è cambiato o non c'è più
            if (kb.f9Key.wasPressedThisFrame)
            {
                if (Attivo) Spegni();
                else if (locale != null) Accendi(locale);
            }
            if (!Attivo || io == null) return;

            // Guardarsi intorno, come fa il ThirdPersonController (che in volo è spento)
            if (input != null)
            {
                yaw += input.look.x;   // col mouse, come il ThirdPersonController
                pitch = Mathf.Clamp(pitch + input.look.y, -85f, 85f);
            }

            Mouse m = Mouse.current;
            if (m != null && Mathf.Abs(m.scroll.ReadValue().y) > 0.01f)
                velocita = Mathf.Clamp(velocita * (m.scroll.ReadValue().y > 0 ? 1.2f : 0.83f), 2f, 200f);

            Quaternion sguardo = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 avanti = sguardo * Vector3.forward, destra = sguardo * Vector3.right;
            Vector3 dir = Vector3.zero;
            if (kb.wKey.isPressed) dir += avanti;
            if (kb.sKey.isPressed) dir -= avanti;
            if (kb.dKey.isPressed) dir += destra;
            if (kb.aKey.isPressed) dir -= destra;
            if (kb.spaceKey.isPressed) dir += Vector3.up;
            if (kb.cKey.isPressed) dir -= Vector3.up;
            float v = velocita * (kb.leftShiftKey.isPressed ? 4f : 1f);
            io.transform.position += dir.normalized * v * Time.deltaTime;

            // Il corpo guarda dove si vola; la telecamera la muoviamo noi (Cinemachine è spento in volo):
            // alle alte velocità il suo inseguimento morbido faceva tremare tutto
            io.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (Camera.main != null)
            {
                Quaternion vista = Quaternion.Euler(pitch, yaw, 0f);
                Vector3 occhio = io.transform.position + Vector3.up * 1.6f;
                Camera.main.transform.SetPositionAndRotation(occhio - vista * Vector3.forward * 4.5f + Vector3.up * 0.3f, vista);
            }
            if (input != null) { input.jump = false; input.move = Vector2.zero; }
        }

        void Accendi(GameObject giocatore)
        {
            io = giocatore;
            cc = io.GetComponent<CharacterController>();
            mover = io.GetComponent<ThirdPersonController>();
            input = io.GetComponent<StarterAssetsInputs>();
            // In volo si spengono le cose che muovono il personaggio da sole
            daSpegnere = new Behaviour[]
            {
                mover, io.GetComponent<Movement.ObstacleTraversal>(), io.GetComponent<Movement.Nuoto>(),
                io.GetComponent<Movement.ReteDiSicurezza>()
            };
            eranoAccesi = new bool[daSpegnere.Length];
            for (int i = 0; i < daSpegnere.Length; i++)
            {
                if (daSpegnere[i] == null) continue;
                eranoAccesi[i] = daSpegnere[i].enabled;
                daSpegnere[i].enabled = false;
            }
            cervello = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            cervelloEraAcceso = cervello != null && cervello.enabled;
            if (cervello != null) cervello.enabled = false;
            ccEraAcceso = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false; // il CharacterController è un collider, non un Behaviour: si spegne a parte
            if (mover != null && mover.CinemachineCameraTarget != null)
            {
                Vector3 e = mover.CinemachineCameraTarget.transform.rotation.eulerAngles;
                yaw = e.y;
                pitch = e.x > 180f ? e.x - 360f : e.x;
            }
            Attivo = true;
            Debug.Log("[Valdorso] Volo di prova acceso (F9 per spegnere).");
        }

        void Spegni()
        {
            // La telecamera resta dove guardava in volo, invece di tornare di scatto a prima
            if (mover != null)
            {
                campoYaw?.SetValue(mover, yaw);
                campoPitch?.SetValue(mover, pitch);
            }
            if (daSpegnere != null)
                for (int i = 0; i < daSpegnere.Length; i++)
                    if (daSpegnere[i] != null) daSpegnere[i].enabled = eranoAccesi[i];
            if (cc != null && ccEraAcceso) cc.enabled = true;
            if (cervello != null && cervelloEraAcceso) cervello.enabled = true;
            if (mover != null && mover.CinemachineCameraTarget != null)
                mover.CinemachineCameraTarget.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            Attivo = false;
            io = null;
            daSpegnere = null;
        }

        void OnGUI()
        {
            if (!Attivo) return;
            GUI.Label(new Rect(10, Screen.height - 30, 700, 24),
                $"Volo di prova · F9 spegne · WASD, Spazio su, C giù, Maiuscolo veloce, rotellina: {velocita:0} m/s");
        }
    }
}