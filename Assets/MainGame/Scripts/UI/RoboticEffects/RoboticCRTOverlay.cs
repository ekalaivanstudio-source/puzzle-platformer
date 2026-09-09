using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UI.RoboticEffects
{
    /// <summary>
    /// Subtle CRT & digital distortion overlay:
    /// - Ultra-faint 1px scanlines (alpha ~0.025) and light vignette
    /// - Momentary transition micro-glitches (1-2 frames of micro slice jitter during screen switches)
    /// Preserves 100% readability at 1920x1080; zero raycast blocking.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public class RoboticCRTOverlay : MonoBehaviour
    {
        private RawImage m_RawImage;
        private Material m_Material;
        private Coroutine m_GlitchRoutine;

        private static readonly int PropGlitch = Shader.PropertyToID("_GlitchIntensity");

        private void Awake()
        {
            m_RawImage = GetComponent<RawImage>();
            m_RawImage.raycastTarget = false;

            Shader crtShader = Shader.Find("MainGame/UI/RoboticCRTOverlay");
            if (crtShader != null)
            {
                m_Material = new Material(crtShader);
                m_RawImage.material = m_Material;
            }
        }

        private void OnDestroy()
        {
            if (m_Material != null)
            {
                Destroy(m_Material);
            }
        }

        /// <summary>
        /// Triggers a brief, controlled micro-glitch (0.06 - 0.09s) during screen transitions or alert moments.
        /// </summary>
        public void TriggerGlitch(float duration = 0.08f)
        {
            if (m_Material == null) return;

            if (m_GlitchRoutine != null)
            {
                StopCoroutine(m_GlitchRoutine);
            }

            m_GlitchRoutine = StartCoroutine(GlitchRoutine(duration));
        }

        private IEnumerator GlitchRoutine(float duration)
        {
            m_Material.SetFloat(PropGlitch, 1f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                m_Material.SetFloat(PropGlitch, 1f - t);
                yield return null;
            }

            m_Material.SetFloat(PropGlitch, 0f);
            m_GlitchRoutine = null;
        }
    }
}
