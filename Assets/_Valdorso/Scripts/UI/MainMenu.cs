using System;
using System.Collections;
using System.Threading.Tasks;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valdorso.Network;
using Valdorso.Server;

namespace Valdorso.UI
{
    /// <summary>
    /// Menu principale: stato del server, entrare nel mondo (dopo la finestra di accesso), impostazioni, uscita.
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
        [SerializeField] TMP_Text serverStatusText;
        [SerializeField] TMP_Text versionText;
        [SerializeField] GameObject settingsPanel;
        [SerializeField] LoginPanel loginPanel;
        [SerializeField] ValdorsoTheme theme;

        [Header("Tempi")]
        [Tooltip("Secondi per sfumare nel nero prima di entrare nel mondo")]
        [SerializeField] float fadeOutDuration = 0.4f;
        [Tooltip("Ogni quanti secondi chiedere lo stato del server")]
        [SerializeField] float serverStatusInterval = 5f;

        bool connecting;
        bool fadingOut;
        bool querying;

        void Start()
        {
            bool devTools = Application.isEditor || Debug.isDebugBuild;
            if (hostButton != null)
            {
                hostButton.gameObject.SetActive(devTools);
                hostButton.onClick.AddListener(() => OpenLogin(true, null));
            }
            if (enterButton != null) enterButton.onClick.AddListener(() => OpenLogin(false, null));
            if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettings);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);

            if (versionText != null) versionText.text = $"v{Application.version}";
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (loginPanel != null) loginPanel.Close();
            SetStatus(string.Empty);

            if (HasCommandLineArg("-server"))
            {
                StartDedicatedServer();
                return;
            }
            StartCoroutine(PollServerStatus());
        }

        void Update()
        {
            if (!connecting) return;

            if (NetworkClient.isConnected)
            {
                // La connessione è aperta, ma si entra solo dopo il via libera del portiere.
                bool authenticated = NetworkClient.connection != null && NetworkClient.connection.isAuthenticated;
                SetStatus(authenticated ? "Ingresso nel mondo..." : "Accesso in corso...");
                if (authenticated && !fadingOut && ScreenFader.Instance != null)
                {
                    fadingOut = true;
                    ScreenFader.Instance.FadeOut(fadeOutDuration);
                }
                return;
            }

            // Il tentativo è finito senza entrare: server irraggiungibile o accesso rifiutato.
            if (!NetworkClient.active)
            {
                connecting = false;
                SetButtons(true);
                if (fadingOut && ScreenFader.Instance != null) ScreenFader.Instance.FadeIn();
                fadingOut = false;

                string reason = ValdorsoAuthenticator.LastError;
                if (!string.IsNullOrEmpty(reason))
                {
                    SetStatus(string.Empty);
                    OpenLogin(false, reason); // la finestra si riapre con il motivo
                }
                else
                {
                    SetStatus("Impossibile raggiungere il server.\nControlla l'indirizzo nel file valdorso_config.json e che il server sia acceso.");
                }
            }
        }

        // ---------- Stato del server ----------

        IEnumerator PollServerStatus()
        {
            if (serverStatusText != null) serverStatusText.text = "Server di Valdorso: verifica in corso...";
            var wait = new WaitForSecondsRealtime(serverStatusInterval);
            while (true)
            {
                if (!connecting && !NetworkClient.active && !NetworkServer.active) _ = RefreshServerStatus();
                yield return wait;
            }
        }

        async Task RefreshServerStatus()
        {
            if (querying || serverStatusText == null) return;
            querying = true;
            ServerConfig config = ServerConfig.Current;
            ServerStatusInfo info = await ServerStatusProbe.QueryAsync(config.serverAddress, config.statusPort);
            querying = false;
            if (this == null || serverStatusText == null) return; // il menu è stato chiuso nel frattempo

            if (info == null)
                ShowServerStatus("Server di Valdorso: chiuso", theme != null ? theme.blood : Color.red);
            else if (info.version != Application.version)
                ShowServerStatus($"Server di Valdorso: aperto, ma con la versione {info.version}. Aggiorna il gioco.", theme != null ? theme.ember : Color.yellow);
            else
                ShowServerStatus($"Server di Valdorso: aperto · {Adventurers(info.players)} nella valle", theme != null ? theme.life : Color.green);
        }

        void ShowServerStatus(string message, Color color)
        {
            serverStatusText.text = message;
            serverStatusText.color = Color.Lerp(color, Color.white, 0.25f);
        }

        static string Adventurers(int count) =>
            count == 0 ? "nessun avventuriero" : count == 1 ? "1 avventuriero" : $"{count} avventurieri";

        // ---------- Accesso ----------

        void OpenLogin(bool asHost, string error)
        {
            if (NetworkClient.active || NetworkServer.active) return;
            if (loginPanel == null)
            {
                SetStatus("Errore: manca la finestra di accesso nel menu.");
                return;
            }

            loginPanel.Open((username, password, create) =>
            {
                string inviteCode = loginPanel.InviteCode;
                if (asHost) StartDevServer(username, password, create, inviteCode);
                else EnterWorld(username, password, create, inviteCode);
            }, error);
        }

        void EnterWorld(string username, string password, bool create, string inviteCode)
        {
            if (NetworkClient.active || NetworkServer.active) return;
            NetworkManager manager = GetManager();
            if (manager == null) return;

            ServerConfig config = ServerConfig.Current;
            ApplyConfig(manager, config);
            ValdorsoAuthenticator.SetCredentials(username, password, create, inviteCode);

            connecting = true;
            fadingOut = false;
            SetButtons(false);
            SetStatus($"Connessione a {config.serverAddress}...");
            manager.StartClient();
        }

        void StartDevServer(string username, string password, bool create, string inviteCode)
        {
            if (NetworkClient.active || NetworkServer.active) return;
            NetworkManager manager = GetManager();
            if (manager == null) return;

            // Il server è questo PC: l'account si controlla subito, prima di avviare il mondo.
            bool ok = create
                ? AccountStore.TryCreate(username, password, inviteCode, out _, out string error)
                : AccountStore.TryLogin(username, password, out _, out error);
            if (!ok)
            {
                OpenLogin(true, error);
                return;
            }
            ValdorsoAuthenticator.SetCredentials(username, password, false);

            ApplyConfig(manager, ServerConfig.Current);
            SetButtons(false);
            SetStatus("Avvio del server...");
            Debug.Log($"[Valdorso] Avvio del server di sviluppo sulla porta {ServerConfig.Current.port}.");

            // Il server parte subito; il sipario si chiude mentre il mondo si carica.
            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeOut(fadeOutDuration);
            manager.StartHost();
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