using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Valdorso.Creation;
using Valdorso.UI;
using Object = UnityEngine.Object;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Costruisce la scena di creazione del personaggio: un cerchio di pietra degli Antichi con le rune,
    /// il frammento del Cuore del Mondo che batte su un altare, due bracieri, menhir in rovina,
    /// nebbia della valle e il personaggio al centro.
    /// Menu di Unity: Valdorso → Crea scena di creazione del personaggio.
    /// Si può rilanciare quando si vuole: ridisegna immagini e materiali e ricostruisce la scena da zero.
    /// Funziona solo nell'editor, non entra nel gioco.
    /// </summary>
    public static class CreationStageBuilder
    {
        const string ScenePath = "Assets/_Valdorso/Scenes/Creazione.unity";
        const string ArtParent = "Assets/_Valdorso/Art";
        const string ArtFolder = ArtParent + "/Creazione";
        const string ThemePath = "Assets/_Valdorso/Art/UI/Tema/TemaValdorso.asset";
        const string PlayerPrefabPath = "Assets/_Valdorso/Prefabs/Giocatore_UMA.prefab";
        const string PolyHavenFolder = ArtParent + "/PolyHaven";

        // Il palco sta molto sotto il mondo: quando si aprirà sopra la valle, i due non si vedranno.
        static readonly Vector3 StageOrigin = new Vector3(0f, -500f, 0f);
        static readonly Color NightColor = new Color(0.035f, 0.04f, 0.055f);

        [MenuItem("Valdorso/Crea scena di creazione del personaggio")]
        static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Valdorso", "Ferma prima il Play.", "OK");
                return;
            }
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Valdorso", "La scena Creazione esiste già. Ricostruirla da zero?", "Ricostruisci", "Annulla"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ValdorsoTheme theme = AssetDatabase.LoadAssetAtPath<ValdorsoTheme>(ThemePath);
            if (theme == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Manca il Tema UI: prima Valdorso → Genera tema e immagini UI.", "OK");
                return;
            }
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo il prefab " + PlayerPrefabPath, "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder(ArtParent, "Creazione");

            // ---------- Immagini e materiali ----------
            EditorUtility.DisplayProgressBar("Valdorso", "Incido le rune del cerchio...", 0.2f);
            Texture2D runeMask = SaveTexture("Cerchio_Rune", DrawCircleRunes(2048), 2048, false);
            EditorUtility.DisplayProgressBar("Valdorso", "Preparo pietra, fuoco e cristallo...", 0.6f);

            // Pietra vera (Poly Haven, CC0). Se una cartella manca, resta la pietra liscia di prima.
            Material stone = PolyHavenMaterial("monastery_stone_floor", "Pavimento_Antico", Vector2.one, new Color(0.30f, 0.27f, 0.24f));
            Material altarStone = PolyHavenMaterial("quarry_wall", "Altare_Pietra", new Vector2(0.7f, 0.6f), new Color(0.30f, 0.27f, 0.24f));
            Material boulderStone = PolyHavenMaterial("boulder_01", "Masso", Vector2.one, new Color(0.30f, 0.27f, 0.24f));
            Material darkStone = LitMaterial("Pietra_Scura", new Color(0.16f, 0.14f, 0.12f), 0.12f, 0f);

            // Le rune: un intarsio d'oro nella pietra. Dove non c'è una runa il quadrato è "tagliato via" (alpha clip).
            Material circle = LitMaterial("Cerchio", new Color(0.42f, 0.31f, 0.12f), 0.7f, 0.9f);
            circle.SetTexture("_BaseMap", runeMask);
            circle.SetFloat("_AlphaClip", 1f);
            circle.SetFloat("_Cutoff", 0.5f);
            circle.EnableKeyword("_ALPHATEST_ON");
            circle.renderQueue = (int)RenderQueue.AlphaTest;
            SetEmission(circle, theme.gold * 1.1f, runeMask);

            Material heart = LitMaterial("Cuore", new Color(0.75f, 0.28f, 0.1f), 0.95f, 0f);
            SetEmission(heart, theme.ember * 1.8f, null);
            Material coals = LitMaterial("Brace", new Color(0.05f, 0.03f, 0.02f), 0.1f, 0f);
            SetEmission(coals, theme.ember * 1.4f, null);
            Material iron = LitMaterial("Ferro", new Color(0.12f, 0.11f, 0.10f), 0.45f, 0.85f);

            Mesh crystal = SaveCrystalMesh();
            AssetDatabase.SaveAssets();

            // ---------- Scena ----------
            EditorUtility.DisplayProgressBar("Valdorso", "Costruisco il palco...", 0.8f);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            // Notte fredda: la nebbia sfuma nello stesso colore del cielo, così le rovine svaniscono nel buio.
            RenderSettings.ambientLight = new Color(0.05f, 0.055f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.14f;
            RenderSettings.fogColor = NightColor;

            var root = new GameObject("Creazione");
            root.transform.position = StageOrigin;
            Transform r = root.transform;

            // Il cerchio di pietra degli Antichi, con le rune che battono insieme al Cuore.
            Primitive(PrimitiveType.Cylinder, "Basamento", r, new Vector3(0f, -0.15f, 0f), Vector3.zero, new Vector3(4.6f, 0.15f, 4.6f), altarStone);
            var floorGO = new GameObject("Pavimento");
            floorGO.transform.SetParent(r, false);
            floorGO.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            floorGO.AddComponent<MeshFilter>().sharedMesh = SaveDiscMesh(2.3f, 1.8f);
            floorGO.AddComponent<MeshRenderer>().sharedMaterial = stone;
            GameObject runes = Primitive(PrimitiveType.Quad, "Cerchio_Rune", r, new Vector3(0f, 0.006f, 0f), new Vector3(90f, 0f, 0f), new Vector3(4.4f, 4.4f, 1f), circle);
            StageLight runesPulse = runes.AddComponent<StageLight>();
            SetFields(runesPulse, ("mode", StageLight.Mode.Heartbeat), ("glowRenderer", runes.GetComponent<Renderer>()),
                ("glowColor", theme.gold * 1.2f), ("glowRest", 0.3f));

            // L'altare con il frammento del Cuore del Mondo.
            // Sta dietro il personaggio, un po' a sinistra: si vede tra il registro e lui.
            // Girato un poco verso destra (per chi guarda): non sta piatto davanti alla telecamera.
            Vector3 altar = new Vector3(0.8f, 0f, -2.3f);
            var altarGroup = new GameObject("Altare_Gruppo").transform;
            altarGroup.SetParent(r, false);
            altarGroup.localPosition = altar;
            altarGroup.localRotation = Quaternion.Euler(0f, -20f, 0f);
            Primitive(PrimitiveType.Cube, "Altare", altarGroup, new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(1.2f, 1.0f, 0.6f), altarStone);
            Primitive(PrimitiveType.Cube, "Altare_Piano", altarGroup, new Vector3(0f, 1.03f, 0f), Vector3.zero, new Vector3(1.36f, 0.06f, 0.72f), altarStone);

            var heartGO = new GameObject("Frammento_del_Cuore");
            heartGO.transform.SetParent(r, false);
            heartGO.transform.localPosition = altar + new Vector3(0f, 1.52f, 0f);
            heartGO.transform.localScale = new Vector3(0.26f, 0.42f, 0.26f);
            heartGO.AddComponent<MeshFilter>().sharedMesh = crystal;
            var heartRenderer = heartGO.AddComponent<MeshRenderer>();
            heartRenderer.sharedMaterial = heart;
            heartRenderer.shadowCastingMode = ShadowCastingMode.Off;

            Light heartLight = PointLight("Luce_del_Cuore", r, altar + new Vector3(0f, 1.55f, 0.3f), new Color(1f, 0.58f, 0.3f), 1.4f, 5f);
            StageLight heartbeat = heartLight.gameObject.AddComponent<StageLight>();
            SetFields(heartbeat, ("mode", StageLight.Mode.Heartbeat), ("target", heartLight), ("baseIntensity", 1.3f),
                ("variation", 2.2f), ("glowRenderer", (Renderer)heartRenderer), ("glowColor", theme.ember * 2.4f), ("glowRest", 0.45f),
                ("floating", heartGO.transform));

            // L'iscrizione sul fronte dell'altare, rivolta verso chi guarda.
            Inscription(altarGroup, theme, "IL CUORE BATTE ANCORA", new Vector3(0f, 0.68f, 0.302f), 0.7f);

            // Due bracieri, uno per lato.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 b = side < 0 ? new Vector3(2.4f, 0f, -1.3f) : new Vector3(-1.35f, 0f, -1.7f);
                string n = side < 0 ? "Braciere_Sinistro" : "Braciere_Destro";
                var brazier = new GameObject(n).transform;
                brazier.SetParent(r, false);
                brazier.localPosition = b;
                Primitive(PrimitiveType.Cylinder, "Gamba", brazier, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(0.1f, 0.45f, 0.1f), iron);
                Primitive(PrimitiveType.Cylinder, "Base", brazier, new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(0.45f, 0.03f, 0.45f), iron);
                Primitive(PrimitiveType.Cylinder, "Coppa", brazier, new Vector3(0f, 0.93f, 0f), Vector3.zero, new Vector3(0.6f, 0.07f, 0.6f), iron);
                GameObject embers = Primitive(PrimitiveType.Sphere, "Braci", brazier, new Vector3(0f, 1.0f, 0f), Vector3.zero, new Vector3(0.48f, 0.14f, 0.48f), coals);
                embers.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Light fire = PointLight("Fuoco", brazier, new Vector3(0f, 1.35f, 0f), new Color(1f, 0.5f, 0.2f), 1.3f, 4f);
                StageLight flicker = fire.gameObject.AddComponent<StageLight>();
                SetFields(flicker, ("mode", StageLight.Mode.Flicker), ("target", fire), ("baseIntensity", 1.3f), ("variation", 0.5f),
                    ("glowRenderer", embers.GetComponent<Renderer>()), ("glowColor", theme.ember * 2f), ("glowRest", 0.6f));
            }

            // Menhir in rovina degli Antichi, dietro e ai lati.
            // Pietre ritte degli Antichi: il masso vero di Poly Haven, in piedi e girato ogni volta in modo diverso.
            GameObject boulder = FindModel("boulder_01");
            if (boulder != null)
            {
                StandingStone(boulder, scene, r, boulderStone, new Vector3(3.9f, 0f, -3.4f), -30f, 2.3f);
                StandingStone(boulder, scene, r, boulderStone, new Vector3(-2.9f, 0f, -4.2f), 115f, 2.7f);
                StandingStone(boulder, scene, r, boulderStone, new Vector3(-3.7f, 0f, -1.9f), 200f, 1.1f);
                StandingStone(boulder, scene, r, boulderStone, new Vector3(-0.6f, 0f, -6.2f), 10f, 3.1f);
                StandingStone(boulder, scene, r, boulderStone, new Vector3(2.3f, 0f, -6.8f), 250f, 2.5f);
            }
            else
            {
                Debug.LogWarning("[Valdorso] Non trovo il modello in " + PolyHavenFolder + "/boulder_01: metto i menhir semplici.");
                Menhir(r, darkStone, new Vector3(3.9f, 1.1f, -3.4f), new Vector3(0f, -30f, -4f), new Vector3(0.55f, 2.2f, 0.36f));
                Menhir(r, darkStone, new Vector3(-2.9f, 1.3f, -4.2f), new Vector3(0f, 25f, 3f), new Vector3(0.6f, 2.6f, 0.4f));
                Menhir(r, darkStone, new Vector3(-0.6f, 1.7f, -6.2f), new Vector3(0f, -8f, 0f), new Vector3(0.75f, 3.4f, 0.45f));
            }

            // Luci: la luna fredda, e una luce calda davanti che illumina il viso.
            var moonGO = new GameObject("Luna");
            moonGO.transform.SetParent(r, false);
            moonGO.transform.rotation = Quaternion.Euler(32f, 160f, 0f);
            Light moon = moonGO.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.62f, 0.82f);
            moon.intensity = 0.55f;
            moon.shadows = LightShadows.Soft;
            PointLight("Luce_del_Viso", r, new Vector3(0.6f, 1.7f, 2.2f), new Color(1f, 0.84f, 0.66f), 0.9f, 5f);

            // Il personaggio al centro, rivolto verso chi guarda.
            GameObject character = PreviewCharacter(playerPrefab, scene, r);

            // La telecamera guarda verso -Z, quindi per lei la destra è -X: spostandola verso +X
            // il personaggio (in X = 0) finisce a destra dello schermo e a sinistra resta posto per il registro.
            var camGO = new GameObject("Telecamera_Creazione");
            camGO.transform.SetParent(r, false);
            camGO.transform.localPosition = new Vector3(0.85f, 1.65f, 3.3f);
            camGO.transform.LookAt(r.TransformPoint(new Vector3(0.85f, 0.95f, 0f)));
            Camera cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = NightColor;
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            // Bordi puliti: con Deferred+ l'MSAA non si usa, si usa l'SMAA.
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            EditorUtility.SetDirty(camData);

            // Atmosfera: bagliore attorno alle luci, toni cinematografici, bordi scuri.
            var volumeGO = new GameObject("Atmosfera");
            volumeGO.transform.SetParent(r, false);
            Volume volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = BuildProfile();

            // Il regista della scena: spegne il mondo, mostra il registro, fa entrare il personaggio.
            CreationController controller = root.AddComponent<CreationController>();
            // Il tema si ricarica qui: salvare il profilo dell'atmosfera (poco sopra) fa rileggere gli asset a Unity,
            // e il riferimento preso all'inizio può non valere più (per questo il campo Theme restava vuoto).
            theme = AssetDatabase.LoadAssetAtPath<ValdorsoTheme>(ThemePath);
            SetFields(controller, ("theme", theme), ("stageCamera", cam), ("fogColor", NightColor),
                ("fogDensity", 0.14f), ("ambientColor", new Color(0.05f, 0.055f, 0.07f)));
            EditorUtility.SetDirty(controller);
            if (new SerializedObject(controller).FindProperty("theme").objectReferenceValue == null)
                Debug.LogWarning("[Valdorso] Il Tema UI non è entrato nel Creation Controller: trascina TemaValdorso nel campo Theme.");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildScenes();
            EditorUtility.ClearProgressBar();

            Selection.activeGameObject = camGO;
            Debug.Log($"[Valdorso] Scena di creazione costruita: {ScenePath}. Post-processing della telecamera: " +
                      $"{(cam.GetUniversalAdditionalCameraData().renderPostProcessing ? "acceso" : "SPENTO")}, " +
                      $"effetti nel profilo: {volume.sharedProfile.components.Count}, nebbia: {(RenderSettings.fog ? "accesa" : "spenta")}.");
        }

        // ---------- Il personaggio di anteprima ----------

        static GameObject PreviewCharacter(GameObject prefab, Scene scene, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Personaggio";
            go.tag = "Untagged";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Via la telecamera del giocatore: qui c'è quella della scena.
            foreach (string childName in new[] { "PlayerCameraRoot", "PlayerFollowCamera" })
            {
                Transform child = go.transform.Find(childName);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }

            // Restano solo Transform, Animator e i componenti di UMA: niente rete, comandi, combattimento.
            for (int pass = 0; pass < 12; pass++)
            {
                bool removed = false;
                Component[] all = go.GetComponents<Component>();
                foreach (Component c in all)
                {
                    if (c == null || Keep(c) || IsRequiredByOthers(c, all)) continue;
                    Object.DestroyImmediate(c);
                    removed = true;
                }
                if (!removed) break;
            }

            go.AddComponent<CharacterPreview>();
            return go;
        }

        static bool Keep(Component c)
        {
            if (c is Transform || c is Animator) return true;
            string ns = c.GetType().Namespace ?? string.Empty;
            return ns == "UMA" || ns.StartsWith("UMA.");
        }

        static bool IsRequiredByOthers(Component c, Component[] all)
        {
            foreach (Component other in all)
            {
                if (other == null || other == c) continue;
                foreach (RequireComponent rc in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if (Requires(rc.m_Type0, c) || Requires(rc.m_Type1, c) || Requires(rc.m_Type2, c)) return true;
                }
            }
            return false;
        }

        static bool Requires(Type t, Component c) => t != null && t.IsAssignableFrom(c.GetType());

        // ---------- Pezzi della scena ----------

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void Menhir(Transform parent, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            Primitive(PrimitiveType.Cube, "Menhir", parent, pos, euler, scale, mat);
        }

        static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        static void Inscription(Transform parent, ValdorsoTheme theme, string text, Vector3 pos, float maxSize)
        {
            var go = new GameObject("Iscrizione", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // il lato leggibile verso la telecamera
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = theme.titleFont;
            tmp.text = text;
            tmp.enableAutoSizing = true; // si rimpicciolisce da sé se non entra nella larghezza dell'altare
            tmp.fontSizeMax = maxSize;
            tmp.fontSizeMin = 0.2f;
            tmp.characterSpacing = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white; // il colore lo dà il materiale inciso
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)go.transform).sizeDelta = new Vector2(1.1f, 0.2f);
            tmp.fontSharedMaterial = EngravedMaterial(theme);
        }

        // Lettere incise nella pietra: oro spento dentro il solco, un'ombra che le fa sembrare scavate,
        // e un bordo scuro come il fondo dell'incisione.
        static Material EngravedMaterial(ValdorsoTheme theme)
        {
            string path = $"{ArtFolder}/Iscrizione_Incisa.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(theme.titleFont.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = theme.titleFont.material.shader;
                mat.CopyPropertiesFromMaterial(theme.titleFont.material);
            }
            mat.SetColor("_FaceColor", new Color(0.62f, 0.47f, 0.2f, 1f));
            mat.SetColor("_OutlineColor", new Color(0.08f, 0.06f, 0.04f, 1f));
            mat.SetFloat("_OutlineWidth", 0.12f);
            mat.SetFloat("_FaceDilate", 0.05f);
            mat.EnableKeyword("UNDERLAY_INNER");
            mat.DisableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
            mat.SetFloat("_UnderlayOffsetX", 0.35f);
            mat.SetFloat("_UnderlayOffsetY", -0.35f);
            mat.SetFloat("_UnderlayDilate", 0.1f);
            mat.SetFloat("_UnderlaySoftness", 0.35f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Atmosfera (post-processing) ----------

        static VolumeProfile BuildProfile()
        {
            string path = $"{ArtFolder}/Atmosfera_Creazione.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.7f);

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.3f);
            color.contrast.Override(12f);
            color.saturation.Override(-8f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);

            foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ---------- Poly Haven ----------

        // Costruisce un materiale URP dalle mappe scaricate da Poly Haven (_diff_, _nor_gl_, _rough_, _ao_).
        // La ruvidità (rough) viene convertita nella mappa di lucentezza che vuole URP.
        static Material PolyHavenMaterial(string assetName, string materialName, Vector2 tiling, Color fallback)
        {
            string folder = $"{PolyHavenFolder}/{assetName}";
            Texture2D diff = FindMap(folder, "_diff_"), nor = FindMap(folder, "_nor_gl_"),
                rough = FindMap(folder, "_rough_"), ao = FindMap(folder, "_ao_");
            if (diff == null)
            {
                Debug.LogWarning($"[Valdorso] Non trovo le texture di {assetName} in {folder}: uso una pietra semplice.");
                return LitMaterial(materialName, fallback, 0.15f, 0f);
            }

            SetImport(nor, true, false);
            SetImport(rough, false, false);
            SetImport(ao, false, false);

            Material mat = LitMaterial(materialName, Color.white, 1f, 0f);
            mat.SetTexture("_BaseMap", diff);
            mat.SetTextureScale("_BaseMap", tiling);
            if (nor != null)
            {
                mat.SetTexture("_BumpMap", nor);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            if (rough != null)
            {
                Texture2D gloss = SmoothnessFromRoughness(rough, materialName);
                mat.SetTexture("_MetallicGlossMap", gloss);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Smoothness", 0.8f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D FindMap(string folder, string tag)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).ToLowerInvariant().Contains(tag)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        static void SetImport(Texture2D tex, bool normalMap, bool srgb)
        {
            if (tex == null) return;
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
            bool changed = false;
            if (normalMap && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; changed = true; }
            if (!normalMap && importer.sRGBTexture != srgb) { importer.sRGBTexture = srgb; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        // URP vuole la lucentezza nel canale alfa (liscio = 1), Poly Haven dà la ruvidità (ruvido = 1): si inverte.
        static Texture2D SmoothnessFromRoughness(Texture2D rough, string materialName)
        {
            string roughPath = AssetDatabase.GetAssetPath(rough);
            Color32[] px;
            int width, height;
            var source = new Texture2D(2, 2);
            bool isJpgOrPng = !roughPath.EndsWith(".exr", StringComparison.OrdinalIgnoreCase) &&
                              source.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, roughPath)));
            if (isJpgOrPng)
            {
                px = source.GetPixels32();
                width = source.width;
                height = source.height;
            }
            else
            {
                // EXR (o altri formati): si fa leggere a Unity, rendendo la texture leggibile solo per un momento.
                var roughImporter = (TextureImporter)AssetImporter.GetAtPath(roughPath);
                bool wasReadable = roughImporter.isReadable;
                TextureImporterCompression wasCompression = roughImporter.textureCompression;
                roughImporter.isReadable = true;
                roughImporter.textureCompression = TextureImporterCompression.Uncompressed;
                roughImporter.SaveAndReimport();
                Texture2D readable = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
                px = readable.GetPixels32();
                width = readable.width;
                height = readable.height;
                roughImporter.isReadable = wasReadable;
                roughImporter.textureCompression = wasCompression;
                roughImporter.SaveAndReimport();
            }
            for (int i = 0; i < px.Length; i++)
            {
                byte smooth = (byte)(255 - px[i].r);
                px[i] = new Color32(0, 0, 0, smooth); // rosso = metallo (nessuno), alfa = lucentezza
            }
            var result = new Texture2D(width, height, TextureFormat.RGBA32, true);
            result.SetPixels32(px);
            result.Apply();
            string assetPath = $"{ArtFolder}/{materialName}_Lucentezza.png";
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath), result.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);

            AssetDatabase.ImportAsset(assetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        static GameObject FindModel(string assetName)
        {
            string folder = $"{PolyHavenFolder}/{assetName}";
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            return null;
        }

        // Un masso in piedi: alto "height" metri, appoggiato a terra e un po' interrato, come se fosse lì da mille anni.
        static void StandingStone(GameObject model, Scene scene, Transform parent, Material mat, Vector3 pos, float yaw, float height)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Pietra_Ritta";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Se il file contiene più versioni dello stesso masso (dettaglio alto e basso) senza un LOD Group,
            // si tiene solo la più dettagliata, altrimenti si vedrebbero sovrapposte.
            MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            if (go.GetComponentInChildren<LODGroup>() == null && renderers.Length > 1)
            {
                MeshRenderer best = null;
                int bestVerts = -1;
                foreach (MeshRenderer mr in renderers)
                {
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    int verts = mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0;
                    if (verts > bestVerts) { bestVerts = verts; best = mr; }
                }
                foreach (MeshRenderer mr in renderers) if (mr != best) mr.gameObject.SetActive(false);
            }
            foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                mr.sharedMaterials = mats;
            }

            // Misura il masso e lo porta all'altezza voluta, un po' più slanciato del vero (è una pietra ritta).
            Bounds b = RendererBounds(go);
            if (b.size.y > 0.001f)
            {
                float k = height / b.size.y;
                go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(k * 0.85f, k, k * 0.85f));
            }
            b = RendererBounds(go);
            float groundY = parent.TransformPoint(Vector3.zero).y;
            go.transform.position += Vector3.up * (groundY - b.min.y - 0.12f);
        }

        static Bounds RendererBounds(GameObject go)
        {
            bool first = true;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer rr in go.GetComponentsInChildren<Renderer>())
            {
                if (!rr.enabled || !rr.gameObject.activeInHierarchy) continue;
                if (first) { b = rr.bounds; first = false; }
                else b.Encapsulate(rr.bounds);
            }
            return b;
        }

        // Il disco del pavimento: la pietra si ripete ogni "tile" metri, così le lastre hanno la loro misura vera.
        static Mesh SaveDiscMesh(float radius, float tile)
        {
            string path = $"{ArtFolder}/Disco_Pavimento.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Disco_Pavimento" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            const int segments = 96;
            var verts = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            uvs[0] = Vector2.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                uvs[i + 1] = new Vector2(verts[i + 1].x / tile, verts[i + 1].z / tile);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 2 > segments ? 1 : i + 2;
                tris[i * 3 + 2] = i + 1;
            }
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents(); // servono alla mappa del rilievo
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ---------- Materiali ----------

        static Material LitMaterial(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetEmission(Material mat, Color hdrColor, Texture map)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", hdrColor);
            if (map != null) mat.SetTexture("_EmissionMap", map);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
        }

        // ---------- Il cristallo: una doppia piramide a sei facce ----------

        static Mesh SaveCrystalMesh()
        {
            string path = $"{ArtFolder}/Cristallo_del_Cuore.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Cristallo_del_Cuore" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();

            const int sides = 6;
            var top = new Vector3(0f, 1f, 0f);
            var bottom = new Vector3(0f, -0.8f, 0f);
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float radius = i % 2 == 0 ? 0.5f : 0.42f; // facce leggermente irregolari, come un cristallo vero
                ring[i] = new Vector3(Mathf.Cos(a) * radius, 0.12f, Mathf.Sin(a) * radius);
            }

            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % sides];
                AddTriangle(verts, tris, top, b, a);
                AddTriangle(verts, tris, bottom, a, b);
            }
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void AddTriangle(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); // vertici separati: facce nette
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        // ---------- Disegni del cerchio ----------

        // Le rune degli Antichi: due anelli con un giro di glifi, e il pentagono dei cinque elementi.
        static Color[] DrawCircleRunes(int s)
        {
            var px = new Color[s * s];
            float half = s / 2f;
            var rng = new System.Random(312); // l'anno in cui comincia il gioco: le rune escono sempre uguali

            // Glifi: ognuno è fatto di 2-4 tratti che uniscono punti di una griglia 3x3.
            const int glyphCount = 40;
            const float bandIn = 0.80f, bandOut = 0.93f; // in frazioni del raggio
            var glyphs = new List<(Vector2 a, Vector2 b)>[glyphCount];
            float cellAngle = Mathf.PI * 2f / glyphCount;
            for (int g = 0; g < glyphCount; g++)
            {
                glyphs[g] = new List<(Vector2, Vector2)>();
                int strokes = 2 + rng.Next(3);
                float center = g * cellAngle;
                for (int k = 0; k < strokes; k++)
                {
                    int p1 = rng.Next(9), p2 = rng.Next(9);
                    if (p1 == p2) p2 = (p2 + 4) % 9;
                    glyphs[g].Add((GridPoint(p1, center, cellAngle, bandIn, bandOut, half), GridPoint(p2, center, cellAngle, bandIn, bandOut, half)));
                }
            }

            // I cinque elementi: cinque cerchietti uniti da un pentagono.
            var elements = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI * 2f / 5f;
                elements[i] = new Vector2(half + Mathf.Cos(a) * 0.62f * half, half + Mathf.Sin(a) * 0.62f * half);
            }

            float line = s / 1024f * 2.2f; // spessore dei tratti in pixel
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float dx = p.x - half, dy = p.y - half;
                    float rPx = Mathf.Sqrt(dx * dx + dy * dy);
                    float r = rPx / half;
                    float d = float.MaxValue;

                    // Anelli
                    d = Mathf.Min(d, Mathf.Abs(rPx - 0.96f * half));
                    d = Mathf.Min(d, Mathf.Abs(rPx - 0.77f * half));
                    d = Mathf.Min(d, Mathf.Abs(rPx - 0.74f * half) + line * 0.4f);
                    d = Mathf.Min(d, Mathf.Abs(rPx - 0.50f * half) + line * 0.3f);

                    // Glifi del giro
                    if (r > bandIn - 0.02f && r < bandOut + 0.02f)
                    {
                        float angle = Mathf.Atan2(dy, dx);
                        if (angle < 0f) angle += Mathf.PI * 2f;
                        int g = Mathf.RoundToInt(angle / cellAngle) % glyphCount;
                        foreach (var seg in glyphs[g]) d = Mathf.Min(d, DistToSegment(p, seg.a, seg.b));
                    }

                    // Pentagono dei cinque elementi
                    for (int i = 0; i < 5; i++)
                    {
                        d = Mathf.Min(d, DistToSegment(p, elements[i], elements[(i + 1) % 5]) + line * 0.3f);
                        d = Mathf.Min(d, Mathf.Abs((p - elements[i]).magnitude - 0.05f * half));
                    }

                    float v = Mathf.Clamp01(line - d + 0.5f);
                    px[y * s + x] = new Color(v, v, v, v); // l'alpha dice dove c'è l'intarsio

                }
            }
            return px;
        }

        static Vector2 GridPoint(int index, float centerAngle, float cellAngle, float rIn, float rOut, float half)
        {
            int col = index % 3, row = index / 3;
            float a = centerAngle + (col - 1) * cellAngle * 0.28f;
            float r = Mathf.Lerp(rIn, rOut, row / 2f) * half;
            return new Vector2(half + Mathf.Cos(a) * r, half + Mathf.Sin(a) * r);
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return (p - (a + ab * t)).magnitude;
        }

        static float Fractal(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq, y * freq);
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum;
        }

        // ---------- Salvataggi ----------

        static Texture2D SaveTexture(string name, Color[] pixels, int size, bool srgb)
        {
            string assetPath = $"{ArtFolder}/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.alphaIsTransparency = srgb;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true; // le rune sottili non spariscono da lontano
            importer.alphaTestReferenceValue = 0.5f;
            importer.maxTextureSize = size;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        static void AddToBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(sc => sc.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // Scrive i campi [SerializeField] privati di un componente, come se li si compilasse nell'Inspector.
        static void SetFields(Component component, params (string name, object value)[] fields)
        {
            var so = new SerializedObject(component);
            foreach (var (name, value) in fields)
            {
                SerializedProperty prop = so.FindProperty(name);
                if (prop == null)
                {
                    Debug.LogWarning($"[Valdorso] Campo {name} non trovato in {component.GetType().Name}");
                    continue;
                }
                switch (value)
                {
                    case float f: prop.floatValue = f; break;
                    case Color c: prop.colorValue = c; break;
                    case Enum e: prop.enumValueIndex = Convert.ToInt32(e); break;
                    case Object o: prop.objectReferenceValue = o; break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}