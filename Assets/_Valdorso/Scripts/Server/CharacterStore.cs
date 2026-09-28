using System;
using System.Collections.Generic;
using System.IO;

namespace Valdorso.Server
{
    /// <summary>
    /// La scheda di un personaggio salvata sul server (un file per personaggio in Salvataggi/Personaggi).
    /// Contiene già i campi dei passi successivi (aspetto, origine, fede) per non cambiarne la forma dopo.
    /// </summary>
    [Serializable]
    public class CharacterRecord
    {
        public int version = CharacterStore.CurrentVersion;
        public string id;
        public string accountName;
        public string name;
        public string createdAt;
        public string lastPlayedAt;
        public double playTimeSeconds;

        public string birthRace = "Umano";
        public string currentRace = "Umano";
        public string origin = "Popolano";
        public string faith = "";
        public string appearanceRecipe = "";

        // Le risposte al sacerdote (passo 5.8): chiavi di Backstory. I vantaggi si attivano con i sistemi che li usano.
        public string homeland = "";            // da dove vieni
        public string formerTrade = "";         // cosa facevi prima
        public string reason = "";              // perché sei venuto nella valle
        public string keepsake = "";            // il ricordo che porti con te
        public string fear = "";                // cosa temi di più
        public string[] traits = new string[0]; // il carattere: una scelta per ognuna delle quattro coppie
        public string story = "";               // il racconto composto dal registro (ritoccabile)
        public string freeStory = "";           // "La tua storia", scritta dal giocatore

        public string sceneName = "";
        public bool hasPosition;
        public float posX, posY, posZ, rotY;

        // -1 vuol dire "valori pieni" (personaggio appena nato)
        public float health = -1f;
        public float stamina = -1f;
        public float mana = -1f;
        public bool isDead;
    }

    [Serializable]
    class CharacterNameEntry
    {
        public string name;
        public string id;
    }

    [Serializable]
    class CharacterNameIndex
    {
        public int version = 1;
        public List<CharacterNameEntry> entries = new List<CharacterNameEntry>();
    }

    /// <summary>
    /// Archivio dei personaggi: creazione, lettura, salvataggio, collegamento all'account.
    /// I nomi sono unici nel server (elenco in Salvataggi/NomiPersonaggi.json).
    /// </summary>
    public static class CharacterStore
    {
        public const int CurrentVersion = 1;
        const string IndexPath = "NomiPersonaggi.json";
        const int MinNameLength = 2;
        const int MaxNameLength = 24;

        static string RelativePath(string id) => Path.Combine("Personaggi", id + ".json");

        public static string FilePathFor(string id) => ServerStorage.FullPath(RelativePath(id));

        public static string NormalizeName(string name) => (name ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>Lettere (anche accentate), spazi singoli, apostrofi e trattini; deve iniziare con una lettera.</summary>
        public static bool IsValidName(string name, out string error)
        {
            string n = (name ?? string.Empty).Trim();
            if (n.Length < MinNameLength || n.Length > MaxNameLength)
            {
                error = $"Il nome del personaggio deve avere da {MinNameLength} a {MaxNameLength} caratteri.";
                return false;
            }
            if (!char.IsLetter(n[0]))
            {
                error = "Il nome del personaggio deve iniziare con una lettera.";
                return false;
            }
            char previous = ' ';
            foreach (char c in n)
            {
                bool allowed = char.IsLetter(c) || c == ' ' || c == '\'' || c == '’' || c == '-';
                if (!allowed)
                {
                    error = "Il nome può contenere solo lettere, spazi, apostrofi e trattini.";
                    return false;
                }
                if (c == ' ' && previous == ' ')
                {
                    error = "Il nome non può avere due spazi di fila.";
                    return false;
                }
                previous = c;
            }
            error = null;
            return true;
        }

        public static bool IsNameTaken(string name)
        {
            string normalized = NormalizeName(name);
            return LoadIndex().entries.Exists(e => NormalizeName(e.name) == normalized);
        }

        public static bool TryCreate(AccountRecord account, string name, out CharacterRecord character, out string error)
        {
            character = null;
            int max = ServerSettings.Current.maxCharactersPerAccount;
            if (account.characterIds != null && account.characterIds.Length >= max)
            {
                error = $"Hai già {max} personaggi: è il massimo per un account.";
                return false;
            }
            if (!IsValidName(name, out error)) return false;
            if (IsNameTaken(name))
            {
                error = "Questo nome è già di un altro personaggio.";
                return false;
            }

            string now = DateTime.UtcNow.ToString("o");
            character = new CharacterRecord
            {
                id = Guid.NewGuid().ToString("N"),
                accountName = account.username,
                name = name.Trim(),
                createdAt = now,
                lastPlayedAt = now
            };
            Save(character);

            var ids = new List<string>(account.characterIds ?? new string[0]) { character.id };
            account.characterIds = ids.ToArray();
            AccountStore.Save(account);

            CharacterNameIndex index = LoadIndex();
            index.entries.Add(new CharacterNameEntry { name = character.name, id = character.id });
            ServerStorage.WriteJson(IndexPath, index);

            error = null;
            return true;
        }

        public static bool TryLoad(string id, out CharacterRecord character)
        {
            character = null;
            if (string.IsNullOrEmpty(id)) return false;
            if (!ServerStorage.TryReadJson(RelativePath(id), out character)) return false;
            Migrate(character);
            return true;
        }

        /// <summary>Tutti i personaggi di un account, nell'ordine in cui sono stati creati.</summary>
        public static List<CharacterRecord> LoadAll(AccountRecord account)
        {
            var list = new List<CharacterRecord>();
            if (account.characterIds == null) return list;
            foreach (string id in account.characterIds)
                if (TryLoad(id, out CharacterRecord c)) list.Add(c);
            return list;
        }

        public static void Save(CharacterRecord character) => ServerStorage.WriteJson(RelativePath(character.id), character);

        /// <summary>Solo per le prove: cancella un personaggio, lo toglie dall'account e dall'elenco dei nomi.</summary>
        public static void DeleteForTests(AccountRecord account, string id)
        {
            ServerStorage.Delete(RelativePath(id));
            var ids = new List<string>(account.characterIds ?? new string[0]);
            ids.Remove(id);
            account.characterIds = ids.ToArray();
            AccountStore.Save(account);

            CharacterNameIndex index = LoadIndex();
            index.entries.RemoveAll(e => e.id == id);
            ServerStorage.WriteJson(IndexPath, index);
        }

        /// <summary>Aggiorna le schede delle versioni precedenti (per ora non ce ne sono).</summary>
        static void Migrate(CharacterRecord character)
        {
            character.version = CurrentVersion;
        }

        static CharacterNameIndex LoadIndex()
        {
            if (ServerStorage.TryReadJson(IndexPath, out CharacterNameIndex index) && index.entries != null) return index;
            return new CharacterNameIndex();
        }
    }
}