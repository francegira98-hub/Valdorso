using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Valdorso.Creatures;

namespace Valdorso.WorldEvents
{
    /// <summary>
    /// Registro di tutto ciò che accade nel mondo. Vive solo sul server.
    /// Va messo una sola volta nella scena, su un oggetto vuoto (es. "Mondo").
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldEventLog : MonoBehaviour
    {
        public static WorldEventLog Instance { get; private set; }

        /// <summary>Scatta a ogni nuovo evento. Qui si agganceranno legge, dicerie e quest segrete.</summary>
        public static event Action<WorldEvent> EventRecorded;

        [Tooltip("Entro questa distanza (metri) le creature vive sono considerate testimoni")]
        [SerializeField] float witnessRadius = 20f;
        [SerializeField] int maxStoredEvents = 1000;
        [SerializeField] bool logToConsole = true;

        readonly List<WorldEvent> events = new List<WorldEvent>();
        long nextId = 1;
        static bool warnedMissing;

        public IReadOnlyList<WorldEvent> Events => events;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Valdorso] C'è più di un WorldEventLog nella scena: tengo solo il primo.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Registra un evento. Funziona solo sul server; altrove viene ignorato.</summary>
        public static WorldEvent Record(WorldEventType type, Creature actor, Creature target, string data)
        {
            if (!NetworkServer.active) return null;
            if (Instance == null)
            {
                if (!warnedMissing)
                {
                    Debug.LogWarning("[Valdorso] Nessun WorldEventLog nella scena: gli eventi non vengono registrati.");
                    warnedMissing = true;
                }
                return null;
            }
            return Instance.Add(type, actor, target, data);
        }

        WorldEvent Add(WorldEventType type, Creature actor, Creature target, string data)
        {
            Vector3 position = target != null ? target.transform.position
                : actor != null ? actor.transform.position
                : Vector3.zero;

            var e = new WorldEvent
            {
                id = nextId++,
                type = type,
                time = NetworkTime.time,
                actorId = actor != null ? actor.netId : 0,
                actorName = actor != null ? actor.DisplayName : string.Empty,
                actorFaction = actor != null ? actor.FactionId : string.Empty,
                targetId = target != null ? target.netId : 0,
                targetName = target != null ? target.DisplayName : string.Empty,
                targetFaction = target != null ? target.FactionId : string.Empty,
                position = position,
                data = data
            };

            // Testimoni: per ora basta la distanza. La linea di vista arriverà con la patch "Legge e ombra".
            float radiusSqr = witnessRadius * witnessRadius;
            foreach (Creature c in Creature.All)
            {
                if (c == null || c == actor || c == target || c.IsDead) continue;
                if ((c.transform.position - position).sqrMagnitude <= radiusSqr) e.witnessIds.Add(c.netId);
            }

            events.Add(e);
            if (events.Count > maxStoredEvents) events.RemoveAt(0);

            if (logToConsole) Debug.Log($"[Evento] {e}");
            EventRecorded?.Invoke(e);
            return e;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            EventRecorded = null;
            warnedMissing = false;
        }
    }
}
