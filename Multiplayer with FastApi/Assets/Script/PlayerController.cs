/*
 * PlayerController.cs  (v2)
 * ──────────────────────────
 * CHANGES from v1:
 *   - Camera logic REMOVED — you control camera yourself in scene
 *   - SendAttack() called by GameManager (Space / button)
 *   - Everything else same: WASD, send pos 20x/sec
 */

using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.8f;

    private CharacterController cc;
    private Vector3 velocity;

    private float sendTimer = 0f;
    private const float SEND_INTERVAL = 0.05f;  // 20 times/sec

    private Vector3 lastSentPos;
    private float lastSentRot;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        HandleMovement();
        HandleNetworkSend();
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 moveDir = new Vector3(h, 0f, v).normalized;

        if (moveDir.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
            cc.Move(moveDir * moveSpeed * Time.deltaTime);
        }

        // Gravity
        if (cc.isGrounded) velocity.y = -0.5f;
        else velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }

    void HandleNetworkSend()
    {
        sendTimer += Time.deltaTime;
        if (sendTimer < SEND_INTERVAL) return;
        sendTimer = 0f;

        Vector3 pos = transform.position;
        float rotY = transform.eulerAngles.y;

        if (Vector3.Distance(pos, lastSentPos) < 0.001f &&
            Mathf.Abs(rotY - lastSentRot) < 0.1f) return;

        lastSentPos = pos;
        lastSentRot = rotY;

        NetworkManager.Instance.SendMove(pos, rotY);
    }
}