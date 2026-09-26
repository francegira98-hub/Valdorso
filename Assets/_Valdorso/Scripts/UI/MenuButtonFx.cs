using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Valdorso.UI
{
    /// <summary>
    /// Effetto dei pulsanti "Oro e brace": sotto il mouse la cornice si accende,
    /// la scritta diventa oro chiaro e il pulsante si ingrandisce appena, con un movimento calmo.
    /// </summary>
    public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] ValdorsoTheme theme;
        [SerializeField] TMP_Text label;
        [SerializeField] Image frame;

        Button button;
        bool hover;
        float amount; // 0 = a riposo, 1 = sotto il mouse

        void Awake()
        {
            button = GetComponent<Button>();
        }

        void OnDisable()
        {
            hover = false;
            amount = 0f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData) => hover = true;
        public void OnPointerExit(PointerEventData eventData) => hover = false;

        void Update()
        {
            if (theme == null) return;
            bool active = hover && (button == null || button.interactable);
            float speed = 1f / Mathf.Max(0.01f, theme.fadeDuration);
            amount = Mathf.MoveTowards(amount, active ? 1f : 0f, speed * Time.unscaledDeltaTime);
            Apply();
        }

        void Apply()
        {
            if (theme == null) return;
            float scale = Mathf.Lerp(1f, theme.hoverScale, amount);
            transform.localScale = new Vector3(scale, scale, 1f);
            if (label != null) label.color = Color.Lerp(theme.text, theme.goldLight, amount);
            if (frame != null)
            {
                Color c = theme.gold;
                c.a = Mathf.Lerp(0.55f, 1f, amount);
                frame.color = c;
            }
        }
    }
}
