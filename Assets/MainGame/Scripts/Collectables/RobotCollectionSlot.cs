using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Collectables
{
    /// <summary>
    /// One robot's entry in the collection UI: a silhouette with one image layer per part
    /// stacked on top. A collected part's layer is shown, an uncollected one hidden, so the
    /// robot visibly fills in piece by piece as parts are found.
    ///
    /// The silhouette underneath is either the robot's own dark chassis (home screen) or a
    /// looping animation (gameplay HUD) when <see cref="m_SilhouetteFrames"/> is authored.
    ///
    /// All the sprites share a canvas size and each part sprite holds only that part's
    /// pixels, so the layers line up exactly (see <see cref="RobotDefinition"/>).
    ///
    /// A slot never reads the save file on its own clock — <see cref="RobotCollectionView"/>
    /// owns the event subscription and calls <see cref="Refresh"/>.
    /// </summary>
    public class RobotCollectionSlot : MonoBehaviour
    {
        [Header("Layers")]
        [Tooltip("The unlit chassis, drawn behind every part layer.")]
        [SerializeField] private Image m_Silhouette;

        [Tooltip("One image per part, in the same order as RobotDefinition.partSprites. " +
                 "Later entries draw in front.")]
        [SerializeField] private Image[] m_PartLayers = new Image[RobotIds.PartsPerRobot];

        [Header("Silhouette animation (optional)")]
        [Tooltip("Frames of a looping placeholder drawn instead of the robot's static silhouette — " +
                 "the gameplay HUD uses a marching-dash outline so an empty slot reads as " +
                 "'still to build' rather than a grey blob. Leave empty to show " +
                 "RobotDefinition.silhouette unchanged.")]
        [SerializeField] private Sprite[] m_SilhouetteFrames;

        [Tooltip("Playback speed of the silhouette frames.")]
        [SerializeField, Min(1f)] private float m_SilhouetteFps = 8f;

        [Header("Labels (optional)")]
        [SerializeField] private TMP_Text m_NameLabel;
        [SerializeField] private TMP_Text m_CountLabel;

        [Tooltip("Format string with {0} = collected, {1} = total.")]
        [SerializeField] private string m_CountFormat = "{0}/{1}";

        [Header("Complete state (optional)")]
        [Tooltip("Shown only once every part of this robot has been found.")]
        [SerializeField] private GameObject m_CompleteBadge;

        [Header("Pop animation")]
        [Tooltip("Scale punch played on a part layer the moment it is collected.")]
        [SerializeField] private bool m_AnimateOnCollect = true;
        [SerializeField, Min(0f)] private float m_PopScale = 1.35f;
        [SerializeField, Min(0.01f)] private float m_PopDuration = 0.28f;

        private RobotDefinition m_Definition;
        private Coroutine m_PopRoutine;
        private Coroutine m_SilhouetteRoutine;

        /// <summary>The robot this slot draws, or null before <see cref="Bind"/>.</summary>
        public RobotDefinition Definition => m_Definition;

        /// <summary>True when this slot animates its silhouette instead of drawing a static one.</summary>
        private bool HasSilhouetteAnimation => m_SilhouetteFrames != null && m_SilhouetteFrames.Length > 0;

        /// <summary>
        /// Points the slot at a robot and writes its art into the layers. Safe to call again
        /// with a different robot — the slot repaints completely.
        /// </summary>
        public void Bind(RobotDefinition definition)
        {
            m_Definition = definition;

            if (definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // An animated slot owns its own sprite — writing the definition's static silhouette
            // here would be overwritten a frame later anyway, and flashes the grey chassis.
            if (m_Silhouette != null && !HasSilhouetteAnimation)
                m_Silhouette.sprite = definition.silhouette;

            if (m_PartLayers != null)
            {
                for (int i = 0; i < m_PartLayers.Length; i++)
                {
                    var layer = m_PartLayers[i];
                    if (layer == null) continue;
                    layer.sprite = definition.GetPartSprite(i);
                }
            }

            if (m_NameLabel != null)
            {
                m_NameLabel.text = definition.displayName;
                m_NameLabel.color = definition.accentColor;
            }

            Refresh();
        }

        /// <summary>Repaints the layers and labels from current save state.</summary>
        public void Refresh()
        {
            if (m_Definition == null) return;

            int collected = 0;

            if (m_PartLayers != null)
            {
                for (int i = 0; i < m_PartLayers.Length; i++)
                {
                    var layer = m_PartLayers[i];
                    if (layer == null) continue;

                    bool has = RobotCollectionService.IsCollected(m_Definition.robot, i);
                    if (has) collected++;
                    layer.enabled = has;
                }
            }
            else
            {
                collected = RobotCollectionService.CollectedCount(m_Definition.robot);
            }

            if (m_CountLabel != null)
                m_CountLabel.text = string.Format(m_CountFormat, collected, m_Definition.PartCount);

            if (m_CompleteBadge != null)
                m_CompleteBadge.SetActive(collected >= m_Definition.PartCount);
        }

        /// <summary>
        /// Repaints, then punches the freshly collected layer so the fill reads as an event
        /// rather than a silent state change.
        /// </summary>
        public void PlayCollectFeedback(int partIndex)
        {
            Refresh();

            if (!m_AnimateOnCollect || !isActiveAndEnabled) return;
            if (m_PartLayers == null || partIndex < 0 || partIndex >= m_PartLayers.Length) return;

            var layer = m_PartLayers[partIndex];
            if (layer == null) return;

            if (m_PopRoutine != null) StopCoroutine(m_PopRoutine);
            m_PopRoutine = StartCoroutine(PopRoutine(layer.rectTransform));
        }

        private IEnumerator PopRoutine(RectTransform target)
        {
            float elapsed = 0f;
            while (elapsed < m_PopDuration)
            {
                // Unscaled: the pause menu and level-complete flow can freeze timeScale.
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / m_PopDuration);
                // Up on the first half, back to 1 on the second.
                float punch = Mathf.Sin(t * Mathf.PI);
                float scale = Mathf.LerpUnclamped(1f, m_PopScale, punch);
                target.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            target.localScale = Vector3.one;
            m_PopRoutine = null;
        }

        private void OnEnable()
        {
            StartSilhouetteAnimation();
        }

        /// <summary>
        /// Loops <see cref="m_SilhouetteFrames"/> on the silhouette image. No-op for a slot that
        /// was authored with a static silhouette.
        /// </summary>
        private void StartSilhouetteAnimation()
        {
            if (!HasSilhouetteAnimation || m_Silhouette == null) return;
            if (m_SilhouetteRoutine != null) StopCoroutine(m_SilhouetteRoutine);
            m_SilhouetteRoutine = StartCoroutine(SilhouetteRoutine());
        }

        private IEnumerator SilhouetteRoutine()
        {
            // Realtime: the placeholder has to keep ticking behind the pause menu and the
            // level-complete flow, both of which zero timeScale.
            var wait = new WaitForSecondsRealtime(1f / Mathf.Max(1f, m_SilhouetteFps));
            int frame = 0;

            while (true)
            {
                var sprite = m_SilhouetteFrames[frame];
                if (sprite != null) m_Silhouette.sprite = sprite;

                frame = (frame + 1) % m_SilhouetteFrames.Length;
                yield return wait;
            }
        }

        private void OnDisable()
        {
            if (m_SilhouetteRoutine != null)
            {
                StopCoroutine(m_SilhouetteRoutine);
                m_SilhouetteRoutine = null;
            }

            // Leave no half-finished punch behind when the HUD is hidden mid-animation.
            if (m_PopRoutine != null)
            {
                StopCoroutine(m_PopRoutine);
                m_PopRoutine = null;
            }

            if (m_PartLayers == null) return;
            for (int i = 0; i < m_PartLayers.Length; i++)
                if (m_PartLayers[i] != null) m_PartLayers[i].rectTransform.localScale = Vector3.one;
        }

        private void OnValidate()
        {
            if (m_PartLayers != null && m_PartLayers.Length != RobotIds.PartsPerRobot)
                System.Array.Resize(ref m_PartLayers, RobotIds.PartsPerRobot);
        }
    }
}
