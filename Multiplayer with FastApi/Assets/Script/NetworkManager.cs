/*
 * NetworkManager.cs
 * ─────────────────
 * Single point of truth for all WebSocket communication.
 * - Connects to FastAPI server
 * - Sends JSON messages
 * - Receives messages and fires C# events
 * - GameManager listens to those events
 *
 * SETUP IN UNITY:
 *   1. Create an empty GameObject → name it "NetworkManager"
 *   2. Attach this script
 *   3. Install websocket package (see bottom of file)
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;   // << install this package (see README at bottom)

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;          // Singleton — access from anywhere

    [Header("Server Settings")]
    public string serverIP = "192.168.1.100";    // ← change to your PC's local IP
    public int serverPort = 8000;

    // ── Public state ──────────────────────────────────────────
    public string MyPlayerId { get; private set; }
    public string CurrentRoom { get; private set; }
    public bool IsHost { get; private set; }

    // ── Events — GameManager subscribes to these ──────────────
    public event Action<string> OnRoomCreated;      // room_id
    public event Action<string, string, List<string>> OnJoinedRoom;       // room_id, my_id, all_players
    public event Action<string> OnOtherPlayerJoined;// other player_id
    public event Action<string, List<string>, Dictionary<string, Vector3>> OnGameStarted;
    public event Action<string, Vector3, float> OnPlayerMoved;      // pid, pos, rot_y
    public event Action<string> OnPlayerLeft;
    public event Action<string> OnError;

    private WebSocket ws;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        // Singleton pattern — only one NetworkManager exists
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Auto-generate unique player ID using device name + random number
        MyPlayerId = SystemInfo.deviceName.Replace(" ", "_") + "_" + UnityEngine.Random.Range(1000, 9999);
    }

    // ─────────────────────────────────────────────────────────
    async void Start()
    {
        string url = $"ws://{serverIP}:{serverPort}/ws";
        Debug.Log($"[NET] Connecting to {url}");

        ws = new WebSocket(url);

        ws.OnOpen += () => Debug.Log("[NET] Connected to server");
        ws.OnError += (e) => Debug.LogError($"[NET] Error: {e}");
        ws.OnClose += (e) => Debug.Log($"[NET] Connection closed: {e}");
        ws.OnMessage += OnMessageReceived;

        await ws.Connect();
    }

    void Update()
    {
        // NativeWebSocket requires this to dispatch messages on main thread
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    async void OnApplicationQuit()
    {
        if (ws != null) await ws.Close();
    }

    // ─────────────────────────────────────────────────────────
    //  SEND helpers — called by GameManager / PlayerController
    // ─────────────────────────────────────────────────────────

    public async void CreateRoom()
    {
        var msg = new Dictionary<string, string>
        {
            ["action"] = "create_room",
            ["player_id"] = MyPlayerId
        };
        await SendJson(msg);
    }

    public async void JoinRoom(string roomId)
    {
        var msg = new Dictionary<string, string>
        {
            ["action"] = "join_room",
            ["room_id"] = roomId,
            ["player_id"] = MyPlayerId
        };
        await SendJson(msg);
    }

    public async void StartGame()
    {
        var msg = new Dictionary<string, string>
        {
            ["action"] = "start_game",
            ["player_id"] = MyPlayerId,
            ["room_id"] = CurrentRoom
        };
        await SendJson(msg);
    }

    public async void SendMove(Vector3 pos, float rotY)
    {
        // Send position 20 times per second (called from PlayerController)
        var msg = new Dictionary<string, object>
        {
            ["action"] = "move",
            ["room_id"] = CurrentRoom,
            ["player_id"] = MyPlayerId,
            ["x"] = pos.x,
            ["y"] = pos.y,
            ["z"] = pos.z,
            ["rot_y"] = rotY
        };
        await SendJson(msg);
    }

    async System.Threading.Tasks.Task SendJson(object obj)
    {
        if (ws == null || ws.State != WebSocketState.Open)
        {
            Debug.LogWarning("[NET] Not connected, cannot send");
            return;
        }
        string json = JsonUtility.ToJson(new SerializableWrapper(obj));
        // Use MiniJSON or Newtonsoft for dict serialization
        string jsonStr = MiniJSON.Json.Serialize(obj);
        await ws.SendText(jsonStr);
    }

    // ─────────────────────────────────────────────────────────
    //  RECEIVE — parse incoming JSON and fire events
    // ─────────────────────────────────────────────────────────
    void OnMessageReceived(byte[] bytes)
    {
        string raw = System.Text.Encoding.UTF8.GetString(bytes);
        Debug.Log($"[NET] Received: {raw}");

        var data = MiniJSON.Json.Deserialize(raw) as Dictionary<string, object>;
        if (data == null) return;

        string evt = data["event"].ToString();

        switch (evt)
        {
            case "room_created":
                CurrentRoom = data["room_id"].ToString();
                IsHost = true;
                OnRoomCreated?.Invoke(CurrentRoom);
                break;

            case "joined_room":
                CurrentRoom = data["room_id"].ToString();
                IsHost = false;
                var players = ParseStringList(data["players"]);
                OnJoinedRoom?.Invoke(CurrentRoom, MyPlayerId, players);
                break;

            case "player_joined":
                string joinedId = data["player_id"].ToString();
                OnOtherPlayerJoined?.Invoke(joinedId);
                break;

            case "game_started":
                var plist = ParseStringList(data["players"]);
                string host = data["host_id"].ToString();
                var spawnRaw = data["spawn_positions"] as Dictionary<string, object>;
                var spawns = new Dictionary<string, Vector3>();
                if (spawnRaw != null)
                {
                    foreach (var kv in spawnRaw)
                    {
                        var posDict = kv.Value as Dictionary<string, object>;
                        spawns[kv.Key] = new Vector3(
                            Convert.ToSingle(posDict["x"]),
                            Convert.ToSingle(posDict["y"]),
                            Convert.ToSingle(posDict["z"])
                        );
                    }
                }
                OnGameStarted?.Invoke(host, plist, spawns);
                break;

            case "player_moved":
                string pid = data["player_id"].ToString();
                var pos = new Vector3(
                    Convert.ToSingle(data["x"]),
                    Convert.ToSingle(data["y"]),
                    Convert.ToSingle(data["z"])
                );
                float rotY = Convert.ToSingle(data["rot_y"]);
                OnPlayerMoved?.Invoke(pid, pos, rotY);
                break;

            case "player_left":
                OnPlayerLeft?.Invoke(data["player_id"].ToString());
                break;

            case "error":
                OnError?.Invoke(data["message"].ToString());
                break;
        }
    }

    List<string> ParseStringList(object raw)
    {
        var result = new List<string>();
        if (raw is List<object> list)
            foreach (var item in list)
                result.Add(item.ToString());
        return result;
    }

    // Dummy wrapper — not actually used, MiniJSON handles serialization
    [Serializable] class SerializableWrapper { public SerializableWrapper(object o) { } }
}

/*
 ═══════════════════════════════════════════════════════════
  PACKAGE SETUP (do this once in Unity)
 ═══════════════════════════════════════════════════════════
 
  1. NativeWebSocket
     Window → Package Manager → + → Add from URL:
     https://github.com/endel/NativeWebSocket.git#upm
 
  2. MiniJSON (for Dictionary serialization)
     - Download: https://github.com/nicloay/miniJson
     - Drop MiniJSON.cs into Assets/Scripts/
     OR use Newtonsoft.Json (also fine)
 
 ═══════════════════════════════════════════════════════════
*/