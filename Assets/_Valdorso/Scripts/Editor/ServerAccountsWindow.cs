using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Valdorso.Server;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Finestra dell'Amministratore nell'editor: elenca gli account del server di questo PC con i loro personaggi,
    /// e permette di cancellare un personaggio o un intero account (per esempio quelli di prova).
    /// Menu di Unity: Valdorso → Server → Account e personaggi.
    /// Funziona solo fuori dal Play, così i file non cambiano mentre il server li usa.
    /// Sul server ufficiale lo stesso lavoro lo farà il pannello staff (v0.1.6).
    /// </summary>
    public class ServerAccountsWindow : EditorWindow
    {
        class Entry
        {
            public AccountRecord account;
            public List<CharacterRecord> characters;
            public bool admin;
        }

        readonly List<Entry> entries = new List<Entry>();
        Vector2 scroll;

        [MenuItem("Valdorso/Server/Account e personaggi")]
        static void Open()
        {
            var window = GetWindow<ServerAccountsWindow>("Account e personaggi");
            window.minSize = new Vector2(460f, 300f);
            window.Reload();
        }

        void OnFocus() => Reload();

        void Reload()
        {
            entries.Clear();
            string folder = Path.Combine(ServerStorage.RootPath, "Account");
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                string username = Path.GetFileNameWithoutExtension(file);
                if (!AccountStore.TryLoad(username, out AccountRecord account)) continue;
                entries.Add(new Entry
                {
                    account = account,
                    characters = CharacterStore.LoadAll(account),
                    admin = ServerSettings.Current.IsAdmin(account.username)
                });
            }
            entries.Sort((a, b) => string.Compare(a.account.username, b.account.username, System.StringComparison.OrdinalIgnoreCase));
        }

        void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Ferma il Play per modificare account e personaggi.", MessageType.Info);
                return;
            }
            if (GUILayout.Button("Aggiorna l'elenco")) Reload();
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("Nessun account nella cartella dei salvataggi di questo PC.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (Entry e in entries.ToArray())
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(e.account.username + (e.admin ? "  (Amministratore)" : string.Empty), EditorStyles.boldLabel);
                if (GUILayout.Button("Elimina account", GUILayout.Width(130f))) DeleteAccount(e);
                EditorGUILayout.EndHorizontal();

                if (e.characters.Count == 0) EditorGUILayout.LabelField("   nessun personaggio");
                foreach (CharacterRecord c in e.characters)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"   {c.name}  ·  {(string.IsNullOrEmpty(c.faith) ? "nessuna fede" : c.faith)}");
                    if (GUILayout.Button("Elimina", GUILayout.Width(80f))) DeleteCharacter(e, c);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        void DeleteCharacter(Entry e, CharacterRecord c)
        {
            if (!EditorUtility.DisplayDialog("Elimina personaggio",
                    $"Eliminare per sempre {c.name} (account {e.account.username})?\nIl suo nome tornerà libero.", "Elimina", "Annulla"))
                return;
            CharacterStore.DeleteForTests(e.account, c.id);
            Debug.Log($"[Valdorso] Eliminato il personaggio {c.name} dell'account {e.account.username}.");
            Reload();
        }

        void DeleteAccount(Entry e)
        {
            string warning = e.admin ? "\n\nATTENZIONE: è un account Amministratore." : string.Empty;
            if (!EditorUtility.DisplayDialog("Elimina account",
                    $"Eliminare per sempre l'account {e.account.username} e i suoi {e.characters.Count} personaggi?{warning}", "Elimina", "Annulla"))
                return;
            foreach (CharacterRecord c in e.characters) CharacterStore.DeleteForTests(e.account, c.id);
            AccountStore.DeleteForTests(e.account.username);
            Debug.Log($"[Valdorso] Eliminato l'account {e.account.username} con i suoi personaggi.");
            Reload();
        }
    }
}
