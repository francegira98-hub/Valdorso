using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Valdorso.Network;
using Valdorso.Server;
using Valdorso.UI;

namespace Valdorso.Creation
{
    /// <summary>
    /// Il regista della scena di creazione: il Registro di Val d'Orso.
    /// Quando la scena si apre sopra il mondo, spegne telecamere e sole del mondo e mette l'atmosfera della notte.
    /// A sinistra c'è la pergamena del registro, a pagine: ogni pagina è una scelta (nome, corpo), l'ultima è la firma.
    /// Il personaggio sul palco cambia mentre si sceglie; alla firma il suo aspetto parte per il server come ricetta UMA.
    /// Dopo la firma riaccende il mondo, chiede di entrare e chiude la scena.
    /// Le pagine si costruiscono da codice con i colori e i caratteri del Tema UI "Oro e brace".
    /// </summary>
    public class CreationController : MonoBehaviour
    {
        [SerializeField] ValdorsoTheme theme;
        [SerializeField] Camera stageCamera;

        [Header("Nitidezza del personaggio (vale mentre la creazione è aperta)")]
        [Tooltip("Risoluzione delle texture che UMA disegna per il personaggio del registro (nel mondo resta quella normale)")]
        [SerializeField] int creationAtlasResolution = 4096;

        [Header("Atmosfera (vale mentre la creazione è aperta)")]
        [SerializeField] Color fogColor = new Color(0.035f, 0.04f, 0.055f);
        [SerializeField] float fogDensity = 0.14f;
        [SerializeField] Color ambientColor = new Color(0.05f, 0.055f, 0.07f);

        // Nomi suggeriti: nomi medievali italiani, tra cui Orso, come lo spirito della valle.
        static readonly string[] SuggestedNames =
        {
            "Aldo", "Bruno", "Corrado", "Duccio", "Ettore", "Folco", "Guido", "Lapo", "Orso", "Pietro",
            "Rinaldo", "Tancredi", "Ugolino", "Vanni", "Baldo", "Cecco", "Nuccio", "Gherardo",
            "Ada", "Bice", "Costanza", "Fiora", "Gemma", "Ilaria", "Lucia", "Mira", "Nives",
            "Selvaggia", "Tessa", "Viola", "Agnese", "Brunilde", "Oderisia", "Lapa"
        };

        // Razze e vestiti di partenza (Popolano appena arrivato nella valle). I nomi sono quelli delle ricette UMA.
        const string MaleRace = "Human Male 3.0";
        const string FemaleRace = "Human Female 3.0";
        static readonly string[] MaleClothes =
        {
            "male_underpants_granit_Recipe", "M_ChallengerTorsoArmor_Recipe", "M_Wrapped Pants_Recipe", "M_ChallengerBoots_Recipe"
        };
        static readonly string[] FemaleClothes =
        {
            "underwear_white_granit_bottom_Recipe", "underwear_white_granit_top_Recipe",
            "F_ChallengerTorsoArmor_Recipe", "F_Wrapped Pants_Recipe", "F_ChallengerBoots_Recipe"
        };

        // Le scelte del volto: (nome nel registro, ricetta UMA). Una ricetta vuota vuol dire "niente".
        static readonly (string label, string recipe)[] MaleHair =
        {
            ("corti", "bb_male_haircut_Recipe"), ("rasati da soldato", "bb_Male_Military_Hair_Recipe"),
            ("tirati indietro", "Hair_PulledBack_Recipe"), ("con la riga", "Hair_LeftPart_Recipe"),
            ("scompigliati", "Hair_MessyRightPart_Recipe"), ("ciuffo ribelle", "Hair_MessyPomp_Recipe"),
            ("lisci", "Hair_Straight_Recipe"), ("lunghi all'indietro", "Hair_LongSwept_Recipe"),
            ("a punte", "Hair_Pointy_Recipe"), ("nessuno", "")
        };
        static readonly (string label, string recipe)[] FemaleHair =
        {
            ("sciolti", "bb_female_hair_Recipe"), ("lisci", "Hair_Straight_Recipe"), ("a caschetto", "Hair_Bob_Recipe"),
            ("raccolti", "Hair_Bun_Recipe"), ("crocchia alta", "Hair_UpwardBun_Recipe"), ("coda", "HairPonytail_Recipe"),
            ("due trecce", "HairPigtails_Recipe"), ("tirati indietro", "Hair_StraigntPulledBack_Recipe"),
            ("lunghi all'indietro", "Hair_LongSwept_Recipe"), ("arricciati sotto", "Hair_CurveUnder_Recipe")
        };
        static readonly (string label, string recipe)[] Beards =
        {
            ("corta", "Beard_Trimmed"), ("folta", "Beard_Trimmed_Bushy"), ("pizzetto", "Beard_Goatee"),
            ("a cerchio", "Beard_Circle"), ("da viandante", "Beard_Drifter"), ("da pistolero", "Beard_Gunslinger"),
            ("a tenda", "Beard_Curtain"), ("sotto il mento", "Beard_Neckbeard"), ("baffi pieni", "Mustache_Full"),
            ("baffi a ferro di cavallo", "Mustache_Horseshoe"), ("baffi a spazzola", "Mustache_Chevron"), ("rasato", "")
        };
        static readonly (string label, string recipe)[] Eyebrows =
        {
            ("normali", "Eyebrows_Average_Average"), ("arcuate", "Eyebrows_Arched_Average"), ("sottili", "Eyebrows_Thin_Average"),
            ("folte", "Eyebrows_Bushy_Average"), ("alzate in fondo", "Eyebrows_EndArch_Average"), ("foltissime", "Eyebrows_Bushy_Bushy"),
            ("unite", "Eyebrows_Unibrow_Average")
        };
        static readonly (string label, string recipe)[] FaceMarks =
        {
            ("nessuno", ""), ("lentiggini", "Freckled_Light_U_Wardrobe"), ("molte lentiggini", "Freckled_More_U_Wardrobe"),
            ("rughe", "Aged_U_Wardrobe"), ("rughe profonde", "Aged_U2_Wardrobe"), ("anziano", "Senior_U_Wardrobe"),
            ("cicatrice", "Scar3_U_Wardrobe"), ("cicatrice profonda", "Scar4_U_Wardrobe")
        };

        // I colori (nomi dei colori condivisi di UMA: Skin, Hair, Eyes). Bianco = il colore naturale della texture.
        static readonly (string label, Color color)[] SkinTones =
        {
            ("naturale", Color.white), ("chiarissima", new Color(1f, 0.93f, 0.88f)), ("chiara", new Color(0.96f, 0.85f, 0.76f)),
            ("ambrata", new Color(0.88f, 0.74f, 0.62f)), ("olivastra", new Color(0.78f, 0.66f, 0.52f)),
            ("bruna", new Color(0.64f, 0.5f, 0.38f)), ("scura", new Color(0.46f, 0.34f, 0.25f))
        };
        // Più scuri e saturi di come appaiono: la texture dei capelli è chiara e la luce li schiarisce ancora.
        static readonly (string label, Color color)[] HairColors =
        {
            ("neri", new Color(0.02f, 0.018f, 0.016f)), ("castano scuro", new Color(0.09f, 0.05f, 0.028f)),
            ("castani", new Color(0.19f, 0.1f, 0.045f)), ("ramati", new Color(0.4f, 0.13f, 0.04f)),
            ("biondo scuro", new Color(0.46f, 0.32f, 0.15f)), ("biondi", new Color(0.78f, 0.6f, 0.32f)),
            ("grigi", new Color(0.42f, 0.41f, 0.4f)), ("bianchi", new Color(0.92f, 0.91f, 0.89f))
        };
        static readonly (string label, Color color)[] EyeColors =
        {
            ("marroni", new Color(0.35f, 0.22f, 0.12f)), ("nocciola", new Color(0.5f, 0.38f, 0.18f)),
            ("verdi", new Color(0.3f, 0.48f, 0.3f)), ("azzurri", new Color(0.35f, 0.52f, 0.75f)),
            ("grigi", new Color(0.55f, 0.58f, 0.62f)), ("ambra", new Color(0.7f, 0.5f, 0.15f))
        };

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

        // Il registro
        readonly List<GameObject> pages = new List<GameObject>();
        int currentPage;
        TMP_Text pageCounter;
        Button backButton, nextButton, signButton;
        TMP_InputField nameField;
        TMP_Text nameError, summaryText, statusText;
        bool busy;

        // Il corpo
        DynamicCharacterAvatar preview;
        bool female;
        Slider heightSlider, buildSlider;
        Button maleButton, femaleButton;
        float rebuildAt = -1f;

        // Il volto
        int hairIndex, beardIndex, browIndex, markIndex;
        int skinIndex, hairColorIndex = 1, beardColorIndex = 1, eyeIndex;
        GameObject beardColorRow;
        TMP_Text hairValue, beardValue, browValue, markValue;
        GameObject beardRow;
        readonly List<Image> skinMarks = new List<Image>(), hairColorMarks = new List<Image>(), beardColorMarks = new List<Image>(), eyeMarks = new List<Image>();

        // La telecamera: a figura intera, oppure vicina al viso nella pagina del volto.
        Vector3 fullShotPosition;
        Quaternion fullShotRotation;
        bool faceShot;

        // La qualità di UMA prima della creazione, per rimetterla uguale.
        readonly Dictionary<UMAGeneratorBase, (int atlas, int scale)> savedQuality = new Dictionary<UMAGeneratorBase, (int atlas, int scale)>();

        Color Ink => theme != null ? theme.ink : new Color(0.17f, 0.11f, 0.07f);
        Color Blood => theme != null ? theme.blood : new Color(0.56f, 0.17f, 0.15f);
        Color GoldDark => theme != null ? theme.goldDark : new Color(0.54f, 0.42f, 0.11f);

        void Start()
        {
            if (stageCamera == null) stageCamera = GetComponentInChildren<Camera>(true);
            preview = GetComponentInChildren<DynamicCharacterAvatar>(true);
            // Il colore dei capelli arriva sui capelli (vedi UmaHairTint).
            if (preview != null && preview.GetComponent<Valdorso.Creatures.UmaHairTint>() == null)
                preview.gameObject.AddComponent<Valdorso.Creatures.UmaHairTint>();
            if (stageCamera != null)
            {
                fullShotPosition = stageCamera.transform.position;
                fullShotRotation = stageCamera.transform.rotation;
            }
            if (theme == null) Debug.LogWarning("[Valdorso] Nel Creation Controller manca il Tema UI: il registro userà colori e caratteri di riserva.");
            HideWorld();
            EnsureEventSystem();
            BuildRegister();
            ShowPage(0);
            ScheduleLook(0f);
        }

        void LateUpdate()
        {
            if (stageCamera == null) return;
            Vector3 targetPosition = fullShotPosition;
            Quaternion targetRotation = fullShotRotation;
            if (faceShot && preview != null)
            {
                // Il viso a destra dello schermo, a poco più di un metro: si vedono bene lineamenti, capelli e colori.
                Vector3 head = HeadPosition();
                Vector3 look = head + new Vector3(0.3f, -0.03f, 0f);
                targetPosition = look + new Vector3(0f, 0.04f, 1.2f);
                targetRotation = Quaternion.LookRotation(look - targetPosition, Vector3.up);
            }
            float t = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            stageCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(stageCamera.transform.position, targetPosition, t),
                Quaternion.Slerp(stageCamera.transform.rotation, targetRotation, t));
        }

        Vector3 HeadPosition()
        {
            Animator animator = preview.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null) return head.position + Vector3.up * 0.06f;
            }
            return preview.transform.position + Vector3.up * 1.62f;
        }

        // Più nitidezza nel registro: UMA disegna le texture del personaggio a piena risoluzione.
        void RaiseUmaQuality()
        {
            foreach (UMAGeneratorBase generator in FindObjectsByType<UMAGeneratorBase>(FindObjectsSortMode.None))
            {
                if (savedQuality.ContainsKey(generator)) continue;
                var builtin = generator as UMAGeneratorBuiltin;
                savedQuality[generator] = (generator.atlasResolution, builtin != null ? builtin.InitialScaleFactor : 1);
                Debug.Log($"[Valdorso] Qualità UMA: atlante {generator.atlasResolution}" +
                          (builtin != null ? $", riduzione iniziale {builtin.InitialScaleFactor}" : string.Empty) +
                          $" → nel registro atlante {Mathf.Max(generator.atlasResolution, creationAtlasResolution)}, riduzione 1.");
                generator.atlasResolution = Mathf.Max(generator.atlasResolution, creationAtlasResolution);
                if (builtin != null) builtin.InitialScaleFactor = 1;
            }
        }

        void RestoreUmaQuality()
        {
            foreach (var pair in savedQuality)
            {
                if (pair.Key == null) continue;
                pair.Key.atlasResolution = pair.Value.atlas;
                if (pair.Key is UMAGeneratorBuiltin builtin) builtin.InitialScaleFactor = pair.Value.scale;
            }
            savedQuality.Clear();
        }

        void Update()
        {
            // Il personaggio si ricostruisce poco dopo l'ultima modifica, non a ogni scatto del cursore.
            if (rebuildAt > 0f && Time.unscaledTime >= rebuildAt)
            {
                rebuildAt = -1f;
                ApplyLook();
            }
        }

        void OnDestroy()
        {
            RestoreWorld();
        }

        // =====================================================================
        // Mondo e atmosfera
        // =====================================================================

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
            RestoreUmaQuality();
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

        // =====================================================================
        // Il registro: la pergamena e le sue pagine
        // =====================================================================

        void BuildRegister()
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

            // La pergamena, con la cornice dorata.
            RectTransform sheet = Box(canvasGO.transform, "Pergamena", new Vector2(90f, -80f), new Vector2(700f, 920f));
            var sheetImage = sheet.gameObject.AddComponent<Image>();
            sheetImage.sprite = theme != null ? theme.parchmentTexture : null;
            sheetImage.color = theme != null ? theme.parchment : new Color(0.85f, 0.77f, 0.6f);
            var frame = new GameObject("Cornice", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(sheet, false);
            Stretch((RectTransform)frame.transform, -8f, -8f, -8f, -8f);
            var frameImage = frame.GetComponent<Image>();
            frameImage.sprite = theme != null ? theme.frame : null;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = theme != null ? theme.gold : new Color(0.83f, 0.69f, 0.22f);
            frameImage.raycastTarget = false;
            if (theme == null || theme.frame == null) frame.SetActive(false);

            // Intestazione del registro.
            Label(sheet, "IL REGISTRO DI VAL D'ORSO", TitleFont, 38f, Ink, new Vector2(0f, -44f), new Vector2(700f, 54f), TextAlignmentOptions.Center);
            Label(sheet, "Anno 312 dopo il Crepuscolo", ItalicFont, 24f, Fade(Ink, 0.75f), new Vector2(0f, -98f), new Vector2(700f, 34f), TextAlignmentOptions.Center);
            if (theme != null && theme.divider != null)
            {
                RectTransform divider = Box(sheet, "Separatore", new Vector2(90f, -136f), new Vector2(520f, 26f));
                var d = divider.gameObject.AddComponent<Image>();
                d.sprite = theme.divider;
                d.color = GoldDark;
                d.raycastTarget = false;
            }

            // Le pagine: ognuna è una scelta; l'ultima è la firma.
            pages.Add(BuildNamePage(sheet));
            pages.Add(BuildBodyPage(sheet));
            pages.Add(BuildFacePage(sheet));
            pages.Add(BuildSignPage(sheet));

            // In fondo: avanti, indietro e numero di pagina.
            backButton = MakeButton(sheet, "‹  INDIETRO", new Vector2(50f, -832f), new Vector2(200f, 52f), 22f);
            backButton.onClick.AddListener(() => ShowPage(currentPage - 1));
            nextButton = MakeButton(sheet, "AVANTI  ›", new Vector2(450f, -832f), new Vector2(200f, 52f), 22f);
            nextButton.onClick.AddListener(Next);
            pageCounter = Label(sheet, string.Empty, ItalicFont, 22f, Fade(Ink, 0.7f), new Vector2(250f, -844f), new Vector2(200f, 30f), TextAlignmentOptions.Center);
        }

        GameObject BuildNamePage(RectTransform sheet)
        {
            RectTransform page = Box(sheet, "Pagina_Nome", new Vector2(0f, -170f), new Vector2(700f, 640f));
            Label(page, "I  ·  IL NOME", ButtonFont, 30f, Ink, new Vector2(60f, -10f), new Vector2(580f, 44f), TextAlignmentOptions.Left);
            Label(page, "Prima del mondo c'era il Silenzio. Poi venne il primo battito, e ogni cosa ebbe un nome.",
                ItalicFont, 25f, Fade(Ink, 0.85f), new Vector2(60f, -64f), new Vector2(580f, 90f), TextAlignmentOptions.TopLeft);

            Label(page, "Come ti chiameranno nella valle?", TextFont, 27f, Ink, new Vector2(60f, -190f), new Vector2(580f, 40f), TextAlignmentOptions.Left);
            nameField = MakeInputField(page, new Vector2(60f, -240f), new Vector2(580f, 62f));
            nameField.onValueChanged.AddListener(_ => ValidateName());
            nameField.onSubmit.AddListener(_ => Next());

            Button suggest = MakeButton(page, "SUGGERISCI UN NOME", new Vector2(60f, -322f), new Vector2(300f, 48f), 20f);
            suggest.onClick.AddListener(SuggestName);

            Label(page, "Da 2 a 24 lettere; spazi, apostrofi e trattini sono ammessi. Nella valle non ci sono due persone con lo stesso nome.",
                ItalicFont, 21f, Fade(Ink, 0.7f), new Vector2(60f, -392f), new Vector2(580f, 70f), TextAlignmentOptions.TopLeft);
            nameError = Label(page, string.Empty, TextFont, 24f, Blood, new Vector2(60f, -470f), new Vector2(580f, 70f), TextAlignmentOptions.TopLeft);
            nameError.fontStyle = FontStyles.Bold; // un errore deve farsi notare anche sulla pergamena
            return page.gameObject;
        }

        GameObject BuildSignPage(RectTransform sheet)
        {
            RectTransform page = Box(sheet, "Pagina_Firma", new Vector2(0f, -170f), new Vector2(700f, 640f));
            Label(page, "IV  ·  LA FIRMA", ButtonFont, 30f, Ink, new Vector2(60f, -10f), new Vector2(580f, 44f), TextAlignmentOptions.Left);
            Label(page, "La corona di Aurelia concede terra e protezione a chi ha il coraggio di restare nella valle.",
                ItalicFont, 25f, Fade(Ink, 0.85f), new Vector2(60f, -64f), new Vector2(580f, 90f), TextAlignmentOptions.TopLeft);

            summaryText = Label(page, string.Empty, TextFont, 25f, Ink, new Vector2(60f, -170f), new Vector2(580f, 220f), TextAlignmentOptions.TopLeft);
            summaryText.lineSpacing = 4f;

            signButton = MakeButton(page, "FIRMA IL REGISTRO", new Vector2(140f, -410f), new Vector2(420f, 64f), 26f);
            signButton.onClick.AddListener(OnSign);
            statusText = Label(page, string.Empty, ItalicFont, 23f, Fade(Ink, 0.85f), new Vector2(60f, -500f), new Vector2(580f, 110f), TextAlignmentOptions.Top);
            return page.gameObject;
        }

        GameObject BuildBodyPage(RectTransform sheet)
        {
            RectTransform page = Box(sheet, "Pagina_Corpo", new Vector2(0f, -170f), new Vector2(700f, 640f));
            Label(page, "II  ·  IL CORPO", ButtonFont, 30f, Ink, new Vector2(60f, -10f), new Vector2(580f, 44f), TextAlignmentOptions.Left);
            Label(page, "Per ultimi vennero gli uomini, fragili e brevi, ma gli unici capaci di scegliere chi diventare.",
                ItalicFont, 25f, Fade(Ink, 0.85f), new Vector2(60f, -64f), new Vector2(580f, 90f), TextAlignmentOptions.TopLeft);

            Label(page, "Chi si presenta al sacerdote?", TextFont, 27f, Ink, new Vector2(60f, -180f), new Vector2(580f, 40f), TextAlignmentOptions.Left);
            maleButton = MakeButton(page, "UOMO", new Vector2(60f, -230f), new Vector2(280f, 56f), 24f);
            maleButton.onClick.AddListener(() => SetFemale(false));
            femaleButton = MakeButton(page, "DONNA", new Vector2(360f, -230f), new Vector2(280f, 56f), 24f);
            femaleButton.onClick.AddListener(() => SetFemale(true));

            Label(page, "Altezza", TextFont, 27f, Ink, new Vector2(60f, -326f), new Vector2(580f, 40f), TextAlignmentOptions.Left);
            heightSlider = MakeSlider(page, new Vector2(60f, -372f), 580f, "bassa", "alta");
            Label(page, "Corporatura", TextFont, 27f, Ink, new Vector2(60f, -446f), new Vector2(580f, 40f), TextAlignmentOptions.Left);
            buildSlider = MakeSlider(page, new Vector2(60f, -492f), 580f, "esile", "robusta");
            heightSlider.onValueChanged.AddListener(_ => ScheduleLook(0.2f));
            buildSlider.onValueChanged.AddListener(_ => ScheduleLook(0.2f));

            Label(page, "Trascina il personaggio con il mouse per girarlo.", ItalicFont, 21f, Fade(Ink, 0.7f),
                new Vector2(60f, -570f), new Vector2(580f, 40f), TextAlignmentOptions.TopLeft);
            RefreshSexButtons();
            return page.gameObject;
        }

        GameObject BuildFacePage(RectTransform sheet)
        {
            RectTransform page = Box(sheet, "Pagina_Volto", new Vector2(0f, -170f), new Vector2(700f, 640f));
            Label(page, "III  ·  IL VOLTO", ButtonFont, 30f, Ink, new Vector2(60f, -10f), new Vector2(580f, 44f), TextAlignmentOptions.Left);
            Label(page, "Gli dei plasmarono le terre e diedero vita ai popoli: ogni volto porta il segno di chi lo ha fatto.",
                ItalicFont, 25f, Fade(Ink, 0.85f), new Vector2(60f, -64f), new Vector2(580f, 90f), TextAlignmentOptions.TopLeft);

            float y = -165f;
            hairValue = SelectorRow(page, "Capelli", y, d => { hairIndex = Step(hairIndex, d, (female ? FemaleHair : MaleHair).Length); });
            y -= 58f;
            beardValue = SelectorRow(page, "Barba", y, d => { beardIndex = Step(beardIndex, d, Beards.Length); });
            beardRow = beardValue.transform.parent.gameObject;
            y -= 58f;
            browValue = SelectorRow(page, "Sopracciglia", y, d => { browIndex = Step(browIndex, d, Eyebrows.Length); });
            y -= 58f;
            markValue = SelectorRow(page, "Segni del viso", y, d => { markIndex = Step(markIndex, d, FaceMarks.Length); });
            y -= 70f;

            SwatchRow(page, "Carnagione", y, SkinTones, skinMarks, i => skinIndex = i);
            y -= 56f;
            SwatchRow(page, "Colore dei capelli", y, HairColors, hairColorMarks, i => hairColorIndex = i);
            y -= 56f;
            beardColorRow = SwatchRow(page, "Barba e sopracciglia", y, HairColors, beardColorMarks, i => beardColorIndex = i);
            y -= 56f;
            SwatchRow(page, "Occhi", y, EyeColors, eyeMarks, i => eyeIndex = i);

            RefreshFaceValues();
            return page.gameObject;
        }

        static int Step(int index, int delta, int count) => ((index + delta) % count + count) % count;

        // Una riga "Nome   ‹  valore  ›": le frecce scorrono le scelte.
        TMP_Text SelectorRow(Transform page, string title, float y, System.Action<int> change)
        {
            RectTransform row = Box(page, "Riga_" + title, new Vector2(60f, y), new Vector2(580f, 48f));
            Label(row, title, TextFont, 25f, Ink, new Vector2(0f, -6f), new Vector2(200f, 40f), TextAlignmentOptions.Left);
            Button prev = MakeButton(row, "‹", new Vector2(210f, 0f), new Vector2(48f, 44f), 26f);
            TMP_Text value = Label(row, string.Empty, ItalicFont, 25f, Ink, new Vector2(262f, -6f), new Vector2(262f, 40f), TextAlignmentOptions.Center);
            Button next = MakeButton(row, "›", new Vector2(528f, 0f), new Vector2(48f, 44f), 26f);
            prev.onClick.AddListener(() => { if (busy) return; change(-1); RefreshFaceValues(); ScheduleLook(0.05f); });
            next.onClick.AddListener(() => { if (busy) return; change(1); RefreshFaceValues(); ScheduleLook(0.05f); });
            return value;
        }

        // Una riga di quadratini di colore; quello scelto ha la cornice d'oro.
        GameObject SwatchRow(Transform page, string title, float y, (string label, Color color)[] palette, List<Image> marks, System.Action<int> pick)
        {
            RectTransform row = Box(page, "Colori_" + title, new Vector2(0f, y), new Vector2(700f, 44f));
            TMP_Text rowTitle = Label(row, title, TextFont, 24f, Ink, new Vector2(60f, -6f), new Vector2(205f, 40f), TextAlignmentOptions.Left);
            rowTitle.textWrappingMode = TextWrappingModes.NoWrap;
            rowTitle.enableAutoSizing = true; // i titoli lunghi si rimpiccioliscono invece di andare a capo
            rowTitle.fontSizeMin = 16f;
            rowTitle.fontSizeMax = 24f;
            for (int i = 0; i < palette.Length; i++)
            {
                int index = i;
                RectTransform swatch = Box(row, "Colore_" + palette[i].label, new Vector2(270f + i * 46f, 0f), new Vector2(38f, 38f));
                var image = swatch.gameObject.AddComponent<Image>();
                image.color = palette[i].color == Color.white && title == "Carnagione" ? new Color(0.93f, 0.8f, 0.7f) : palette[i].color;
                var button = swatch.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => { if (busy) return; pick(index); RefreshFaceValues(); ScheduleLook(0.05f); });

                var markGO = new GameObject("Scelto", typeof(RectTransform), typeof(Image));
                markGO.transform.SetParent(swatch, false);
                Stretch((RectTransform)markGO.transform, -5f, -5f, -5f, -5f);
                var mark = markGO.GetComponent<Image>();
                mark.sprite = theme != null ? theme.buttonFrame : null;
                mark.type = Image.Type.Sliced;
                mark.color = theme != null ? theme.gold : new Color(0.83f, 0.69f, 0.22f);
                mark.raycastTarget = false;
                if (mark.sprite == null) mark.color = new Color(mark.color.r, mark.color.g, mark.color.b, 0.5f);
                marks.Add(mark);
            }
            return row.gameObject;
        }

        void RefreshFaceValues()
        {
            if (hairValue == null) return;
            (string label, string recipe)[] hairs = female ? FemaleHair : MaleHair;
            hairIndex %= hairs.Length;
            hairValue.text = hairs[hairIndex].label;
            beardValue.text = Beards[beardIndex].label;
            browValue.text = Eyebrows[browIndex].label;
            markValue.text = FaceMarks[markIndex].label;
            beardRow.SetActive(!female);
            if (beardColorRow != null) beardColorRow.SetActive(!female);
            for (int i = 0; i < beardColorMarks.Count; i++) beardColorMarks[i].enabled = i == beardColorIndex;
            for (int i = 0; i < skinMarks.Count; i++) skinMarks[i].enabled = i == skinIndex;
            for (int i = 0; i < hairColorMarks.Count; i++) hairColorMarks[i].enabled = i == hairColorIndex;
            for (int i = 0; i < eyeMarks.Count; i++) eyeMarks[i].enabled = i == eyeIndex;
        }

        // ---------- Il corpo sul palco ----------

        void SetFemale(bool value)
        {
            if (busy || female == value) return;
            female = value;
            hairIndex = 0;
            if (browIndex == 0 || browIndex == 1) browIndex = female ? 1 : 0; // arcuate per lei, normali per lui
            RefreshSexButtons();
            RefreshFaceValues();
            ScheduleLook(0f);
        }

        void RefreshSexButtons()
        {
            // La scelta attiva ha il fondo acceso, come un pulsante sotto il mouse.
            MarkSelected(maleButton, !female);
            MarkSelected(femaleButton, female);
        }

        void MarkSelected(Button button, bool selected)
        {
            if (button == null) return;
            ColorBlock colors = button.colors;
            Color normal = theme != null ? theme.button : new Color(0.11f, 0.09f, 0.07f);
            Color lit = theme != null ? theme.buttonPressed : new Color(0.35f, 0.27f, 0.19f);
            colors.normalColor = selected ? lit : normal;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            // E la scritta della scelta attiva in oro chiaro, così si vede subito.
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                Color idle = theme != null ? theme.text : Color.white;
                Color gold = theme != null ? theme.goldLight : new Color(0.95f, 0.86f, 0.54f);
                label.color = selected ? gold : idle;
            }
        }

        void ScheduleLook(float delay)
        {
            rebuildAt = Time.unscaledTime + Mathf.Max(0.01f, delay);
        }

        /// <summary>L'aspetto scelto nel registro, nella forma che UMA sa caricare e il server sa conservare.</summary>
        AvatarDefinition ChosenLook()
        {
            float h = heightSlider != null ? heightSlider.value : 0.5f;
            float b = buildSlider != null ? buildSlider.value : 0.5f;
            return new AvatarDefinition
            {
                RaceName = female ? FemaleRace : MaleRace,
                Wardrobe = ChosenWardrobe(),
                Colors = new SharedColorDef[0],
                Dna = new[]
                {
                    new DnaDef("height", Mathf.Lerp(0.36f, 0.64f, h)),
                    new DnaDef("upperWeight", Mathf.Lerp(0.3f, 0.7f, b)),
                    new DnaDef("lowerWeight", Mathf.Lerp(0.3f, 0.7f, b)),
                    new DnaDef("upperMuscle", Mathf.Lerp(0.38f, 0.66f, b)),
                    new DnaDef("lowerMuscle", Mathf.Lerp(0.38f, 0.62f, b)),
                    new DnaDef("belly", Mathf.Lerp(0.35f, 0.6f, b)),
                    new DnaDef("waist", Mathf.Lerp(0.4f, 0.6f, b))
                }
            };
        }

        string[] ChosenWardrobe()
        {
            var list = new List<string>(female ? FemaleClothes : MaleClothes);
            (string label, string recipe)[] hairs = female ? FemaleHair : MaleHair;
            AddIfAny(list, hairs[hairIndex % hairs.Length].recipe);
            if (!female) AddIfAny(list, Beards[beardIndex % Beards.Length].recipe);
            AddIfAny(list, Eyebrows[browIndex % Eyebrows.Length].recipe);
            AddIfAny(list, FaceMarks[markIndex % FaceMarks.Length].recipe);
            return list.ToArray();
        }

        static void AddIfAny(List<string> list, string recipe)
        {
            if (!string.IsNullOrEmpty(recipe)) list.Add(recipe);
        }

        void ApplyColors()
        {
            preview.SetColorValue("Skin", SkinTones[skinIndex].color);
            preview.SetColorValue("Hair", HairColors[hairColorIndex].color);
            preview.SetColorValue("Beard", HairColors[beardColorIndex].color);
            preview.SetColorValue("Eyes", EyeColors[eyeIndex].color);
        }

        /// <summary>La ricetta completa da mandare al server: razza, vestiti e misure del registro, più i colori scelti.</summary>
        string ChosenRecipe()
        {
            AvatarDefinition look = ChosenLook();
            if (preview != null)
            {
                ApplyColors();
                look.Colors = preview.GetAvatarDefinition(false, false).Colors;
            }
            return look.ToCompressedString();
        }

        void ApplyLook()
        {
            if (preview == null) return;
            RaiseUmaQuality();
            try
            {
                preview.LoadAvatarDefinition(ChosenLook());
                ApplyColors();
                preview.BuildCharacter(true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Valdorso] Il personaggio del registro non si è ricostruito: " + e.Message);
            }
        }

        static string Describe(float value, string low, string mid, string high) =>
            value < 0.34f ? low : value > 0.66f ? high : mid;

        // ---------- Navigazione ----------

        void ShowPage(int index)
        {
            if (busy) return;
            currentPage = Mathf.Clamp(index, 0, pages.Count - 1);
            for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == currentPage);
            faceShot = pages[currentPage].name == "Pagina_Volto";

            bool last = currentPage == pages.Count - 1;
            backButton.gameObject.SetActive(currentPage > 0);
            nextButton.gameObject.SetActive(!last);
            pageCounter.text = $"Pagina {currentPage + 1} di {pages.Count}";

            if (currentPage == 0 && nameField != null)
            {
                nameField.Select();
                nameField.ActivateInputField();
            }
            if (last) RefreshSummary();
            ValidateName();
        }

        void Next()
        {
            if (currentPage == 0 && !ValidateName()) return;
            ShowPage(currentPage + 1);
        }

        // ---------- Pagina del nome ----------

        string ChosenName => nameField != null ? nameField.text.Trim() : string.Empty;

        bool ValidateName()
        {
            string n = ChosenName;
            bool ok = CharacterStore.IsValidName(n, out string error);
            // L'errore si mostra solo quando c'è già qualcosa di scritto: un campo vuoto non è uno sbaglio.
            if (nameError != null) nameError.text = ok || n.Length == 0 ? string.Empty : error;
            if (nextButton != null && currentPage == 0) nextButton.interactable = ok;
            return ok;
        }

        void SuggestName()
        {
            string current = ChosenName;
            string pick = current;
            for (int i = 0; i < 10 && pick == current; i++)
                pick = SuggestedNames[Random.Range(0, SuggestedNames.Length)];
            nameField.text = pick;
            nameField.caretPosition = pick.Length;
        }

        // ---------- Pagina della firma ----------

        void RefreshSummary()
        {
            summaryText.text =
                $"Nome:  <b>{ChosenName}</b>\n" +
                $"{(female ? "Donna" : "Uomo")}, {Describe(heightSlider.value, "bassa statura", "statura media", "alta statura")}, " +
                $"{Describe(buildSlider.value, "corporatura esile", "corporatura media", "corporatura robusta")}\n" +
                $"Capelli {(female ? FemaleHair : MaleHair)[hairIndex].label}, {HairColors[hairColorIndex].label}; occhi {EyeColors[eyeIndex].label}\n" +
                "Origine:  Popolano\n" +
                "Razza:  Umano\n" +
                "Fede:  nessuna, per ora";
            statusText.text = string.Empty;
        }

        void OnSign()
        {
            if (busy) return;
            if (!NetworkClient.isConnected)
            {
                statusText.text = "Nessun server collegato: questa è solo un'anteprima della scena.";
                return;
            }
            busy = true;
            SetButtons(false);
            statusText.text = "Il sacerdote scrive il tuo nome nel registro...";
            // L'aspetto scelto parte come ricetta UMA; la fede arriverà con la sua pagina (passo 5.5).
            string recipe = ChosenRecipe();
            ValdorsoNetworkManager.RequestCreateCharacter(ChosenName, string.Empty, recipe, OnCreated);
        }

        void OnCreated(bool success, string message, string characterId)
        {
            if (this == null) return;
            if (!success)
            {
                // Di solito è il nome già preso: si torna alla pagina del nome con il motivo.
                busy = false;
                SetButtons(true);
                ShowPage(0);
                nameError.text = message;
                return;
            }
            StartCoroutine(EnterWorld(characterId));
        }

        void SetButtons(bool interactable)
        {
            signButton.interactable = interactable;
            backButton.interactable = interactable;
            nextButton.interactable = interactable;
            if (maleButton != null) maleButton.interactable = interactable;
            if (femaleButton != null) femaleButton.interactable = interactable;
        }

        IEnumerator EnterWorld(string characterId)
        {
            statusText.text = "Il frammento batte più forte, per un istante.\nLa valle ti ha sentito.";
            yield return new WaitForSecondsRealtime(2f);

            bool dark = ScreenFader.Instance == null;
            if (!dark) ScreenFader.Instance.FadeOut(0.8f, () => dark = true);
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

        // =====================================================================
        // Pezzi di interfaccia
        // =====================================================================

        TMP_FontAsset TitleFont => theme != null ? theme.titleFont : null;
        TMP_FontAsset ButtonFont => theme != null ? theme.buttonFont : null;
        TMP_FontAsset TextFont => theme != null ? theme.textFont : null;
        TMP_FontAsset ItalicFont => theme != null ? theme.italicFont : null;

        static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

        // Un riquadro con l'angolo in alto a sinistra in "topLeft" (in pixel della risoluzione di riferimento).
        static RectTransform Box(Transform parent, string name, Vector2 topLeft, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
            return rect;
        }

        static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static TMP_Text Label(Transform parent, string content, TMP_FontAsset font, float size, Color color, Vector2 topLeft, Vector2 box, TextAlignmentOptions align)
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

        Button MakeButton(Transform parent, string label, Vector2 topLeft, Vector2 size, float fontSize)
        {
            RectTransform rect = Box(parent, "Pulsante", topLeft, size);
            var fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = theme != null ? theme.buttonFill : null;
            fill.color = Color.white;

            var frameGO = new GameObject("Cornice", typeof(RectTransform), typeof(Image));
            frameGO.transform.SetParent(rect, false);
            Stretch((RectTransform)frameGO.transform, 0f, 0f, 0f, 0f);
            var frame = frameGO.GetComponent<Image>();
            frame.sprite = theme != null ? theme.buttonFrame : null;
            frame.type = Image.Type.Sliced;
            frame.color = theme != null ? theme.gold : new Color(0.83f, 0.69f, 0.22f);
            frame.raycastTarget = false;
            if (theme == null || theme.buttonFrame == null) frameGO.SetActive(false);

            TMP_Text text = Label(rect, label, ButtonFont, fontSize, theme != null ? theme.text : Color.white, Vector2.zero, size, TextAlignmentOptions.Center);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = theme != null ? theme.button : new Color(0.11f, 0.09f, 0.07f);
            colors.highlightedColor = theme != null ? theme.buttonHover : new Color(0.23f, 0.17f, 0.12f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = theme != null ? theme.buttonPressed : new Color(0.35f, 0.27f, 0.19f);
            colors.disabledColor = theme != null ? theme.buttonDisabled : new Color(0.11f, 0.09f, 0.07f, 0.5f);
            colors.fadeDuration = theme != null ? theme.fadeDuration : 0.12f;
            button.colors = colors;
            return button;
        }

        Slider MakeSlider(Transform parent, Vector2 topLeft, float width, string leftWord, string rightWord)
        {
            RectTransform rect = Box(parent, "Cursore", topLeft, new Vector2(width, 30f));
            rect.gameObject.SetActive(false); // si accende solo quando tutti i pezzi sono collegati

            // La linea del cursore
            RectTransform track = Box(rect, "Linea", new Vector2(0f, -12f), new Vector2(width, 6f));
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = Fade(Ink, 0.25f);

            RectTransform fillArea = Box(rect, "Area", new Vector2(0f, -12f), new Vector2(width, 6f));
            RectTransform fill = Box(fillArea, "Riempimento", Vector2.zero, Vector2.zero);
            Stretch(fill, 0f, 0f, 0f, 0f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = GoldDark;

            // La maniglia: un rombo d'oro, come quello del separatore.
            RectTransform handleArea = Box(rect, "Area_Maniglia", new Vector2(0f, 0f), new Vector2(width, 30f));
            RectTransform handle = Box(handleArea, "Maniglia", Vector2.zero, new Vector2(20f, 20f));
            handle.anchorMin = new Vector2(0f, 0.5f);
            handle.anchorMax = new Vector2(0f, 0.5f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = theme != null ? theme.gold : new Color(0.83f, 0.69f, 0.22f);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            ColorBlock colors = slider.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.8f);
            slider.colors = colors;

            Label(parent, leftWord, ItalicFont, 20f, Fade(Ink, 0.7f), topLeft + new Vector2(0f, -30f), new Vector2(200f, 28f), TextAlignmentOptions.Left);
            Label(parent, rightWord, ItalicFont, 20f, Fade(Ink, 0.7f), topLeft + new Vector2(width - 200f, -30f), new Vector2(200f, 28f), TextAlignmentOptions.Right);

            rect.gameObject.SetActive(true);
            return slider;
        }

        TMP_InputField MakeInputField(Transform parent, Vector2 topLeft, Vector2 size)
        {
            RectTransform rect = Box(parent, "Campo_Nome", topLeft, size);
            rect.gameObject.SetActive(false); // si accende solo quando tutti i pezzi sono collegati

            var background = rect.gameObject.AddComponent<Image>();
            background.color = Fade(Ink, 0.1f);

            var frameGO = new GameObject("Cornice", typeof(RectTransform), typeof(Image));
            frameGO.transform.SetParent(rect, false);
            Stretch((RectTransform)frameGO.transform, 0f, 0f, 0f, 0f);
            var frame = frameGO.GetComponent<Image>();
            frame.sprite = theme != null ? theme.buttonFrame : null;
            frame.type = Image.Type.Sliced;
            frame.color = GoldDark;
            frame.raycastTarget = false;
            if (theme == null || theme.buttonFrame == null) frameGO.SetActive(false);

            var area = new GameObject("Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(rect, false);
            var areaRect = (RectTransform)area.transform;
            Stretch(areaRect, 18f, 18f, 8f, 8f);

            TMP_Text placeholder = Label(areaRect, "Scrivi il tuo nome", ItalicFont, 30f, Fade(Ink, 0.45f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
            Stretch((RectTransform)placeholder.transform, 0f, 0f, 0f, 0f);
            TMP_Text text = Label(areaRect, string.Empty, TextFont, 32f, Ink, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
            Stretch((RectTransform)text.transform, 0f, 0f, 0f, 0f);

            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 24;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = Ink;
            input.customCaretColor = true;
            input.selectionColor = Fade(GoldDark, 0.45f);
            input.targetGraphic = background;
            if (TextFont != null) input.fontAsset = TextFont;
            input.pointSize = 32f;

            rect.gameObject.SetActive(true);
            return input;
        }
    }
}