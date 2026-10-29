using System.Collections;
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
///   • <see cref="ByteHint.MissedShard"/> — the level was won with its memory shard still
///     lying in it. Said from the win routine in <see cref="PlayerController"/> through
///     <see cref="MissedShardRoutine"/>, every time, so a replay that finally grabs it goes quiet.
///
/// Lives in Managers next to Dr.Glitch, so every level carries it without wiring.
/// </summary>
public class ByteHintController : MonoBehaviour
{
    public enum ByteHint
    {
        Rewind,
        Undo,
        MissedShard
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

    [SerializeField] private HintLine m_MissedShardHint = new HintLine
    {
        pcText = "You missed a memory shard! Shards unlock your lost memories.",
        xboxText = "You missed a memory shard! Shards unlock your lost memories."
    };

    [Tooltip("Seconds the missed-shard bubble stays up. Longer than the control hints, since " +
             "it is a sentence to read rather than a key to spot.")]
    [SerializeField, Min(0f)] private float m_MissedShardStayDuration = 3f;

    // Left inset of the text inside the bubble. With a key icon (Tab / Backspace) it has to clear
    // the icon, which ends at x 148, and the font draws ~26px left of its rect. Without one, a
    // longer line gets the whole bubble.
    private const float TextLeftWithIcon = 190f;
    private const float TextLeftWithoutIcon = 66.25f;

    private int m_Frame;
    private float m_DefaultStayDuration;
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
        else
            m_DefaultStayDuration = m_UIFloatEffect.StayDuration;
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
        HintLine line = hint switch
        {
            ByteHint.Rewind => m_RewindHint,
            ByteHint.MissedShard => m_MissedShardHint,
            _ => m_UndoHint
        };
        bool xbox = IsXbox();

        if (m_Text != null)
            m_Text.text = xbox ? line.xboxText : line.pcText;

        Sprite icon = xbox ? line.xboxIcon : line.pcIcon;
        if (m_Icon != null)
        {
            m_Icon.sprite = icon;
            m_Icon.gameObject.SetActive(icon != null);
        }

        if (m_Text != null)
        {
            RectTransform textRect = m_Text.rectTransform;
            bool hasIcon = m_Icon != null && icon != null;
            textRect.offsetMin = new Vector2(hasIcon ? TextLeftWithIcon : TextLeftWithoutIcon,
                                             textRect.offsetMin.y);
        }

        m_Frame = 0;
        m_FrameTimer = 0f;
        if (m_Portrait != null && m_IdleFrames != null && m_IdleFrames.Length > 0)
            m_Portrait.sprite = m_IdleFrames[0];

        if (m_UIFloatEffect != null)
        {
            m_UIFloatEffect.SetStayDuration(
                hint == ByteHint.MissedShard ? m_MissedShardStayDuration : m_DefaultStayDuration);
            m_UIFloatEffect.Play();
        }
    }

    /// <summary>
    /// Has Byte point out a memory shard left behind in the level just won, and returns once
    /// the pop has gone back down. Safe to yield on unconditionally from the win routine: it
    /// completes in a single frame when there is no Byte in the scene, the level hides no
    /// shard, or the shard was picked up.
    /// </summary>
    public static IEnumerator MissedShardRoutine()
    {
        if (Instance == null || !IsShardMissedInThisLevel()) yield break;

        Instance.Say(ByteHint.MissedShard);

        UIFloatEffect pop = Instance.m_UIFloatEffect;
        while (pop != null && pop.IsPlaying) yield return null;
    }

    // Same source of truth MemoryShardPickup uses to decide whether it is in the level at all.
    private static bool IsShardMissedInThisLevel()
    {
        LevelContext context = LevelContext.Instance;
        LevelConfig config = context != null ? context.Config : null;
        if (config == null || config.memoryShard == null || !config.memoryShard.placeShard) return false;

        int level = context.CurrentLevel;
        return level > 0 && !Collectables.MemoryShardService.IsCollected(level);
    }

    [ContextMenu("Say Missed Shard")]
    private void SayMissedShard() => Say(ByteHint.MissedShard);

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
