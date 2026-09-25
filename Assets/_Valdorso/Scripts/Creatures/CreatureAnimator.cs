using UnityEngine;

namespace Valdorso.Creatures
{
    /// <summary>
    /// Traduce ciò che accade alla creatura (colpi, morte, attacchi) in animazioni.
    /// Funziona su ogni PC: ognuno anima localmente ciò che il server gli comunica.
    /// L'Animator deve avere i parametri: Attack (Trigger), Hit (Trigger), Dead (Bool).
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class CreatureAnimator : MonoBehaviour
    {
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int DeadHash = Animator.StringToHash("Dead");

        [SerializeField] Animator animator;

        Creature creature;

        void Awake()
        {
            creature = GetComponent<Creature>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        void OnEnable()
        {
            creature.Damaged += OnDamaged;
            creature.Died += OnDied;
            creature.Revived += OnRevived;
        }

        void OnDisable()
        {
            creature.Damaged -= OnDamaged;
            creature.Died -= OnDied;
            creature.Revived -= OnRevived;
        }

        void Start()
        {
            // Chi entra in partita dopo deve vedere subito i caduti a terra.
            if (animator != null) animator.SetBool(DeadHash, creature.IsDead);
        }

        public void PlayAttack()
        {
            if (animator != null && !creature.IsDead) animator.SetTrigger(AttackHash);
        }

        void OnDamaged(float amount)
        {
            if (animator != null && !creature.IsDead) animator.SetTrigger(HitHash);
        }

        void OnDied()
        {
            if (animator == null) return;
            animator.ResetTrigger(AttackHash);
            animator.ResetTrigger(HitHash);
            animator.SetBool(DeadHash, true);
        }

        void OnRevived()
        {
            if (animator != null) animator.SetBool(DeadHash, false);
        }
    }
}
