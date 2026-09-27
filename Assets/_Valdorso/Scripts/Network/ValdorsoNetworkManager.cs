using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valdorso.Creatures;
using Valdorso.Server;
using Valdorso.Stats;

namespace Valdorso.Network
{
    /// <summary>
    /// Il NetworkManager di Valdorso, il "maestro di cerimonie" del server.
    /// Quando un giocatore entra, carica il suo personaggio (o ne crea uno provvisorio)
    /// e lo fa comparire dove l'aveva lasciato, con salute, stamina e mana salvati.
    /// Salva i personaggi quando escono, ogni autosaveInterval secondi e quando il server si spegne.
    /// </summary>
    public class ValdorsoNetworkManager : NetworkManager
    {
        [Header("Valdorso")]
        [Tooltip("Ogni quanti secondi il server salva tutti i personaggi presenti")]
        [SerializeField] float autosaveInterval = 60f;
        [Tooltip("Salute (frazione del massimo) di chi era morto quando è uscito; provvisorio fino alla rinascita al villaggio")]
        [SerializeField] float revivedHealthFraction = 0.25f;

        class ActiveCharacter
        {
            public CharacterRecord record;
            public GameObject player;
            public double lastSaveTime;
        }

        readonly Dictionary<NetworkConnectionToClient, ActiveCharacter> active = new Dictionary<NetworkConnectionToClient, ActiveCharacter>();
        double nextAutosave;

        public override void OnStartServer()
        {
            base.OnStartServer();
            nextAutosave = Time.unscaledTimeAsDouble + autosaveInterval;
        }

        public override void Update()
        {
            base.Update();
            if (!NetworkServer.active || Time.unscaledTimeAsDouble < nextAutosave) return;
            nextAutosave = Time.unscaledTimeAsDouble + autosaveInterval;
            SaveAll("salvataggio automatico");
        }

        // ---------- Entrata ----------

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (!(conn.authenticationData is AccountSession session) || !AccountStore.TryLoad(session.username, out AccountRecord account))
            {
                Debug.LogWarning($"[Valdorso] Connessione {conn.connectionId} senza un account valido: niente personaggio.");
                conn.Disconnect();
                return;
            }

            CharacterRecord character = FindOrCreateCharacter(account);
            if (character == null)
            {
                conn.Disconnect();
                return;
            }

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

        /// <summary>Fino al passo 6 (scelta del personaggio) si usa il primo; se non ce ne sono, uno provvisorio.</summary>
        static CharacterRecord FindOrCreateCharacter(AccountRecord account)
        {
            List<CharacterRecord> characters = CharacterStore.LoadAll(account);
            if (characters.Count > 0) return characters[0];

            foreach (string candidate in ProvisionalNames(account.username))
            {
                if (CharacterStore.TryCreate(account, candidate, out CharacterRecord created, out _))
                {
                    Debug.Log($"[Valdorso] Creato il personaggio provvisorio {created.name} per {account.username}.");
                    return created;
                }
            }
            Debug.LogError($"[Valdorso] Impossibile creare un personaggio provvisorio per {account.username}.");
            return null;
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

        public override void OnStopServer()
        {
            SaveAll("spegnimento del server");
            active.Clear();
            base.OnStopServer();
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
    }
}
