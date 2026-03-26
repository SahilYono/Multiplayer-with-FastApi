// NetworkManager.cs
// Attach to an empty GameObject called "NetworkManager"
// Handles ALL WebSocket communication with the FastAPI server.
// Supports TWO instances running in the same scene (Player1 + Player2)

using System;
using UnityEngine;
using NativeWebSocket;

public class NetworkManager : MonoBehaviour
{
    // ── Which player is this instance? Set in Inspector ──────────
    // 0 = Player 1 (left side)   1 = Player 2 (right side)
    public int playerIndex = 0;

    // ── Static references so other scripts can find each instance ─
    public static NetworkManager Player1;
    public static NetworkManager Player2;

    // ── Connection state (read by UIManager / GameManager) ────────
    [HideInInspector] public string playerID = "";
    [HideInInspector] public bool connected = false;
    [HideInInspector] public string statusMsg = "Not connected";

    // ── The WebSocket connection ───────────────────────────────────
    WebSocket ws;

    // ── GameManager that belongs to THIS player ────────────────────
    // Drag the correct GameManager in Inspector
    public GameManager myGameManager;

    void Awake()
    {
        // Register as Player1 or Player2 static reference
        if (playerIndex == 0) Player1 = this;
        else Player2 = this;
    }

    // Called by UIManager when the player clicks Connect
    public async void Connect(string ip, string room, string pid)
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            Debug.LogWarning($"[NM{playerIndex}] Already connected!");
            return;
        }

        playerID = pid;
        connected = false;
        statusMsg = "Connecting...";

        string url = $"ws://{ip}:8000/ws/{room}/{pid}";
        Debug.Log($"[NM{playerIndex}] Connecting to: {url}");

        ws = new WebSocket(url);

        ws.OnOpen += () =>
        {
            connected = true;
            statusMsg = "Connected — waiting for opponent...";
            Debug.Log($"[NM{playerIndex}] WebSocket OPEN");
        };

        ws.OnMessage += (bytes) =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log($"[NM{playerIndex}] MSG: {json}");
            // Forward to THIS player's GameManager
            if (myGameManager != null)
                myGameManager.HandleServerMessage(json);
        };

        ws.OnError += (err) =>
        {
            statusMsg = "Error: " + err;
            Debug.LogError($"[NM{playerIndex}] ERROR: {err}");
        };

        ws.OnClose += (code) =>
        {
            connected = false;
            statusMsg = "Disconnected (" + code + ")";
            Debug.Log($"[NM{playerIndex}] CLOSED: {code}");
        };

        await ws.Connect();
    }

    // Unity calls this every frame — NativeWebSocket NEEDS this
    // to safely deliver messages on the main thread
    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    // ── SEND: player moved ─────────────────────────────────────────
    public async void SendMove(Vector3 pos, float rotY)
    {
        if (!IsReady()) return;

        // Use InvariantCulture so decimals always use "." not "," (locale safe)
        string px = pos.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        string py = pos.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        string pz = pos.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        string ry = rotY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        string msg = "{\"type\":\"move\","
                   + "\"position\":{\"x\":" + px + ",\"y\":" + py + ",\"z\":" + pz + "},"
                   + "\"rotation\":{\"y\":" + ry + "}}";

        await ws.SendText(msg);
    }

    // ── SEND: player attacked ──────────────────────────────────────
    public async void SendAttack()
    {
        if (!IsReady()) return;
        Debug.Log($"[NM{playerIndex}] Sending attack");
        await ws.SendText("{\"type\":\"attack\"}");
    }

    // ── SEND: restart request ──────────────────────────────────────
    public async void SendRestart()
    {
        if (!IsReady()) return;
        await ws.SendText("{\"type\":\"restart\"}");
    }

    bool IsReady()
    {
        return ws != null && ws.State == WebSocketState.Open;
    }

    async void OnApplicationQuit()
    {
        if (ws != null && ws.State == WebSocketState.Open)
            await ws.Close();
    }

    async void OnDestroy()
    {
        if (ws != null && ws.State == WebSocketState.Open)
            await ws.Close();
    }
}