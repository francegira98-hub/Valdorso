using Mirror;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Valdorso.UI
{
    /// <summary>
    /// Il menu di pausa nel mondo (Esc): Riprendi, Impostazioni, Torna al menu, Esci dal gioco.
    /// Il mondo è online e non si ferma: la pausa toglie solo i comandi al proprio personaggio e libera il mouse.
    /// Nasce da solo all'avvio del gioco e resta per tutta la partita. Si occupa anche di mettere in pratica
    /// le impostazioni che riguardano tutte le finestre (grandezza dei testi) e i comandi del personaggio.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        static PauseMenu instance;
        public static bool IsPaused => instance != null && instance.paused;

        RectTransform root;
        bool paused;
        int settingsClosedFrame = -1;

        // Stato del proprio personaggio mentre si è in pausa
        GameObject pausedPlayer;
        readonly System.Collections.Generic.List<Behaviour> disabledWhilePaused = new System.Collections.Generic.List<Behaviour>();
        static readonly string[] ActionsLockedInPause = { "MeleeCombat", "DodgeRoll", "DebugVitalsHUD" };

        // Impostazioni già messe in pratica
        GameObject controlsAppliedTo;
        int controlsVersion = -1, canvasVersion = -1, canvasCount = -1;
        float nextCanvasCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("MenuDiPausa");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PauseMenu>();
            SettingsWindow.Closed += () => { if (instance != null) instance.settingsClosedFrame = Time.frameCount; };
        }

        void Update()
        {
            ApplyCanvasSettings();
            ApplyControlSettings();

            GameObject player = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
            if (paused && player == null) Resume(); // tornati al menu o disconnessi

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (SettingsWindow.IsOpen || settingsClosedFrame == Time.frameCount) return; // quell'Esc era per le impostazioni
            if (player == null || SceneManager.GetSceneByName("Creazione").isLoaded) return;

            if (paused) Resume();
            else Pause(player);
        }

        // ---------- Pausa ----------

        void Pause(GameObject player)
        {
            if (root == null) Build();
            paused = true;
            pausedPlayer = player;
            root.gameObject.SetActive(true);

            var playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.DeactivateInput();
            var inputs = player.GetComponent<StarterAssetsInputs>();
            if (inputs != null)
            {
                inputs.MoveInput(Vector2.zero);
                inputs.LookInput(Vector2.zero);
                inputs.JumpInput(false);
                inputs.SprintInput(false);
                inputs.cursorLocked = false;
                inputs.cursorInputForLook = false;
            }
            disabledWhilePaused.Clear();
            foreach (MonoBehaviour b in player.GetComponents<MonoBehaviour>())
            {
                if (b == null || !b.enabled) continue;
                if (System.Array.IndexOf(ActionsLockedInPause, b.GetType().Name) < 0) continue;
                b.enabled = false;
                disabledWhilePaused.Add(b);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Resume()
        {
            paused = false;
            if (root != null) root.gameObject.SetActive(false);
            if (SettingsWindow.IsOpen) SettingsWindow.Close();

            if (pausedPlayer != null)
            {
                var playerInput = pausedPlayer.GetComponent<PlayerInput>();
                if (playerInput != null) playerInput.ActivateInput();
                var inputs = pausedPlayer.GetComponent<StarterAssetsInputs>();
                if (inputs != null)
                {
                    inputs.cursorLocked = true;
                    inputs.cursorInputForLook = true;
                }
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            foreach (Behaviour b in disabledWhilePaused)
                if (b != null) b.enabled = true;
            disabledWhilePaused.Clear();
            pausedPlayer = null;
        }

        void Build()
        {
            RectTransform canvas = UIKit.PersistentCanvas("MenuDiPausa_Tela", 450);
            canvas.SetParent(transform, false);
            root = UIKit.Veil(canvas, 0.6f);
            RectTransform window = UIKit.Window(root, "Finestra", new Vector2(620f, 620f));

            UIKit.Label(window, "PAUSA", UIKit.TitleFont, 50f, UIKit.Gold, new Vector2(0f, -34f), new Vector2(620f, 64f), TextAlignmentOptions.Center);
            UIKit.Divider(window, new Vector2(135f, -100f), 350f);
            UIKit.Label(window, "Il mondo non si ferma: intorno a te la valle continua a vivere.",
                UIKit.ItalicFont, 22f, UIKit.TextSoft, new Vector2(60f, -134f), new Vector2(500f, 60f), TextAlignmentOptions.Center);

            UIKit.MakeButton(window, "RIPRENDI", new Vector2(110f, -215f), new Vector2(400f, 62f), 24f, Resume);
            UIKit.MakeButton(window, "IMPOSTAZIONI", new Vector2(110f, -295f), new Vector2(400f, 62f), 24f, SettingsWindow.Open);
            UIKit.MakeButton(window, "TORNA AL MENU", new Vector2(110f, -375f), new Vector2(400f, 62f), 24f, BackToMenu);
            UIKit.MakeButton(window, "ESCI DAL GIOCO", new Vector2(110f, -455f), new Vector2(400f, 62f), 24f, QuitGame);
            UIKit.Label(window, "Esc per riprendere", UIKit.ItalicFont, 19f, UIKit.Fade(UIKit.TextSoft, 0.7f),
                new Vector2(0f, -550f), new Vector2(620f, 30f), TextAlignmentOptions.Center);
            root.gameObject.SetActive(false);
        }

        void BackToMenu()
        {
            Resume();
            // Il server salva il personaggio quando esce; Mirror riporta alla scena del menu.
            if (NetworkServer.active && NetworkClient.isConnected) NetworkManager.singleton.StopHost();
            else if (NetworkClient.isConnected) NetworkManager.singleton.StopClient();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Impostazioni che valgono ovunque ----------

        void ApplyCanvasSettings()
        {
            if (Time.unscaledTime < nextCanvasCheck) return;
            nextCanvasCheck = Time.unscaledTime + 0.5f;
            CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (GameSettings.Version == canvasVersion && scalers.Length == canvasCount) return;
            canvasVersion = GameSettings.Version;
            canvasCount = scalers.Length;
            foreach (CanvasScaler scaler in scalers) GameSettings.ApplyToCanvas(scaler);
        }

        void ApplyControlSettings()
        {
            GameObject player = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
            if (player == null || (player == controlsAppliedTo && controlsVersion == GameSettings.Version)) return;
            controlsAppliedTo = player;
            controlsVersion = GameSettings.Version;
            GameSettings.ApplyControls(player.GetComponent<PlayerInput>());
        }
    }
}