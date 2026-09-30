using System.Collections.Generic;
using UnityEngine;

namespace Valdorso.World
{
    /// <summary>
    /// Dove c'è acqua nella valle e a che altezza sta il pelo dell'acqua, per il nuoto.
    /// Lago e fiume di R.A.M non hanno collider: il menu Valdorso → Valle → Misura l'acqua li "tasta"
    /// in una griglia di celle da 2 m e scrive qui le quote. Si rilancia se si cambiano il lago o il fiume.
    /// Vale uguale sul server e su tutti i PC, senza fisica.
    /// </summary>
    public class AcqueValle : MonoBehaviour
    {
        [Tooltip("Lato delle celle in metri")]
        [SerializeField] float cella = 2f;

        [Tooltip("Le celle con l'acqua: per ognuna x e z (short) e quota (float). Le scrive il menu Misura l'acqua")]
        [SerializeField, HideInInspector] byte[] dati = new byte[0];

        [SerializeField] int numeroCelle;

        public static AcqueValle Istanza { get; private set; }

        readonly Dictionary<int, float> quote = new Dictionary<int, float>();

        public float Cella => cella;
        public int NumeroCelle => numeroCelle;

        void Awake()
        {
            Istanza = this;
            Carica();
        }

        void OnDestroy()
        {
            if (Istanza == this) Istanza = null;
        }

        static int Chiave(int ix, int iz) => ((ix + 32768) << 16) | ((iz + 32768) & 0xFFFF);

        void Carica()
        {
            quote.Clear();
            for (int i = 0; i + 8 <= dati.Length; i += 8)
            {
                short ix = System.BitConverter.ToInt16(dati, i);
                short iz = System.BitConverter.ToInt16(dati, i + 2);
                float y = System.BitConverter.ToSingle(dati, i + 4);
                quote[Chiave(ix, iz)] = y;
            }
        }

        /// <summary>Vero se in quel punto c'è acqua; 'y' è la quota del pelo dell'acqua.</summary>
        public bool Superficie(Vector3 p, out float y)
        {
            int ix = Mathf.FloorToInt(p.x / cella), iz = Mathf.FloorToInt(p.z / cella);
            return quote.TryGetValue(Chiave(ix, iz), out y);
        }

#if UNITY_EDITOR
        /// <summary>Solo per il menu Misura l'acqua: scrive le celle trovate.</summary>
        public void ScriviCelle(float lato, Dictionary<Vector2Int, float> celle)
        {
            cella = lato;
            var b = new List<byte>(celle.Count * 8);
            foreach (var kv in celle)
            {
                b.AddRange(System.BitConverter.GetBytes((short)kv.Key.x));
                b.AddRange(System.BitConverter.GetBytes((short)kv.Key.y));
                b.AddRange(System.BitConverter.GetBytes(kv.Value));
            }
            dati = b.ToArray();
            numeroCelle = celle.Count;
            Carica();
        }
#endif
    }
}