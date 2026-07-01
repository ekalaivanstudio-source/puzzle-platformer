using UnityEngine;

public class CameraFollowDeadZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Camera targetCamera;

    [Header("Dead Zone")]
    [SerializeField] private float deadZoneX = 0.5f;
    [SerializeField] private float deadZoneY = 0.3f;

    [Header("Offset")]
    [SerializeField] private Vector2 offset;

    [Header("Smooth")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Bounds")]
    [SerializeField] private float minX;
    [SerializeField] private float maxX;
    [SerializeField] private float minY;
    [SerializeField] private float maxY;

    [Header("Options")]
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = true;

    private Vector3 velocity;

    private void Awake()
    {
        if (target == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player != null)
                target = player.transform;
        }
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (target == null || targetCamera == null)
            return;

        Vector3 camPos = targetCamera.transform.position;

        float targetX = camPos.x;
        float targetY = camPos.y;

        float deltaX = (target.position.x + offset.x) - camPos.x;
        float deltaY = (target.position.y + offset.y) - camPos.y;

        if (followX && Mathf.Abs(deltaX) > deadZoneX)
        {
            targetX += deltaX - Mathf.Sign(deltaX) * deadZoneX;
        }

        if (followY && Mathf.Abs(deltaY) > deadZoneY)
        {
            targetY += deltaY - Mathf.Sign(deltaY) * deadZoneY;
        }

        targetX = Mathf.Clamp(targetX, minX, maxX);
        targetY = Mathf.Clamp(targetY, minY, maxY);

        Vector3 desiredPosition = new Vector3(
            targetX,
            targetY,
            camPos.z);

        targetCamera.transform.position = Vector3.SmoothDamp(
            camPos,
            desiredPosition,
            ref velocity,
            smoothTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (targetCamera == null)
            return;

        Gizmos.color = Color.yellow;

        Vector3 center = targetCamera.transform.position;

        Gizmos.DrawWireCube(
            center,
            new Vector3(deadZoneX * 2f, deadZoneY * 2f, 0));
    }
#endif
}