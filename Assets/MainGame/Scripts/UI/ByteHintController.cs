using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Byte popping up from the bottom-left corner with a speech bubble — the player's own robot
/// pointing out a control the level is not teaching on its own. The bottom-right corner is
/// Dr.Glitch's, and this is built the same way: a <see cref="UIFloatEffect"/> slides the
/// portrait up, shows the bubble at the top, and takes both back down.
///
/// Two hints, each said by whoever asks for it:
///   • <see cref="ByteHint.Rewind"/> — Tab / LT to look back at a previous attempt. Said by
///     this component itself, once per playthrough: the first time the player is put back at
///     spawn after a death or a sequence that ran out. Remembered in
///     <see cref="ModernLevelSelection.SaveManager"/>, so a new game shows it again.
///   • <see cref="ByteHint.Undo"/> — Backspace / B to clear the last input. Said by
///     <see cref="TutorialSequenceGuide"/> when a tutorial player queues a step the solution
///     does not have.
///
/// Lives in Managers next to Dr.Glitch, so every level carries it without wiring.
/// </summary>
public class ByteHintController : MonoBehaviour
{
    public enum ByteHint
    {
        Rewind,
        Undo
    }

    [System.Serializable]
    public class HintLine
    {
        [TextArea] public string pcText;
        public Sprite pcIcon;

        [TextArea] public string xboxText;
        [Tooltip("Left empty, the bubble shows the text alone on Xbox.")]
        public Sprite xboxIcon;
    }

    public static ByteHintController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private UIFloatEffect m_UIFloatEffect;
    [SerializeField] private Image m_Portrait;
    [SerializeField] private TMP_Text m_Text;
    [SerializeField] private Image m_Icon;

    [Header("Portrait")]
    [Tooltip("Byte's idle loop, played on the portrait for as long as it is on screen.")]
    [SerializeField] private Sprite[] m_IdleFrames;
    [SerializeField, Min(1f)] private float m_IdleFps = 6f;

    [Header("Hints")]
    [SerializeField] private HintLine m_RewindHint = new HintLine
    {
        pcText = "Tab to see last input",
        xboxText = "LT to see last input"
    };

    [SerializeField] private HintLine m_UndoHint = new HintLine
    {
        pcText = "Use Backspace to clear the last input",
        xboxText = "Press B to clear the last input"
    };

    private int m_Frame;
    private float m_FrameTimer;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (m_UIFloatEffect == null)
            Debug.LogWarning($"[{nameof(ByteHintController)}] {nameof(m_UIFloatEffect)} is not assigned.", this);
        if (m_Text == null)
            Debug.LogWarning($"[{nameof(ByteHintController)}] {nameof(m_Text)} is not assigned.", this);
    }

    private void OnEnable() => GameManager.OnPlayerRespawn += OnPlayerRespawn;

    private void OnDisable() => GameManager.OnPlayerRespawn -= OnPlayerRespawn;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Unscaled, like the float itself: the death slow-motion would otherwise freeze Byte
    // mid-blink.
    private void Update()
    {
        if (m_Portrait == null || m_IdleFrames == null || m_IdleFrames.Length == 0) return;
        if (m_UIFloatEffect != null && !m_UIFloatEffect.IsPlaying) return;

        m_FrameTimer += Time.unscaledDeltaTime;
        float frameDuration = 1f / m_IdleFps;
        if (m_FrameTimer < frameDuration) return;

        m_FrameTimer -= frameDuration;
        m_Frame = (m_Frame + 1) % m_IdleFrames.Length;
        m_Portrait.sprite = m_IdleFrames[m_Frame];
    }

    // ─── Triggers ─────────────────────────────────────────────────────────────

    // Fires only on the way back from a failure — a death or a sequence that ran out. A win
    // loads the next level instead.
    private void OnPlayerRespawn()
    {
        if (ModernLevelSelection.SaveManager.IsRewindHintShown()) return;

        ModernLevelSelection.SaveManager.MarkRewindHintShown();
        Say(ByteHint.Rewind);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Pops Byte up with the given hint, restarting the pop if one is already up.</summary>
    public void Say(ByteHint hint)
    {
        HintLine line = hint == ByteHint.Rewind ? m_RewindHint : m_UndoHint;
        bool xbox = IsXbox();

        if (m_Text != null)
            m_Text.text = xbox ? line.xboxText : line.pcText;

        if (m_Icon != null)
        {
            Sprite icon = xbox ? line.xboxIcon : line.pcIcon;
            m_Icon.sprite = icon;
            m_Icon.gameObject.SetActive(icon != null);
        }

        m_Frame = 0;
        m_FrameTimer = 0f;
        if (m_Portrait != null && m_IdleFrames != null && m_IdleFrames.Length > 0)
            m_Portrait.sprite = m_IdleFrames[0];

        if (m_UIFloatEffect != null) m_UIFloatEffect.Play();
    }

    [ContextMenu("Say Rewind")]
    private void SayRewind() => Say(ByteHint.Rewind);

    [ContextMenu("Say Undo")]
    private void SayUndo() => Say(ByteHint.Undo);

    private static bool IsXbox()
    {
#if UNITY_GAMECORE || UNITY_XBOXONE
        return true;
#else
        return false;
#endif
    }
}
