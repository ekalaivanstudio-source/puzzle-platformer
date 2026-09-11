using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One group of things that assemble into (or out of) a level together, driven off a single
/// clock — the motion behind <see cref="LevelBuildDirector"/>.
///
/// A wave is prepared ONCE, when the level loads, and then poked every frame with "this many
/// seconds in". Nothing here allocates, searches or decides ordering while it is running: the
/// director works out WHAT is in a wave and in WHICH order, and a wave only knows HOW the
/// things in it move. That split is what lets ground tiles, scenery, pipe pieces and doorways
/// all be sequenced by the same handful of lines in the director.
///
/// Every wave runs backwards as well as forwards — the level's outro is the same wave played
/// with <c>building: false</c>, which also reverses the order, so the last thing to appear is
/// the first thing to leave.
/// </summary>
public interface ILevelBuildWave
{
    /// <summary>How many items this wave drives. Zero means the phase has nothing to play.</summary>
    int Count { get; }

    /// <summary>
    /// Seconds from the first item starting to the last one settling — the spread across the
    /// group plus the time one item takes on its own.
    /// </summary>
    float Duration { get; }

    /// <summary>
    /// Drives every item to where it should be <paramref name="elapsed"/> seconds into the
    /// wave. <paramref name="building"/> false plays the same wave in reverse, both in motion
    /// (items shrink away instead of popping in) and in order.
    /// </summary>
    void Apply(float elapsed, bool building);

    /// <summary>
    /// Snaps every item to an end state and hands back whatever the wave borrowed — exact
    /// authored scale and position when <paramref name="built"/>, fully hidden when not.
    /// Called once at each end of a wave, and used on its own to hide a level before its
    /// first frame renders.
    /// </summary>
    void Finish(bool built);
}

/// <summary>
/// The easing curves the level build is drawn with. Two of them, because the whole look is
/// one idea: things overshoot on the way in, and wind up before they leave.
/// </summary>
public static class LevelBuildEase
{
    // The classic "back" constant. 1.70158 overshoots by roughly 10%, which is enough to read
    // as a pop at the sizes and durations this system runs at without looking rubbery.
    private const float k_Back = 1.70158f;

    /// <summary>Shoots past 1 and settles back — the pop.</summary>
    public static float OutBack(float x)
    {
        float t = x - 1f;
        return 1f + (k_Back + 1f) * t * t * t + k_Back * t * t;
    }

    /// <summary>Dips below 0 first — the wind-up, used to take things away again.</summary>
    public static float InBack(float x) => (k_Back + 1f) * x * x * x - k_Back * x * x;

    /// <summary>Plain deceleration, for anything that should not bounce.</summary>
    public static float OutCubic(float x)
    {
        float t = 1f - x;
        return 1f - t * t * t;
    }
}

/// <summary>
/// A group of loose Transforms that pop in one after another — the level's scenery, its props
/// and its run of pipe. Scale ONLY (and an optional spin): position is never touched, so a
/// moving platform mid-patrol keeps moving while it pops in, and every object stays exactly
/// where its own script put it.
/// </summary>
public sealed class TransformPopWave : ILevelBuildWave
{
    private readonly Transform[] m_Transforms;
    private readonly Vector3[] m_Scales;
    private readonly Quaternion[] m_Rotations;
    private readonly float[] m_Delays;      // seconds into the wave, 0..spread
    private readonly float m_Spread;
    private readonly float m_ItemDuration;
    private readonly float m_Spin;

    // Only populated when the caller asked for collider management. Scaling a collider to
    // nothing makes Physics2D rebuild a shape it cannot use; switching them off for the
    // length of the wave skips that entirely.
    private readonly Collider2D[] m_Colliders;
    private readonly bool[] m_ColliderEnabled;

    /// <param name="items">The transforms, already in the order they should appear.</param>
    /// <param name="normalisedDelays">Each item's place in the wave, 0..1, same length as
    /// <paramref name="items"/>. The director decides this — a spatial sweep for scenery, a
    /// straight count for pipe pieces.</param>
    /// <param name="spread">Seconds between the first item starting and the last.</param>
    /// <param name="itemDuration">Seconds one item takes to pop.</param>
    /// <param name="spin">Degrees an item is turned by at the start of its pop, unwinding to
    /// its authored rotation. 0 leaves rotation alone entirely.</param>
    /// <param name="manageColliders">Switch this group's 2D colliders off while it is hidden
    /// and restore them when it has finished building.</param>
    public TransformPopWave(
        IReadOnlyList<Transform> items,
        IReadOnlyList<float> normalisedDelays,
        float spread,
        float itemDuration,
        float spin,
        bool manageColliders)
    {
        int count = items != null ? items.Count : 0;
        m_Transforms = new Transform[count];
        m_Scales = new Vector3[count];
        m_Rotations = new Quaternion[count];
        m_Delays = new float[count];
        m_Spread = Mathf.Max(0f, spread);
        m_ItemDuration = Mathf.Max(0.01f, itemDuration);
        m_Spin = spin;

        for (int i = 0; i < count; i++)
        {
            Transform item = items[i];
            m_Transforms[i] = item;
            m_Scales[i] = item != null ? item.localScale : Vector3.one;
            m_Rotations[i] = item != null ? item.localRotation : Quaternion.identity;

            float normalised = normalisedDelays != null && i < normalisedDelays.Count
                ? Mathf.Clamp01(normalisedDelays[i])
                : 0f;
            m_Delays[i] = normalised * m_Spread;
        }

        if (!manageColliders)
        {
            m_Colliders = System.Array.Empty<Collider2D>();
            m_ColliderEnabled = System.Array.Empty<bool>();
            return;
        }

        var colliders = new List<Collider2D>();
        var buffer = new List<Collider2D>();
        for (int i = 0; i < count; i++)
        {
            if (m_Transforms[i] == null) continue;
            m_Transforms[i].GetComponentsInChildren(false, buffer);
            colliders.AddRange(buffer);
        }

        m_Colliders = colliders.ToArray();
        m_ColliderEnabled = new bool[m_Colliders.Length];
        for (int i = 0; i < m_Colliders.Length; i++)
            m_ColliderEnabled[i] = m_Colliders[i] != null && m_Colliders[i].enabled;
    }

    public int Count => m_Transforms.Length;

    public float Duration => m_Spread + m_ItemDuration;

    public void Apply(float elapsed, bool building)
    {
        for (int i = 0; i < m_Transforms.Length; i++)
        {
            Transform item = m_Transforms[i];
            if (item == null) continue;

            // Reversed as well as rewound: on the way out the item that appeared last is the
            // first to go, which is what makes a teardown read as the level unbuilding itself
            // rather than as a second, unrelated animation.
            float delay = building ? m_Delays[i] : m_Spread - m_Delays[i];
            float local = Mathf.Clamp01((elapsed - delay) / m_ItemDuration);
            float scale = building
                ? LevelBuildEase.OutBack(local)
                : 1f - LevelBuildEase.InBack(local);

            item.localScale = m_Scales[i] * Mathf.Max(0f, scale);

            if (m_Spin == 0f) continue;

            // Alternating sign, so a row of props does not all lean the same way.
            float sign = (i & 1) == 0 ? 1f : -1f;
            float remaining = building ? 1f - local : local;
            item.localRotation = m_Rotations[i] * Quaternion.Euler(0f, 0f, m_Spin * remaining * sign);
        }
    }

    public void Finish(bool built)
    {
        for (int i = 0; i < m_Transforms.Length; i++)
        {
            Transform item = m_Transforms[i];
            if (item == null) continue;

            item.localScale = built ? m_Scales[i] : Vector3.zero;
            if (m_Spin != 0f) item.localRotation = m_Rotations[i];
        }

        SetCollidersEnabled(built);
    }

    // Restores each collider to the state it was found in rather than blanket-enabling them:
    // a level holds plenty of colliders that are meant to start switched off.
    private void SetCollidersEnabled(bool enabled)
    {
        for (int i = 0; i < m_Colliders.Length; i++)
        {
            if (m_Colliders[i] == null) continue;
            m_Colliders[i].enabled = enabled && m_ColliderEnabled[i];
        }
    }
}

/// <summary>
/// A single object growing up out of the floor — the level's doorways.
///
/// It grows from its own BASE rather than sliding up from underground, and that is
/// deliberate: a door slid up from below the floor would be visible through it, because the
/// doorway art draws in front of the ground. Anchoring the growth to the bottom edge of the
/// door's own renderer bounds gives the same read — something rising out of the ground — with
/// nothing to clip and no sprite mask to maintain. The width starts wide and pinches in as
/// the door overshoots its full height, which is what sells the weight.
/// </summary>
public sealed class TransformRiseWave : ILevelBuildWave
{
    private readonly Transform m_Transform;
    private readonly Vector3 m_Scale;
    private readonly Vector3 m_Position;    // localPosition, restored exactly when finished
    private readonly float m_AnchorY;       // parent-local Y of the door's base
    private readonly float m_Duration;
    private readonly float m_WidthSquash;

    /// <param name="target">The doorway object. Its whole subtree rises with it.</param>
    /// <param name="duration">Seconds the rise takes.</param>
    /// <param name="widthSquash">How much wider than authored the door starts, 0..1.</param>
    public TransformRiseWave(Transform target, float duration, float widthSquash)
    {
        m_Transform = target;
        m_Duration = Mathf.Max(0.01f, duration);
        m_WidthSquash = Mathf.Max(0f, widthSquash);

        if (target == null) return;

        m_Scale = target.localScale;
        m_Position = target.localPosition;
        m_AnchorY = ResolveBaseY(target);
    }

    public int Count => m_Transform != null ? 1 : 0;

    public float Duration => m_Duration;

    public void Apply(float elapsed, bool building)
    {
        float local = Mathf.Clamp01(elapsed / m_Duration);
        float height = building
            ? LevelBuildEase.OutBack(local)
            : 1f - LevelBuildEase.InBack(local);

        SetHeight(height);
    }

    // A finished rise writes the captured values straight back rather than evaluating the
    // curve at 1: the width lerp lands a float hair short of the authored scale, and a door
    // that has stopped moving should be bit for bit where the scene put it — the interaction
    // point hanging off it is read from that transform for the rest of the level.
    public void Finish(bool built)
    {
        if (m_Transform == null) return;

        if (!built)
        {
            SetHeight(0f);
            return;
        }

        m_Transform.localScale = m_Scale;
        m_Transform.localPosition = m_Position;
    }

    private void SetHeight(float height)
    {
        if (m_Transform == null) return;

        float width = Mathf.LerpUnclamped(1f + m_WidthSquash, 1f, height);

        m_Transform.localScale = new Vector3(
            m_Scale.x * width, m_Scale.y * Mathf.Max(0f, height), m_Scale.z);

        m_Transform.localPosition = new Vector3(
            m_Position.x, m_AnchorY + (m_Position.y - m_AnchorY) * height, m_Position.z);
    }

    // The bottom edge of everything the door draws, in the parent's space. Renderer bounds
    // rather than the transform's own position, because a doorway's pivot is wherever the art
    // was authored around — usually its middle, which would grow the door out of thin air
    // from its waist.
    private static float ResolveBaseY(Transform target)
    {
        Vector3 origin = target.position;
        float baseWorldY = origin.y;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            baseWorldY = bounds.min.y;
        }

        Vector3 basePoint = new Vector3(origin.x, baseWorldY, origin.z);
        return target.parent != null
            ? target.parent.InverseTransformPoint(basePoint).y
            : basePoint.y;
    }
}

/// <summary>
/// The level's floor, laid one tile at a time.
///
/// Ground is a <see cref="Tilemap"/>, not a GameObject per tile, so there is no transform to
/// animate — instead each CELL gets its own transform matrix, which is what
/// <see cref="Tilemap.SetTransformMatrix"/> exists for. The matrix is applied around the
/// tilemap's tile anchor (the cell centre by default), so a scale matrix pops the tile in
/// place rather than sliding it out of its cell.
///
/// The tilemap's colliders are switched off for the length of the wave. A per-cell matrix
/// feeds the collider geometry too, so leaving them on would rebuild the whole floor's
/// collision every frame of the intro — for shapes nothing can touch yet, since the player is
/// still waiting in the doorway.
/// </summary>
public sealed class TilemapPopWave : ILevelBuildWave
{
    private readonly Tilemap m_Tilemap;
    private readonly Vector3Int[] m_Cells;
    private readonly float[] m_Delays;      // seconds into the wave, 0..spread
    private readonly float[] m_Applied;     // last scale written per cell, so idle cells are skipped
    private readonly float m_Spread;
    private readonly float m_ItemDuration;
    private readonly float m_Spin;

    private readonly Collider2D[] m_Colliders;
    private readonly bool[] m_ColliderEnabled;

    public TilemapPopWave(
        Tilemap tilemap,
        IReadOnlyList<Vector3Int> cells,
        IReadOnlyList<float> normalisedDelays,
        float spread,
        float itemDuration,
        float spin)
    {
        m_Tilemap = tilemap;
        m_Spread = Mathf.Max(0f, spread);
        m_ItemDuration = Mathf.Max(0.01f, itemDuration);
        m_Spin = spin;

        int count = cells != null ? cells.Count : 0;
        m_Cells = new Vector3Int[count];
        m_Delays = new float[count];
        m_Applied = new float[count];

        for (int i = 0; i < count; i++)
        {
            m_Cells[i] = cells[i];

            float normalised = normalisedDelays != null && i < normalisedDelays.Count
                ? Mathf.Clamp01(normalisedDelays[i])
                : 0f;
            m_Delays[i] = normalised * m_Spread;
            m_Applied[i] = float.NaN;   // nothing written yet, so the first Apply always lands

            if (m_Tilemap == null) continue;

            // A tile asset that locks its transform silently ignores every matrix set on it.
            // Unlocking is per-cell state on THIS tilemap, not an edit to the tile asset, so
            // it goes no further than the loaded level.
            TileFlags flags = m_Tilemap.GetTileFlags(m_Cells[i]);
            if ((flags & TileFlags.LockTransform) != 0)
                m_Tilemap.SetTileFlags(m_Cells[i], flags & ~TileFlags.LockTransform);
        }

        m_Colliders = tilemap != null
            ? tilemap.GetComponentsInChildren<Collider2D>(true)
            : System.Array.Empty<Collider2D>();

        m_ColliderEnabled = new bool[m_Colliders.Length];
        for (int i = 0; i < m_Colliders.Length; i++)
            m_ColliderEnabled[i] = m_Colliders[i] != null && m_Colliders[i].enabled;
    }

    public int Count => m_Cells.Length;

    public float Duration => m_Spread + m_ItemDuration;

    public void Apply(float elapsed, bool building)
    {
        if (m_Tilemap == null) return;

        for (int i = 0; i < m_Cells.Length; i++)
        {
            float delay = building ? m_Delays[i] : m_Spread - m_Delays[i];
            float local = Mathf.Clamp01((elapsed - delay) / m_ItemDuration);
            float scale = building
                ? LevelBuildEase.OutBack(local)
                : 1f - LevelBuildEase.InBack(local);
            scale = Mathf.Max(0f, scale);

            // Every write dirties the tilemap and costs a chunk rebuild, so a cell that has
            // not moved since last frame — one still waiting its turn, or one already
            // settled — is left alone.
            if (scale == m_Applied[i]) continue;
            m_Applied[i] = scale;

            Quaternion rotation = m_Spin == 0f
                ? Quaternion.identity
                : Quaternion.Euler(
                    0f, 0f,
                    m_Spin * (building ? 1f - local : local) * ((i & 1) == 0 ? 1f : -1f));

            m_Tilemap.SetTransformMatrix(
                m_Cells[i], Matrix4x4.TRS(Vector3.zero, rotation, new Vector3(scale, scale, 1f)));
        }
    }

    public void Finish(bool built)
    {
        if (m_Tilemap == null) return;

        Matrix4x4 matrix = built ? Matrix4x4.identity : Matrix4x4.Scale(Vector3.zero);

        for (int i = 0; i < m_Cells.Length; i++)
        {
            m_Applied[i] = built ? 1f : 0f;
            m_Tilemap.SetTransformMatrix(m_Cells[i], matrix);
        }

        for (int i = 0; i < m_Colliders.Length; i++)
        {
            if (m_Colliders[i] == null) continue;
            m_Colliders[i].enabled = built && m_ColliderEnabled[i];
        }
    }
}
