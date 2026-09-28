using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Valdorso.UI
{
    /// <summary>
    /// Le impostazioni del giocatore su questo PC: grafica, audio, comandi, accessibilità.
    /// Si salvano sul PC (PlayerPrefs), non sul server: ognuno ha le sue.
    /// Apply() le mette in pratica; le pagine delle impostazioni le cambiano e le salvano.
    /// </summary>
    public static class GameSettings
    {
        // ---------- Grafica ----------
        public static int QualityLevel;
        public static int ResolutionWidth, ResolutionHeight;
        public static FullScreenMode ScreenMode = FullScreenMode.FullScreenWindow;
        public static bool VSync = true;
        public static int FrameLimit;             // 0 = senza limite

        // ---------- Audio (0-1) ----------
        public static float MasterVolume = 1f;
        public static float MusicVolume = 0.8f;   // usato quando arriveranno le musiche
        public static float EffectsVolume = 1f;   // usato quando arriveranno i suoni

        // ---------- Comandi ----------
        public static float MouseSensitivity = 1f; // 0,25 - 3
        public static bool InvertY;

        // ---------- Accessibilità ----------
        public static float UiScale = 1f;          // 0,8 - 1,4: grandezza di testi e finestre
        public static int ColorblindMode;          // 0 nessuno, 1 protanopia, 2 deuteranopia, 3 tritanopia

        public static readonly int[] FrameLimits = { 0, 30, 60, 120, 144 };
        public static readonly string[] ColorblindNames = { "Nessuno", "Protanopia (rosso)", "Deuteranopia (verde)", "Tritanopia (blu)" };

        /// <summary>Cambia ogni volta che le impostazioni cambiano: chi le usa può accorgersene.</summary>
        public static int Version { get; private set; }
        public static event Action Changed;

        static bool loaded;

        const string Prefix = "Valdorso.";

        public static void Load()
        {
            QualityLevel = PlayerPrefs.GetInt(Prefix + "Quality", QualitySettings.GetQualityLevel());
            Resolution current = Screen.currentResolution;
            ResolutionWidth = PlayerPrefs.GetInt(Prefix + "ResW", Screen.width > 0 ? Screen.width : current.width);
            ResolutionHeight = PlayerPrefs.GetInt(Prefix + "ResH", Screen.height > 0 ? Screen.height : current.height);
            ScreenMode = (FullScreenMode)PlayerPrefs.GetInt(Prefix + "ScreenMode", (int)Screen.fullScreenMode);
            VSync = PlayerPrefs.GetInt(Prefix + "VSync", 1) == 1;
            FrameLimit = PlayerPrefs.GetInt(Prefix + "FrameLimit", 0);
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "Master", 1f);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "Music", 0.8f);
            EffectsVolume = PlayerPrefs.GetFloat(Prefix + "Effects", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat(Prefix + "MouseSens", 1f);
            InvertY = PlayerPrefs.GetInt(Prefix + "InvertY", 0) == 1;
            UiScale = PlayerPrefs.GetFloat(Prefix + "UiScale", 1f);
            ColorblindMode = PlayerPrefs.GetInt(Prefix + "Colorblind", 0);
            loaded = true;
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(Prefix + "Quality", QualityLevel);
            PlayerPrefs.SetInt(Prefix + "ResW", ResolutionWidth);
            PlayerPrefs.SetInt(Prefix + "ResH", ResolutionHeight);
            PlayerPrefs.SetInt(Prefix + "ScreenMode", (int)ScreenMode);
            PlayerPrefs.SetInt(Prefix + "VSync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "FrameLimit", FrameLimit);
            PlayerPrefs.SetFloat(Prefix + "Master", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "Music", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "Effects", EffectsVolume);
            PlayerPrefs.SetFloat(Prefix + "MouseSens", MouseSensitivity);
            PlayerPrefs.SetInt(Prefix + "InvertY", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "UiScale", UiScale);
            PlayerPrefs.SetInt(Prefix + "Colorblind", ColorblindMode);
            PlayerPrefs.Save();
        }

        /// <summary>Rimette i valori di partenza (senza salvarli).</summary>
        public static void ResetToDefaults()
        {
            QualityLevel = QualitySettings.names.Length - 1;
            Resolution native = Screen.currentResolution;
            ResolutionWidth = native.width;
            ResolutionHeight = native.height;
            ScreenMode = FullScreenMode.FullScreenWindow;
            VSync = true;
            FrameLimit = 0;
            MasterVolume = 1f;
            MusicVolume = 0.8f;
            EffectsVolume = 1f;
            MouseSensitivity = 1f;
            InvertY = false;
            UiScale = 1f;
            ColorblindMode = 0;
        }

        // Si caricano e si applicano appena parte il gioco, prima della prima scena.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            Load();
            Apply(false);
        }

        /// <summary>Mette in pratica le impostazioni. Con changeScreen = false non tocca risoluzione e modalità dello schermo.</summary>
        public static void Apply(bool changeScreen = true)
        {
            if (!loaded) Load();
            if (QualityLevel >= 0 && QualityLevel < QualitySettings.names.Length && QualityLevel != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync || FrameLimit <= 0 ? -1 : FrameLimit;
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
            if (changeScreen && !Application.isEditor && ResolutionWidth > 0 && ResolutionHeight > 0)
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, ScreenMode);
            Version++;
            Changed?.Invoke();
        }

        // ---------- Testi e finestre ----------

        /// <summary>
        /// Adatta una tela dell'interfaccia: grandezza scelta dal giocatore e forma dello schermo
        /// (sugli schermi larghi conta l'altezza, su quelli stretti la larghezza, così niente esce dai bordi).
        /// </summary>
        public static void ApplyToCanvas(CanvasScaler scaler)
        {
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;
            float scale = Mathf.Clamp(UiScale, 0.6f, 1.6f);
            scaler.referenceResolution = new Vector2(1920f, 1080f) / scale;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            scaler.matchWidthOrHeight = aspect >= 16f / 9f ? 1f : 0f;
        }

        // ---------- Comandi ----------

        /// <summary>Sensibilità e asse invertito sul comando "Look" del personaggio (mouse e levetta).</summary>
        public static void ApplyControls(PlayerInput playerInput)
        {
            if (playerInput == null || playerInput.actions == null) return;
            InputAction look = playerInput.actions.FindAction("Look");
            if (look == null) return;

            string invert = InvertY ? "false" : "true"; // di serie la visuale inverte l'asse Y: è il movimento "normale"
            float s = Mathf.Clamp(MouseSensitivity, 0.1f, 5f);
            for (int i = 0; i < look.bindings.Count; i++)
            {
                InputBinding b = look.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                string path = b.path ?? string.Empty;
                string processors;
                if (path.Contains("Pointer") || path.Contains("Mouse"))
                    processors = $"InvertVector2(invertX=false,invertY={invert}),ScaleVector2(x={F(0.05f * s)},y={F(0.05f * s)})";
                else if (path.Contains("Gamepad") || path.Contains("Stick"))
                    processors = $"InvertVector2(invertX=false,invertY={invert}),StickDeadzone,ScaleVector2(x={F(300f * s)},y={F(300f * s)})";
                else continue;
                look.ApplyBindingOverride(i, new InputBinding { overrideProcessors = processors });
            }
        }

        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>Le risoluzioni dello schermo, senza doppioni, dalla più piccola alla più grande.</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (Resolution r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!list.Contains(size)) list.Add(size);
            }
            if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }
    }
}
