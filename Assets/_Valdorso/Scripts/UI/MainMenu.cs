using System;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valdorso.Network;

namespace Valdorso.UI
{
    /// <summary>
    /// Menu principale: entrare nel mondo, impostazioni, uscita.
    /// Il pulsante "Avvia server (sviluppo)" compare solo nell'editor e nelle build di sviluppo:
    /// nel gioco pubblicato esiste un solo server, quello ufficiale.
    /// Avviando il gioco con l'opzione -server, parte direttamente come server.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Pulsanti")]
        [SerializeField] Button enterButton;
        [SerializeField] Button hostButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;

        [Header("Testi e pannelli")]
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text versionText;
        [SerializeField] GameObject settingsPanel;

        [Header("Dissolvenza")]
        [Tooltip("Secondi per sfumare nel nero prima di entrare nel mondo")]
        [SerializeField] float fadeOutDuration = 0.4f;

        bool connecting;
        bool fadingOut;

        void Start()
        {
            bool devTools = Application.isEditor || Debug.isDebugBuild;
            if (hostButton != null)
            {
                hostButton.gameObject.SetActive(devTools);
                hostButton.onClick.AddListener(StartDevServer);
            }
            if (enterButton != null) enterButton.onClick.AddListener(EnterWorld);
            if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettings);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);

            if (versionText != null) versionText.text = $"v{Application.version}";
            if (settingsPanel != null) settingsPanel.SetActive(false);
            SetStatus(string.Empty);

            if (HasCommandLineArg("-server")) StartDedicatedServer();
        }

        void Update()
        {
            if (!connecting) return;

            if (NetworkClient.isConnected)
            {
                SetStatus("Ingresso nel mondo...");
                if (!fadingOut && ScreenFader.Instance != null)
                {
                    fadingOut = true;
                    ScreenFader.Instance.FadeOut(fadeOutDuration);
                }
                return;
            }

            // Il tentativo è finito senza connessione: il server non ha risposto.
            if (!NetworkClient.active)
            {
                connecting = false;
                SetButtons(true);
                SetStatus("Impossibile raggiungere il server.\nControlla l'indirizzo nel file valdorso_config.json e che il server sia acceso.");
            }
        }

        void EnterWorld()
        {
            if (NetworkClient.active || NetworkServer.active) return;
            NetworkManager manager = GetManager();
            if (manager == null) return;

            ServerConfig config = ServerConfig.Current;
            ApplyConfig(manager, config);

            connecting = true;
            fadingOut = false;
            SetButtons(false);
            SetStatus($"Connessione a {config.serverAddress}...");
            manager.StartClient();
        }

        void StartDevServer()
        {
            if (NetworkClient.active || NetworkServer.active) return;
            NetworkManager manager = GetManager();
            if (manager == null) return;

            ApplyConfig(manager, ServerConfig.Current);
            SetButtons(false);
            SetStatus("Avvio del server...");
            Debug.Log($"[Valdorso] Avvio del server di sviluppo sulla porta {ServerConfig.Current.port}.");

            // Il server parte subito; il sipario si chiude mentre il mondo si carica.
            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeOut(fadeOutDuration);
            manager.StartHost();
            Debug.Log($"[Valdorso] Server avviato: {NetworkServer.active}, caricamento del mondo in corso.");
        }

        void StartDedicatedServer()
        {
            NetworkManager manager = GetManager();
            if (manager == null) return;

            ApplyConfig(manager, ServerConfig.Current);
            Debug.Log($"[Valdorso] Avvio come server dedicato sulla porta {ServerConfig.Current.port}.");
            manager.StartServer();
        }

        static void ApplyConfig(NetworkManager manager, ServerConfig config)
        {
            manager.networkAddress = config.serverAddress;
            if (Transport.active is PortTransport portTransport)
                portTransport.Port = (ushort)config.port;
        }

        NetworkManager GetManager()
        {
            if (NetworkManager.singleton != null) return NetworkManager.singleton;
            SetStatus("Errore: manca il NetworkManager nella scena Menu.");
            return null;
        }

        void ToggleSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SetButtons(bool interactable)
        {
            if (enterButton != null) enterButton.interactable = interactable;
            if (hostButton != null) hostButton.interactable = interactable;
        }

        void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        static bool HasCommandLineArg(string arg)
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (string.Equals(a, arg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}