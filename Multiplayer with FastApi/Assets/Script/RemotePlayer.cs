/*
 * RemotePlayer.cs
 * ───────────────
 * Handles the REMOTE (opponent) player capsule:
 *   - Receives target position from GameManager
 *   - Smoothly interpolates to that position (lerp)
 *   - No CharacterController — purely visual
 *
 * SETUP:
 *   - Attach to RemotePlayerPrefab (Red Capsule)
 *   - GameManager calls SetTarget() whenever a move packet arrives
 */

using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    [Header("Smoothing")]
    public float positionLerpSpeed = 15f;  // Higher = snappier, lower = smoother
    public float rotationLerpSpeed = 15f;

    // ── Targets set by GameManager ─────────────────────────────
    private Vector3 targetPosition;
    private float targetRotY;
    private bool hasTarget = false;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        targetPosition = transform.position;
        targetRotY = transform.eulerAngles.y;
    }

    // Called by GameManager when a "player_moved" packet arrives
    public void SetTarget(Vector3 position, float rotY)
    {
        targetPosition = position;
        targetRotY = rotY;
        hasTarget = true;
    }

    // ─────────────────────────────────────────────────────────
    void Update()
    {
        if (!hasTarget) return;

        // Smoothly glide to the latest received position
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * positionLerpSpeed
        );

        // Smoothly rotate to face the right direction
        Quaternion targetRot = Quaternion.Euler(0f, targetRotY, 0f);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * rotationLerpSpeed
        );
    }
}