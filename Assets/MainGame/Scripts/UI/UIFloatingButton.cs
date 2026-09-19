using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MainGame.UI.Unified;

namespace MainGame.UI
{
    /// <summary>
    /// Universal floating / bobbing effect component that gives any UI Button or RectTransform
    /// an organic, zero-gravity floating feel with zero transform drift.
    /// Supports:
    /// - Continuous sinusoidal bobbing and gentle tilt.
    /// - Staggered phase offset per button position to create natural waves.
    /// - Elevated lift and subtle scale pulse on hover and controller focus.
    /// - Tactile dip on pointer press.
    /// - Unscaled time support for running while paused.
    /// - Automatic conflict detection with MainMenuButtonEnergyAnimator and UIAnimatedButton.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIFloatingButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        #region Inspector Fields

        [Header("Target Visual")]
        [Tooltip("The RectTransform to animate. If null, uses this GameObject's RectTransform.")]
        [SerializeField] private RectTransform m_Target;

        [Header("Floating Parameters")]
        [Tooltip("Master switch to enable or disable floating.")]
        [SerializeField] private bool m_EnableFloating = true;

        [Tooltip("Vertical floating bob amplitude in pixels.")]
        [SerializeField] private float m_FloatAmplitude = 4f;

        [Tooltip("Floating oscillation speed in radians/sec.")]
        [SerializeField] private float m_FloatSpeed = 2.2f;

        [Tooltip("Subtle angular tilt rocking in degrees.")]
        [SerializeField] private float m_TiltAngle = 0.8f;

        [Tooltip("Subtle horizontal floating displacement in pixels.")]
        [SerializeField] private float m_HorizontalAmplitude = 0f;

        [Header("Focus & Interaction Lift")]
        [Tooltip("Extra upward elevation in pixels when hovered or selected.")]
        [SerializeField] private float m_FocusLift = 3.5f;

        [Tooltip("Scale multiplier applied when hovered or selected.")]
        [SerializeField] private float m_FocusScale = 1.03f;

        [Tooltip("Depression depth in pixels when pressed down.")]
        [SerializeField] private float m_PressDip = 2.5f;

        [Tooltip("Scale multiplier applied when pressed down.")]
        [SerializeField] private float m_PressScale = 0.95f;

        [Header("Timing & Options")]
        [Tooltip("Use unscaled time so buttons float even when game is paused (Time.timeScale == 0).")]
        [SerializeField] private bool m_UseUnscaledTime = true;

        [Tooltip("Auto-calculate phase from position and sibling index to prevent buttons moving in rigid unison.")]
        [SerializeField] private bool m_AutoPhase = true;

        [Tooltip("Manual phase offset in radians (used when AutoPhase is false).")]
        [SerializeField] private float m_CustomPhase = 0f;

        [Tooltip("If true, non-interactable buttons continue gentle ambient floating.")]
        [SerializeField] private bool m_FloatWhenNonInteractable = true;

        #endregion

        #region Private Fields

        private Vector2 m_RestPosition;
        private Vector3 m_RestScale = Vector3.one;
        private Vector3 m_RestRotation = Vector3.zero;
        private bool m_HasCapturedRestState = false;

        private float m_PhaseOffset;
        private float m_CurrentLift = 0f;
        private float m_CurrentScaleMultiplier = 1f;
        private bool m_IsFocused = false;
        private bool m_IsPressed = false;
        private float m_SuppressedUntilTime = 0f;

        private Selectable m_Selectable;

        #endregion

        #region Properties

        public bool EnableFloating
        {
            get => m_EnableFloating;
            set
            {
                m_EnableFloating = value;
                if (!value) ResetToRestState();
            }
        }

        public float FloatAmplitude { get => m_FloatAmplitude; set => m_FloatAmplitude = value; }
        public float FloatSpeed { get => m_FloatSpeed; set => m_FloatSpeed = value; }
        public float TiltAngle { get => m_TiltAngle; set => m_TiltAngle = value; }
        public float FocusLift { get => m_FocusLift; set => m_FocusLift = value; }
        public bool IsFocused => m_IsFocused;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // If another specialized animator is attached, disable this component to prevent duplicate transforms
            if (GetComponent<MainMenuButtonEnergyAnimator>() != null || GetComponent<UIAnimatedButton>() != null)
            {
                enabled = false;
                return;
            }

            if (m_Target == null)
            {
                m_Target = GetComponent<RectTransform>();
            }

            m_Selectable = GetComponent<Selectable>();
            CaptureRestState();
        }

        private void Start()
        {
            CaptureRestState();
        }

        private void OnEnable()
        {
            CaptureRestState();
            m_IsPressed = false;
        }

        private void OnDisable()
        {
            ResetToRestState();
            m_IsFocused = false;
            m_IsPressed = false;
        }

        private void Update()
        {
            if (!m_EnableFloating || m_Target == null) return;

            float dt = m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float time = m_UseUnscaledTime ? Time.unscaledTime : Time.time;

            if (time < m_SuppressedUntilTime)
            {
                // Smoothly ease back toward rest position during suppression
                m_CurrentLift = Mathf.Lerp(m_CurrentLift, 0f, dt * 10f);
                m_CurrentScaleMultiplier = Mathf.Lerp(m_CurrentScaleMultiplier, 1f, dt * 10f);
                ApplyTransform(0f, 0f, 0f);
                return;
            }

            bool isInteractable = m_Selectable == null || m_Selectable.interactable;
            if (!isInteractable && !m_FloatWhenNonInteractable)
            {
                m_CurrentLift = Mathf.Lerp(m_CurrentLift, 0f, dt * 8f);
                m_CurrentScaleMultiplier = Mathf.Lerp(m_CurrentScaleMultiplier, 1f, dt * 8f);
                ApplyTransform(0f, 0f, 0f);
                return;
            }

            // Determine target lift and scale based on input states
            float targetLift = 0f;
            float targetScale = 1f;

            if (isInteractable)
            {
                if (m_IsPressed)
                {
                    targetLift = -m_PressDip;
                    targetScale = m_PressScale;
                }
                else if (m_IsFocused)
                {
                    targetLift = m_FocusLift;
                    targetScale = m_FocusScale;
                }
            }

            m_CurrentLift = Mathf.Lerp(m_CurrentLift, targetLift, dt * 10f);
            m_CurrentScaleMultiplier = Mathf.Lerp(m_CurrentScaleMultiplier, targetScale, dt * 10f);

            // Compute sinusoidal floating oscillation
            float wave = Mathf.Sin(time * m_FloatSpeed + m_PhaseOffset);
            float yBob = (wave * m_FloatAmplitude) + m_CurrentLift;
            float xBob = m_HorizontalAmplitude > 0.01f ? Mathf.Cos(time * (m_FloatSpeed * 0.7f) + m_PhaseOffset) * m_HorizontalAmplitude : 0f;
            float tilt = m_TiltAngle > 0.01f ? Mathf.Cos(time * (m_FloatSpeed * 0.85f) + m_PhaseOffset) * m_TiltAngle : 0f;

            ApplyTransform(xBob, yBob, tilt);
        }

        #endregion

        #region Transform & State Management

        public void CaptureRestState()
        {
            if (m_Target == null) m_Target = GetComponent<RectTransform>();
            if (m_Target == null) return;

            if (!m_HasCapturedRestState)
            {
                m_RestPosition = m_Target.anchoredPosition;
                m_RestScale = m_Target.localScale;
                m_RestRotation = m_Target.localEulerAngles;
                m_HasCapturedRestState = true;
                m_HasAppliedTransform = false;
            }

            if (m_AutoPhase)
            {
                m_PhaseOffset = (m_RestPosition.x * 0.015f) + (m_RestPosition.y * -0.025f) + (transform.GetSiblingIndex() * 0.55f);
            }
            else
            {
                m_PhaseOffset = m_CustomPhase;
            }
        }

        public void ResetToRestState()
        {
            if (!m_HasCapturedRestState || m_Target == null) return;

            m_Target.anchoredPosition = m_RestPosition;
            m_Target.localScale = m_RestScale;
            m_Target.localEulerAngles = m_RestRotation;
            m_CurrentLift = 0f;
            m_CurrentScaleMultiplier = 1f;
            m_HasAppliedTransform = false;
        }

        // Last values actually pushed to the transform. Every write here marks the RectTransform
        // dirty and forces the Canvas to re-batch this button, and localEulerAngles additionally
        // pays a euler-to-quaternion conversion. Once the easing has converged -- at rest, or while
        // suppressed and already settled -- the computed values stop changing, and re-writing the
        // identical value each frame keeps the canvas permanently dirty for no visible difference.
        private Vector2 m_LastAppliedPosition;
        private Vector3 m_LastAppliedScale;
        private Vector3 m_LastAppliedEuler;
        private bool m_HasAppliedTransform;

        private const float ApplyEpsilonSqr = 1e-8f;

        private void ApplyTransform(float xOffset, float yOffset, float tiltOffset)
        {
            if (m_Target == null) return;

            Vector2 position = new Vector2(m_RestPosition.x + xOffset, m_RestPosition.y + yOffset);
            Vector3 scale = new Vector3(
                m_RestScale.x * m_CurrentScaleMultiplier,
                m_RestScale.y * m_CurrentScaleMultiplier,
                m_RestScale.z
            );
            Vector3 euler = new Vector3(
                m_RestRotation.x,
                m_RestRotation.y,
                m_RestRotation.z + tiltOffset
            );

            if (!m_HasAppliedTransform)
            {
                m_Target.anchoredPosition = position;
                m_Target.localScale = scale;
                m_Target.localEulerAngles = euler;
                m_LastAppliedPosition = position;
                m_LastAppliedScale = scale;
                m_LastAppliedEuler = euler;
                m_HasAppliedTransform = true;
                return;
            }

            if ((position - m_LastAppliedPosition).sqrMagnitude > ApplyEpsilonSqr)
            {
                m_Target.anchoredPosition = position;
                m_LastAppliedPosition = position;
            }

            if ((scale - m_LastAppliedScale).sqrMagnitude > ApplyEpsilonSqr)
            {
                m_Target.localScale = scale;
                m_LastAppliedScale = scale;
            }

            if ((euler - m_LastAppliedEuler).sqrMagnitude > ApplyEpsilonSqr)
            {
                m_Target.localEulerAngles = euler;
                m_LastAppliedEuler = euler;
            }
        }

        /// <summary>
        /// Temporarily suppresses floating for a given duration (useful during entrance or punch animations).
        /// </summary>
        public void Suppress(float duration)
        {
            float now = m_UseUnscaledTime ? Time.unscaledTime : Time.time;
            m_SuppressedUntilTime = Mathf.Max(m_SuppressedUntilTime, now + duration);
        }

        #endregion

        #region Event Handlers

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_IsFocused = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Only clear focus if EventSystem isn't currently selecting this object
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != gameObject)
            {
                m_IsFocused = false;
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            m_IsFocused = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            m_IsFocused = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_IsPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            m_IsPressed = false;
        }

        #endregion

        #region Global Registration & Scene Hooks

        /// <summary>
        /// Attaches UIFloatingButton to all UI Buttons within the specified root hierarchy
        /// that don't already have an active button animator.
        /// </summary>
        public static void AttachToAllButtonsIn(GameObject root)
        {
            if (root == null) return;

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn == null) continue;

                if (btn.GetComponent<MainMenuButtonEnergyAnimator>() != null ||
                    btn.GetComponent<UIAnimatedButton>() != null ||
                    btn.GetComponent<UIFloatingButton>() != null)
                {
                    continue;
                }

                btn.gameObject.AddComponent<UIFloatingButton>();
            }
        }

        /// <summary>
        /// Automatic scene hook that ensures every Button in newly loaded scenes is equipped with floating.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeSceneHook()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToActiveScene();
        }

        private static void ApplyToActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                AttachToAllButtonsIn(roots[i]);
            }
        }

        #endregion
    }
}
