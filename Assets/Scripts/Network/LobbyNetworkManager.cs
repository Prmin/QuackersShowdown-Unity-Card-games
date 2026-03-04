using Mirror;
using UnityEngine;
using Mirror.Discovery;
using UnityEngine.SceneManagement;
using System.Collections;

public class LobbyNetworkManager : NetworkRoomManager
{
    [Header("Player Limits")]
    [Range(2, 6)] public int maxPlayersAllowed = 6;

    [Header("Discovery (optional)")]
    public MyNetworkDiscovery discovery;

    [Header("UI Flow")]
    [SerializeField] private string lobbySceneName = "LobbyTutorial_Done";

    [Header("Gameplay Disconnect Policy")]
    [SerializeField] private bool returnToLobbyWhenAnyPlayerDisconnectsInGameplay = true;
    [SerializeField] private float staleGameplaySyncTimeoutSeconds = 8f;

    private bool _pendingShowLobbyListAfterDisconnect;
    private bool _watchGameplayDisconnect;
    private bool _disconnectRecoveryTriggered;
    private bool _hasSeenLiveTurnState;
    private float _enteredGameplayAt;
    private int _maxSeenGameplayPlayers;
    private int _lastObservedTurnIndex = int.MinValue;
    private int _lastObservedTurnRemaining = int.MinValue;
    private uint _lastObservedTurnNetId = uint.MaxValue;
    private int _lastObservedTurnOrderCount = int.MinValue;
    private float _lastTurnStateObservedAt;

    public override void OnStartHost()
    {
        base.OnStartHost();
        if (discovery)
        {
            discovery.AdvertiseServer();
            ;
        }
        else
        {
            Debug.LogWarning("[Discovery] No NetworkDiscovery assigned on LobbyNetworkManager.");
        }
    }

    public override GameObject OnRoomServerCreateGamePlayer(NetworkConnectionToClient conn, GameObject roomPlayer)
    {
        Transform start = GetStartPosition()?.transform;
        Vector3 pos = start ? start.position : Vector3.zero;
        Quaternion rot = start ? start.rotation : Quaternion.identity;

        // ✅ ใช้ playerPrefab (รองรับทุกเวอร์ชันของ Mirror)
        // ตั้งใน Inspector ของ LobbyNetworkManager: Player Prefab = Gameplay Player (มี PlayerManager)
        GameObject gamePlayer = Instantiate(this.playerPrefab, pos, rot);

        // คัดลอก "สี" จาก RoomPlayer → GamePlayer
        var rp = roomPlayer.GetComponent<LobbyRoomPlayer>();
        var pm = gamePlayer.GetComponent<PlayerManager>();
        if (rp != null && pm != null)
        {
            pm.duckColorIndex = rp.duckColorIndex;
            pm.SetDisplayName(rp.displayName);
            pm.SetProfileAvatarIndex(rp.profileAvatarIndex);
            // ถ้าต้องการก๊อปชื่อด้วยก็ทำที่นี่ เช่น:
        }

        return gamePlayer; // Mirror จะ spawn และ sync vars ให้เอง
    }

    // ====== เพิ่ม helper ปิด UI ทั้งหมดของเมนู ======
    void HideAllMenuUI()
    {
        UIFlow flow = UIFlow.I;
        if (flow == null)
            return;

        // Use UIFlow internal safe path (EnsureRefs + null-safe checks)
        // instead of touching serialized panel refs directly from here.
        flow.HideAllForGameplay();
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        // ซีนปัจจุบันชื่ออะไร
        Scene active = SceneManager.GetActiveScene();
        if (IsSceneMatch(active, GameplayScene))
        {
            _watchGameplayDisconnect = true;
            _disconnectRecoveryTriggered = false;
            _hasSeenLiveTurnState = false;
            _enteredGameplayAt = Time.unscaledTime;
            _maxSeenGameplayPlayers = 0;
            _lastObservedTurnIndex = int.MinValue;
            _lastObservedTurnRemaining = int.MinValue;
            _lastObservedTurnNetId = uint.MaxValue;
            _lastObservedTurnOrderCount = int.MinValue;
            _lastTurnStateObservedAt = Time.unscaledTime;
            HideAllMenuUI();
            return;
        }

        _watchGameplayDisconnect = false;
        _disconnectRecoveryTriggered = false;
        _hasSeenLiveTurnState = false;
        _enteredGameplayAt = 0f;
        _maxSeenGameplayPlayers = 0;
    }

    public override void OnStopHost()
    {
        if (discovery)
        {
            discovery.StopDiscovery();
            ;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded_ShowLobbyListAfterDisconnect;
        _pendingShowLobbyListAfterDisconnect = false;
        _watchGameplayDisconnect = false;
        _disconnectRecoveryTriggered = false;
        _hasSeenLiveTurnState = false;
        _enteredGameplayAt = 0f;
        _maxSeenGameplayPlayers = 0;
        base.OnStopHost();
    }

    public override void Awake()
    {
        base.Awake();
        minPlayers = 2;
        maxConnections = Mathf.Clamp(maxConnections, 1, maxPlayersAllowed);
    }

    public override void OnRoomServerConnect(NetworkConnectionToClient conn)
    {
        if (numPlayers >= maxConnections) { conn.Disconnect(); return; }
        base.OnRoomServerConnect(conn);
    }

    public override void OnRoomServerPlayersReady() { /* no auto-start */ }

    public bool CanStartGameNow(out string reason)
    {
        if (numPlayers < minPlayers)
        {
            reason = $"ต้องการอย่างน้อย {minPlayers} คน (ปัจจุบัน {numPlayers})";
            return false;
        }

        foreach (var rp in roomSlots)
        {
            if (rp == null) continue;

            // ข้ามโฮสต์: โฮสต์ถือว่า Ready เสมอ
            if (rp.connectionToClient == NetworkServer.localConnection)
                continue;

            if (!rp.readyToBegin)
            {
                reason = "ยังมีผู้เล่นที่ไม่ Ready";
                return false;
            }
        }

        reason = null;
        return true;
    }

    public bool CanStartGameNow() => CanStartGameNow(out _);


    [Server]
    public void StartGameIfReady()
    {
        if (CanStartGameNow(out var reason))
        {
            // ✅ ปิด Lobby UI สำหรับทุกเครื่องไว้ก่อน (โฮสต์เครื่องตัวเองเห็นผลทันที)
            HideAllMenuUI();

            ServerChangeScene(GameplayScene);
        }
        else
        {
            ;
        }
    }

    public override bool OnRoomServerSceneLoadedForPlayer(
        NetworkConnectionToClient conn, GameObject roomPlayer, GameObject gamePlayer)
    {
        bool result = base.OnRoomServerSceneLoadedForPlayer(conn, roomPlayer, gamePlayer);

        var lp = roomPlayer ? roomPlayer.GetComponent<LobbyRoomPlayer>() : null;
        var pm = gamePlayer ? gamePlayer.GetComponent<PlayerManager>() : null;

        if (lp != null && pm != null)
        {
            pm.SetDisplayName(lp.displayName);
            pm.SetProfileAvatarIndex(lp.profileAvatarIndex);
        }

        return result;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        // กลับหน้า LobbyList แล้วเริ่มสแกนใหม่
        HandleDisconnectedClientUIFlow();
        // ;
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        HandleDisconnectedClientUIFlow();
    }

    public override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_ShowLobbyListAfterDisconnect;
        _pendingShowLobbyListAfterDisconnect = false;
        _watchGameplayDisconnect = false;
        _disconnectRecoveryTriggered = false;
        _hasSeenLiveTurnState = false;
        _enteredGameplayAt = 0f;
        _maxSeenGameplayPlayers = 0;
        base.OnDestroy();
    }

    [ClientCallback]
    private void Update()
    {
        // Host-side has its own lifecycle and should not run this client recovery.
        if (NetworkServer.active)
            return;

        // Hard fallback: whenever client is disconnected, force return to lobby scene.
        if (IsClientDisconnected())
        {
            Scene activeNow = SceneManager.GetActiveScene();
            string lobbyTarget = ResolveLobbySceneName();
            if (!IsSceneMatch(activeNow, lobbyTarget))
            {
                _disconnectRecoveryTriggered = true;
                _watchGameplayDisconnect = false;
                RequestShowLobbyListAfterDisconnect();
                SceneManager.LoadScene(lobbyTarget);
                return;
            }

            if (!_disconnectRecoveryTriggered)
            {
                _disconnectRecoveryTriggered = true;
                StartCoroutine(CoEnsureLobbyListVisible());
            }
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        bool inGameplay = IsSceneMatch(active, GameplayScene) ||
                          string.Equals(active.name, "GamePlayScene", System.StringComparison.OrdinalIgnoreCase);
        if (!inGameplay)
        {
            _watchGameplayDisconnect = false;
            return;
        }

        // Arm watchdog even when OnClientSceneChanged is not fired as expected.
        _watchGameplayDisconnect = true;

        if (_disconnectRecoveryTriggered)
            return;

        int playersInGameplay = CountGameplayPlayers();
        if (playersInGameplay > _maxSeenGameplayPlayers)
            _maxSeenGameplayPlayers = playersInGameplay;

        if (playersInGameplay <= 0 &&
            (_maxSeenGameplayPlayers > 0 || _hasSeenLiveTurnState) &&
            _enteredGameplayAt > 0f &&
            Time.unscaledTime - _enteredGameplayAt >= 5f)
        {
            ForceReturnToLobbyClient("NoPlayersInGameplay");
            return;
        }

        TurnManager tm = TurnManager.Instance;
        if (tm != null)
        {
            if (tm.currentTurnNetId != 0 || tm.TurnOrder.Count >= 2)
                _hasSeenLiveTurnState = true;

            if (HasTurnStateChanged(tm))
                _lastTurnStateObservedAt = Time.unscaledTime;
        }

        if (_hasSeenLiveTurnState && _maxSeenGameplayPlayers >= 2 && playersInGameplay > 0 && playersInGameplay < _maxSeenGameplayPlayers)
        {
            ForceReturnToLobbyClient("PlayerCountDropped");
            return;
        }

        if (_hasSeenLiveTurnState && tm != null && !tm.isMatchEnded)
        {
            float staleAfter = Mathf.Max(3f, staleGameplaySyncTimeoutSeconds);
            if (Time.unscaledTime - _lastTurnStateObservedAt >= staleAfter)
            {
                ForceReturnToLobbyClient("StaleGameplaySync");
                return;
            }
        }

        bool disconnected = IsClientDisconnected();
        if (!disconnected)
            return;

        ForceReturnToLobbyClient("Disconnected");
    }

    public override void OnRoomServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn == null)
            return;

        string lobbyTarget = ResolveLobbySceneName();

        // Guard against duplicate calls while scene transition to lobby is already running.
        if (NetworkServer.isLoadingScene &&
            string.Equals(NetworkManager.networkSceneName, lobbyTarget, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!IsSceneMatch(SceneManager.GetActiveScene(), GameplayScene))
            return;

        TurnManager tm = TurnManager.Instance;
        bool matchEnded = tm != null && tm.ServerIsMatchEnded();
        if (returnToLobbyWhenAnyPlayerDisconnectsInGameplay && !matchEnded)
        {
            Debug.LogWarning($"[LobbyNetworkManager] Player disconnected in gameplay -> return all to lobby target={lobbyTarget}");
            ServerChangeScene(lobbyTarget);
            return;
        }

        if (!TryResolveDisconnectedPlayerInfo(conn, out uint disconnectedNetId, out int duckColorIndex))
            return;

        if (tm == null)
            return;

        tm.ServerHandlePlayerDisconnected(
            disconnectedNetId,
            duckColorIndex,
            $"OnRoomServerDisconnect connId={conn.connectionId}"
        );
    }


    private void HandleDisconnectedClientUIFlow()
    {
        UIFlow flow = UIFlow.I;
        bool disconnected = IsClientDisconnected();

        Scene active = SceneManager.GetActiveScene();
        if (IsSceneMatch(active, GameplayScene))
        {
            // If host goes away while clients are in gameplay, force return to lobby scene.
            if (disconnected)
            {
                ForceReturnToLobbyClient("DisconnectedInGameplay");
                return;
            }

            flow?.HideAllForGameplay();
            return;
        }

        if (disconnected)
            ForceReturnToLobbyClient("DisconnectedOutsideGameplay");

        flow?.ShowLobbyList();
    }

    private static bool IsClientDisconnected()
    {
        if (!NetworkClient.active)
            return true;

        if (!NetworkClient.isConnected)
            return true;

        if (NetworkClient.connection == null)
            return true;

        return false;
    }

    private bool HasTurnStateChanged(TurnManager tm)
    {
        if (tm == null)
            return false;

        bool changed =
            tm.currentTurnIndex != _lastObservedTurnIndex ||
            tm.currentTurnNetId != _lastObservedTurnNetId ||
            tm.currentTurnRemainingSeconds != _lastObservedTurnRemaining ||
            tm.TurnOrder.Count != _lastObservedTurnOrderCount;

        _lastObservedTurnIndex = tm.currentTurnIndex;
        _lastObservedTurnNetId = tm.currentTurnNetId;
        _lastObservedTurnRemaining = tm.currentTurnRemainingSeconds;
        _lastObservedTurnOrderCount = tm.TurnOrder.Count;

        return changed;
    }

    private static int CountGameplayPlayers()
    {
        PlayerManager[] players = FindObjectsOfType<PlayerManager>();
        int count = 0;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerManager pm = players[i];
            if (pm == null || pm.SeatIndex < 0)
                continue;
            count++;
        }

        return count;
    }

    private void ForceReturnToLobbyClient(string reason)
    {
        _disconnectRecoveryTriggered = true;
        _watchGameplayDisconnect = false;
        RequestShowLobbyListAfterDisconnect();

        string lobbyTarget = ResolveLobbySceneName();
        Debug.LogWarning($"[LobbyNetworkManager] ForceReturnToLobbyClient reason={reason} target={lobbyTarget}");
        Scene active = SceneManager.GetActiveScene();
        if (!IsSceneMatch(active, lobbyTarget))
            SceneManager.LoadScene(lobbyTarget);
        else
            StartCoroutine(CoEnsureLobbyListVisible());
    }

    private string ResolveLobbySceneName()
    {
        return string.IsNullOrWhiteSpace(RoomScene) ? lobbySceneName : RoomScene;
    }

    private void RequestShowLobbyListAfterDisconnect()
    {
        _pendingShowLobbyListAfterDisconnect = true;
        SceneManager.sceneLoaded -= OnSceneLoaded_ShowLobbyListAfterDisconnect;
        SceneManager.sceneLoaded += OnSceneLoaded_ShowLobbyListAfterDisconnect;
    }

    private void OnSceneLoaded_ShowLobbyListAfterDisconnect(Scene scene, LoadSceneMode mode)
    {
        if (!_pendingShowLobbyListAfterDisconnect)
            return;

        if (!IsSceneMatch(scene, ResolveLobbySceneName()))
            return;

        StartCoroutine(CoEnsureLobbyListVisible());
    }

    private IEnumerator CoEnsureLobbyListVisible()
    {
        for (int i = 0; i < 20; i++)
        {
            UIFlow flow = UIFlow.I ?? FindObjectOfType<UIFlow>(true);
            if (flow != null)
            {
                flow.ShowLobbyList();
                _pendingShowLobbyListAfterDisconnect = false;
                SceneManager.sceneLoaded -= OnSceneLoaded_ShowLobbyListAfterDisconnect;
                yield break;
            }

            yield return null;
        }
    }

    private static bool IsSceneMatch(Scene scene, string configuredPathOrName)
    {
        if (string.IsNullOrWhiteSpace(configuredPathOrName))
            return false;

        if (string.Equals(scene.path, configuredPathOrName, System.StringComparison.OrdinalIgnoreCase))
            return true;

        string expectedName = System.IO.Path.GetFileNameWithoutExtension(configuredPathOrName);
        if (string.IsNullOrWhiteSpace(expectedName))
            expectedName = configuredPathOrName;

        return string.Equals(scene.name, expectedName, System.StringComparison.OrdinalIgnoreCase);
    }

    [Server]
    private static bool TryResolveDisconnectedPlayerInfo(NetworkConnectionToClient conn, out uint netId, out int duckColorIndex)
    {
        netId = 0;
        duckColorIndex = -1;

        if (conn?.identity != null)
        {
            if (conn.identity.TryGetComponent(out PlayerManager pm))
            {
                netId = pm.netId;
                duckColorIndex = pm.duckColorIndex;
                return true;
            }

            netId = conn.identity.netId;
            return netId != 0;
        }

        if (conn?.owned != null)
        {
            foreach (NetworkIdentity owned in conn.owned)
            {
                if (owned == null)
                    continue;

                if (owned.TryGetComponent(out PlayerManager pm))
                {
                    netId = pm.netId;
                    duckColorIndex = pm.duckColorIndex;
                    return true;
                }

                if (netId == 0)
                    netId = owned.netId;
            }
        }

        return netId != 0;
    }
}
