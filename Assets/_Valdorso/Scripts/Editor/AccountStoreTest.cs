using UnityEditor;
using UnityEngine;
using Valdorso.Server;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Prova dell'archivio degli account e dei codici d'invito (menu Valdorso → Prove → Prova archivio account).
    /// Crea l'account "prova_archivio" con un codice nuovo, prova accessi giusti e sbagliati e lascia il file da guardare.
    /// </summary>
    public static class AccountStoreTest
    {
        [MenuItem("Valdorso/Prove/Prova archivio account")]
        static void Run()
        {
            const string name = "prova_archivio";
            const string password = "passwordProva1";
            AccountStore.DeleteForTests(name);
            AccountStore.DeleteForTests("prova_senza_codice");
            AccountStore.DeleteForTests("prova_riuso");

            string code = InviteCodeStore.Generate("prova automatica");
            bool created = AccountStore.TryCreate(name, password, code, out AccountRecord account, out string error);
            Debug.Log(created ? $"[Prova] 1. Account creato con il codice {code}: {account.username}" : $"[Prova] 1. ERRORE nella creazione: {error}");

            bool duplicate = AccountStore.TryCreate("Prova_Archivio", "altraPassword1", null, out _, out error);
            Debug.Log(!duplicate ? $"[Prova] 2. Nome già usato (anche con maiuscole diverse) rifiutato: {error}" : "[Prova] 2. ERRORE: doppione accettato");

            bool login = AccountStore.TryLogin(name, password, out account, out error);
            Debug.Log(login ? $"[Prova] 3. Accesso con la password giusta riuscito (ultimo accesso {account.lastLoginAt})" : $"[Prova] 3. ERRORE: {error}");

            bool wrong = AccountStore.TryLogin(name, "sbagliata123", out _, out error);
            Debug.Log(!wrong ? $"[Prova] 4. Password sbagliata rifiutata: {error}" : "[Prova] 4. ERRORE: password sbagliata accettata");

            bool badName = AccountStore.TryCreate("a!", password, null, out _, out error);
            Debug.Log(!badName ? $"[Prova] 5. Nome non valido rifiutato: {error}" : "[Prova] 5. ERRORE: nome non valido accettato");

            bool shortPassword = AccountStore.TryCreate("prova_corta", "123", null, out _, out error);
            Debug.Log(!shortPassword ? $"[Prova] 6. Password troppo corta rifiutata: {error}" : "[Prova] 6. ERRORE: password corta accettata");

            if (ServerSettings.Current.requireInviteCode)
            {
                bool noCode = AccountStore.TryCreate("prova_senza_codice", password, null, out _, out error);
                Debug.Log(!noCode ? $"[Prova] 7. Creazione senza codice rifiutata: {error}" : "[Prova] 7. ERRORE: account creato senza codice");

                bool reused = AccountStore.TryCreate("prova_riuso", password, code, out _, out error);
                Debug.Log(!reused ? $"[Prova] 8. Codice già usato rifiutato: {error}" : "[Prova] 8. ERRORE: codice usato due volte");
            }
            else
            {
                Debug.Log("[Prova] 7-8. Saltate: nelle regole del server i codici d'invito non sono richiesti.");
            }

            Debug.Log($"[Prova] 9. File dell'account: {AccountStore.FilePathFor(name)}");
            Debug.Log($"[Prova] 10. Regole del server: {ServerSettings.FilePath}");
        }
    }
}