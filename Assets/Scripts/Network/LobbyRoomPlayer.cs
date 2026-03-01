using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class LobbyRoomPlayer : NetworkRoomPlayer
{
    public static LobbyRoomPlayer Local { get; private set; }

    [SyncVar] public string displayName;
    [SyncVar] public bool isHost;

    // 0=Blue,1=Orange,2=Pink,3=Green,4=Yellow,5=Purple
    [SyncVar(hook = nameof(OnDuckColorChanged))] public int duckColorIndex;

    // Shared in lobby for everyone to see.
    [SyncVar] public int statsPlayed;
    [SyncVar] public int statsWin;
    [SyncVar] public int statsLoss;
    [SyncVar] public int statsDraw;

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (duckColorIndex < 0 || duckColorIndex > 5 || !IsColorAvailable(duckColorIndex, this))
            duckColorIndex = PickFreeColor();

        isHost = (connectionToClient == NetworkServer.localConnection);
    }

    [Client]
    public void ClientToggleReady()
    {
        if (!netIdentity || !netIdentity.isOwned)
            return;

        CmdChangeReadyState(!readyToBegin);
    }

    [Command]
    public void CmdSetDuckColor(int index)
    {
        index = Mathf.Clamp(index, 0, 5);

        if (!IsColorAvailable(index, this))
        {
            TargetColorDenied(connectionToClient, index);
            return;
        }

        duckColorIndex = index;
    }

    [TargetRpc]
    private void TargetColorDenied(NetworkConnection target, int index)
    {
    }

    private void OnDuckColorChanged(int oldValue, int newValue)
    {
        LobbyUI.Instance?.RefreshColorLocks();
    }

    private bool IsColorAvailable(int index, LobbyRoomPlayer requester)
    {
        LobbyRoomPlayer[] all = GameObject.FindObjectsOfType<LobbyRoomPlayer>();
        foreach (LobbyRoomPlayer p in all)
        {
            if (!p || p == requester)
                continue;
            if (p.duckColorIndex == index)
                return false;
        }

        return true;
    }

    private int PickFreeColor()
    {
        bool[] used = new bool[6];
        LobbyRoomPlayer[] all = GameObject.FindObjectsOfType<LobbyRoomPlayer>();
        foreach (LobbyRoomPlayer p in all)
        {
            if (!p)
                continue;

            int idx = p.duckColorIndex;
            if (idx >= 0 && idx < 6)
                used[idx] = true;
        }

        List<int> free = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            if (!used[i])
                free.Add(i);
        }

        if (free.Count == 0)
            return 0;

        return free[Random.Range(0, free.Count)];
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Local = this;

        string playerName = PlayerPrefs.GetString(LobbyManager.KEY_PLAYER_NAME, $"Player {Random.Range(100, 999)}");
        CmdSetName(playerName);

        int savedColor = PlayerPrefs.GetInt(LobbyManager.KEY_DUCK_COLOR, 0);
        CmdSetDuckColor(savedColor);

        SubmitLocalStatsToServer();
    }

    public override void OnStopClient()
    {
        if (isLocalPlayer && Local == this)
            Local = null;

        base.OnStopClient();
    }

    [Command]
    public void CmdSetName(string name)
    {
        displayName = string.IsNullOrWhiteSpace(name) ? $"Player {Random.Range(100, 999)}" : name.Trim();
    }

    [Client]
    public void SubmitLocalStatsToServer()
    {
        if (!isLocalPlayer || !netIdentity || !netIdentity.isOwned)
            return;

        LocalMatchStats.Snapshot snap = LocalMatchStats.Get();
        CmdSubmitLocalStats(snap.played, snap.win, snap.loss, snap.draw);
    }

    [Command]
    private void CmdSubmitLocalStats(int played, int win, int loss, int draw)
    {
        played = Mathf.Max(0, played);
        win = Mathf.Max(0, win);
        loss = Mathf.Max(0, loss);
        draw = Mathf.Max(0, draw);

        int sum = win + loss + draw;
        if (played < sum)
            played = sum;

        statsPlayed = played;
        statsWin = win;
        statsLoss = loss;
        statsDraw = draw;
    }

    [Command(requiresAuthority = false)]
    public void CmdKickPlayer(uint targetNetId, NetworkConnectionToClient sender = null)
    {
        if (sender != NetworkServer.localConnection)
            return;

        if (NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity id))
        {
            NetworkConnectionToClient conn = id.connectionToClient;
            if (conn != null)
                conn.Disconnect();
        }
    }
}
