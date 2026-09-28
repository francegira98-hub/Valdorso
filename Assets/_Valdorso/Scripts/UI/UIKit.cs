using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Valdorso.UI
{
    /// <summary>
    /// Pezzi dell'interfaccia "Oro e brace" costruiti da codice: tele, finestre scure con cornice d'oro,
    /// testi, pulsanti, cursori e selettori ‹ valore ›. Li usano le impostazioni e il menu di pausa.
    /// Il tema lo registra il menu principale all'avvio (Theme); senza tema si usano colori di riserva.
    /// </summary>
    public static class UIKit
    {
        public static ValdorsoTheme Theme;

        public static Color Gold => Theme != null ? Theme.gold : new Color(0.83f, 0.69f, 0.22f);
        public static Color GoldLight => Theme != null ? Theme.goldLight : new Color(0.95f, 0.86f, 0.54f);
        public static Color Text => Theme != null ? Theme.text : new Color(0.91f, 0.85f, 0.71f);
        public static Color TextSoft => Theme != null ? Theme.textSoft : new Color(0.79f, 0.73f, 0.6f);
        public static Color PanelColor => Theme != null ? Theme.panel : new Color(0.08f, 0.07f, 0.05f, 0.98f);
        public static TMP_FontAsset TitleFont => Theme != null ? Theme.titleFont : null;
        public static TMP_FontAsset ButtonFont => Theme != null ? Theme.buttonFont : null;
        public static TMP_FontAsset TextFont => Theme != null ? Theme.textFont : null;
        public static TMP_FontAsset ItalicFont => Theme != null ? Theme.italicFont : null;

        public static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

        /// <summary>Una tela sopra tutto, che sopravvive ai cambi di scena.</summary>
        public static RectTransform PersistentCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            GameSettings.ApplyToCanvas(scaler);
            return (RectTransform)go.transform;
        }

        public static RectTransform Box(Transform parent, string name, Vector2 topLeft, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Un riquadro centrato nella tela.</summary>
        public static RectTransform Centered(Transform parent, string name, Vector2 size)
        {
            RectTransform rect = Box(parent, name, Vector2.zero, size);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Un velo scuro su tutto lo schermo, che blocca i clic sotto.</summary>
        public static RectTransform Veil(Transform parent, float alpha = 0.7f)
        {
            var go = new GameObject("Velo", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, alpha);
            return rect;
        }

        /// <summary>Una finestra scura con la cornice d'oro.</summary>
        public static RectTransform Window(Transform parent, string name, Vector2 size)
        {
            RectTransform window = Centered(parent, name, size);
            var back = window.gameObject.AddComponent<Image>();
            back.color = PanelColor;
            var frameGO = new GameObject("Cornice", typeof(RectTransform), typeof(Image));
            frameGO.transform.SetParent(window, false);
            Stretch((RectTransform)frameGO.transform, -6f, -6f, -6f, -6f);
            var frame = frameGO.GetComponent<Image>();
            frame.sprite = Theme != null ? Theme.frame : null;
            frame.type = Image.Type.Sliced;
            frame.color = Gold;
            frame.raycastTarget = false;
            if (frame.sprite == null) frameGO.SetActive(false);
            return window;
        }

        public static TMP_Text Label(Transform parent, string content, TMP_FontAsset font, float size, Color color,
            Vector2 topLeft, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            RectTransform rect = Box(parent, "Testo", topLeft, box);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = content;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static void Divider(Transform parent, Vector2 topLeft, float width)
        {
            if (Theme == null || Theme.divider == null) return;
            RectTransform rect = Box(parent, "Separatore", topLeft, new Vector2(width, 26f));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Theme.divider;
            image.color = Fade(Gold, 0.8f);
            image.raycastTarget = false;
        }

        public static Button MakeButton(Transform parent, string label, Vector2 topLeft, Vector2 size, float fontSize, Action onClick)
        {
            RectTransform rect = Box(parent, "Pulsante", topLeft, size);
            var fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = Theme != null ? Theme.buttonFill : null;
            fill.color = Color.white;

            var frameGO = new GameObject("Cornice", typeof(RectTransform), typeof(Image));
            frameGO.transform.SetParent(rect, false);
            Stretch((RectTransform)frameGO.transform);
            var frame = frameGO.GetComponent<Image>();
            frame.sprite = Theme != null ? Theme.buttonFrame : null;
            frame.type = Image.Type.Sliced;
            frame.color = Gold;
            frame.raycastTarget = false;
            if (frame.sprite == null) frameGO.SetActive(false);

            Label(rect, label, ButtonFont, fontSize, Text, Vector2.zero, size, TextAlignmentOptions.Center);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = Theme != null ? Theme.button : new Color(0.11f, 0.09f, 0.07f);
            colors.highlightedColor = Theme != null ? Theme.buttonHover : new Color(0.23f, 0.17f, 0.12f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = Theme != null ? Theme.buttonPressed : new Color(0.35f, 0.27f, 0.19f);
            colors.disabledColor = Theme != null ? Theme.buttonDisabled : new Color(0.11f, 0.09f, 0.07f, 0.5f);
            colors.fadeDuration = Theme != null ? Theme.fadeDuration : 0.12f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>La scelta attiva ha il fondo acceso e la scritta in oro chiaro.</summary>
        public static void MarkSelected(Button button, bool selected)
        {
            if (button == null) return;
            ColorBlock colors = button.colors;
            Color normal = Theme != null ? Theme.button : new Color(0.11f, 0.09f, 0.07f);
            Color lit = Theme != null ? Theme.buttonPressed : new Color(0.35f, 0.27f, 0.19f);
            colors.normalColor = selected ? lit : normal;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = selected ? GoldLight : Text;
        }

        /// <summary>Una riga "Nome   ‹ valore ›". Restituisce il testo del valore.</summary>
        public static TMP_Text Selector(Transform parent, string title, float y, Action<int> step)
        {
            Label(parent, title, TextFont, 26f, TextSoft, new Vector2(60f, y - 4f), new Vector2(380f, 44f));
            MakeButton(parent, "‹", new Vector2(460f, y), new Vector2(52f, 48f), 28f, () => step(-1));
            TMP_Text value = Label(parent, string.Empty, ItalicFont, 26f, Text, new Vector2(518f, y - 4f), new Vector2(424f, 44f), TextAlignmentOptions.Center);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.enableAutoSizing = true;
            value.fontSizeMin = 16f;
            value.fontSizeMax = 26f;
            MakeButton(parent, "›", new Vector2(948f, y), new Vector2(52f, 48f), 28f, () => step(1));
            return value;
        }

        /// <summary>Una riga "Nome   ————◆———  valore".</summary>
        public static Slider SliderRow(Transform parent, string title, float y, float min, float max, float value,
            Func<float, string> format, Action<float> changed)
        {
            Label(parent, title, TextFont, 26f, TextSoft, new Vector2(60f, y - 4f), new Vector2(380f, 44f));
            TMP_Text valueText = Label(parent, format(value), ItalicFont, 24f, Text, new Vector2(880f, y - 4f), new Vector2(120f, 44f), TextAlignmentOptions.Right);

            RectTransform rect = Box(parent, "Cursore", new Vector2(460f, y - 8f), new Vector2(400f, 30f));
            rect.gameObject.SetActive(false);
            RectTransform track = Box(rect, "Linea", new Vector2(0f, -12f), new Vector2(400f, 6f));
            track.gameObject.AddComponent<Image>().color = Fade(Text, 0.25f);
            RectTransform fillArea = Box(rect, "Area", new Vector2(0f, -12f), new Vector2(400f, 6f));
            RectTransform fill = Box(fillArea, "Riempimento", Vector2.zero, Vector2.zero);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = Fade(Gold, 0.8f);
            RectTransform handleArea = Box(rect, "Area_Maniglia", Vector2.zero, new Vector2(400f, 30f));
            RectTransform handle = Box(handleArea, "Maniglia", Vector2.zero, new Vector2(20f, 20f));
            handle.anchorMin = handle.anchorMax = new Vector2(0f, 0.5f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Gold;

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v =>
            {
                valueText.text = format(v);
                changed(v);
            });
            rect.gameObject.SetActive(true);
            return slider;
        }
    }
}