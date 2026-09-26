using UnityEditor;
using UnityEngine;
using Valdorso.Server;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Prova dell'archivio degli account (menu Valdorso → Prove → Prova archivio account).
    /// Crea l'account "prova_archivio", prova accessi giusti e sbagliati e lascia il file da guardare.
    /// Ogni volta che si rilancia, l'account di prova viene ricreato da capo.
    /// </summary>
    public static class AccountStoreTest
    {
        [MenuItem("Valdorso/Prove/Prova archivio account")]
        static void Run()
        {
            const string name = "prova_archivio";
            const string password = "passwordProva1";
            AccountStore.DeleteForTests(name);

            bool created = AccountStore.TryCreate(name, password, out AccountRecord account, out string error);
            Debug.Log(created ? $"[Prova] 1. Account creato: {account.username}" : $"[Prova] 1. ERRORE nella creazione: {error}");

            bool duplicate = AccountStore.TryCreate("Prova_Archivio", "altraPassword1", out _, out error);
            Debug.Log(!duplicate ? $"[Prova] 2. Nome già usato (anche con maiuscole diverse) rifiutato: {error}" : "[Prova] 2. ERRORE: doppione accettato");

            bool login = AccountStore.TryLogin(name, password, out account, out error);
            Debug.Log(login ? $"[Prova] 3. Accesso con la password giusta riuscito (ultimo accesso {account.lastLoginAt})" : $"[Prova] 3. ERRORE: {error}");

            bool wrong = AccountStore.TryLogin(name, "sbagliata123", out _, out error);
            Debug.Log(!wrong ? $"[Prova] 4. Password sbagliata rifiutata: {error}" : "[Prova] 4. ERRORE: password sbagliata accettata");

            bool badName = AccountStore.TryCreate("a!", password, out _, out error);
            Debug.Log(!badName ? $"[Prova] 5. Nome non valido rifiutato: {error}" : "[Prova] 5. ERRORE: nome non valido accettato");

            bool shortPassword = AccountStore.TryCreate("prova_corta", "123", out _, out error);
            Debug.Log(!shortPassword ? $"[Prova] 6. Password troppo corta rifiutata: {error}" : "[Prova] 6. ERRORE: password corta accettata");

            Debug.Log($"[Prova] 7. File dell'account: {AccountStore.FilePathFor(name)}");
            Debug.Log($"[Prova] 8. Regole del server: {ServerSettings.FilePath}");
        }
    }
}
