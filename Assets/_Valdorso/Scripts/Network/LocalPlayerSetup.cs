using System.Collections;
using Mirror;
using UnityEngine;
using Valdorso.Creatures;

namespace Valdorso.Network
{
    /// <summary>
    /// Ogni PC vede tutti i personaggi, ma ne controlla uno solo.
    /// Questo componente accende input, controller e telecamera soltanto sul personaggio di chi gioca,
    /// blocca i comandi quando il personaggio è morto e li restituisce solo quando,
    /// dopo la rianimazione, ha finito di rialzarsi.
    /// </summary>
    public class LocalPlayerSetup : NetworkBehaviour
    {
        [Tooltip("Componenti attivi solo sul proprio personaggio: ThirdPersonController, StarterAssetsInputs, PlayerInput")]
        [SerializeField] Behaviour[] localOnlyBehaviours;

        [Tooltip("Oggetti attivi solo sul proprio personaggio: la PlayerFollowCamera")]
        [SerializeField] GameObject[] localOnlyObjects;

        [Tooltip("Stacca questi oggetti dal personaggio all'avvio, così la telecamera non ne eredita i movimenti")]
        [SerializeField] bool detachObjectsOnStart = true;

        [Tooltip("Azioni bloccate da morti e mentre ci si rialza: MeleeCombat, DodgeRoll, ObstacleTraversal")]
        [SerializeField] Behaviour[] lockedWhileDown;

        [Tooltip("Secondi dopo la rianimazione prima di ridare i comandi: durata della clip Rialzati diviso la sua Speed")]
        [SerializeField] float getUpDuration = 3f;

        Creature creature;
        Coroutine getUpRoutine;

        /// <summary>
        /// Vero mentre il personaggio è a terra o si sta rialzando.
        /// </summary>
        public bool IsDown { get; private set; }

        void Awake()
        {
            creature = GetComponent<Creature>();
            SetBehaviours(false);
            SetObjects(false);
        }

        public override void OnStartLocalPlayer()
        {
            SetBehaviours(true);
            SetObjects(true);

            if (creature != null)
            {
                creature.Died += OnLocalDied;
                creature.Revived += OnLocalRevived;
                if (creature.IsDead) OnLocalDied();
            }

            if (!detachObjectsOnStart) return;
            foreach (GameObject obj in localOnlyObjects)
                if (obj != null) obj.transform.SetParent(null, true);
        }

        void OnDestroy()
        {
            if (creature != null)
            {
                creature.Died -= OnLocalDied;
                creature.Revived -= OnLocalRevived;
            }

            // Se gli oggetti erano stati staccati, vanno eliminati insieme al personaggio.
            if (!detachObjectsOnStart) return;
            foreach (GameObject obj in localOnlyObjects)
                if (obj != null && obj.transform.parent == null) Destroy(obj);
        }

        void OnLocalDied()
        {
            // Se muore di nuovo mentre si rialza, l'attesa precedente non vale più.
            if (getUpRoutine != null)
            {
                StopCoroutine(getUpRoutine);
                getUpRoutine = null;
            }

            IsDown = true;
            SetBehaviours(false);
            SetLocked(false);
        }

        void OnLocalRevived()
        {
            if (getUpRoutine != null) StopCoroutine(getUpRoutine);
            getUpRoutine = StartCoroutine(GetUp());
        }

        /// <summary>
        /// Aspetta la fine dell'animazione per rialzarsi, poi restituisce i comandi.
        /// </summary>
        IEnumerator GetUp()
        {
            yield return new WaitForSeconds(getUpDuration);
            getUpRoutine = null;
            if (creature != null && creature.IsDead) yield break;

            IsDown = false;
            SetBehaviours(true);
            SetLocked(true);
        }

        void SetBehaviours(bool enabled)
        {
            foreach (Behaviour b in localOnlyBehaviours)
                if (b != null) b.enabled = enabled;
        }

        void SetLocked(bool enabled)
        {
            foreach (Behaviour b in lockedWhileDown)
                if (b != null) b.enabled = enabled;
        }

        void SetObjects(bool active)
        {
            foreach (GameObject obj in localOnlyObjects)
                if (obj != null) obj.SetActive(active);
        }
    }
}