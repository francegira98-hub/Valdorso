using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Valdorso.Network;
using Valdorso.UI;

namespace Valdorso.Creation
{
    /// <summary>
    /// Il regista della scena di creazione (il Registro di Val d'Orso).
    /// Quando la scena si apre sopra il mondo, spegne le telecamere del mondo e mette l'atmosfera della notte;
    /// quando il personaggio è scritto nel registro, riaccende il mondo, chiede di entrare e chiude la scena.
    /// Per ora (passo 5.1b) il registro ha solo la firma: le pagine arrivano dal passo 5.2.
    /// </summary>
    public class CreationController : MonoBehaviour
    {
        [SerializeField] ValdorsoTheme theme;
        [SerializeField] Camera stageCamera;

        [Header("Atmosfera (vale mentre la creazione è aperta)")]
        [SerializeField] Color fogColor = new Color(0.035f, 0.04f, 0.055f);
        [SerializeField] float fogDensity = 0.14f;
        [SerializeField] Color ambientColor = new Color(0.05f, 0.055f, 0.07f);

        // Com'era il mondo prima di aprire la creazione, per rimetterlo uguale.
        readonly List<Camera> hiddenCameras = new List<Camera>();
        readonly List<Light> hiddenSuns = new List<Light>();
        bool worldSaved;
        bool savedFog;
        FogMode savedFogMode;
        Color savedFogColor;
        float savedFogDensity;
        AmbientMode savedAmbientMode;
        Color savedAmbientLight;
        Material savedSkybox;

        Button signButton;
        TMP_Text statusText;
        bool busy;

        void Start()
        {
            if (stageCamera == null) stageCamera = GetComponentInChildren<Camera>(true);
            HideWorld();
            EnsureEventSystem();
            BuildTemporaryRegister();
        }

        void OnDestroy()
        {
            RestoreWorld();
        }

        // ---------- Mondo e atmosfera ----------

        void HideWorld()
        {
            foreach (Camera c in Camera.allCameras)
            {
                if (c == stageCamera) continue;
                c.enabled = false;
                hiddenCameras.Add(c);
            }
            // Il sole (o la luna) del mondo illuminerebbe anche il palco: si spegne finché la creazione è aperta.
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional || !l.enabled || l.gameObject.scene == gameObject.scene) continue;
                l.enabled = false;
                hiddenSuns.Add(l);
            }

            savedFog = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            savedAmbientMode = RenderSettings.ambientMode;
            savedAmbientLight = RenderSettings.ambientLight;
            savedSkybox = RenderSettings.skybox;
            worldSaved = true;

            // La scena del mondo resta quella "attiva" (così i giocatori degli altri nascono nel mondo, non qui):
            // l'atmosfera della notte si mette a mano e poi si toglie.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.skybox = null;
        }

        void RestoreWorld()
        {
            if (!worldSaved) return;
            worldSaved = false;
            foreach (Camera c in hiddenCameras)
                if (c != null) c.enabled = true;
            hiddenCameras.Clear();
            foreach (Light l in hiddenSuns)
                if (l != null) l.enabled = true;
            hiddenSuns.Clear();

            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbientLight;
            RenderSettings.skybox = savedSkybox;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        }

        // ---------- Il registro (provvisorio: solo la firma) ----------

        void BuildTemporaryRegister()
        {
            var canvasGO = new GameObject("Registro", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGO.transform;

            Color gold = theme != null ? theme.gold : new Color(0.83f, 0.69f, 0.22f);
            Color soft = theme != null ? theme.textSoft : new Color(0.79f, 0.73f, 0.6f);
            Color text = theme != null ? theme.text : new Color(0.91f, 0.85f, 0.71f);

            Text(root, "IL REGISTRO DI VAL D'ORSO", theme != null ? theme.titleFont : null, 50f, gold, new Vector2(140f, -170f), new Vector2(760f, 70f));
            Text(root, "Anno 312 dopo il Crepuscolo", theme != null ? theme.italicFont : null, 30f, soft, new Vector2(140f, -240f), new Vector2(760f, 44f));
            Text(root, "Per ultimi vennero gli uomini, fragili e brevi,\nma gli unici capaci di scegliere chi diventare.",
                theme != null ? theme.italicFont : null, 30f, text, new Vector2(140f, -330f), new Vector2(760f, 100f));

            signButton = MakeButton(root, "FIRMA IL REGISTRO", new Vector2(140f, -500f), new Vector2(420f, 64f));
            signButton.onClick.AddListener(OnSign);

            statusText = Text(root, string.Empty, theme != null ? theme.italicFont : null, 26f, soft, new Vector2(140f, -590f), new Vector2(760f, 120f));
        }

        TMP_Text Text(Transform parent, string content, TMP_FontAsset font, float size, Color color, Vector2 topLeft, Vector2 box)
        {
            var go = new GameObject("Testo", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = box;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = content;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            return tmp;
        }

        Button MakeButton(Transform parent, string label, Vector2 topLeft, Vector2 size)
        {
            var go = new GameObject("Pulsante_Firma", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;

            var fill = go.GetComponent<Image>();
            fill.sprite = theme != null ? theme.buttonFill : null;
            fill.color = Color.white;

            var frameGO = new GameObject("Cornice", typeof(RectTransform), typeof(Image));
            frameGO.transform.SetParent(go.transform, false);
            var frameRect = (RectTransform)frameGO.transform;
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
            var frame = frameGO.GetComponent<Image>();
            frame.sprite = theme != null ? theme.buttonFrame : null;
            frame.type = Image.Type.Sliced;
            frame.color = theme != null ? theme.gold : Color.yellow;
            frame.raycastTarget = false;

            TMP_Text text = Text(go.transform, label, theme != null ? theme.buttonFont : null, 28f, theme != null ? theme.text : Color.white, Vector2.zero, size);
            text.alignment = TextAlignmentOptions.Center;

            var button = go.GetComponent<Button>();
            button.targetGraphic = fill;
            if (theme != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = theme.button;
                colors.highlightedColor = theme.buttonHover;
                colors.selectedColor = theme.buttonHover;
                colors.pressedColor = theme.buttonPressed;
                colors.disabledColor = theme.buttonDisabled;
                colors.fadeDuration = theme.fadeDuration;
                button.colors = colors;
            }
            return button;
        }

        void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        // ---------- La firma ----------

        void OnSign()
        {
            if (busy) return;
            if (!NetworkClient.isConnected)
            {
                SetStatus("Nessun server collegato: questa è solo un'anteprima della scena.");
                return;
            }
            busy = true;
            signButton.interactable = false;
            SetStatus("Il sacerdote scrive il tuo nome nel registro...");
            // Nome, fede e aspetto arriveranno dalle pagine del registro (passi 5.2-5.5): per ora vuoti.
            ValdorsoNetworkManager.RequestCreateCharacter(string.Empty, string.Empty, string.Empty, OnCreated);
        }

        void OnCreated(bool success, string message, string characterId)
        {
            if (this == null) return;
            if (!success)
            {
                busy = false;
                signButton.interactable = true;
                SetStatus(message);
                return;
            }
            StartCoroutine(EnterWorld(characterId));
        }

        IEnumerator EnterWorld(string characterId)
        {
            SetStatus("Il frammento batte più forte, per un istante. La valle ti ha sentito.");
            yield return new WaitForSecondsRealtime(1.5f);

            bool dark = ScreenFader.Instance == null;
            if (!dark) ScreenFader.Instance.FadeOut(0.6f, () => dark = true);
            while (!dark) yield return null;

            // Nel buio: si riaccende il mondo e si chiede di entrare.
            RestoreWorld();
            ValdorsoNetworkManager.RequestEnterWorld(characterId);

            float timeout = 10f;
            while (NetworkClient.localPlayer == null && NetworkClient.isConnected && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn();
            SceneManager.UnloadSceneAsync(gameObject.scene);
        }
    }
}
