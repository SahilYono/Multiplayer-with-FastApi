/*
 * GameManager.cs  (v2 — with HUD, Hit Detection, Scores, Health, Kill Feed)
 * ───────────────────────────────────────────────────────────────────────────
 * WHAT'S NEW:
 *   - PanelGame starts INACTIVE (no more button blocking)
 *   - Camera NOT touched — you place it yourself in scene
 *   - Space bar = Attack (server-authoritative hit detection)
 *   - Score, Health, Kill Feed, End Game flow
 *   - PanelGameOver with Play Again / Home
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // ═══════════════════════════════════════════════════════════
    //  INSPECTOR REFERENCES
    // ═══════════════════════════════════════════════════════════

    [Header("── Panels ──")]
    public GameObject panelMain;
    public GameObject panelLobby;
    public GameObject panelJoin;
    public GameObject panelGame;      // MUST BE INACTIVE in Inspector
    public GameObject panelGameOver;  // MUST BE INACTIVE in Inspector

    [Header("── PanelMain ──")]
    public Button btnCreateRoom;
    public Button btnGoToJoin;

    [Header("── PanelLobby ──")]
    public TMP_Text txtRoomId;
    public TMP_Text txtWaiting;
    public TMP_Text txtMyPlayerId;
    public Button btnStartGame;     // hidden until P2 joins

    [Header("── PanelJoin ──")]
    public TMP_InputField inputRoomId;
    public Button btnJoinRoom;
    public Button btnBackToMain;
    public TMP_Text txtJoinStatus;

    [Header("── PanelGame HUD ──")]
    public TMP_Text txtMyScore;
    public TMP_Text txtEnemyScore;
    public TMP_Text txtMyHealth;
    public TMP_Text txtEnemyHealth;
    public TMP_Text txtKillFeed;
    public Button btnAttack;
    public Button btnEndGame;       // hidden by default

    [Header("── PanelGameOver ──")]
    public TMP_Text txtGameOverTitle;
    public Button btnPlayAgain;

    [Header("── Prefabs ──")]
    public GameObject localPlayerPrefab;
    public GameObject remotePlayerPrefab;

    // ═══════════════════════════════════════════════════════════
    //  GAME STATE
    // ═══════════════════════════════════════════════════════════

    private GameObject localPlayerObj;
    private Dictionary<string, GameObject> remotePlayers = new Dictionary<string, GameObject>();

    private int myScore = 0;
    private int enemyScore = 0;
    private int myHealth = 100;
    private int enemyHealth = 100;

    private const int MAX_SCORE = 5;    // first to 5 kills wins
    private const int HIT_DAMAGE = 25;   // each hit = 25 damage
    private const float ATTACK_RANGE = 3f;

    private bool gameActive = false;
    private Coroutine killFeedCoroutine;

    // ═══════════════════════════════════════════════════════════
    //  INIT
    // ═══════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Make sure game panels are off
        panelGame.SetActive(false);
        panelGameOver.SetActive(false);
        btnStartGame.gameObject.SetActive(false);
        btnEndGame.gameObject.SetActive(false);

        ShowPanel(panelMain);

        // Button listeners
        btnCreateRoom.onClick.AddListener(OnClickCreate);
        btnGoToJoin.onClick.AddListener(() => ShowPanel(panelJoin));
        btnJoinRoom.onClick.AddListener(OnClickJoin);
        btnBackToMain.onClick.AddListener(() => ShowPanel(panelMain));
        btnStartGame.onClick.AddListener(OnClickStart);
        btnAttack.onClick.AddListener(OnClickAttack);
        btnEndGame.onClick.AddListener(OnClickEndGame);
        btnPlayAgain.onClick.AddListener(OnClickPlayAgain);

        // Network events
        var net = NetworkManager.Instance;
        net.OnRoomCreated += HandleRoomCreated;
        net.OnJoinedRoom += HandleJoinedRoom;
        net.OnOtherPlayerJoined += HandleOtherPlayerJoined;
        net.OnGameStarted += HandleGameStarted;
        net.OnPlayerMoved += HandlePlayerMoved;
        net.OnPlayerLeft += HandlePlayerLeft;
        net.OnHit += HandleHit;
        net.OnGameOver += HandleGameOver;
        net.OnError += HandleError;
    }

    void Update()
    {
        if (!gameActive) return;

        // Space bar = attack (keyboard shortcut in addition to button)
        if (Input.GetKeyDown(KeyCode.Space))
            OnClickAttack();
    }

    // ═══════════════════════════════════════════════════════════
    //  BUTTON HANDLERS
    // ═══════════════════════════════════════════════════════════

    void OnClickCreate()
    {
        txtWaiting.text = "Creating room...";
        ShowPanel(panelLobby);
        btnStartGame.gameObject.SetActive(false);
        NetworkManager.Instance.CreateRoom();
    }

    void OnClickJoin()
    {
        string id = inputRoomId.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(id)) { txtJoinStatus.text = "Enter a room ID"; return; }
        txtJoinStatus.text = "Joining...";
        NetworkManager.Instance.JoinRoom(id);
    }

    void OnClickStart()
    {
        NetworkManager.Instance.StartGame();
    }

    void OnClickAttack()
    {
        if (!gameActive || localPlayerObj == null) return;
        NetworkManager.Instance.SendAttack();
        // Small visual flash on button
        StartCoroutine(FlashAttackButton());
    }

    void OnClickEndGame()
    {
        // Only host can force-end
        if (NetworkManager.Instance.IsHost)
            NetworkManager.Instance.SendEndGame();
    }

    void OnClickPlayAgain()
    {
        // Reset everything and go back to main menu
        ResetGameState();
        panelGameOver.SetActive(false);
        ShowPanel(panelMain);
    }

    // ═══════════════════════════════════════════════════════════
    //  NETWORK EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════

    void HandleRoomCreated(string roomId)
    {
        txtRoomId.text = $"Room ID:  <b>{roomId}</b>";
        txtWaiting.text = "Waiting for second player...";
        txtMyPlayerId.text = $"You: {NetworkManager.Instance.MyPlayerId}";
        btnStartGame.gameObject.SetActive(false);
        ShowPanel(panelLobby);
    }

    void HandleJoinedRoom(string roomId, string myId, List<string> players)
    {
        txtRoomId.text = $"Room ID:  <b>{roomId}</b>";
        txtWaiting.text = "Joined! Waiting for host to start...";
        txtMyPlayerId.text = $"You: {myId}";
        btnStartGame.gameObject.SetActive(false);
        ShowPanel(panelLobby);
    }

    void HandleOtherPlayerJoined(string playerId)
    {
        txtWaiting.text = $"<color=#00ff88>Player 2 connected!</color>\nReady to start.";
        if (NetworkManager.Instance.IsHost)
            btnStartGame.gameObject.SetActive(true);
    }

    void HandleGameStarted(string hostId, List<string> players, Dictionary<string, Vector3> spawns)
    {
        // Hide ALL menus
        panelMain.SetActive(false);
        panelLobby.SetActive(false);
        panelJoin.SetActive(false);
        panelGameOver.SetActive(false);

        // Show HUD
        panelGame.SetActive(true);

        // Host gets end game button
        btnEndGame.gameObject.SetActive(NetworkManager.Instance.IsHost);

        ResetGameState();
        gameActive = true;

        string myId = NetworkManager.Instance.MyPlayerId;

        // Destroy old player objects if any (play again case)
        if (localPlayerObj != null) Destroy(localPlayerObj);
        foreach (var kv in remotePlayers) Destroy(kv.Value);
        remotePlayers.Clear();

        // Spawn players
        foreach (string pid in players)
        {
            Vector3 spawnPos = spawns.ContainsKey(pid) ? spawns[pid] : Vector3.zero;

            if (pid == myId)
            {
                localPlayerObj = Instantiate(localPlayerPrefab, spawnPos, Quaternion.identity);
                localPlayerObj.name = "LocalPlayer";
                var pc = localPlayerObj.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = true;
            }
            else
            {
                var remoteObj = Instantiate(remotePlayerPrefab, spawnPos, Quaternion.identity);
                remoteObj.name = $"RemotePlayer_{pid}";
                remotePlayers[pid] = remoteObj;
            }
        }

        UpdateHUD();
        ShowKillFeed("<color=#ffff00>⚔ GAME STARTED — FIGHT!</color>", 3f);
    }

    void HandlePlayerMoved(string playerId, Vector3 pos, float rotY)
    {
        if (remotePlayers.ContainsKey(playerId))
            remotePlayers[playerId].GetComponent<RemotePlayer>().SetTarget(pos, rotY);
    }

    void HandlePlayerLeft(string playerId)
    {
        if (remotePlayers.ContainsKey(playerId))
        {
            Destroy(remotePlayers[playerId]);
            remotePlayers.Remove(playerId);
        }
        ShowKillFeed("<color=#ff4444>Enemy disconnected.</color>", 4f);
        gameActive = false;
    }

    // Server says someone got hit
    // payload: { attacker, victim, damage, victim_health, scores:{p1:x, p2:y}, killed:bool }
    void HandleHit(string attacker, string victim, int damage, int victimHealth,
                   Dictionary<string, int> scores, bool killed)
    {
        string myId = NetworkManager.Instance.MyPlayerId;

        // Update health
        if (victim == myId)
        {
            myHealth = victimHealth;
            ShowKillFeed($"<color=#ff4444>You took {damage} damage!</color>", 2f);
            StartCoroutine(FlashDamage());
        }
        else
        {
            enemyHealth = victimHealth;
            ShowKillFeed($"<color=#00ff88>Hit! Enemy took {damage} damage!</color>", 2f);
        }

        // Update scores if someone died
        if (killed)
        {
            if (victim == myId)
            {
                // I died — respawn with full health after 2 sec
                myHealth = 100;
                enemyScore++;
                ShowKillFeed("<color=#ff4444>💀 YOU WERE KILLED</color>", 3f);
                StartCoroutine(RespawnLocal());
            }
            else
            {
                enemyHealth = 100;
                myScore++;
                ShowKillFeed("<color=#00ff88>🏆 ENEMY ELIMINATED!</color>", 3f);
            }
        }

        UpdateHUD();
    }

    void HandleGameOver(string winner, Dictionary<string, int> finalScores)
    {
        gameActive = false;
        panelGame.SetActive(false);
        panelGameOver.SetActive(true);

        string myId = NetworkManager.Instance.MyPlayerId;
        bool iWon = winner == myId;

        txtGameOverTitle.text = iWon
            ? "<color=#FFD700>🏆 YOU WIN!</color>"
            : "<color=#FF4444>💀 YOU LOSE</color>";

        // Clean up players
        if (localPlayerObj != null) Destroy(localPlayerObj);
        foreach (var kv in remotePlayers) Destroy(kv.Value);
        remotePlayers.Clear();
    }

    void HandleError(string message)
    {
        txtJoinStatus.text = $"<color=#ff4444>Error: {message}</color>";
        Debug.LogWarning($"[GAME] Server error: {message}");
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC — called by PlayerController to get attack range
    // ═══════════════════════════════════════════════════════════

    public float GetAttackRange() => ATTACK_RANGE;

    public Vector3 GetLocalPlayerPosition()
    {
        if (localPlayerObj != null) return localPlayerObj.transform.position;
        return Vector3.zero;
    }

    public Vector3 GetRemotePlayerPosition()
    {
        foreach (var kv in remotePlayers)
            return kv.Value.transform.position;
        return Vector3.one * 9999f;
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════

    void UpdateHUD()
    {
        txtMyScore.text = $"Score: {myScore}";
        txtEnemyScore.text = $"Enemy: {enemyScore}";
        txtMyHealth.text = $"HP: {myHealth}";
        txtEnemyHealth.text = $"Enemy HP: {enemyHealth}";
    }

    void ShowKillFeed(string message, float duration)
    {
        if (killFeedCoroutine != null) StopCoroutine(killFeedCoroutine);
        killFeedCoroutine = StartCoroutine(KillFeedRoutine(message, duration));
    }

    IEnumerator KillFeedRoutine(string message, float duration)
    {
        txtKillFeed.text = message;
        yield return new WaitForSeconds(duration);
        txtKillFeed.text = "";
    }

    IEnumerator FlashAttackButton()
    {
        var colors = btnAttack.colors;
        var orig = colors.normalColor;
        colors.normalColor = Color.yellow;
        btnAttack.colors = colors;
        yield return new WaitForSeconds(0.15f);
        colors.normalColor = orig;
        btnAttack.colors = colors;
    }

    IEnumerator FlashDamage()
    {
        // Flash health text red briefly
        txtMyHealth.color = Color.red;
        yield return new WaitForSeconds(0.3f);
        txtMyHealth.color = Color.white;
    }

    IEnumerator RespawnLocal()
    {
        if (localPlayerObj != null)
        {
            // Briefly disable movement during respawn
            var pc = localPlayerObj.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;
            yield return new WaitForSeconds(1.5f);
            // Respawn at starting position
            localPlayerObj.transform.position = new Vector3(-2f, 0f, 0f);
            if (pc != null) pc.enabled = true;
        }
    }

    void ResetGameState()
    {
        myScore = 0;
        enemyScore = 0;
        myHealth = 100;
        enemyHealth = 100;
        gameActive = false;
        UpdateHUD();
    }

    void ShowPanel(GameObject panel)
    {
        panelMain.SetActive(false);
        panelLobby.SetActive(false);
        panelJoin.SetActive(false);
        panel.SetActive(true);
    }
}