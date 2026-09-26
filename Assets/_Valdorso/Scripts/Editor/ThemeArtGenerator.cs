using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Valdorso.UI;

namespace Valdorso.EditorTools
{
    /// <summary>
    /// Disegna le immagini dello stile "Oro e brace" e crea (o aggiorna) il Tema UI.
    /// Menu di Unity: Valdorso → Genera tema e immagini UI.
    /// Le immagini vengono ridisegnate ogni volta; i colori del tema, se già esiste, non vengono toccati.
    /// Funziona solo nell'editor, non entra nel gioco.
    /// </summary>
    public static class ThemeArtGenerator
    {
        const string ParentFolder = "Assets/_Valdorso/Art/UI";
        const string Folder = ParentFolder + "/Tema";
        const string FontFolder = ParentFolder + "/Font/";
        const string ThemePath = Folder + "/TemaValdorso.asset";

        [MenuItem("Valdorso/Genera tema e immagini UI")]
        static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(ParentFolder, "Tema");

            Save("Cornice", DrawFrame(), 128, 128, new Vector4(28, 28, 28, 28));
            Save("Pulsante_Cornice", DrawButtonFrame(), 64, 64, new Vector4(12, 12, 12, 12));
            Save("Pulsante_Fondo", DrawButtonFill(), 64, 64, Vector4.zero);
            Save("Separatore", DrawDivider(), 512, 32, Vector4.zero);
            Save("Pergamena", DrawParchment(), 512, 512, Vector4.zero);
            Save("Brace", DrawEmber(), 32, 32, Vector4.zero);
            Save("Vignetta", DrawRadial(512, 0f, 1f, 0.3f, 1f, 1.41421f), 512, 512, Vector4.zero);
            Save("Bagliore", DrawRadial(512, 1f, 0f, 0f, 1f, 1f), 512, 512, Vector4.zero);

            ValdorsoTheme theme = AssetDatabase.LoadAssetAtPath<ValdorsoTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<ValdorsoTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.titleFont = LoadFont("Cinzel-Bold SDF");
            theme.buttonFont = LoadFont("Cinzel-Regular SDF");
            theme.textFont = LoadFont("EBGaramond-Regular SDF");
            theme.italicFont = LoadFont("EBGaramond-Italic SDF");

            theme.frame = LoadSprite("Cornice");
            theme.buttonFrame = LoadSprite("Pulsante_Cornice");
            theme.buttonFill = LoadSprite("Pulsante_Fondo");
            theme.divider = LoadSprite("Separatore");
            theme.parchmentTexture = LoadSprite("Pergamena");
            theme.emberDot = LoadSprite("Brace");
            theme.vignette = LoadSprite("Vignetta");
            theme.glow = LoadSprite("Bagliore");

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Selection.activeObject = theme;
            Debug.Log("[Valdorso] Tema UI e immagini generati in " + Folder);
        }

        // ---------- Disegni ----------

        static Color[] DrawFrame()
        {
            const int s = 128;
            Color[] px = NewCanvas(s, s);
            var c = new Vector2(64f, 64f);
            // Filo esterno spesso e filo interno sottile.
            DrawShape(px, s, s, p => Mathf.Abs(SdBox(p, c, new Vector2(57f, 57f))) - 1.0f, 1f);
            DrawShape(px, s, s, p => Mathf.Abs(SdBox(p, c, new Vector2(51f, 51f))) - 0.5f, 0.55f);
            // Gemme a rombo agli angoli, con un puntino verso l'interno.
            foreach (Vector2 corner in new[] { new Vector2(7f, 7f), new Vector2(121f, 7f), new Vector2(7f, 121f), new Vector2(121f, 121f) })
            {
                DrawShape(px, s, s, p => SdDiamond(p, corner, 8f), 1f);
                Vector2 inward = corner + new Vector2(corner.x < 64f ? 12f : -12f, corner.y < 64f ? 12f : -12f);
                DrawShape(px, s, s, p => (p - inward).magnitude - 1.6f, 0.8f);
            }
            return px;
        }

        static Color[] DrawButtonFrame()
        {
            const int s = 64;
            Color[] px = NewCanvas(s, s);
            var c = new Vector2(32f, 32f);
            DrawShape(px, s, s, p => Mathf.Abs(SdBox(p, c, new Vector2(29.5f, 29.5f))) - 0.75f, 0.9f);
            foreach (Vector2 corner in new[] { new Vector2(3f, 3f), new Vector2(61f, 3f), new Vector2(3f, 61f), new Vector2(61f, 61f) })
                DrawShape(px, s, s, p => SdDiamond(p, corner, 3.5f), 1f);
            return px;
        }

        static Color[] DrawButtonFill()
        {
            const int s = 64;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                float v = Mathf.Lerp(0.78f, 1f, y / (s - 1f)); // più chiaro in alto, come luce dall'alto
                for (int x = 0; x < s; x++) px[y * s + x] = new Color(v, v, v, 1f);
            }
            return px;
        }

        static Color[] DrawDivider()
        {
            const int w = 512, h = 32;
            Color[] px = NewCanvas(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float u = Mathf.Abs(p.x - w / 2f) / (w / 2f);                 // 0 al centro, 1 ai lati
                    float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.55f, u));
                    float line = Mathf.Clamp01(0.5f - (Mathf.Abs(p.y - h / 2f) - 0.75f));
                    Blend(px, w, x, y, line * fade);
                }
            }
            var center = new Vector2(w / 2f, h / 2f);
            DrawShape(px, w, h, p => SdDiamond(p, center, 7f), 1f);
            DrawShape(px, w, h, p => Mathf.Abs(SdDiamond(p, center, 11f)) - 0.6f, 0.8f);
            DrawShape(px, w, h, p => SdDiamond(p, center + new Vector2(-34f, 0f), 3.5f), 0.9f);
            DrawShape(px, w, h, p => SdDiamond(p, center + new Vector2(34f, 0f), 3.5f), 0.9f);
            return px;
        }

        static Color[] DrawParchment()
        {
            const int s = 512;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float grain = Fractal(x / 48f, y / 48f, 3);
                    float stains = Fractal(x / 170f + 13f, y / 170f + 7f, 11);
                    float v = 0.86f + 0.12f * grain - 0.09f * Mathf.SmoothStep(0.55f, 0.8f, stains);
                    float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(s - 1 - x, s - 1 - y)) / (s * 0.12f);
                    v *= 1f - 0.2f * (1f - Mathf.Clamp01(edge));                  // bordi più scuri, carta vecchia
                    px[y * s + x] = new Color(v, v, v, 1f);
                }
            }
            return px;
        }

        static Color[] DrawEmber()
        {
            const int s = 32;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = new Vector2(x + 0.5f - s / 2f, y + 0.5f - s / 2f).magnitude / (s / 2f);
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f));
                }
            }
            return px;
        }

        /// <summary>Cerchio sfumato: opacità da innerAlpha al centro a outerAlpha verso i bordi.</summary>
        static Color[] DrawRadial(int s, float innerAlpha, float outerAlpha, float start, float end, float normalize)
        {
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f;
                    float dy = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / normalize;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, d));
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Lerp(innerAlpha, outerAlpha, t));
                }
            }
            return px;
        }

        // ---------- Strumenti di disegno ----------

        static Color[] NewCanvas(int w, int h)
        {
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);
            return px;
        }

        /// <summary>Disegna una forma descritta dalla sua distanza con bordi morbidi (antialiasing).</summary>
        static void DrawShape(Color[] px, int w, int h, Func<Vector2, float> distance, float alpha)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float coverage = Mathf.Clamp01(0.5f - distance(new Vector2(x + 0.5f, y + 0.5f)));
                    if (coverage > 0f) Blend(px, w, x, y, coverage * alpha);
                }
        }

        static void Blend(Color[] px, int w, int x, int y, float alpha)
        {
            int i = y * w + x;
            if (alpha > px[i].a) px[i] = new Color(1f, 1f, 1f, alpha);
        }

        static float SdBox(Vector2 p, Vector2 center, Vector2 half)
        {
            float qx = Mathf.Abs(p.x - center.x) - half.x;
            float qy = Mathf.Abs(p.y - center.y) - half.y;
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f);
        }

        static float SdDiamond(Vector2 p, Vector2 center, float radius) =>
            (Mathf.Abs(p.x - center.x) + Mathf.Abs(p.y - center.y) - radius) * 0.7071f;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float tx = x - xi, ty = y - yi;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Mathf.Lerp(Hash(xi, yi, seed), Hash(xi + 1, yi, seed), tx);
            float b = Mathf.Lerp(Hash(xi, yi + 1, seed), Hash(xi + 1, yi + 1, seed), tx);
            return Mathf.Lerp(a, b, ty);
        }

        static float Fractal(float x, float y, int seed)
        {
            float sum = 0f, amplitude = 0.5f, frequency = 1f;
            for (int octave = 0; octave < 5; octave++)
            {
                sum += amplitude * ValueNoise(x * frequency, y * frequency, seed + octave * 31);
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return sum;
        }

        // ---------- Salvataggio e caricamento ----------

        static void Save(string name, Color[] pixels, int w, int h, Vector4 border)
        {
            string assetPath = $"{Folder}/{name}.png";
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        static TMP_FontAsset LoadFont(string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + name + ".asset");
            if (font == null) Debug.LogError($"[Valdorso] Font Asset non trovato: {FontFolder}{name}.asset");
            return font;
        }
    }
}
