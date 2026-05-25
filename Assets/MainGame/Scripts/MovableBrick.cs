using System.Collections;
using UnityEngine;

public class MovableBrick : MonoBehaviour
{
    [Header("Brick Movement")]
    [SerializeField] private Transform[] brickWaypoints;
    [SerializeField] private float brickMoveSpeed = 3f;

    [Header("Player Movement")]
    [SerializeField] private Transform[] playerWaypoints;
    [SerializeField] private float playerMoveSpeed = 5f;

    [Header("On Complete")]
    [Tooltip("Objects to disable once the brick finishes moving (e.g. InvisibleLockPoint).")]
    [SerializeField] private GameObject[] objectsToDisableWhenDone;

    private bool triggered = false;
    private Vector3 m_StartPosition;
    private Coroutine m_MoveCoroutine;
    private int m_OriginalLayer;

    private void Awake()
    {
        m_StartPosition = transform.position;
        m_OriginalLayer = gameObject.layer;
    }

    private void OnEnable()
    {
        GameManager.OnTurnReset += ResetBrick;
    }

    private void OnDisable()
    {
        GameManager.OnTurnReset -= ResetBrick;
    }

    private void ResetBrick()
    {
        if (m_MoveCoroutine != null)
        {
            StopCoroutine(m_MoveCoroutine);
            m_MoveCoroutine = null;
        }

        transform.position = m_StartPosition;
        triggered = false;

        // Re-enable anything that was disabled when the brick finished (e.g. InvisibleLockPoint).
        if (objectsToDisableWhenDone != null)
            foreach (GameObject obj in objectsToDisableWhenDone)
                if (obj != null) obj.SetActive(true);

        gameObject.layer = m_OriginalLayer;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!triggered && collision.gameObject.CompareTag("Player"))
        {
            triggered = true;

            // Move the brick along its own waypoints
            m_MoveCoroutine = StartCoroutine(MoveBrickAlongWaypoints());

            // Lock player input and move the player along its waypoints
            if (collision.gameObject.TryGetComponent(out PlayerController player))
                player.StartWaypointTransport(playerWaypoints, playerMoveSpeed);
        }
    }

    private IEnumerator MoveBrickAlongWaypoints()
    {
        this.gameObject.layer = LayerMask.NameToLayer("Ground");
        foreach (Transform target in brickWaypoints)
        {
            if (target == null) continue;

            while (Vector2.Distance(transform.position, target.position) > 0.01f)
            {
                transform.position = Vector2.MoveTowards(transform.position, target.position, brickMoveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = target.position;
        }

        // Brick reached its final position — disable the trap zone(s).
        if (objectsToDisableWhenDone != null)
            foreach (GameObject obj in objectsToDisableWhenDone)
                if (obj != null) obj.SetActive(false);

    }
}
