using System.Collections;
using System.Collections.Generic;
using MainGame.UI.Unified;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// The level assembling itself in front of the player, and taking itself apart again when
/// they leave — one system, one component, one per level scene.
///
/// ─── The sequence ────────────────────────────────────────────────────────────────────────
/// The screen fades up on an empty level (only the far background is left standing), and then:
///
///   1. the GROUND lays itself in, tile by tile, on a wave that sweeps across the level,
///   2. the SCENERY and props pop in behind it, on the same sweep,
///   3. the EXIT DOOR grows up out of the floor, and only once it is standing is it allowed
///      to light its doorway effect — see <see cref="LevelExitDoor.SetStanding"/>,
///   4. the PIPE run appears piece by piece, from the socket to that door,
///   5. the HUD pops in over the top of it, an element at a time and a CANVAS at a time —
///      the tutorial prompt waits for the input row under it to finish arriving,
///   6. the ENTRY DOOR grows up out of the floor — and <see cref="LevelEntryDoor"/> takes it
///      from there: the door opens, the player spins out of it, the door shuts behind them,
///      and the doorway sinks back into the floor, leaving the level clean to play on.
///
/// That last beat is a doorway, not a one-off: every later arrival plays it again. A death
/// respawns the player through it — the doorway rises, opens, hands them back out and sinks
/// away — so coming back after a mistake reads as arriving rather than as being teleported.
///
/// When the level is won, <see cref="PlayerController"/> shuts the exit door behind the player
/// and then runs the whole thing backwards: the HUD goes away an element at a time, the pipe
/// empties out from the door end, and the exit doorway sinks back into the floor before the
/// screen fades — joined by the entry doorway on a level that was set to keep that one
/// standing.
///
/// ─── What it animates, and how it finds it ───────────────────────────────────────────────
/// Nothing needs wiring per level. Left empty, every field below is resolved from the open
/// scene as it loads: the ground is every <see cref="Tilemap"/> in it, the doorways and the pipe
/// run are the components that own them, and the scenery is everything else in the scene that
/// draws something — collected as the TOPMOST object carrying a renderer, so a bush is one
/// item and the prefab of forty bushes is forty. Anything already switched off in the scene is
/// left alone, and so is anything drawing on a background sorting layer, so the backdrop the
/// level is built against does not pop in with it. The HUD is found the same way — every
/// canvas in the scene, walked down to the elements that draw — minus the two things a build
/// must never scale: a SCREEN, which opens and closes itself, and a full-screen VEIL like the
/// fade or the brightness sheet, which would read as the whole picture shrinking. Each canvas
/// is its own group, and they come in one after another rather than all at once.
///
/// The serialized lists are overrides for the levels that want one, not required set-up.
///
/// ─── The rules it plays by ───────────────────────────────────────────────────────────────
/// Everything is hidden before the first frame renders, so the fade never reveals a level that
/// then vanishes. Everything is driven on UNSCALED time, like the rest of the
/// level's opening, so a paused or slowed game still builds at the authored pace. And every
/// item is put back to its exact authored transform when its wave finishes — a built level is
/// bit for bit the level the scene holds, with no drift for the rest of the game to inherit.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class LevelBuildDirector : MonoBehaviour
{
    /// <summary>How long one phase of the build takes, and how far it runs into the next.</summary>
    [System.Serializable]
    public class PhaseTiming
    {
        [Tooltip("Seconds between the FIRST item of the phase starting and the LAST one " +
                 "starting. This is what makes the phase read as one-by-one rather than all " +
                 "at once — and it is a TOTAL, so a level with four hundred tiles takes " +
                 "exactly as long as one with forty.")]
        [Min(0f)] public float Spread = 0.4f;

        [Tooltip("Seconds a single item takes to pop from nothing to full size.")]
        [Min(0.01f)] public float ItemDuration = 0.26f;

        [Tooltip("Seconds the NEXT phase starts before this one has finished. A little " +
                 "overlap is what keeps the whole build feeling like one motion instead of " +
                 "five that queue politely behind each other.")]
        [Min(0f)] public float Overlap = 0.12f;

        public PhaseTiming(float spread, float itemDuration, float overlap)
        {
            Spread = spread;
            ItemDuration = itemDuration;
            Overlap = overlap;
        }
    }

    private enum State { Hidden, Building, Built, TearingDown, TornDown }

    // ─── Singleton ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The director in the level being played, or null in a level that has none — every
    /// caller keeps the null check, because the intro and the win both have to work in a
    /// scene where nobody dropped this in.
    /// </summary>
    public static LevelBuildDirector Instance { get; private set; }

    // ─── What gets built ──────────────────────────────────────────────────────────────────

    [Header("What Gets Built (leave empty to find it in the scene)")]
    [Tooltip("The tilemaps laid in during phase 1. Empty: every tilemap in the open scene.")]
    [SerializeField] private Tilemap[] m_GroundTilemaps;

    [Tooltip("Roots the scenery is collected from during phase 2. Empty: every root object " +
             "in the open scene, minus the ground, the doorways, the pipe run, the player " +
             "and the exclusions below.")]
    [SerializeField] private Transform[] m_SceneryRoots;

    [Tooltip("Objects the build must never touch, with everything under them. Use this for " +
             "anything that has to be visible or running from the first frame.")]
    [SerializeField] private Transform[] m_Excluded;

    [Tooltip("Sorting layers whose renderers are left standing — the backdrop the level is " +
             "built against, which should be there before the first tile lands.")]
    [SerializeField] private string[] m_BackdropSortingLayers = { "Bg1", "Bg2" };

    [Tooltip("The run of pipe generated in phase 4. Empty: the PipeConnection in the scene.")]
    [SerializeField] private PipeConnection m_Pipes;

    [Tooltip("The doorway raised in phase 3. Empty: the LevelExitDoor in the scene.")]
    [SerializeField] private LevelExitDoor m_ExitDoor;

    [Tooltip("The canvases the HUD is collected from during phase 5, each popped as its " +
             "own group in this order. Empty: every canvas in the open scene, in scene order.")]
    [SerializeField] private RectTransform[] m_UIRoots;

    [Tooltip("The doorway raised in phase 6, which then opens and lets the player in. " +
             "Empty: the LevelEntryDoor in the scene.")]
    [SerializeField] private LevelEntryDoor m_EntryDoor;

    // ─── Timing ───────────────────────────────────────────────────────────────────────────

    [Header("Timing")]
    [Tooltip("Direction the ground and scenery waves sweep across the level, in degrees. " +
             "0 is straight left-to-right; a little tilt makes the wave read as a diagonal " +
             "wipe rather than a wall of tiles marching across.")]
    [SerializeField] private float m_SweepAngle = 20f;

    [SerializeField] private PhaseTiming m_GroundTiming = new PhaseTiming(0.45f, 0.26f, 0.15f);
    [SerializeField] private PhaseTiming m_SceneryTiming = new PhaseTiming(0.30f, 0.24f, 0.12f);
    [SerializeField] private PhaseTiming m_PipeTiming = new PhaseTiming(0.30f, 0.20f, 0.08f);

    // Spread and duration are PER CANVAS, since the canvases queue behind each other. The
    // overlap is not: only the last canvas overlaps, and it is a long one — the HUD arriving
    // is the phase the player is least likely to be looking at, so it finishes UNDER the
    // entry door rising rather than holding it up.
    [SerializeField] private PhaseTiming m_UITiming = new PhaseTiming(0.35f, 0.22f, 0.45f);

    [Tooltip("Seconds a doorway takes to grow up out of the floor.")]
    [Min(0.05f)][SerializeField] private float m_DoorRiseDuration = 0.38f;

    [Tooltip("Seconds a doorway takes to sink back into it when the level is over.")]
    [Min(0.05f)][SerializeField] private float m_DoorSinkDuration = 0.30f;

    [Tooltip("How much wider than authored a doorway starts as it rises, 0..1. The width " +
             "pinches back in as the door overshoots its full height — the squash and " +
             "stretch that gives it weight.")]
    [Range(0f, 0.6f)][SerializeField] private float m_DoorWidthSquash = 0.18f;

    [Tooltip("Degrees each ground tile is turned by as it pops, unwinding to square. Small " +
             "values only — the floor is a continuous surface and a big spin breaks it up.")]
    [Range(0f, 25f)][SerializeField] private float m_TileSpin = 0f;

    [Tooltip("Degrees each prop is turned by as it pops in, unwinding to its authored angle.")]
    [Range(0f, 25f)][SerializeField] private float m_ScenerySpin = 0f;

    [Tooltip("Seconds to wait between the pipe run finishing and the entry door rising — the " +
             "beat of quiet before the player arrives.")]
    [Min(0f)][SerializeField] private float m_HoldBeforeEntry = 0.05f;

    [Tooltip("Sink the entry doorway back into the floor once the player is out of it and it " +
             "has shut behind them. The doorway has done its job at that point, and a level " +
             "played around a door standing in it reads as clutter. Untick to leave it up.")]
    [SerializeField] private bool m_SinkEntryDoorAfterArrival = true;

    [Tooltip("Seconds between the entry doorway shutting behind the player and it sinking — " +
             "a beat, so the shut and the sink read as two moments rather than one.")]
    [Min(0f)][SerializeField] private float m_EntryDoorSinkDelay = 0.1f;

    // ─── Juice ────────────────────────────────────────────────────────────────────────────

    [Header("Juice")]
    [Tooltip("Fired when the last ground tile lands — the level dropping into place.")]
    [SerializeField] private FeelPreset m_GroundSettleFeel = new FeelPreset();

    [Tooltip("Fired when a doorway finishes rising, at the doorway.")]
    [SerializeField] private FeelPreset m_DoorLandFeel = new FeelPreset();

    [Tooltip("Fired when the pipe run reaches the exit door.")]
    [SerializeField] private FeelPreset m_PipesReadyFeel = new FeelPreset();

    [Tooltip("Ticked while a wave is running — the sound of the level being laid in. " +
             "Optional; left empty the build is silent.")]
    [SerializeField] private AudioClip m_StepClip;

    [Tooltip("Seconds between ticks while a wave runs.")]
    [Min(0.01f)][SerializeField] private float m_StepInterval = 0.05f;

    [Range(0f, 1f)][SerializeField] private float m_StepVolume = 0.5f;

    [Tooltip("Played once as each doorway rises. Optional.")]
    [SerializeField] private AudioClip m_DoorRiseClip;

    // ─── Safety ───────────────────────────────────────────────────────────────────────────

    [Header("Safety")]
    [Tooltip("Seconds to wait for the level's opening to ask for the build before running it " +
             "anyway. The intro belongs to the entry door (or to the player in a level " +
             "without one) — this only catches a level where neither exists, which would " +
             "otherwise sit there permanently empty. 0 switches the net off.")]
    [Min(0f)][SerializeField] private float m_AutoBuildAfter = 3f;

    [Tooltip("Log what each phase collected. Useful the first time a level is set up.")]
    [SerializeField] private bool m_LogPhases;

    // ─── Runtime ──────────────────────────────────────────────────────────────────────────

    private State m_State = State.Hidden;

    private ILevelBuildWave m_GroundWave;
    private ILevelBuildWave m_SceneryWave;
    private ILevelBuildWave m_PipeWave;
    private ILevelBuildWave m_ExitDoorWave;
    private ILevelBuildWave m_EntryDoorWave;

    private readonly List<ILevelBuildWave> m_GroundWaves = new List<ILevelBuildWave>();
    private readonly List<ILevelBuildWave> m_UIWaves = new List<ILevelBuildWave>();
    private readonly List<Coroutine> m_Running = new List<Coroutine>();

    private float m_NextStepTime;
    private bool m_Collected;
    private bool m_EntryDoorSunk;

    /// <summary>True while the level is assembling or taking itself apart.</summary>
    public bool IsPlaying => m_State == State.Building || m_State == State.TearingDown;

    /// <summary>True once the level is fully built and back to its authored state.</summary>
    public bool IsBuilt => m_State == State.Built;

    // ─── Lifecycle ────────────────────────────────────────────────────────────────────────

    // Only the singleton is claimed this early. Everything else waits for Start — see below.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LevelBuildDirector] Another director is already in this scene; " +
                             "this one will not run.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // The level is collected AND hidden here rather than in Awake, and that is not a detail:
    // while a scene is loading it reports itself as NOT LOADED for the whole of Awake, so every
    // scene-scoped search comes back empty and the director would quietly build nothing. Start
    // is the first moment the scene can be walked — and it is still before anything renders,
    // since Unity draws the frame only once every Start has run. The negative execution order
    // on this class is what puts it ahead of the Starts that own the level's opening.
    private IEnumerator Start()
    {
        EnsureCollected();

        if (m_AutoBuildAfter <= 0f) yield break;

        float elapsed = 0f;
        while (elapsed < m_AutoBuildAfter && m_State == State.Hidden)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        if (m_State != State.Hidden) yield break;

        Debug.LogWarning("[LevelBuildDirector] Nothing asked for the level build within " +
                         $"{m_AutoBuildAfter}s — running it so the level is not left empty. " +
                         "A level normally starts its build from its entry door.", this);
        yield return BuildRoutine();
    }

    // ─── Public API ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the level: ground, scenery, exit door, pipe run, entry door. Awaited by
    /// <see cref="LevelEntryDoor"/> before it opens itself, and by
    /// <see cref="PlayerController"/> in a level with no entry door.
    ///
    /// Safe to call from more than one place: a second call while a build is in flight simply
    /// waits for it, and a call on an already-built level returns straight away.
    /// </summary>
    public IEnumerator BuildRoutine()
    {
        if (m_State == State.Building)
        {
            while (m_State == State.Building) yield return null;
            yield break;
        }

        if (m_State != State.Hidden) yield break;

        EnsureCollected();

        m_State = State.Building;
        m_Running.Clear();

        yield return PlayPhase(m_GroundWave, m_GroundTiming, building: true, onDone: OnGroundSettled);
        yield return PlayPhase(m_SceneryWave, m_SceneryTiming, building: true, onDone: null);
        yield return PlayDoor(m_ExitDoorWave, m_ExitDoor != null ? m_ExitDoor.transform : null,
                              rising: true, onLanded: () => SetExitDoorStanding(true));
        yield return PlayPhase(m_PipeWave, m_PipeTiming, building: true, onDone: OnPipesReady);
        yield return PlayUIPhase();

        if (m_HoldBeforeEntry > 0f) yield return WaitUnscaled(m_HoldBeforeEntry);

        yield return PlayDoor(m_EntryDoorWave, m_EntryDoor != null ? m_EntryDoor.transform : null, rising: true);

        // Phases overlap, so the last one to be STARTED is not necessarily the last one still
        // running. Waiting on every handle is what guarantees the level is whole — and back
        // to its exact authored transforms — before the entry door opens on it.
        yield return WaitForRunning();

        m_State = State.Built;
    }

    /// <summary>
    /// Stands the entry doorway back up, for an arrival after the level's opening — a respawn
    /// after a death. The doorway sank away once the player was out of it, so it has to be
    /// there again before it can open and hand them back into the level.
    ///
    /// Drives the SAME wave the build raised it with, which is holding the doorway's authored
    /// transform: a fresh wave built now would read the sunk doorway and take zero height for
    /// its full size. Its colliders are deliberately left switched off — see
    /// <see cref="OnDoorSunk"/>; the doorway is scenery from its first sink onwards.
    ///
    /// Does nothing on a doorway that is already standing, so the level's own opening — where
    /// the build raised it a moment ago — pays nothing for calling it.
    /// </summary>
    public IEnumerator RaiseEntryDoorRoutine()
    {
        if (!m_EntryDoorSunk || m_EntryDoor == null || m_EntryDoorWave == null) yield break;

        m_EntryDoorSunk = false;

        AudioManager.Instance?.PlaySfx(m_DoorRiseClip);

        Vector3 position = m_EntryDoor.transform.position;
        yield return DriveWave(m_EntryDoorWave, building: true, () => PlayFeel(m_DoorLandFeel, position));
    }

    /// <summary>
    /// Sinks the entry doorway back into the floor, awaited by <see cref="LevelEntryDoor"/>
    /// once the player is out of it and it has shut behind them. The doorway is only there to
    /// deliver the player, so it leaves the same way it arrived and the level is played on a
    /// clean floor.
    ///
    /// Does nothing on a level that has ticked the doorway to stay up, and nothing twice —
    /// the teardown checks the same flag, so a doorway that has already gone does not sink a
    /// second time when the level ends. Runs again for every later arrival, since
    /// <see cref="RaiseEntryDoorRoutine"/> puts the doorway back first.
    /// </summary>
    public IEnumerator SinkEntryDoorRoutine()
    {
        if (!m_SinkEntryDoorAfterArrival || m_EntryDoorSunk || m_EntryDoor == null) yield break;

        m_EntryDoorSunk = true;

        if (m_EntryDoorSinkDelay > 0f) yield return WaitUnscaled(m_EntryDoorSinkDelay);

        yield return SinkDoorRoutine(m_EntryDoor.transform);
    }

    /// <summary>
    /// Takes the level apart again, after the player has gone through the exit door and it has
    /// shut behind them: the pipe empties from the door end back to the socket, then both
    /// doorways sink into the floor. The ground and the scenery are left standing — the screen
    /// fade is what takes those away.
    ///
    /// Awaited by <see cref="PlayerController"/>'s win sequence. A level that never built
    /// (no director ran, or the win came before the build finished) does nothing here.
    /// </summary>
    public IEnumerator TeardownRoutine()
    {
        if (m_State != State.Built) yield break;

        m_State = State.TearingDown;
        m_Running.Clear();

        // Started rather than awaited, and every canvas at once rather than one after
        // another: the HUD clears itself while the pipe drains. The canvases queue on the way
        // IN because the player has to read them; on the way out the level is over and a queue
        // is only a delay.
        foreach (ILevelBuildWave wave in m_UIWaves) StartWave(wave, building: false, onDone: null);

        yield return PlayPhase(m_PipeWave, m_PipeTiming, building: false, onDone: null);

        // Both doorways go together rather than one after the other: the level is over and the
        // player is watching a shut door, so a queue of two sinks reads as a delay.
        //
        // Built fresh rather than replaying the rise waves backwards, so the sink can run at
        // its own pace — and so a doorway the level legitimately moved sinks from where it
        // actually stands rather than from where it was authored.
        StartSink(m_ExitDoor != null ? m_ExitDoor.transform : null);

        // The entry doorway normally sank the moment the player was out of it, at the top of
        // the level — only a level that keeps its doorway up has one left to take away here.
        if (!m_EntryDoorSunk && m_EntryDoor != null)
        {
            m_EntryDoorSunk = true;
            StartSink(m_EntryDoor.transform);
        }

        yield return WaitForRunning();

        m_State = State.TornDown;
    }

    /// <summary>
    /// Drops the whole build and puts the level straight into its finished state. For anything
    /// that needs the level whole right now and cannot wait on the sequence — a level skip, an
    /// automated play test, a designer previewing from the inspector.
    /// </summary>
    [ContextMenu("Skip To Built")]
    public void SkipToBuilt()
    {
        StopAllCoroutines();
        m_Running.Clear();

        m_GroundWave?.Finish(true);
        m_SceneryWave?.Finish(true);
        m_PipeWave?.Finish(true);
        foreach (ILevelBuildWave wave in m_UIWaves) wave.Finish(true);
        m_ExitDoorWave?.Finish(true);
        SetExitDoorStanding(true);
        m_EntryDoorWave?.Finish(true);

        m_State = State.Built;
    }

    /// <summary>
    /// Hides the level again and plays the whole build from the top. For tuning: change a
    /// number in the inspector while the game is running and watch the result immediately,
    /// instead of leaving play mode and coming back for every tweak.
    /// </summary>
    [ContextMenu("Replay Build")]
    public void Replay()
    {
        if (!Application.isPlaying) return;

        StopAllCoroutines();
        m_Running.Clear();
        HideAll();
        StartCoroutine(BuildRoutine());
    }

    // ─── Phase driving ────────────────────────────────────────────────────────────────────

    // Starts a phase and returns once the NEXT one is due — which is before this one has
    // finished, by the phase's own overlap. The wave carries on in its own coroutine.
    private IEnumerator PlayPhase(ILevelBuildWave wave, PhaseTiming timing, bool building, System.Action onDone)
    {
        if (wave == null || wave.Count == 0) yield break;

        StartWave(wave, building, onDone);

        float hold = Mathf.Max(0f, wave.Duration - Mathf.Max(0f, timing.Overlap));
        yield return WaitUnscaled(hold);
    }

    // The HUD, a CANVAS at a time, each one waiting for the last to land. The input row and
    // the tutorial prompt sitting over it are two separate things to read, and popping them
    // together turns them into one busy screen — so they queue, and the player is shown one
    // and then the other. Only the final canvas overlaps, handing the build on to the entry
    // door the way every other phase hands on to the next.
    private IEnumerator PlayUIPhase()
    {
        for (int i = 0; i < m_UIWaves.Count; i++)
        {
            ILevelBuildWave wave = m_UIWaves[i];
            if (wave.Count == 0) continue;

            StartWave(wave, building: true, onDone: null);

            bool last = i == m_UIWaves.Count - 1;
            float hold = last
                ? Mathf.Max(0f, wave.Duration - Mathf.Max(0f, m_UITiming.Overlap))
                : wave.Duration;

            yield return WaitUnscaled(hold);
        }
    }

    // A doorway is one object rather than a group, so it gets its own little phase: no
    // overlap, a sound as it starts and a thump as it lands.
    private IEnumerator PlayDoor(
        ILevelBuildWave wave, Transform door, bool rising, System.Action onLanded = null)
    {
        if (wave == null || wave.Count == 0) yield break;

        if (rising) AudioManager.Instance?.PlaySfx(m_DoorRiseClip);

        Vector3 position = door != null ? door.position : transform.position;
        StartWave(wave, rising, () =>
        {
            PlayFeel(m_DoorLandFeel, position);
            onLanded?.Invoke();
        });

        yield return WaitUnscaled(wave.Duration);
    }

    // Awaited on its own, for the entry doorway leaving mid-level.
    private IEnumerator SinkDoorRoutine(Transform door)
    {
        TransformRiseWave wave = SinkWave(door);
        if (wave == null) yield break;

        yield return DriveWave(wave, building: false, () => OnDoorSunk(door));
    }

    // Fired and forgotten, for the doorways going down together as the level ends.
    private void StartSink(Transform door)
    {
        TransformRiseWave wave = SinkWave(door);
        if (wave == null) return;

        StartWave(wave, building: false, () => OnDoorSunk(door));
    }

    // The exit door's own effect is the one thing on a doorway that must not appear before
    // the doorway does: a level that opens without a key is open from its first frame, so the
    // glow would sit in mid-air over a door still scaled to nothing. The door owns the rule;
    // this only tells it where the build has got to.
    private void SetExitDoorStanding(bool standing)
    {
        if (m_ExitDoor != null) m_ExitDoor.SetStanding(standing);
    }

    private TransformRiseWave SinkWave(Transform door) =>
        door != null ? new TransformRiseWave(door, m_DoorSinkDuration, m_DoorWidthSquash) : null;

    // A doorway that has gone back into the floor stops being there for physics too. The
    // entry doorway is the one that matters: it carries the "Door" tag, and an invisible
    // trigger left standing on the player's start cell is a level the player can win by
    // walking home. Nothing switches these back on — a sunk doorway is gone for the level.
    private static void OnDoorSunk(Transform door)
    {
        if (door == null) return;

        foreach (Collider2D collider in door.GetComponentsInChildren<Collider2D>(true))
            collider.enabled = false;
    }

    private void StartWave(ILevelBuildWave wave, bool building, System.Action onDone)
    {
        if (wave == null || wave.Count == 0) return;
        m_Running.Add(StartCoroutine(DriveWave(wave, building, onDone)));
    }

    // The only place a wave is stepped. One coroutine per wave, whatever it holds — four
    // hundred tiles cost one coroutine, not four hundred.
    private IEnumerator DriveWave(ILevelBuildWave wave, bool building, System.Action onDone)
    {
        float duration = wave.Duration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            wave.Apply(elapsed, building);
            Tick();

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        wave.Finish(building);
        onDone?.Invoke();
    }

    private IEnumerator WaitForRunning()
    {
        for (int i = 0; i < m_Running.Count; i++)
        {
            if (m_Running[i] != null) yield return m_Running[i];
        }

        m_Running.Clear();
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        if (seconds <= 0f) yield break;
        yield return new WaitForSecondsRealtime(seconds);
    }

    // Rate-limited rather than one sound per item: a wave lands dozens of items a second and
    // one clip each is a machine gun, not a rhythm.
    private void Tick()
    {
        if (m_StepClip == null || Time.unscaledTime < m_NextStepTime) return;

        m_NextStepTime = Time.unscaledTime + m_StepInterval;
        AudioManager.Instance?.PlaySfx(m_StepClip, m_StepVolume);
    }

    private void OnGroundSettled() => PlayFeel(m_GroundSettleFeel, GroundCentre());

    private void OnPipesReady() =>
        PlayFeel(m_PipesReadyFeel, m_ExitDoor != null ? m_ExitDoor.transform.position : GroundCentre());

    // Guarded rather than called blind: a preset left switched off would still reach through
    // FeelPreset.Play and spin the FeelService up for a level that asked for no feel at all.
    private static void PlayFeel(FeelPreset preset, Vector3 position)
    {
        if (preset != null && preset.IsActive) preset.Play(position);
    }

    private Vector3 GroundCentre()
    {
        if (m_GroundTilemaps != null)
        {
            foreach (Tilemap map in m_GroundTilemaps)
            {
                if (map != null) return map.transform.TransformPoint(map.localBounds.center);
            }
        }

        return transform.position;
    }

    // ─── Collection ───────────────────────────────────────────────────────────────────────

    // Everything the build will ever touch is decided here, once, while the level loads. The
    // waves that come out of it hold plain arrays, so not a single search, sort or allocation
    // happens again while the level is playing.
    // Idempotent, and called from both Start and BuildRoutine: whichever gets there first does
    // the work, so a level driven from an unusual place still hides itself before it builds.
    private void EnsureCollected()
    {
        if (m_Collected) return;
        m_Collected = true;

        Collect();
        HideAll();
    }

    private void Collect()
    {
        if (m_ExitDoor == null) m_ExitDoor = SceneObjects.FindInActiveScene<LevelExitDoor>();
        if (m_EntryDoor == null) m_EntryDoor = SceneObjects.FindInActiveScene<LevelEntryDoor>();
        if (m_Pipes == null) m_Pipes = SceneObjects.FindInActiveScene<PipeConnection>();

        if (m_GroundTilemaps == null || m_GroundTilemaps.Length == 0)
            m_GroundTilemaps = SceneObjects.FindAllInActiveScene<Tilemap>().ToArray();

        BuildGroundWaves();
        BuildSceneryWave();
        BuildPipeWave();
        BuildUIWaves();

        m_ExitDoorWave = m_ExitDoor != null
            ? new TransformRiseWave(m_ExitDoor.transform, m_DoorRiseDuration, m_DoorWidthSquash)
            : null;
        m_EntryDoorWave = m_EntryDoor != null
            ? new TransformRiseWave(m_EntryDoor.transform, m_DoorRiseDuration, m_DoorWidthSquash)
            : null;

        if (!m_LogPhases) return;

        int hud = 0;
        foreach (ILevelBuildWave wave in m_UIWaves) hud += wave.Count;

        Debug.Log($"[LevelBuildDirector] ground {(m_GroundWave?.Count ?? 0)} tiles across " +
                  $"{m_GroundWaves.Count} tilemap(s), scenery {(m_SceneryWave?.Count ?? 0)}, " +
                  $"pipe {(m_PipeWave?.Count ?? 0)}, " +
                  $"HUD {hud} across {m_UIWaves.Count} canvas(es), " +
                  $"exit door {(m_ExitDoorWave != null ? "yes" : "no")}, " +
                  $"entry door {(m_EntryDoorWave != null ? "yes" : "no")}.", this);
    }

    // Every tilemap sweeps on ONE shared wave: the cells are gathered from all of them first
    // so the sweep is normalised across the whole level, and only then split back per tilemap.
    // Two tilemaps otherwise each run their own left-to-right sweep and the floor lands twice.
    private void BuildGroundWaves()
    {
        m_GroundWaves.Clear();

        var maps = new List<Tilemap>();
        var cells = new List<List<Vector3Int>>();
        var keys = new List<float>();
        Vector2 sweep = SweepDirection();

        foreach (Tilemap map in m_GroundTilemaps)
        {
            if (map == null || !map.gameObject.activeInHierarchy) continue;

            // Only the hand-listed exclusions apply here: the component check below trips on
            // every tilemap by design, because scenery must never collect the floor.
            if (IsManuallyExcluded(map.transform)) continue;

            var mapCells = new List<Vector3Int>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(cell)) continue;

                mapCells.Add(cell);
                keys.Add(Vector2.Dot(map.GetCellCenterWorld(cell), sweep));
            }

            if (mapCells.Count == 0) continue;

            maps.Add(map);
            cells.Add(mapCells);
        }

        if (maps.Count == 0) return;

        float[] delays = Normalise(keys);

        int next = 0;
        for (int i = 0; i < maps.Count; i++)
        {
            var mapDelays = new float[cells[i].Count];
            System.Array.Copy(delays, next, mapDelays, 0, mapDelays.Length);
            next += mapDelays.Length;

            m_GroundWaves.Add(new TilemapPopWave(
                maps[i], cells[i], mapDelays,
                m_GroundTiming.Spread, m_GroundTiming.ItemDuration, m_TileSpin));
        }

        // One handle for the phase, however many tilemaps are under it.
        m_GroundWave = m_GroundWaves.Count == 1
            ? m_GroundWaves[0]
            : new CompositeWave(m_GroundWaves);
    }

    private void BuildSceneryWave()
    {
        var items = new List<Transform>();

        if (m_SceneryRoots != null && m_SceneryRoots.Length > 0)
        {
            foreach (Transform root in m_SceneryRoots) CollectBranch(root, items);
        }
        else
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    CollectBranch(root.transform, items);
            }
        }

        if (items.Count == 0) return;

        Vector2 sweep = SweepDirection();
        var keys = new List<float>(items.Count);
        foreach (Transform item in items) keys.Add(Vector2.Dot(ItemCentre(item), sweep));

        m_SceneryWave = new TransformPopWave(
            items, Normalise(keys),
            m_SceneryTiming.Spread, m_SceneryTiming.ItemDuration, m_ScenerySpin,
            manageColliders: true);
    }

    // The pipe is the one group that is NOT swept: its pieces run in a line from the battery
    // socket to the door, and the order they were authored in is the order the charge travels
    // later. Laying them in that same order is what makes the run read as plumbing being
    // connected rather than as scenery appearing near a door.
    private void BuildPipeWave()
    {
        if (m_Pipes == null) return;

        var pieces = new List<Transform>();
        CollectBranch(m_Pipes.transform, pieces, ignoreExclusions: true);

        if (pieces.Count == 0) return;

        var delays = new float[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
            delays[i] = pieces.Count == 1 ? 0f : i / (float)(pieces.Count - 1);

        m_PipeWave = new TransformPopWave(
            pieces, delays,
            m_PipeTiming.Spread, m_PipeTiming.ItemDuration, spin: 0f,
            manageColliders: false);
    }

    // The HUD, popped in the order it was authored in rather than swept across the screen.
    // The level's own waves sweep because a level is a PLACE; an interface is a LIST, and
    // sweeping it would hand the order to whichever corner of the screen an element happened
    // to sit in — a row of input slots coming in from the right on one level and the left on
    // the next. Hierarchy order is the order a designer can actually see and change.
    private void BuildUIWaves()
    {
        m_UIWaves.Clear();

        if (m_UIRoots != null && m_UIRoots.Length > 0)
        {
            foreach (RectTransform root in m_UIRoots) AddUIWave(root);
            return;
        }

        foreach (Canvas canvas in SceneObjects.FindAllInActiveScene<Canvas>())
        {
            if (canvas == null || !canvas.isRootCanvas) continue;
            if (!canvas.gameObject.activeInHierarchy) continue;

            AddUIWave(canvas.transform);
        }
    }

    // One wave per canvas rather than one for the whole HUD, because the canvases play one
    // after another — see PlayUIPhase.
    private void AddUIWave(Transform root)
    {
        var items = new List<Transform>();
        CollectUIBranch(root, items);

        if (items.Count == 0) return;

        var delays = new float[items.Count];
        for (int i = 0; i < items.Count; i++)
            delays[i] = items.Count == 1 ? 0f : i / (float)(items.Count - 1);

        // A canvas holding a single element has nothing to spread across, and charging it the
        // full spread would leave the next canvas waiting on a wave that landed long ago.
        float spread = items.Count > 1 ? m_UITiming.Spread : 0f;

        m_UIWaves.Add(new TransformPopWave(
            items, delays,
            spread, m_UITiming.ItemDuration, spin: 0f,
            manageColliders: false));
    }

    // Walks a canvas down to the things that actually draw, exactly as the scenery is
    // collected: a button is one item, and the panel of six buttons is six.
    //
    // Two kinds are stepped over on the way. A SCREEN — the pause menu, a confirmation popup —
    // opens and closes itself, and must never be found sitting at zero scale when it does. A
    // VEIL — anything stretched edge to edge over its canvas, which is the screen fade, the
    // brightness sheet and every other full-screen sheet — is not a HUD element at all:
    // popping one in scales the whole picture. The veil is stepped THROUGH rather than
    // dropped, so a HUD laid out inside a full-screen container still comes in.
    private void CollectUIBranch(Transform branch, List<Transform> into)
    {
        if (branch == null || !branch.gameObject.activeSelf) return;
        if (IsManuallyExcluded(branch)) return;
        if (branch.GetComponent<UIScreen>() != null) return;

        if (IsDrawnUI(branch) && !CoversCanvas(branch as RectTransform))
        {
            into.Add(branch);
            return;
        }

        for (int i = 0; i < branch.childCount; i++)
            CollectUIBranch(branch.GetChild(i), into);
    }

    // Walks down until it finds something that draws, and takes THAT — so a prefab of forty
    // bushes contributes forty items and a single spike contributes one, without either being
    // listed anywhere. Objects that are switched off in the scene are left switched off: they
    // are off for a reason the build has no business overruling.
    private void CollectBranch(Transform branch, List<Transform> into, bool ignoreExclusions = false)
    {
        if (branch == null || !branch.gameObject.activeSelf) return;
        if (!ignoreExclusions && IsExcluded(branch)) return;

        if (IsDrawn(branch))
        {
            into.Add(branch);
            return;
        }

        for (int i = 0; i < branch.childCount; i++)
            CollectBranch(branch.GetChild(i), into, ignoreExclusions);
    }

    // Everything the build must not own: what has its own phase (ground, doorways, pipe), what
    // has its own opening (the player), what is not part of the level at all (cameras, UI), and
    // whatever the level listed by hand.
    private bool IsExcluded(Transform candidate)
    {
        if (IsManuallyExcluded(candidate)) return true;

        if (candidate.GetComponent<Grid>() != null) return true;
        if (candidate.GetComponent<Tilemap>() != null) return true;
        if (candidate.GetComponent<PipeConnection>() != null) return true;
        if (candidate.GetComponent<LevelEntryDoor>() != null) return true;
        if (candidate.GetComponent<LevelExitDoor>() != null) return true;
        if (candidate.GetComponent<PlayerController>() != null) return true;
        if (candidate.GetComponent<Camera>() != null) return true;
        if (candidate.GetComponent<Canvas>() != null) return true;

        return false;
    }

    // The director itself, and whatever the level listed by hand.
    private bool IsManuallyExcluded(Transform candidate)
    {
        if (candidate == transform) return true;
        if (m_Excluded == null) return false;

        foreach (Transform excluded in m_Excluded)
        {
            if (excluded != null && candidate.IsChildOf(excluded)) return true;
        }

        return false;
    }

    // A renderer that actually puts something on screen this frame — and not one of the two
    // kinds this system drives another way (tilemaps have their own wave) or must not drive at
    // all (a particle system scaled to nothing plays its burst into a point). The backdrop
    // sorting layers are skipped so the level builds AGAINST the background rather than
    // building it too.
    private bool IsDrawn(Transform candidate)
    {
        if (!candidate.TryGetComponent(out Renderer renderer)) return false;
        if (!renderer.enabled) return false;
        if (renderer is TilemapRenderer || renderer is ParticleSystemRenderer) return false;

        if (renderer is SpriteRenderer sprite && sprite.sprite == null) return false;

        if (m_BackdropSortingLayers != null)
        {
            foreach (string layer in m_BackdropSortingLayers)
            {
                if (!string.IsNullOrEmpty(layer) && renderer.sortingLayerName == layer)
                    return false;
            }
        }

        return true;
    }

    // The UI half of IsDrawn. An Image, a RawImage, a TMP label — every one of them a
    // Graphic, so one check covers the lot. A fully transparent one is skipped for the same
    // reason a sprite-less SpriteRenderer is: it is on the canvas to be hit by raycasts, not
    // to be seen, and giving it a slot in the wave only puts a silent gap in the sequence.
    private static bool IsDrawnUI(Transform candidate)
    {
        if (!candidate.TryGetComponent(out Graphic graphic)) return false;

        return graphic.enabled && graphic.color.a > 0f;
    }

    // True when the element is stretched edge to edge over its canvas with no inset — the
    // screen fade, the brightness sheet, any full-screen veil.
    //
    // Read off the ANCHORS rather than measured against the canvas on screen, deliberately:
    // this runs in Start, and a screen-space canvas has not necessarily been sized to the
    // display yet on the frame the level loads. Anchors are authored data and are already
    // correct there.
    private static bool CoversCanvas(RectTransform rect)
    {
        while (rect != null)
        {
            if (rect.GetComponent<Canvas>() != null) return true;
            if (!FillsParent(rect)) return false;

            rect = rect.parent as RectTransform;
        }

        return false;
    }

    private static bool FillsParent(RectTransform rect) =>
        rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
        rect.sizeDelta.sqrMagnitude <= 1f && rect.anchoredPosition.sqrMagnitude <= 1f;

    private static Vector3 ItemCentre(Transform item) =>
        item.TryGetComponent(out Renderer renderer) ? renderer.bounds.center : item.position;

    private Vector2 SweepDirection()
    {
        float radians = m_SweepAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    // Turns raw sweep distances into each item's 0..1 place in its phase. A level one tile
    // wide, or a phase holding one object, collapses to "everything at once" rather than
    // dividing by nothing.
    private static float[] Normalise(List<float> keys)
    {
        var delays = new float[keys.Count];
        if (keys.Count == 0) return delays;

        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (float key in keys)
        {
            if (key < min) min = key;
            if (key > max) max = key;
        }

        float range = max - min;
        if (range <= Mathf.Epsilon) return delays;

        for (int i = 0; i < keys.Count; i++)
            delays[i] = (keys[i] - min) / range;

        return delays;
    }

    private void HideAll()
    {
        m_GroundWave?.Finish(false);
        m_SceneryWave?.Finish(false);
        m_PipeWave?.Finish(false);
        foreach (ILevelBuildWave wave in m_UIWaves) wave.Finish(false);
        m_ExitDoorWave?.Finish(false);
        SetExitDoorStanding(false);
        m_EntryDoorWave?.Finish(false);

        m_State = State.Hidden;
    }

    /// <summary>
    /// Several waves the director drives as one phase — the levels whose floor is spread over
    /// more than one tilemap. Their delays were normalised together, so playing them off a
    /// shared clock is all it takes for them to sweep as a single wave.
    /// </summary>
    private sealed class CompositeWave : ILevelBuildWave
    {
        private readonly ILevelBuildWave[] m_Waves;

        public CompositeWave(List<ILevelBuildWave> waves) => m_Waves = waves.ToArray();

        public int Count
        {
            get
            {
                int total = 0;
                foreach (ILevelBuildWave wave in m_Waves) total += wave.Count;
                return total;
            }
        }

        public float Duration
        {
            get
            {
                float longest = 0f;
                foreach (ILevelBuildWave wave in m_Waves) longest = Mathf.Max(longest, wave.Duration);
                return longest;
            }
        }

        public void Apply(float elapsed, bool building)
        {
            foreach (ILevelBuildWave wave in m_Waves) wave.Apply(elapsed, building);
        }

        public void Finish(bool built)
        {
            foreach (ILevelBuildWave wave in m_Waves) wave.Finish(built);
        }
    }
}
