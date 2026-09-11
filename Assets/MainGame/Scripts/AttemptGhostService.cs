using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Leaves a faded grey copy of the player standing wherever an attempt ended, and keeps it
/// there for the rest of the level.
///
/// The puzzle is solved by rewriting a sequence over and over, and between attempts nothing
/// on screen says what the last one did or how far it got. These markers put both back into
/// the level itself: after every failed attempt the spot the player stopped on is still
/// occupied by a greyed-out ghost of them, and hovering that ghost shows the exact sequence
/// that ended there, drawn with the same slot art the HUD uses.
///
/// That hover panel replaced a fixed bottom-left recap that used to show only the most
/// recent attempt. Putting it on the marker instead means every attempt still on screen can
/// be asked about, and it is read where it happened rather than in a corner.
///
/// Two kinds of marker, one per attempt:
///   • <see cref="RecordStop"/>  — the sequence ran out and the body was pulled back to
///     spawn. The whole body is left behind, in the pose and facing it stopped in.
///   • <see cref="RecordBlast"/> — a hazard killed the player. The death debris' pieces are
///     read off the particle systems where they came to rest and left lying there, so the
///     wreckage of each death stays on the floor exactly where it landed.
///
/// Lifetime: markers are plain scene objects, so they survive
/// <see cref="GameManager.SoftResetLevel"/> (which is what a death runs through) and die with
/// the scene on an explicit restart or a level change. <see cref="ClearAll"/> wipes them the
/// moment the level is won.
///
/// No wiring: the service creates itself the first time something records against it, so
/// every level scene gets it without a per-scene reference. Dropping the component into a
/// scene by hand still works and is how the values below are overridden — put it at the scene
/// root, since it re-parents itself to the root anyway to keep the markers' world scale
/// honest.
/// </summary>
[DisallowMultipleComponent]
public class AttemptGhostService : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Look")]
    [Tooltip("Colour every marker is drawn in. The alpha is the opacity — a ghost has to read " +
             "as a leftover rather than as a second player, so it wants to be well under " +
             "half. The RGB tints the greyscale the shader produces; a hair cooler than " +
             "neutral keeps it from looking like a lighting artefact.")]
    [SerializeField] private Color m_Tint = new Color(0.72f, 0.76f, 0.82f, 0.38f);

    [Tooltip("How far the sprite is pushed toward grey. 1 removes its colour entirely; lower " +
             "values leave some of the original showing through. Ignored when the ghost " +
             "shader is missing, where the tint above is all there is.")]
    [SerializeField, Range(0f, 1f)] private float m_Desaturation = 1f;

    [Tooltip("Multiplies the greyed sprite's brightness. Below 1 the marker sinks into the " +
             "background instead of glowing on top of it.")]
    [SerializeField, Range(0f, 2f)] private float m_Brightness = 0.85f;

    [Tooltip("Seconds the marker takes to fade up once it is placed. A stop marker appears at " +
             "the same moment the body is yanked back to spawn, and popping in at full " +
             "opacity there reads as the player having split in two. Zero places it instantly.")]
    [SerializeField] private float m_FadeInDuration = 0.25f;

    [Header("Sorting")]
    [Tooltip("Added to the sorting order of the renderer being copied, so a marker draws " +
             "behind the live body rather than over it. Keep it negative.")]
    [SerializeField] private int m_SortingOffset = -1;

    [Header("Blast Pieces")]
    [Tooltip("Drops each frozen piece onto the ground beneath it. The debris is read at the " +
             "moment the death fades to black, and the fastest-thrown pieces are still in " +
             "the air at that point — left where they were, they hang in mid-air for the " +
             "rest of the level and read as a bug rather than as wreckage. Off leaves every " +
             "piece exactly where the explosion had it.")]
    [SerializeField] private bool m_SettlePiecesOnGround = true;

    [Tooltip("Layer the pieces are dropped onto. The same layer the debris' own particle " +
             "collision bounces off, so a piece that had already landed does not move.")]
    [SerializeField] private string m_GroundLayer = "Ground";

    [Tooltip("How far below a piece the ground is looked for. Past this there is nothing to " +
             "lie on — a death over a pit — and the piece is left in the air.")]
    [SerializeField] private float m_MaxSettleDrop = 8f;

    [Header("Hover")]
    [Tooltip("Colour of the border drawn around a marker while the mouse is over it. Drawn " +
             "on the INSIDE edge of the silhouette — these sprites are imported with tight " +
             "meshes, so there is no room outside them to draw into.")]
    [SerializeField] private Color m_HoverOutline = new Color(1f, 0.86f, 0.35f, 1f);

    [Tooltip("Thickness of that border, in sprite texels. Byte is a small pixel-art sprite, " +
             "so one texel is already a clear line and three is a thick rim.")]
    [SerializeField, Range(0f, 4f)] private float m_HoverOutlineWidth = 1f;

    [Tooltip("Opacity the marker's body is lifted to while it is hovered. Above the resting " +
             "tint, so picking one out of a row of faded markers brings it forward.")]
    [SerializeField, Range(0f, 1f)] private float m_HoverAlpha = 0.72f;

    [Header("Hover Panel")]
    [Tooltip("Edge length of one slot in the hover panel, in world units. A slot is the same " +
             "box the HUD frames every queued action with, so this is really 'how big should " +
             "a HUD slot look out in the level'. The grid cell is one unit and the player is " +
             "about one tall, so a slot near one unit reads at the weight the HUD does.")]
    [SerializeField, Min(0.05f)] private float m_PanelSlotSize = 0.9f;

    [Tooltip("Gap between two slots, in world units.")]
    [SerializeField, Min(0f)] private float m_PanelSlotSpacing = 0.12f;

    [Tooltip("How far above the marker the panel floats, in world units.")]
    [SerializeField, Min(0f)] private float m_PanelGap = 0.35f;

    [Tooltip("Added to the marker's sorting order to put the panel in front of it and of " +
             "the level art around it.")]
    [SerializeField] private int m_PanelSortingOffset = 50;

    [Header("Bookkeeping")]
    [Tooltip("Most markers kept at once. Past this the oldest is dropped — a level that has " +
             "been failed thirty times would otherwise be a wall of ghosts with nothing " +
             "readable left in it.")]
    [SerializeField] private int m_MaxMarks = 12;

    [Tooltip("Two attempts that ended this close together count as the same spot: the newer " +
             "marker replaces the older one instead of stacking on it. Stacked ghosts add " +
             "their opacity up and the pile ends up darker than the live player. Roughly a " +
             "third of a cell.")]
    [SerializeField] private float m_MergeDistance = 0.35f;

    // ─── Singleton ────────────────────────────────────────────────────────────

    private static AttemptGhostService s_Instance;
    private static bool s_Quitting;

    /// <summary>
    /// The service for the current scene, created on first use. Null only while the
    /// application is shutting down, so callers keep the null-conditional.
    /// </summary>
    public static AttemptGhostService Instance
    {
        get
        {
            if (s_Instance != null) return s_Instance;
            if (s_Quitting || !Application.isPlaying) return null;

            s_Instance = FindAnyObjectByType<AttemptGhostService>(FindObjectsInactive.Exclude);
            if (s_Instance == null)
                s_Instance = new GameObject(nameof(AttemptGhostService))
                    .AddComponent<AttemptGhostService>();

            return s_Instance;
        }
    }

    /// <summary>
    /// Removes every marker, if there are any. Static and null-safe on purpose: the win path
    /// calls this on levels that were solved first try, and asking for <see cref="Instance"/>
    /// there would spin up a service just to empty it.
    /// </summary>
    public static void ClearAll()
    {
        if (s_Instance != null) s_Instance.Clear();
    }

    // ─── State ────────────────────────────────────────────────────────────────

    // One entry per attempt. Origin is where the attempt ended — the body's position for a
    // stop, the seat of the explosion for a blast — and is what the merge test compares
    // against; the pieces of a blast are scattered around it.
    private class Mark
    {
        public Vector3 Origin;
        public GameObject Root;

        // The sequence the player actually submitted on this attempt, snapshotted when the
        // marker was made. This is what the hover panel draws — without it a marker says
        // where the attempt ended but not what was asked for.
        public ActionTypeEnum[] Sequence;

        // Cached at creation: the hover test runs every frame over every marker, and
        // GetComponentsInChildren per marker per frame is pure garbage.
        public SpriteRenderer[] Renderers = System.Array.Empty<SpriteRenderer>();

        public void CacheRenderers()
        {
            if (Root != null) Renderers = Root.GetComponentsInChildren<SpriteRenderer>();
        }
    }

    private readonly List<Mark> m_Marks = new List<Mark>();

    // Reused across a single RecordBlast call rather than allocated per death.
    private readonly List<PieceSample> m_PieceSamples = new List<PieceSample>();

    // Sprites built from the debris' particle textures, keyed by texture. Static and never
    // emptied: there are five of them for the whole game, and rebuilding them on every death
    // would leak a Sprite per piece per death.
    private static readonly Dictionary<Texture2D, Sprite> s_PieceSprites =
        new Dictionary<Texture2D, Sprite>();

    private static readonly int k_DesaturationId = Shader.PropertyToID("_Desaturation");
    private static readonly int k_BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int k_OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int k_OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int k_OutlineTexelId = Shader.PropertyToID("_OutlineTexel");

    private const string k_ShaderResource = "SpriteGhost";
    private const string k_ShaderName = "MainGame/Sprite Ghost";

    private Material m_Material;
    private Material m_HoverMaterial;
    private bool m_MaterialResolved;

    private ParticleSystem.Particle[] m_ParticleBuffer;
    private int m_NextMarkId;

    // Tracked by root GameObject rather than by list index: markers are removed and
    // replaced underneath the pointer, and an index would quietly start pointing at a
    // different attempt.
    private GameObject m_HoveredRoot;

    private GameObject m_Panel;

    // One entry per slot, in step: the frame is the HUD's empty-slot box, the icon is the
    // action drawn on top of it. Exactly the pair PlayerInputUIHelper's own slots are made
    // of, so the panel cannot drift away from the HUD's look.
    private readonly List<SpriteRenderer> m_PanelFrames = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> m_PanelIcons = new List<SpriteRenderer>();
    private PlayerInputUIHelper m_IconSource;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (s_Instance != null && s_Instance != this) { Destroy(gameObject); return; }
        s_Instance = this;

        // Markers are positioned in world space and then parented here, so anything but an
        // identity transform on this object would shear them. It is a container and nothing
        // else, so it can simply be straightened out.
        if (transform.parent != null) transform.SetParent(null, worldPositionStays: false);
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        if (s_Instance == this) s_Instance = null;

        // Built at runtime with HideAndDontSave, so nothing else will ever collect them.
        if (m_Material != null) Destroy(m_Material);
        if (m_HoverMaterial != null) Destroy(m_HoverMaterial);
    }

    private void OnApplicationQuit() { s_Quitting = true; }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Number of attempts currently marked in the level.</summary>
    public int MarkCount => m_Marks.Count;

    /// <summary>
    /// Leaves a marker of the whole body exactly as <paramref name="body"/> is drawing it
    /// right now — sprite frame, facing and all. Call it while the player is still standing
    /// where the attempt ended, before anything moves them back to spawn.
    ///
    /// <paramref name="spawnPosition"/> is skipped rather than marked: an attempt that ended
    /// on the spawn cell would put a ghost directly underneath the live player, where it says
    /// nothing and only costs a marker slot.
    /// </summary>
    public void RecordStop(SpriteRenderer body, Vector3 spawnPosition)
    {
        if (body == null || body.sprite == null) return;

        Vector3 position = body.transform.position;
        if (IsWithinMergeDistance(position, spawnPosition)) return;

        Mark mark = BeginMark(position);
        AddBodyGhost(mark.Root.transform, body, position);
        mark.CacheRenderers();
        StartFadeIn(mark.Root.transform);
    }

    /// <summary>
    /// Leaves the death debris behind as a marker: every piece still in flight on
    /// <paramref name="debris"/> is frozen where it currently is, at the size it was thrown
    /// at. Call it once the pieces have arced, bounced and settled but before their particle
    /// systems expire — <see cref="PlayerController"/> does it on the beat between the
    /// explosion and the fade to black.
    ///
    /// Falls back to a whole-body marker at the player's position when there is no debris to
    /// read (the prefab is optional), so a death always leaves something behind.
    /// </summary>
    public void RecordBlast(GameObject debris, SpriteRenderer body)
    {
        if (body == null) return;

        Vector3 origin = body.transform.position;

        m_PieceSamples.Clear();
        SampleDebris(debris, origin.z, m_PieceSamples);

        if (m_PieceSamples.Count == 0)
        {
            if (body.sprite == null) return;

            Mark fallback = BeginMark(origin);
            AddBodyGhost(fallback.Root.transform, body, origin);
            fallback.CacheRenderers();
            StartFadeIn(fallback.Root.transform);
            return;
        }

        // Resolved once for the whole marker rather than per piece: LayerMask.GetMask is a
        // string lookup, and the answer is the same for all five of them.
        int groundMask = m_SettlePiecesOnGround ? LayerMask.GetMask(m_GroundLayer) : 0;

        Mark mark = BeginMark(origin);
        foreach (PieceSample piece in m_PieceSamples)
        {
            CreateGhost(mark.Root.transform, piece.Sprite,
                        SettleOnGround(piece.Position, piece.Size, groundMask),
                        piece.Rotation, Vector3.one * piece.Size, body);
        }

        m_PieceSamples.Clear();
        mark.CacheRenderers();
        StartFadeIn(mark.Root.transform);
    }

    /// <summary>Removes every marker. Called when the level is won.</summary>
    public void Clear()
    {
        StopAllCoroutines();

        foreach (Mark mark in m_Marks)
            if (mark.Root != null) Destroy(mark.Root);

        m_Marks.Clear();

        m_HoveredRoot = null;
        HidePanel();
    }

    // ─── Marks ────────────────────────────────────────────────────────────────

    // Opens a marker for an attempt that ended at `origin`, first making room for it:
    // anything already marked at the same spot is replaced rather than drawn over, and the
    // oldest marker goes once the cap is reached.
    private Mark BeginMark(Vector3 origin)
    {
        for (int i = m_Marks.Count - 1; i >= 0; i--)
        {
            if (!IsWithinMergeDistance(m_Marks[i].Origin, origin)) continue;
            if (m_Marks[i].Root != null) Destroy(m_Marks[i].Root);
            m_Marks.RemoveAt(i);
        }

        while (m_Marks.Count >= Mathf.Max(1, m_MaxMarks))
        {
            if (m_Marks[0].Root != null) Destroy(m_Marks[0].Root);
            m_Marks.RemoveAt(0);
        }

        var root = new GameObject($"Attempt Ghost {++m_NextMarkId}");
        root.transform.SetParent(transform, worldPositionStays: false);
        root.transform.position = origin;

        var mark = new Mark { Origin = origin, Root = root, Sequence = CaptureSequence() };
        m_Marks.Add(mark);
        return mark;
    }

    // The sequence the player submitted for the attempt that has just ended.
    //
    // Read from the LIVE queue, not from SequenceManager.PreviousSequence. Both markers are
    // made before the turn-end flow reaches SequenceManager.OnTurnEnded — the stop marker
    // from WaitForEndStuff before PlayEnded, the blast marker from DeathRoutine before
    // SoftResetLevel — so at this moment the live queue still holds THIS attempt and
    // PreviousSequence still holds the one before it.
    private static ActionTypeEnum[] CaptureSequence()
    {
        IReadOnlyList<ActionTypeEnum> queued =
            SequenceManager.Instance != null ? SequenceManager.Instance.Sequence : null;

        if (queued == null || queued.Count == 0) return System.Array.Empty<ActionTypeEnum>();

        var copy = new ActionTypeEnum[queued.Count];
        for (int i = 0; i < queued.Count; i++) copy[i] = queued[i];
        return copy;
    }

    // Flattened to XY: this is a 2D game and a marker's Z is only ever the sorting plane, so
    // a difference there must not read as a different spot.
    private bool IsWithinMergeDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return dx * dx + dy * dy <= m_MergeDistance * m_MergeDistance;
    }

    // ─── Ghost construction ───────────────────────────────────────────────────

    private void AddBodyGhost(Transform mark, SpriteRenderer body, Vector3 position)
    {
        // lossyScale, not localScale: the player's facing is a sign flip on the transform's
        // scale, and copying the world value carries that over without having to know it.
        SpriteRenderer ghost = CreateGhost(
            mark, body.sprite, position, body.transform.rotation, body.transform.lossyScale, body);

        // Carried over as well, in case a sprite is ever mirrored through the renderer rather
        // than through the transform.
        ghost.flipX = body.flipX;
        ghost.flipY = body.flipY;
    }

    private SpriteRenderer CreateGhost(
        Transform mark, Sprite sprite, Vector3 position, Quaternion rotation, Vector3 scale,
        SpriteRenderer template)
    {
        var go = new GameObject(sprite != null ? sprite.name : "Ghost");
        go.transform.SetParent(mark, worldPositionStays: false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;   // the mark chain is identity, so this is world scale

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = template.sortingLayerID;
        renderer.sortingOrder = template.sortingOrder + m_SortingOffset;

        // Shared, so every ghost in the level batches into one draw. The per-ghost value —
        // the fade-in alpha — rides on the renderer's own colour instead.
        Material material = GhostMaterial;
        if (material != null) renderer.sharedMaterial = material;

        Color color = m_Tint;
        if (m_FadeInDuration > 0f) color.a = 0f;
        renderer.color = color;

        return renderer;
    }

    // The greyscale material, built once from the shader kept in Resources. Null when the
    // shader cannot be found, which is not fatal: the tint alone still draws a translucent
    // marker, it just keeps the sprite's original hues and cannot draw a hover border.
    private Material GhostMaterial => ResolveMaterials() ? m_Material : null;

    // The same material with the hover border switched on. Swapped in wholesale rather than
    // driven per-renderer through a MaterialPropertyBlock: the SRP batcher ignores property
    // blocks for anything declared in UnityPerMaterial, which is exactly where the outline
    // properties have to live. Only one marker is hovered at a time, so the extra material
    // costs one broken batch at most.
    private Material HoverMaterial => ResolveMaterials() ? m_HoverMaterial : null;

    private bool ResolveMaterials()
    {
        if (m_MaterialResolved) return m_Material != null;
        m_MaterialResolved = true;

        Shader shader = Resources.Load<Shader>(k_ShaderResource);
        if (shader == null) shader = Shader.Find(k_ShaderName);

        if (shader == null)
        {
            Debug.LogWarning(
                $"[AttemptGhostService] Shader '{k_ShaderName}' not found — attempt " +
                "markers fall back to a plain tint and keep their original colours.", this);
            return false;
        }

        m_Material = BuildMaterial(shader, "Attempt Ghost", Color.clear);
        m_HoverMaterial = BuildMaterial(shader, "Attempt Ghost (hovered)", m_HoverOutline);
        return true;
    }

    private Material BuildMaterial(Shader shader, string name, Color outline)
    {
        var material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
        };

        if (material.HasProperty(k_DesaturationId))
            material.SetFloat(k_DesaturationId, m_Desaturation);
        if (material.HasProperty(k_BrightnessId))
            material.SetFloat(k_BrightnessId, m_Brightness);
        if (material.HasProperty(k_OutlineColorId))
            material.SetColor(k_OutlineColorId, outline);
        if (material.HasProperty(k_OutlineWidthId))
            material.SetFloat(k_OutlineWidthId, m_HoverOutlineWidth);

        return material;
    }

    // ─── Debris sampling ──────────────────────────────────────────────────────

    private readonly struct PieceSample
    {
        public readonly Sprite Sprite;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Size;

        public PieceSample(Sprite sprite, Vector3 position, Quaternion rotation, float size)
        {
            Sprite = sprite;
            Position = position;
            Rotation = rotation;
            Size = size;
        }
    }

    // Reads every live particle off the debris instance. One particle is one body piece:
    // ByteDeathDebris is one single-particle system per piece, each with its own material.
    private void SampleDebris(GameObject debris, float z, List<PieceSample> into)
    {
        if (debris == null) return;

        foreach (ParticleSystem system in debris.GetComponentsInChildren<ParticleSystem>())
        {
            int alive = system.particleCount;
            if (alive <= 0) continue;

            Sprite sprite = ResolvePieceSprite(system);
            if (sprite == null) continue;

            if (m_ParticleBuffer == null || m_ParticleBuffer.Length < alive)
                m_ParticleBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(alive)];

            int count = system.GetParticles(m_ParticleBuffer);
            bool isWorldSpace =
                system.main.simulationSpace == ParticleSystemSimulationSpace.World;

            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = m_ParticleBuffer[i];

                Vector3 position = isWorldSpace
                    ? particle.position
                    : system.transform.TransformPoint(particle.position);

                into.Add(new PieceSample(
                    sprite,
                    // Pinned to the player's own plane: the pieces are thrown across XY and
                    // never off it, and a stray Z would sort the marker away from the level.
                    new Vector3(position.x, position.y, z),
                    // Negated: a particle's rotation turns the billboard clockwise, a
                    // transform's Z turns it the other way.
                    Quaternion.Euler(0f, 0f, -particle.rotation),
                    // startSize, NOT the current one. By the time the pieces have settled the
                    // debris' size-over-lifetime curve has already begun shrinking them away,
                    // and freezing that would leave the wreckage undersized.
                    particle.startSize));
            }
        }
    }

    // Drops a piece onto whatever is under it. A piece that had already landed barely moves —
    // it is resting on the same surface this finds — and one still in flight is brought down
    // to the floor below it, which is where it was heading anyway.
    private Vector3 SettleOnGround(Vector3 position, float size, int groundMask)
    {
        if (groundMask == 0) return position;

        // Started above the piece rather than at it, so a piece that has already sunk
        // slightly into the surface still finds that surface instead of reporting a hit at
        // its own position.
        const float k_RayLift = 0.5f;

        RaycastHit2D hit = Physics2D.Raycast(
            new Vector2(position.x, position.y + k_RayLift), Vector2.down,
            k_RayLift + m_MaxSettleDrop, groundMask);

        if (hit.collider == null) return position;

        // Sitting ON the line, not centred on it, with a quarter of the piece below — which
        // is roughly where the debris' own collision leaves the pieces that did land.
        return new Vector3(position.x, hit.point.y + size * 0.25f, position.z);
    }

    // The piece's art, taken from the material the particle system draws it with. The debris
    // prefab is generated (see ByteDeathEffectBuilder) and carries the pieces as plain
    // textures on per-piece materials, so there is no Sprite to borrow — one is built here
    // and cached against the texture.
    private static Sprite ResolvePieceSprite(ParticleSystem system)
    {
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        Material material = renderer != null ? renderer.sharedMaterial : null;
        var texture = material != null ? material.mainTexture as Texture2D : null;
        if (texture == null) return null;

        if (s_PieceSprites.TryGetValue(texture, out Sprite cached) && cached != null)
            return cached;

        // Pixels-per-unit set to the long edge, so the sprite is one world unit across at
        // scale 1 and the caller can size it straight from the particle's own size — which is
        // what the billboard's side length was.
        //
        // FullRect, not the default tight mesh: a tight mesh is traced from the texture's
        // alpha and needs Read/Write enabled, which the piece textures do not have. A quad is
        // the right shape for them anyway.
        float pixelsPerUnit = Mathf.Max(texture.width, texture.height);
        Sprite sprite = Sprite.Create(
            texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
            pixelsPerUnit, 0, SpriteMeshType.FullRect);

        sprite.name = $"{texture.name} (ghost)";
        sprite.hideFlags = HideFlags.HideAndDontSave;

        s_PieceSprites[texture] = sprite;
        return sprite;
    }

    // ─── Fade in ──────────────────────────────────────────────────────────────

    private void StartFadeIn(Transform mark)
    {
        if (m_FadeInDuration <= 0f) return;   // CreateGhost already placed it at full tint
        StartCoroutine(FadeInRoutine(mark));
    }

    // Unscaled: a marker is placed during the death flow, which runs on realtime waits and
    // can be sitting under a stopped clock.
    private IEnumerator FadeInRoutine(Transform mark)
    {
        SpriteRenderer[] renderers = mark.GetComponentsInChildren<SpriteRenderer>();
        float elapsed = 0f;

        while (elapsed < m_FadeInDuration)
        {
            if (mark == null) yield break;

            elapsed += Time.unscaledDeltaTime;
            float alpha = m_Tint.a * Mathf.Clamp01(elapsed / m_FadeInDuration);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null) continue;

                Color color = m_Tint;
                color.a = alpha;
                renderer.color = color;
            }

            yield return null;
        }

        foreach (SpriteRenderer renderer in renderers)
            if (renderer != null) renderer.color = m_Tint;
    }

    // --- Hover ----------------------------------------------------------------

    // Markers carry no colliders, on purpose: this is a grid game whose spike and brick
    // logic runs on overlap tests, and dropping a dozen new colliders into the level to
    // support a tooltip would be a real risk for a cosmetic feature. Hit-testing the
    // renderers' own bounds costs nothing at this count and touches no physics at all.
    private void Update()
    {
        if (m_Marks.Count == 0)
        {
            if (m_HoveredRoot != null) { ClearHighlight(); HidePanel(); m_HoveredRoot = null; }
            return;
        }

        Mark hovered = FindHoveredMark();
        GameObject root = hovered != null ? hovered.Root : null;

        if (root != m_HoveredRoot)
        {
            ClearHighlight();
            m_HoveredRoot = root;

            if (hovered != null)
            {
                ApplyHighlight(hovered);
                BuildPanel(hovered);
            }
            else
            {
                HidePanel();
            }
        }

        if (hovered != null) PositionPanel(hovered);
    }

    private Mark FindHoveredMark()
    {
        Camera camera = Camera.main;
        Mouse mouse = Mouse.current;
        if (camera == null || mouse == null) return null;

        Vector2 screen = mouse.position.ReadValue();
        if (screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height)
            return null;

        // Distance from the camera along its own forward axis, so this reads correctly for a
        // perspective camera as well as the orthographic one the game ships with.
        float depth = Mathf.Abs(camera.transform.position.z);
        Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        var point = new Vector2(world.x, world.y);

        // Newest first: markers can overlap, and the one the player just made is the one
        // they are most likely asking about.
        for (int i = m_Marks.Count - 1; i >= 0; i--)
        {
            if (m_Marks[i].Root == null) continue;
            if (IsPointOverMark(m_Marks[i], point)) return m_Marks[i];
        }

        return null;
    }

    // Each renderer is tested on its own rather than against the marker's combined bounds.
    // A blast marker's pieces are scattered across several cells, and one box around all of
    // them would claim a large stretch of empty floor between them.
    private static bool IsPointOverMark(Mark mark, Vector2 point)
    {
        foreach (SpriteRenderer renderer in mark.Renderers)
        {
            if (renderer == null || !renderer.enabled) continue;

            Bounds bounds = renderer.bounds;
            if (point.x >= bounds.min.x && point.x <= bounds.max.x &&
                point.y >= bounds.min.y && point.y <= bounds.max.y)
                return true;
        }

        return false;
    }

    private void ApplyHighlight(Mark mark)
    {
        Material material = HoverMaterial;
        Color color = m_Tint;
        color.a = m_HoverAlpha;

        // The outline walks one texel out from each pixel to find the silhouette's edge, so
        // it needs to know how big a texel is in UV. Fed from the sprite itself rather than
        // read from Unity's _MainTex_TexelSize, which does not survive this shader pass —
        // and a wrong step here does not soften the outline, it floods the whole body with
        // it. Set per hover because a body marker and a debris piece are different sizes,
        // and only one marker is ever lit at a time.
        SpriteRenderer reference = FirstRenderer(mark);
        Texture texture = reference != null && reference.sprite != null
            ? reference.sprite.texture : null;

        if (material != null && texture != null && texture.width > 0 && texture.height > 0)
        {
            material.SetVector(k_OutlineTexelId, new Vector4(
                1f / texture.width, 1f / texture.height, texture.width, texture.height));
        }

        foreach (SpriteRenderer renderer in mark.Renderers)
        {
            if (renderer == null) continue;
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
        }
    }

    private void ClearHighlight()
    {
        if (m_HoveredRoot == null) return;

        Material material = GhostMaterial;

        foreach (SpriteRenderer renderer in m_HoveredRoot.GetComponentsInChildren<SpriteRenderer>())
        {
            if (renderer == null) continue;
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = m_Tint;
        }
    }

    // --- Hover panel ----------------------------------------------------------

    // Draws the attempt's queued actions as a row of HUD slots floating above the marker.
    // The frame art and the action icons both come from PlayerInputUIHelper -- the same
    // source the HUD's own slot row reads from -- so the panel is literally the HUD's slots
    // rendered out in the world rather than a second look that has to be kept in step.
    private void BuildPanel(Mark mark)
    {
        if (m_IconSource == null)
            m_IconSource = SceneObjects.FindInActiveScene<PlayerInputUIHelper>();

        int shown = 0;

        if (m_IconSource != null && mark.Sequence != null)
        {
            EnsurePanel();

            // The "Any" sprite IS the empty slot box the HUD frames every action with.
            Sprite frameSprite = m_IconSource.GetSpriteForAction(ActionTypeEnum.Any);

            for (int i = 0; i < mark.Sequence.Length; i++)
            {
                // Interact is not a queued movement command and has no icon of its own --
                // the HUD's slot row leaves it out for the same reason.
                if (mark.Sequence[i] == ActionTypeEnum.Interact) continue;

                Sprite icon = m_IconSource.GetSpriteForAction(mark.Sequence[i]);
                if (icon == null) continue;

                EnsureSlot(shown);

                SpriteRenderer frame = m_PanelFrames[shown];
                frame.sprite = frameSprite;
                frame.enabled = frameSprite != null;
                frame.transform.localScale = FitScale(frameSprite);

                SpriteRenderer slot = m_PanelIcons[shown];
                slot.sprite = icon;
                slot.transform.localScale = FitScale(icon);

                frame.gameObject.SetActive(true);
                slot.gameObject.SetActive(true);
                shown++;
            }
        }

        // An attempt with nothing drawable -- no icon source in the scene, or a marker made
        // outside the normal turn flow -- still highlights, it just has nothing to say.
        if (shown == 0) { HidePanel(); return; }

        for (int i = shown; i < m_PanelFrames.Count; i++)
        {
            m_PanelFrames[i].gameObject.SetActive(false);
            m_PanelIcons[i].gameObject.SetActive(false);
        }

        LayOutPanel(mark, shown);
        m_Panel.SetActive(true);
    }

    // Uniform scale that makes a sprite's longest side exactly one slot across. Taken from
    // the sprite's own world bounds, so art authored at different pixel sizes or pixels-per-
    // unit still comes out the same size on the row -- the world-space equivalent of the
    // HUD's preserveAspect.
    private Vector3 FitScale(Sprite sprite)
    {
        if (sprite == null) return Vector3.one;

        float extent = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        return Vector3.one * (extent > 0f ? m_PanelSlotSize / extent : 1f);
    }

    private void LayOutPanel(Mark mark, int count)
    {
        float width = count * m_PanelSlotSize + (count - 1) * m_PanelSlotSpacing;

        for (int i = 0; i < count; i++)
        {
            float x = -width * 0.5f + m_PanelSlotSize * 0.5f
                      + i * (m_PanelSlotSize + m_PanelSlotSpacing);
            var position = new Vector3(x, 0f, 0f);
            m_PanelFrames[i].transform.localPosition = position;
            m_PanelIcons[i].transform.localPosition = position;
        }

        // Sorted off the marker it belongs to, so the panel sits in front of the level art
        // around it whatever layer the markers ended up on.
        SpriteRenderer reference = FirstRenderer(mark);
        int layer = reference != null ? reference.sortingLayerID : 0;
        int order = (reference != null ? reference.sortingOrder : 0) + m_PanelSortingOffset;

        for (int i = 0; i < count; i++)
        {
            m_PanelFrames[i].sortingLayerID = layer;
            m_PanelFrames[i].sortingOrder = order;
            m_PanelIcons[i].sortingLayerID = layer;
            m_PanelIcons[i].sortingOrder = order + 1;   // the arrow sits on its box
        }
    }

    // Positioned every frame rather than parented to the marker: the panel has to sit above
    // the marker's drawn TOP, and a blast marker's pieces are spread over an area whose top
    // is nowhere near its origin.
    private void PositionPanel(Mark mark)
    {
        if (m_Panel == null || !m_Panel.activeSelf) return;
        if (!TryGetMarkBounds(mark, out Bounds bounds)) return;

        m_Panel.transform.position = new Vector3(
            bounds.center.x,
            bounds.max.y + m_PanelGap + m_PanelSlotSize * 0.5f,
            mark.Origin.z);
    }

    private static bool TryGetMarkBounds(Mark mark, out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (SpriteRenderer renderer in mark.Renderers)
        {
            if (renderer == null || !renderer.enabled) continue;

            if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        return any;
    }

    private static SpriteRenderer FirstRenderer(Mark mark)
    {
        foreach (SpriteRenderer renderer in mark.Renderers)
            if (renderer != null) return renderer;

        return null;
    }

    private void HidePanel()
    {
        if (m_Panel != null) m_Panel.SetActive(false);
    }

    private void EnsurePanel()
    {
        if (m_Panel != null) return;

        m_Panel = new GameObject("Attempt Ghost Hover Panel");
        m_Panel.transform.SetParent(transform, worldPositionStays: false);
        m_Panel.SetActive(false);
    }

    // Pooled, never destroyed: hovering back and forth along a row of markers would
    // otherwise churn a fresh set of objects on every crossing.
    private void EnsureSlot(int index)
    {
        while (m_PanelFrames.Count <= index)
        {
            var frame = new GameObject("Slot " + m_PanelFrames.Count);
            frame.transform.SetParent(m_Panel.transform, worldPositionStays: false);
            m_PanelFrames.Add(frame.AddComponent<SpriteRenderer>());

            // Parented to the frame so the two move together, but NOT scaled by it: the
            // frame carries its own fit scale, and nesting the icon under it would multiply
            // the two together and shrink every arrow.
            var icon = new GameObject("Icon");
            icon.transform.SetParent(m_Panel.transform, worldPositionStays: false);
            m_PanelIcons.Add(icon.AddComponent<SpriteRenderer>());
        }
    }
}
