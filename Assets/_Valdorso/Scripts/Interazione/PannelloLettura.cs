using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valdorso.UI;

namespace Valdorso.Interazione
{
    /// <summary>
    /// Il foglio che si apre leggendo una bacheca: una finestra "Oro e brace" al centro dello schermo
    /// con il titolo in Cinzel e il testo in EB Garamond. Il cursore resta nascosto: si chiude con E
    /// o allontanandosi (lo gestisce l'Interattore). Ne esiste uno solo alla volta.
    /// </summary>
    public static class PannelloLettura
    {
        static GameObject radice;
        static TMP_Text titoloTesto;
        static TMP_Text corpoTesto;

        public static bool Aperto => radice != null && radice.activeSelf;

        /// <summary>L'oggetto che si sta leggendo (per chiudere il foglio quando ci si allontana).</summary>
        public static Transform Fonte { get; private set; }

        public static void Apri(string titolo, string testo, Transform fonte)
        {
            if (radice == null) Costruisci();
            Fonte = fonte;
            titoloTesto.text = titolo;
            corpoTesto.text = string.IsNullOrWhiteSpace(testo) ? "<i>La bacheca è vuota.</i>" : testo;
            radice.SetActive(true);
        }

        public static void Chiudi()
        {
            Fonte = null;
            if (radice != null) radice.SetActive(false);
        }

        static void Costruisci()
        {
            radice = new GameObject("Pannello lettura", typeof(Canvas), typeof(CanvasScaler));
            Object.DontDestroyOnLoad(radice); // resta pronto per le bacheche successive; si nasconde e basta
            var canvas = radice.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30; // sopra la scritta con la E, sotto il menu di pausa
            var scaler = radice.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            GameSettings.ApplyToCanvas(scaler);

            RectTransform foglio = UIKit.Window(radice.transform, "Foglio", new Vector2(760f, 560f));

            titoloTesto = UIKit.Label(foglio, "", UIKit.TitleFont, 34f, UIKit.GoldLight,
                new Vector2(40f, -34f), new Vector2(680f, 50f), TextAlignmentOptions.Center);

            // Una linea d'oro sottile sotto il titolo
            RectTransform linea = UIKit.Box(foglio, "Linea", new Vector2(230f, -94f), new Vector2(300f, 2f));
            var img = linea.gameObject.AddComponent<Image>();
            img.color = UIKit.Fade(UIKit.Gold, 0.7f);
            img.raycastTarget = false;

            corpoTesto = UIKit.Label(foglio, "", UIKit.TextFont, 25f, UIKit.Text,
                new Vector2(56f, -118f), new Vector2(648f, 400f), TextAlignmentOptions.TopLeft);
            corpoTesto.textWrappingMode = TextWrappingModes.Normal;
            corpoTesto.overflowMode = TextOverflowModes.Ellipsis;
            corpoTesto.paragraphSpacing = 14f;

            radice.SetActive(false);
        }
    }
}