using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Valdorso.Server
{
    /// <summary>Un codice d'invito: chi l'ha creato, e chi l'ha usato.</summary>
    [Serializable]
    public class InviteCode
    {
        public string code;
        public string createdAt;
        public string createdBy;
        public string usedBy;
        public string usedAt;
    }

    [Serializable]
    class InviteCodeFile
    {
        public int version = 1;
        public List<InviteCode> codes = new List<InviteCode>();
    }

    /// <summary>
    /// Codici d'invito per creare un account (file Salvataggi/CodiciInvito.json).
    /// Ogni codice vale una volta sola. Formato: VALD-XXXX-XXXX, senza lettere che si confondono (O/0, I/1).
    /// </summary>
    public static class InviteCodeStore
    {
        const string RelativePath = "CodiciInvito.json";
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Normalize(string code) => (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);

        /// <summary>Crea un codice nuovo e lo salva.</summary>
        public static string Generate(string createdBy)
        {
            InviteCodeFile file = Load();
            string code;
            do code = NewCode(); while (file.codes.Exists(c => c.code == code));

            file.codes.Add(new InviteCode
            {
                code = code,
                createdAt = DateTime.UtcNow.ToString("o"),
                createdBy = createdBy
            });
            ServerStorage.WriteJson(RelativePath, file);
            return code;
        }

        public static bool IsAvailable(string code, out string error)
        {
            if (Normalize(code).Length == 0)
            {
                error = "Serve un codice d'invito per creare un account.";
                return false;
            }
            InviteCode entry = Find(Load(), code);
            if (entry == null)
            {
                error = "Codice d'invito non valido.";
                return false;
            }
            if (!string.IsNullOrEmpty(entry.usedBy))
            {
                error = "Questo codice d'invito è già stato usato.";
                return false;
            }
            error = null;
            return true;
        }

        public static void MarkUsed(string code, string username)
        {
            InviteCodeFile file = Load();
            InviteCode entry = Find(file, code);
            if (entry == null) return;
            entry.usedBy = username;
            entry.usedAt = DateTime.UtcNow.ToString("o");
            ServerStorage.WriteJson(RelativePath, file);
        }

        public static int CountUnused() => Load().codes.FindAll(c => string.IsNullOrEmpty(c.usedBy)).Count;

        static InviteCode Find(InviteCodeFile file, string code)
        {
            string normalized = Normalize(code);
            return file.codes.Find(c => c.code == normalized);
        }

        static InviteCodeFile Load()
        {
            if (ServerStorage.TryReadJson(RelativePath, out InviteCodeFile file) && file.codes != null) return file;
            return new InviteCodeFile();
        }

        static string NewCode()
        {
            var bytes = new byte[8];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var chars = new char[8];
            for (int i = 0; i < chars.Length; i++) chars[i] = Alphabet[bytes[i] % Alphabet.Length];
            return $"VALD-{new string(chars, 0, 4)}-{new string(chars, 4, 4)}";
        }
    }
}
