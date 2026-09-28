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
using UnityEngine.Rendering.Universal;
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
    /// Chi ha già dei personaggi vede prima "I tuoi nomi nel registro": li sceglie (con ritratto e aspetto sul palco),
    /// entra con uno, ne scrive uno nuovo o ne cancella uno riscrivendone il nome.
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

        // I lineamenti, divisi in schede: (scheda, nome nel registro, misura del DNA di UMA).
        // Ogni cursore è una misura: a metà è il volto di base, agli estremi un tratto marcato.
        static readonly string[] FeatureTabs = { "Testa", "Occhi", "Naso", "Zigomi", "Bocca", "Mento", "Orecchie" };
        static readonly (string tab, string label, string dna)[] Features =
        {
            ("Testa", "Grandezza", "headSize"), ("Testa", "Larghezza", "headWidth"),
            ("Testa", "Fronte alta", "foreheadSize"), ("Testa", "Fronte sporgente", "foreheadPosition"),
            ("Testa", "Sopracciglia", "BrowPosition"), ("Testa", "Collo", "neckThickness"),
            ("Occhi", "Grandezza", "eyeSize"), ("Occhi", "Distanza", "eyeSpacing"), ("Occhi", "Taglio", "eyeRotation"),
            ("Naso", "Grandezza", "noseSize"), ("Naso", "Larghezza", "noseWidth"), ("Naso", "Profilo", "noseCurve"),
            ("Naso", "Punta", "noseInclination"), ("Naso", "Sporgenza", "nosePronounced"), ("Naso", "Schiacciato", "noseFlatten"),
            ("Naso", "Altezza", "nosePosition"), ("Naso", "Naso rotto", "noseBroken"),
            ("Zigomi", "Grandezza", "cheekSize"), ("Zigomi", "Sporgenza", "cheekPronounced"), ("Zigomi", "Altezza", "cheekPosition"),
            ("Zigomi", "Larghezza", "cheekWidth"), ("Zigomi", "Guance", "lowCheekPronounced"), ("Zigomi", "Altezza guance", "lowCheekPosition"),
            ("Bocca", "Grandezza", "mouthSize"), ("Bocca", "Labbra", "lipsSize"),
            ("Mento", "Mascella", "jawsSize"), ("Mento", "Mascella avanti", "jawsPosition"), ("Mento", "Mandibola", "mandibleSize"),
            ("Mento", "Mento", "chinSize"), ("Mento", "Mento sporgente", "chinPronounced"), ("Mento", "Altezza del mento", "chinPosition"),
            ("Orecchie", "Grandezza", "earsSize"), ("Orecchie", "Altezza", "earsPosition"), ("Orecchie", "Inclinazione", "earsRotation"),
            ("Orecchie", "Apertura", "earsYaw"), ("Orecchie", "Pendenza", "earsPitch")
        };

        // Le fedi che un colono può dichiarare al sacerdote (la chiave va al server, che accetta solo queste).
        // Gli dei oscuri non compaiono: nessuno li pronuncia davanti al sacerdote.
        static readonly (string key, string label, string text)[] Faiths =
        {
            ("Solara", "Solara", "La prima luce: giustizia e guarigione. È la fede della corona di Aurelia; la seguono guaritori, cavalieri e inquisitori."),
            ("Ignar", "Ignar", "Il primo fuoco, che forgia e distrugge. Lo pregano fabbri e soldati prima del lavoro e della battaglia."),
            ("Nereia", "Nereia", "La prima acqua, che viaggia e ritorna. Pescatori, marinai e mercanti le affidano ogni partenza."),
            ("Torvald", "Torvald", "La prima terra, paziente, che nutre e custodisce. Contadini, minatori e artigiani gli offrono il primo raccolto."),
            ("Zefira", "Zefira", "Il primo vento, libero, che porta le voci da un capo all'altro del mondo. La amano viaggiatori, corrieri e bardi."),
            ("Vecchi Dei", "Vecchi Dei", "Gli spiriti della foresta, più antichi di ogni nome. Il più grande era l'Orso, che morì difendendo questa valle."),
            ("", "Nessuna fede", "Il sacerdote scrive una sola parola: nessuna. Nella valle non è un crimine, ma qualcuno lo noterà.")
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
        readonly List<Slider> featureSliders = new List<Slider>();          // uno per riga di Features
        readonly Dictionary<string, GameObject> featureTabPages = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Button> featureTabButtons = new Dictionary<string, Button>();
        string currentFeatureTab = "Testa";
        bool dnaChecked;
        int faithIndex = 6; // nessuna fede, finché non si sceglie
        readonly List<Button> faithButtons = new List<Button>();
        TMP_Text faithText;

        // Il ritratto: lo fa una piccola telecamera "da ritrattista" quando si arriva alla firma.
        RawImage portraitImage;
        Texture2D portraitTexture;
        byte[] portraitBytes;
        Coroutine portraitRoutine;
        const int PortraitWidth = 320, PortraitHeight = 400;

        // La scelta del personaggio ("I tuoi nomi nel registro")
        GameObject selectionPage, confirmPanel;
        RectTransform selectionRows;
        Button enterButton, newCharacterButton, deleteButton, confirmDeleteButton;
        TMP_Text selectionStatus, selectionCount, confirmText;
        TMP_InputField confirmField;
        readonly List<(CharacterSummary data, Button row, Texture2D portrait)> entries = new List<(CharacterSummary, Button, Texture2D)>();
        int selectedEntry = -1;
        bool selectionMode;

        bool HasCharacters => ValdorsoNetworkManager.LastCharacterList is CharacterListResponse list &&
                              list.characters != null && list.characters.Length > 0;
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
                // Un passo indietro rispetto alla scena: anche i personaggi più alti restano interi nell'inquadratura.
                stageCamera.transform.position -= stageCamera.transform.forward * 0.6f;
                fullShotPosition = stageCamera.transform.position;
                fullShotRotation = stageCamera.transform.rotation;
            }
            if (theme == null) Debug.LogWarning("[Valdorso] Nel Creation Controller manca il Tema UI: il registro userà colori e caratteri di riserva.");
            HideWorld();
            EnsureEventSystem();
            BuildRegister();
            ValdorsoNetworkManager.CharacterListUpdated += OnCharacterListUpdated;
            if (HasCharacters) ShowSelection(ValdorsoNetworkManager.LastCharacterList.Value);
            else ShowCreation();
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
            ValdorsoNetworkManager.CharacterListUpdated -= OnCharacterListUpdated;
            if (portraitTexture != null) Destroy(portraitTexture);
            ClearEntries();
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
            pages.Add(BuildFeaturesPage(sheet));
            pages.Add(BuildFaithPage(sheet));
            pages.Add(BuildSignPage(sheet));
            BuildSelectionPage(sheet);
            BuildConfirmPanel(sheet);

            // In fondo: avanti, indietro e numero di pagina.
            backButton = MakeButton(sheet, "‹  INDIETRO", new Vector2(50f, -832f), new Vector2(200f, 52f), 22f);
            backButton.onClick.AddListener(GoBack);
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
            Label(page, "VI  ·  LA FIRMA", ButtonFont, 30f, Ink, new Vector2(60f, -10f), new Vector2(580f, 44f), TextAlignmentOptions.Left);
            Label(page, "La corona di Aurelia concede terra e protezione a chi ha il coraggio di restare nella valle.",
                ItalicFont, 25f, Fade(Ink, 0.85f), new Vector2(60f, -64f), new Vector2(580f, 90f), TextAlignmentOptions.TopLeft);

            summaryText = Label(page, string.Empty, TextFont, 22f, Ink, new Vector2(60f, -170f), new Vector2(380f, 230f), TextAlignmentOptions.TopLeft);
            summaryText.lineSpacing = 4f;

            // Il ritratto del sacerdote, in una cornice d'oro accanto al riepilogo.
            RectTransform portraitBox = Box(page, "Ritratto", new Vector2(460f, -165f), new Vector2(176f, 220f));
            var portraitBack = portraitBox.gameObject.AddComponent<Image>();
            portraitBack.color = Fade(Ink, 0.25f);
            portraitBack.raycastTarget = false;
            RectTransform picture = Box(portraitBox, "Immagine", Vector2.zero, Vector2.zero);
            Stretch(picture, 4f, 4f, 4f, 4f);
            portraitImage = picture.gameObject.AddComponent<RawImage>();
            portraitImage.raycastTarget = false;
            portraitImage.color = new Color(1f, 1f, 1f, 0f); // invisibile finché il ritratto non è pronto
            var portraitFrameGO = new GameObject("Cornice", typeof(RectTransform), typeof(Image));
            portraitFrameGO.transform.SetParent(portraitBox, false);
            Stretch((RectTransform)portraitFrameGO.transform, -3f, -3f, -3f, -3f);
            var portraitFrame = portraitFrameGO.GetComponent<Image>();
            portraitFrame.sprite = theme != null ? theme.buttonFrame : null;
            portraitFrame.type = Image.Type.Sliced;
            portraitFrame.color = GoldDark;
            portraitFrame.raycastTarget = false;
            if (portraitFrame.sprite == null) portraitFrameGO.SetActive(false);

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

        GameObject BuildFeaturesPage(RectTransform sheet)
        {
            RectTransform page = Box(sheet, "Pagina_Lineamenti", new Vector2(0f, -170f), new Vector2(700f, 640f));
            Label(page, "IV  ·  I LINEAMENTI", ButtonFont, 30f, Ink, new Vector2(60f, -10f), new Vector2(580f, 44f), TextAlignmentOptions.Left);
            Label(page, "Non ci sono due volti uguali nella valle: anche i gemelli, gli anziani li distinguono.",
                ItalicFont, 23f, Fade(Ink, 0.85f), new Vector2(60f, -58f), new Vector2(580f, 70f), TextAlignmentOptions.TopLeft);

            // Le schede: Testa, Occhi, Naso...
            float tabWidth = 580f / FeatureTabs.Length;
            for (int t = 0; t < FeatureTabs.Length; t++)
            {
                string tab = FeatureTabs[t];
                Button tabButton = MakeButton(page, tab.ToUpperInvariant(), new Vector2(60f + t * tabWidth, -128f), new Vector2(tabWidth - 4f, 38f), 14f);
                tabButton.onClick.AddListener(() => ShowFeatureTab(tab));
                featureTabButtons[tab] = tabButton;

                RectTransform tabPage = Box(page, "Scheda_" + tab, new Vector2(0f, -180f), new Vector2(700f, 400f));
                featureTabPages[tab] = tabPage.gameObject;
            }

            // Un cursore per misura, nella sua scheda.
            var rowsPerTab = new Dictionary<string, int>();
            foreach (var feature in Features)
            {
                rowsPerTab.TryGetValue(feature.tab, out int row);
                rowsPerTab[feature.tab] = row + 1;
                Transform tabPage = featureTabPages[feature.tab].transform;
                float y = -row * 48f;
                TMP_Text title = Label(tabPage, feature.label, TextFont, 23f, Ink, new Vector2(60f, y - 2f), new Vector2(210f, 36f), TextAlignmentOptions.Left);
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.enableAutoSizing = true;
                title.fontSizeMin = 15f;
                title.fontSizeMax = 23f;
                Slider slider = MakeSlider(tabPage, new Vector2(290f, y), 350f, null, null);
                slider.minValue = 0.1f;
                slider.maxValue = 0.9f;
                slider.SetValueWithoutNotify(0.5f);
                slider.onValueChanged.AddListener(_ => ScheduleLook(0.2f));
                featureSliders.Add(slider);
            }

            Button random = MakeButton(page, "UN VOLTO A CASO", new Vector2(60f, -590f), new Vector2(290f, 46f), 20f);
            random.onClick.AddListener(RandomFace);
            Button reset = MakeButton(page, "VOLTO DI BASE", new Vector2(370f, -590f), new Vector2(270f, 46f), 20f);
            reset.onClick.AddListener(() => { foreach (Slider s in featureSliders) s.SetValueWithoutNotify(0.5f); ScheduleLook(0.05f); });

            ShowFeatureTab(currentFeatureTab);
            return page.gameObject;
        }

        void ShowFeatureTab(string tab)
        {
            currentFeatureTab = tab;
            foreach (var pair in featureTabPages) pair.Value.SetActive(pair.Key == tab);
            foreach (var pair in featureTabButtons) MarkSelected(pair.Value, pair.Key == tab);
        }

        void RandomFace()
        {
            if (busy) return;
            // La media di due tiri: quasi sempre vicino al centro, ogni tanto un tratto deciso.
            foreach (Slider slider in featureSliders)
                slider.SetValueWithoutNotify(Mathf.Lerp(0.15f, 0.85f, (Random.value + Random.value) * 0.5f));
            ScheduleLook(0.05f);
        }

        // Una volta sola: avvisa in Console se questa versione di UMA non conosce qualcuna delle misure usate.
        void CheckDnaNames()
        {
            if (dnaChecked || preview == null) return;
            dnaChecked = true;
            var known = preview.GetDNA();
            var missing = new List<string>();
            foreach (var feature in Features)
                if (!known.ContainsKey(feature.dna)) missing.Add(feature.dna);
            if (missing.Count > 0)
                Debug.LogWarning($"[Valdorso] Misure del volto che UMA non conosce per {preview.activeRace.name}: {string.Join(", ", missing)}.");
            else
                Debug.Log($"[Valdorso] Tutte le {Features.Length} misure dei lineamenti sono riconosciute da UMA.");
        }

        GameObject BuildFaithPage(RectTransform sheet)
        {
            RectTransform page = Box(sheet, "Pagina_Fede", new Vector2(0f, -170f), new Vector2(700f, 640f));
            Label(page, "V  ·  LA FEDE", ButtonFont, 30f, Ink, new Vector2(60f, -10f), new Vector2(580f, 44f), TextAlignmentOptions.Left);
            Label(page, "Ogni dio lasciò ai popoli una parte del suo potere. A chi rivolgi le tue preghiere?",
                ItalicFont, 25f, Fade(Ink, 0.85f), new Vector2(60f, -64f), new Vector2(580f, 90f), TextAlignmentOptions.TopLeft);

            for (int i = 0; i < Faiths.Length; i++)
            {
                int index = i;
                int col = i % 2, row = i / 2;
                Button button = MakeButton(page, Faiths[i].label.ToUpperInvariant(), new Vector2(60f + col * 300f, -160f - row * 54f), new Vector2(280f, 46f), 21f);
                button.onClick.AddListener(() => { if (busy) return; faithIndex = index; RefreshFaith(); });
                faithButtons.Add(button);
            }

            faithText = Label(page, string.Empty, TextFont, 24f, Ink, new Vector2(60f, -390f), new Vector2(580f, 120f), TextAlignmentOptions.TopLeft);
            Label(page, "Nessun colono pronuncia davanti al sacerdote i nomi che non si pronunciano.",
                ItalicFont, 21f, Fade(Ink, 0.7f), new Vector2(60f, -525f), new Vector2(580f, 60f), TextAlignmentOptions.TopLeft);
            Label(page, "Per ora la fede non dà poteri: è ciò in cui il tuo personaggio crede.",
                ItalicFont, 19f, Fade(Ink, 0.6f), new Vector2(60f, -585f), new Vector2(580f, 40f), TextAlignmentOptions.TopLeft);
            RefreshFaith();
            return page.gameObject;
        }

        void RefreshFaith()
        {
            for (int i = 0; i < faithButtons.Count; i++) MarkSelected(faithButtons[i], i == faithIndex);
            if (faithText != null) faithText.text = Faiths[faithIndex].text;
        }

        // ---------- Il ritratto ----------

        IEnumerator PortraitWhenReady()
        {
            // Si aspetta che UMA abbia finito di ricostruire il personaggio, poi un attimo perché si assesti.
            while (rebuildAt > 0f) yield return null;
            yield return new WaitForSecondsRealtime(0.6f);
            TakePortrait();
            portraitRoutine = null;
        }

        void TakePortrait()
        {
            if (preview == null) return;
            RenderTexture target = RenderTexture.GetTemporary(PortraitWidth, PortraitHeight, 24, RenderTextureFormat.ARGB32);
            var cameraGO = new GameObject("Ritrattista");
            cameraGO.transform.SetParent(transform, false);
            try
            {
                Camera painter = cameraGO.AddComponent<Camera>();
                painter.enabled = true; // acceso solo per questo fotogramma: poi l'oggetto si distrugge
                painter.fieldOfView = 24f;
                painter.nearClipPlane = 0.05f;
                painter.farClipPlane = 30f;
                painter.clearFlags = CameraClearFlags.SolidColor;
                painter.backgroundColor = fogColor;
                painter.targetTexture = target;
                UniversalAdditionalCameraData data = painter.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

                // Di fronte al viso, un poco sopra gli occhi, come il sacerdote che disegna il nuovo colono.
                Vector3 face = HeadPosition() - Vector3.up * 0.05f;
                Vector3 forward = preview.transform.forward;
                painter.transform.position = face + forward * 0.9f + Vector3.up * 0.03f;
                painter.transform.rotation = Quaternion.LookRotation(face - painter.transform.position, Vector3.up);

                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(painter, request)) RenderPipeline.SubmitRenderRequest(painter, request);
                else painter.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                if (portraitTexture == null) portraitTexture = new Texture2D(PortraitWidth, PortraitHeight, TextureFormat.RGB24, false);
                portraitTexture.ReadPixels(new Rect(0, 0, PortraitWidth, PortraitHeight), 0, 0);
                portraitTexture.Apply();
                RenderTexture.active = previous;

                portraitBytes = portraitTexture.EncodeToJPG(85);
                portraitImage.texture = portraitTexture;
                portraitImage.color = Color.white;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Valdorso] Il ritratto non è riuscito: " + e.Message);
                portraitBytes = null;
            }
            finally
            {
                Camera used = cameraGO.GetComponent<Camera>();
                if (used != null) used.targetTexture = null;
                DestroyImmediate(cameraGO); // subito, così non disegna niente sullo schermo in questo fotogramma
                RenderTexture.ReleaseTemporary(target);
            }
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
            var look = new AvatarDefinition
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
            // I lineamenti: ogni cursore muove le sue misure intorno al valore di base (0,5).
            var dna = new List<DnaDef>(look.Dna);
            for (int i = 0; i < Features.Length && i < featureSliders.Count; i++)
                dna.Add(new DnaDef(Features[i].dna, featureSliders[i].value));
            look.Dna = dna.ToArray();
            return look;
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

        // ---------- La scelta del personaggio ----------

        void BuildSelectionPage(RectTransform sheet)
        {
            RectTransform page = Box(sheet, "Pagina_Scelta", new Vector2(0f, -170f), new Vector2(700f, 740f));
            selectionPage = page.gameObject;
            Label(page, "I TUOI NOMI NEL REGISTRO", ButtonFont, 30f, Ink, new Vector2(60f, -10f), new Vector2(580f, 44f), TextAlignmentOptions.Left);
            Label(page, "Chi è scritto nel registro può tornare nella valle quando vuole. Il Cuore ricorda ogni nome.",
                ItalicFont, 23f, Fade(Ink, 0.85f), new Vector2(60f, -58f), new Vector2(580f, 70f), TextAlignmentOptions.TopLeft);
            selectionRows = Box(page, "Elenco", new Vector2(60f, -135f), new Vector2(580f, 330f));
            selectionCount = Label(page, string.Empty, ItalicFont, 20f, Fade(Ink, 0.7f), new Vector2(60f, -470f), new Vector2(580f, 30f), TextAlignmentOptions.TopLeft);

            enterButton = MakeButton(page, "ENTRA NELLA VALLE", new Vector2(60f, -510f), new Vector2(580f, 58f), 26f);
            enterButton.onClick.AddListener(EnterWithSelected);
            newCharacterButton = MakeButton(page, "SCRIVI UN NUOVO NOME", new Vector2(60f, -582f), new Vector2(285f, 48f), 19f);
            newCharacterButton.onClick.AddListener(() => { if (!busy) ShowCreation(); });
            deleteButton = MakeButton(page, "CANCELLA DAL REGISTRO", new Vector2(355f, -582f), new Vector2(285f, 48f), 19f);
            deleteButton.onClick.AddListener(OpenConfirm);
            selectionStatus = Label(page, string.Empty, ItalicFont, 22f, Fade(Ink, 0.85f), new Vector2(60f, -645f), new Vector2(580f, 70f), TextAlignmentOptions.Top);
            selectionPage.SetActive(false);
        }

        void BuildConfirmPanel(RectTransform sheet)
        {
            RectTransform panel = Box(sheet, "Conferma_Cancellazione", new Vector2(50f, -300f), new Vector2(600f, 330f));
            confirmPanel = panel.gameObject;
            var back = panel.gameObject.AddComponent<Image>();
            back.sprite = theme != null ? theme.parchmentTexture : null;
            back.color = theme != null ? Color.Lerp(theme.parchment, theme.ink, 0.12f) : new Color(0.78f, 0.7f, 0.55f);
            var frameGO = new GameObject("Cornice", typeof(RectTransform), typeof(Image));
            frameGO.transform.SetParent(panel, false);
            Stretch((RectTransform)frameGO.transform, 0f, 0f, 0f, 0f);
            var frame = frameGO.GetComponent<Image>();
            frame.sprite = theme != null ? theme.buttonFrame : null;
            frame.type = Image.Type.Sliced;
            frame.color = Blood;
            frame.raycastTarget = false;
            if (frame.sprite == null) frameGO.SetActive(false);

            confirmText = Label(panel, string.Empty, TextFont, 24f, Ink, new Vector2(30f, -25f), new Vector2(540f, 110f), TextAlignmentOptions.TopLeft);
            confirmField = MakeInputField(panel, new Vector2(30f, -140f), new Vector2(540f, 58f));
            confirmField.onValueChanged.AddListener(_ => RefreshConfirm());
            confirmDeleteButton = MakeButton(panel, "CANCELLA PER SEMPRE", new Vector2(30f, -240f), new Vector2(300f, 52f), 19f);
            confirmDeleteButton.onClick.AddListener(ConfirmDelete);
            Button cancel = MakeButton(panel, "ANNULLA", new Vector2(350f, -240f), new Vector2(220f, 52f), 19f);
            cancel.onClick.AddListener(() => confirmPanel.SetActive(false));
            TMP_Text dangerLabel = confirmDeleteButton.GetComponentInChildren<TMP_Text>();
            if (dangerLabel != null) dangerLabel.color = new Color(1f, 0.78f, 0.72f);
            confirmPanel.SetActive(false);
        }

        void ShowSelection(CharacterListResponse list)
        {
            selectionMode = true;
            faceShot = false;
            rebuildAt = -1f;
            foreach (GameObject page in pages) page.SetActive(false);
            backButton.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            pageCounter.gameObject.SetActive(false);
            confirmPanel.SetActive(false);
            selectionPage.SetActive(true);

            ClearEntries();
            CharacterSummary[] characters = list.characters ?? new CharacterSummary[0];
            for (int i = 0; i < characters.Length; i++) entries.Add(BuildEntry(characters[i], i));
            selectionCount.text = $"{characters.Length} di {list.maxCharacters} nomi possibili per questo account.";
            newCharacterButton.interactable = characters.Length < list.maxCharacters;
            selectionStatus.text = characters.Length < list.maxCharacters ? string.Empty : "Il registro non ha più spazio per nuovi nomi di questo account.";
            SelectEntry(Mathf.Clamp(selectedEntry, 0, characters.Length - 1));
        }

        void ShowCreation()
        {
            selectionMode = false;
            if (selectionPage != null) selectionPage.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            ShowPage(0);
            ScheduleLook(0f);
        }

        void GoBack()
        {
            if (busy) return;
            if (currentPage == 0 && HasCharacters) ShowSelection(ValdorsoNetworkManager.LastCharacterList.Value);
            else ShowPage(currentPage - 1);
        }

        void OnCharacterListUpdated(CharacterListResponse list)
        {
            if (this == null || busy) return;
            if (list.characters != null && list.characters.Length > 0) ShowSelection(list);
            else ShowCreation();
        }

        (CharacterSummary, Button, Texture2D) BuildEntry(CharacterSummary data, int index)
        {
            Button row = MakeButton(selectionRows, string.Empty, new Vector2(0f, -index * 104f), new Vector2(580f, 96f), 20f);
            row.onClick.AddListener(() => { if (!busy) SelectEntry(index); });

            Texture2D portrait = null;
            RectTransform picture = Box(row.transform, "Ritratto", new Vector2(10f, -8f), new Vector2(64f, 80f));
            var image = picture.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            if (data.portrait != null && data.portrait.Length > 0)
            {
                portrait = new Texture2D(2, 2);
                if (portrait.LoadImage(data.portrait)) image.texture = portrait;
            }
            if (image.texture == null) image.color = new Color(0f, 0f, 0f, 0.35f);

            Color light = theme != null ? theme.text : Color.white;
            Label(row.transform, data.name, TitleFont, 26f, light, new Vector2(92f, -12f), new Vector2(470f, 38f), TextAlignmentOptions.Left);
            string faith = string.IsNullOrEmpty(data.faith) ? "nessuna fede" : data.faith;
            Label(row.transform, $"{faith}  ·  {WhenPlayed(data.lastPlayedAt)}", ItalicFont, 20f, Fade(light, 0.75f),
                new Vector2(92f, -52f), new Vector2(470f, 30f), TextAlignmentOptions.Left);
            return (data, row, portrait);
        }

        void ClearEntries()
        {
            foreach (var entry in entries)
            {
                if (entry.portrait != null) Destroy(entry.portrait);
                if (entry.row != null) Destroy(entry.row.gameObject);
            }
            entries.Clear();
        }

        void SelectEntry(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            selectedEntry = index;
            for (int i = 0; i < entries.Count; i++) MarkSelected(entries[i].row, i == index);

            // Sul palco compare il personaggio scelto, con il suo aspetto.
            string recipe = entries[index].data.appearanceRecipe;
            if (preview == null || string.IsNullOrEmpty(recipe)) return;
            RaiseUmaQuality();
            try
            {
                preview.LoadAvatarDefinition(recipe);
                preview.BuildCharacter(true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Valdorso] Aspetto del personaggio non leggibile: " + e.Message);
            }
        }

        void EnterWithSelected()
        {
            if (busy || selectedEntry < 0 || selectedEntry >= entries.Count) return;
            busy = true;
            SetSelectionButtons(false);
            CharacterSummary chosen = entries[selectedEntry].data;
            StartCoroutine(EnterWorld(chosen.id, selectionStatus, $"Il sacerdote legge il nome di {chosen.name}.\nIl frammento batte, e la valle ti riconosce.", 1.6f));
        }

        void SetSelectionButtons(bool interactable)
        {
            enterButton.interactable = interactable;
            deleteButton.interactable = interactable;
            newCharacterButton.interactable = interactable && HasCharacters &&
                ValdorsoNetworkManager.LastCharacterList.Value.characters.Length < ValdorsoNetworkManager.LastCharacterList.Value.maxCharacters;
            foreach (var entry in entries) entry.row.interactable = interactable;
        }

        // ---------- La cancellazione (bisogna riscrivere il nome) ----------

        void OpenConfirm()
        {
            if (busy || selectedEntry < 0 || selectedEntry >= entries.Count) return;
            string name = entries[selectedEntry].data.name;
            confirmText.text = $"Per cancellare per sempre <b>{name}</b> dal registro, scrivi il suo nome qui sotto. Non si potrà tornare indietro.";
            confirmField.text = string.Empty;
            confirmPanel.SetActive(true);
            confirmPanel.transform.SetAsLastSibling();
            RefreshConfirm();
            confirmField.Select();
            confirmField.ActivateInputField();
        }

        void RefreshConfirm()
        {
            if (selectedEntry < 0 || selectedEntry >= entries.Count) return;
            confirmDeleteButton.interactable = string.Equals(confirmField.text.Trim(), entries[selectedEntry].data.name.Trim(),
                System.StringComparison.OrdinalIgnoreCase);
        }

        void ConfirmDelete()
        {
            if (busy || selectedEntry < 0 || selectedEntry >= entries.Count) return;
            CharacterSummary target = entries[selectedEntry].data;
            busy = true;
            confirmDeleteButton.interactable = false;
            ValdorsoNetworkManager.RequestDeleteCharacter(target.id, confirmField.text, (success, message) =>
            {
                if (this == null) return;
                busy = false;
                confirmPanel.SetActive(false);
                selectedEntry = 0;
                selectionStatus.text = success ? $"Il nome di {target.name} è stato cancellato dal registro." : message;
                SetSelectionButtons(true);
                // Se è riuscita, l'elenco aggiornato arriva da solo dal server.
            });
        }

        static string WhenPlayed(string isoDate)
        {
            if (!System.DateTime.TryParse(isoDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out System.DateTime when))
                return "mai entrato nella valle";
            int days = (int)(System.DateTime.UtcNow.Date - when.ToUniversalTime().Date).TotalDays;
            return days <= 0 ? "nella valle oggi" : days == 1 ? "nella valle ieri" : $"nella valle {days} giorni fa";
        }

        // ---------- Navigazione ----------

        void ShowPage(int index)
        {
            if (busy) return;
            currentPage = Mathf.Clamp(index, 0, pages.Count - 1);
            for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == currentPage);
            pageCounter.gameObject.SetActive(true);
            string pageName = pages[currentPage].name;
            faceShot = pageName == "Pagina_Volto" || pageName == "Pagina_Lineamenti";
            if (pageName == "Pagina_Lineamenti") CheckDnaNames();

            bool last = currentPage == pages.Count - 1;
            backButton.gameObject.SetActive(currentPage > 0 || HasCharacters); // dalla prima pagina si torna all'elenco
            nextButton.gameObject.SetActive(!last);
            pageCounter.text = $"Pagina {currentPage + 1} di {pages.Count}";

            if (currentPage == 0 && nameField != null)
            {
                nameField.Select();
                nameField.ActivateInputField();
            }
            if (last)
            {
                RefreshSummary();
                if (portraitRoutine != null) StopCoroutine(portraitRoutine);
                portraitBytes = null;
                portraitRoutine = StartCoroutine(PortraitWhenReady());
            }
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
                $"Fede:  {(Faiths[faithIndex].key.Length > 0 ? Faiths[faithIndex].label : "nessuna")}";
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
            // L'aspetto scelto parte come ricetta UMA, insieme alla fede.
            string recipe = ChosenRecipe();
            if (portraitBytes == null) TakePortrait();
            ValdorsoNetworkManager.RequestCreateCharacter(ChosenName, Faiths[faithIndex].key, recipe, portraitBytes, OnCreated);
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
            foreach (Button b in faithButtons) b.interactable = interactable;
            if (femaleButton != null) femaleButton.interactable = interactable;
        }

        IEnumerator EnterWorld(string characterId)
        {
            yield return EnterWorld(characterId, statusText, "Il frammento batte più forte, per un istante.\nLa valle ti ha sentito.", 2f);
        }

        IEnumerator EnterWorld(string characterId, TMP_Text where, string message, float pause)
        {
            where.text = message;
            yield return new WaitForSecondsRealtime(pause);

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

            if (leftWord != null)
                Label(parent, leftWord, ItalicFont, 20f, Fade(Ink, 0.7f), topLeft + new Vector2(0f, -30f), new Vector2(200f, 28f), TextAlignmentOptions.Left);
            if (rightWord != null)
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