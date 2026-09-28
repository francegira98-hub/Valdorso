using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Valdorso.UI
{
    /// <summary>
    /// La finestra delle impostazioni, uguale nel menu principale e nel menu di pausa:
    /// quattro schede (Grafica, Audio, Comandi, Accessibilità). Ogni cambio si vede subito;
    /// alla chiusura le impostazioni si salvano sul PC.
    /// Si apre con SettingsWindow.Open(); si costruisce da sola la prima volta.
    /// </summary>
    public class SettingsWindow : MonoBehaviour
    {
        static SettingsWindow instance;

        public static bool IsOpen => instance != null && instance.root != null && instance.root.gameObject.activeSelf;
        public static event Action Closed;

        RectTransform root;
        readonly Dictionary<string, GameObject> tabPages = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Button> tabButtons = new Dictionary<string, Button>();
        static readonly string[] Tabs = { "Grafica", "Audio", "Comandi", "Accessibilità" };

        List<Vector2Int> resolutions;
        TMP_Text qualityValue, resolutionValue, screenModeValue, vsyncValue, frameLimitValue, invertValue, colorblindValue;
        readonly List<Slider> sliders = new List<Slider>();

        public static void Open()
        {
            if (instance == null)
            {
                RectTransform canvas = UIKit.PersistentCanvas("Impostazioni", 500);
                instance = canvas.gameObject.AddComponent<SettingsWindow>();
                instance.Build(canvas);
            }
            instance.Refresh();
            instance.root.gameObject.SetActive(true);
            instance.ShowTab(Tabs[0]);
        }

        public static void Close()
        {
            if (instance == null || !IsOpen) return;
            GameSettings.Save();
            instance.root.gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void Update()
        {
            // Esc chiude la finestra (prima di tutto il resto).
            if (IsOpen && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        void Build(RectTransform canvas)
        {
            root = UIKit.Veil(canvas, 0.72f);
            RectTransform window = UIKit.Window(root, "Finestra", new Vector2(1060f, 760f));

            UIKit.Label(window, "IMPOSTAZIONI", UIKit.TitleFont, 46f, UIKit.Gold, new Vector2(0f, -30f), new Vector2(1060f, 60f), TextAlignmentOptions.Center);
            UIKit.Divider(window, new Vector2(330f, -92f), 400f);

            float tabWidth = 940f / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                string tab = Tabs[i];
                tabButtons[tab] = UIKit.MakeButton(window, tab.ToUpperInvariant(), new Vector2(60f + i * tabWidth, -130f),
                    new Vector2(tabWidth - 8f, 48f), 21f, () => ShowTab(tab));
                RectTransform page = UIKit.Box(window, "Scheda_" + tab, new Vector2(0f, -205f), new Vector2(1060f, 440f));
                tabPages[tab] = page.gameObject;
            }

            BuildGraphics(tabPages["Grafica"].transform);
            BuildAudio(tabPages["Audio"].transform);
            BuildControls(tabPages["Comandi"].transform);
            BuildAccessibility(tabPages["Accessibilità"].transform);

            UIKit.MakeButton(window, "RIPRISTINA PREDEFINITI", new Vector2(60f, -670f), new Vector2(380f, 56f), 20f, () =>
            {
                GameSettings.ResetToDefaults();
                GameSettings.Apply();
                Refresh();
            });
            UIKit.MakeButton(window, "CHIUDI", new Vector2(700f, -670f), new Vector2(300f, 56f), 22f, Close);
            root.gameObject.SetActive(false);
        }

        void ShowTab(string tab)
        {
            foreach (var pair in tabPages) pair.Value.SetActive(pair.Key == tab);
            foreach (var pair in tabButtons) UIKit.MarkSelected(pair.Value, pair.Key == tab);
        }

        // ---------- Schede ----------

        void BuildGraphics(Transform page)
        {
            resolutions = GameSettings.Resolutions();
            qualityValue = UIKit.Selector(page, "Qualità", 0f, d =>
            {
                int count = QualitySettings.names.Length;
                GameSettings.QualityLevel = ((GameSettings.QualityLevel + d) % count + count) % count;
                ApplyAndRefresh(false);
            });
            resolutionValue = UIKit.Selector(page, "Risoluzione", -62f, d =>
            {
                int index = CurrentResolutionIndex();
                index = Mathf.Clamp(index + d, 0, resolutions.Count - 1);
                GameSettings.ResolutionWidth = resolutions[index].x;
                GameSettings.ResolutionHeight = resolutions[index].y;
                ApplyAndRefresh(true);
            });
            screenModeValue = UIKit.Selector(page, "Schermo", -124f, d =>
            {
                FullScreenMode[] modes = { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen };
                int index = Array.IndexOf(modes, GameSettings.ScreenMode);
                index = ((index + d) % modes.Length + modes.Length) % modes.Length;
                GameSettings.ScreenMode = modes[index];
                ApplyAndRefresh(true);
            });
            vsyncValue = UIKit.Selector(page, "Sincronia verticale (VSync)", -186f, d =>
            {
                GameSettings.VSync = !GameSettings.VSync;
                ApplyAndRefresh(false);
            });
            frameLimitValue = UIKit.Selector(page, "Limite di fotogrammi", -248f, d =>
            {
                int[] limits = GameSettings.FrameLimits;
                int index = Mathf.Max(0, Array.IndexOf(limits, GameSettings.FrameLimit));
                index = ((index + d) % limits.Length + limits.Length) % limits.Length;
                GameSettings.FrameLimit = limits[index];
                ApplyAndRefresh(false);
            });
            UIKit.Label(page, "Risoluzione e modalità dello schermo si vedono solo nel gioco completo, non nell'editor. Con la sincronia verticale il limite di fotogrammi non serve.",
                UIKit.ItalicFont, 20f, UIKit.Fade(UIKit.TextSoft, 0.8f), new Vector2(60f, -320f), new Vector2(940f, 80f));
        }

        void BuildAudio(Transform page)
        {
            sliders.Add(UIKit.SliderRow(page, "Volume generale", 0f, 0f, 1f, GameSettings.MasterVolume, Percent,
                v => { GameSettings.MasterVolume = v; GameSettings.Apply(false); }));
            sliders.Add(UIKit.SliderRow(page, "Musica", -70f, 0f, 1f, GameSettings.MusicVolume, Percent,
                v => GameSettings.MusicVolume = v));
            sliders.Add(UIKit.SliderRow(page, "Effetti sonori", -140f, 0f, 1f, GameSettings.EffectsVolume, Percent,
                v => GameSettings.EffectsVolume = v));
            UIKit.Label(page, "Musica ed effetti sonori arriveranno più avanti: la loro regolazione è già pronta.",
                UIKit.ItalicFont, 20f, UIKit.Fade(UIKit.TextSoft, 0.8f), new Vector2(60f, -220f), new Vector2(940f, 60f));
        }

        void BuildControls(Transform page)
        {
            sliders.Add(UIKit.SliderRow(page, "Sensibilità del mouse", 0f, 0.25f, 3f, GameSettings.MouseSensitivity, v => v.ToString("0.00"),
                v => { GameSettings.MouseSensitivity = v; GameSettings.Apply(false); }));
            invertValue = UIKit.Selector(page, "Asse verticale invertito", -70f, d =>
            {
                GameSettings.InvertY = !GameSettings.InvertY;
                ApplyAndRefresh(false);
            });
            UIKit.Label(page, "I COMANDI", UIKit.ButtonFont, 22f, UIKit.Gold, new Vector2(60f, -140f), new Vector2(940f, 34f));
            string keys =
                "W A S D  ·  muoversi               Mouse  ·  guardarsi intorno\n" +
                "Maiuscolo  ·  correre              Spazio  ·  saltare, scavalcare, arrampicarsi\n" +
                "C  ·  capriola                       Tasto sinistro  ·  attaccare\n" +
                "Esc  ·  menu di pausa";
            TMP_Text list = UIKit.Label(page, keys, UIKit.TextFont, 22f, UIKit.Text, new Vector2(60f, -180f), new Vector2(940f, 180f));
            list.lineSpacing = 10f;
            UIKit.Label(page, "Più avanti si potranno cambiare anche i tasti.",
                UIKit.ItalicFont, 20f, UIKit.Fade(UIKit.TextSoft, 0.8f), new Vector2(60f, -380f), new Vector2(940f, 40f));
        }

        void BuildAccessibility(Transform page)
        {
            sliders.Add(UIKit.SliderRow(page, "Grandezza di testi e finestre", 0f, 0.8f, 1.4f, GameSettings.UiScale, Percent,
                v => { GameSettings.UiScale = v; GameSettings.Apply(false); }));
            colorblindValue = UIKit.Selector(page, "Colori per daltonici", -70f, d =>
            {
                int count = GameSettings.ColorblindNames.Length;
                GameSettings.ColorblindMode = ((GameSettings.ColorblindMode + d) % count + count) % count;
                ApplyAndRefresh(false);
            });
            UIKit.Label(page, "La grandezza cambia subito tutte le finestre del gioco. I colori per daltonici si applicheranno alle barre e ai segni del gioco quando arriveranno (salute, nemici, alleati).",
                UIKit.ItalicFont, 20f, UIKit.Fade(UIKit.TextSoft, 0.8f), new Vector2(60f, -150f), new Vector2(940f, 90f));
        }

        // ---------- Valori ----------

        static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        int CurrentResolutionIndex()
        {
            for (int i = 0; i < resolutions.Count; i++)
                if (resolutions[i].x == GameSettings.ResolutionWidth && resolutions[i].y == GameSettings.ResolutionHeight) return i;
            return resolutions.Count - 1;
        }

        void ApplyAndRefresh(bool changeScreen)
        {
            GameSettings.Apply(changeScreen);
            Refresh();
        }

        void Refresh()
        {
            string[] names = QualitySettings.names;
            int q = Mathf.Clamp(GameSettings.QualityLevel, 0, names.Length - 1);
            qualityValue.text = QualityName(names[q]);
            resolutionValue.text = $"{GameSettings.ResolutionWidth} × {GameSettings.ResolutionHeight}";
            screenModeValue.text = GameSettings.ScreenMode == FullScreenMode.Windowed ? "In finestra"
                : GameSettings.ScreenMode == FullScreenMode.ExclusiveFullScreen ? "Schermo intero esclusivo" : "Schermo intero";
            vsyncValue.text = GameSettings.VSync ? "Attiva" : "Spenta";
            frameLimitValue.text = GameSettings.FrameLimit <= 0 ? "Senza limite" : GameSettings.FrameLimit + " al secondo";
            invertValue.text = GameSettings.InvertY ? "Sì" : "No";
            colorblindValue.text = GameSettings.ColorblindNames[Mathf.Clamp(GameSettings.ColorblindMode, 0, GameSettings.ColorblindNames.Length - 1)];

            // I cursori prendono i valori salvati (per esempio dopo "Ripristina").
            if (sliders.Count >= 5)
            {
                sliders[0].value = GameSettings.MasterVolume;
                sliders[1].value = GameSettings.MusicVolume;
                sliders[2].value = GameSettings.EffectsVolume;
                sliders[3].value = GameSettings.MouseSensitivity;
                sliders[4].value = GameSettings.UiScale;
            }
        }

        static string QualityName(string unityName)
        {
            switch (unityName)
            {
                case "Mobile": return "Leggera";
                case "PC": return "Alta";
                default: return unityName;
            }
        }
    }
}