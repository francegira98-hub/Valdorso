using System.Collections.Generic;
using UnityEngine;

namespace Valdorso.Creatures
{
    /// <summary>
    /// Una fazione del mondo (viandanti, paesani, banditi, bestie, non morti...).
    /// Si crea da Project: clic destro > Create > Valdorso > Fazione.
    /// </summary>
    [CreateAssetMenu(fileName = "NuovaFazione", menuName = "Valdorso/Fazione")]
    public class FactionDefinition : ScriptableObject
    {
        [Tooltip("Identificativo unico, senza spazi. Non cambiarlo dopo averlo usato nei salvataggi.")]
        [SerializeField] string id = "neutrale";
        [SerializeField] string displayName = "Neutrale";
        [TextArea] [SerializeField] string description;
        [Tooltip("Fazioni con cui questa è in guerra aperta")]
        [SerializeField] List<FactionDefinition> hostileTo = new List<FactionDefinition>();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;

        public bool IsHostileTo(FactionDefinition other)
        {
            if (other == null || other == this) return false;
            return hostileTo.Contains(other) || other.hostileTo.Contains(this);
        }
    }
}
