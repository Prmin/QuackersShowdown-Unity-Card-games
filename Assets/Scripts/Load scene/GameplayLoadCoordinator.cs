using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class GameplayLoadCoordinator : NetworkBehaviour
{
    public static GameplayLoadCoordinator Instance;

    // ให้ระบบอื่นผูกใช้งาน (เช่นย้าย "แจกการ์ดเริ่มเกม" มาไว้ตรงนี้)
    public static event Action BarrierGoServer;
    public static event Action BarrierGoClient;

    [SyncVar(hook = nameof(OnAllReadyChanged))]
    private bool allReady = false;

    // ✅ จำนวนผู้เล่นเป้าหมาย “มาจากล็อบบี้” (maxPlayers ที่เลือกไว้)
    [SyncVar] private int expectedPlayers = 0;

    private readonly HashSet<uint> _readyPlayers = new HashSet<uint>();

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!allReady) SceneLoadingOverlay.EnsureShown("Loading...");
        else SceneLoadingOverlay.Hide();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // รอ 1 เฟรมให้ LobbyManager (DontDestroyOnLoad) พร้อม แล้วดึงค่าจากล็อบบี้
        StartCoroutine(ServerInitExpectedFromLobby());
    }

    private IEnumerator ServerInitExpectedFromLobby()
    {
        yield return null;

        // 1) เอาจากล็อบบี้ (ค่าที่หน้า Create/Discovery ตั้งไว้)
        int target = 0;
        if (LobbyManager.Instance != null)
        {
            target = LobbyManager.Instance.LastKnownMaxPlayers;
        }

        // 2) เผื่อ safety: ถ้า 0 ให้ fallback จาก NetworkManager.maxConnections
        if (target <= 0 && NetworkManager.singleton != null)
        {
            target = NetworkManager.singleton.maxConnections;
        }

        // 3) สุดท้ายถ้ายัง 0 ให้ล็อก 2..6
        target = Mathf.Clamp(target <= 0 ? 2 : target, 2, 6);

        expectedPlayers = target;

        // แจ้งทุกคนให้เห็น (0/expected)
        RpcUpdateWaiting(_readyPlayers.Count, expectedPlayers);
    }

    [Server]
    public void ServerMarkReady(NetworkIdentity playerNI)
    {
        if (playerNI == null) return;

        _readyPlayers.Add(playerNI.netId);

        // แสดงผลตาม “เป้าหมายจากล็อบบี้” เท่านั้น (ไม่เปลี่ยนไปตาม numPlayers)
        RpcUpdateWaiting(_readyPlayers.Count, expectedPlayers);

        if (!allReady && expectedPlayers > 0 && _readyPlayers.Count >= expectedPlayers)
        {
            allReady = true;   // SyncVar -> Hook client ทุกคน
            RpcGo();           // ปิด overlay พร้อมกันทุกไคลเอนต์
            BarrierGoServer?.Invoke(); // ← จุดเริ่มเกมฝั่งเซิร์ฟเวอร์ (ย้ายแจกการ์ด/เริ่มเทิร์นมาไว้ที่นี่)
        }
    }

    [ClientRpc]
    private void RpcUpdateWaiting(int ready, int total)
    {
        SceneLoadingOverlay.EnsureShown();
        SceneLoadingOverlay.SetProgress(ready, total);
    }

    [ClientRpc]
    private void RpcGo()
    {
        SceneLoadingOverlay.Hide();
        // จัดที่นั่งใหม่อีกรอบหลังทุกคนพร้อมแน่
        PlayerTurnSeatingBinder.ForceRecompute();
        BarrierGoClient?.Invoke();
    }

    private void OnAllReadyChanged(bool _, bool now)
    {
        if (now) SceneLoadingOverlay.Hide();
    }
}
