using Mirror;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using Valdorso.Creatures;
using Valdorso.Stats;

namespace Valdorso.Combat
{
    /// <summary>
    /// Schivata: una capriola rapida nella direzione del movimento.
    /// Il movimento lo fa subito il PC di chi gioca, così è reattivo;
    /// il server scala la stamina, rende il personaggio intoccabile per un attimo
    /// e fa vedere l'animazione agli altri giocatori.
    /// Durante la capriola non si può attaccare (controllato anche dal server).
    /// </summary>
    [RequireComponent(typeof(Creature))]
    [RequireComponent(typeof(CharacterController))]
    public class DodgeRoll : NetworkBehaviour
    {
        [Header("Comando")]
        [SerializeField] Key dodgeKey = Key.C;

        [Header("Movimento")]
        [Tooltip("Metri percorsi con la capriola")]
        [SerializeField] float distance = 4f;
        [Tooltip("Secondi che dura la capriola")]
        [SerializeField] float duration = 0.9f;

        [Header("Costi e difesa")]
        [SerializeField] float staminaCost = 20f;
        [Tooltip("Secondi minimi tra una schivata e l'altra")]
        [SerializeField] float cooldown = 1.1f;
        [Tooltip("Secondi in cui il personaggio non subisce danni, dall'inizio della schivata")]
        [SerializeField] float invulnerableTime = 0.5f;

        Creature creature;
        CharacterController controller;
        CreatureAnimator creatureAnimator;
        StarterAssetsInputs input;
        Transform cameraTransform;

        double localNextDodge;
        double serverNextDodge;
        double serverDodgeEnd;
        float dodgeTimer;
        Vector3 dodgeDirection;

        /// <summary>Vero sul PC di chi gioca mentre la capriola è in corso.</summary>
        public bool IsDodging => dodgeTimer > 0f;

        /// <summary>Vero sul server mentre la capriola è in corso.</summary>
        public bool IsDodgingOnServer => Time.timeAsDouble < serverDodgeEnd;

        void Awake()
        {
            creature = GetComponent<Creature>();
            controller = GetComponent<CharacterController>();
            creatureAnimator = GetComponent<CreatureAnimator>();
            input = GetComponent<StarterAssetsInputs>();
        }

        void Update()
        {
            if (!isLocalPlayer) return;
            if (IsDodging) { ContinueDodge(); return; }
            if (creature.IsDead) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[dodgeKey].wasPressedThisFrame) return;
            if (Time.timeAsDouble < localNextDodge) return;
            if (creature.Stats.Stamina < staminaCost)
            {
                // In futuro: suono di fatica e barra lampeggiante.
                Debug.Log("[Valdorso] Stamina insufficiente per schivare.");
                return;
            }

            StartDodge();
        }

        void StartDodge()
        {
            localNextDodge = Time.timeAsDouble + cooldown;
            dodgeDirection = ChooseDirection();
            transform.rotation = Quaternion.LookRotation(dodgeDirection);
            dodgeTimer = duration;

            if (creatureAnimator != null) creatureAnimator.PlayDodge();
            CmdDodge();
        }

        /// <summary>Direzione in cui si sta andando, rispetto alla telecamera; se fermi, in avanti.</summary>
        Vector3 ChooseDirection()
        {
            Vector2 move = input != null ? input.move : Vector2.zero;
            if (move.sqrMagnitude < 0.01f) return transform.forward;

            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
            return (Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.y)).normalized;
        }

        void ContinueDodge()
        {
            // Veloce all'inizio, più lenta alla fine: in totale percorre "distance" metri.
            float progress = 1f - dodgeTimer / duration;
            float speed = distance / duration * 2f * (1f - progress);
            float step = Mathf.Min(Time.deltaTime, dodgeTimer);
            controller.Move(dodgeDirection * speed * step);

            dodgeTimer -= Time.deltaTime;
            if (creature.IsDead) dodgeTimer = 0f;
        }

        [Command]
        void CmdDodge()
        {
            double now = Time.timeAsDouble;
            if (creature.IsDead || now < serverNextDodge - 0.05) return;
            if (!creature.Stats.TrySpend(VitalType.Stamina, staminaCost)) return;

            serverNextDodge = now + cooldown;
            serverDodgeEnd = now + duration;
            creature.SetInvulnerable(invulnerableTime);
            RpcPlayDodge();
        }

        [ClientRpc(includeOwner = false)]
        void RpcPlayDodge()
        {
            // Chi schiva ha già fatto partire l'animazione da solo; qui la vedono gli altri.
            if (creatureAnimator != null) creatureAnimator.PlayDodge();
        }
    }
}