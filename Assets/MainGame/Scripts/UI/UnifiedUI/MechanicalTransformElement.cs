using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using MainGame.UI.Feedback;
using MainGame.UI.CinematicEffects;

namespace MainGame.UI.Unified
{
    public enum MechanicalMechanismType
    {
        RotatingArm,
        SlidingChassisPanel,
        HingedFoldOut,
        CornerPivotSwing,
        PneumaticExtension,
        HeavyDualRailPlunge
    }

    public enum MechanicalWeight
    {
        Light,
        Medium,
        Heavy,
        VeryHeavy
    }

    /// <summary>
    /// Governs the motion and cinematic visual layer for UI signboard elements.
    /// Preserves the clean pixel-art signboard artwork as the hero visual,
    /// while adding:
    /// - Pre-movement electrical flicker and border energy activation
    /// - Velocity energy trails during flight
    /// - Kinetic lock impact with flash, border pulse, and spark bursts
    /// - Reversible rest-state coordinates (zero drift)
    /// </summary>
    [DisallowMultipleComponent]
    public class MechanicalTransformElement : MonoBehaviour
    {
        [Header("Mechanical Profile")]
        [SerializeField] private MechanicalMechanismType m_Mechanism = MechanicalMechanismType.SlidingChassisPanel;
        [SerializeField] private MechanicalWeight m_Weight = MechanicalWeight.Medium;

        [Header("Secondary Components (Inertial Reaction)")]
        [Tooltip("Child element that experiences inertial lag during movement (e.g. Select Icon, text, or bolt detail).")]
        [SerializeField] private RectTransform m_SecondaryAccent;

        // Baseline Rest State (Permanently captured to eliminate any transform drift)
        private RectTransform m_Rect;
        private Vector2 m_RestAnchoredPos;
        private Vector3 m_RestLocalScale = Vector3.one;
        private Vector3 m_RestEulerAngles = Vector3.zero;
        private Vector2 m_RestPivot = new Vector2(0.5f, 0.5f);

        private Vector2 m_SecondaryRestPos;
        private bool m_HasSecondaryRest = false;
        private bool m_HasCapturedRest = false;

        private Coroutine m_ActiveMotionRoutine;
        private Coroutine m_SecondaryRoutine;

        private CinematicUIEffect m_CinematicUI;

        public MechanicalMechanismType Mechanism
        {
            get => m_Mechanism;
            set => m_Mechanism = value;
        }

        public MechanicalWeight Weight
        {
            get => m_Weight;
            set => m_Weight = value;
        }

        public RectTransform RectTransformComponent
        {
            get
            {
                if (m_Rect == null) m_Rect = GetComponent<RectTransform>();
                return m_Rect;
            }
        }

        public CinematicUIEffect CinematicUI
        {
            get
            {
                if (m_CinematicUI == null)
                {
                    Graphic g = GetComponent<Graphic>() ?? GetComponentInChildren<Graphic>();
                    if (g != null)
                    {
                        m_CinematicUI = g.GetComponent<CinematicUIEffect>() ?? g.gameObject.AddComponent<CinematicUIEffect>();
                    }
                }
                return m_CinematicUI;
            }
        }

        public bool IsAnimating => m_ActiveMotionRoutine != null;

        private void Awake()
        {
            CaptureRestState();
            // Cleanup any legacy armature objects in the parent
            if (transform.parent != null)
            {
                Transform legacyArm = transform.parent.Find($"__Armature_{gameObject.name}");
                if (legacyArm != null) Destroy(legacyArm.gameObject);
            }
        }

        private void OnDisable()
        {
            KillMotion();
        }

        public void CaptureRestState()
        {
            if (m_HasCapturedRest) return;

            if (m_Rect == null) m_Rect = GetComponent<RectTransform>();
            if (m_Rect != null)
            {
                m_RestAnchoredPos = m_Rect.anchoredPosition;
                m_RestLocalScale = m_Rect.localScale;
                m_RestEulerAngles = m_Rect.localEulerAngles;
                m_RestPivot = m_Rect.pivot;
            }

            if (m_SecondaryAccent == null)
            {
                Transform selectIcon = transform.Find("Select Icon") ?? transform.Find("pointer") ?? transform.Find("Accent");
                if (selectIcon != null) m_SecondaryAccent = selectIcon as RectTransform;
            }

            if (m_SecondaryAccent != null)
            {
                m_SecondaryRestPos = m_SecondaryAccent.anchoredPosition;
                m_HasSecondaryRest = true;
            }

            _ = CinematicUI; // Ensure initialized
            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            KillMotion();
            CaptureRestState();

            if (m_Rect != null)
            {
                m_Rect.pivot = m_RestPivot;
                m_Rect.anchoredPosition = m_RestAnchoredPos;
                m_Rect.localScale = m_RestLocalScale;
                m_Rect.localEulerAngles = m_RestEulerAngles;
            }

            if (m_SecondaryAccent != null && m_HasSecondaryRest)
            {
                m_SecondaryAccent.anchoredPosition = m_SecondaryRestPos;
                m_SecondaryAccent.localEulerAngles = Vector3.zero;
                m_SecondaryAccent.localScale = Vector3.one;
            }

            if (CinematicUI != null)
            {
                CinematicUI.ResetToIdle();
            }
        }

        public void KillMotion()
        {
            if (m_ActiveMotionRoutine != null)
            {
                StopCoroutine(m_ActiveMotionRoutine);
                m_ActiveMotionRoutine = null;
            }

            if (m_SecondaryRoutine != null)
            {
                StopCoroutine(m_SecondaryRoutine);
                m_SecondaryRoutine = null;
            }

            if (CinematicUI != null)
            {
                CinematicUI.ResetToIdle();
            }
        }

        public void PrepareRetractedState()
        {
            PrepareDormantState();
        }

        public void PrepareDormantState()
        {
            CaptureRestState();

            switch (m_Mechanism)
            {
                case MechanicalMechanismType.RotatingArm:
                    m_Rect.pivot = new Vector2(0.85f, 0.90f);
                    m_Rect.anchoredPosition = m_RestAnchoredPos + new Vector2(420f, 380f);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, -28f);
                    m_Rect.localScale = new Vector3(0.92f, 0.92f, 1f);
                    break;

                case MechanicalMechanismType.SlidingChassisPanel:
                    m_Rect.pivot = m_RestPivot;
                    m_Rect.anchoredPosition = m_RestAnchoredPos + new Vector2(680f, 0f);
                    m_Rect.localEulerAngles = Vector3.zero;
                    m_Rect.localScale = new Vector3(0.85f, 1.0f, 1f);
                    break;

                case MechanicalMechanismType.HingedFoldOut:
                    m_Rect.pivot = new Vector2(0.15f, 0.85f);
                    m_Rect.anchoredPosition = m_RestAnchoredPos + new Vector2(180f, -260f);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, -38f);
                    m_Rect.localScale = new Vector3(0.68f, 0.68f, 1f);
                    break;

                case MechanicalMechanismType.CornerPivotSwing:
                    m_Rect.pivot = new Vector2(0.08f, 0.92f);
                    m_Rect.anchoredPosition = m_RestAnchoredPos + new Vector2(-40f, 320f);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, 42f);
                    m_Rect.localScale = new Vector3(0.80f, 0.80f, 1f);
                    break;

                case MechanicalMechanismType.PneumaticExtension:
                    m_Rect.pivot = m_RestPivot;
                    m_Rect.anchoredPosition = m_RestAnchoredPos + new Vector2(0f, -540f);
                    m_Rect.localEulerAngles = Vector3.zero;
                    m_Rect.localScale = new Vector3(1.0f, 0.78f, 1f);
                    break;

                case MechanicalMechanismType.HeavyDualRailPlunge:
                    m_Rect.pivot = m_RestPivot;
                    m_Rect.anchoredPosition = m_RestAnchoredPos + new Vector2(0f, 850f);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, -5f);
                    m_Rect.localScale = new Vector3(0.92f, 1.08f, 1f);
                    break;
            }

            if (m_SecondaryAccent != null && m_HasSecondaryRest)
            {
                m_SecondaryAccent.anchoredPosition = m_SecondaryRestPos;
            }
        }

        public void Deploy(Action onLocked = null)
        {
            KillMotion();
            CaptureRestState();
            m_ActiveMotionRoutine = StartCoroutine(DeployKinematicsRoutine(onLocked));
        }

        public void Retract(Action onRetracted = null)
        {
            KillMotion();
            CaptureRestState();
            m_ActiveMotionRoutine = StartCoroutine(RetractKinematicsRoutine(onRetracted));
        }

        public void PlaySelectionLockPunch(Action onComplete = null)
        {
            KillMotion();
            CaptureRestState();
            m_ActiveMotionRoutine = StartCoroutine(SelectionLockPunchRoutine(onComplete));
        }

        private Color GetMechanismColor()
        {
            return (m_Mechanism == MechanicalMechanismType.HeavyDualRailPlunge)
                ? new Color(1.0f, 0.45f, 0.20f, 1f)
                : new Color(0.35f, 0.85f, 1.0f, 1f);
        }

        private IEnumerator DeployKinematicsRoutine(Action onLocked)
        {
            Color pulseCol = GetMechanismColor();

            // Pre-movement electrical flicker
            if (CinematicUI != null)
            {
                CinematicUI.TriggerBorderPulse(0.12f, pulseCol, 2.0f);
            }
            yield return new WaitForSecondsRealtime(0.04f);

            switch (m_Mechanism)
            {
                case MechanicalMechanismType.RotatingArm:
                    yield return StartCoroutine(DeployRotatingArmRoutine(pulseCol));
                    break;

                case MechanicalMechanismType.SlidingChassisPanel:
                    yield return StartCoroutine(DeploySlidingChassisPanelRoutine(pulseCol));
                    break;

                case MechanicalMechanismType.HingedFoldOut:
                    yield return StartCoroutine(DeployHingedFoldOutRoutine(pulseCol));
                    break;

                case MechanicalMechanismType.CornerPivotSwing:
                    yield return StartCoroutine(DeployCornerPivotSwingRoutine(pulseCol));
                    break;

                case MechanicalMechanismType.PneumaticExtension:
                    yield return StartCoroutine(DeployPneumaticExtensionRoutine(pulseCol));
                    break;

                case MechanicalMechanismType.HeavyDualRailPlunge:
                    yield return StartCoroutine(DeployHeavyDualRailPlungeRoutine(pulseCol));
                    break;
            }

            // Restore strict base pivot and coordinates
            if (m_Rect != null)
            {
                m_Rect.pivot = m_RestPivot;
                m_Rect.anchoredPosition = m_RestAnchoredPos;
                m_Rect.localScale = m_RestLocalScale;
                m_Rect.localEulerAngles = m_RestEulerAngles;
            }

            if (m_SecondaryAccent != null && m_HasSecondaryRest)
            {
                m_SecondaryAccent.anchoredPosition = m_SecondaryRestPos;
            }

            m_ActiveMotionRoutine = null;
            onLocked?.Invoke();
        }

        #region Mechanism Routines

        private IEnumerator DeployRotatingArmRoutine(Color pulseCol)
        {
            Vector2 startPos = m_RestAnchoredPos + new Vector2(420f, 380f);
            Vector2 overshootPos = m_RestAnchoredPos + new Vector2(-12f, -14f);

            m_Rect.pivot = new Vector2(0.85f, 0.90f);
            m_Rect.anchoredPosition = startPos;
            m_Rect.localEulerAngles = new Vector3(0f, 0f, -28f);
            m_Rect.localScale = new Vector3(0.92f, 0.92f, 1f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.45f, 0.04f);

            float travelDur = 0.30f;
            float elapsed = 0f;
            Vector2 prevPos = startPos;

            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-28f, 5f, ease));
                    m_Rect.localScale = Vector3.Lerp(new Vector3(0.92f, 0.92f, 1f), new Vector3(1.04f, 1.04f, 1f), ease);

                    if (Time.frameCount % 3 == 0 && CinematicUIParticleSystem.Instance != null)
                    {
                        Vector2 vel = (m_Rect.anchoredPosition - prevPos) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                        if (vel.sqrMagnitude > 400f)
                        {
                            CinematicUIParticleSystem.Instance.SpawnEnergyTrail(m_Rect.anchoredPosition, transform.parent, pulseCol, vel);
                        }
                    }
                    prevPos = m_Rect.anchoredPosition;
                }
                yield return null;
            }

            // Lock impact
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.75f, 0.03f);
            UIMicroShake.Shake(0.35f, 0.05f);
            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 1.8f);
                CinematicUI.TriggerBorderPulse(0.24f, pulseCol);
            }
            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_Rect, pulseCol, 5, 18f);
            }

            // Settle
            elapsed = 0f;
            float settleDur = 0.12f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.Lerp(overshootPos, m_RestAnchoredPos, ease);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(5f, 0f, ease));
                    m_Rect.localScale = Vector3.Lerp(new Vector3(1.04f, 0.96f, 1f), m_RestLocalScale, ease);
                }
                yield return null;
            }
        }

        private IEnumerator DeploySlidingChassisPanelRoutine(Color pulseCol)
        {
            Vector2 startPos = m_RestAnchoredPos + new Vector2(680f, 0f);
            Vector2 overshootPos = m_RestAnchoredPos + new Vector2(-22f, 0f);

            m_Rect.pivot = m_RestPivot;
            m_Rect.anchoredPosition = startPos;
            m_Rect.localEulerAngles = Vector3.zero;
            m_Rect.localScale = new Vector3(0.85f, 1.0f, 1f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.45f, 0.03f);

            float travelDur = 0.28f;
            float elapsed = 0f;
            Vector2 prevPos = startPos;

            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInCubic, t);

                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    float tilt = Mathf.Sin(t * Mathf.PI) * -3.5f;
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, tilt);
                    m_Rect.localScale = Vector3.Lerp(new Vector3(0.85f, 1.0f, 1f), new Vector3(1.03f, 1.0f, 1f), ease);

                    if (Time.frameCount % 3 == 0 && CinematicUIParticleSystem.Instance != null)
                    {
                        Vector2 vel = (m_Rect.anchoredPosition - prevPos) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                        if (vel.sqrMagnitude > 400f)
                        {
                            CinematicUIParticleSystem.Instance.SpawnEnergyTrail(m_Rect.anchoredPosition, transform.parent, pulseCol, vel);
                        }
                    }
                    prevPos = m_Rect.anchoredPosition;
                }
                yield return null;
            }

            // Lock impact
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.75f, 0.03f);
            UIMicroShake.Shake(0.35f, 0.05f);
            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 1.8f);
                CinematicUI.TriggerBorderPulse(0.24f, pulseCol);
            }
            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_Rect, pulseCol, 5, 18f);
            }

            // Settle
            elapsed = 0f;
            float settleDur = 0.12f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.Lerp(overshootPos, m_RestAnchoredPos, ease);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(1.5f, 0f, ease));
                    m_Rect.localScale = Vector3.Lerp(new Vector3(1.05f, 0.95f, 1f), m_RestLocalScale, ease);
                }
                yield return null;
            }
        }

        private IEnumerator DeployHingedFoldOutRoutine(Color pulseCol)
        {
            Vector2 startPos = m_RestAnchoredPos + new Vector2(180f, -260f);
            Vector2 overshootPos = m_RestAnchoredPos + new Vector2(8f, 6f);

            m_Rect.pivot = new Vector2(0.15f, 0.85f);
            m_Rect.anchoredPosition = startPos;
            m_Rect.localEulerAngles = new Vector3(0f, 0f, -38f);
            m_Rect.localScale = new Vector3(0.68f, 0.68f, 1f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.40f, 0.04f);

            float travelDur = 0.28f;
            float elapsed = 0f;
            Vector2 prevPos = startPos;

            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-38f, 3.5f, ease));
                    m_Rect.localScale = Vector3.Lerp(new Vector3(0.68f, 0.68f, 1f), new Vector3(1.03f, 1.03f, 1f), ease);

                    if (Time.frameCount % 3 == 0 && CinematicUIParticleSystem.Instance != null)
                    {
                        Vector2 vel = (m_Rect.anchoredPosition - prevPos) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                        if (vel.sqrMagnitude > 400f)
                        {
                            CinematicUIParticleSystem.Instance.SpawnEnergyTrail(m_Rect.anchoredPosition, transform.parent, pulseCol, vel);
                        }
                    }
                    prevPos = m_Rect.anchoredPosition;
                }
                yield return null;
            }

            // Lock impact
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.70f, 0.03f);
            UIMicroShake.Shake(0.30f, 0.04f);
            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 1.8f);
                CinematicUI.TriggerBorderPulse(0.24f, pulseCol);
            }
            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_Rect, pulseCol, 5, 18f);
            }

            // Settle
            elapsed = 0f;
            float settleDur = 0.11f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.Lerp(overshootPos, m_RestAnchoredPos, ease);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(3.5f, 0f, ease));
                    m_Rect.localScale = Vector3.Lerp(new Vector3(1.03f, 1.03f, 1f), m_RestLocalScale, ease);
                }
                yield return null;
            }
        }

        private IEnumerator DeployCornerPivotSwingRoutine(Color pulseCol)
        {
            Vector2 startPos = m_RestAnchoredPos + new Vector2(-40f, 320f);
            Vector2 overshootPos = m_RestAnchoredPos + new Vector2(6f, -10f);

            m_Rect.pivot = new Vector2(0.08f, 0.92f);
            m_Rect.anchoredPosition = startPos;
            m_Rect.localEulerAngles = new Vector3(0f, 0f, 42f);
            m_Rect.localScale = new Vector3(0.80f, 0.80f, 1f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.40f, 0.03f);

            float travelDur = 0.28f;
            float elapsed = 0f;
            Vector2 prevPos = startPos;

            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(42f, -4f, ease));
                    m_Rect.localScale = Vector3.Lerp(new Vector3(0.80f, 0.80f, 1f), new Vector3(1.02f, 1.02f, 1f), ease);

                    if (Time.frameCount % 3 == 0 && CinematicUIParticleSystem.Instance != null)
                    {
                        Vector2 vel = (m_Rect.anchoredPosition - prevPos) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                        if (vel.sqrMagnitude > 400f)
                        {
                            CinematicUIParticleSystem.Instance.SpawnEnergyTrail(m_Rect.anchoredPosition, transform.parent, pulseCol, vel);
                        }
                    }
                    prevPos = m_Rect.anchoredPosition;
                }
                yield return null;
            }

            // Lock impact
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.70f, 0.03f);
            UIMicroShake.Shake(0.30f, 0.04f);
            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 1.8f);
                CinematicUI.TriggerBorderPulse(0.24f, pulseCol);
            }
            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_Rect, pulseCol, 5, 18f);
            }

            // Settle
            elapsed = 0f;
            float settleDur = 0.11f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.Lerp(overshootPos, m_RestAnchoredPos, ease);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-4f, 0f, ease));
                    m_Rect.localScale = Vector3.Lerp(new Vector3(1.02f, 1.02f, 1f), m_RestLocalScale, ease);
                }
                yield return null;
            }
        }

        private IEnumerator DeployPneumaticExtensionRoutine(Color pulseCol)
        {
            Vector2 startPos = m_RestAnchoredPos + new Vector2(0f, -540f);
            Vector2 overshootPos = m_RestAnchoredPos + new Vector2(0f, 16f);

            m_Rect.pivot = m_RestPivot;
            m_Rect.anchoredPosition = startPos;
            m_Rect.localEulerAngles = Vector3.zero;
            m_Rect.localScale = new Vector3(1.0f, 0.78f, 1f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.45f, 0.03f);

            float travelDur = 0.28f;
            float elapsed = 0f;
            Vector2 prevPos = startPos;

            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutCubic, t);

                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    m_Rect.localScale = Vector3.Lerp(new Vector3(1.0f, 0.78f, 1f), new Vector3(1.02f, 1.02f, 1f), ease);

                    if (Time.frameCount % 3 == 0 && CinematicUIParticleSystem.Instance != null)
                    {
                        Vector2 vel = (m_Rect.anchoredPosition - prevPos) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                        if (vel.sqrMagnitude > 400f)
                        {
                            CinematicUIParticleSystem.Instance.SpawnEnergyTrail(m_Rect.anchoredPosition, transform.parent, pulseCol, vel);
                        }
                    }
                    prevPos = m_Rect.anchoredPosition;
                }
                yield return null;
            }

            // Lock impact
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.75f, 0.03f);
            UIMicroShake.Shake(0.35f, 0.04f);
            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 1.8f);
                CinematicUI.TriggerBorderPulse(0.24f, pulseCol);
            }
            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_Rect, pulseCol, 5, 18f);
            }

            // Settle
            elapsed = 0f;
            float settleDur = 0.12f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.Lerp(overshootPos, m_RestAnchoredPos, ease);
                    m_Rect.localScale = Vector3.Lerp(new Vector3(1.02f, 1.02f, 1f), m_RestLocalScale, ease);
                }
                yield return null;
            }
        }

        private IEnumerator DeployHeavyDualRailPlungeRoutine(Color pulseCol)
        {
            Vector2 startPos = m_RestAnchoredPos + new Vector2(0f, 850f);
            Vector2 overshootPos = m_RestAnchoredPos + new Vector2(0f, -28f);

            m_Rect.pivot = m_RestPivot;
            m_Rect.anchoredPosition = startPos;
            m_Rect.localEulerAngles = new Vector3(0f, 0f, -5f);
            m_Rect.localScale = new Vector3(0.92f, 1.08f, 1f);

            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.55f, 0.03f);

            float travelDur = 0.32f;
            float elapsed = 0f;
            Vector2 prevPos = startPos;

            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDur);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                    m_Rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-5f, 0f, ease));

                    if (Time.frameCount % 3 == 0 && CinematicUIParticleSystem.Instance != null)
                    {
                        Vector2 vel = (m_Rect.anchoredPosition - prevPos) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                        if (vel.sqrMagnitude > 400f)
                        {
                            CinematicUIParticleSystem.Instance.SpawnEnergyTrail(m_Rect.anchoredPosition, transform.parent, pulseCol, vel);
                        }
                    }
                    prevPos = m_Rect.anchoredPosition;
                }
                yield return null;
            }

            // Very heavy impact!
            UIFeedbackAudio.PlaySfx(UISfxType.Impact, 0.95f, 0.04f);
            UIMicroShake.Shake(1.4f, 0.08f);
            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.10f, 2.4f, pulseCol);
                CinematicUI.TriggerBorderPulse(0.28f, pulseCol);
            }
            if (CinematicUIParticleSystem.Instance != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_Rect, pulseCol, 8, 24f);
            }

            // Rebound & settle
            elapsed = 0f;
            float settleDur = 0.16f;
            while (elapsed < settleDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.Lerp(overshootPos, m_RestAnchoredPos, ease);
                    m_Rect.localScale = Vector3.Lerp(new Vector3(1.14f, 0.86f, 1f), m_RestLocalScale, ease);
                }
                yield return null;
            }
        }

        #endregion

        #region Retract & Selection Lock

        private IEnumerator RetractKinematicsRoutine(Action onRetracted)
        {
            Color pulseCol = GetMechanismColor();
            UIFeedbackAudio.PlaySfx(UISfxType.Retract, 0.65f, 0.02f);

            if (CinematicUI != null)
            {
                CinematicUI.TriggerBorderPulse(0.20f, pulseCol, 1.5f, clockwise: false);
            }

            Vector2 targetPos = m_RestAnchoredPos;
            switch (m_Mechanism)
            {
                case MechanicalMechanismType.RotatingArm:
                    targetPos = m_RestAnchoredPos + new Vector2(420f, 380f);
                    break;
                case MechanicalMechanismType.SlidingChassisPanel:
                    targetPos = m_RestAnchoredPos + new Vector2(680f, 0f);
                    break;
                case MechanicalMechanismType.HingedFoldOut:
                    targetPos = m_RestAnchoredPos + new Vector2(180f, -260f);
                    break;
                case MechanicalMechanismType.CornerPivotSwing:
                    targetPos = m_RestAnchoredPos + new Vector2(-40f, 320f);
                    break;
                case MechanicalMechanismType.PneumaticExtension:
                    targetPos = m_RestAnchoredPos + new Vector2(0f, -540f);
                    break;
                case MechanicalMechanismType.HeavyDualRailPlunge:
                    targetPos = m_RestAnchoredPos + new Vector2(0f, 850f);
                    break;
            }

            float duration = 0.24f;
            float elapsed = 0f;
            Vector2 startPos = (m_Rect != null) ? m_Rect.anchoredPosition : m_RestAnchoredPos;
            Vector2 prevPos = startPos;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(EasingType.EaseInQuad, t);

                if (m_Rect != null)
                {
                    m_Rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                    if (Time.frameCount % 3 == 0 && CinematicUIParticleSystem.Instance != null)
                    {
                        Vector2 vel = (m_Rect.anchoredPosition - prevPos) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                        if (vel.sqrMagnitude > 400f)
                        {
                            CinematicUIParticleSystem.Instance.SpawnEnergyTrail(m_Rect.anchoredPosition, transform.parent, pulseCol, vel);
                        }
                    }
                    prevPos = m_Rect.anchoredPosition;
                }
                yield return null;
            }

            if (CinematicUI != null)
            {
                CinematicUI.ResetToIdle();
            }

            m_ActiveMotionRoutine = null;
            onRetracted?.Invoke();
        }

        private IEnumerator SelectionLockPunchRoutine(Action onComplete)
        {
            UIMicroShake.Shake(1.1f, 0.08f);
            UIFeedbackAudio.PlaySfx(UISfxType.Confirm, 0.85f, 0.02f);

            Color goldSpark = new Color(1f, 0.88f, 0.35f, 1f);

            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 2.2f, goldSpark);
                CinematicUI.TriggerBorderPulse(0.22f, goldSpark, 3.0f);
            }

            if (CinematicUIParticleSystem.Instance != null && m_Rect != null)
            {
                CinematicUIParticleSystem.Instance.SpawnDirectionalBurst(Vector2.zero, m_Rect, goldSpark, Vector2.right, 8, 45f, 35f);
            }

            // Punch compression
            Vector3 compressedScale = new Vector3(0.94f, 0.94f, 1f);
            Vector2 punchPos = m_RestAnchoredPos + new Vector2(6f, 0f);

            float halfDur = 0.07f;
            float elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDur);
                if (m_Rect != null)
                {
                    m_Rect.localScale = Vector3.Lerp(m_RestLocalScale, compressedScale, t);
                    m_Rect.anchoredPosition = Vector2.Lerp(m_RestAnchoredPos, punchPos, t);
                }
                yield return null;
            }

            elapsed = 0f;
            float returnDur = 0.09f;
            while (elapsed < returnDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / returnDur);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);
                if (m_Rect != null)
                {
                    m_Rect.localScale = Vector3.Lerp(compressedScale, m_RestLocalScale, ease);
                    m_Rect.anchoredPosition = Vector2.Lerp(punchPos, m_RestAnchoredPos, ease);
                }
                yield return null;
            }

            if (m_Rect != null)
            {
                m_Rect.anchoredPosition = m_RestAnchoredPos;
                m_Rect.localScale = m_RestLocalScale;
            }

            m_ActiveMotionRoutine = null;
            onComplete?.Invoke();
        }

        #endregion
    }
}
