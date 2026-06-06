using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Proximity-based key collection zone.
///
/// Flow:
///   1. When the player enters within <see cref="m_ProximityDistance"/> units, the game
///      slows to <see cref="m_SlowTimeScale"/> and the pick icon becomes visible.
///   2. The player presses the configured pickup button to collect the key.
///   3. Time and the icon restore; <see cref="Key.Interact"/> notifies GameManager.
///
/// The <see cref="m_PickupAction"/> is a standalone <see cref="InputAction"/> â€” configure
/// its bindings in the Inspector. It is independent of DeviceInputProvider so it
/// fires correctly during the execution phase.
/// </summary>
[RequireComponent(typeof(Key))]
public class KeyPickupZone : MonoBehaviour
{
    [Header("Proximity")]
    [Tooltip("Distance (units) at which slow motion and the pick icon activate.")]
    [SerializeField] private float m_ProximityDistance = 3f;

    [Tooltip("Tag used to locate the player GameObject at runtime.")]
    [SerializeField] private string m_PlayerTag = "Player";

    [Header("Slow Motion")]
    [Tooltip("Time scale applied when the player is within proximity (0â€“1).")]
    [SerializeField] private float m_SlowTimeScale = 0.25f;

    [Header("UI")]
    [Tooltip("GameObject shown as the pick prompt (e.g. a sprite with the interact icon).")]
    [SerializeField] private GameObject m_PickIcon;

    [Header("Light")]
    [Tooltip("Spot Light 2D on this key. Its intensity is set to m_CollectedLightIntensity when collected.")]
    [SerializeField] private Light2D m_SpotLight;
    [SerializeField] private float m_CollectedLightIntensity = 10f;
    [SerializeField] private float m_CollectedOuterRadius = 2f;

    [Header("Collect FX")]
    [Tooltip("Seconds the light shines after the sprite hides before the object is deactivated.")]
    [SerializeField] private float m_DisableDelay = 0.2f;

    [Header("Camera Focus")]
    [Tooltip("Field of view to zoom to during slow motion (smaller = more zoomed in).")]
    [SerializeField] private float m_FocusCameraSize = 30f;
    [Tooltip("Seconds (real time) to reach the focus zoom level.")]
    [SerializeField] private float m_ZoomDuration = 0.3f;
    [Tooltip("Seconds (real time) to revert back to the original FOV.")]
    [SerializeField] private float m_RevertZoomDuration = 0.2f;

    [SerializeField] private BoxCollider2D doorCollider; // optional collider to disable when the key is collected

    private Key m_Key;
    private Transform m_PlayerTransform;
    private InputAction m_PickupAction;
    private bool m_InProximity;
    private bool m_Collected;
    private SpriteRenderer m_SpriteRenderer;
    private Camera m_Camera;
    private float m_OriginalCameraSize;
    private Vector3 m_OriginalCameraPosition;
    private Coroutine m_ZoomCoroutine;
    private float m_OriginalLightIntensity;
    private float m_OriginalOuterRadius;

    // â”€â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void Awake()
    {
        m_Key = GetComponent<Key>();
        m_SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        m_Camera = Camera.main;
        if (m_Camera != null)
        {
            m_OriginalCameraSize = m_Camera.fieldOfView;
            m_OriginalCameraPosition = m_Camera.transform.position;
        }

        if (m_SpotLight != null)
        {
            m_OriginalLightIntensity = m_SpotLight.intensity;
            m_OriginalOuterRadius = m_SpotLight.pointLightOuterRadius;
        }

        GameManager.OnTurnReset += ResetKey;

        // Mirrors the Interact bindings from PlayerInputActions (E, Z, Gamepad ButtonWest).
        // Standalone action â€” not part of the main asset, so it fires during execution too.
        m_PickupAction = new InputAction("KeyPickup", InputActionType.Button);
        m_PickupAction.AddBinding("<Keyboard>/e");
        m_PickupAction.AddBinding("<Keyboard>/z");
        m_PickupAction.AddBinding("<Gamepad>/buttonWest");

        if (m_PickIcon != null)
            m_PickIcon.SetActive(false);
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag(m_PlayerTag);
        if (player != null)
            m_PlayerTransform = player.transform;
        else
            Debug.LogWarning($"[KeyPickupZone] No GameObject with tag '{m_PlayerTag}' found.", this);
    }

    private void OnDisable()
    {
        // Restore time and clean up input if the object is disabled mid-proximity.
        // Do NOT Dispose here â€” Key.Interact() calls SetActive(false) which fires OnDisable
        // while still inside the InputAction performed callback; Dispose during a callback crashes.
        if (m_InProximity) RestoreTime();
        m_InProximity = false;

        if (m_PickIcon != null)
            m_PickIcon.SetActive(false);

        UnbindPickup();
    }

    private void OnDestroy()
    {
        // Safe to Dispose here â€” OnDestroy never fires during an input callback.
        GameManager.OnTurnReset -= ResetKey;
        UnbindPickup();
        m_PickupAction?.Dispose();
    }

    // â”€â”€â”€ Reset â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void ResetKey()
    {
        StopAllCoroutines();
        m_ZoomCoroutine = null;

        m_Collected = false;
        m_InProximity = false;

        // Restore visuals
        if (m_SpriteRenderer != null) m_SpriteRenderer.enabled = true;
        if (m_SpotLight != null)
        {
            m_SpotLight.enabled = true;
            m_SpotLight.intensity = m_OriginalLightIntensity;
            m_SpotLight.pointLightOuterRadius = m_OriginalOuterRadius;
        }
        if (m_PickIcon != null) m_PickIcon.SetActive(false);

        // Restore time and camera in case reset happened mid-proximity
        RestoreTime();
        if (m_Camera != null)
        {
            m_Camera.fieldOfView = m_OriginalCameraSize;
            m_Camera.transform.position = m_OriginalCameraPosition;
        }

        UnbindPickup();

        // Re-enable the GameObject (Key.Interact set it inactive)
        gameObject.SetActive(true);
    }

    // â”€â”€â”€ Update â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void Update()
    {
        if (m_Collected || m_PlayerTransform == null) return;

        bool inRange = Vector2.Distance(transform.position, m_PlayerTransform.position) <= m_ProximityDistance;

        if (inRange && !m_InProximity)
            OnEnterProximity();
        else if (!inRange && m_InProximity)
            OnExitProximity();
    }

    // â”€â”€â”€ Proximity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnEnterProximity()
    {
        m_InProximity = true;
        ApplySlowMo();

        // Zoom in and pan camera to the player's current world position.
        Vector3 focusPos = m_Camera != null
            ? new Vector3(m_PlayerTransform.position.x, m_PlayerTransform.position.y, m_Camera.transform.position.z)
            : m_OriginalCameraPosition;
        StartZoom(m_FocusCameraSize, m_ZoomDuration, focusPos);

        if (m_PickIcon != null)
            m_PickIcon.SetActive(true);

        BindPickup();
    }

    private void OnExitProximity()
    {
        m_InProximity = false;
        RestoreTime();
        StartZoom(m_OriginalCameraSize, m_RevertZoomDuration, m_OriginalCameraPosition);

        if (m_PickIcon != null)
            m_PickIcon.SetActive(false);

        UnbindPickup();
    }

    // â”€â”€â”€ Input â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void BindPickup()
    {
        m_PickupAction.performed += OnPickup;
        m_PickupAction.Enable();
    }

    private void UnbindPickup()
    {
        m_PickupAction.performed -= OnPickup;
        m_PickupAction.Disable();
    }

    private void OnPickup(InputAction.CallbackContext ctx)
    {
        if (!m_InProximity || m_Collected) return;
        Collect();
    }

    // â”€â”€â”€ Collection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void Collect()
    {
        m_Collected = true;
        m_InProximity = false;
        RestoreTime();

        if (m_PickIcon != null)
            m_PickIcon.SetActive(false);

        UnbindPickup();

        if (m_SpotLight != null)
        {
            m_SpotLight.intensity = m_CollectedLightIntensity;
            m_SpotLight.pointLightOuterRadius = m_CollectedOuterRadius;
        }

        StartZoom(m_OriginalCameraSize, m_RevertZoomDuration, m_OriginalCameraPosition);


        StartCoroutine(CollectRoutine());
        if (doorCollider != null)
            doorCollider.enabled = true;
    }

    private IEnumerator CollectRoutine()
    {
        // Hide the sprite immediately so the key looks collected
        if (m_SpriteRenderer != null)
            m_SpriteRenderer.enabled = false;

        // Let the light flash briefly then turn off
        yield return new WaitForSecondsRealtime(0.1f);
        if (m_SpotLight != null)
            m_SpotLight.enabled = false;

        // Wait for the zoom-out and any remaining delay to finish
        yield return new WaitForSecondsRealtime(Mathf.Max(m_DisableDelay, m_RevertZoomDuration) - 0.1f);

        // Guarantee FOV and position are fully restored even if zoom coroutine didn't finish
        if (m_Camera != null)
        {
            m_Camera.fieldOfView = m_OriginalCameraSize;
            m_Camera.transform.position = m_OriginalCameraPosition;
        }

        m_Key.Interact(); // notifies GameManager and calls SetActive(false)
    }

    // â”€â”€â”€ Time â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void ApplySlowMo()
    {
        Time.timeScale = m_SlowTimeScale;
        Time.fixedDeltaTime = 0.02f * m_SlowTimeScale; // keep physics steps consistent
    }

    private void RestoreTime()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    // â”€â”€â”€ Camera Zoom â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void StartZoom(float targetSize, float duration, Vector3 targetPosition)
    {
        if (m_Camera == null) return;
        if (m_ZoomCoroutine != null) StopCoroutine(m_ZoomCoroutine);
        m_ZoomCoroutine = StartCoroutine(ZoomRoutine(targetSize, duration, targetPosition));
    }

    private IEnumerator ZoomRoutine(float targetSize, float duration, Vector3 targetPosition)
    {
        float startSize = m_Camera.fieldOfView;
        Vector3 startPos = m_Camera.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            m_Camera.fieldOfView = Mathf.Lerp(startSize, targetSize, t);
            m_Camera.transform.position = Vector3.Lerp(startPos, targetPosition, t);
            yield return null;
        }

        m_Camera.fieldOfView = targetSize;
        m_Camera.transform.position = targetPosition;
        m_ZoomCoroutine = null;
    }
}
