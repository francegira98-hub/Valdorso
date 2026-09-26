using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace Valdorso.Server
{
    /// <summary>
    /// Un account salvato sul server. La password non c'è: solo la sua impronta (hash) con il sale.
    /// </summary>
    [Serializable]
    public class AccountRecord
    {
        public int version = AccountStore.CurrentVersion;
        public string username;
        public string passwordHash;
        public string passwordSalt;
        public int passwordIterations;
        public string createdAt;
        public string lastLoginAt;
        public string[] characterIds = new string[0];
    }

    /// <summary>
    /// Registro degli account del server: creazione, accesso e salvataggio.
    /// Un file per account in Salvataggi/Account, con copie di sicurezza.
    /// Le password sono protette con PBKDF2-SHA256 e un sale casuale diverso per ogni account.
    /// Pensato per essere sostituito da Steam o Unity Authentication senza toccare personaggi e salvataggi.
    /// </summary>
    public static class AccountStore
    {
        public const int CurrentVersion = 1;
        const int Iterations = 100000;
        const int SaltBytes = 16;
        const int HashBytes = 32;
        const int MinNameLength = 3;
        const int MaxNameLength = 20;
        const int MaxPasswordLength = 128;
        const string WrongCredentials = "Nome utente o password errati.";

        public static string Normalize(string username) => (username ?? string.Empty).Trim().ToLowerInvariant();

        static string RelativePath(string username) => Path.Combine("Account", Normalize(username) + ".json");

        public static string FilePathFor(string username) => ServerStorage.FullPath(RelativePath(username));

        public static bool Exists(string username) => ServerStorage.Exists(RelativePath(username));

        public static bool IsValidUsername(string username, out string error)
        {
            string name = (username ?? string.Empty).Trim();
            if (name.Length < MinNameLength || name.Length > MaxNameLength)
            {
                error = $"Il nome utente deve avere da {MinNameLength} a {MaxNameLength} caratteri.";
                return false;
            }
            foreach (char c in name)
            {
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!allowed)
                {
                    error = "Il nome utente può contenere solo lettere senza accenti, numeri e il trattino basso (_).";
                    return false;
                }
            }
            error = null;
            return true;
        }

        public static bool IsValidPassword(string password, out string error)
        {
            int min = ServerSettings.Current.minPasswordLength;
            if (string.IsNullOrEmpty(password) || password.Length < min)
            {
                error = $"La password deve avere almeno {min} caratteri.";
                return false;
            }
            if (password.Length > MaxPasswordLength)
            {
                error = $"La password può avere al massimo {MaxPasswordLength} caratteri.";
                return false;
            }
            error = null;
            return true;
        }

        public static bool TryCreate(string username, string password, string inviteCode, out AccountRecord account, out string error)
        {
            account = null;
            ServerSettings settings = ServerSettings.Current;
            if (!settings.allowNewAccounts)
            {
                error = "La creazione di nuovi account è chiusa.";
                return false;
            }
            if (!IsValidUsername(username, out error)) return false;
            if (!IsValidPassword(password, out error)) return false;
            if (Exists(username))
            {
                error = "Questo nome utente è già in uso.";
                return false;
            }

            // Gli Amministratori elencati nelle regole del server non hanno bisogno del codice.
            bool needsCode = settings.requireInviteCode && !settings.IsAdmin(username);
            if (needsCode && !InviteCodeStore.IsAvailable(inviteCode, out error)) return false;

            byte[] salt = new byte[SaltBytes];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);

            string now = DateTime.UtcNow.ToString("o");
            account = new AccountRecord
            {
                username = username.Trim(),
                passwordSalt = Convert.ToBase64String(salt),
                passwordIterations = Iterations,
                passwordHash = Convert.ToBase64String(Hash(password, salt, Iterations)),
                createdAt = now,
                lastLoginAt = now
            };
            Save(account);
            if (needsCode) InviteCodeStore.MarkUsed(inviteCode, account.username);
            error = null;
            return true;
        }

        public static bool TryLogin(string username, string password, out AccountRecord account, out string error)
        {
            account = null;
            error = WrongCredentials;
            if (!IsValidUsername(username, out _) || string.IsNullOrEmpty(password)) return false;
            if (!ServerStorage.TryReadJson(RelativePath(username), out AccountRecord stored)) return false;

            Migrate(stored);
            byte[] salt = Convert.FromBase64String(stored.passwordSalt);
            byte[] expected = Convert.FromBase64String(stored.passwordHash);
            byte[] actual = Hash(password, salt, stored.passwordIterations);
            if (!SlowEquals(expected, actual)) return false;

            stored.lastLoginAt = DateTime.UtcNow.ToString("o");
            Save(stored);
            account = stored;
            error = null;
            return true;
        }

        public static void Save(AccountRecord account) => ServerStorage.WriteJson(RelativePath(account.username), account);

        /// <summary>Solo per le prove: cancella un account e le sue copie.</summary>
        public static void DeleteForTests(string username) => ServerStorage.Delete(RelativePath(username));

        /// <summary>Aggiorna i salvataggi delle versioni precedenti (per ora non ce ne sono).</summary>
        static void Migrate(AccountRecord account)
        {
            if (account.characterIds == null) account.characterIds = new string[0];
            account.version = CurrentVersion;
        }

        static byte[] Hash(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return kdf.GetBytes(HashBytes);
        }

        /// <summary>Confronto che impiega sempre lo stesso tempo, per non dare indizi a chi prova a indovinare.</summary>
        static bool SlowEquals(byte[] a, byte[] b)
        {
            uint difference = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++) difference |= (uint)(a[i] ^ b[i]);
            return difference == 0;
        }
    }
}
