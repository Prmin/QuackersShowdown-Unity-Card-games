using UnityEngine;

public class LobbyAssets : MonoBehaviour
{
    public static LobbyAssets Instance { get; private set; }

    // ลำดับต้องเป็น: 0=น้ำเงิน,1=ส้ม,2=ชมพู,3=เขียว,4=เหลือง,5=ม่วง
    [Header("Duck Sprites (0 Blue, 1 Orange, 2 Pink, 3 Green, 4 Yellow, 5 Purple)")]
    public Sprite[] duckSprites = new Sprite[6];

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public Sprite GetDuckSpriteByIndex(int idx)
    {
        if (duckSprites == null || duckSprites.Length == 0) return null;
        idx = Mathf.Clamp(idx, 0, duckSprites.Length - 1);
        return duckSprites[idx];
    }
}
