using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Valdorso → Luce → Prova di bellezza (giorno): primo giro della prova di bellezza (6.7).
    /// Tocca solo luce e atmosfera, niente modelli:
    /// sole più caldo e deciso, luce del cielo sulle parti in ombra (tre colori: alto, orizzonte, terra),
    /// foschia in lontananza, ritocchi del filtro dell'immagine (profilo Atmosfera_Valle),
    /// ombre di contatto (SSAO) più visibili e correzione colore in alta gamma (HDR).
    /// Si può rilanciare: rimette sempre gli stessi valori, scritti qui sotto.
    /// Nota: ValleBuilder, se rilanciato, rifà il profilo Atmosfera_Valle da capo; dopo va rilanciato anche questo.
    ///
    /// Valdorso → Luce → Metti il cielo (HDRI selezionato): usa come cielo il file .hdr selezionato nel pannello Project
    /// (foto a 360° di un cielo vero). Il cielo diventa anche la luce delle ombre e dei riflessi (niente lightmap,
    /// niente calcolo lanciato dallo strumento).
    /// </summary>
    public static class ProvaDiBellezza
    {
        const string AtmosferaPath = "Assets/_Valdorso/Mondo/Valle/Atmosfera_Valle.asset";
        const string RendererPC = "Assets/Settings/PC_Renderer.asset";
        const string PipelinePC = "Assets/Settings/PC_RPAsset.asset";
        const string CieloPath = "Assets/_Valdorso/Mondo/Valle/Cielo_Valle.mat";
        const string LucePath = "Assets/_Valdorso/Mondo/Valle/Luce_Valle.lighting";

        // Il cielo HDRI: luminosità e rotazione (gradi) della foto del cielo.
        const float EsposizioneCielo = 1f;
        const float RotazioneCielo = 0f;

        // Il sole: un po' più caldo e più forte.
        static readonly Color ColoreSole = new Color(1f, 0.93f, 0.82f);
        const float IntensitaSole = 1.8f;

        // La luce del cielo che arriva nelle ombre: azzurra dall'alto, neutra di lato, bruna dal terreno.
        static readonly Color CieloAlto = new Color(0.50f, 0.60f, 0.78f);
        static readonly Color CieloOrizzonte = new Color(0.42f, 0.44f, 0.42f);
        static readonly Color CieloTerra = new Color(0.20f, 0.18f, 0.15f);

        // La foschia: azzurrina, leggera vicino, piena all'orizzonte (a 200 m vela circa un terzo del colore).
        static readonly Color ColoreFoschia = new Color(0.64f, 0.72f, 0.82f);
        const float DensitaFoschia = 0.0018f;

        [MenuItem("Valdorso/Luce/Prova di bellezza (giorno)")]
        static void Applica()
        {
            if (GameObject.Find("/Valle") == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Valle.", "OK");
                return;
            }
            var profilo = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AtmosferaPath);
            if (profilo == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non trovo il profilo " + AtmosferaPath + ".", "OK");
                return;
            }

            // 1. Il sole
            string esitoSole = "sole non trovato (RenderSettings.sun vuoto)";
            Light sole = RenderSettings.sun;
            if (sole != null)
            {
                Undo.RecordObject(sole, "Prova di bellezza");
                sole.color = ColoreSole;
                sole.intensity = IntensitaSole;
                sole.shadowStrength = 1f;
                EditorUtility.SetDirty(sole);
                esitoSole = "sole " + sole.name + " a " + IntensitaSole;
            }

            // 2. La luce del cielo nelle ombre (niente calcolo della luce: si vede subito).
            //    Se c'è già il cielo HDRI, la luce delle ombre viene da lui e qui non si tocca.
            bool cieloHdri = RenderSettings.skybox != null && RenderSettings.skybox.shader != null
                             && RenderSettings.skybox.shader.name == "Skybox/Cubemap";
            if (!cieloHdri)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = CieloAlto;
                RenderSettings.ambientEquatorColor = CieloOrizzonte;
                RenderSettings.ambientGroundColor = CieloTerra;
            }

            // 3. La foschia
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = ColoreFoschia;
            RenderSettings.fogDensity = DensitaFoschia;

            // 4. Il filtro dell'immagine (profilo Atmosfera_Valle, già nella scena)
            var tonemapping = Prendi<Tonemapping>(profilo);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var colori = Prendi<ColorAdjustments>(profilo);
            colori.postExposure.Override(0.3f);
            colori.contrast.Override(15f);
            colori.saturation.Override(8f);

            var bilanciamento = Prendi<WhiteBalance>(profilo);
            bilanciamento.temperature.Override(6f);

            var vignetta = Prendi<Vignette>(profilo);
            vignetta.intensity.Override(0.2f);
            vignetta.smoothness.Override(0.4f);

            EditorUtility.SetDirty(profilo);

            // 5. Ombre di contatto (SSAO) più visibili
            string esitoSSAO = RegolaSSAO();

            // 6. Correzione colore in alta gamma
            string esitoHDR = "pipeline PC non trovata";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePC);
            if (pipeline != null)
            {
                pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
                EditorUtility.SetDirty(pipeline);
                esitoHDR = "correzione colore HDR";
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[Valdorso] Prova di bellezza (giorno): " + esitoSole + "; foschia "
                      + DensitaFoschia + "; filtro ACES, esposizione 0,3, contrasto 15, saturazione 8, caldo 6, vignetta 0,2; "
                      + esitoSSAO + "; " + esitoHDR + "; luce delle ombre: " + (cieloHdri ? "dal cielo HDRI" : "tre colori") + ". Ora Ctrl+S sulla scena.");
        }

        [MenuItem("Valdorso/Luce/Metti il cielo (HDRI selezionato)")]
        static void MettiCielo()
        {
            if (GameObject.Find("/Valle") == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Apri prima la scena Valle.", "OK");
                return;
            }
            string percorso = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : "";
            string minuscolo = percorso.ToLowerInvariant();
            if (!minuscolo.EndsWith(".hdr") && !minuscolo.EndsWith(".exr"))
            {
                EditorUtility.DisplayDialog("Valdorso", "Seleziona prima nel pannello Project il file .hdr del cielo (un clic sul file), poi rilancia.", "OK");
                return;
            }

            // Il file diventa un "cubo" di cielo (Cubemap), la forma che Unity usa per i cieli.
            var importatore = (TextureImporter)AssetImporter.GetAtPath(percorso);
            if (importatore.textureShape != TextureImporterShape.TextureCube)
            {
                importatore.textureShape = TextureImporterShape.TextureCube;
                importatore.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                importatore.maxTextureSize = 4096;
                importatore.textureCompression = TextureImporterCompression.CompressedHQ;
                importatore.SaveAndReimport();
            }
            var cubo = AssetDatabase.LoadAssetAtPath<Cubemap>(percorso);
            if (cubo == null)
            {
                EditorUtility.DisplayDialog("Valdorso", "Non riesco a usare questo file come cielo: mandami uno screenshot del suo Inspector.", "OK");
                return;
            }

            // Il materiale del cielo
            Shader shader = Shader.Find("Skybox/Cubemap");
            var cielo = AssetDatabase.LoadAssetAtPath<Material>(CieloPath);
            if (cielo == null)
            {
                cielo = new Material(shader);
                AssetDatabase.CreateAsset(cielo, CieloPath);
            }
            else cielo.shader = shader;
            cielo.SetTexture("_Tex", cubo);
            cielo.SetFloat("_Exposure", EsposizioneCielo);
            cielo.SetFloat("_Rotation", RotazioneCielo);
            EditorUtility.SetDirty(cielo);

            // Il cielo illumina le ombre e dà i riflessi
            RenderSettings.skybox = cielo;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;

            // Impostazioni della luce della scena: niente lightmap, si calcola solo la luce dell'ambiente
            var impostazioni = AssetDatabase.LoadAssetAtPath<LightingSettings>(LucePath);
            if (impostazioni == null)
            {
                impostazioni = new LightingSettings { name = "Luce_Valle" };
                AssetDatabase.CreateAsset(impostazioni, LucePath);
            }
            impostazioni.bakedGI = false;
            impostazioni.realtimeGI = false;
            EditorUtility.SetDirty(impostazioni);
            Lightmapping.lightingSettings = impostazioni;

            AssetDatabase.SaveAssets();
            var scena = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scena);
            EditorSceneManager.SaveScene(scena);
            // Niente calcolo della luce qui: in Unity 6 la luce del cielo si aggiorna da sola.
            // (Il 30/09 il calcolo lanciato da qui è durato oltre 25 minuti e Unity si è chiuso.)

            Debug.Log("[Valdorso] Cielo: " + System.IO.Path.GetFileName(percorso) + ", esposizione " + EsposizioneCielo
                      + ", rotazione " + RotazioneCielo + "°. Luce e riflessi dal cielo, scena salvata.");
        }

        /// <summary>Prende un effetto dal profilo; se manca lo aggiunge e lo salva dentro il file del profilo.</summary>
        static T Prendi<T>(VolumeProfile profilo) where T : VolumeComponent
        {
            if (profilo.TryGet(out T componente)) return componente;
            componente = profilo.Add<T>(false);
            componente.name = typeof(T).Name;
            componente.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(componente, profilo);
            return componente;
        }

        static string RegolaSSAO()
        {
            foreach (Object oggetto in AssetDatabase.LoadAllAssetsAtPath(RendererPC))
            {
                if (oggetto is ScriptableRendererFeature funzione && funzione.name == "ScreenSpaceAmbientOcclusion")
                {
                    var so = new SerializedObject(funzione);
                    SerializedProperty intensita = so.FindProperty("m_Settings.Intensity");
                    SerializedProperty raggio = so.FindProperty("m_Settings.Radius");
                    SerializedProperty luceDiretta = so.FindProperty("m_Settings.DirectLightingStrength");
                    if (intensita == null || raggio == null) return "SSAO: campi non trovati, lasciato com'era";
                    intensita.floatValue = 0.8f;
                    raggio.floatValue = 0.5f;
                    if (luceDiretta != null) luceDiretta.floatValue = 0.3f;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(funzione);
                    return "SSAO intensità 0,8, raggio 0,5";
                }
            }
            return "SSAO non trovato nel PC_Renderer";
        }
    }
}