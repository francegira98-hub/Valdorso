using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Valdorso.UI
{
    /// <summary>
    /// Braci che salgono lentamente, oscillano e si spengono tremolando.
    /// Va messo su un rettangolo dell'interfaccia che copre la zona dove devono apparire.
    /// Leggero: poche immagini riciclate, nessun sistema di particelle.
    /// </summary>
    public class UIEmbers : MonoBehaviour
    {
        [SerializeField] ValdorsoTheme theme;
        [Tooltip("Quante braci contemporaneamente")]
        [SerializeField] int count = 55;
        [Tooltip("Velocità di salita (minima e massima), in pixel al secondo")]
        [SerializeField] Vector2 speedRange = new Vector2(20f, 60f);
        [Tooltip("Grandezza (minima e massima), in pixel")]
        [SerializeField] Vector2 sizeRange = new Vector2(4f, 13f);
        [Tooltip("Quanto oscillano a destra e a sinistra, in pixel")]
        [SerializeField] float sway = 22f;

        class Ember
        {
            public RectTransform rect;
            public Image image;
            public Color color;
            public float x, speed, phase, swayAmount, life, age;
        }

        readonly List<Ember> embers = new List<Ember>();
        RectTransform area;

        void Start()
        {
            if (theme == null || theme.emberDot == null)
            {
                enabled = false;
                return;
            }

            area = (RectTransform)transform;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Brace", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(area, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                var image = go.GetComponent<Image>();
                image.sprite = theme.emberDot;
                image.raycastTarget = false;

                var ember = new Ember { rect = rect, image = image };
                Respawn(ember, true);
                embers.Add(ember);
            }
        }

        void Respawn(Ember e, bool randomAge)
        {
            Rect r = area.rect;
            e.x = Random.Range(r.xMin, r.xMax);
            e.speed = Random.Range(speedRange.x, speedRange.y);
            e.phase = Random.value * Mathf.PI * 2f;
            e.swayAmount = Random.Range(0.3f, 1f) * sway;
            e.life = r.height * Random.Range(0.35f, 0.9f) / e.speed;
            e.age = randomAge ? Random.Range(0f, e.life) : 0f;
            e.color = Color.Lerp(theme.ember, theme.goldLight, Random.value * 0.6f);
            float size = Random.Range(sizeRange.x, sizeRange.y);
            e.rect.sizeDelta = new Vector2(size, size);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Rect r = area.rect;

            foreach (Ember e in embers)
            {
                e.age += dt;
                if (e.age >= e.life) Respawn(e, false);

                float t = e.age / e.life;
                float x = e.x + Mathf.Sin(e.phase + e.age * 1.3f) * e.swayAmount;
                float y = r.yMin + e.speed * e.age;
                e.rect.anchoredPosition = new Vector2(x, y);

                // Si accende all'inizio, si spegne alla fine, e intanto tremola come una brace vera.
                float fade = Mathf.SmoothStep(0f, 1f, t / 0.15f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
                float flicker = 0.75f + 0.25f * Mathf.Sin(e.age * 9f + e.phase * 3f);
                Color c = e.color;
                c.a = fade * flicker * 0.9f;
                e.image.color = c;
            }
        }
    }
}
