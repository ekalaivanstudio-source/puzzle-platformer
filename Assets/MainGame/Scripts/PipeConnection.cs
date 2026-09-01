using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The run of pipe that carries power from the battery socket to the level's exit door.
///
/// The pipe art is always visible; what this owns is the glow laid over it. Every pipe piece
/// has one or more glow children — a straight piece has a single full-length glow, a corner
/// has two halves so the light can turn — and they all start switched off. When
/// <see cref="KeySlot"/> drops the battery in it calls <see cref="Power"/>, the glows come on
/// one at a time from the socket end, and the door is only told to open once the last one
/// lights. The player watches the charge travel to the door rather than the door simply
/// popping open somewhere off screen.
///
/// Segment order IS the direction the power travels, so the array reads socket-first. Left
/// empty it is filled in from the hierarchy at Awake: every descendant named "Glow…" in
/// depth-first order, which is exactly the order a run authored by hand in the Scene window
/// already sits in. A run built the other way round only needs <see cref="m_ReverseOrder"/>.
///
/// Nothing here reaches for the door itself — the slot passes in what to do when the run
/// finishes, so a level with no pipe still opens its door the instant the battery goes in.
/// </summary>
public class PipeConnection : MonoBehaviour
{
    [Header("Segments")]
    [Tooltip("The glow objects, in the order the power travels — socket end first, door end " +
             "last. Left empty, every descendant whose name starts with \"Glow\" is collected " +
             "in hierarchy order instead, which is the order a hand-authored run is already in.")]
    [SerializeField] private GameObject[] m_GlowSegments;

    [Tooltip("Tick when the run was authored from the door end backwards, so the collected " +
             "order has to be flipped to travel socket-to-door.")]
    [SerializeField] private bool m_ReverseOrder;

    [Header("Timing")]
    [Tooltip("Seconds the charge takes to cross the whole run, however many segments that " +
             "run happens to be — the door opens at the end of it. Timed end to end rather " +
             "than per segment so a long pipe reads at the same pace as a short one instead " +
             "of leaving the player waiting on the plumbing.")]
    [SerializeField] private float m_PowerDuration = 1f;

    // Resolved once in Awake so the auto-collect walk never runs mid-level, and so a run
    // whose segments were collected keeps the same order every time it is powered.
    private GameObject[] m_Resolved;

    private Coroutine m_Routine;

    /// <summary>Seconds a full <see cref="Power"/> run takes before the door is told to open.</summary>
    public float PowerDuration => Mathf.Max(0f, m_PowerDuration);

    private void Awake()
    {
        m_Resolved = ResolveSegments();
        SetAllLit(false);
    }

    /// <summary>
    /// Lights the run from the socket end, spread evenly over
    /// <see cref="m_PowerDuration"/> seconds, and calls <paramref name="onComplete"/> at the
    /// end of it — that callback is what opens the door. A run with no segments completes
    /// straight away rather than swallowing the door open.
    /// </summary>
    public void Power(Action onComplete)
    {
        Unpower();

        if (m_Resolved == null || m_Resolved.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        // isActiveAndEnabled, not activeInHierarchy: a disabled component cannot run a
        // coroutine either, and the door must still open in both cases.
        if (!isActiveAndEnabled)
        {
            SetAllLit(true);
            onComplete?.Invoke();
            return;
        }

        m_Routine = StartCoroutine(PowerRoutine(onComplete));
    }

    /// <summary>
    /// Back to a dark run, dropping any charge still travelling. Called by
    /// <see cref="KeySlot"/> both when the level sets up and when the slot resets — the
    /// running coroutine has to go with it, or a run cancelled halfway would still reach its
    /// end and open a door the slot has just shut.
    /// </summary>
    public void Unpower()
    {
        if (m_Routine != null)
        {
            StopCoroutine(m_Routine);
            m_Routine = null;
        }

        SetAllLit(false);
    }

    // How far along the run the charge has got is read off the clock rather than counted out
    // in waits: a thirty-piece run splits its second into steps shorter than a frame, and a
    // wait per segment would round every one of them up to a whole frame and take three times
    // as long as it was asked to. Off the clock, the run lands on its duration whatever the
    // frame rate and however many pieces it was drawn with.
    private IEnumerator PowerRoutine(Action onComplete)
    {
        float duration = PowerDuration;
        float elapsed = 0f;
        int lit = 0;

        while (elapsed < duration)
        {
            lit = LightTo(lit, Mathf.FloorToInt(elapsed / duration * m_Resolved.Length) + 1);

            yield return null;
            elapsed += Time.deltaTime;
        }

        LightTo(lit, m_Resolved.Length);

        m_Routine = null;
        onComplete?.Invoke();
    }

    // Lights everything from the first segment still dark up to (but not including) reached,
    // and reports back where it got to, so no segment is ever switched on twice.
    private int LightTo(int lit, int reached)
    {
        for (; lit < Mathf.Min(reached, m_Resolved.Length); lit++)
        {
            if (m_Resolved[lit] != null)
                m_Resolved[lit].SetActive(true);
        }

        return lit;
    }

    private void SetAllLit(bool lit)
    {
        if (m_Resolved == null)
            return;

        foreach (GameObject segment in m_Resolved)
        {
            if (segment != null)
                segment.SetActive(lit);
        }
    }

    // The inspector list wins when it has anything in it; otherwise the hierarchy is the
    // authoring surface. GetComponentsInChildren returns depth-first in sibling order, which
    // for "pipe piece, then its glow children" is the order the power travels.
    private GameObject[] ResolveSegments()
    {
        List<GameObject> segments = new List<GameObject>();

        if (m_GlowSegments != null && m_GlowSegments.Length > 0)
        {
            foreach (GameObject segment in m_GlowSegments)
            {
                if (segment != null)
                    segments.Add(segment);
            }
        }
        else
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (child != transform && child.name.StartsWith("Glow", StringComparison.Ordinal))
                    segments.Add(child.gameObject);
            }
        }

        if (m_ReverseOrder)
            segments.Reverse();

        return segments.ToArray();
    }
}
