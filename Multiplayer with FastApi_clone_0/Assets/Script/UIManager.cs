// UIManager.cs
// Attach to the Canvas GameObject.

using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Room Panel (shown before game)")]
    public GameObject roomPanel;
    public InputField ipInput;        // server IP field
    public InputField roomInput;      // room ID field
    public InputField playerIDInput;  // player1 or player2
    public Button connectButton;

    [Header("Game Panel (shown during game)")]
    public GameObject gamePanel;

    void Start()
    {
        // Show room panel first, hide game panel
        roomPanel.SetActive(true);
        gamePanel.SetActive(false);

        // Pre-fill defaults so testing is faster
        ipInput.text = "10.201.31.104";   // ← change to your PC's IP
        roomInput.text = "ROOM42";
        playerIDInput.text = "player1";       // player2 on second device

        connectButton.onClick.AddListener(OnConnectClicked);
    }

    void OnConnectClicked()
    {
        string ip = ipInput.text.Trim();
        string room = roomInput.text.Trim().ToUpper();
        string pid = playerIDInput.text.Trim().ToLower();

        if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(room) || string.IsNullOrEmpty(pid))
        {
            Debug.LogWarning("Fill in all fields!");
            return;
        }

        // Switch panels
        roomPanel.SetActive(false);
        gamePanel.SetActive(true);

        // Tell NetworkManager to connect
        NetworkManager.Instance.Connect(ip, room, pid);
    }
}