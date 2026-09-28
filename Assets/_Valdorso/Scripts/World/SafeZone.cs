using System.Collections.Generic;
using UnityEngine;

namespace Valdorso.World
{
    /// <summary>
    /// Un luogo sicuro (locanda, casa propria): chi lascia la valle da qui esce subito, senza attesa,
    /// e chi chiude il gioco qui dentro non resta nel mondo.
    /// Si mette su un oggetto con un collider "Is Trigger" che copre il luogo (una scatola intorno alla locanda).
    /// Non si vede e non ferma nessuno: serve solo a dire "qui sei al sicuro".
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SafeZone : MonoBehaviour
    {
        [Tooltip("Il nome del luogo, per i messaggi (es. la locanda del Guado)")]
        [SerializeField] string placeName = "un luogo sicuro";

        static readonly List<SafeZone> zones = new List<SafeZone>();
        Collider area;

        public string PlaceName => placeName;

        void Awake()
        {
            area = GetComponent<Collider>();
            area.isTrigger = true;
        }

        void OnEnable() => zones.Add(this);
        void OnDisable() => zones.Remove(this);

        /// <summary>Il luogo sicuro in cui si trova questo punto, oppure null.</summary>
        public static SafeZone At(Vector3 position)
        {
            foreach (SafeZone zone in zones)
            {
                if (zone == null || zone.area == null) continue;
                // Il punto è dentro se il punto più vicino del collider è il punto stesso.
                if ((zone.area.ClosestPoint(position) - position).sqrMagnitude < 0.0001f) return zone;
            }
            return null;
        }
    }
}
