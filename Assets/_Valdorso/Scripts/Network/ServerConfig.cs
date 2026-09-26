using System;
using System.IO;
using UnityEngine;

namespace Valdorso.Network
{
    /// <summary>
    /// Indirizzo e porta del server, letti dal file valdorso_config.json
    /// che sta accanto al gioco (nell'editor: nella cartella del progetto).
    /// Se il file non esiste viene creato con i valori predefiniti.
    /// Per collegarsi via VPN o al server ufficiale basta cambiare l'indirizzo nel file.
    /// </summary>
    [Serializable]
    public class ServerConfig
    {
        public string serverAddress = "localhost";
        public int port = 7777;

        [UnityEngine.Tooltip("Porta per chiedere lo stato del server dal menu (UDP)")]
        public int statusPort = 7778;

        const string FileName = "valdorso_config.json";
        static ServerConfig current;

        /// <summary>Percorso completo del file di configurazione.</summary>
        public static string FilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", FileName));

        /// <summary>La configurazione in uso (letta dal file la prima volta che serve).</summary>
        public static ServerConfig Current
        {
            get
            {
                if (current == null) current = Load();
                return current;
            }
        }

        static ServerConfig Load()
        {
            string path = FilePath;
            try
            {
                if (File.Exists(path))
                {
                    ServerConfig loaded = JsonUtility.FromJson<ServerConfig>(File.ReadAllText(path));
                    if (loaded != null && !string.IsNullOrWhiteSpace(loaded.serverAddress) && loaded.port > 0 && loaded.port <= 65535)
                        return loaded;

                    Debug.LogWarning($"[Valdorso] Il file di configurazione non è valido, uso i valori predefiniti: {path}");
                    return new ServerConfig();
                }

                var fresh = new ServerConfig();
                File.WriteAllText(path, JsonUtility.ToJson(fresh, true));
                Debug.Log($"[Valdorso] Creato il file di configurazione: {path}");
                return fresh;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Valdorso] Impossibile leggere o creare il file di configurazione ({e.Message}), uso i valori predefiniti.");
                return new ServerConfig();
            }
        }

        // A ogni Play nell'editor il file viene riletto, così le modifiche si vedono subito.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
        }
    }
}