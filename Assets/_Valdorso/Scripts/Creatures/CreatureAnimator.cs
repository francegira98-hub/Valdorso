using UMA.CharacterSystem;
using UnityEngine;

namespace Valdorso.Creatures
{
    /// <summary>
    /// Traduce ciò che accade alla creatura (colpi, morte, attacchi, schivate, ostacoli) in animazioni.
    /// Usa due livelli dell'Animator, accesi solo mentre servono:
    /// - "Parte superiore" (con Avatar Mask): Attacco e Colpito, così le gambe continuano a camminare;
    /// - "Combattimento" (corpo intero): Schivata, Scavalca, Sale, Arrampica e Morte.
    /// Combattimento sta sopra Parte superiore nella lista dei livelli, quindi quando è acceso vince lui.
    /// A riposo entrambi restano spenti e si vede solo il livello base.
    /// L'Animator deve avere i parametri: Attack, Hit, Dodge, Vault, Climb, ClimbHigh (Trigger), Dead (Bool).
    /// ClimbHigh (arrampicata alta) lo aggiunge il menu Valdorso → Animazioni → Aggiungi le animazioni degli ostacoli.
    /// Per sedersi e sdraiarsi (Postura) servono anche Seduto e Sdraiato (Bool) nel livello Combattimento:
    /// se mancano, il personaggio si mette al posto ma resta in piedi, senza errori.
    /// Femminile (Bool) sceglie la posa da seduti: vero per i corpi femminili di UMA.
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class CreatureAnimator : MonoBehaviour
    {
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int DodgeHash = Animator.StringToHash("Dodge");
        static readonly int VaultHash = Animator.StringToHash("Vault");
        static readonly int ClimbHash = Animator.StringToHash("Climb");
        static readonly int ClimbHighHash = Animator.StringToHash("ClimbHigh");
        static readonly int DeadHash = Animator.StringToHash("Dead");
        static readonly int SedutoHash = Animator.StringToHash("Seduto");
        static readonly int SdraiatoHash = Animator.StringToHash("Sdraiato");
        static readonly int FemminileHash = Animator.StringToHash("Femminile");

        bool seduto, sdraiato;

        [SerializeField] Animator animator;
        [Tooltip("Livello a corpo intero: schivata, scavalcata, salita, morte")]
        [SerializeField] string combatLayerName = "Combattimento";
        [Tooltip("Livello con la maschera della parte superiore: attacco e colpo subito")]
        [SerializeField] string upperLayerName = "Parte superiore";
        [Tooltip("Nome dello stato di riposo, uguale in entrambi i livelli")]
        [SerializeField] string idleStateName = "Nessuna";
        [Tooltip("Velocità con cui i livelli si accendono e si spengono")]
        [SerializeField] float layerBlendSpeed = 12f;

        Creature creature;
        int combatLayer = -1;
        int upperLayer = -1;

        void Awake()
        {
            creature = GetComponent<Creature>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            FindLayers();
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
            if (upperLayer >= 0) animator.SetLayerWeight(upperLayer, 0f);
        }

        void Update()
        {
            if (animator == null) return;

            // Il livello a corpo intero resta acceso anche da morti (posa a terra).
            UpdateLayerWeight(combatLayer, creature.IsDead);
            UpdateLayerWeight(upperLayer, false);
        }

        /// <summary>
        /// Accende il livello se non è nello stato di riposo (o se forceOn è vero), altrimenti lo spegne piano.
        /// </summary>
        void UpdateLayerWeight(int layer, bool forceOn)
        {
            if (layer < 0) return;

            bool idle = animator.GetCurrentAnimatorStateInfo(layer).IsName(idleStateName)
                        && !animator.IsInTransition(layer);
            bool active = !idle || forceOn;

            float current = animator.GetLayerWeight(layer);
            float target = active ? 1f : 0f;
            animator.SetLayerWeight(layer, Mathf.MoveTowards(current, target, layerBlendSpeed * Time.deltaTime));
        }

        void FindLayers()
        {
            combatLayer = animator != null ? animator.GetLayerIndex(combatLayerName) : -1;
            upperLayer = animator != null ? animator.GetLayerIndex(upperLayerName) : -1;
        }

        /// <summary>
        /// Usa un nuovo Animator (per esempio quello ricreato da UMA).
        /// </summary>
        public void SetAnimator(Animator newAnimator)
        {
            animator = newAnimator;
            FindLayers();
            if (animator != null && creature != null) animator.SetBool(DeadHash, creature.IsDead);
            ApplicaPosa();
        }

        public void PlayAttack() => Trigger(AttackHash);
        public void PlayDodge() => Trigger(DodgeHash);
        public void PlayVault() => Trigger(VaultHash);
        public void PlayClimb() => Trigger(ClimbHash);

        /// <summary>Arrampicata alta. Se l'Animator non ha ancora ClimbHigh, usa la salita normale.</summary>
        public void PlayClimbHigh()
        {
            if (animator != null && HaParametro(ClimbHighHash)) Trigger(ClimbHighHash);
            else Trigger(ClimbHash);
        }

        /// <summary>Seduto, sdraiato o in piedi (tutti e due falsi). La chiama Postura su ogni PC.</summary>
        public void ImpostaPosa(bool siede, bool giace)
        {
            seduto = siede;
            sdraiato = giace;
            ApplicaPosa();
        }

        void ApplicaPosa()
        {
            if (animator == null) return;
            if (HaParametro(SedutoHash)) animator.SetBool(SedutoHash, seduto);
            if (HaParametro(SdraiatoHash)) animator.SetBool(SdraiatoHash, sdraiato);
            if (HaParametro(FemminileHash)) animator.SetBool(FemminileHash, CorpoFemminile());
        }

        /// <summary>Vero se il corpo UMA del personaggio è femminile (razza "Human Female").</summary>
        bool CorpoFemminile()
        {
            var avatar = GetComponent<DynamicCharacterAvatar>();
            return avatar != null && avatar.activeRace != null && avatar.activeRace.name != null
                   && avatar.activeRace.name.Contains("Female");
        }

        bool HaParametro(int hash)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.nameHash == hash) return true;
            return false;
        }

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
            if (HaParametro(ClimbHighHash)) animator.ResetTrigger(ClimbHighHash);
            seduto = sdraiato = false;
            ApplicaPosa();
            animator.SetBool(DeadHash, true);
        }

        void OnRevived()
        {
            if (animator != null) animator.SetBool(DeadHash, false);
        }
    }
}