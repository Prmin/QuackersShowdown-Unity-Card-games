using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Static helper for managing the duck card pool.
/// Abstracts away storage and instantiation logic.
/// </summary>
public static class CardPoolManager
{
    // Mapping from card name to its prefab
    private static Dictionary<string, GameObject> _prefabs;
    // Mapping from card name to remaining count in pool
    private static Dictionary<string, int> _pool;

    /// <summary>
    /// Initialize the pool manager. Call this once on server start with your prefab dictionary.
    /// </summary>
    public static void Initialize(Dictionary<string, GameObject> cardPrefabs, int initialCount)
    {
        _prefabs = new Dictionary<string, GameObject>(cardPrefabs);
        _pool = new Dictionary<string, int>();
        foreach (var kv in _prefabs)
            _pool[kv.Key] = initialCount;
    }

    // Remove "(Clone)" suffix
    private static string CleanCardName(string raw) => raw.Replace("(Clone)", "").Trim();

    /// <summary>
    /// Return a destroyed card back to the pool. Call on server only.
    /// </summary>
    public static void ReturnCard(GameObject cardGO)
    {
        if (!NetworkServer.active || cardGO == null || _pool == null)
            return;
        string key = CleanCardName(cardGO.name);
        if (_pool.ContainsKey(key)) _pool[key]++;
        else _pool[key] = 1;
    }

     /// <summary>
    /// สุ่มหยิบการ์ดจากเด็ค (Server เท่านั้น) โดยไม่ผูก parent
    /// ปล่อยให้ฝั่งที่เรียกเป็นคนกำหนดโซน/ตำแหน่งผ่าน SyncVar (เช่น DuckCard.ServerAssignToZone)
    /// </summary>
    [Server]
    public static GameObject DrawRandomCard()
    {
        if (!NetworkServer.active || _pool == null || _prefabs == null)
            return null;

        // เลือกคีย์ที่ยังมีเหลือและ prefab ไม่เป็น null
        var available = new System.Collections.Generic.List<string>();
        foreach (var kv in _pool)
        {
            if (kv.Value > 0 && _prefabs.TryGetValue(kv.Key, out var pf) && pf != null)
                available.Add(kv.Key);
        }
        if (available.Count == 0) return null;

        string choice = available[UnityEngine.Random.Range(0, available.Count)];
        _pool[choice]--;

        var prefab = _prefabs[choice];
        var go = Object.Instantiate(prefab);   // ❗ ไม่ส่ง parent ใด ๆ
        return go;
    }

    /// <summary>
    /// (รองรับของเดิม) สุ่มการ์ดและ "ถ้า" parent เป็นวัตถุในซีนจริง ค่อย SetParent ให้
    /// แนะนำให้อัปเดต call-site ไปใช้ DrawRandomCard() แล้วกำหนดโซนผ่าน SyncVar แทน
    /// </summary>
    [System.Obsolete("Use DrawRandomCard() and assign zone/position via SyncVars instead.")]
    [Server]
    public static GameObject DrawRandomCard(Transform parent)
    {
        var go = DrawRandomCard();
        if (go == null) return null;

        if (parent != null)
        {
            var pgo = parent.gameObject;
            if (pgo.scene.IsValid() && pgo.scene.isLoaded)
                go.transform.SetParent(parent, false); // เฉพาะกรณี parent อยู่ในซีนเท่านั้น
        }
        return go;
    }


    /// <summary>
    /// Checks if any card remains in the pool. Server only.
    /// </summary>
    public static bool HasCards()
    {
        if (!NetworkServer.active || _pool == null) return false;
        foreach (var qty in _pool.Values)
            if (qty > 0) return true;
        return false;
    }

    /// <summary>
    /// Get remaining count of a specific card name.
    /// </summary>
    public static int GetRemainingCount(string cardName)
    {
        if (_pool == null) return 0;
        return _pool.TryGetValue(cardName, out var count) ? count : 0;
    }

    /// <summary>
    /// Get a snapshot of all pool counts: card name → remaining count. Server only.
    /// </summary>
    public static Dictionary<string, int> GetAllPoolCounts()
    {
        if (!NetworkServer.active || _pool == null)
            return new Dictionary<string, int>();
        // Return a copy to prevent external modification
        return new Dictionary<string, int>(_pool);
    }


    public static void AddToPool(string cardName)
    {
        if (_pool == null) return;
        if (_pool.ContainsKey(cardName)) _pool[cardName]++;
        else _pool[cardName] = 1;
    }

}
