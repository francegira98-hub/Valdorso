using System.IO;
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
    /// Costruisce con un clic la schermata del menu principale nella scena Menu
    /// (menu di Unity: Valdorso → Crea schermata del menu principale).
    /// Se la schermata esiste già, la ricrea da capo: per cambiare lo stile si modifica questo script.
    /// Funziona solo nell'editor, non entra nel gioco.
    /// </summary>
    public static class MenuBuilder
    {
        const string FontFolder = "Assets/_Valdorso/Art/UI/Font/";
        const string VignettePath = "Assets/_Valdorso/Art/UI/Sfondo_Vignetta.png";
        const string GlowPath = "Assets/_Valdorso/Art/UI/Sfondo_Bagliore.png";
        const string RootName = "MenuCanvas";

        // Colori dello stile fantasy
        static readonly Color Gold = new Color32(0xD4, 0xAF, 0x37, 0xFF);
        static readonly Color Parchment = new Color32(0xE8, 0xD9, 0xB5, 0xFF);
        static readonly Color SoftText = new Color32(0xC9, 0xB9, 0x9A, 0xFF);
        static readonly Color Background = new Color32(0x0E, 0x0B, 0x09, 0xFF);
        static readonly Color ButtonNormal = new Color32(0x1C, 0x16, 0x12, 0xF0);
        static readonly Color ButtonHover = new Color32(0x3A, 0x2C, 0x1F, 0xFF);
        static readonly Color ButtonPressed = new Color32(0x5A, 0x44, 0x30, 0xFF);
        static readonly Color ButtonDisabled = new Color32(0x1C, 0x16, 0x12, 0x80);

        [MenuItem("Valdorso/Crea schermata del menu principale")]
        static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Menu")
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Menu (Assets/_Valdorso/Scenes/Menu).", "OK");
                return;
            }

            TMP_FontAsset titleFont = LoadFont("Cinzel-Bold SDF");
            TMP_FontAsset buttonFont = LoadFont("Cinzel-Regular SDF");
            TMP_FontAsset textFont = LoadFont("EBGaramond-Regular SDF");
            TMP_FontAsset italicFont = LoadFont("EBGaramond-Italic SDF");
            if (titleFont == null || buttonFont == null || textFont == null || italicFont == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Mancano uno o più Font Asset in " + FontFolder + " (controlla la Console).", "OK");
                return;
            }

            // Se la schermata esiste già, la tolgo per ricrearla.
            GameObject old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            // I pulsantini grigi di Mirror non servono più: c'è il nostro menu.
            NetworkManagerHUD hud = Object.FindFirstObjectByType<NetworkManagerHUD>();
            if (hud != null) Undo.DestroyObjectImmediate(hud);

            // Senza EventSystem i pulsanti non ricevono i clic.
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystem, "Crea EventSystem");
            }

            // Tela che copre lo schermo, pensata per 1920x1080 e adattata alle altre risoluzioni.
            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root, "Crea menu principale");
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvas = root.transform;

            // Sfondo: colore scuro, bordi sfumati nel nero, bagliore dorato dietro il titolo.
            Stretch(AddImage(canvas, "Sfondo", Background, null));
            Stretch(AddImage(canvas, "Vignetta", new Color(0f, 0f, 0f, 0.9f), RadialSprite(VignettePath, 0f, 1f, 0.3f, 1f)));
            RectTransform glow = AddImage(canvas, "Bagliore", new Color(Gold.r, Gold.g, Gold.b, 0.10f), RadialSprite(GlowPath, 1f, 0f, 0f, 0.7f));
            Place(glow, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1500f, 700f));

            // Titolo e riga decorativa.
            TextMeshProUGUI title = AddText(canvas, "Titolo", "VALDORSO", titleFont, 130f, Gold, TextAlignmentOptions.Center);
            title.characterSpacing = 18f;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(1400f, 200f));
            RectTransform line = AddImage(canvas, "Riga", new Color(Gold.r, Gold.g, Gold.b, 0.7f), null);
            Place(line, new Vector2(0.5f, 1f), new Vector2(0f, -335f), new Vector2(520f, 2f));

            // Colonna dei pulsanti.
            RectTransform column = CreateUI("Pulsanti", canvas);
            column.anchorMin = column.anchorMax = new Vector2(0.5f, 0.5f);
            column.pivot = new Vector2(0.5f, 1f);
            column.anchoredPosition = new Vector2(0f, 60f);
            column.sizeDelta = new Vector2(560f, 0f);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button enter = AddButton(column, "Entra", "Entra nel mondo", buttonFont);
            Button host = AddButton(column, "AvviaServer", "Avvia server (sviluppo)", buttonFont);
            Button settings = AddButton(column, "Impostazioni", "Impostazioni", buttonFont);
            Button quit = AddButton(column, "Esci", "Esci", buttonFont);

            // Messaggi (connessione, errori) e versione del gioco.
            TextMeshProUGUI status = AddText(canvas, "Stato", string.Empty, textFont, 28f, SoftText, TextAlignmentOptions.Center);
            Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1200f, 120f));
            TextMeshProUGUI version = AddText(canvas, "Versione", "v0.1", italicFont, 24f, new Color(SoftText.r, SoftText.g, SoftText.b, 0.6f), TextAlignmentOptions.BottomRight);
            version.rectTransform.pivot = new Vector2(1f, 0f);
            Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(400f, 40f));

            // Pannello delle impostazioni (per ora solo la cornice: si riempie al passo delle impostazioni).
            GameObject settingsPanel = BuildSettingsPanel(canvas, titleFont, buttonFont, textFont);

            // Collegamento dei campi del componente MainMenu.
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
            Debug.Log("[Valdorso] Schermata del menu principale creata. Salva la scena con Ctrl+S.");
        }

        static GameObject BuildSettingsPanel(Transform canvas, TMP_FontAsset titleFont, TMP_FontAsset buttonFont, TMP_FontAsset textFont)
        {
            // Velo scuro su tutto lo schermo, così il resto del menu passa in secondo piano.
            RectTransform veil = AddImage(canvas, "PannelloImpostazioni", new Color(0f, 0f, 0f, 0.65f), null);
            Stretch(veil);

            RectTransform frame = AddImage(veil, "Cornice", new Color(Gold.r, Gold.g, Gold.b, 0.6f), null);
            Place(frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 600f));
            RectTransform inner = AddImage(frame, "Fondo", new Color32(0x15, 0x11, 0x0D, 0xFA), null);
            Inset(inner, 2f);

            TextMeshProUGUI title = AddText(inner, "Titolo", "Impostazioni", titleFont, 56f, Gold, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(800f, 90f));
            TextMeshProUGUI body = AddText(inner, "Testo", "In preparazione: grafica, audio e comandi.", textFont, 30f, SoftText, TextAlignmentOptions.Center);
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(760f, 200f));

            RectTransform closeHolder = CreateUI("Chiudi", inner);
            Place(closeHolder, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(320f, 72f));
            Button close = BuildButton(closeHolder, "Chiudi", buttonFont);
            UnityEventTools.AddBoolPersistentListener(close.onClick, veil.gameObject.SetActive, false);

            veil.gameObject.SetActive(false);
            return veil.gameObject;
        }

        // ---------- Mattoncini ----------

        static Button AddButton(Transform parent, string name, string label, TMP_FontAsset font)
        {
            RectTransform holder = CreateUI(name, parent);
            var element = holder.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 78f;
            return BuildButton(holder, label, font);
        }

        /// <summary>Pulsante con cornice dorata: la cornice è il contenitore, il fondo scuro cambia colore col mouse.</summary>
        static Button BuildButton(RectTransform holder, string label, TMP_FontAsset font)
        {
            Image border = holder.gameObject.AddComponent<Image>();
            border.color = new Color(Gold.r, Gold.g, Gold.b, 0.55f);

            RectTransform fill = AddImage(holder, "Fondo", Color.white, null);
            Inset(fill, 2f);
            Image fillImage = fill.GetComponent<Image>();

            Button button = holder.gameObject.AddComponent<Button>();
            button.targetGraphic = fillImage;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonNormal;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            TextMeshProUGUI text = AddText(fill, "Testo", label, font, 34f, Parchment, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
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

        static RectTransform AddImage(Transform parent, string name, Color color, Sprite sprite)
        {
            RectTransform rt = CreateUI(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = name != "Vignetta" && name != "Bagliore" && name != "Riga";
            return rt;
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

        static TMP_FontAsset LoadFont(string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + name + ".asset");
            if (font == null) Debug.LogError($"[Valdorso] Font Asset non trovato: {FontFolder}{name}.asset");
            return font;
        }

        /// <summary>
        /// Crea (una volta sola) un'immagine circolare sfumata e la salva nel progetto.
        /// L'opacità va da innerAlpha al centro a outerAlpha ai bordi, tra le distanze start ed end (0 = centro, 1 = angolo).
        /// </summary>
        static Sprite RadialSprite(string assetPath, float innerAlpha, float outerAlpha, float start, float end)
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (existing != null) return existing;

            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, distance));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Lerp(innerAlpha, outerAlpha, t)));
                }
            }
            texture.Apply();

            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
