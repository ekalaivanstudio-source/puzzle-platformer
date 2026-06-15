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
    [SerializeField] private Sprite m_DoorClosedSprite;
    [SerializeField] private Sprite m_DoorOpenSprite;

    private bool m_Filled;

    private void Awake()
    {
        if (m_EmptyVisual != null)
            m_EmptyVisual.SetActive(true);

        if (m_FilledVisual != null)
            m_FilledVisual.SetActive(false);

        if (m_DoorCollider != null)
            m_DoorCollider.enabled = false;

        if (m_DoorRenderer != null && m_DoorClosedSprite != null)
            m_DoorRenderer.sprite = m_DoorClosedSprite;
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

        m_LinkedKey?.Place();
        AudioManager.Instance?.PlayKeyPlaced();

        if (m_DoorCollider != null)
        {
            m_DoorCollider.enabled = true;
            AudioManager.Instance?.PlayDoorOpen();
        }

        if (m_DoorRenderer != null && m_DoorOpenSprite != null)
            m_DoorRenderer.sprite = m_DoorOpenSprite;

        GameManager.Instance?.KeyCollected();
    }

    private void ResetSlot()
    {
        m_Filled = false;

        if (m_EmptyVisual != null)
            m_EmptyVisual.SetActive(true);

        if (m_FilledVisual != null)
            m_FilledVisual.SetActive(false);

        if (m_DoorCollider != null)
            m_DoorCollider.enabled = false;

        if (m_DoorRenderer != null && m_DoorClosedSprite != null)
            m_DoorRenderer.sprite = m_DoorClosedSprite;
    }
}