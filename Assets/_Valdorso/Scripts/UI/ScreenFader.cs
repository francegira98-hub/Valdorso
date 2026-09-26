using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Valdorso.UI
{
    /// <summary>
    /// Il "sipario" del gioco: uno schermo nero che sfuma.
    /// Si crea da solo all'avvio, sopravvive ai cambi di scena e fa comparire ogni scena dal nero.
    /// Per chiudere il sipario prima di un cambio: ScreenFader.Instance.FadeOut(durata, azione da fare dopo).
    /// Sul server senza grafica non viene creato.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        const float FadeInDuration = 0.8f;

        Image veil;
        Coroutine running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if (Application.isBatchMode) return;
            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ScreenFader>();
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // sopra a tutto

            var veilObject = new GameObject("Velo", typeof(RectTransform), typeof(Image));
            veilObject.transform.SetParent(transform, false);
            var rect = (RectTransform)veilObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            veil = veilObject.GetComponent<Image>();
            veil.color = Color.black;
            veil.raycastTarget = false;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start()
        {
            // All'avvio la prima scena è già caricata e il segnale "scena caricata" può non arrivare:
            // il sipario si apre comunque.
            Fade(veil.color.a, 0f, FadeInDuration, null);
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Fade(1f, 0f, FadeInDuration, null);
        }

        /// <summary>Riapre il sipario (per esempio se l'ingresso nel mondo è fallito).</summary>
        public void FadeIn(float duration = FadeInDuration)
        {
            Fade(veil.color.a, 0f, duration, null);
        }

        /// <summary>Sfuma verso il nero, poi (se indicata) esegue un'azione.</summary>
        public void FadeOut(float duration, Action then = null)
        {
            Fade(veil.color.a, 1f, duration, then);
        }

        void Fade(float from, float to, float duration, Action then)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FadeRoutine(from, to, duration, then));
        }

        IEnumerator FadeRoutine(float from, float to, float duration, Action then)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, time / duration)));
                yield return null;
            }
            SetAlpha(to);
            running = null;
            then?.Invoke();
        }

        void SetAlpha(float alpha)
        {
            Color c = veil.color;
            c.a = alpha;
            veil.color = c;
        }
    }
}
