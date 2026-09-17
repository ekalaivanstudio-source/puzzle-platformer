using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using MainGame.UI.Feedback;
using MainGame.UI.CinematicEffects;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Controls the cinematic presentation of the RETRY title logo (Holder Tittle / Group 65.png).
    /// Uses the CinematicUI shader and 2D UI particle system to provide:
    /// - Activation diagonal power sweep across the logo lettering
    /// - Traveling border electrical pulse
    /// - Pixel spark bursts
    /// - Subtle living idle breathing
    /// - Stylized pixel fragment dissolve on screen exit (without modifying original sprite)
    /// </summary>
    [DisallowMultipleComponent]
    public class MechanicalLogoAssembly : MonoBehaviour
    {
        [Header("Logo Containers")]
        [Tooltip("Root container holding the title graphic (Holder Tittle).")]
        [SerializeField] private RectTransform m_LogoRoot;
        [Tooltip("Original Title image GameObject.")]
        [SerializeField] private Image m_OriginalTitleImage;

        // Baseline Rest State
        private Vector2 m_RootRestPos;
        private Vector3 m_RootRestScale = Vector3.one;
        private Vector3 m_RootRestAngles = Vector3.zero;

        private Coroutine m_ActiveRoutine;
        private bool m_IsConfigured = false;
        private bool m_HasCapturedRest = false;

        private CinematicUIEffect m_CinematicUI;

        public CinematicUIEffect CinematicUI
        {
            get
            {
                if (m_CinematicUI == null)
                {
                    Image targetImg = m_OriginalTitleImage != null ? m_OriginalTitleImage : GetComponentInChildren<Image>();
                    if (targetImg != null)
                    {
                        m_CinematicUI = targetImg.GetComponent<CinematicUIEffect>() ?? targetImg.gameObject.AddComponent<CinematicUIEffect>();
                    }
                }
                return m_CinematicUI;
            }
        }

        public bool IsAnimating => m_ActiveRoutine != null;

        private void Awake()
        {
            SetupAssembly();
        }

        private void Start()
        {
            SetupAssembly();
        }

        public void SetupAssembly()
        {
            if (m_IsConfigured && m_HasCapturedRest) return;

            if (m_LogoRoot == null)
            {
                m_LogoRoot = GetComponent<RectTransform>();
            }

            if (m_OriginalTitleImage == null && m_LogoRoot != null)
            {
                Transform titleChild = m_LogoRoot.Find("Title") ?? m_LogoRoot.Find("Title Image") ?? m_LogoRoot.Find("Group 65");
                if (titleChild != null)
                {
                    m_OriginalTitleImage = titleChild.GetComponent<Image>();
                }
                else
                {
                    m_OriginalTitleImage = m_LogoRoot.GetComponentInChildren<Image>();
                }
            }

            if (m_LogoRoot != null)
            {
                m_RootRestPos = m_LogoRoot.anchoredPosition;
                // Enforce y = 0 if it was captured near 125 (to keep villain face visible)
                if (Mathf.Abs(m_RootRestPos.y) > 0.01f && Mathf.Abs(m_RootRestPos.y - 125f) < 50f)
                {
                    m_RootRestPos.y = 0f;
                    m_LogoRoot.anchoredPosition = m_RootRestPos;
                }
                m_RootRestScale = m_LogoRoot.localScale;
                m_RootRestAngles = m_LogoRoot.localEulerAngles;
            }

            // Cleanup any legacy split masks
            if (m_LogoRoot != null)
            {
                Transform left = m_LogoRoot.Find("LeftPlateMask");
                if (left != null) Destroy(left.gameObject);
                Transform right = m_LogoRoot.Find("RightPlateMask");
                if (right != null) Destroy(right.gameObject);
                Transform seam = m_LogoRoot.Find("__SeamArcLine");
                if (seam != null) Destroy(seam.gameObject);
            }

            if (m_OriginalTitleImage != null)
            {
                m_OriginalTitleImage.gameObject.SetActive(true);
            }

            _ = CinematicUI; // Ensure initialized

            m_IsConfigured = true;
            m_HasCapturedRest = true;
        }

        public void ResetToRestState()
        {
            KillMotion();
            SetupAssembly();

            if (m_LogoRoot != null)
            {
                m_LogoRoot.anchoredPosition = m_RootRestPos;
                m_LogoRoot.localScale = m_RootRestScale;
                m_LogoRoot.localEulerAngles = m_RootRestAngles;
            }

            if (m_OriginalTitleImage != null)
            {
                m_OriginalTitleImage.gameObject.SetActive(true);
            }

            if (CinematicUI != null)
            {
                CinematicUI.SetPixelDissolve(0f);
                CinematicUI.ResetToIdle();
            }
        }

        public void PrepareRetractedState()
        {
            SetupAssembly();
            if (m_LogoRoot != null)
            {
                m_LogoRoot.anchoredPosition = m_RootRestPos + new Vector2(0f, 150f);
                m_LogoRoot.localScale = new Vector3(0.85f, 0.85f, 1f);
            }
            if (CinematicUI != null)
            {
                CinematicUI.SetPixelDissolve(1f);
            }
        }

        public void KillMotion()
        {
            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
                m_ActiveRoutine = null;
            }

            if (CinematicUI != null)
            {
                CinematicUI.ResetToIdle();
            }
        }

        /// <summary>
        /// Deploys the RETRY logo with a cinematic power sweep, border energy pulse, and spark burst.
        /// </summary>
        public void DeploySequence(Action onComplete = null, Action onImpact = null)
        {
            KillMotion();
            SetupAssembly();
            m_ActiveRoutine = StartCoroutine(CinematicDeployRoutine(onComplete, onImpact));
        }

        private IEnumerator CinematicDeployRoutine(Action onComplete, Action onImpact)
        {
            if (m_OriginalTitleImage != null)
            {
                m_OriginalTitleImage.gameObject.SetActive(true);
            }

            if (m_LogoRoot != null)
            {
                m_LogoRoot.anchoredPosition = m_RootRestPos;
                m_LogoRoot.localScale = m_RootRestScale;
                m_LogoRoot.localEulerAngles = m_RootRestAngles;
            }

            if (CinematicUI != null)
            {
                CinematicUI.SetPixelDissolve(0f);
                // Step 1: Pre-sweep border pulse
                CinematicUI.TriggerBorderPulse(0.24f, new Color(0.35f, 0.85f, 1.0f, 1f), 1.5f);
            }

            yield return new WaitForSecondsRealtime(0.06f);

            // Step 2: Diagonal activation power sweep
            UIFeedbackAudio.PlaySfx(UISfxType.Whoosh, 0.50f, 0.03f);
            if (CinematicUI != null)
            {
                CinematicUI.PlayActivationSweep(0.32f, new Color(0.85f, 0.95f, 1.0f, 1f), 50f);
            }

            yield return new WaitForSecondsRealtime(0.12f);

            // Step 3: Spark burst & impact flash (synchronized to beat downbeat)
            UIFeedbackAudio.PlaySfx(UISfxType.RetryCoreActivation, 0.90f, 0.02f);
            UIMicroShake.Shake(0.30f, 0.05f);

            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 1.6f);
            }

            if (CinematicUIParticleSystem.Instance != null && m_LogoRoot != null)
            {
                CinematicUIParticleSystem.Instance.SpawnSparkBurst(Vector2.zero, m_LogoRoot, new Color(1.0f, 0.85f, 0.35f, 1f), 6, 22f);
            }

            onImpact?.Invoke();

            // Step 4: Settle into subtle living breathing
            if (CinematicUI != null)
            {
                CinematicUI.SetIdleBreathing(true, 3.2f, 0.16f);
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Disassembles the RETRY logo on screen transition using a shader pixel dissolve and scattering shards.
        /// </summary>
        public void RetractSequence(Action onComplete = null)
        {
            KillMotion();
            SetupAssembly();
            m_ActiveRoutine = StartCoroutine(CinematicRetractRoutine(onComplete));
        }

        /// <summary>
        /// Alias for RetractSequence to support exit transitions.
        /// </summary>
        public void BreakdownSequence(Action onComplete = null)
        {
            RetractSequence(onComplete);
        }

        private IEnumerator CinematicRetractRoutine(Action onComplete)
        {
            UIFeedbackAudio.PlaySfx(UISfxType.Retract, 0.65f, 0.02f);

            // Phase 1: Impact flash & energy buildup
            if (CinematicUI != null)
            {
                CinematicUI.TriggerImpactFlash(0.08f, 2.0f);
            }

            // Phase 2: Shards break apart
            if (CinematicUIParticleSystem.Instance != null && m_LogoRoot != null)
            {
                CinematicUIParticleSystem.Instance.SpawnPixelDissolveShards(m_LogoRoot, transform.parent, new Color(0.35f, 0.85f, 1.0f, 1f), 14);
            }

            // Phase 3: Blocky pixel dissolve
            float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (CinematicUI != null)
                {
                    CinematicUI.SetPixelDissolve(t, new Color(0.35f, 0.90f, 1.0f, 1f));
                }
                yield return null;
            }

            if (CinematicUI != null)
            {
                CinematicUI.SetPixelDissolve(1f);
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }
    }
}
