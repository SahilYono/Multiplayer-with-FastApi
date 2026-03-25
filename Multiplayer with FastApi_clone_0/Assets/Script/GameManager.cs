using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Transform remotePlayer;
    public Text statusText;
    public Text scoreText;

    int myScore = 0;
    int theirScore = 0;

    // Target position for smooth movement
    Vector3 remoteTargetPos;
    Quaternion remoteTargetRot;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Initialize target to current position
        if (remotePlayer != null)
        {
            remoteTargetPos = remotePlayer.position;
            remoteTargetRot = remotePlayer.rotation;
        }
    }

    void Update()
    {
        // Smoothly move remote player every frame toward target
        // This makes movement look smooth even if updates come 20x/sec
        if (remotePlayer != null)
        {
            remotePlayer.position = Vector3.Lerp(
                remotePlayer.position, remoteTargetPos, Time.deltaTime * 15f);
            remotePlayer.rotation = Quaternion.Lerp(
                remotePlayer.rotation, remoteTargetRot, Time.deltaTime * 15f);
        }
    }

    public void HandleServerMessage(string json)
    {
        Debug.Log("[GameManager received]: " + json);

        if (json.Contains("\"type\":\"game_start\""))
        {
            SetStatus("GAME ON! WASD = move | Space = attack");
        }

        else if (json.Contains("\"type\":\"player_joined\""))
        {
            if (json.Contains("\"count\":2"))
                SetStatus("Both players in. Starting...");
            else
                SetStatus("Waiting for second player...");
        }

        else if (json.Contains("\"type\":\"player_moved\""))
        {
            // Parse the position block specifically
            // JSON looks like: ..."position":{"x":1.23,"y":1.0,"z":0.5},"rotation":{"y":45.0}
            int posStart = json.IndexOf("\"position\":{");
            int rotStart = json.IndexOf("\"rotation\":{");

            if (posStart >= 0 && remotePlayer != null)
            {
                // Get the substring just for position block: {"x":1.23,"y":1.0,"z":0.5}
                string posSub = json.Substring(posStart + 11); // skip "position":{
                float x = ParseKeyFloat(posSub, "\"x\":");
                float y = ParseKeyFloat(posSub, "\"y\":");
                float z = ParseKeyFloat(posSub, "\"z\":");

                remoteTargetPos = new Vector3(x, y, z);
            }

            if (rotStart >= 0 && remotePlayer != null)
            {
                string rotSub = json.Substring(rotStart + 11);
                float ry = ParseKeyFloat(rotSub, "\"y\":");
                remoteTargetRot = Quaternion.Euler(0, ry, 0);
            }
        }

        else if (json.Contains("\"type\":\"hit\""))
        {
            string me = NetworkManager.Instance.playerID;

            if (json.Contains("\"attacker\":\"" + me + "\""))
            {
                myScore++;
                SetStatus("YOU HIT THEM!");
            }
            else
            {
                theirScore++;
                SetStatus("You got hit!");
            }

            if (scoreText != null)
                scoreText.text = "You: " + myScore + "  |  Them: " + theirScore;
        }

        else if (json.Contains("\"type\":\"game_over\""))
        {
            string me = NetworkManager.Instance.playerID;
            if (json.Contains("\"winner\":\"" + me + "\""))
                SetStatus("=== YOU WIN! ===");
            else
                SetStatus("=== You lose. GG ===");
        }

        else if (json.Contains("\"type\":\"player_left\""))
        {
            SetStatus("Other player disconnected.");
        }

        else if (json.Contains("\"type\":\"error\""))
        {
            SetStatus("Error: Room is full!");
        }
    }

    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log("[Status] " + msg);
    }

    // Finds a float value right after a key in a substring
    // Much safer because you pass in the right section of JSON
    float ParseKeyFloat(string sub, string key)
    {
        int i = sub.IndexOf(key);
        if (i < 0) return 0f;

        int s = i + key.Length;
        int e = s;

        while (e < sub.Length &&
               (char.IsDigit(sub[e]) || sub[e] == '.' || sub[e] == '-'))
            e++;

        if (e > s && float.TryParse(sub.Substring(s, e - s),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float val))
            return val;

        return 0f;
    }
}