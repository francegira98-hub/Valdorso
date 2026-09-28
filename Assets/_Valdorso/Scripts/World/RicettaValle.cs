using UnityEngine;

namespace Valdorso.World
{
    /// <summary>
    /// La "ricetta" della valle: quali texture usare e quanto alti fare monti, collina e lago.
    /// Lo strumento Valdorso → Valle → Crea la valle la crea da solo la prima volta
    /// (Assets/_Valdorso/Mondo/Valle/RicettaValle) e sceglie le texture per nome.
    /// Per cambiare una texture: selezionare la ricetta, trascinare un altro Terrain Layer
    /// nel campo giusto e ricostruire la valle.
    /// </summary>
    public class RicettaValle : ScriptableObject
    {
        [Header("Texture del terreno (Terrain Layer)")]
        [Tooltip("Il prato della conca, la texture più usata.")]
        public TerrainLayer prato;
        [Tooltip("Foglie e aghi sotto gli alberi del bosco e del boschetto.")]
        public TerrainLayer sottobosco;
        [Tooltip("Terra battuta: strade, villaggio, qualche chiazza nei prati.")]
        public TerrainLayer terra;
        [Tooltip("Roccia: pendii ripidi e cime dei monti.")]
        public TerrainLayer roccia;
        [Tooltip("Ciottoli: letto e rive del fiume, riva del lago.")]
        public TerrainLayer ciottoli;

        [Header("Tinte (bianco = colore originale; più scuro = texture più scura)")]
        public Color tintaPrato = Color.white;
        public Color tintaSottobosco = Color.white;
        public Color tintaTerra = new Color(0.62f, 0.55f, 0.48f);
        public Color tintaRoccia = new Color(0.68f, 0.66f, 0.63f);
        public Color tintaCiottoli = new Color(0.75f, 0.72f, 0.68f);

        [Header("Forma (metri)")]
        [Tooltip("Cambia il disegno delle colline e delle creste mantenendo la mappa.")]
        public int seme = 312;
        [Tooltip("Altezza del fondo della valle vicino al lago.")]
        public float fondoValle = 60f;
        [Tooltip("Quanto sale la valle da sud-ovest (lago) a nord-est (sorgente del fiume).")]
        public float pendenzaVersoLago = 12f;
        [Tooltip("Altezza dei monti a nord, sopra il fondo della valle.")]
        public float monteNord = 280f;
        [Tooltip("Altezza dei monti a sud, sopra il fondo della valle.")]
        public float monteSud = 110f;
        [Tooltip("Altezza della collina dell'Orso sopra la valle.")]
        public float collina = 55f;
        [Tooltip("Altezza dell'acqua del lago.")]
        public float livelloLago = 60f;
        [Tooltip("Profondità del lago al centro.")]
        public float profonditaLago = 7f;
    }
}