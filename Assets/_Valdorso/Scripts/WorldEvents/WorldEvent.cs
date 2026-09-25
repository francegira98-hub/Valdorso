using System;
using System.Collections.Generic;
using UnityEngine;

namespace Valdorso.WorldEvents
{
    /// <summary>Tipi di evento. Le patch future ne aggiungeranno altri (furto, crimine, quest, rituale...).</summary>
    public enum WorldEventType
    {
        Damage,
        Kill,
        Revive,
        Custom
    }

    /// <summary>
    /// Un fatto accaduto nel mondo: chi, cosa, a chi, dove, quando e chi l'ha visto.
    /// È la base di crimini, taglie, dicerie e quest segrete.
    /// </summary>
    [Serializable]
    public class WorldEvent
    {
        public long id;
        public WorldEventType type;
        public double time;
        public uint actorId;
        public string actorName;
        public string actorFaction;
        public uint targetId;
        public string targetName;
        public string targetFaction;
        public Vector3 position;
        public string data;
        public List<uint> witnessIds = new List<uint>();

        public override string ToString()
        {
            string actor = actorId != 0 ? $"{actorName} [{actorFaction}]" : "nessuno";
            string target = targetId != 0 ? $"{targetName} [{targetFaction}]" : "nessuno";
            return $"#{id} {type} | autore: {actor} | bersaglio: {target} | {data} | testimoni: {witnessIds.Count}";
        }
    }
}
