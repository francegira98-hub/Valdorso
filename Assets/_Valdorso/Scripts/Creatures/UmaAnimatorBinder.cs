using Mirror;
using UnityEngine;

namespace Valdorso.Creatures
{
    /// <summary>
    /// UMA distrugge e ricrea l'Animator ogni volta che costruisce il personaggio.
    /// Questo componente se ne accorge e collega il nuovo Animator
    /// agli script che lo usano (rete e combattimento).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class UmaAnimatorBinder : MonoBehaviour
    {
        Animator current;
        NetworkAnimator networkAnimator;
        CreatureAnimator creatureAnimator;

        void Awake()
        {
            networkAnimator = GetComponent<NetworkAnimator>();
            creatureAnimator = GetComponent<CreatureAnimator>();
            // Collega subito l'Animator, prima che gli altri componenti lo cerchino:
            // così funziona anche se nel prefab il collegamento si è rotto.
            Rebind();
        }

        void FixedUpdate() => Rebind();
        void Update() => Rebind();

        void Rebind()
        {
            // Se l'Animator che conosciamo esiste ancora, non c'è niente da fare.
            if (current != null) return;

            // Altrimenti cerchiamo quello nuovo creato da UMA.
            Animator found = GetComponent<Animator>();
            if (found == null) return;

            current = found;
            if (networkAnimator != null) networkAnimator.animator = found;
            if (creatureAnimator != null) creatureAnimator.SetAnimator(found);
        }
    }
}
