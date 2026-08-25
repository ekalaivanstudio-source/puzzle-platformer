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

    private void Awake()
    {
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
    private void OnDisable() => GameManager.OnKeyReset -= ResetSlot;

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

        // The door opens itself, doorway trigger and all.
        m_ExitDoor?.Open();
        AudioManager.Instance?.PlayDoorOpen();

        GameManager.Instance?.KeyCollected();
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

        m_ExitDoor?.SetClosed();
    }

}