// PlayerController.cs
// Attach to the LOCAL player capsule (LocalPlayer1 or LocalPlayer2)
//
// Player 1 controls: WASD to move, Space to attack
// Player 2 controls: Arrow Keys to move, Enter/Return to attack
// This way both players work independently in the SAME Unity window.

using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // ── Set in Inspector ──────────────────────────────────────────
    // 0 = Player 1 (uses WASD + Space)
    // 1 = Player 2 (uses Arrow Keys + Enter)
    public int playerIndex = 0;

    public float moveSpeed = 5f;

    // ── Reference to THIS player's NetworkManager ──────────────────
    public NetworkManager networkManager;

    // ── Internal state ─────────────────────────────────────────────
    float sendTimer = 0f;
    float sendInterval = 0.05f;   // send position 20x per second
    Vector3 lastSentPos;
    float lastSentRotY;

    bool gameActive = false;   // only send data after game starts

    public void SetGameActive(bool active)
    {
        gameActive = active;
    }

    void Update()
    {
        if (!gameActive) return;
        if (networkManager == null || !networkManager.connected) return;

        HandleMovement();
        HandleAttack();
        HandlePositionSync();
    }

    void HandleMovement()
    {
        float h = 0f;
        float v = 0f;

        if (playerIndex == 0)
        {
            // Player 1: WASD
            if (Input.GetKey(KeyCode.W)) v = 1f;
            if (Input.GetKey(KeyCode.S)) v = -1f;
            if (Input.GetKey(KeyCode.A)) h = -1f;
            if (Input.GetKey(KeyCode.D)) h = 1f;
        }
        else
        {
            // Player 2: Arrow Keys
            if (Input.GetKey(KeyCode.UpArrow)) v = 1f;
            if (Input.GetKey(KeyCode.DownArrow)) v = -1f;
            if (Input.GetKey(KeyCode.LeftArrow)) h = -1f;
            if (Input.GetKey(KeyCode.RightArrow)) h = 1f;
        }

        Vector3 dir = new Vector3(h, 0f, v).normalized;

        if (dir.magnitude > 0.01f)
        {
            transform.Translate(dir * moveSpeed * Time.deltaTime, Space.World);
            transform.rotation = Quaternion.LookRotation(dir);
        }

        // Keep player on the ground plane (y stays at 1)
        Vector3 pos = transform.position;
        pos.y = 1f;
        transform.position = pos;
    }

    void HandleAttack()
    {
        bool attacked = false;

        if (playerIndex == 0 && Input.GetKeyDown(KeyCode.Space))
            attacked = true;

        if (playerIndex == 1 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            attacked = true;

        if (attacked)
        {
            networkManager.SendAttack();
            // Flash the capsule briefly to show attack visually
            StartCoroutine(FlashAttack());
        }
    }

    System.Collections.IEnumerator FlashAttack()
    {
        var rend = GetComponent<Renderer>();
        if (rend == null) yield break;

        Color original = rend.material.color;
        rend.material.color = Color.yellow;
        yield return new WaitForSeconds(0.15f);
        rend.material.color = original;
    }

    void HandlePositionSync()
    {
        sendTimer += Time.deltaTime;
        if (sendTimer < sendInterval) return;
        sendTimer = 0f;

        // Only send if actually moved
        if (transform.position == lastSentPos &&
            transform.eulerAngles.y == lastSentRotY)
            return;

        lastSentPos = transform.position;
        lastSentRotY = transform.eulerAngles.y;

        networkManager.SendMove(transform.position, transform.eulerAngles.y);
    }
}