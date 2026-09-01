using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class KeySlot : MonoBehaviour
{
    [Header("Key")]
    [SerializeField] private PlaceableKey m_LinkedKey;

    [Header("Player")]
    [SerializeField] private string m_PlayerTag = "Player";

    [Header("Visuals")]
    [SerializeField] private GameObject m_EmptyVisual;
    [SerializeField] private GameObject m_FilledVisual;

    [Header("Door")]
    [Tooltip("The exit door this slot opens. It owns the doorway's animation, its trigger " +
             "and its open effect — the slot only tells it when to open and when to go back " +
             "to shut — so the frames, the renderer, the collider and the effect are all " +
             "configured over there, on the door.")]
    [SerializeField] private LevelExitDoor m_ExitDoor;

    [Header("Pipe Connection")]
    [Tooltip("The run of pipe between this socket and the door. Its glow travels the run " +
             "when the battery goes in, and the door is only opened once the charge reaches " +
             "the far end. Optional — a level with no pipe run opens its door the instant " +
             "the battery is placed, exactly as it always did.")]
    [SerializeField] private PipeConnection m_PipeConnection;

    [SerializeField] private GameObject shineEffect;

    [Header("Place Effect")]
    [Tooltip("Burst spawned at the slot the moment the key is placed. Optional. A particle " +
             "prefab — it is destroyed automatically once its systems have finished, so it " +
             "needs no self-cleanup.")]
    [SerializeField] private GameObject m_PlaceEffect;
    [Tooltip("Offset from the slot's position where the place effect spawns.")]
    [SerializeField] private Vector3 m_PlaceEffectOffset;
    [Tooltip("Uniform scale applied to the spawned effect. The FX pack's flashes are authored " +
             "around a dozen world units wide, so a burst meant to sit on a one-unit grid cell " +
             "needs shrinking here.")]
    [SerializeField] private float m_PlaceEffectScale = 1f;

    private bool m_Filled;

    // The slot whose battery is still being answered — the charge travelling its pipe, or the
    // door at the far end still swinging open. Static and not a serialized reference because
    // PlayerController is what waits on it and has no business knowing which of the level's
    // objects it is, the same way it already learns from PlaceableKey whether a battery is
    // being carried.
    private static KeySlot s_Answering;

    /// <summary>
    /// True from the moment a battery drops into a socket until the door it powers has
    /// finished opening. <see cref="PlayerController"/> holds the queued run on this, so the
    /// player stands and watches the charge reach the door instead of walking their remaining
    /// moves against a doorway that is still shut.
    /// </summary>
    public static bool IsAnsweringBattery => s_Answering != null;

    private Coroutine m_Answer;

    private void Awake()
    {
        // A level that has just loaded has nothing being placed in it, whatever the slot in
        // the level before it was in the middle of when the scene went away.
        s_Answering = null;

        if (m_EmptyVisual != null)
            m_EmptyVisual.SetActive(true);

        if (m_FilledVisual != null)
            m_FilledVisual.SetActive(false);

        if (shineEffect != null)
            shineEffect.SetActive(false);

        m_ExitDoor?.SetClosed();
    }

    // Reset on OnKeyReset (fired only when a full input run finishes) — NOT OnTurnReset.
    // OnTurnReset also fires when the player accesses a rotator/mover/checkpoint
    // (ResetAtCheckpoint), and the placed key must survive those. Death reloads the
    // scene, which re-initialises the slot via Awake. This keeps the slot in sync with
    // PlaceableKey, which already resets on OnKeyReset.
    private void OnEnable() => GameManager.OnKeyReset += ResetSlot;

    private void OnDisable()
    {
        GameManager.OnKeyReset -= ResetSlot;

        // Nothing is going to finish answering once this is gone, and a run left waiting on
        // it would never move again.
        if (s_Answering == this)
            s_Answering = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (m_Filled)
            return;

        if (!other.CompareTag(m_PlayerTag))
            return;

        if (!PlaceableKey.IsCarried)
            return;

        PlaceKey();
    }

    private void PlaceKey()
    {
        m_Filled = true;

        if (m_EmptyVisual != null)
            m_EmptyVisual.SetActive(false);

        if (m_FilledVisual != null)
            m_FilledVisual.SetActive(true);

        if (shineEffect != null)
            shineEffect.SetActive(true);

        // Burst at the socket, punctuating the moment the key drops in.
        ParticleEffectSpawner.Spawn(
            m_PlaceEffect, transform.position + m_PlaceEffectOffset, m_PlaceEffectScale);

        m_LinkedKey?.Place();
        AudioManager.Instance?.PlayKeyPlaced();

        // Recorded now rather than when the door finishes opening: this is the flag
        // PlayerController reads to know the battery is in, and it has nothing to do with
        // how long the doorway takes to react.
        GameManager.Instance?.KeyCollected();

        // Held from here until the door has finished opening, whether that is a whole pipe
        // away or the very next frame — the run stops for as long as the level takes to
        // answer the battery.
        s_Answering = this;

        // The charge has to travel the pipe before the doorway reacts, so the player can see
        // where the socket they just filled was wired to. With no run to travel the door
        // opens on the same frame, as it did before there were pipes.
        if (m_PipeConnection != null)
            m_PipeConnection.Power(OpenDoor);
        else
            OpenDoor();
    }

    // The door opens itself, doorway trigger and all — but the queued run is held until it has
    // finished doing so, so the doorway the player walks their remaining moves towards is
    // already standing open.
    private void OpenDoor()
    {
        m_ExitDoor?.Open();
        AudioManager.Instance?.PlayDoorOpen();

        StopAnswer();

        if (isActiveAndEnabled)
            m_Answer = StartCoroutine(AnswerRoutine());
        else
            s_Answering = null;
    }

    private IEnumerator AnswerRoutine()
    {
        // The door animates itself, so this only has to outlast it. Waiting on the door's own
        // coroutine instead would mean owning it from here, and a slot reset mid-swing could
        // then no longer shut the door out from under it.
        if (m_ExitDoor != null && m_ExitDoor.OpenDuration > 0f)
            yield return new WaitForSeconds(m_ExitDoor.OpenDuration);

        m_Answer = null;

        if (s_Answering == this)
            s_Answering = null;
    }

    private void StopAnswer()
    {
        if (m_Answer == null)
            return;

        StopCoroutine(m_Answer);
        m_Answer = null;
    }

    private void ResetSlot()
    {
        m_Filled = false;

        if (m_EmptyVisual != null)
            m_EmptyVisual.SetActive(true);

        if (m_FilledVisual != null)
            m_FilledVisual.SetActive(false);

        if (shineEffect != null)
            shineEffect.SetActive(false);

        // Before the door: this also drops a charge still travelling, which would otherwise
        // reach the end of the run and re-open the door that was just shut.
        m_PipeConnection?.Unpower();

        StopAnswer();

        if (s_Answering == this)
            s_Answering = null;

        m_ExitDoor?.SetClosed();
    }

}