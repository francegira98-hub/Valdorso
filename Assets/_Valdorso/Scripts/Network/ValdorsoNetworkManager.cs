using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valdorso.Creatures;
using Valdorso.Server;
using Valdorso.Stats;
using Valdorso.UI;
using Valdorso.World;
using Valdorso.WorldEvents;

namespace Valdorso.Network
{
    /// <summary>
    /// Il NetworkManager di Valdorso, il "maestro di cerimonie" del server.
    /// Dopo l'accesso nessuno nasce da solo: il PC chiede i suoi personaggi (anticamera)
    /// e apre il Registro di Val d'Orso (scena Creazione): chi non ha personaggi ne crea uno,
    /// chi ne ha li vede con i ritratti e sceglie con chi entrare (oppure ne crea o cancella uno).
    /// Si entra nel mondo dove si era usciti, con salute, stamina e mana salvati.
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
        [Tooltip("Secondi di attesa per lasciare la valle fuori dai luoghi sicuri (anche chi chiude il gioco di colpo resta tanto nel mondo)")]
        [SerializeField] float leaveDelay = 20f;
        [Tooltip("Di quanti metri ci si può spostare durante l'attesa prima che l'uscita si annulli")]
        [SerializeField] float leaveMoveTolerance = 0.5f;

        class ActiveCharacter
        {
            public CharacterRecord record;
            public GameObject player;
            public double lastSaveTime;
        }

        // Le fedi che il sacerdote scrive nel registro (vuoto = nessuna). Gli dei oscuri non si dichiarano.
        static readonly HashSet<string> AllowedFaiths = new HashSet<string> { "", "Solara", "Ignar", "Nereia", "Torvald", "Zefira", "Vecchi Dei" };

        // Chi sta lasciando la valle (attesa in corso) e chi è rimasto nel mondo dopo aver chiuso il gioco.
        class Leaving
        {
            public LeaveMode mode;
            public double endTime;
            public Vector3 startPosition;
            public int lastSecondsSent = -1;
        }
        readonly Dictionary<NetworkConnectionToClient, Leaving> leaving = new Dictionary<NetworkConnectionToClient, Leaving>();
        readonly List<(ActiveCharacter entry, double endTime)> lingering = new List<(ActiveCharacter, double)>();

        readonly Dictionary<NetworkConnectionToClient, ActiveCharacter> active = new Dictionary<NetworkConnectionToClient, ActiveCharacter>();

        /// <summary>
        /// Solo sul server: il personaggio con cui è entrato nel mondo questo collegamento (id e nome).
        /// Serve a chi deve sapere "chi" ha fatto qualcosa (es. accettare un incarico dalla bacheca).
        /// </summary>
        public static bool TryGetCharacter(NetworkConnectionToClient conn, out string id, out string name)
        {
            id = name = null;
            var manager = singleton as ValdorsoNetworkManager;
            if (manager == null || conn == null || !manager.active.TryGetValue(conn, out ActiveCharacter entry) || entry.record == null)
                return false;
            id = entry.record.id;
            name = entry.record.name;
            return true;
        }
        double nextAutosave;

        // Lato PC
        static Action<bool, string, string> pendingCreate;
        static Action<bool, string> pendingDelete;

        /// <summary>L'ultimo elenco dei personaggi arrivato dal server (lo legge il Registro).</summary>
        public static CharacterListResponse? LastCharacterList { get; private set; }
        /// <summary>Arriva un elenco nuovo mentre il Registro è aperto (per esempio dopo una cancellazione).</summary>
        public static event Action<CharacterListResponse> CharacterListUpdated;
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
            if (NetworkServer.active)
            {
                UpdateLeaving();
                UpdateLingering();
            }

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
            NetworkServer.RegisterHandler<DeleteCharacterRequest>(OnDeleteCharacterRequest);
            NetworkServer.RegisterHandler<LeaveWorldRequest>(OnLeaveWorldRequest);
            NetworkServer.RegisterHandler<LeaveWorldCancel>(OnLeaveWorldCancel);
            WorldEventLog.EventRecorded += OnWorldEvent;
        }

        public override void OnStopServer()
        {
            SaveAll("spegnimento del server");
            active.Clear();
            leaving.Clear();
            lingering.Clear();
            WorldEventLog.EventRecorded -= OnWorldEvent;
            NetworkServer.UnregisterHandler<LeaveWorldRequest>();
            NetworkServer.UnregisterHandler<LeaveWorldCancel>();
            NetworkServer.UnregisterHandler<CharacterListRequest>();
            NetworkServer.UnregisterHandler<CreateCharacterRequest>();
            NetworkServer.UnregisterHandler<EnterWorldRequest>();
            NetworkServer.UnregisterHandler<DeleteCharacterRequest>();
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
            {
                CharacterRecord c = characters[i];
                summaries[i] = new CharacterSummary
                {
                    id = c.id,
                    name = c.name,
                    faith = c.faith ?? string.Empty,
                    lastPlayedAt = c.lastPlayedAt ?? string.Empty,
                    appearanceRecipe = c.appearanceRecipe ?? string.Empty,
                    portrait = LoadPortrait(c.id)
                };
            }

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
            string backstoryError = Backstory.Validate(msg.homeland, msg.formerTrade, msg.reason, msg.keepsake, msg.fear,
                msg.traits, msg.story, msg.freeStory);
            if (backstoryError != null)
            {
                RefuseCreation(conn, backstoryError);
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
            created.homeland = msg.homeland;
            created.formerTrade = msg.formerTrade;
            created.reason = msg.reason;
            created.keepsake = msg.keepsake;
            created.fear = msg.fear;
            created.traits = (string[])msg.traits.Clone();
            created.story = Backstory.CleanText(msg.story);
            created.freeStory = Backstory.CleanText(msg.freeStory);
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

        static byte[] LoadPortrait(string characterId)
        {
            try
            {
                string path = PortraitPath(characterId);
                return System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Valdorso] Ritratto {characterId} non leggibile: {e.Message}");
                return null;
            }
        }

        void OnDeleteCharacterRequest(NetworkConnectionToClient conn, DeleteCharacterRequest msg)
        {
            if (!TryGetAccount(conn, out AccountRecord account))
            {
                conn.Disconnect();
                return;
            }
            if (active.ContainsKey(conn))
            {
                conn.Send(new DeleteCharacterResponse { success = false, message = "Non si cancella un nome mentre si è nella valle." });
                return;
            }
            bool owned = account.characterIds != null && Array.IndexOf(account.characterIds, msg.characterId) >= 0;
            if (!owned || !CharacterStore.TryLoad(msg.characterId, out CharacterRecord character))
            {
                conn.Send(new DeleteCharacterResponse { success = false, message = "Questo nome non è nel tuo registro." });
                return;
            }
            // Per cancellare bisogna riscrivere il nome: è per sempre.
            if (!string.Equals(character.name.Trim(), (msg.confirmName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            {
                conn.Send(new DeleteCharacterResponse { success = false, message = "Il nome scritto non corrisponde." });
                return;
            }

            CharacterStore.DeleteForTests(account, character.id); // cancella la scheda, la toglie dall'account e libera il nome
            try
            {
                string portrait = PortraitPath(character.id);
                if (System.IO.File.Exists(portrait)) System.IO.File.Delete(portrait);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Valdorso] Ritratto di {character.name} non cancellato: {e.Message}");
            }
            Debug.Log($"[Valdorso] {account.username} ha cancellato dal registro il personaggio {character.name}.");
            conn.Send(new DeleteCharacterResponse { success = true, message = string.Empty });
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

            // Se questo personaggio è ancora nel mondo dopo un'uscita brusca, prima lo si salva e lo si toglie:
            // rientra esattamente dov'era, con le stesse ferite (niente scappatoie).
            for (int i = lingering.Count - 1; i >= 0; i--)
                if (lingering[i].entry.record.id == msg.characterId) FinishLingering(i);

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

        // ---------- Lasciare la valle ----------

        void OnLeaveWorldRequest(NetworkConnectionToClient conn, LeaveWorldRequest msg)
        {
            if (!active.TryGetValue(conn, out ActiveCharacter entry) || entry.player == null) return;
            SafeZone zone = SafeZone.At(entry.player.transform.position);
            if (zone != null)
            {
                Debug.Log($"[Valdorso] {entry.record.name} lascia la valle da {zone.PlaceName}: uscita immediata.");
                FinishLeaving(conn, entry, msg.mode);
                return;
            }
            leaving[conn] = new Leaving
            {
                mode = msg.mode,
                endTime = Time.unscaledTimeAsDouble + leaveDelay,
                startPosition = entry.player.transform.position
            };
        }

        void OnLeaveWorldCancel(NetworkConnectionToClient conn, LeaveWorldCancel msg)
        {
            CancelLeaving(conn, "Resti nella valle.");
        }

        void CancelLeaving(NetworkConnectionToClient conn, string reason)
        {
            if (!leaving.TryGetValue(conn, out Leaving l)) return;
            leaving.Remove(conn);
            conn.Send(new LeaveWorldStatus { mode = l.mode, cancelled = true, message = reason });
        }

        void UpdateLeaving()
        {
            if (leaving.Count == 0) return;
            double now = Time.unscaledTimeAsDouble;
            foreach (NetworkConnectionToClient conn in new List<NetworkConnectionToClient>(leaving.Keys))
            {
                Leaving l = leaving[conn];
                if (!active.TryGetValue(conn, out ActiveCharacter entry) || entry.player == null)
                {
                    leaving.Remove(conn);
                    continue;
                }
                if ((entry.player.transform.position - l.startPosition).sqrMagnitude > leaveMoveTolerance * leaveMoveTolerance)
                {
                    CancelLeaving(conn, "Ti sei mosso: resti nella valle.");
                    continue;
                }
                if (now >= l.endTime)
                {
                    FinishLeaving(conn, entry, l.mode);
                    continue;
                }
                int seconds = Mathf.CeilToInt((float)(l.endTime - now));
                if (seconds != l.lastSecondsSent)
                {
                    l.lastSecondsSent = seconds;
                    conn.Send(new LeaveWorldStatus { mode = l.mode, secondsLeft = seconds });
                }
            }
        }

        // Chi colpisce o viene colpito durante l'attesa resta nella valle.
        void OnWorldEvent(WorldEvent e)
        {
            if (e.type != WorldEventType.Damage || leaving.Count == 0) return;
            foreach (NetworkConnectionToClient conn in new List<NetworkConnectionToClient>(leaving.Keys))
            {
                if (!active.TryGetValue(conn, out ActiveCharacter entry) || entry.player == null) continue;
                uint id = entry.player.GetComponent<NetworkIdentity>().netId;
                if (e.actorId == id || e.targetId == id) CancelLeaving(conn, "Sei in combattimento: resti nella valle.");
            }
        }

        void FinishLeaving(NetworkConnectionToClient conn, ActiveCharacter entry, LeaveMode mode)
        {
            leaving.Remove(conn);
            SaveCharacter(entry);
            active.Remove(conn);
            Debug.Log($"[Valdorso] {entry.record.name} ha lasciato la valle: personaggio salvato.");
            // Il personaggio sparisce dal mondo; il PC resta collegato (per tornare ai personaggi) o si scollega da solo.
            NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.Destroy);
            conn.Send(new LeaveWorldStatus { mode = mode, done = true });
        }

        void UpdateLingering()
        {
            double now = Time.unscaledTimeAsDouble;
            for (int i = lingering.Count - 1; i >= 0; i--)
                if (now >= lingering[i].endTime || lingering[i].entry.player == null) FinishLingering(i);
        }

        void FinishLingering(int index)
        {
            ActiveCharacter entry = lingering[index].entry;
            lingering.RemoveAt(index);
            SaveCharacter(entry);
            if (entry.player != null) NetworkServer.Destroy(entry.player);
            Debug.Log($"[Valdorso] {entry.record.name} ha lasciato la valle: personaggio salvato.");
        }

        // ---------- Salvataggio ----------

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (active.TryGetValue(conn, out ActiveCharacter entry))
            {
                active.Remove(conn);
                double end = leaving.TryGetValue(conn, out Leaving l) ? l.endTime : Time.unscaledTimeAsDouble + leaveDelay;
                leaving.Remove(conn);

                SafeZone zone = entry.player != null ? SafeZone.At(entry.player.transform.position) : null;
                if (zone != null || entry.player == null || end <= Time.unscaledTimeAsDouble)
                {
                    SaveCharacter(entry);
                    Debug.Log($"[Valdorso] {entry.record.name} è uscito: personaggio salvato.");
                }
                else
                {
                    // Chi chiude il gioco fuori da un luogo sicuro resta nel mondo, fermo e attaccabile, fino alla fine dell'attesa.
                    NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.KeepActive);
                    lingering.Add((entry, end));
                    Debug.Log($"[Valdorso] {entry.record.name} ha lasciato il gioco fuori da un luogo sicuro: resta nella valle per {end - Time.unscaledTimeAsDouble:0} secondi.");
                }
            }
            base.OnServerDisconnect(conn);
        }

        public void SaveAll(string reason)
        {
            if (active.Count == 0 && lingering.Count == 0) return;
            foreach (ActiveCharacter entry in active.Values) SaveCharacter(entry);
            foreach (var l in lingering) SaveCharacter(l.entry);
            Debug.Log($"[Valdorso] Salvati {active.Count + lingering.Count} personaggi ({reason}).");
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
            NetworkClient.RegisterHandler<DeleteCharacterResponse>(OnDeleteResponse);
            NetworkClient.RegisterHandler<LeaveWorldStatus>(OnLeaveStatus);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            listRequested = false;
            pendingCreate = null;
            pendingDelete = null;
            LastCharacterList = null;
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
            // Il Registro si apre sempre: con l'elenco dei personaggi, oppure sulla creazione se non ce ne sono.
            LastCharacterList = msg;
            if (SceneManager.GetSceneByName(creationScene).isLoaded) CharacterListUpdated?.Invoke(msg);
            else OpenCreation();
        }

        void OnDeleteResponse(DeleteCharacterResponse msg)
        {
            Action<bool, string> callback = pendingDelete;
            pendingDelete = null;
            callback?.Invoke(msg.success, msg.message);
            // Dopo una cancellazione riuscita si chiede l'elenco aggiornato.
            if (msg.success) NetworkClient.Send(new CharacterListRequest());
        }

        /// <summary>A che punto è l'uscita dal mondo (lo mostra il menu di pausa).</summary>
        public static event Action<LeaveWorldStatus> LeaveStatusReceived;

        void OnLeaveStatus(LeaveWorldStatus msg)
        {
            // Tornando ai personaggi si resta collegati: si richiede l'elenco e si riapre il Registro.
            if (msg.done && msg.mode == LeaveMode.Characters) listRequested = false;
            LeaveStatusReceived?.Invoke(msg);
        }

        public static void RequestLeaveWorld(LeaveMode mode)
        {
            if (NetworkClient.isConnected) NetworkClient.Send(new LeaveWorldRequest { mode = mode });
        }

        public static void CancelLeaveWorld()
        {
            if (NetworkClient.isConnected) NetworkClient.Send(new LeaveWorldCancel());
        }

        /// <summary>Chiede al server di cancellare un personaggio; bisogna riscriverne il nome. Risposta nel callback (riuscito, messaggio).</summary>
        public static void RequestDeleteCharacter(string characterId, string confirmName, Action<bool, string> callback)
        {
            if (!NetworkClient.isConnected)
            {
                callback?.Invoke(false, "Non sei collegato al server.");
                return;
            }
            pendingDelete = callback;
            NetworkClient.Send(new DeleteCharacterRequest { characterId = characterId, confirmName = confirmName });
        }

        void OpenCreation()
        {
            if (SceneManager.GetSceneByName(creationScene).isLoaded) return;
            Debug.Log("[Valdorso] Si apre il Registro di Val d'Orso.");
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
        public static void RequestCreateCharacter(CreateCharacterRequest request, Action<bool, string, string> callback)
        {
            if (!NetworkClient.isConnected)
            {
                callback?.Invoke(false, "Non sei collegato al server.", null);
                return;
            }
            pendingCreate = callback;
            NetworkClient.Send(request);
        }

        /// <summary>Chiede al server di entrare nel mondo con questo personaggio.</summary>
        public static void RequestEnterWorld(string characterId)
        {
            if (NetworkClient.isConnected) NetworkClient.Send(new EnterWorldRequest { characterId = characterId });
        }
    }
}