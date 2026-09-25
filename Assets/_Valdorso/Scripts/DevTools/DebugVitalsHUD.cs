using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Valdorso.Creatures;
using Valdorso.Stats;

namespace Valdorso.DevTools
{
    /// <summary>
    /// Strumento di sviluppo: mostra i valori vitali e permette di testarli da tastiera.
    /// K = danno, H = cura, J = consuma stamina, L = rianima. Da togliere prima dell'uscita pubblica.
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class DebugVitalsHUD : NetworkBehaviour
    {
        Creature creature;

        void Awake()
        {
            creature = GetComponent<Creature>();
        }

        void Update()
        {
            if (!isLocalPlayer) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.kKey.wasPressedThisFrame) CmdDamageSelf(15f);
            if (kb.hKey.wasPressedThisFrame) CmdHealSelf(25f);
            if (kb.jKey.wasPressedThisFrame) CmdSpendStamina(30f);
            if (kb.lKey.wasPressedThisFrame) CmdRevive();
        }

        [Command]
        void CmdDamageSelf(float amount)
        {
            creature.ReceiveDamage(new DamageInfo { amount = amount, type = DamageType.Physical, source = "test" });
        }

        [Command]
        void CmdHealSelf(float amount)
        {
            if (!creature.IsDead) creature.Stats.Heal(amount);
        }

        [Command]
        void CmdSpendStamina(float amount)
        {
            creature.Stats.TrySpend(VitalType.Stamina, amount);
        }

        [Command]
        void CmdRevive()
        {
            creature.Revive();
        }

        void OnGUI()
        {
            if (!isLocalPlayer) return;
            CreatureStats s = creature.Stats;

            GUILayout.BeginArea(new Rect(Screen.width - 280, 10, 270, 170), GUI.skin.box);
            GUILayout.Label($"{creature.DisplayName}   (fazione: {creature.FactionId})");
            GUILayout.Label($"Salute:   {s.Health:0} / {s.MaxHealth:0}");
            GUILayout.Label($"Stamina:  {s.Stamina:0} / {s.MaxStamina:0}");
            GUILayout.Label($"Mana:     {s.Mana:0} / {s.MaxMana:0}");
            if (creature.IsDead) GUILayout.Label("SEI MORTO: premi L per rianimarti");
            GUILayout.Label("K danno   H cura   J stamina   L rianima");
            GUILayout.EndArea();
        }
    }
}
