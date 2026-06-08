using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An invisible trigger zone that locks the player into a scripted path (waypoints)
/// and then ends the turn — resetting everything like a wrong-input run.
/// Disabled automatically by MovableBrick once the player uses the correct route.
/// Re-enabled on turn reset so subsequent runs start fresh.
/// </summary>
public class InvisibleLockPoint : MonoBehaviour
{
    [SerializeField] private Transform[] playerWaypoints;
    [SerializeField] private float playerMoveSpeed = 5f;

    private Collider2D m_Collider;
    private bool m_Triggered = false;

    // Reused across frames so the per-frame overlap check allocates no garbage.
    private readonly List<Collider2D> m_OverlapResults = new List<Collider2D>();
    private ContactFilter2D m_NoFilter;

    private void Awake()
    {
        m_Collider = GetComponent<Collider2D>();
        m_NoFilter = ContactFilter2D.noFilter;
    }

    // Reset triggered flag whenever this object is re-enabled (e.g. after turn reset).
    private void OnEnable()
    {
        m_Triggered = false;
        GameManager.OnTurnReset += ResetLockPoint;
    }

    private void OnDisable()
    {
        GameManager.OnTurnReset -= ResetLockPoint;
    }

    private void ResetLockPoint()
    {
        m_Triggered = false;
    }

    // No Rigidbody2D on the player, so OnTriggerEnter2D won't fire.
    // Use an overlap check each frame instead.
    private void Update()
    {
        if (m_Triggered || m_Collider == null) return;

        int count = Physics2D.OverlapBox(
            m_Collider.bounds.center, m_Collider.bounds.size, 0f, m_NoFilter, m_OverlapResults);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = m_OverlapResults[i];
            if (hit.gameObject == gameObject) continue;
            if (!hit.CompareTag("Player")) continue;

            m_Triggered = true;
            if (hit.TryGetComponent(out PlayerController player))
                player.StartWaypointTransportThenEndTurn(playerWaypoints, playerMoveSpeed);
            return;
        }
    }
}

