using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valdorso.UI;

namespace Valdorso.Interazione
{
    /// <summary>
    /// La scritta in basso al centro dello schermo quando si può usare qualcosa: un tasto "E" nella cornice d'oro
    /// e accanto l'azione ("Accendi la lanterna"), nello stile "Oro e brace". Compare e sparisce con una dissolvenza.
    /// La crea l'Interattore solo sul PC di chi gioca.
    /// </summary>
    public class SuggerimentoInterazione : MonoBehaviour
    {
        const float LatoTasto = 46f;
        const float Spazio = 16f;
        const float AltezzaDalFondo = 230f;

        CanvasGroup gruppo;
        RectTransform riga;
        RectTransform tasto;
        RectTransform fondo;
        TMP_Text testo;
        float alfaVoluto;

        public static SuggerimentoInterazione Crea()
        {
            var go = new GameObject("Suggerimento interazione", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // sotto il menu di pausa (450) e le impostazioni (500)
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            GameSettings.ApplyToCanvas(scaler);

            var s = go.AddComponent<SuggerimentoInterazione>();
            s.Costruisci();
            return s;
        }

        void Costruisci()
        {
            gruppo = GetComponent<CanvasGroup>();
            gruppo.alpha = 0f;
            gruppo.interactable = false;
            gruppo.blocksRaycasts = false;

            // La riga intera, ancorata in basso al centro
            riga = UIKit.Box(transform, "Riga", Vector2.zero, new Vector2(600f, LatoTasto));
            riga.anchorMin = riga.anchorMax = new Vector2(0.5f, 0f);
            riga.pivot = new Vector2(0.5f, 0.5f);
            riga.anchoredPosition = new Vector2(0f, AltezzaDalFondo);

            // Il tasto: finestrella scura con la cornice d'oro e la lettera E
            tasto = UIKit.Window(riga, "Tasto", new Vector2(LatoTasto, LatoTasto));
            tasto.anchorMin = tasto.anchorMax = new Vector2(0f, 0.5f);
            tasto.pivot = new Vector2(0f, 0.5f);
            TMP_Text lettera = UIKit.Label(tasto, "E", UIKit.ButtonFont, 26f, UIKit.GoldLight,
                Vector2.zero, new Vector2(LatoTasto, LatoTasto), TextAlignmentOptions.Center);
            UIKit.Stretch((RectTransform)lettera.transform);

            // Un velo scuro leggero dietro la riga, per leggerla anche sul cielo chiaro o sulla neve
            fondo = UIKit.Box(riga, "Fondo", Vector2.zero, new Vector2(100f, LatoTasto + 12f));
            fondo.anchorMin = fondo.anchorMax = new Vector2(0.5f, 0.5f);
            fondo.pivot = new Vector2(0.5f, 0.5f);
            var velo = fondo.gameObject.AddComponent<Image>();
            velo.color = new Color(0f, 0f, 0f, 0.45f);
            velo.raycastTarget = false;
            fondo.SetAsFirstSibling();

            // L'azione
            testo = UIKit.Label(riga, "", UIKit.TextFont, 28f, UIKit.Text,
                Vector2.zero, new Vector2(500f, LatoTasto), TextAlignmentOptions.MidlineLeft);
            RectTransform rt = (RectTransform)testo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            testo.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>Mostra (o aggiorna) la scritta con l'azione.</summary>
        public void Mostra(string azione)
        {
            if (testo.text != azione)
            {
                testo.text = azione;
                // Centra la riga: tasto + spazio + testo
                float larghezzaTesto = testo.preferredWidth;
                float totale = LatoTasto + Spazio + larghezzaTesto;
                float sinistra = -totale / 2f + riga.sizeDelta.x / 2f;
                tasto.anchoredPosition = new Vector2(sinistra, 0f);
                ((RectTransform)testo.transform).anchoredPosition = new Vector2(sinistra + LatoTasto + Spazio, 0f);
                ((RectTransform)testo.transform).sizeDelta = new Vector2(larghezzaTesto + 4f, LatoTasto);
                fondo.sizeDelta = new Vector2(totale + 40f, LatoTasto + 12f);
            }
            alfaVoluto = 1f;
        }

        public void Nascondi() => alfaVoluto = 0f;

        void Update()
        {
            gruppo.alpha = Mathf.MoveTowards(gruppo.alpha, alfaVoluto, Time.unscaledDeltaTime * 6f);
        }
    }
}