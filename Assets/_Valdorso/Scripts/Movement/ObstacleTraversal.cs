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
    /// Premendo Salto davanti a un ostacolo, tre mosse secondo l'altezza:
    /// - Scavalca: basso e sottile (fino a 1,2 m), mani sull'ostacolo e si passa dall'altra parte;
    /// - Sale: basso ma largo (fino a 1,3 m), mani sul bordo, ginocchio su, in piedi sopra;
    /// - Arrampica: alto (da 1,3 a 1,9 m), ci si aggrappa al bordo e ci si tira su.
    /// Altrimenti è un salto normale.
    /// I mobili degli interni (livello "Arredi") non si scavalcano: lì Spazio è un salto normale.
    /// Il movimento lo fa il PC di chi gioca; il server scala la stamina
    /// e fa vedere l'animazione agli altri.
    /// </summary>
    [DefaultExecutionOrder(-100)] // prima del ThirdPersonController, per "rubargli" il salto
    [RequireComponent(typeof(Creature))]
    [RequireComponent(typeof(CharacterController))]
    public class ObstacleTraversal : NetworkBehaviour
    {
        public enum Mossa : byte { Scavalca, Sale, Arrampica }

        /// <summary>Il livello dei mobili degli interni, creato da "Rendi vivo il villaggio".</summary>
        public const string LivelloArredi = "Arredi";

        [Header("Ostacoli")]
        [Tooltip("Sotto questa altezza (metri) si salta normalmente")]
        [SerializeField] float minHeight = 0.5f;
        [Tooltip("Sopra questa altezza l'ostacolo è troppo alto: circa l'altezza di un uomo")]
        [SerializeField] float maxHeight = 1.9f;
        [Tooltip("Fino a questa altezza gli ostacoli sottili si scavalcano")]
        [SerializeField] float vaultMaxHeight = 1.2f;
        [Tooltip("Spessore massimo per scavalcare (staccionate, muretti)")]
        [SerializeField] float vaultMaxThickness = 0.8f;
        [Tooltip("Da questa altezza in su non si sale più con il ginocchio: ci si aggrappa e ci si tira su")]
        [SerializeField] float altezzaArrampica = 1.3f;
        [Tooltip("Distanza massima dall'ostacolo")]
        [SerializeField] float reach = 0.9f;
        [SerializeField] LayerMask obstacleLayers = ~0;

        [Header("Tempi (li usa anche il menu delle animazioni per la velocità degli stati)")]
        [SerializeField] float durataScavalca = 0.9f;
        [SerializeField] float durataSale = 1.1f;
        [SerializeField] float durataArrampica = 1.6f;

        [Header("Costi")]
        [SerializeField] float staminaCost = 10f;
        [SerializeField] float costoArrampica = 15f;

        public float DurataScavalca => durataScavalca;
        public float DurataSale => durataSale;
        public float DurataArrampica => durataArrampica;

        Creature creature;
        CharacterController controller;
        ThirdPersonController mover;
        StarterAssetsInputs input;
        CreatureAnimator creatureAnimator;
        DodgeRoll dodge;
        MeleeCombat melee;
        bool busy;

        int ostacoli; // cosa si può scavalcare: tutto tranne gli arredi
        int spazio;   // cosa occupa spazio all'arrivo: tutto, arredi compresi

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

            int arredi = LayerMask.GetMask(LivelloArredi); // 0 se il livello non esiste ancora
            spazio = obstacleLayers;
            ostacoli = obstacleLayers & ~arredi;

            // Sui mobili si deve poter stare in piedi (tappeti, pedane): il controller li conta come terreno.
            // Mai il livello del giocatore stesso: si crederebbe sempre a terra e salterebbe all'infinito.
            int propri = 1 << gameObject.layer;
            if (mover != null && (arredi & propri) == 0) mover.GroundLayers |= arredi;
        }

        void Update()
        {
            if (!isLocalPlayer || busy || creature.IsDead || input == null) return;
            if (!input.jump) return;
            if (mover != null && !mover.Grounded) return;
            if (dodge != null && dodge.IsDodging) return;

            if (!TryFindObstacle(out Mossa mossa, out Vector3 over, out Vector3 landing)) return;
            if (creature.Stats.Stamina < Costo(mossa)) return; // niente stamina: salto normale

            input.jump = false; // il salto diventa scavalcare, salire o arrampicarsi
            StartCoroutine(Traverse(mossa, over, landing));
            Anima(mossa);
            CmdTraverse((byte)mossa);
        }

        float Costo(Mossa m) => m == Mossa.Arrampica ? costoArrampica : staminaCost;

        float Durata(Mossa m) => m == Mossa.Scavalca ? durataScavalca : m == Mossa.Sale ? durataSale : durataArrampica;

        void Anima(Mossa m)
        {
            if (creatureAnimator == null) return;
            switch (m)
            {
                case Mossa.Scavalca: creatureAnimator.PlayVault(); break;
                case Mossa.Sale: creatureAnimator.PlayClimb(); break;
                default: creatureAnimator.PlayClimbHigh(); break;
            }
        }

        /// <summary>Guarda davanti al personaggio e decide la mossa, oppure niente.</summary>
        bool TryFindObstacle(out Mossa mossa, out Vector3 over, out Vector3 landing)
        {
            mossa = Mossa.Sale;
            over = landing = Vector3.zero;
            Vector3 feet = transform.position;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            fwd.Normalize();

            // 1. C'è qualcosa davanti, all'altezza delle ginocchia? (gli arredi non contano)
            if (!Physics.Raycast(feet + Vector3.up * 0.3f, fwd, out RaycastHit front, reach + controller.radius,
                    ostacoli, QueryTriggerInteraction.Ignore)) return false;
            if (front.collider.GetComponentInParent<Creature>() != null) return false; // le persone non si scavalcano
            if (Vector3.Angle(front.normal, Vector3.up) < 60f) return false;          // è una rampa, non un ostacolo

            // 2. Quanto è alto? Cerchiamo la cima guardando dall'alto verso il basso.
            Vector3 topStart = front.point + fwd * 0.1f + Vector3.up * (maxHeight + 0.5f);
            if (!Physics.Raycast(topStart, Vector3.down, out RaycastHit top, maxHeight + 0.5f,
                    ostacoli, QueryTriggerInteraction.Ignore)) return false;
            float height = top.point.y - feet.y;
            if (height < minHeight || height > maxHeight) return false;
            if (Vector3.Angle(top.normal, Vector3.up) > 30f) return false; // cima troppo inclinata

            // 3. Basso e sottile: si scavalca, se dall'altra parte c'è spazio per atterrare.
            if (height <= vaultMaxHeight)
            {
                Vector3 backStart = front.point + fwd * (vaultMaxThickness + 0.2f) + Vector3.up * 0.3f;
                if (Physics.Raycast(backStart, -fwd, out RaycastHit back, vaultMaxThickness + 0.2f,
                        ostacoli, QueryTriggerInteraction.Ignore))
                {
                    Vector3 land = back.point + fwd * (controller.radius + 0.3f);
                    if (Physics.Raycast(land + Vector3.up, Vector3.down, out RaycastHit ground, 2.5f,
                            spazio, QueryTriggerInteraction.Ignore) && FitsAt(ground.point))
                    {
                        mossa = Mossa.Scavalca;
                        over = top.point + Vector3.up * 0.15f;
                        landing = ground.point;
                        return true;
                    }
                }
            }

            // 4. Altrimenti ci si sale sopra, se in cima c'è spazio per stare in piedi.
            Vector3 onTop = top.point + fwd * (controller.radius + 0.2f);
            if (!Physics.Raycast(onTop + Vector3.up * 0.5f, Vector3.down, out RaycastHit stand, 1f,
                    spazio, QueryTriggerInteraction.Ignore)) return false;
            if (!FitsAt(stand.point)) return false;

            mossa = height > altezzaArrampica ? Mossa.Arrampica : Mossa.Sale;
            // Prima si va contro il bordo salendo (mani sul bordo), poi si avanza sulla cima.
            Vector3 contro = front.point - fwd * (controller.radius + 0.05f);
            over = new Vector3(contro.x, stand.point.y + 0.05f, contro.z);
            landing = stand.point;
            return true;
        }

        /// <summary>Il personaggio ci sta in piedi in quel punto senza toccare niente (mobili compresi)?</summary>
        bool FitsAt(Vector3 feetPoint)
        {
            float r = controller.radius;
            Vector3 bottom = feetPoint + Vector3.up * (r + 0.05f);
            Vector3 head = feetPoint + Vector3.up * (controller.height - r);
            return !Physics.CheckCapsule(bottom, head, r * 0.9f, spazio, QueryTriggerInteraction.Ignore);
        }

        IEnumerator Traverse(Mossa mossa, Vector3 over, Vector3 landing)
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

            float duration = Durata(mossa);
            // Scavalcando la salita e la discesa durano uguali; salendo, tirarsi su è la parte lunga.
            float meta = mossa == Mossa.Scavalca ? 0.5f : 0.7f;
            float t = 0f;
            while (t < 1f && !creature.IsDead)
            {
                t += Time.deltaTime / duration;
                float k = Mathf.Clamp01(t);
                transform.position = k < meta
                    ? Vector3.Lerp(start, over, Smooth(k / meta))
                    : Vector3.Lerp(over, landing, Smooth((k - meta) / (1f - meta)));
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
        void CmdTraverse(byte m)
        {
            if (creature.IsDead || m > (byte)Mossa.Arrampica) return;
            creature.Stats.TrySpend(VitalType.Stamina, Costo((Mossa)m));
            RpcPlayTraverse(m);
        }

        [ClientRpc(includeOwner = false)]
        void RpcPlayTraverse(byte m)
        {
            // Chi scavalca ha già fatto partire l'animazione da solo; qui la vedono gli altri.
            Anima((Mossa)m);
        }
    }
}