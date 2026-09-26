using UnityEditor;
using UnityEngine;
using Valdorso.Server;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Comandi dell'Amministratore nell'editor (menu Valdorso → Server).
    /// Agiscono sui file del server che gira da questo PC. Sul server dedicato arriverà il pannello staff.
    /// </summary>
    public static class ServerTools
    {
        [MenuItem("Valdorso/Server/Genera codice d'invito")]
        static void GenerateInvite()
        {
            string code = InviteCodeStore.Generate("Amministratore (editor)");
            EditorGUIUtility.systemCopyBuffer = code;
            Debug.Log($"[Valdorso] Nuovo codice d'invito: {code} (copiato negli appunti). Codici non ancora usati: {InviteCodeStore.CountUnused()}");
            EditorUtility.DisplayDialog("Codice d'invito", $"{code}\n\nÈ già copiato negli appunti: incollalo nel messaggio per il tuo amico.", "OK");
        }

        [MenuItem("Valdorso/Server/Apri cartella dei salvataggi")]
        static void OpenSaves()
        {
            System.IO.Directory.CreateDirectory(ServerStorage.RootPath);
            EditorUtility.RevealInFinder(ServerStorage.RootPath);
        }

        [MenuItem("Valdorso/Server/Apri regole del server")]
        static void OpenSettings()
        {
            string path = ServerSettings.FilePath;
            if (!System.IO.File.Exists(path)) _ = ServerSettings.Current; // lo crea se manca
            EditorUtility.OpenWithDefaultApp(path);
        }
    }
}
