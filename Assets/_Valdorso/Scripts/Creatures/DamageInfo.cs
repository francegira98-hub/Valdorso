namespace Valdorso.Creatures
{
    /// <summary>Tipi di danno: fisico, i cinque elementi, ombra (poteri oscuri) e puro (ignora le resistenze).</summary>
    public enum DamageType
    {
        Physical,
        Fire,
        Water,
        Earth,
        Air,
        Light,
        Shadow,
        Pure
    }

    /// <summary>Descrive un colpo: quanto, di che tipo, chi l'ha inferto e con cosa.</summary>
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public uint attackerNetId; // 0 = nessun attaccante (trappola, caduta, test)
        public string source;      // es. "Spada lunga", "Dardo infuocato"

        public static DamageInfo Create(float amount, DamageType type, Creature attacker, string source)
        {
            return new DamageInfo
            {
                amount = amount,
                type = type,
                attackerNetId = attacker != null ? attacker.netId : 0,
                source = source
            };
        }
    }
}
