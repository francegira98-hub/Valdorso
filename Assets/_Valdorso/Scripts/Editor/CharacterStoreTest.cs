using System.IO;
using UnityEditor;
using UnityEngine;
using Valdorso.Server;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Prova dell'archivio dei personaggi (menu Valdorso → Prove → Prova archivio personaggi).
    /// Usa l'account di prova "prova_archivio": se manca, lancia prima la prova dell'archivio account.
    /// </summary>
    public static class CharacterStoreTest
    {
        [MenuItem("Valdorso/Prove/Prova archivio personaggi")]
        static void Run()
        {
            if (!AccountStore.TryLoad("prova_archivio", out AccountRecord account))
            {
                Debug.LogError("[Prova] Manca l'account prova_archivio: lancia prima Valdorso → Prove → Prova archivio account.");
                return;
            }

            // Si riparte da zero: via i personaggi delle prove precedenti.
            foreach (string oldId in (string[])account.characterIds.Clone())
                CharacterStore.DeleteForTests(account, oldId);

            bool created = CharacterStore.TryCreate(account, "Aldo d'Orso", out CharacterRecord aldo, out string error);
            Debug.Log(created ? $"[Prova] 1. Personaggio creato: {aldo.name} (id {aldo.id})" : $"[Prova] 1. ERRORE nella creazione: {error}");

            bool duplicate = CharacterStore.TryCreate(account, "aldo D'ORSO", out _, out error);
            Debug.Log(!duplicate ? $"[Prova] 2. Nome già usato (maiuscole diverse) rifiutato: {error}" : "[Prova] 2. ERRORE: nome doppio accettato");

            bool badName = CharacterStore.TryCreate(account, "X7", out _, out error);
            Debug.Log(!badName ? $"[Prova] 3. Nome non valido rifiutato: {error}" : "[Prova] 3. ERRORE: nome non valido accettato");

            CharacterStore.TryCreate(account, "Bruna della Valle", out _, out _);
            CharacterStore.TryCreate(account, "Cesco", out _, out _);
            bool fourth = CharacterStore.TryCreate(account, "Dario", out _, out error);
            Debug.Log(!fourth ? $"[Prova] 4. Quarto personaggio rifiutato: {error}" : "[Prova] 4. ERRORE: superato il massimo di personaggi");

            // Salvataggio e rilettura di posizione e salute.
            aldo.hasPosition = true;
            aldo.posX = 12.5f; aldo.posY = 0f; aldo.posZ = -3f; aldo.rotY = 90f;
            aldo.health = 42f;
            CharacterStore.Save(aldo);
            bool reloaded = CharacterStore.TryLoad(aldo.id, out CharacterRecord again);
            bool same = reloaded && again.health == 42f && again.posX == 12.5f && again.rotY == 90f;
            Debug.Log(same ? "[Prova] 5. Posizione e salute salvate e rilette uguali" : "[Prova] 5. ERRORE: dati riletti diversi");

            // File rovinato apposta: deve tornare l'ultima copia di sicurezza.
            File.WriteAllText(CharacterStore.FilePathFor(aldo.id), "{ questo file è rovinato");
            bool recovered = CharacterStore.TryLoad(aldo.id, out CharacterRecord fromBackup);
            Debug.Log(recovered ? $"[Prova] 6. File rovinato: recuperata la copia di sicurezza di {fromBackup.name} (salute {fromBackup.health})" : "[Prova] 6. ERRORE: nessuna copia recuperata");
            if (recovered) CharacterStore.Save(fromBackup); // si ripara il file

            Debug.Log($"[Prova] 7. Personaggi dell'account: {CharacterStore.LoadAll(account).Count} (massimo {ServerSettings.Current.maxCharactersPerAccount})");
            Debug.Log($"[Prova] 8. Cartella dei personaggi: {Path.GetDirectoryName(CharacterStore.FilePathFor(aldo.id))}");
        }
    }
}
