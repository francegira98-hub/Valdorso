using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valdorso.Creatures;
using Valdorso.Server;
using Valdorso.Stats;
using Valdorso.UI;

namespace Valdorso.Network
{
    /// <summary>
    /// Il NetworkManager di Valdorso, il "maestro di cerimonie" del server.
    /// Dopo l'accesso nessuno nasce da solo: il PC chiede i suoi personaggi (anticamera).
    /// Chi non ne ha apre il Registro di Val d'Orso (scena Creazione) e ne crea uno;
    /// chi ne ha entra nel mondo dove l'aveva lasciato, con salute, stamina e mana salvati.
    /// Salva i personaggi quando escono, ogni autosaveInterval secondi e quando il server si spegne.
    /// </summary>
    public class ValdorsoNetworkManager : NetworkManager
    {
        [Header("Valdorso")]
        [Tooltip("Ogni quanti secondi il server salva tutti i personaggi presenti")]
        [SerializeField] float autosaveInterval = 60f;
        [Tooltip("Salute (frazione del massimo) di chi era morto quando è uscito; provvisorio fino alla rinascita al villaggio")]
        [SerializeField] float revivedHealthFraction = 0.25f;
        [Tooltip("Nome della scena di creazione del personaggio (deve essere nella Scene List)")]
        [SerializeField] string creationScene = "Creazione";
        [Tooltip("Lunghezza massima della ricetta dell'aspetto accettata alla creazione (caratteri)")]
        [SerializeField] int maxRecipeLength = 8000;
        [Tooltip("Peso massimo del ritratto accettato alla creazione (byte)")]
        [SerializeField] int maxPortraitBytes = 200000;

        class ActiveCharacter
        {
            public CharacterRecord record;
            public GameObject player;
            public double lastSaveTime;
        }

        // Le fedi che il sacerdote scrive nel registro (vuoto = nessuna). Gli dei oscuri non si dichiarano.
        static readonly HashSet<string> AllowedFaiths = new HashSet<string> { "", "Solara", "Ignar", "Nereia", "Torvald", "Zefira", "Vecchi Dei" };

        readonly Dictionary<NetworkConnectionToClient, ActiveCharacter> active = new Dictionary<NetworkConnectionToClient, ActiveCharacter>();
        double nextAutosave;

        // Lato PC
        static Action<bool, string, string> pendingCreate;
        bool listRequested;

        public override void Awake()
        {
            // I giocatori non nascono da soli: prima l'anticamera decide se creare un personaggio o entrare con uno esistente.
            autoCreatePlayer = false;
            base.Awake();
        }

        public override void Update()
        {
            base.Update();
            AskForCharactersWhenReady();

            if (!NetworkServer.active || Time.unscaledTimeAsDouble < nextAutosave) return;
            nextAutosave = Time.unscaledTimeAsDouble + autosaveInterval;
            SaveAll("salvataggio automatico");
        }

        // =====================================================================
        // SERVER
        // =====================================================================

        public override void OnStartServer()
        {
            base.OnStartServer();
            nextAutosave = Time.unscaledTimeAsDouble + autosaveInterval;
            NetworkServer.RegisterHandler<CharacterListRequest>(OnCharacterListRequest);
            NetworkServer.RegisterHandler<CreateCharacterRequest>(OnCreateCharacterRequest);
            NetworkServer.RegisterHandler<EnterWorldRequest>(OnEnterWorldRequest);
        }

        public override void OnStopServer()
        {
            SaveAll("spegnimento del server");
            active.Clear();
            NetworkServer.UnregisterHandler<CharacterListRequest>();
            NetworkServer.UnregisterHandler<CreateCharacterRequest>();
            NetworkServer.UnregisterHandler<EnterWorldRequest>();
            base.OnStopServer();
        }

        /// <summary>Con l'anticamera nessuno chiede di nascere così: si entra solo con EnterWorldRequest.</summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            Debug.LogWarning($"[Valdorso] Richiesta di giocatore ignorata per la connessione {conn.connectionId}: si entra dall'anticamera.");
        }

        static bool TryGetAccount(NetworkConnectionToClient conn, out AccountRecord account)
        {
            account = null;
            return conn.authenticationData is AccountSession session && AccountStore.TryLoad(session.username, out account);
        }

        void OnCharacterListRequest(NetworkConnectionToClient conn, CharacterListRequest msg)
        {
            if (!TryGetAccount(conn, out AccountRecord account))
            {
                conn.Disconnect();
                return;
            }
            List<CharacterRecord> characters = CharacterStore.LoadAll(account);
            var summaries = new CharacterSummary[characters.Count];
            for (int i = 0; i < characters.Count; i++)
                summaries[i] = new CharacterSummary { id = characters[i].id, name = characters[i].name };

            conn.Send(new CharacterListResponse
            {
                characters = summaries,
                maxCharacters = ServerSettings.Current.maxCharactersPerAccount
            });
        }

        void OnCreateCharacterRequest(NetworkConnectionToClient conn, CreateCharacterRequest msg)
        {
            if (!TryGetAccount(conn, out AccountRecord account))
            {
                conn.Disconnect();
                return;
            }
            if (active.ContainsKey(conn))
            {
                RefuseCreation(conn, "Sei già nel mondo.");
                return;
            }

            string recipe = msg.appearanceRecipe ?? string.Empty;
            if (recipe.Length > maxRecipeLength || (recipe.Length > 0 && !(recipe.StartsWith("BB*") || recipe.StartsWith("AA*"))))
            {
                RefuseCreation(conn, "L'aspetto scelto non è valido.");
                return;
            }
            string faith = (msg.faith ?? string.Empty).Trim();
            if (!AllowedFaiths.Contains(faith))
            {
                RefuseCreation(conn, "Il sacerdote non conosce questa fede.");
                return;
            }

            CharacterRecord created = null;
            string error = null;
            if (string.IsNullOrWhiteSpace(msg.name))
            {
                // Finché non c'è la pagina del nome (passo 5.2): un nome provvisorio preso dall'account.
                foreach (string candidate in ProvisionalNames(account.username))
                    if (CharacterStore.TryCreate(account, candidate, out created, out error)) break;
            }
            else
            {
                CharacterStore.TryCreate(account, msg.name, out created, out error);
            }

            if (created == null)
            {
                RefuseCreation(conn, error ?? "Il sacerdote non riesce a scrivere questo nome nel registro.");
                return;
            }

            created.faith = faith;
            created.appearanceRecipe = recipe;
            CharacterStore.Save(created);
            SavePortrait(created, msg.portrait);
            Debug.Log($"[Valdorso] {account.username} ha scritto nel registro il personaggio {created.name}.");
            conn.Send(new CreateCharacterResponse { success = true, message = string.Empty, characterId = created.id, characterName = created.name });
        }

        // ---------- Ritratti ----------

        /// <summary>Dove il server conserva il ritratto di un personaggio (Salvataggi/Ritratti/id.jpg).</summary>
        public static string PortraitPath(string characterId) =>
            ServerStorage.FullPath(System.IO.Path.Combine("Ritratti", characterId + ".jpg"));

        void SavePortrait(CharacterRecord character, byte[] portrait)
        {
            if (portrait == null || portrait.Length == 0) return;
            // Solo immagini JPG (cominciano con FF D8) e non troppo pesanti.
            if (portrait.Length > maxPortraitBytes || portrait.Length < 4 || portrait[0] != 0xFF || portrait[1] != 0xD8)
            {
                Debug.LogWarning($"[Valdorso] Ritratto di {character.name} rifiutato (non è un JPG o pesa troppo: {portrait.Length} byte).");
                return;
            }
            try
            {
                string path = PortraitPath(character.id);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path, portrait);
                Debug.Log($"[Valdorso] Ritratto di {character.name} salvato ({portrait.Length / 1024} KB).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Valdorso] Ritratto di {character.name} non salvato: {e.Message}");
            }
        }

        static void RefuseCreation(NetworkConnectionToClient conn, string message)
        {
            conn.Send(new CreateCharacterResponse { success = false, message = message });
        }

        void OnEnterWorldRequest(NetworkConnectionToClient conn, EnterWorldRequest msg)
        {
            if (!TryGetAccount(conn, out AccountRecord account))
            {
                conn.Disconnect();
                return;
            }
            if (active.ContainsKey(conn) || conn.identity != null) return; // è già dentro

            bool owned = account.characterIds != null && Array.IndexOf(account.characterIds, msg.characterId) >= 0;
            if (!owned || !CharacterStore.TryLoad(msg.characterId, out CharacterRecord character))
            {
                Debug.LogWarning($"[Valdorso] {account.username} ha chiesto di entrare con un personaggio che non è suo o non esiste.");
                return;
            }
            SpawnCharacter(conn, account, character);
        }

        /// <summary>Fa comparire il personaggio dove l'aveva lasciato (o al punto di partenza), con i valori salvati.</summary>
        void SpawnCharacter(NetworkConnectionToClient conn, AccountRecord account, CharacterRecord character)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            Vector3 position;
            Quaternion rotation;
            bool returning = character.hasPosition && character.sceneName == sceneName;
            if (returning)
            {
                position = new Vector3(character.posX, character.posY + 0.1f, character.posZ);
                rotation = Quaternion.Euler(0f, character.rotY, 0f);
            }
            else
            {
                Transform start = GetStartPosition();
                position = start != null ? start.position : Vector3.zero;
                rotation = start != null ? start.rotation : Quaternion.identity;
            }

            GameObject player = Instantiate(playerPrefab, position, rotation);
            player.name = $"{playerPrefab.name} [{character.name}]";
            // L'aspetto salvato nella scheda parte insieme al personaggio, così ogni PC lo riceve subito.
            if (player.TryGetComponent(out UmaAppearance appearance)) appearance.SetRecipeOnServer(character.appearanceRecipe);
            NetworkServer.AddPlayerForConnection(conn, player);

            if (player.TryGetComponent(out Creature creature)) creature.SetDisplayName(character.name);
            if (player.TryGetComponent(out CreatureStats stats))
            {
                if (character.isDead) stats.SetVitals(stats.MaxHealth * revivedHealthFraction, -1f, -1f);
                else stats.SetVitals(character.health, character.stamina, character.mana);
            }

            character.lastPlayedAt = DateTime.UtcNow.ToString("o");
            CharacterStore.Save(character);
            active[conn] = new ActiveCharacter { record = character, player = player, lastSaveTime = Time.unscaledTimeAsDouble };
            Debug.Log($"[Valdorso] {account.username} entra con {character.name}{(returning ? ", dove l'aveva lasciato" : ", al punto di partenza")}.");
        }

        static IEnumerable<string> ProvisionalNames(string username)
        {
            var letters = new System.Text.StringBuilder();
            foreach (char c in username)
                if (char.IsLetter(c)) letters.Append(c);
            string baseName = letters.Length >= 2
                ? char.ToUpperInvariant(letters[0]) + letters.ToString(1, letters.Length - 1)
                : "Viandante";
            yield return baseName;
            yield return baseName + " di Valdorso";
            yield return baseName + " il Viandante";
        }

        // ---------- Salvataggio ----------

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (active.TryGetValue(conn, out ActiveCharacter entry))
            {
                SaveCharacter(entry);
                active.Remove(conn);
                Debug.Log($"[Valdorso] {entry.record.name} è uscito: personaggio salvato.");
            }
            base.OnServerDisconnect(conn);
        }

        public void SaveAll(string reason)
        {
            if (active.Count == 0) return;
            foreach (ActiveCharacter entry in active.Values) SaveCharacter(entry);
            Debug.Log($"[Valdorso] Salvati {active.Count} personaggi ({reason}).");
        }

        void SaveCharacter(ActiveCharacter entry)
        {
            if (entry.player == null) return;
            CharacterRecord c = entry.record;

            Transform t = entry.player.transform;
            c.sceneName = SceneManager.GetActiveScene().name;
            c.hasPosition = true;
            c.posX = t.position.x;
            c.posY = t.position.y;
            c.posZ = t.position.z;
            c.rotY = t.eulerAngles.y;

            if (entry.player.TryGetComponent(out CreatureStats stats))
            {
                c.health = stats.Health;
                c.stamina = stats.Stamina;
                c.mana = stats.Mana;
            }
            if (entry.player.TryGetComponent(out Creature creature)) c.isDead = creature.IsDead;
            // L'aspetto (anche quello casuale appena inventato) torna nella scheda.
            if (entry.player.TryGetComponent(out UmaAppearance appearance) && !string.IsNullOrEmpty(appearance.Recipe))
                c.appearanceRecipe = appearance.Recipe;

            double now = Time.unscaledTimeAsDouble;
            c.playTimeSeconds += now - entry.lastSaveTime;
            entry.lastSaveTime = now;
            c.lastPlayedAt = DateTime.UtcNow.ToString("o");

            try
            {
                CharacterStore.Save(c);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Valdorso] Salvataggio di {c.name} non riuscito: {e.Message}");
            }
        }

        // =====================================================================
        // PC (anticamera)
        // =====================================================================

        public override void OnStartClient()
        {
            base.OnStartClient();
            listRequested = false;
            NetworkClient.RegisterHandler<CharacterListResponse>(OnCharacterList);
            NetworkClient.RegisterHandler<CreateCharacterResponse>(OnCreateResponse);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            listRequested = false;
            pendingCreate = null;
        }

        // Appena il PC è entrato, pronto e con la scena del mondo caricata, chiede i suoi personaggi (una volta sola).
        void AskForCharactersWhenReady()
        {
            if (listRequested || !NetworkClient.isConnected || NetworkClient.connection == null) return;
            if (!NetworkClient.connection.isAuthenticated || !NetworkClient.ready || NetworkClient.localPlayer != null) return;
            if (NetworkClient.isLoadingScene || loadingSceneAsync != null) return;
            if (!string.IsNullOrEmpty(onlineScene) && !Utils.IsSceneActive(onlineScene)) return;

            listRequested = true;
            NetworkClient.Send(new CharacterListRequest());
        }

        void OnCharacterList(CharacterListResponse msg)
        {
            if (msg.characters != null && msg.characters.Length > 0)
            {
                // Fino al passo 6 (scelta del personaggio) si entra con il primo.
                RequestEnterWorld(msg.characters[0].id);
                return;
            }
            OpenCreation();
        }

        void OpenCreation()
        {
            if (SceneManager.GetSceneByName(creationScene).isLoaded) return;
            Debug.Log("[Valdorso] Nessun personaggio: si apre il Registro di Val d'Orso.");
            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeOut(0.15f, LoadCreation);
            else LoadCreation();
        }

        void LoadCreation()
        {
            // La scena si carica solo su questo PC, sopra il mondo (il palco sta 500 metri più in basso).
            AsyncOperation op = SceneManager.LoadSceneAsync(creationScene, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[Valdorso] Non riesco ad aprire la scena {creationScene}: è nella Scene List?");
                if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn();
                return;
            }
            op.completed += _ =>
            {
                if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn();
            };
        }

        void OnCreateResponse(CreateCharacterResponse msg)
        {
            Action<bool, string, string> callback = pendingCreate;
            pendingCreate = null;
            callback?.Invoke(msg.success, msg.message, msg.characterId);
        }

        /// <summary>Chiede al server di scrivere un personaggio nel registro; la risposta arriva nel callback (riuscito, messaggio, id).</summary>
        public static void RequestCreateCharacter(string name, string faith, string appearanceRecipe, byte[] portrait, Action<bool, string, string> callback)
        {
            if (!NetworkClient.isConnected)
            {
                callback?.Invoke(false, "Non sei collegato al server.", null);
                return;
            }
            pendingCreate = callback;
            NetworkClient.Send(new CreateCharacterRequest { name = name, faith = faith, appearanceRecipe = appearanceRecipe, portrait = portrait });
        }

        /// <summary>Chiede al server di entrare nel mondo con questo personaggio.</summary>
        public static void RequestEnterWorld(string characterId)
        {
            if (NetworkClient.isConnected) NetworkClient.Send(new EnterWorldRequest { characterId = characterId });
        }
    }
}