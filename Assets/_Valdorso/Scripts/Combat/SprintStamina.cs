using Mirror;
using StarterAssets;
using UnityEngine;
using Valdorso.Creatures;
using Valdorso.Stats;

namespace Valdorso.Combat
{
    /// <summary>
    /// La corsa consuma stamina. Il giocatore avvisa il server quando inizia e smette di correre;
    /// il server scala la stamina. A stamina esaurita la corsa si blocca finché non si recupera.
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class SprintStamina : NetworkBehaviour
    {
        [SerializeField] float drainPerSecond = 12f;
        [Tooltip("Stamina minima per poter ricominciare a correre dopo essersi sfiniti")]
        [SerializeField] float staminaToRecover = 25f;
        [SerializeField] StarterAssetsInputs input;

        Creature creature;
        bool lastSentSprinting;
        bool exhausted;
        bool serverSprinting;

        void Awake()
        {
            creature = GetComponent<Creature>();
            if (input == null) input = GetComponent<StarterAssetsInputs>();
        }

        void Update()
        {
            if (isLocalPlayer && input != null) UpdateLocal();
            if (isServer && serverSprinting) UpdateServer();
        }

        void UpdateLocal()
        {
            float stamina = creature.Stats.Stamina;
            if (stamina <= 0.5f) exhausted = true;
            else if (exhausted && stamina >= staminaToRecover) exhausted = false;

            if (exhausted) input.sprint = false; // sfinito: per ripartire bisogna ripremere Shift

            bool sprinting = input.sprint && input.move.sqrMagnitude > 0.01f && !creature.IsDead;
            if (sprinting != lastSentSprinting)
            {
                lastSentSprinting = sprinting;
                CmdSetSprinting(sprinting);
            }
        }

        void UpdateServer()
        {
            if (creature.IsDead || !creature.Stats.TrySpend(VitalType.Stamina, drainPerSecond * Time.deltaTime))
                serverSprinting = false;
        }

        [Command]
        void CmdSetSprinting(bool sprinting)
        {
            serverSprinting = sprinting;
        }
    }
}
