// GameManager.cs
// ONE instance per player.
// GameManager1 handles messages for Player 1.
// GameManager2 handles messages for Player 2.
//
// Attach to an empty GameObject. Wire up references in Inspector.

using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // ── Set in Inspector ──────────────────────────────────────────
    public int playerIndex = 0;   // 0 = Player1's GM,  1 = Player2's GM

    [Header("This player's objects")]
    public PlayerController myPlayerController;   // LocalPlayer1 or LocalPlayer2
    public Transform remotePlayerTransform; // the OTHER player's capsule

    [Header("This player's UI")]
    public Text statusText;   // shows game status
    public Text scoreText;    // shows scores
    public Text controlsText; // shows control hint

    // ── Internal state ─────────────────────────────────────────────
    string myID = "";
    int myScore = 0;
    int theirScore = 0;

    void Start()
    {
        // Show the correct controls hint
        if (controlsText != null)
        {
            if (playerIndex == 0)
                controlsText.text = "P1: WASD move | Space attack";
            else
                controlsText.text = "P2: Arrows move | Enter attack";
        }

        SetStatus("Enter details and click Connect");
        UpdateScoreDisplay();
    }

    // ── Called by NetworkManager when a message arrives ────────────
    public void HandleServerMessage(string json)
    {
        // We manually check what type of message it is
        // and act accordingly. No external JSON library needed.

        string type = ExtractString(json, "\"type\":\"", "\"");

        switch (type)
        {
            case "game_start":
                myID = ExtractString(json, "\"your_id\":\"", "\"");
                myScore = 0;
                theirScore = 0;
                UpdateScoreDisplay();
                SetStatus("GAME ON! First to 5 hits wins!");
                // Tell PlayerController it can start
                if (myPlayerController != null)
                    myPlayerController.SetGameActive(true);
                break;

            case "player_joined":
                string count = ExtractString(json, "\"count\":", ",");
                if (count == "") count = ExtractString(json, "\"count\":", "}");
                if (count.Trim() == "1")
                    SetStatus("Waiting for second player...");
                else
                    SetStatus("Both players joined!");
                break;

            case "player_moved":
                // Move the remote player's capsule
                float x = ExtractFloat(json, "\"x\":");
                float y = ExtractFloat(json, "\"y\":");
                float z = ExtractFloat(json, "\"z\":");
                float ry = ExtractFloat(json, "\"rotation\":{\"y\":");

                if (remotePlayerTransform != null)
                {
                    // Smooth interpolation so movement doesn't look jerky
                    remotePlayerTransform.position = Vector3.Lerp(
                        remotePlayerTransform.position,
                        new Vector3(x, y, z),
                        0.6f
                    );
                    remotePlayerTransform.rotation = Quaternion.Lerp(
                        remotePlayerTransform.rotation,
                        Quaternion.Euler(0, ry, 0),
                        0.6f
                    );
                }
                break;

            case "hit":
                string attacker = ExtractString(json, "\"attacker\":\"", "\"");
                string victim = ExtractString(json, "\"victim\":\"", "\"");

                if (attacker == myID)
                {
                    myScore++;
                    SetStatus("YOUR HIT! +" + myScore);
                    FlashStatus(Color.green);
                }
                else if (victim == myID)
                {
                    theirScore++;
                    SetStatus("Got hit! Dodge!");
                    FlashStatus(Color.red);
                }

                UpdateScoreDisplay();
                break;

            case "miss":
                float dist = ExtractFloat(json, "\"dist\":");
                SetStatus($"Miss! Too far ({dist:F1} units). Get closer!");
                break;

            case "game_over":
                string winner = ExtractString(json, "\"winner\":\"", "\"");
                if (winner == myID)
                    SetStatus("=== YOU WIN! === Press R to restart");
                else
                    SetStatus("=== You lose === Press R to restart");

                if (myPlayerController != null)
                    myPlayerController.SetGameActive(false);
                break;

            case "game_restart":
                myScore = 0;
                theirScore = 0;
                UpdateScoreDisplay();
                SetStatus("RESTARTED! WASD move, Space/Enter attack");
                if (myPlayerController != null)
                    myPlayerController.SetGameActive(true);
                break;

            case "player_left":
                SetStatus("Other player left the game.");
                if (myPlayerController != null)
                    myPlayerController.SetGameActive(false);
                break;

            case "error":
                string errMsg = ExtractString(json, "\"msg\":\"", "\"");
                SetStatus("Error: " + errMsg);
                break;

            default:
                Debug.Log($"[GM{playerIndex}] Unknown message type: {type}");
                break;
        }
    }

    // ── Restart key ────────────────────────────────────────────────
    void Update()
    {
        // Player 1 presses R to request restart
        if (playerIndex == 0 && Input.GetKeyDown(KeyCode.R))
        {
            if (NetworkManager.Player1 != null)
                NetworkManager.Player1.SendRestart();
        }
    }

    // ── UI helpers ─────────────────────────────────────────────────
    void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log($"[GM{playerIndex}] {msg}");
    }

    void UpdateScoreDisplay()
    {
        if (scoreText != null)
            scoreText.text = $"You: {myScore}  |  Them: {theirScore}";
    }

    Coroutine flashCoroutine;
    void FlashStatus(Color col)
    {
        if (statusText == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(DoFlash(col));
    }

    System.Collections.IEnumerator DoFlash(Color col)
    {
        statusText.color = col;
        yield return new WaitForSeconds(0.4f);
        statusText.color = Color.white;
    }

    // ── JSON helpers ───────────────────────────────────────────────
    // Extracts a string value from JSON: finds key, reads until endChar
    string ExtractString(string json, string key, string endChar)
    {
        int i = json.IndexOf(key);
        if (i < 0) return "";
        int start = i + key.Length;
        int end = json.IndexOf(endChar, start);
        if (end < 0) return json.Substring(start);
        return json.Substring(start, end - start);
    }

    // Extracts a float value from JSON: finds key, reads numeric chars
    float ExtractFloat(string json, string key)
    {
        int i = json.IndexOf(key);
        if (i < 0) return 0f;
        int s = i + key.Length;
        int e = s;
        while (e < json.Length &&
               (char.IsDigit(json[e]) || json[e] == '.' || json[e] == '-'))
            e++;
        return float.TryParse(
            json.Substring(s, e - s),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float v) ? v : 0f;
    }
}