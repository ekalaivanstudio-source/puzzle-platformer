using UnityEngine;

public class CameraFollowDeadZone : MonoBehaviour
{
    // All tuning below is driven by the level's LevelConfig (via LevelContext) at runtime.
    // The fields stay serialized but hidden so existing per-scene values are preserved as a
    // fallback (and can be migrated into a LevelConfig by the collectable tools).

    // References — resolved automatically at runtime.
    [HideInInspector, SerializeField] private Transform target;
    [HideInInspector, SerializeField] private Camera targetCamera;

    // Dead zone
    [HideInInspector, SerializeField] private float deadZoneX = 0.5f;
    [HideInInspector, SerializeField] private float deadZoneY = 0.3f;

    // Offset
    [HideInInspector, SerializeField] private Vector2 offset;

    // Smooth
    [HideInInspector, SerializeField] private float smoothTime = 0.15f;

    // Bounds
    [HideInInspector, SerializeField] private float minX;
    [HideInInspector, SerializeField] private float maxX;
    [HideInInspector, SerializeField] private float minY;
    [HideInInspector, SerializeField] private float maxY;

    // Options
    [HideInInspector, SerializeField] private bool followX = true;
    [HideInInspector, SerializeField] private bool followY = true;

    private Vector3 velocity;

    private void Awake()
    {
        ApplyConfig();

        if (target == null)
        {
            // Scene-scoped, and disabled objects included: a level with an entry door starts
            // with its player disabled in the doorway, so the ordinary search finds nothing
            // and the camera would never follow anyone — while an inactive-inclusive Unity
            // search would hand back the player PREFAB ASSET and follow that instead.
            PlayerController player = SceneObjects.FindInActiveScene<PlayerController>();

            if (player != null)
                target = player.transform;
        }
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    /// <summary>Copies the dead-zone settings from the level's config, if one is present.</summary>
    private void ApplyConfig()
    {
        LevelConfig cfg = LevelContext.Instance != null ? LevelContext.Instance.Config : null;
        if (cfg == null) return;

        LevelConfig.CameraDeadZoneSettings c = cfg.cameraDeadZone;
        deadZoneX = c.deadZoneX;
        deadZoneY = c.deadZoneY;
        offset = c.offset;
        smoothTime = c.smoothTime;
        minX = c.minX;
        maxX = c.maxX;
        minY = c.minY;
        maxY = c.maxY;
        followX = c.followX;
        followY = c.followY;
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