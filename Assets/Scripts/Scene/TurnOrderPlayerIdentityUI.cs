using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnOrderPlayerIdentityUI : MonoBehaviour
{
    [Serializable]
    private class SlotUI
    {
        public GameObject root;
        public Image avatarImage;
        public TMP_Text nameText;
        public bool hideWhenEmpty = true;
    }

    [Header("PlayerArea (PA)")]
    [SerializeField] private SlotUI playerArea = new SlotUI();

    [Header("EnemyArea (EA1..EA5)")]
    [SerializeField] private SlotUI enemyArea1 = new SlotUI();
    [SerializeField] private SlotUI enemyArea2 = new SlotUI();
    [SerializeField] private SlotUI enemyArea3 = new SlotUI();
    [SerializeField] private SlotUI enemyArea4 = new SlotUI();
    [SerializeField] private SlotUI enemyArea5 = new SlotUI();

    [Header("Avatar Sprites (Profile index)")]
    [SerializeField] private Sprite[] profileAvatarSprites = new Sprite[0];

    [Header("Fallback")]
    [SerializeField] private Sprite[] duckColorSprites = new Sprite[6];
    [SerializeField] private string unknownName = "Player";
    [SerializeField] private bool hideUnusedEnemySlots = true;

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.2f;

    private SlotUI[] _enemySlots;
    private float _nextRefreshAt;

    private void Awake()
    {
        _enemySlots = new[] { enemyArea1, enemyArea2, enemyArea3, enemyArea4, enemyArea5 };
    }

    private void OnEnable()
    {
        _nextRefreshAt = 0f;
        ForceRefresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshAt)
            return;

        _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshIntervalSeconds);
        ForceRefresh();
    }

    public void ForceRefresh()
    {
        PlayerManager local = PlayerManager.localInstance;
        if (local != null)
            ApplySlot(playerArea, local, true);
        else
            ApplySlot(playerArea, null, true);

        ClearEnemySlots();

        if (!NetworkClient.active || local == null)
            return;

        TurnManager tm = TurnManager.Instance;
        if (tm == null || tm.TurnOrder.Count <= 0)
            return;

        List<uint> order = tm.TurnOrder.ToList();
        int myIndex = order.IndexOf(local.netId);
        if (myIndex < 0)
            return;

        for (int i = 0; i < order.Count; i++)
        {
            uint netId = order[i];
            if (netId == local.netId)
                continue;

            if (!TryGetPlayerByNetId(netId, out PlayerManager other))
                continue;

            int slot = ComputeEnemySlotByTurnOrder(myIndex, i, order.Count);
            if (slot < 1 || slot > 5)
                continue;

            ApplySlot(_enemySlots[slot - 1], other, true);
        }
    }

    private void ClearEnemySlots()
    {
        if (_enemySlots == null)
            return;

        foreach (SlotUI slot in _enemySlots)
            ApplySlot(slot, null, !hideUnusedEnemySlots);
    }

    private void ApplySlot(SlotUI slot, PlayerManager player, bool visible)
    {
        if (slot == null)
            return;

        if (slot.nameText != null)
        {
            string name = player != null ? player.DisplayName : "-";
            if (string.IsNullOrWhiteSpace(name))
                name = unknownName;
            slot.nameText.text = name;
        }

        if (slot.avatarImage != null)
        {
            Sprite avatar = ResolveAvatar(player);
            slot.avatarImage.sprite = avatar;
            slot.avatarImage.enabled = avatar != null;
            if (avatar != null)
                slot.avatarImage.preserveAspect = true;
        }

        if (slot.root != null && slot.hideWhenEmpty)
            slot.root.SetActive(visible);
    }

    private Sprite ResolveAvatar(PlayerManager player)
    {
        if (player == null)
            return null;

        int avatarIndex = player.ProfileAvatarIndex;

        if (profileAvatarSprites != null &&
            avatarIndex >= 0 &&
            avatarIndex < profileAvatarSprites.Length &&
            profileAvatarSprites[avatarIndex] != null)
        {
            return profileAvatarSprites[avatarIndex];
        }

        if (LobbyAssets.Instance != null)
        {
            Sprite fromLobbyAssets = LobbyAssets.Instance.GetProfileAvatarSpriteByIndex(avatarIndex);
            if (fromLobbyAssets != null)
                return fromLobbyAssets;
        }

        int duckIndex = player.duckColorIndex;
        if (duckColorSprites != null &&
            duckIndex >= 0 &&
            duckIndex < duckColorSprites.Length)
        {
            return duckColorSprites[duckIndex];
        }

        return null;
    }

    private static bool TryGetPlayerByNetId(uint netId, out PlayerManager pm)
    {
        pm = null;
        if (!NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity ni) || ni == null)
            return false;

        return ni.TryGetComponent(out pm);
    }

    private static int ComputeEnemySlotByTurnOrder(int myIndex, int otherIndex, int orderCount)
    {
        if (orderCount < 2 || myIndex < 0 || otherIndex < 0 || myIndex == otherIndex)
            return -1;

        int delta = otherIndex - myIndex;
        int slot = delta < 0 ? -delta : 6 - delta;
        return Mathf.Clamp(slot, 1, 5);
    }
}
