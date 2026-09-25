using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Valdorso.Stats
{
    /// <summary>
    /// Salute, stamina e mana di una creatura (giocatore, abitante, mob o evocazione).
    /// Il server calcola e modifica i valori; i client li ricevono in automatico.
    /// </summary>
    [DisallowMultipleComponent]
    public class CreatureStats : NetworkBehaviour
    {
        [Header("Valori massimi di base")]
        [SerializeField] float baseMaxHealth = 100f;
        [SerializeField] float baseMaxStamina = 100f;
        [SerializeField] float baseMaxMana = 50f;

        [Header("Rigenerazione al secondo")]
        [SerializeField] float healthRegen = 0.5f;
        [SerializeField] float staminaRegen = 15f;
        [SerializeField] float manaRegen = 1f;
        [Tooltip("Secondi di attesa dopo aver usato stamina prima che torni a rigenerarsi")]
        [SerializeField] float staminaRegenDelay = 1.2f;

        [SyncVar(hook = nameof(OnHealthSync))] float health;
        [SyncVar(hook = nameof(OnStaminaSync))] float stamina;
        [SyncVar(hook = nameof(OnManaSync))] float mana;
        [SyncVar] float maxHealth;
        [SyncVar] float maxStamina;
        [SyncVar] float maxMana;

        public float Health => health;
        public float Stamina => stamina;
        public float Mana => mana;
        public float MaxHealth => maxHealth;
        public float MaxStamina => maxStamina;
        public float MaxMana => maxMana;
        public bool IsDepleted => health <= 0f;

        /// <summary>Avvisa i client quando un valore cambia: tipo, valore attuale, massimo.</summary>
        public event Action<VitalType, float, float> VitalChanged;

        readonly List<StatModifier> modifiers = new List<StatModifier>();
        double lastStaminaUse = double.NegativeInfinity;

        public override void OnStartServer()
        {
            RecalculateMaximums();
            health = maxHealth;
            stamina = maxStamina;
            mana = maxMana;
        }

        [ServerCallback]
        void Update()
        {
            if (IsDepleted) return;

            double now = Time.timeAsDouble;
            if (modifiers.RemoveAll(m => m.IsExpired(now)) > 0) RecalculateMaximums();

            float dt = Time.deltaTime;
            if (health < maxHealth) health = Mathf.Min(maxHealth, health + healthRegen * dt);
            if (stamina < maxStamina && now - lastStaminaUse >= staminaRegenDelay)
                stamina = Mathf.Min(maxStamina, stamina + staminaRegen * dt);
            if (mana < maxMana) mana = Mathf.Min(maxMana, mana + manaRegen * dt);
        }

        public float Get(VitalType type) => type switch
        {
            VitalType.Health => health,
            VitalType.Stamina => stamina,
            _ => mana
        };

        public float GetMax(VitalType type) => type switch
        {
            VitalType.Health => maxHealth,
            VitalType.Stamina => maxStamina,
            _ => maxMana
        };

        /// <summary>Toglie salute. Restituisce il danno effettivo.</summary>
        [Server]
        public float ApplyDamage(float amount)
        {
            if (amount <= 0f || IsDepleted) return 0f;
            float dealt = Mathf.Min(health, amount);
            health -= dealt;
            return dealt;
        }

        /// <summary>Ridà salute. Restituisce la cura effettiva.</summary>
        [Server]
        public float Heal(float amount)
        {
            if (amount <= 0f || IsDepleted) return 0f;
            float healed = Mathf.Min(maxHealth - health, amount);
            health += healed;
            return healed;
        }

        /// <summary>
        /// Consuma un valore se ce n'è abbastanza (stamina per correre e colpire, mana per gli incantesimi,
        /// salute per la magia del sangue). Restituisce false se non basta.
        /// </summary>
        [Server]
        public bool TrySpend(VitalType type, float amount)
        {
            if (amount <= 0f) return true;
            switch (type)
            {
                case VitalType.Health:
                    if (health <= amount) return false; // la magia del sangue non può uccidere chi la usa
                    health -= amount;
                    return true;
                case VitalType.Stamina:
                    if (stamina < amount) return false;
                    stamina -= amount;
                    lastStaminaUse = Time.timeAsDouble;
                    return true;
                case VitalType.Mana:
                    if (mana < amount) return false;
                    mana -= amount;
                    return true;
            }
            return false;
        }

        [Server]
        public void RestoreFull()
        {
            health = maxHealth;
            stamina = maxStamina;
            mana = maxMana;
        }

        [Server]
        public void AddModifier(StatModifier modifier)
        {
            if (modifier == null) return;
            modifiers.Add(modifier);
            RecalculateMaximums();
        }

        [Server]
        public int RemoveModifiersFromSource(string source)
        {
            int removed = modifiers.RemoveAll(m => m.source == source);
            if (removed > 0) RecalculateMaximums();
            return removed;
        }

        [Server]
        void RecalculateMaximums()
        {
            maxHealth = Compute(VitalType.Health, baseMaxHealth);
            maxStamina = Compute(VitalType.Stamina, baseMaxStamina);
            maxMana = Compute(VitalType.Mana, baseMaxMana);
            health = Mathf.Min(health, maxHealth);
            stamina = Mathf.Min(stamina, maxStamina);
            mana = Mathf.Min(mana, maxMana);
        }

        float Compute(VitalType type, float baseValue)
        {
            float flat = 0f, percent = 0f;
            foreach (var m in modifiers)
            {
                if (m.target != type) continue;
                if (m.kind == ModifierKind.Flat) flat += m.value;
                else percent += m.value;
            }
            return Mathf.Max(1f, (baseValue + flat) * (1f + percent / 100f));
        }

        void OnHealthSync(float oldValue, float newValue) => VitalChanged?.Invoke(VitalType.Health, newValue, maxHealth);
        void OnStaminaSync(float oldValue, float newValue) => VitalChanged?.Invoke(VitalType.Stamina, newValue, maxStamina);
        void OnManaSync(float oldValue, float newValue) => VitalChanged?.Invoke(VitalType.Mana, newValue, maxMana);
    }
}
