// PlayerController.cs
// Attach this to the LocalPlayer GameObject.

using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;

    // How often to send position to server (every 0.05s = 20 times/sec)
    float sendInterval = 0.05f;
    float sendTimer = 0f;

    // Track last sent position to avoid sending duplicates
    Vector3 lastSentPosition;
    float lastSentRotY;

    void Update()
    {
        HandleMovement();
        HandleAttack();
        HandlePositionSync();
    }

    void HandleMovement()
    {
        // Read WASD / Arrow keys
        float h = Input.GetAxis("Horizontal");  // A/D keys → left/right
        float v = Input.GetAxis("Vertical");    // W/S keys → forward/back

        // Build movement vector — we ignore Y axis (no flying)
        Vector3 move = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);

        // Rotate character to face movement direction
        if (move.magnitude > 0.01f)
            transform.LookAt(transform.position + move);
    }

    void HandleAttack()
    {
        // Space bar = attack
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Attack sent!");
            // Tell the server — server will check if it hit
            NetworkManager.Instance.SendAttack();

            // Optional: play an animation here (not required for the task)
        }
    }

    void HandlePositionSync()
    {
        // Don't send every single frame — that's too much data.
        // Send at a fixed interval (20 times/second is smooth enough).
        sendTimer += Time.deltaTime;
        if (sendTimer < sendInterval) return;
        sendTimer = 0f;

        // Only send if position actually changed (saves bandwidth)
        if (transform.position == lastSentPosition &&
            transform.eulerAngles.y == lastSentRotY)
            return;

        lastSentPosition = transform.position;
        lastSentRotY = transform.eulerAngles.y;

        NetworkManager.Instance.SendMove(transform.position, transform.eulerAngles.y);
    }
}