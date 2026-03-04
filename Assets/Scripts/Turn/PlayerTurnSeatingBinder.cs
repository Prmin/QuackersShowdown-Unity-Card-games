using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerManager))]
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerTurnSeatingBinder : NetworkBehaviour
{
    // Slot visibility plan compatible with TurnOrder ring mapping.
    private static readonly Dictionary<int, int[]> VisibleSlotsByPlayerCount = new Dictionary<int, int[]>
    {
        { 2, new[] { 1, 5 } },
        { 3, new[] { 1, 2, 4, 5 } },
        { 4, new[] { 1, 2, 3, 4, 5 } },
        { 5, new[] { 1, 2, 3, 4, 5 } },
        { 6, new[] { 1, 2, 3, 4, 5 } },
    };

    private const string EnemiesAreaRootName = "EnemiesArea";
    private const string EnemySlotPrefix = "EnemyArea";

    public override void OnStartServer()
    {
        base.OnStartServer();

        TurnManager tm = TurnManager.Instance;
        if (tm == null) return;

        tm.ServerRebuildTurnOrder($"Binder.OnStartServer netId={netId}");
        tm.ServerRequestClientLayoutRefresh("Binder.OnStartServer");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        TurnManager tm = TurnManager.Instance;
        if (tm == null) return;

        // During gameplay disconnect flow, TurnManager already removed this netId from TurnOrder.
        // Skip seat-based rebuild here to preserve "next player shifts up" order.
        if (tm.TurnOrder.Count > 0 && !tm.TurnOrder.Contains(netId))
        {
            tm.ServerRequestClientLayoutRefresh("Binder.OnStopServer");
            return;
        }

        tm.ServerRebuildTurnOrder($"Binder.OnStopServer netId={netId}");
        tm.ServerRequestClientLayoutRefresh("Binder.OnStopServer");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(DelayThenForceRecompute());
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        ForceRecompute();
    }

    private void OnDestroy()
    {
        if (!NetworkClient.active) return;
        ForceRecompute();
    }

    private IEnumerator DelayThenForceRecompute()
    {
        yield return null;
        ForceRecompute();
    }

    public static void ForceRecompute()
    {
        if (!NetworkClient.active) return;

        // Keep a safe fallback visible set first; local turn-order mapping will refine it after recompute.
        int playerCount = CountClientPlayers();
        RefreshVisibleSlotsForCount(playerCount);
        PlayerManager.RequestTurnOrderLayoutRefresh("PlayerTurnSeatingBinder.ForceRecompute");
    }

    public static void RefreshVisibleSlotsForCount(int playerCount)
    {
        Transform root = FindEnemiesAreaRoot();
        if (root == null) return;

        int count = Mathf.Clamp(playerCount, 2, 6);
        HashSet<int> visible = VisibleSlotsByPlayerCount.TryGetValue(count, out int[] slots)
            ? new HashSet<int>(slots)
            : new HashSet<int>(new[] { 1, 2, 3, 4, 5 });

        for (int slot = 1; slot <= 5; slot++)
        {
            Transform tr = FindEnemySlot(root, slot);
            if (tr == null) continue;
            tr.gameObject.SetActive(visible.Contains(slot));
        }
    }

    public static void RefreshVisibleSlotsByUsedSlots(IEnumerable<int> usedSlots)
    {
        Transform root = FindEnemiesAreaRoot();
        if (root == null) return;

        HashSet<int> visible = new HashSet<int>();
        if (usedSlots != null)
        {
            foreach (int slot in usedSlots)
            {
                if (slot >= 1 && slot <= 5)
                    visible.Add(slot);
            }
        }

        for (int slot = 1; slot <= 5; slot++)
        {
            Transform tr = FindEnemySlot(root, slot);
            if (tr == null) continue;
            tr.gameObject.SetActive(visible.Contains(slot));
        }
    }

    private static int CountClientPlayers()
    {
        int count = GameObject.FindObjectsOfType<PlayerManager>()
            .Count(pm => pm != null && pm.isActiveAndEnabled && pm.SeatIndex >= 0);

        return Mathf.Clamp(count, 2, 6);
    }

    private static Transform FindEnemiesAreaRoot()
    {
        Transform canvas = GameObject.Find("Main Canvas")?.transform ?? GameObject.Find("Canvas")?.transform;
        if (canvas == null) return GameObject.Find(EnemiesAreaRootName)?.transform;

        Transform uiRoot = FindChildRecursive(canvas, "Image") ?? canvas;
        Transform root = FindChildRecursive(uiRoot, EnemiesAreaRootName)
                         ?? FindChildRecursive(canvas, EnemiesAreaRootName)
                         ?? GameObject.Find(EnemiesAreaRootName)?.transform;
        return root;
    }

    private static Transform FindEnemySlot(Transform root, int slot)
    {
        Transform byOneBased = FindChildRecursive(root, $"{EnemySlotPrefix}{slot}");
        if (byOneBased != null) return byOneBased;

        // Fallback for projects that use EnemyArea0..4.
        return FindChildRecursive(root, $"{EnemySlotPrefix}{slot - 1}");
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
