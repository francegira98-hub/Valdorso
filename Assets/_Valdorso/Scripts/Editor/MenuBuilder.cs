using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Valdorso.UI;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Costruisce con un clic la schermata del menu principale nella scena Menu, nello stile "Oro e brace"
    /// (menu di Unity: Valdorso → Crea schermata del menu principale).
    /// Colori, caratteri e immagini vengono dal Tema UI (Art/UI/Tema/TemaValdorso).
    /// Se la schermata esiste già, la ricrea da capo. Funziona solo nell'editor, non entra nel gioco.
    /// </summary>
    public static class MenuBuilder
    {
        const string ThemePath = "Assets/_Valdorso/Art/UI/Tema/TemaValdorso.asset";
        const string RootName = "MenuCanvas";

        [MenuItem("Valdorso/Crea schermata del menu principale")]
        static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Menu")
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Menu (Assets/_Valdorso/Scenes/Menu).", "OK");
                return;
            }

            var theme = AssetDatabase.LoadAssetAtPath<ValdorsoTheme>(ThemePath);
            if (theme == null || theme.titleFont == null || theme.buttonFrame == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Manca il Tema UI: usa prima Valdorso → Genera tema e immagini UI.", "OK");
                return;
            }

            GameObject old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            NetworkManagerHUD hud = Object.FindFirstObjectByType<NetworkManagerHUD>();
            if (hud != null) Undo.DestroyObjectImmediate(hud);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystem, "Crea EventSystem");
            }

            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root, "Crea menu principale");
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvas = root.transform;

            // Sfondo: colore scuro, bagliore dorato dietro il titolo, braci, bordi sfumati nel nero.
            Stretch(AddImage(canvas, "Sfondo", theme.background, null, false));
            RectTransform glow = AddImage(canvas, "Bagliore", WithAlpha(Color.Lerp(theme.gold, theme.ember, 0.35f), 0.11f), theme.glow, false);
            Place(glow, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1500f, 760f));
            RectTransform embersArea = CreateUI("Braci", canvas);
            Stretch(embersArea);
            UIEmbers embers = embersArea.gameObject.AddComponent<UIEmbers>();
            SetField(embers, "theme", theme);
            Stretch(AddImage(canvas, "Vignetta", new Color(0f, 0f, 0f, 0.9f), theme.vignette, false));

            // Titolo: ombra sotto, poi il titolo con la sfumatura dall'oro chiaro all'oro scuro.
            TextMeshProUGUI shadow = AddText(canvas, "TitoloOmbra", "VALDORSO", theme.titleFont, 140f, new Color(0f, 0f, 0f, 0.7f), TextAlignmentOptions.Center);
            shadow.characterSpacing = 20f;
            Place(shadow.rectTransform, new Vector2(0.5f, 1f), new Vector2(4f, -236f), new Vector2(1500f, 210f));
            TextMeshProUGUI title = AddText(canvas, "Titolo", "VALDORSO", theme.titleFont, 140f, Color.white, TextAlignmentOptions.Center);
            title.characterSpacing = 20f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(theme.goldLight, theme.goldLight, theme.goldDark, theme.goldDark);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(1500f, 210f));

            RectTransform divider = AddImage(canvas, "Separatore", WithAlpha(theme.gold, 0.9f), theme.divider, false);
            Place(divider, new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(620f, 38f));
            TextMeshProUGUI motto = AddText(canvas, "Motto", "Ogni gesto lascia un segno, scegli chi diventare.", theme.italicFont, 30f, WithAlpha(theme.text, 0.8f), TextAlignmentOptions.Center);
            Place(motto.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(1200f, 50f));

            // Colonna dei pulsanti.
            RectTransform column = CreateUI("Pulsanti", canvas);
            column.anchorMin = column.anchorMax = new Vector2(0.5f, 0.5f);
            column.pivot = new Vector2(0.5f, 1f);
            column.anchoredPosition = new Vector2(0f, 60f);
            column.sizeDelta = new Vector2(560f, 0f);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button enter = AddButton(column, "Entra", "Entra nel mondo", theme);
            Button host = AddButton(column, "AvviaServer", "Avvia server (sviluppo)", theme);
            Button settings = AddButton(column, "Impostazioni", "Impostazioni", theme);
            Button quit = AddButton(column, "Esci", "Esci", theme);

            TextMeshProUGUI status = AddText(canvas, "Stato", string.Empty, theme.textFont, 28f, theme.textSoft, TextAlignmentOptions.Center);
            Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1200f, 120f));
            TextMeshProUGUI version = AddText(canvas, "Versione", "v0.1", theme.italicFont, 24f, WithAlpha(theme.textSoft, 0.6f), TextAlignmentOptions.BottomRight);
            version.rectTransform.pivot = new Vector2(1f, 0f);
            Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(400f, 40f));

            GameObject settingsPanel = BuildSettingsPanel(canvas, theme);

            MainMenu menu = root.AddComponent<MainMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("enterButton").objectReferenceValue = enter;
            so.FindProperty("hostButton").objectReferenceValue = host;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("versionText").objectReferenceValue = version;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("[Valdorso] Schermata del menu principale creata nello stile \"Oro e brace\". Salva la scena con Ctrl+S.");
        }

        static GameObject BuildSettingsPanel(Transform canvas, ValdorsoTheme theme)
        {
            RectTransform veil = AddImage(canvas, "PannelloImpostazioni", new Color(0f, 0f, 0f, 0.7f), null, true);
            Stretch(veil);

            RectTransform window = CreateUI("Finestra", veil);
            Place(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 600f));
            RectTransform fill = AddImage(window, "Fondo", theme.panel, null, true);
            Inset(fill, 6f);
            RectTransform frame = AddImage(window, "Cornice", theme.gold, theme.frame, false);
            Stretch(frame);
            MakeSliced(frame);

            TextMeshProUGUI title = AddText(window, "Titolo", "Impostazioni", theme.titleFont, 56f, theme.gold, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(800f, 90f));
            RectTransform divider = AddImage(window, "Separatore", WithAlpha(theme.gold, 0.8f), theme.divider, false);
            Place(divider, new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(420f, 26f));
            TextMeshProUGUI body = AddText(window, "Testo", "In preparazione: grafica, audio e comandi.", theme.textFont, 30f, theme.textSoft, TextAlignmentOptions.Center);
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(760f, 200f));

            RectTransform closeHolder = CreateUI("Chiudi", window);
            Place(closeHolder, new Vector2(0.5f, 0f), new Vector2(0f, 85f), new Vector2(320f, 72f));
            Button close = BuildButton(closeHolder, "Chiudi", theme);
            UnityEventTools.AddBoolPersistentListener(close.onClick, veil.gameObject.SetActive, false);

            veil.gameObject.SetActive(false);
            return veil.gameObject;
        }

        // ---------- Mattoncini ----------

        static Button AddButton(Transform parent, string name, string label, ValdorsoTheme theme)
        {
            RectTransform holder = CreateUI(name, parent);
            var element = holder.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 80f;
            return BuildButton(holder, label, theme);
        }

        /// <summary>Pulsante "Oro e brace": fondo sfumato che cambia col mouse, cornice a gemme, scritta in Cinzel.</summary>
        static Button BuildButton(RectTransform holder, string label, ValdorsoTheme theme)
        {
            RectTransform fill = AddImage(holder, "Fondo", Color.white, theme.buttonFill, true);
            Inset(fill, 3f);
            RectTransform frame = AddImage(holder, "Cornice", WithAlpha(theme.gold, 0.55f), theme.buttonFrame, false);
            Stretch(frame);
            MakeSliced(frame);

            Button button = holder.gameObject.AddComponent<Button>();
            button.targetGraphic = fill.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = theme.button;
            colors.highlightedColor = theme.buttonHover;
            colors.pressedColor = theme.buttonPressed;
            colors.selectedColor = theme.button;
            colors.disabledColor = theme.buttonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = theme.fadeDuration;
            button.colors = colors;

            TextMeshProUGUI text = AddText(holder, "Testo", label, theme.buttonFont, 34f, theme.text, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            MenuButtonFx fx = holder.gameObject.AddComponent<MenuButtonFx>();
            SetField(fx, "theme", theme);
            SetField(fx, "label", text);
            SetField(fx, "frame", frame.GetComponent<Image>());
            return button;
        }

        static RectTransform CreateUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform AddImage(Transform parent, string name, Color color, Sprite sprite, bool catchesClicks)
        {
            RectTransform rt = CreateUI(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = catchesClicks;
            return rt;
        }

        static void MakeSliced(RectTransform rt)
        {
            Image image = rt.GetComponent<Image>();
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
        }

        static TextMeshProUGUI AddText(Transform parent, string name, string content, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rt = CreateUI(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Inset(RectTransform rt, float border)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(border, border);
            rt.offsetMax = new Vector2(-border, -border);
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}