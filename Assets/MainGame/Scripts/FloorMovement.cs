using UnityEngine;

/// <summary>
/// Moves a platform vertically back and forth between two endpoints.
/// The platform ping-pongs between <c>startPos.y - moveDistance</c>
/// and <c>startPos.y + moveDistance</c> at a constant speed.
/// For a generic horizontal/vertical option, use <see cref="MovingPlatform"/> instead.
/// </summary>
public class FloorMovement : MonoBehaviour
{
    [Tooltip("Movement speed in units per second.")]
    [SerializeField] private float m_Speed = 2f;

    [Tooltip("Distance from the start position to either end of the vertical path.")]
    [SerializeField] private float m_MoveDistance = 3f;

    private Vector3 m_StartPos;
    private int m_Direction = 1; // 1 = moving up, -1 = moving down

    // Threshold for considering the platform as having reached its target
    private const float k_ArrivalThreshold = 0.01f;

    private void OnValidate()
    {
        if (m_Speed <= 0f)        m_Speed = 2f;
        if (m_MoveDistance <= 0f) m_MoveDistance = 3f;
    }

    private void Start()
    {
        m_StartPos = transform.position;
    }

    private void Update()
    {
        Vector3 target = m_StartPos + Vector3.up * (m_Direction * m_MoveDistance);

        transform.position = Vector3.MoveTowards(transform.position, target, m_Speed * Time.deltaTime);

        // Reverse direction when the platform reaches the endpoint
        if (Vector3.Distance(transform.position, target) < k_ArrivalThreshold)
        {
            m_Direction *= -1;
        }
    }
}
