// NetworkManager.cs
// Attach this to an empty GameObject called "NetworkManager" in the scene.
// This is the single point of contact with the FastAPI server.

using System;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;          // the package we installed

public class NetworkManager : MonoBehaviour
{
    // Static instance so ANY script can call NetworkManager.Instance.Send(...)
    public static NetworkManager Instance;

    WebSocket ws;               // the actual WebSocket connection object

    // These are set from the UI before connecting
    [HideInInspector] public string serverIP = "192.168.1.5";   // your PC's local IP
    [HideInInspector] public string roomID = "ROOM42";
    [HideInInspector] public string playerID = "player1";       // unique per device

    // Other scripts subscribe to these events to react to server messages
    public event Action<Dictionary<string, object>> OnMessageReceived;

    void Awake()
    {
        // Singleton pattern: only one NetworkManager should exist
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Called from UIManager when the player clicks "Connect"
    public async void Connect(string ip, string room, string pid)
    {
        serverIP = ip;
        roomID = room;
        playerID = pid;

        // Build the WebSocket URL.
        // Format: ws://SERVER_IP:8000/ws/ROOM_ID/PLAYER_ID
        string url = $"ws://{serverIP}:8000/ws/{roomID}/{playerID}";
        Debug.Log($"Connecting to: {url}");

        ws = new WebSocket(url);

        // ── EVENT: Connection opened ──────────────────────────────
        ws.OnOpen += () => {
            Debug.Log("WebSocket connected!");
        };

        // ── EVENT: Message received from server ───────────────────
        // All game logic reacts here — positions, hits, scores, game state.
        ws.OnMessage += (bytes) => {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log($"Server says: {json}");

            // Parse JSON → Dictionary
            var msg = JsonUtility.FromJson<ServerMessage>(json);

            // Convert to generic dict so GameManager can read any field
            var data = new Dictionary<string, object> {
                { "type", msg.type }
            };
            // Fire the event — GameManager listens to this
            OnMessageReceived?.Invoke(data);

            // Better: use a full JSON parser (MiniJSON or Newtonsoft)
            // For simplicity here we pass the raw JSON to GameManager too
            GameManager.Instance.HandleServerMessage(json);
        };

        // ── EVENT: Connection closed ──────────────────────────────
        ws.OnClose += (code) => {
            Debug.Log($"WebSocket closed: {code}");
        };

        // ── EVENT: Error ──────────────────────────────────────────
        ws.OnError += (err) => {
            Debug.LogError($"WebSocket error: {err}");
        };

        // Actually open the connection
        await ws.Connect();
    }

    // Call this every frame — NativeWebSocket needs this to dispatch messages
    // on the Unity main thread (otherwise you can't touch GameObjects safely)
    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    // ── SEND HELPERS ──────────────────────────────────────────────
    // PlayerController calls these to tell the server what happened.

    public async void SendMove(Vector3 position, float rotY)
    {
        if (ws == null || ws.State != WebSocketState.Open) return;

        // Build the message as a simple JSON string
        string msg = $"{{" +
            $"\"type\":\"move\"," +
            $"\"position\":{{\"x\":{position.x:F3},\"y\":{position.y:F3},\"z\":{position.z:F3}}}," +
            $"\"rotation\":{{\"y\":{rotY:F3}}}" +
            $"}}";

        await ws.SendText(msg);
    }

    public async void SendAttack()
    {
        if (ws == null || ws.State != WebSocketState.Open) return;
        await ws.SendText("{\"type\":\"attack\"}");
    }

    // Close connection cleanly when the app quits
    async void OnApplicationQuit()
    {
        if (ws != null) await ws.Close();
    }
}

// Simple class to deserialize just the "type" field of server messages
[Serializable]
public class ServerMessage
{
    public string type;
}