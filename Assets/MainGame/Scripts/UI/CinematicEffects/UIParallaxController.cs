using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UI.CinematicEffects
{
    [Serializable]
    public class ParallaxLayerItem
    {
        public string name;
        public RectTransform target;
        [Range(0f, 25f)] public float maxOffset = 5.0f;
        [HideInInspector] public Vector2 restPosition;
        [HideInInspector] public Vector2 currentVelocity;
    }

    /// <summary>
    /// Screen Space Overlay compatible multi-layer UI parallax depth system.
    /// Drives very subtle sub-pixel layered offsets based on cursor position, gamepad tilt,
    /// and slow ambient atmospheric drift without destabilizing the UI layout:
    /// - BG Red: ±2.5px
    /// - Red/Yellow Layer: ±5.0px
    /// - Villain: ±8.0px
    /// - Title: ±11.0px
    /// - Robot: ±13.0px
    /// - Foreground FX: ±16.0px
    /// Zero transform drift: all shifts are calculated strictly relative to captured rest positions.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIParallaxController : MonoBehaviour
    {
        [Header("Layer Configuration")]
        [SerializeField] private List<ParallaxLayerItem> m_Layers = new List<ParallaxLayerItem>();

        [Header("Parallax Dynamics")]
        [SerializeField] private bool m_EnableParallax = true;
        [SerializeField] private float m_SmoothTime = 0.35f;
        [SerializeField] private float m_AmbientDriftSpeed = 0.5f;
        [SerializeField] private float m_AmbientDriftAmount = 0.35f;

        private Vector2 m_NormalizedInput;
        private bool m_HasCapturedRest = false;
        private bool m_IsActive = true;

        public List<ParallaxLayerItem> Layers => m_Layers;

        private void Awake()
        {
            CaptureRestPositions();
        }

        private void Start()
        {
            CaptureRestPositions();
        }

        private void OnEnable()
        {
            CaptureRestPositions();
            m_IsActive = true;
        }

        private void OnDisable()
        {
            Stop();
        }

        public void CaptureRestPositions()
        {
            if (m_HasCapturedRest) return;

            for (int i = 0; i < m_Layers.Count; i++)
            {
                if (m_Layers[i].target != null)
                {
                    m_Layers[i].restPosition = m_Layers[i].target.anchoredPosition;
                }
            }
            m_HasCapturedRest = true;
        }

        private void Update()
        {
            if (!m_IsActive || !m_EnableParallax) return;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0.0001f) return;

            // 1. Read input (cursor normalized to -1..1 from screen center)
            Vector2 targetInput = Vector2.zero;
            if (Input.mousePresent)
            {
                Vector2 mousePos = Input.mousePosition;
                targetInput.x = Mathf.Clamp((mousePos.x / Mathf.Max(1, Screen.width) - 0.5f) * 2f, -1f, 1f);
                targetInput.y = Mathf.Clamp((mousePos.y / Mathf.Max(1, Screen.height) - 0.5f) * 2f, -1f, 1f);
            }

            // Gamepad navigation input fallback / blend
            float horiz = Input.GetAxisRaw("Horizontal");
            float vert = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(horiz) > 0.1f || Mathf.Abs(vert) > 0.1f)
            {
                targetInput.x = Mathf.Clamp(targetInput.x + horiz * 0.5f, -1f, 1f);
                targetInput.y = Mathf.Clamp(targetInput.y + vert * 0.5f, -1f, 1f);
            }

            // Ambient organic drift so parallax is alive even without player input
            float time = Time.unscaledTime * m_AmbientDriftSpeed;
            Vector2 ambient = new Vector2(Mathf.Sin(time), Mathf.Cos(time * 0.85f)) * m_AmbientDriftAmount;
            targetInput += ambient;

            m_NormalizedInput = Vector2.Lerp(m_NormalizedInput, targetInput, dt * 4f);

            // 2. Apply layered displacement
            for (int i = 0; i < m_Layers.Count; i++)
            {
                var layer = m_Layers[i];
                if (layer.target == null) continue;

                Vector2 offset = m_NormalizedInput * layer.maxOffset;
                Vector2 targetPos = layer.restPosition + offset;

                Vector2 vel = layer.currentVelocity;
                layer.target.anchoredPosition = Vector2.SmoothDamp(layer.target.anchoredPosition, targetPos, ref vel, m_SmoothTime, Mathf.Infinity, dt);
                layer.currentVelocity = vel;
            }
        }

        public void Play()
        {
            m_IsActive = true;
        }

        public void Stop()
        {
            m_IsActive = false;
        }

        public void Reset()
        {
            Stop();
            for (int i = 0; i < m_Layers.Count; i++)
            {
                if (m_Layers[i].target != null)
                {
                    m_Layers[i].target.anchoredPosition = m_Layers[i].restPosition;
                    m_Layers[i].currentVelocity = Vector2.zero;
                }
            }
        }

        public void SetIdle()
        {
            m_IsActive = true;
        }
    }
}
