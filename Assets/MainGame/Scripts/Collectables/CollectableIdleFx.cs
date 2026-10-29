using UnityEngine;

namespace Collectables
{
    /// <summary>
    /// The idle motion that tells the player "this can be picked up": the pickup breathes
    /// (scales in and out), the god rays behind it turn slowly, and the glow pulses on the
    /// same beat as the breath.
    ///
    /// Purely cosmetic and self-contained — it never touches collection state. It drives
    /// the transforms it is given and nothing else, so disabling the pickup (on collect, or
    /// when the level hides no shard) simply stops it.
    ///
    /// The shine sweep across the sprite itself is not done here; that is the
    /// <c>MainGame/Sprite Shine</c> shader on the pickup's material.
    /// </summary>
    public class CollectableIdleFx : MonoBehaviour
    {
        [Header("Breathe")]
        [Tooltip("Transform that scales in and out. Falls back to this object.")]
        [SerializeField] private Transform m_ScaleTarget;

        [Tooltip("How far the scale swings either side of its rest size (0.1 = ±10%).")]
        [SerializeField, Range(0f, 0.5f)] private float m_ScaleAmplitude = 0.1f;

        [Tooltip("Seconds for one full in-and-out breath.")]
        [SerializeField, Min(0.05f)] private float m_BreathePeriod = 1.4f;

        [Header("God Rays")]
        [Tooltip("Ray layers to turn. Alternate entries spin the opposite way so the beams cross.")]
        [SerializeField] private Transform[] m_Rays;

        [Tooltip("Degrees per second for the first ray layer.")]
        [SerializeField] private float m_RaySpeed = 25f;

        [Header("Glow")]
        [Tooltip("Halo whose alpha pulses with the breath. Optional.")]
        [SerializeField] private SpriteRenderer m_Glow;

        [SerializeField, Range(0f, 1f)] private float m_GlowMinAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] private float m_GlowMaxAlpha = 0.8f;

        private Vector3 m_RestScale;
        private float m_StartTime;

        private void Awake()
        {
            if (m_ScaleTarget == null) m_ScaleTarget = transform;
            m_RestScale = m_ScaleTarget.localScale;
        }

        private void OnEnable()
        {
            m_StartTime = Time.time;
        }

        private void OnDisable()
        {
            // Leave the pickup at its authored size so a re-enable starts clean.
            if (m_ScaleTarget != null) m_ScaleTarget.localScale = m_RestScale;
        }

        private void Update()
        {
            float t = Time.time - m_StartTime;

            // 0..1..0 over one period, eased at both ends so the breath never snaps.
            float wave = 0.5f - 0.5f * Mathf.Cos(t * (2f * Mathf.PI / m_BreathePeriod));

            m_ScaleTarget.localScale = m_RestScale * (1f + m_ScaleAmplitude * (wave * 2f - 1f));

            if (m_Rays != null)
            {
                for (int i = 0; i < m_Rays.Length; i++)
                {
                    if (m_Rays[i] == null) continue;
                    // Odd layers counter-rotate a little slower, so the overlap keeps shifting.
                    float speed = (i % 2 == 0) ? m_RaySpeed : -m_RaySpeed * 0.6f;
                    m_Rays[i].localRotation = Quaternion.Euler(0f, 0f, t * speed);
                }
            }

            if (m_Glow != null)
            {
                Color c = m_Glow.color;
                c.a = Mathf.Lerp(m_GlowMinAlpha, m_GlowMaxAlpha, wave);
                m_Glow.color = c;
            }
        }
    }
}
