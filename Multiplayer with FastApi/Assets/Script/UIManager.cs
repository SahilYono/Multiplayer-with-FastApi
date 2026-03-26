// UIManager.cs
// ONE instance per player.
// Each player has their own connect panel and connect button.
// Attach to the Canvas (or a child panel).

using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // ── Set in Inspector ──────────────────────────────────────────
    public int playerIndex = 0;   // 0 = Player1,  1 = Player2

    [Header("Panels")]
    public GameObject connectPanel;   // shown before connecting
    public GameObject gamePanel;      // shown after connecting

    [Header("Connect Panel Inputs")]
    public InputField ipInput;
    public InputField roomInput;
    public InputField playerIDInput;
    public Button connectButton;

    [Header("Network Manager for this player")]
    public NetworkManager networkManager;

    void Start()
    {
        // Show connect panel, hide game panel
        connectPanel.SetActive(true);
        gamePanel.SetActive(false);

        // Pre-fill sensible defaults
        ipInput.text = "127.0.0.1";           // localhost — same machine
        roomInput.text = "ROOM1";

        if (playerIndex == 0)
        {
            playerIDInput.text = "player1";
        }
        else
        {
            playerIDInput.text = "player2";
            // Player 2 defaults to a different position in the input
        }

        connectButton.onClick.AddListener(OnConnectClicked);
    }

    void OnConnectClicked()
    {
        string ip = ipInput.text.Trim();
        string room = roomInput.text.Trim().ToUpper();
        string pid = playerIDInput.text.Trim().ToLower();

        // Validate
        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning($"[UI{playerIndex}] IP is empty!");
            return;
        }
        if (string.IsNullOrEmpty(room))
        {
            Debug.LogWarning($"[UI{playerIndex}] Room ID is empty!");
            return;
        }
        if (string.IsNullOrEmpty(pid))
        {
            Debug.LogWarning($"[UI{playerIndex}] Player ID is empty!");
            return;
        }

        Debug.Log($"[UI{playerIndex}] Connecting as {pid} to room {room} at {ip}");

        // Switch panels
        connectPanel.SetActive(false);
        gamePanel.SetActive(true);

        // Connect
        networkManager.Connect(ip, room, pid);
    }
}