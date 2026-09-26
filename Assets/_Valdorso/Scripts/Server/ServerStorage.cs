using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Valdorso.Server
{
    /// <summary>
    /// L'archivista del server: scrive e legge i file di salvataggio in formato JSON
    /// nella cartella "Salvataggi" accanto al gioco (nell'editor: nella cartella del progetto).
    /// A ogni scrittura la versione precedente diventa .bak1, quella prima .bak2 e così via.
    /// Se un file è rovinato, legge automaticamente l'ultima copia buona.
    /// Più avanti potrà essere sostituito da un database senza cambiare chi lo usa.
    /// </summary>
    public static class ServerStorage
    {
        public const string FolderName = "Salvataggi";
        const int MaxBackups = 9;

        public static string RootPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName));

        public static string FullPath(string relativePath) => Path.Combine(RootPath, relativePath);

        public static bool Exists(string relativePath) => File.Exists(FullPath(relativePath));

        /// <summary>Salva i dati; la versione precedente diventa una copia di sicurezza.</summary>
        public static void WriteJson<T>(string relativePath, T data, int keepBackups = 3)
        {
            string path = FullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Prima si scrive un file temporaneo: se il PC si spegne a metà, l'originale resta intatto.
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));

            keepBackups = Mathf.Clamp(keepBackups, 0, MaxBackups);
            for (int i = keepBackups; i >= 1; i--)
            {
                string source = i == 1 ? path : BackupPath(path, i - 1);
                string target = BackupPath(path, i);
                if (!File.Exists(source)) continue;
                if (File.Exists(target)) File.Delete(target);
                if (i == 1) File.Copy(source, target);
                else File.Move(source, target);
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        /// <summary>Legge i dati; se il file manca o è rovinato prova le copie di sicurezza.</summary>
        public static bool TryReadJson<T>(string relativePath, out T data) where T : class
        {
            string path = FullPath(relativePath);
            foreach (string candidate in Candidates(path))
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    data = JsonUtility.FromJson<T>(File.ReadAllText(candidate));
                    if (data != null)
                    {
                        if (candidate != path)
                            Debug.LogWarning($"[Valdorso] {path} mancante o rovinato: usata la copia di sicurezza {candidate}");
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Valdorso] File rovinato {candidate}: {e.Message}");
                }
            }
            data = null;
            return false;
        }

        /// <summary>Cancella un file e tutte le sue copie di sicurezza.</summary>
        public static void Delete(string relativePath)
        {
            foreach (string candidate in Candidates(FullPath(relativePath)))
                if (File.Exists(candidate)) File.Delete(candidate);
        }

        static IEnumerable<string> Candidates(string path)
        {
            yield return path;
            for (int i = 1; i <= MaxBackups; i++) yield return BackupPath(path, i);
        }

        static string BackupPath(string path, int index) => path + ".bak" + index;
    }
}
