using System;
using System.IO;
using UnityEngine;

namespace Valdorso.Server
{
    /// <summary>
    /// Regole del server, lette dal file valdorso_server.json accanto al gioco
    /// (nell'editor: nella cartella del progetto). Il file esiste solo sul server e non va su GitHub.
    /// Se manca viene creato con i valori predefiniti.
    /// </summary>
    [Serializable]
    public class ServerSettings
    {
        [Tooltip("Nomi utente con il ruolo di Amministratore")]
        public string[] adminAccounts = new string[0];
        public int maxCharactersPerAccount = 3;
        public bool allowNewAccounts = true;

        [Tooltip("Per creare un account serve un codice d'invito (gli Amministratori ne sono esenti)")]
        public bool requireInviteCode = true;
        public int minPasswordLength = 8;

        const string FileName = "valdorso_server.json";
        static ServerSettings current;

        public static string FilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", FileName));

        public static ServerSettings Current
        {
            get
            {
                if (current == null) current = Load();
                return current;
            }
        }

        public bool IsAdmin(string username)
        {
            if (string.IsNullOrWhiteSpace(username) || adminAccounts == null) return false;
            foreach (string admin in adminAccounts)
                if (string.Equals(admin?.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static ServerSettings Load()
        {
            string path = FilePath;
            try
            {
                if (File.Exists(path))
                {
                    ServerSettings loaded = JsonUtility.FromJson<ServerSettings>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                    Debug.LogWarning($"[Valdorso] Il file delle regole del server non è valido, uso i valori predefiniti: {path}");
                    return new ServerSettings();
                }

                var fresh = new ServerSettings();
                File.WriteAllText(path, JsonUtility.ToJson(fresh, true));
                Debug.Log($"[Valdorso] Creato il file delle regole del server: {path}");
                return fresh;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Valdorso] Impossibile leggere o creare le regole del server ({e.Message}), uso i valori predefiniti.");
                return new ServerSettings();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
        }
    }
}
