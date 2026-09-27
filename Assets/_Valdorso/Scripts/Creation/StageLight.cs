using UnityEngine;

namespace Valdorso.Creation
{
    /// <summary>
    /// Le luci vive della scena di creazione.
    /// Heartbeat: il battito del frammento del Cuore del Mondo (due colpi ravvicinati, poi una pausa).
    /// Flicker: il tremolio del fuoco dei bracieri.
    /// Può far brillare insieme alla luce anche un materiale (il cristallo, le rune del pavimento)
    /// e far fluttuare e ruotare lentamente un oggetto.
    /// </summary>
    public class StageLight : MonoBehaviour
    {
        public enum Mode { Heartbeat, Flicker }

        [SerializeField] Mode mode = Mode.Flicker;

        [Header("Luce (facoltativa)")]
        [SerializeField] Light target;
        [SerializeField] float baseIntensity = 2f;
        [Tooltip("Quanto sale la luce a ogni battito, o quanto trema il fuoco")]
        [SerializeField] float variation = 0.6f;
        [Tooltip("Battiti al minuto (solo Heartbeat)")]
        [SerializeField] float beatsPerMinute = 48f;

        [Header("Materiale che brilla insieme alla luce (facoltativo)")]
        [SerializeField] Renderer glowRenderer;
        [SerializeField, ColorUsage(false, true)] Color glowColor = new Color(2f, 1.1f, 0.35f);
        [Tooltip("Luminosità del materiale tra un battito e l'altro (1 = piena)")]
        [SerializeField, Range(0f, 1f)] float glowRest = 0.55f;

        [Header("Movimento (facoltativo)")]
        [SerializeField] Transform floating;
        [SerializeField] float floatHeight = 0.05f;
        [Tooltip("Gradi al secondo")]
        [SerializeField] float spinSpeed = 10f;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        Vector3 floatingStart;
        float seed;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (floating != null) floatingStart = floating.localPosition;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time;
            // k va da 0 (riposo) a 1 (picco); per il fuoco oscilla intorno a 0.5
            float k = mode == Mode.Heartbeat ? Heartbeat(t) : Flicker(t);

            if (target != null)
                target.intensity = Mathf.Max(0f, baseIntensity + variation * (mode == Mode.Heartbeat ? k : (k - 0.5f) * 2f));

            if (glowRenderer != null)
            {
                glowRenderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, glowColor * Mathf.Lerp(glowRest, 1f, k));
                glowRenderer.SetPropertyBlock(block);
            }

            if (floating != null)
            {
                floating.localPosition = floatingStart + Vector3.up * (Mathf.Sin(t * 0.8f) * floatHeight);
                floating.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            }
        }

        float Heartbeat(float t)
        {
            float period = 60f / Mathf.Max(1f, beatsPerMinute);
            float p = (t % period) / period;
            return Mathf.Clamp01(Pulse(p, 0f, 0.1f) + 0.65f * Pulse(p, 0.17f, 0.12f));
        }

        static float Pulse(float p, float start, float length)
        {
            float x = (p - start) / length;
            return x < 0f || x > 1f ? 0f : Mathf.Sin(x * Mathf.PI);
        }

        float Flicker(float t)
        {
            float slow = Mathf.PerlinNoise(seed, t * 2.5f);
            float fast = Mathf.PerlinNoise(seed + 7f, t * 11f);
            return Mathf.Clamp01(0.65f * slow + 0.35f * fast);
        }
    }
}
