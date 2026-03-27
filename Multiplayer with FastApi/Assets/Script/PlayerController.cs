/*
 * PlayerController.cs
 * ────────────────────
 * Handles LOCAL player:
 *   - WASD / Arrow key movement
 *   - Sends position to server every 50ms (20 times/sec)
 *   - Camera follows this player
 *
 * SETUP:
 *   - Attach to LocalPlayerPrefab (Blue Capsule)
 *   - Requires CharacterController component on same GameObject
 *   - Disable this script by default; GameManager enables it on game start
 */

using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.8f;

    [Header("Camera")]
    public float cameraHeight = 8f;   // How high the camera is above player
    public float cameraDistance = 5f;   // How far behind the player

    // ── Internal ──────────────────────────────────────────────
    private CharacterController cc;
    private Camera mainCam;
    private Vector3 velocity;        // for gravity
    private float sendTimer = 0f;
    private const float SEND_INTERVAL = 0.05f;  // 50ms = 20 updates/sec

    private Vector3 lastSentPos;
    private float lastSentRot;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        cc = GetComponent<CharacterController>();
        mainCam = Camera.main;
    }

    void Update()
    {
        HandleMovement();
       // HandleCameraFollow();
        HandleNetworkSend();
    }

    // ─────────────────────────────────────────────────────────
    //  MOVEMENT
    // ─────────────────────────────────────────────────────────
    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");  // A/D or ←/→
        float v = Input.GetAxis("Vertical");    // W/S or ↑/↓

        // Move relative to world axes (top-down style)
        Vector3 moveDir = new Vector3(h, 0f, v).normalized;

        if (moveDir.magnitude > 0.1f)
        {
            // Rotate to face movement direction
            float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);

            cc.Move(moveDir * moveSpeed * Time.deltaTime);
        }

        // Simple gravity
        if (cc.isGrounded) velocity.y = -0.5f;
        else velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────
    //  CAMERA — follows player from above/behind (isometric-ish)
    // ─────────────────────────────────────────────────────────
    void HandleCameraFollow()
    {
        if (mainCam == null) return;

        Vector3 desiredPos = transform.position
                           + Vector3.up * cameraHeight
                           - transform.forward * cameraDistance;

        mainCam.transform.position = Vector3.Lerp(
            mainCam.transform.position, desiredPos, Time.deltaTime * 8f);

        mainCam.transform.LookAt(transform.position + Vector3.up * 1f);
    }

    // ─────────────────────────────────────────────────────────
    //  NETWORK SEND — throttled to 20/sec
    // ─────────────────────────────────────────────────────────
    void HandleNetworkSend()
    {
        sendTimer += Time.deltaTime;
        if (sendTimer < SEND_INTERVAL) return;
        sendTimer = 0f;

        Vector3 pos = transform.position;
        float rotY = transform.eulerAngles.y;

        // Only send if we actually moved (saves bandwidth)
        if (Vector3.Distance(pos, lastSentPos) < 0.001f &&
            Mathf.Abs(rotY - lastSentRot) < 0.1f)
            return;

        lastSentPos = pos;
        lastSentRot = rotY;

        NetworkManager.Instance.SendMove(pos, rotY);
    }
}