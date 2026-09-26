using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Valdorso.Stats;
using Valdorso.WorldEvents;

namespace Valdorso.Creatures
{
    /// <summary>
    /// Il cuore di ogni essere vivente (o non morto) del mondo: giocatori, abitanti, mob, evocazioni.
    /// Gestisce identità, fazione, danni e morte. Ogni evento importante finisce nel registro eventi.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreatureStats))]
    public class Creature : NetworkBehaviour
    {
        static readonly List<Creature> all = new List<Creature>();
        static int playerCounter;
        /// <summary>Tutte le creature attive sul server.</summary>
        public static IReadOnlyList<Creature> All => all;

        [SerializeField] string defaultName = "Creatura";
        [SerializeField] FactionDefinition faction;

        [SyncVar] string displayName;
        [SyncVar(hook = nameof(OnDeadSync))] bool isDead;

        // NUOVO: fino a quando (sul server) la creatura non subisce danni, per esempio durante una schivata.
        double invulnerableUntil;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? defaultName : displayName;
        public FactionDefinition Faction => faction;
        public string FactionId => faction != null ? faction.Id : "neutrale";
        public bool IsDead => isDead;
        public CreatureStats Stats { get; private set; }

        // NUOVO: vero mentre la creatura è intoccabile.
        public bool IsInvulnerable => Time.timeAsDouble < invulnerableUntil;

        /// <summary>Scatta alla morte, sul server e su tutti i client.</summary>
        public event Action Died;
        /// <summary>Scatta alla rianimazione, sul server e su tutti i client.</summary>
        public event Action Revived;
        /// <summary>Scatta sui client quando la creatura subisce un colpo (danno effettivo).</summary>
        public event Action<float> Damaged;

        void Awake()
        {
            Stats = GetComponent<CreatureStats>();
        }

        public override void OnStartServer()
        {
            all.Add(this);
            if (string.IsNullOrEmpty(displayName))
                displayName = connectionToClient != null ? $"Giocatore {++playerCounter}" : defaultName;
        }

        public override void OnStopServer()
        {
            all.Remove(this);
        }

        [Server]
        public void SetDisplayName(string newName)
        {
            if (!string.IsNullOrWhiteSpace(newName)) displayName = newName.Trim();
        }

        /// <summary>NUOVO: per qualche istante la creatura non subisce danni (schivata, benedizioni...).</summary>
        [Server]
        public void SetInvulnerable(float seconds)
        {
            double until = Time.timeAsDouble + seconds;
            if (until > invulnerableUntil) invulnerableUntil = until;
        }

        /// <summary>Applica un colpo. Restituisce il danno effettivo.</summary>
        [Server]
        public float ReceiveDamage(DamageInfo info)
        {
            if (isDead || IsInvulnerable) return 0f;

            float dealt = Stats.ApplyDamage(info.amount);
            if (dealt <= 0f) return 0f;

            Creature attacker = FromNetId(info.attackerNetId);
            WorldEventLog.Record(WorldEventType.Damage, attacker, this, $"{dealt:0.#} danno {info.type} ({info.source})");

            if (Stats.IsDepleted) Die(info, attacker);
            else RpcDamaged(dealt);
            return dealt;
        }

        [ClientRpc]
        void RpcDamaged(float dealt)
        {
            Damaged?.Invoke(dealt);
        }

        [Server]
        void Die(DamageInfo info, Creature killer)
        {
            isDead = true;
            WorldEventLog.Record(WorldEventType.Kill, killer, this, info.source);
            Died?.Invoke();
        }

        [Server]
        public void Revive()
        {
            if (!isDead) return;
            isDead = false;
            Stats.RestoreFull();
            WorldEventLog.Record(WorldEventType.Revive, null, this, string.Empty);
            Revived?.Invoke();
        }

        void OnDeadSync(bool wasDead, bool nowDead)
        {
            // Sul server gli eventi sono già scattati in Die() e Revive(); qui avvisiamo i client puri.
            if (isServer) return;
            if (nowDead && !wasDead) Died?.Invoke();
            else if (!nowDead && wasDead) Revived?.Invoke();
        }

        public static Creature FromNetId(uint id)
        {
            if (id == 0) return null;
            return NetworkServer.spawned.TryGetValue(id, out NetworkIdentity identity) && identity != null
                ? identity.GetComponent<Creature>()
                : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            all.Clear();
            playerCounter = 0;
        }
    }
}