using System.Collections.Generic;
using UnityEngine;
using TMPro;          // ถ้านายใช้ TextMeshPro
using UnityEngine.UI; // ถ้าใช้ Text ปกติ

public class ActionHandUI : MonoBehaviour
{
    public static ActionHandUI Instance { get; private set; }

    [Header("Local player hand root (PlayerArea)")]
    public Transform localHandRoot;   // ตรง PlayerArea ของเรา

    [Header("Enemy hand count UI โดย map ตาม seat หรือ index ที่นายใช้")]
    public List<HandCountSlot> enemyHandSlots = new List<HandCountSlot>();

    [Header("Prefabs ของ action card ตาม key")]
    public List<ActionCardPrefabEntry> actionCardPrefabs = new List<ActionCardPrefabEntry>();

    Dictionary<string, GameObject> _prefabLookup;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _prefabLookup = new Dictionary<string, GameObject>();
        foreach (var entry in actionCardPrefabs)
        {
            if (!string.IsNullOrEmpty(entry.cardKey) && entry.prefab != null)
            {
                _prefabLookup[entry.cardKey] = entry.prefab;
            }
        }
    }

    // ------- เรียกจาก PlayerManager.TargetRpc_ReceiveActionCard -------
    public void SpawnLocalHandCard(PlayerManager owner, string cardKey)
    {
        if (!owner.isLocalPlayer) return;   // แสดงในมือแค่ของเรา

        if (!_prefabLookup.TryGetValue(cardKey, out var prefab))
        {
            Debug.LogError($"[ActionHandUI] No prefab mapped for cardKey={cardKey}");
            return;
        }

        var cardObj = Instantiate(prefab, localHandRoot, false);

        // ผูกสคริปต์ LocalHandCard เพื่อส่ง CmdPlayActionCard เมื่อเล่นใบนี้
        var localHandCard = cardObj.GetComponent<LocalHandCard>();
        if (localHandCard == null)
        {
            localHandCard = cardObj.AddComponent<LocalHandCard>();
        }

        localHandCard.Initialize(owner, cardKey);
    }

    // ------- SyncVar hook เรียกตอนจำนวนการ์ดเปลี่ยน -------

    public void UpdateHandCountUI(PlayerManager pm, int newCount)
    {
        if (pm.isLocalPlayer)
        {
            // จะโชว์เป็น text, icon อะไรก็แล้วแต่
            // ตัวอย่าง: หา slot "MyHand" จาก enemyHandSlots ก็ยังได้
            foreach (var slot in enemyHandSlots)
            {
                if (slot.isLocal)
                {
                    slot.SetCount(newCount);
                    return;
                }
            }
        }
        else
        {
            // ผูกตาม seat / playerId ที่นายใช้
            foreach (var slot in enemyHandSlots)
            {
                if (!slot.isLocal && slot.seatIndex == pm.SeatIndex) // สมมติ pm มี SeatIndex
                {
                    slot.SetCount(newCount);
                    return;
                }
            }
        }
    }

    [System.Serializable]
    public class ActionCardPrefabEntry
    {
        public string cardKey;      // เช่น "QuickShot", "BumpLeft"
        public GameObject prefab;   // prefab ที่ใช้สร้างในมือเรา (local-only)
    }

    [System.Serializable]
    public class HandCountSlot
    {
        public bool isLocal;
        public int seatIndex;
        public TextMeshProUGUI countText;   // หรือ Text ปกติ

        public void SetCount(int count)
        {
            if (countText != null)
            {
                countText.text = count.ToString();
            }
        }
    }
}
