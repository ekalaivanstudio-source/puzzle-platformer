using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Patrol Points")]
    public Transform pointA;
    public Transform pointB;

    [Header("Movement Settings")]
    public float moveSpeed = 2f;

    [Header("Death Effect")]
    [SerializeField] private GameObject m_DeathParticle;

    private Transform currentTarget;

    private void Start()
    {
        // Start moving towards Point B
        currentTarget = pointB;
    }

    private void Update()
    {
        if (pointA == null || pointB == null)
            return;

        // Move towards current target
        transform.position = Vector2.MoveTowards(
            transform.position,
            currentTarget.position,
            moveSpeed * Time.deltaTime
        );

        // Check if reached target
        if (Vector2.Distance(transform.position, currentTarget.position) < 0.01f)
        {
            // Switch target
            if (currentTarget == pointB)
            {
                currentTarget = pointA;
            }
            else
            {
                currentTarget = pointB;
            }
        }
    }

    /// <summary>Called by PlayerController when a Dash or GroundPound hits this enemy.</summary>
    public void Die()
    {
        if (m_DeathParticle != null)
            Instantiate(m_DeathParticle, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawSphere(pointA.position, 0.2f);
            Gizmos.DrawSphere(pointB.position, 0.2f);

            Gizmos.DrawLine(pointA.position, pointB.position);
        }
    }
}