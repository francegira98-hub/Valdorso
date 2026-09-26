using UnityEngine;

namespace Valdorso.Creatures
{
    /// <summary>
    /// Traduce ciò che accade alla creatura (colpi, morte, attacchi, schivate, ostacoli) in animazioni.
    /// Il livello "Combattimento" viene acceso solo mentre serve, così a riposo
    /// restano intatte le animazioni di movimento del livello base.
    /// L'Animator deve avere i parametri: Attack, Hit, Dodge, Vault, Climb (Trigger), Dead (Bool).
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class CreatureAnimator : MonoBehaviour
    {
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int DodgeHash = Animator.StringToHash("Dodge");
        static readonly int VaultHash = Animator.StringToHash("Vault");
        static readonly int ClimbHash = Animator.StringToHash("Climb");
        static readonly int DeadHash = Animator.StringToHash("Dead");

        [SerializeField] Animator animator;
        [SerializeField] string combatLayerName = "Combattimento";
        [SerializeField] string idleStateName = "Nessuna";
        [Tooltip("Velocità con cui il livello di combattimento si accende e si spegne")]
        [SerializeField] float layerBlendSpeed = 12f;

        Creature creature;
        int combatLayer = -1;

        void Awake()
        {
            creature = GetComponent<Creature>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) combatLayer = animator.GetLayerIndex(combatLayerName);
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
            if (animator == null) return;
            animator.SetBool(DeadHash, creature.IsDead);
            if (combatLayer >= 0) animator.SetLayerWeight(combatLayer, creature.IsDead ? 1f : 0f);
        }

        void Update()
        {
            if (animator == null || combatLayer < 0) return;

            bool idle = animator.GetCurrentAnimatorStateInfo(combatLayer).IsName(idleStateName)
                        && !animator.IsInTransition(combatLayer);
            bool active = !idle || creature.IsDead;

            float current = animator.GetLayerWeight(combatLayer);
            float target = active ? 1f : 0f;
            animator.SetLayerWeight(combatLayer, Mathf.MoveTowards(current, target, layerBlendSpeed * Time.deltaTime));
        }

        /// <summary>
        /// Usa un nuovo Animator (per esempio quello ricreato da UMA).
        /// </summary>
        public void SetAnimator(Animator newAnimator)
        {
            animator = newAnimator;
            combatLayer = animator != null ? animator.GetLayerIndex(combatLayerName) : -1;
            if (animator != null && creature != null) animator.SetBool(DeadHash, creature.IsDead);
        }

        public void PlayAttack() => Trigger(AttackHash);
        public void PlayDodge() => Trigger(DodgeHash);
        public void PlayVault() => Trigger(VaultHash);
        public void PlayClimb() => Trigger(ClimbHash);

        void Trigger(int hash)
        {
            if (animator != null && !creature.IsDead) animator.SetTrigger(hash);
        }

        void OnDamaged(float amount) => Trigger(HitHash);

        void OnDied()
        {
            if (animator == null) return;
            animator.ResetTrigger(AttackHash);
            animator.ResetTrigger(HitHash);
            animator.ResetTrigger(DodgeHash);
            animator.ResetTrigger(VaultHash);
            animator.ResetTrigger(ClimbHash);
            animator.SetBool(DeadHash, true);
        }

        void OnRevived()
        {
            if (animator != null) animator.SetBool(DeadHash, false);
        }
    }
}