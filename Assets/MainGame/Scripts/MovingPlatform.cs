using UnityEngine;

/// <summary>
/// Generic ping-pong moving platform that supports both vertical and horizontal axes.
/// Consolidates the logic from <see cref="FloorMovement"/> (vertical) and
/// <see cref="FloorMovementLvl4"/> (horizontal) into a single reusable component.
/// Use this for any new moving platforms instead of the axis-specific variants.
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    /// <summary>Axis along which the platform travels.</summary>
    public enum MoveAxis { Vertical, Horizontal }

    [Tooltip("Choose whether the platform moves vertically or horizontally.")]
    [SerializeField] private MoveAxis m_Axis = MoveAxis.Vertical;

    [Tooltip("Movement speed in units per second.")]
    [SerializeField] private float m_Speed = 2f;

    [Tooltip("Distance from the start position to either end of the path.")]
    [SerializeField] private float m_MoveDistance = 3f;

    private Vector3 m_StartPos;
    private int m_Direction = 1; // 1 = positive axis direction, -1 = negative

    // Threshold for considering the platform as having reached its endpoint
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
        // Select axis direction vector based on the chosen axis
        Vector3 axisDir = m_Axis == MoveAxis.Vertical ? Vector3.up : Vector3.right;
        Vector3 target  = m_StartPos + axisDir * (m_Direction * m_MoveDistance);

        transform.position = Vector3.MoveTowards(transform.position, target, m_Speed * Time.deltaTime);

        // Reverse direction when the platform arrives at the endpoint
        if (Vector3.Distance(transform.position, target) < k_ArrivalThreshold)
        {
            m_Direction *= -1;
        }
    }
}
