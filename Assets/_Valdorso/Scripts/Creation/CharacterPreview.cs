using UnityEngine;
using UnityEngine.InputSystem;

namespace Valdorso.Creation
{
    /// <summary>
    /// Il personaggio al centro della scena di creazione: resta in piedi fermo
    /// e si gira trascinando con il tasto sinistro del mouse nella parte destra dello schermo
    /// (a sinistra ci sarà il registro con le scelte).
    /// </summary>
    public class CharacterPreview : MonoBehaviour
    {
        [Tooltip("Gradi di rotazione per ogni pixel di trascinamento")]
        [SerializeField] float dragSpeed = 0.35f;
        [Tooltip("Il trascinamento vale solo a destra di questo punto dello schermo (0 = bordo sinistro, 1 = bordo destro)")]
        [SerializeField, Range(0f, 1f)] float dragAreaStartX = 0.42f;

        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int MotionSpeedId = Animator.StringToHash("MotionSpeed");

        Animator lastAnimator;
        bool hasGrounded, hasMotionSpeed;
        float yaw;
        bool dragging;

        void Start()
        {
            yaw = transform.eulerAngles.y;
        }

        void Update()
        {
            KeepStanding();
            HandleDrag();
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // UMA ricrea l'Animator a ogni costruzione: lo si cerca ogni volta.
        // Senza "Grounded" il controller dei movimenti crederebbe di essere in aria.
        void KeepStanding()
        {
            Animator animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;

            if (animator != lastAnimator)
            {
                lastAnimator = animator;
                animator.applyRootMotion = false;
                hasGrounded = HasParameter(animator, "Grounded", AnimatorControllerParameterType.Bool);
                hasMotionSpeed = HasParameter(animator, "MotionSpeed", AnimatorControllerParameterType.Float);
            }
            if (hasGrounded) animator.SetBool(GroundedId, true);
            if (hasMotionSpeed) animator.SetFloat(MotionSpeedId, 1f);
        }

        void HandleDrag()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame && mouse.position.ReadValue().x >= Screen.width * dragAreaStartX)
                dragging = true;
            if (!mouse.leftButton.isPressed)
                dragging = false;
            if (dragging)
                yaw -= mouse.delta.ReadValue().x * dragSpeed;
        }

        static bool HasParameter(Animator animator, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name && p.type == type) return true;
            return false;
        }
    }
}
