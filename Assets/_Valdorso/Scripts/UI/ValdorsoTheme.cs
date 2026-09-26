using TMPro;
using UnityEngine;

namespace Valdorso.UI
{
    /// <summary>
    /// Tema UI "Oro e brace": colori, caratteri e immagini usati da tutte le schermate di Valdorso.
    /// Cambiando un valore qui cambia in tutto il gioco. Vedi la sezione "Stile di Valdorso" nel documento.
    /// Le immagini sono disegnate in bianco: il colore lo danno i campi qui sotto.
    /// </summary>
    [CreateAssetMenu(fileName = "TemaValdorso", menuName = "Valdorso/Tema UI")]
    public class ValdorsoTheme : ScriptableObject
    {
        [Header("Fondi e pannelli")]
        public Color background = new Color32(0x0E, 0x0B, 0x09, 0xFF);
        public Color panel = new Color32(0x15, 0x11, 0x0D, 0xFA);

        [Header("Pulsanti")]
        public Color button = new Color32(0x1C, 0x16, 0x12, 0xF0);
        public Color buttonHover = new Color32(0x3A, 0x2C, 0x1F, 0xFF);
        public Color buttonPressed = new Color32(0x5A, 0x44, 0x30, 0xFF);
        public Color buttonDisabled = new Color32(0x1C, 0x16, 0x12, 0x80);

        [Header("Oro")]
        public Color gold = new Color32(0xD4, 0xAF, 0x37, 0xFF);
        public Color goldLight = new Color32(0xF3, 0xDC, 0x8A, 0xFF);
        public Color goldDark = new Color32(0x8A, 0x6A, 0x1C, 0xFF);

        [Header("Testi")]
        public Color text = new Color32(0xE8, 0xD9, 0xB5, 0xFF);
        public Color textSoft = new Color32(0xC9, 0xB9, 0x9A, 0xFF);

        [Header("Pergamena (lettere, contratti, incarichi, mappa, diario)")]
        public Color parchment = new Color32(0xD8, 0xC4, 0x9A, 0xFF);
        public Color ink = new Color32(0x2B, 0x1D, 0x12, 0xFF);

        [Header("Colori di significato")]
        public Color ember = new Color32(0xD9, 0x77, 0x2B, 0xFF);
        public Color blood = new Color32(0x8E, 0x2B, 0x25, 0xFF);
        public Color arcane = new Color32(0x3E, 0x6F, 0xA8, 0xFF);
        public Color life = new Color32(0x5E, 0x8C, 0x3A, 0xFF);

        [Header("Caratteri")]
        public TMP_FontAsset titleFont;
        public TMP_FontAsset buttonFont;
        public TMP_FontAsset textFont;
        public TMP_FontAsset italicFont;

        [Header("Immagini (disegnate in bianco, colorate dal tema)")]
        [Tooltip("Cornice ornata a 9 fette per pannelli e finestre")]
        public Sprite frame;
        [Tooltip("Cornice semplice a 9 fette per i pulsanti")]
        public Sprite buttonFrame;
        [Tooltip("Fondo dei pulsanti, leggermente sfumato")]
        public Sprite buttonFill;
        [Tooltip("Separatore decorativo con rombo centrale")]
        public Sprite divider;
        [Tooltip("Texture di pergamena")]
        public Sprite parchmentTexture;
        [Tooltip("Puntino di luce morbida per le braci")]
        public Sprite emberDot;
        public Sprite vignette;
        public Sprite glow;

        [Header("Movimento")]
        [Tooltip("Durata delle dissolvenze dei pulsanti, in secondi")]
        public float fadeDuration = 0.12f;
        [Tooltip("Ingrandimento del pulsante sotto il mouse")]
        public float hoverScale = 1.03f;
    }
}
