using System.Collections;
using Mirror;
using StarterAssets;
using UnityEngine;
using Valdorso.Combat;
using Valdorso.Creatures;
using Valdorso.Stats;

namespace Valdorso.Movement
{
    /// <summary>
    /// Scavalcare e salire su ostacoli fino ad altezza uomo (casse, muretti, staccionate).
    /// Premendo Salto davanti a un ostacolo: se è basso e sottile lo si scavalca,
    /// se è più alto (fino a circa 1,9 m) ci si sale sopra. Altrimenti è un salto normale.
    /// Il movimento lo fa il PC di chi gioca; il server scala la stamina
    /// e fa vedere l'animazione agli altri.
    /// </summary>
    [DefaultExecutionOrder(-100)] // prima del ThirdPersonController, per "rubargli" il salto
    [RequireComponent(typeof(Creature))]
    [RequireComponent(typeof(CharacterController))]
    public class ObstacleTraversal : NetworkBehaviour
    {
        [Header("Ostacoli")]
        [Tooltip("Sotto questa altezza (metri) si salta normalmente")]
        [SerializeField] float minHeight = 0.5f;
        [Tooltip("Sopra questa altezza l'ostacolo è troppo alto: circa l'altezza di un uomo")]
        [SerializeField] float maxHeight = 1.9f;
        [Tooltip("Fino a questa altezza gli ostacoli sottili si scavalcano; oltre ci si sale sopra")]
        [SerializeField] float vaultMaxHeight = 1.2f;
        [Tooltip("Spessore massimo per scavalcare (staccionate, muretti)")]
        [SerializeField] float vaultMaxThickness = 0.8f;
        [Tooltip("Distanza massima dall'ostacolo")]
        [SerializeField] float reach = 0.9f;
        [SerializeField] LayerMask obstacleLayers = ~0;

        [Header("Tempi e costi")]
        [SerializeField] float vaultDuration = 0.8f;
        [SerializeField] float climbDuration = 1.2f;
        [SerializeField] float staminaCost = 10f;

        Creature creature;
        CharacterController controller;
        ThirdPersonController mover;
        StarterAssetsInputs input;
        CreatureAnimator creatureAnimator;
        DodgeRoll dodge;
        MeleeCombat melee;
        bool busy;

        /// <summary>Vero mentre si sta scavalcando o salendo.</summary>
        public bool IsTraversing => busy;

        void Awake()
        {
            creature = GetComponent<Creature>();
            controller = GetComponent<CharacterController>();
            mover = GetComponent<ThirdPersonController>();
            input = GetComponent<StarterAssetsInputs>();
            creatureAnimator = GetComponent<CreatureAnimator>();
            dodge = GetComponent<DodgeRoll>();
            melee = GetComponent<MeleeCombat>();
        }

        void Update()
        {
            if (!isLocalPlayer || busy || creature.IsDead || input == null) return;
            if (!input.jump) return;
            if (mover != null && !mover.Grounded) return;
            if (dodge != null && dodge.IsDodging) return;
            if (creature.Stats.Stamina < staminaCost) return; // niente stamina: salto normale

            if (!TryFindObstacle(out bool vault, out Vector3 over, out Vector3 landing)) return;

            input.jump = false; // il salto diventa "scavalcare" o "salire"
            StartCoroutine(Traverse(vault, over, landing));
            if (creatureAnimator != null)
            {
                if (vault) creatureAnimator.PlayVault();
                else creatureAnimator.PlayClimb();
            }
            CmdTraverse(vault);
        }

        /// <summary>Guarda davanti al personaggio e decide se scavalcare, salire o niente.</summary>
        bool TryFindObstacle(out bool vault, out Vector3 over, out Vector3 landing)
        {
            vault = false;
            over = landing = Vector3.zero;
            Vector3 feet = transform.position;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            fwd.Normalize();

            // 1. C'è qualcosa davanti, all'altezza delle ginocchia?
            if (!Physics.Raycast(feet + Vector3.up * 0.3f, fwd, out RaycastHit front, reach + controller.radius,
                    obstacleLayers, QueryTriggerInteraction.Ignore)) return false;
            if (front.collider.GetComponentInParent<Creature>() != null) return false; // le persone non si scavalcano
            if (Vector3.Angle(front.normal, Vector3.up) < 60f) return false;          // è una rampa, non un ostacolo

            // 2. Quanto è alto? Cerchiamo la cima guardando dall'alto verso il basso.
            Vector3 topStart = front.point + fwd * 0.1f + Vector3.up * (maxHeight + 0.5f);
            if (!Physics.Raycast(topStart, Vector3.down, out RaycastHit top, maxHeight + 0.5f,
                    obstacleLayers, QueryTriggerInteraction.Ignore)) return false;
            float height = top.point.y - feet.y;
            if (height < minHeight || height > maxHeight) return false;
            if (Vector3.Angle(top.normal, Vector3.up) > 30f) return false; // cima troppo inclinata

            // 3. Basso e sottile: si scavalca, se dall'altra parte c'è spazio per atterrare.
            if (height <= vaultMaxHeight)
            {
                Vector3 backStart = front.point + fwd * (vaultMaxThickness + 0.2f) + Vector3.up * 0.3f;
                if (Physics.Raycast(backStart, -fwd, out RaycastHit back, vaultMaxThickness + 0.2f,
                        obstacleLayers, QueryTriggerInteraction.Ignore))
                {
                    Vector3 land = back.point + fwd * (controller.radius + 0.3f);
                    if (Physics.Raycast(land + Vector3.up, Vector3.down, out RaycastHit ground, 2.5f,
                            obstacleLayers, QueryTriggerInteraction.Ignore) && FitsAt(ground.point))
                    {
                        vault = true;
                        over = top.point + Vector3.up * 0.15f;
                        landing = ground.point;
                        return true;
                    }
                }
            }

            // 4. Altrimenti ci si sale sopra, se in cima c'è spazio per stare in piedi.
            Vector3 onTop = top.point + fwd * (controller.radius + 0.2f);
            if (!Physics.Raycast(onTop + Vector3.up * 0.5f, Vector3.down, out RaycastHit stand, 1f,
                    obstacleLayers, QueryTriggerInteraction.Ignore)) return false;
            if (!FitsAt(stand.point)) return false;

            over = new Vector3(feet.x, stand.point.y + 0.05f, feet.z); // prima si sale in verticale...
            landing = stand.point;                                    // ...poi si avanza sulla cima
            return true;
        }

        /// <summary>Il personaggio ci sta in piedi in quel punto senza toccare niente?</summary>
        bool FitsAt(Vector3 feetPoint)
        {
            float r = controller.radius;
            Vector3 bottom = feetPoint + Vector3.up * (r + 0.05f);
            Vector3 head = feetPoint + Vector3.up * (controller.height - r);
            return !Physics.CheckCapsule(bottom, head, r * 0.9f, obstacleLayers, QueryTriggerInteraction.Ignore);
        }

        IEnumerator Traverse(bool vault, Vector3 over, Vector3 landing)
        {
            busy = true;

            // Durante il movimento spegniamo controller, attacco e schivata, per non farli interferire.
            bool moverWasOn = mover != null && mover.enabled;
            bool dodgeWasOn = dodge != null && dodge.enabled;
            bool meleeWasOn = melee != null && melee.enabled;
            if (mover != null) mover.enabled = false;
            if (dodge != null) dodge.enabled = false;
            if (melee != null) melee.enabled = false;
            controller.enabled = false;

            Vector3 start = transform.position;
            Vector3 flat = landing - start;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat);

            float duration = vault ? vaultDuration : climbDuration;
            float t = 0f;
            while (t < 1f && !creature.IsDead)
            {
                t += Time.deltaTime / duration;
                float k = Mathf.Clamp01(t);
                // Prima metà: si arriva sopra l'ostacolo. Seconda metà: si scende o si avanza fino all'arrivo.
                transform.position = k < 0.5f
                    ? Vector3.Lerp(start, over, Smooth(k * 2f))
                    : Vector3.Lerp(over, landing, Smooth((k - 0.5f) * 2f));
                yield return null;
            }
            if (!creature.IsDead) transform.position = landing;

            controller.enabled = true;
            if (dodge != null) dodge.enabled = dodgeWasOn;
            if (melee != null) melee.enabled = meleeWasOn;
            if (mover != null && moverWasOn && !creature.IsDead) mover.enabled = true;
            busy = false;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        [Command]
        void CmdTraverse(bool vault)
        {
            if (creature.IsDead) return;
            creature.Stats.TrySpend(VitalType.Stamina, staminaCost);
            RpcPlayTraverse(vault);
        }

        [ClientRpc(includeOwner = false)]
        void RpcPlayTraverse(bool vault)
        {
            // Chi scavalca ha già fatto partire l'animazione da solo; qui la vedono gli altri.
            if (creatureAnimator == null) return;
            if (vault) creatureAnimator.PlayVault();
            else creatureAnimator.PlayClimb();
        }
    }
}
