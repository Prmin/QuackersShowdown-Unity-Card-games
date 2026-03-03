using UnityEngine;

public class LobbyAssets : MonoBehaviour
{
    public static LobbyAssets Instance { get; private set; }

    [Header("Duck Sprites (0 Blue, 1 Orange, 2 Pink, 3 Green, 4 Yellow, 5 Purple)")]
    public Sprite[] duckSprites = new Sprite[6];

    [Header("Profile Avatar Sprites (shared for all players)")]
    public Sprite[] profileAvatarSprites = new Sprite[0];

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public Sprite GetDuckSpriteByIndex(int idx)
    {
        if (duckSprites == null || duckSprites.Length == 0)
            return null;

        idx = Mathf.Clamp(idx, 0, duckSprites.Length - 1);
        return duckSprites[idx];
    }

    public Sprite GetProfileAvatarSpriteByIndex(int idx)
    {
        if (profileAvatarSprites == null || profileAvatarSprites.Length == 0)
            return null;

        idx = Mathf.Clamp(idx, 0, profileAvatarSprites.Length - 1);
        return profileAvatarSprites[idx];
    }
}
