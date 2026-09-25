using Mirror;
using UnityEngine;
using Valdorso.Creatures;

namespace Valdorso.Network
{
    /// <summary>
    /// Ogni PC vede tutti i personaggi, ma ne controlla uno solo.
    /// Questo componente accende input, controller e telecamera soltanto sul personaggio di chi gioca,
    /// e blocca i comandi quando il personaggio è morto.
    /// </summary>
    public class LocalPlayerSetup : NetworkBehaviour
    {
        [Tooltip("Componenti attivi solo sul proprio personaggio: ThirdPersonController, StarterAssetsInputs, PlayerInput")]
        [SerializeField] Behaviour[] localOnlyBehaviours;

        [Tooltip("Oggetti attivi solo sul proprio personaggio: la PlayerFollowCamera")]
        [SerializeField] GameObject[] localOnlyObjects;

        [Tooltip("Stacca questi oggetti dal personaggio all'avvio, così la telecamera non ne eredita i movimenti")]
        [SerializeField] bool detachObjectsOnStart = true;

        Creature creature;

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
                if (creature.IsDead) SetBehaviours(false);
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

        void OnLocalDied() => SetBehaviours(false);
        void OnLocalRevived() => SetBehaviours(true);

        void SetBehaviours(bool enabled)
        {
            foreach (Behaviour b in localOnlyBehaviours)
                if (b != null) b.enabled = enabled;
        }

        void SetObjects(bool active)
        {
            foreach (GameObject obj in localOnlyObjects)
                if (obj != null) obj.SetActive(active);
        }
    }
}
