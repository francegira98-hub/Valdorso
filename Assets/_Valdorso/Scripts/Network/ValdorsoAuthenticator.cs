using System.Collections;
using Mirror;
using UnityEngine;
using Valdorso.Server;

namespace Valdorso.Network
{
    /// <summary>Richiesta di accesso mandata dal client (dentro la connessione cifrata).</summary>
    public struct LoginRequestMessage : NetworkMessage
    {
        public string username;
        public string password;
        public bool createAccount;
        public string gameVersion;
    }

    /// <summary>Risposta del server alla richiesta di accesso.</summary>
    public struct LoginResponseMessage : NetworkMessage
    {
        public bool success;
        public string message;
        public string username;
        public bool isAdmin;
    }

    /// <summary>Chi è entrato: il server lo annota sulla connessione dopo l'accesso.</summary>
    public class AccountSession
    {
        public string username;
        public bool isAdmin;
    }

    /// <summary>
    /// Il portiere del server: prima di far entrare un giocatore nel mondo controlla
    /// versione del gioco, nome utente e password (o crea l'account), e rifiuta i doppi accessi.
    /// Va nel campo Authenticator del NetworkManager.
    /// </summary>
    public class ValdorsoAuthenticator : NetworkAuthenticator
    {
        [Header("Solo per le prove nell'editor (da togliere con la schermata di login)")]
        [SerializeField] string testUsername = "prova_archivio";
        [SerializeField] string testPassword = "passwordProva1";

        [Tooltip("Secondi di attesa prima di chiudere la porta a un accesso sbagliato")]
        [SerializeField] float rejectDelay = 1f;

        static string pendingUsername;
        static string pendingPassword;
        static bool pendingCreate;

        /// <summary>Motivo dell'ultimo accesso rifiutato, da mostrare nel menu.</summary>
        public static string LastError { get; private set; }
        public static string LoggedInUsername { get; private set; }
        public static bool IsAdmin { get; private set; }

        /// <summary>La schermata di login li imposta prima di collegarsi.</summary>
        public static void SetCredentials(string username, string password, bool createAccount)
        {
            pendingUsername = username;
            pendingPassword = password;
            pendingCreate = createAccount;
        }

        // ---------- Server ----------

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<LoginRequestMessage>(OnLoginRequest, false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<LoginRequestMessage>();
        }

        void OnLoginRequest(NetworkConnectionToClient conn, LoginRequestMessage msg)
        {
            if (conn.isAuthenticated) return;

            AccountRecord account = null;
            string error;
            bool ok;

            if (msg.gameVersion != Application.version)
            {
                ok = false;
                error = $"La tua versione del gioco ({msg.gameVersion}) è diversa da quella del server ({Application.version}): aggiorna il gioco.";
            }
            else if (IsOnline(msg.username))
            {
                ok = false;
                error = "Questo account è già nel mondo.";
            }
            else if (msg.createAccount)
            {
                ok = AccountStore.TryCreate(msg.username, msg.password, out account, out error);
            }
            else
            {
                ok = AccountStore.TryLogin(msg.username, msg.password, out account, out error);
            }

            if (ok)
            {
                bool admin = ServerSettings.Current.IsAdmin(account.username);
                conn.authenticationData = new AccountSession { username = account.username, isAdmin = admin };
                conn.Send(new LoginResponseMessage
                {
                    success = true,
                    message = msg.createAccount ? "Account creato." : "Accesso riuscito.",
                    username = account.username,
                    isAdmin = admin
                });
                Debug.Log($"[Valdorso] Accesso: {account.username}{(admin ? " (Amministratore)" : "")} da {conn.address}");
                ServerAccept(conn);
            }
            else
            {
                Debug.Log($"[Valdorso] Accesso rifiutato per \"{msg.username}\" da {conn.address}: {error}");
                conn.Send(new LoginResponseMessage { success = false, message = error });
                conn.isAuthenticated = false;
                StartCoroutine(DelayedReject(conn));
            }
        }

        static bool IsOnline(string username)
        {
            string name = AccountStore.Normalize(username);
            foreach (NetworkConnectionToClient other in NetworkServer.connections.Values)
                if (other.authenticationData is AccountSession session && AccountStore.Normalize(session.username) == name)
                    return true;
            return false;
        }

        IEnumerator DelayedReject(NetworkConnectionToClient conn)
        {
            yield return new WaitForSeconds(rejectDelay);
            ServerReject(conn);
        }

        // ---------- Client ----------

        public override void OnStartClient()
        {
            LastError = null;
            NetworkClient.RegisterHandler<LoginResponseMessage>(OnLoginResponse, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<LoginResponseMessage>();
        }

        public override void OnClientAuthenticate()
        {
            string username = pendingUsername;
            string password = pendingPassword;
            bool create = pendingCreate;

            if (string.IsNullOrEmpty(username) && Application.isEditor)
            {
                username = testUsername;
                password = testPassword;
                create = false;
                Debug.Log("[Valdorso] Nessun login inserito: uso l'account di prova dell'editor.");
            }

            NetworkClient.Send(new LoginRequestMessage
            {
                username = username ?? string.Empty,
                password = password ?? string.Empty,
                createAccount = create,
                gameVersion = Application.version
            });

            pendingPassword = null; // la password non resta in memoria più del necessario
        }

        void OnLoginResponse(LoginResponseMessage msg)
        {
            if (msg.success)
            {
                LoggedInUsername = msg.username;
                IsAdmin = msg.isAdmin;
                LastError = null;
                ClientAccept();
            }
            else
            {
                LastError = msg.message;
                ClientReject();
            }
        }
    }
}
