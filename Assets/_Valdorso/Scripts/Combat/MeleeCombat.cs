using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Valdorso.Creatures;
using Valdorso.Stats;

namespace Valdorso.Combat
{
    /// <summary>
    /// Attacco corpo a corpo. Il giocatore chiede di attaccare (clic sinistro),
    /// il server controlla stamina e tempi, fa partire l'animazione per tutti
    /// e decide chi viene colpito. Nessun client può inventarsi un colpo.
    /// Durante una schivata non si attacca.
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class MeleeCombat : NetworkBehaviour
    {
        [Header("Arma")]
        [SerializeField] string weaponName = "Pugni";
        [SerializeField] float damage = 20f;
        [SerializeField] DamageType damageType = DamageType.Physical;

        [Header("Ritmo")]
        [SerializeField] float staminaCost = 15f;
        [Tooltip("Secondi minimi tra un attacco e l'altro")]
        [SerializeField] float cooldown = 0.9f;
        [Tooltip("Secondi dall'inizio dell'animazione al momento in cui il colpo arriva")]
        [SerializeField] float hitDelay = 0.4f;

        [Header("Area del colpo")]
        [SerializeField] float reach = 1.2f;
        [SerializeField] float radius = 0.9f;
        [SerializeField] float hitHeight = 1.0f;
        [SerializeField] LayerMask hitLayers = ~0;

        Creature creature;
        CreatureAnimator creatureAnimator;
        DodgeRoll dodge; // NUOVO: per non attaccare durante la capriola
        double serverNextAttack;
        double localNextAttack;
        readonly Collider[] hitBuffer = new Collider[16];
        readonly HashSet<Creature> hitThisSwing = new HashSet<Creature>();

        void Awake()
        {
            creature = GetComponent<Creature>();
            creatureAnimator = GetComponent<CreatureAnimator>();
            dodge = GetComponent<DodgeRoll>(); // NUOVO
        }

        void Update()
        {
            if (!isLocalPlayer || creature.IsDead) return;
            if (dodge != null && dodge.IsDodging) return; // NUOVO: niente attacchi mentre si rotola
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (Time.timeAsDouble < localNextAttack) return;

            localNextAttack = Time.timeAsDouble + cooldown;
            CmdAttack();
        }

        [Command]
        void CmdAttack()
        {
            double now = Time.timeAsDouble;
            if (creature.IsDead || now < serverNextAttack - 0.05) return;
            if (dodge != null && dodge.IsDodgingOnServer) return; // NUOVO: anche il server rifiuta
            if (!creature.Stats.TrySpend(VitalType.Stamina, staminaCost))
            {
                TargetNotEnoughStamina();
                return;
            }

            serverNextAttack = now + cooldown;
            RpcPlayAttack();
            StartCoroutine(ResolveHit());
        }

        [ClientRpc]
        void RpcPlayAttack()
        {
            if (creatureAnimator != null) creatureAnimator.PlayAttack();
        }

        [TargetRpc]
        void TargetNotEnoughStamina()
        {
            // In futuro: suono di fatica e barra lampeggiante.
            Debug.Log("[Valdorso] Stamina insufficiente per attaccare.");
        }

        IEnumerator ResolveHit()
        {
            yield return new WaitForSeconds(hitDelay);
            if (creature.IsDead) yield break;

            Vector3 center = HitCenter();
            int count = Physics.OverlapSphereNonAlloc(center, radius, hitBuffer, hitLayers, QueryTriggerInteraction.Ignore);
            hitThisSwing.Clear();

            for (int i = 0; i < count; i++)
            {
                Creature target = hitBuffer[i].GetComponentInParent<Creature>();
                if (target == null || target == creature || target.IsDead) continue;
                if (!hitThisSwing.Add(target)) continue; // un solo colpo per bersaglio a ogni fendente

                target.ReceiveDamage(DamageInfo.Create(damage, damageType, creature, weaponName));
            }
        }

        Vector3 HitCenter() => transform.position + transform.forward * reach + Vector3.up * hitHeight;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawSphere(HitCenter(), radius);
        }
    }
}