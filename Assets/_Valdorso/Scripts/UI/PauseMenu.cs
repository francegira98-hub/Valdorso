using Mirror;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Valdorso.Network;

namespace Valdorso.UI
{
    /// <summary>
    /// Il menu di pausa nel mondo (Esc): Riprendi, Impostazioni, Torna ai personaggi, Torna al menu, Esci dal gioco.
    /// Per lasciare la valle il server fa aspettare 20 secondi (subito nei luoghi sicuri): qui si mostra il conto alla rovescia.
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
        // L'uscita dal mondo
        bool leavingWorld;
        LeaveMode leaveMode;
        RectTransform notice;
        TMP_Text noticeText;
        float hideNoticeAt = -1f;

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
            ValdorsoNetworkManager.LeaveStatusReceived += msg => { if (instance != null) instance.OnLeaveStatus(msg); };
        }

        void Update()
        {
            ApplyCanvasSettings();
            ApplyControlSettings();

            GameObject player = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
            if (paused && player == null) Resume(); // tornati al menu o disconnessi
            if (leavingWorld && !NetworkClient.isConnected) { leavingWorld = false; ShowNotice(null, 0f); }
            if (hideNoticeAt > 0f && Time.unscaledTime >= hideNoticeAt) ShowNotice(null, 0f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (SettingsWindow.IsOpen || settingsClosedFrame == Time.frameCount) return; // quell'Esc era per le impostazioni
            if (player == null || SceneManager.GetSceneByName("Creazione").isLoaded) return;

            if (leavingWorld)
            {
                // Durante l'attesa, Esc vuol dire "ci ho ripensato".
                ValdorsoNetworkManager.CancelLeaveWorld();
                return;
            }
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
            RectTransform window = UIKit.Window(root, "Finestra", new Vector2(620f, 700f));

            UIKit.Label(window, "PAUSA", UIKit.TitleFont, 50f, UIKit.Gold, new Vector2(0f, -34f), new Vector2(620f, 64f), TextAlignmentOptions.Center);
            UIKit.Divider(window, new Vector2(135f, -100f), 350f);
            UIKit.Label(window, "Il mondo non si ferma: intorno a te la valle continua a vivere.",
                UIKit.ItalicFont, 22f, UIKit.TextSoft, new Vector2(60f, -134f), new Vector2(500f, 60f), TextAlignmentOptions.Center);

            UIKit.MakeButton(window, "RIPRENDI", new Vector2(110f, -215f), new Vector2(400f, 62f), 24f, Resume);
            UIKit.MakeButton(window, "IMPOSTAZIONI", new Vector2(110f, -295f), new Vector2(400f, 62f), 24f, SettingsWindow.Open);
            UIKit.MakeButton(window, "TORNA AI PERSONAGGI", new Vector2(110f, -375f), new Vector2(400f, 62f), 22f, () => LeaveWorld(LeaveMode.Characters));
            UIKit.MakeButton(window, "TORNA AL MENU", new Vector2(110f, -455f), new Vector2(400f, 62f), 24f, () => LeaveWorld(LeaveMode.Menu));
            UIKit.MakeButton(window, "ESCI DAL GIOCO", new Vector2(110f, -535f), new Vector2(400f, 62f), 24f, () => LeaveWorld(LeaveMode.Quit));
            UIKit.Label(window, "Fuori da un luogo sicuro, lasciare la valle richiede 20 secondi fermi.", UIKit.ItalicFont, 18f, UIKit.Fade(UIKit.TextSoft, 0.7f),
                new Vector2(40f, -612f), new Vector2(540f, 30f), TextAlignmentOptions.Center);
            UIKit.Label(window, "Esc per riprendere", UIKit.ItalicFont, 19f, UIKit.Fade(UIKit.TextSoft, 0.7f),
                new Vector2(0f, -645f), new Vector2(620f, 30f), TextAlignmentOptions.Center);

            // L'avviso del conto alla rovescia, in alto al centro.
            notice = UIKit.Box(canvas, "Avviso", Vector2.zero, new Vector2(860f, 90f));
            notice.anchorMin = notice.anchorMax = notice.pivot = new Vector2(0.5f, 1f);
            notice.anchoredPosition = new Vector2(0f, -60f);
            var noticeBack = notice.gameObject.AddComponent<Image>();
            noticeBack.color = UIKit.Fade(UIKit.PanelColor, 0.92f);
            noticeBack.raycastTarget = false;
            noticeText = UIKit.Label(notice, string.Empty, UIKit.ItalicFont, 26f, UIKit.Text, new Vector2(20f, -10f), new Vector2(820f, 70f), TextAlignmentOptions.Center);
            notice.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        // ---------- Lasciare la valle ----------

        void LeaveWorld(LeaveMode mode)
        {
            Resume();
            if (!NetworkClient.isConnected)
            {
                Disconnect(mode);
                return;
            }
            leavingWorld = true;
            leaveMode = mode;
            ShowNotice("Ti prepari a lasciare la valle...", 0f);
            ValdorsoNetworkManager.RequestLeaveWorld(mode);
        }

        void OnLeaveStatus(LeaveWorldStatus msg)
        {
            if (msg.cancelled)
            {
                leavingWorld = false;
                ShowNotice(msg.message, 3f);
                return;
            }
            if (msg.done)
            {
                leavingWorld = false;
                ShowNotice(null, 0f);
                // Tornando ai personaggi si resta collegati: il Registro si riapre da solo.
                if (msg.mode != LeaveMode.Characters) Disconnect(msg.mode);
                return;
            }
            leavingWorld = true;
            ShowNotice($"Lascerai la valle tra {msg.secondsLeft}...\n<size=70%>Muoverti o combattere annulla. Esc per restare.</size>", 0f);
        }

        static void Disconnect(LeaveMode mode)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (mode == LeaveMode.Quit)
            {
                QuitGame();
                return;
            }
            // Mirror riporta alla scena del menu.
            if (NetworkServer.active && NetworkClient.isConnected) NetworkManager.singleton.StopHost();
            else if (NetworkClient.isConnected) NetworkManager.singleton.StopClient();
        }

        void ShowNotice(string text, float seconds)
        {
            if (notice == null)
            {
                if (text == null) return;
                Build();
            }
            notice.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) noticeText.text = text;
            hideNoticeAt = seconds > 0f ? Time.unscaledTime + seconds : -1f;
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