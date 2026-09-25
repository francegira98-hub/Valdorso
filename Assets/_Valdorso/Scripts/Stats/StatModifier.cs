using System;
using UnityEngine;

namespace Valdorso.Stats
{
    public enum ModifierKind
    {
        Flat,    // +20 salute massima
        Percent  // +10% salute massima
    }

    /// <summary>
    /// Un bonus o malus ai valori massimi (equipaggiamento, pozioni, maledizioni, talenti).
    /// Vive solo sul server.
    /// </summary>
    [Serializable]
    public class StatModifier
    {
        public VitalType target;
        public ModifierKind kind;
        public float value;
        [Tooltip("Chi ha applicato il modificatore, es. 'Anello del lupo'. Serve per toglierlo.")]
        public string source;
        [Tooltip("Momento di scadenza (tempo del server). 0 = permanente.")]
        public double expiresAt;

        public StatModifier(VitalType target, ModifierKind kind, float value, string source, float durationSeconds = 0f)
        {
            this.target = target;
            this.kind = kind;
            this.value = value;
            this.source = source;
            expiresAt = durationSeconds > 0f ? Time.timeAsDouble + durationSeconds : 0d;
        }

        public bool IsExpired(double now) => expiresAt > 0d && now >= expiresAt;
    }
}
