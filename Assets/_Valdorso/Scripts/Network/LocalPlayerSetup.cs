using Mirror;
using UnityEngine;

namespace Valdorso.Network
{
    /// <summary>
    /// Ogni PC vede tutti i personaggi, ma ne controlla uno solo.
    /// Questo componente accende input, controller e telecamera soltanto sul personaggio di chi gioca.
    /// </summary>
    public class LocalPlayerSetup : NetworkBehaviour
    {
        [Tooltip("Componenti attivi solo sul proprio personaggio: ThirdPersonController, StarterAssetsInputs, PlayerInput")]
        [SerializeField] Behaviour[] localOnlyBehaviours;

        [Tooltip("Oggetti attivi solo sul proprio personaggio: la PlayerFollowCamera")]
        [SerializeField] GameObject[] localOnlyObjects;

        [Tooltip("Stacca questi oggetti dal personaggio all'avvio, così la telecamera non ne eredita i movimenti")]
        [SerializeField] bool detachObjectsOnStart = true;

        void Awake()
        {
            SetLocalControl(false);
        }

        public override void OnStartLocalPlayer()
        {
            SetLocalControl(true);
            if (!detachObjectsOnStart) return;
            foreach (GameObject obj in localOnlyObjects)
                if (obj != null) obj.transform.SetParent(null, true);
        }

        void OnDestroy()
        {
            // Se gli oggetti erano stati staccati, vanno eliminati insieme al personaggio.
            if (!detachObjectsOnStart) return;
            foreach (GameObject obj in localOnlyObjects)
                if (obj != null && obj.transform.parent == null) Destroy(obj);
        }

        void SetLocalControl(bool isLocal)
        {
            foreach (Behaviour b in localOnlyBehaviours)
                if (b != null) b.enabled = isLocal;
            foreach (GameObject obj in localOnlyObjects)
                if (obj != null) obj.SetActive(isLocal);
        }
    }
}
