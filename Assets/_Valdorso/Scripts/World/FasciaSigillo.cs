using UnityEngine;
using Valdorso.Creatures;

namespace Valdorso.World
{
    /// <summary>La fascia davanti al velo: avvisa il sigillo quando ci entra il personaggio di chi gioca su questo PC.</summary>
    public class FasciaSigillo : MonoBehaviour
    {
        void OnTriggerEnter(Collider altro) => Controlla(altro);
        void OnTriggerStay(Collider altro) => Controlla(altro);

        void Controlla(Collider altro)
        {
            Creature c = altro.GetComponentInParent<Creature>();
            if (c == null || !c.isLocalPlayer) return;
            GetComponentInParent<SigilloCorona>()?.ToccatoDaQui(c);
        }
    }
}
