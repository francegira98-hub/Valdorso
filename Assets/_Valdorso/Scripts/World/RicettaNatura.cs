using System.Collections.Generic;
using UnityEngine;

namespace Valdorso.World
{
    /// <summary>
    /// La "ricetta" della natura della valle: quali alberi, rocce ed erbe piantare, e quanto fitti.
    /// Lo strumento Valdorso → Valle → Semina la natura la crea da solo la prima volta
    /// (Assets/_Valdorso/Mondo/Valle/RicettaNatura) e riempie gli elenchi cercando i prefab per nome.
    /// Per cambiare: selezionare la ricetta, togliere o trascinare prefab negli elenchi e seminare di nuovo.
    /// </summary>
    public class RicettaNatura : ScriptableObject
    {
        [Header("Alberi (diventano alberi del terreno, con i LOD e le sagome da lontano)")]
        [Tooltip("Bosco grande a ovest e boschetto a est.")]
        public List<GameObject> alberiBosco = new List<GameObject>();
        [Tooltip("Macchie di conifere sui pendii dei monti.")]
        public List<GameObject> alberiMonte = new List<GameObject>();
        [Tooltip("Alberi isolati nei prati della conca.")]
        public List<GameObject> alberiPrato = new List<GameObject>();
        [Tooltip("Cespugli ai margini del bosco e lungo il fiume.")]
        public List<GameObject> cespugli = new List<GameObject>();

        [Header("Rocce (diventano oggetti della scena, con il loro collider)")]
        public List<GameObject> rocceValle = new List<GameObject>();
        public List<GameObject> rocceBosco = new List<GameObject>();
        public List<GameObject> rocceMonte = new List<GameObject>();
        public List<GameObject> rocceFiume = new List<GameObject>();

        [Header("Erba (dettagli del terreno)")]
        public List<GameObject> erbaPrato = new List<GameObject>();
        public List<GameObject> erbaBosco = new List<GameObject>();
        public List<GameObject> erbaMonte = new List<GameObject>();

        [Header("Quantità")]
        [Tooltip("Cambia la disposizione mantenendo le regole.")]
        public int seme = 312;
        [Tooltip("Metri quadrati per albero nel bosco (più piccolo = più fitto).")]
        public float boscoMetriPerAlbero = 45f;
        [Tooltip("Metri quadrati per albero nelle macchie dei monti.")]
        public float monteMetriPerAlbero = 90f;
        [Tooltip("Probabilità di un albero isolato ogni 36 m² di prato.")]
        public float probabilitaAlberoPrato = 0.006f;
        [Tooltip("Quante rocce in tutto (valle, bosco, fiume, monti).")]
        public int rocceValleNumero = 70, rocceBoscoNumero = 140, rocceFiumeNumero = 50, rocceMonteNumero = 300;
        [Tooltip("Fili d'erba per cella di 2 × 2 m nei prati (l'erba di Meadow è a fili singoli: ne servono tanti).")]
        public int erbaPratoDensita = 24;
        [Tooltip("Distanza entro cui si vede l'erba, in metri (lo shader di Meadow la fa sparire comunque oltre 45-55 m).")]
        public float distanzaErba = 60f;
    }
}