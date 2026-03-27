/*
 * GameManager.cs
 * ──────────────
 * Controls:
 *   - All UI panels (Main Menu, Lobby, Game)
 *   - Spawning local + remote player capsules
 *   - Listening to NetworkManager events
 *
 * SETUP IN UNITY:
 *   1. Create empty GameObject → "GameManager" → attach this script
 *   2. Build the UI (see UI SETUP below) and wire references in Inspector
 *   3. Create two prefabs: PlayerPrefab (blue) and RemotePlayerPrefab (red)
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;  // if using TextMeshPro; swap to Text if using legacy UI

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // ── Inspector References — drag GameObjects here ──────────
    [Header("UI Panels")]
    public GameObject panelMain;     // Main menu: Create / Join buttons
    public GameObject panelLobby;    // After create: shows room ID + waiting
    public GameObject panelJoin;     // Join room: input field + join button
    public GameObject panelGame;     // In-game HUD (empty or minimal)

    [Header("Main Panel UI")]
    public Button btnCreateRoom;
    public Button btnGoToJoin;

    [Header("Lobby Panel UI")]
    public TMP_Text txtRoomId;        // Shows "Room: ABCD12"
    public TMP_Text txtWaiting;       // "Waiting for player..." or "Player 2 joined!"
    public Button btnStartGame;     // Only visible to host after P2 joins
    public TMP_Text txtMyPlayerId;    // Shows your player ID (optional debug info)

    [Header("Join Panel UI")]
    public TMP_InputField inputRoomId;  // Player types room ID here
    public Button btnJoinRoom;
    public Button btnBackToMain;
    public TMP_Text txtJoinStatus; // Error messages

    [Header("Player Prefabs")]
    public GameObject localPlayerPrefab;   // Blue capsule
    public GameObject remotePlayerPrefab;  // Red capsule

    // ── Internal state ─────────────────────────────────────────
    private GameObject localPlayerObj;
    private Dictionary<string, GameObject> remotePlayers = new Dictionary<string, GameObject>();
    private string hostId;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        ShowPanel(panelMain);
        btnStartGame.gameObject.SetActive(false);

        // Wire buttons
        btnCreateRoom.onClick.AddListener(OnClickCreate);
        btnGoToJoin.onClick.AddListener(() => ShowPanel(panelJoin));
        btnJoinRoom.onClick.AddListener(OnClickJoin);
        btnBackToMain.onClick.AddListener(() => ShowPanel(panelMain));
        btnStartGame.onClick.AddListener(OnClickStart);

        // Subscribe to network events
        var net = NetworkManager.Instance;
        net.OnRoomCreated += HandleRoomCreated;
        net.OnJoinedRoom += HandleJoinedRoom;
        net.OnOtherPlayerJoined += HandleOtherPlayerJoined;
        net.OnGameStarted += HandleGameStarted;
        net.OnPlayerMoved += HandlePlayerMoved;
        net.OnPlayerLeft += HandlePlayerLeft;
        net.OnError += HandleError;
    }

    // ─────────────────────────────────────────────────────────
    //  BUTTON HANDLERS
    // ─────────────────────────────────────────────────────────

    void OnClickCreate()
    {
        NetworkManager.Instance.CreateRoom();
        txtWaiting.text = "Creating room...";
        ShowPanel(panelLobby);
        btnStartGame.gameObject.SetActive(false);
    }

    void OnClickJoin()
    {
        string roomId = inputRoomId.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(roomId))
        {
            txtJoinStatus.text = "Please enter a room ID";
            return;
        }
        txtJoinStatus.text = "Joining...";
        NetworkManager.Instance.JoinRoom(roomId);
    }

    void OnClickStart()
    {
        NetworkManager.Instance.StartGame();
    }

    // ─────────────────────────────────────────────────────────
    //  NETWORK EVENT HANDLERS
    // ─────────────────────────────────────────────────────────

    // Server confirmed room was created
    void HandleRoomCreated(string roomId)
    {
        txtRoomId.text = $"Room ID:  {roomId}";
        txtWaiting.text = "Waiting for second player...";
        txtMyPlayerId.text = $"You: {NetworkManager.Instance.MyPlayerId}";
        btnStartGame.gameObject.SetActive(false);
        ShowPanel(panelLobby);
    }

    // Server confirmed we joined a room
    void HandleJoinedRoom(string roomId, string myId, List<string> players)
    {
        txtRoomId.text = $"Room ID:  {roomId}";
        txtWaiting.text = "Joined! Waiting for host to start...";
        txtMyPlayerId.text = $"You: {myId}";
        btnStartGame.gameObject.SetActive(false);  // joiner never sees start button
        ShowPanel(panelLobby);
    }

    // Second player arrived (host sees this)
    void HandleOtherPlayerJoined(string playerId)
    {
        txtWaiting.text = $"Player 2 connected!\nReady to start.";

        // Show start button ONLY to the host
        if (NetworkManager.Instance.IsHost)
            btnStartGame.gameObject.SetActive(true);
    }

    // Game started — hide all UI, spawn players
    void HandleGameStarted(string hostPlayerId, List<string> players, Dictionary<string, Vector3> spawns)
    {
        hostId = hostPlayerId;

        // Hide ALL UI panels
        panelMain.SetActive(false);
        panelLobby.SetActive(false);
        panelJoin.SetActive(false);
        panelGame.SetActive(true);  // minimal HUD (can be empty)

        string myId = NetworkManager.Instance.MyPlayerId;

        // Spawn players
        foreach (string pid in players)
        {
            Vector3 spawnPos = spawns.ContainsKey(pid) ? spawns[pid] : Vector3.zero;

            if (pid == myId)
            {
                // Spawn LOCAL player (blue)
                localPlayerObj = Instantiate(localPlayerPrefab, spawnPos, Quaternion.identity);
                localPlayerObj.name = "LocalPlayer";
                localPlayerObj.GetComponent<PlayerController>().enabled = true;
            }
            else
            {
                // Spawn REMOTE player (red)
                var remoteObj = Instantiate(remotePlayerPrefab, spawnPos, Quaternion.identity);
                remoteObj.name = $"RemotePlayer_{pid}";
                remotePlayers[pid] = remoteObj;
            }
        }
    }

    // Move a remote player's capsule
    void HandlePlayerMoved(string playerId, Vector3 pos, float rotY)
    {
        if (remotePlayers.ContainsKey(playerId))
        {
            var remote = remotePlayers[playerId].GetComponent<RemotePlayer>();
            remote.SetTarget(pos, rotY);
        }
    }

    // Remove a player who disconnected
    void HandlePlayerLeft(string playerId)
    {
        if (remotePlayers.ContainsKey(playerId))
        {
            Destroy(remotePlayers[playerId]);
            remotePlayers.Remove(playerId);
        }
        // Show a reconnect screen or just log
        Debug.Log($"[GAME] Player {playerId} left the game");
    }

    void HandleError(string message)
    {
        // Show error on whichever panel is active
        txtJoinStatus.text = $"Error: {message}";
        Debug.LogWarning($"[GAME] Server error: {message}");
    }

    // ─────────────────────────────────────────────────────────
    //  UI HELPER
    // ─────────────────────────────────────────────────────────
    void ShowPanel(GameObject panel)
    {
        panelMain.SetActive(false);
        panelLobby.SetActive(false);
        panelJoin.SetActive(false);
        // panelGame stays controlled by game state
        panel.SetActive(true);
    }
}

/*
 ═══════════════════════════════════════════════════════════
  UI SETUP IN UNITY (build this in Canvas)
 ═══════════════════════════════════════════════════════════

  Canvas
  ├── PanelMain
  │   ├── Text "Local WiFi Multiplayer"
  │   ├── Button "Create Room"   → btnCreateRoom
  │   └── Button "Join Room"     → btnGoToJoin
  │
  ├── PanelLobby
  │   ├── Text (Room ID)         → txtRoomId
  │   ├── Text (waiting msg)     → txtWaiting
  │   ├── Text (your ID)         → txtMyPlayerId
  │   └── Button "Start Game"    → btnStartGame  (hidden by default)
  │
  ├── PanelJoin
  │   ├── InputField             → inputRoomId
  │   ├── Button "Join"          → btnJoinRoom
  │   ├── Button "Back"          → btnBackToMain
  │   └── Text (status/error)    → txtJoinStatus
  │
  └── PanelGame  (empty HUD panel, active during gameplay)

 ═══════════════════════════════════════════════════════════
  PLAYER PREFAB SETUP
 ═══════════════════════════════════════════════════════════

  LocalPlayerPrefab:
    - 3D Object → Capsule
    - Material: Blue
    - Add PlayerController.cs
    - Add CharacterController component
    - Tag: "LocalPlayer"

  RemotePlayerPrefab:
    - 3D Object → Capsule
    - Material: Red
    - Add RemotePlayer.cs
    - NO CharacterController (server drives position)
    - Tag: "RemotePlayer"

 ═══════════════════════════════════════════════════════════
*/