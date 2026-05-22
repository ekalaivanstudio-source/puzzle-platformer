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

    private bool triggered = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!triggered && collision.gameObject.CompareTag("Player"))
        {
            triggered = true;

            // Move the brick along its own waypoints
            StartCoroutine(MoveBrickAlongWaypoints());

            // Lock player input and move the player along its waypoints
            if (collision.gameObject.TryGetComponent(out PlayerController player))
                player.StartWaypointTransport(playerWaypoints, playerMoveSpeed);
        }
    }

    private IEnumerator MoveBrickAlongWaypoints()
    {
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
    }
}
