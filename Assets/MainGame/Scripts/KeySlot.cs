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

    private bool m_Filled;

    private void Awake()
    {
        if (m_EmptyVisual != null)
            m_EmptyVisual.SetActive(true);

        if (m_FilledVisual != null)
            m_FilledVisual.SetActive(false);

        if (m_DoorCollider != null)
            m_DoorCollider.enabled = false;
    }

    private void OnEnable() => GameManager.OnTurnReset += ResetSlot;
    private void OnDisable() => GameManager.OnTurnReset -= ResetSlot;

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
    }
}