/*
 * NetworkManager.cs  (v2)
 * ───────────────────────
 * ADDED vs v1:
 *   - SendAttack()
 *   - SendEndGame()
 *   - OnHit event
 *   - OnGameOver event
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    [Header("Server Settings")]
    public string serverIP = "192.168.1.100";  // ← your PC's local IP
    public int serverPort = 8000;

    public string MyPlayerId { get; private set; }
    public string CurrentRoom { get; private set; }
    public bool IsHost { get; private set; }

    // ── Events ────────────────────────────────────────────────
    public event Action<string> OnRoomCreated;
    public event Action<string, string, List<string>> OnJoinedRoom;
    public event Action<string> OnOtherPlayerJoined;
    public event Action<string, List<string>, Dictionary<string, Vector3>> OnGameStarted;
    public event Action<string, Vector3, float> OnPlayerMoved;
    public event Action<string> OnPlayerLeft;
    public event Action<string, string, int, int, Dictionary<string, int>, bool> OnHit;
    //                  attacker  victim  dmg  victimHP  scores                  killed
    public event Action<string, Dictionary<string, int>> OnGameOver;
    //                  winner    finalScores
    public event Action<string> OnError;

    private WebSocket ws;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        MyPlayerId = SystemInfo.deviceName.Replace(" ", "_") + "_" + UnityEngine.Random.Range(1000, 9999);
    }

    async void Start()
    {
        string url = $"ws://{serverIP}:{serverPort}/ws";
        Debug.Log($"[NET] Connecting to {url}");

        ws = new WebSocket(url);
        ws.OnOpen += () => Debug.Log("[NET] Connected");
        ws.OnError += (e) => Debug.LogError($"[NET] Error: {e}");
        ws.OnClose += (e) => Debug.Log($"[NET] Closed: {e}");
        ws.OnMessage += OnMessageReceived;

        await ws.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    async void OnApplicationQuit()
    {
        if (ws != null) await ws.Close();
    }

    // ── SEND ──────────────────────────────────────────────────

    public async void CreateRoom()
    {
        await Send(new Dictionary<string, object>
        {
            ["action"] = "create_room",
            ["player_id"] = MyPlayerId
        });
    }

    public async void JoinRoom(string roomId)
    {
        await Send(new Dictionary<string, object>
        {
            ["action"] = "join_room",
            ["room_id"] = roomId,
            ["player_id"] = MyPlayerId
        });
    }

    public async void StartGame()
    {
        await Send(new Dictionary<string, object>
        {
            ["action"] = "start_game",
            ["player_id"] = MyPlayerId,
            ["room_id"] = CurrentRoom
        });
    }

    public async void SendMove(Vector3 pos, float rotY)
    {
        await Send(new Dictionary<string, object>
        {
            ["action"] = "move",
            ["room_id"] = CurrentRoom,
            ["player_id"] = MyPlayerId,
            ["x"] = pos.x,
            ["y"] = pos.y,
            ["z"] = pos.z,
            ["rot_y"] = rotY
        });
    }

    public async void SendAttack()
    {
        // Send local player position so server can do range check
        Vector3 myPos = GameManager.Instance.GetLocalPlayerPosition();
        Vector3 enemyPos = GameManager.Instance.GetRemotePlayerPosition();
        await Send(new Dictionary<string, object>
        {
            ["action"] = "attack",
            ["room_id"] = CurrentRoom,
            ["player_id"] = MyPlayerId,
            ["attacker_x"] = myPos.x,
            ["attacker_z"] = myPos.z,
            ["target_x"] = enemyPos.x,
            ["target_z"] = enemyPos.z
        });
    }

    public async void SendEndGame()
    {
        await Send(new Dictionary<string, object>
        {
            ["action"] = "end_game",
            ["room_id"] = CurrentRoom,
            ["player_id"] = MyPlayerId
        });
    }

    async System.Threading.Tasks.Task Send(Dictionary<string, object> obj)
    {
        if (ws == null || ws.State != WebSocketState.Open)
        {
            Debug.LogWarning("[NET] Not connected");
            return;
        }
        await ws.SendText(MiniJSON.Json.Serialize(obj));
    }

    // ── RECEIVE ───────────────────────────────────────────────

    void OnMessageReceived(byte[] bytes)
    {
        string raw = System.Text.Encoding.UTF8.GetString(bytes);
        Debug.Log($"[NET] ← {raw}");

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
                OnJoinedRoom?.Invoke(CurrentRoom, MyPlayerId, ParseStringList(data["players"]));
                break;

            case "player_joined":
                OnOtherPlayerJoined?.Invoke(data["player_id"].ToString());
                break;

            case "game_started":
                var plist = ParseStringList(data["players"]);
                string host = data["host_id"].ToString();
                var spawns = ParseSpawns(data["spawn_positions"]);
                OnGameStarted?.Invoke(host, plist, spawns);
                break;

            case "player_moved":
                OnPlayerMoved?.Invoke(
                    data["player_id"].ToString(),
                    new Vector3(ToFloat(data["x"]), ToFloat(data["y"]), ToFloat(data["z"])),
                    ToFloat(data["rot_y"])
                );
                break;

            case "hit":
                var scores = ParseIntDict(data["scores"]);
                bool killed = (bool)data["killed"];
                OnHit?.Invoke(
                    data["attacker"].ToString(),
                    data["victim"].ToString(),
                    ToInt(data["damage"]),
                    ToInt(data["victim_health"]),
                    scores,
                    killed
                );
                break;

            case "game_over":
                var finalScores = ParseIntDict(data["final_scores"]);
                OnGameOver?.Invoke(data["winner"].ToString(), finalScores);
                break;

            case "player_left":
                OnPlayerLeft?.Invoke(data["player_id"].ToString());
                break;

            case "error":
                OnError?.Invoke(data["message"].ToString());
                break;
        }
    }

    // ── Parse helpers ─────────────────────────────────────────

    List<string> ParseStringList(object raw)
    {
        var result = new List<string>();
        if (raw is List<object> list)
            foreach (var item in list) result.Add(item.ToString());
        return result;
    }

    Dictionary<string, Vector3> ParseSpawns(object raw)
    {
        var result = new Dictionary<string, Vector3>();
        if (raw is Dictionary<string, object> dict)
            foreach (var kv in dict)
            {
                var p = kv.Value as Dictionary<string, object>;
                if (p != null)
                    result[kv.Key] = new Vector3(ToFloat(p["x"]), ToFloat(p["y"]), ToFloat(p["z"]));
            }
        return result;
    }

    Dictionary<string, int> ParseIntDict(object raw)
    {
        var result = new Dictionary<string, int>();
        if (raw is Dictionary<string, object> dict)
            foreach (var kv in dict) result[kv.Key] = ToInt(kv.Value);
        return result;
    }

    float ToFloat(object o) => Convert.ToSingle(o);
    int ToInt(object o) => Convert.ToInt32(o);
}