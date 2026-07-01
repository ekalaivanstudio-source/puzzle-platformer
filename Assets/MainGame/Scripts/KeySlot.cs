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
    [SerializeField] private BoxCollider2D m_DoorCollider;
    [Tooltip("Door sprite renderer whose sprite swaps when the key is placed.")]
    [SerializeField] private SpriteRenderer m_DoorRenderer;

    [Tooltip("Door open animation frames. Frame 0 is the closed state; the last frame is the fully-open state.")]
    [SerializeField] private Sprite[] doorOpenSS;
    [Tooltip("Seconds each frame of the door open animation is shown.")]
    [SerializeField] private float m_DoorFrameTime = 0.06f;

    [SerializeField] private GameObject shineEffect;
    private bool m_Filled;
    private Coroutine m_DoorAnimation;

    private void Awake()
    {
        if (m_EmptyVisual != null)
            m_EmptyVisual.SetActive(true);

        if (m_FilledVisual != null)
            m_FilledVisual.SetActive(false);

        if (shineEffect != null)
            shineEffect.SetActive(false);

        if (m_DoorCollider != null)
            m_DoorCollider.enabled = false;

        SetDoorClosed();
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

        m_LinkedKey?.Place();
        AudioManager.Instance?.PlayKeyPlaced();

        if (m_DoorCollider != null)
        {
            m_DoorCollider.enabled = true;
            AudioManager.Instance?.PlayDoorOpen();
        }

        PlayDoorOpenAnimation();

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

        if (m_DoorCollider != null)
            m_DoorCollider.enabled = false;

        SetDoorClosed();
    }

    // Closed state uses the first frame of the door open animation.
    private void SetDoorClosed()
    {
        if (m_DoorAnimation != null)
        {
            StopCoroutine(m_DoorAnimation);
            m_DoorAnimation = null;
        }

        if (m_DoorRenderer != null && doorOpenSS != null && doorOpenSS.Length > 0)
            m_DoorRenderer.sprite = doorOpenSS[0];
    }

    // Plays doorOpenSS from start to finish and holds on the last frame.
    private void PlayDoorOpenAnimation()
    {
        if (m_DoorRenderer == null || doorOpenSS == null || doorOpenSS.Length == 0)
            return;

        if (m_DoorAnimation != null)
            StopCoroutine(m_DoorAnimation);

        m_DoorAnimation = StartCoroutine(DoorOpenRoutine());
    }

    private IEnumerator DoorOpenRoutine()
    {
        for (int i = 0; i < doorOpenSS.Length; i++)
        {
            m_DoorRenderer.sprite = doorOpenSS[i];

            if (i < doorOpenSS.Length - 1 && m_DoorFrameTime > 0f)
                yield return new WaitForSeconds(m_DoorFrameTime);
        }

        // Hold on the last (fully-open) frame.
        m_DoorAnimation = null;
    }
}