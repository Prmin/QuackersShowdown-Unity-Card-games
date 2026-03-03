using UnityEngine;

public static class LocalProfileData
{
    public const string KeyPlayerName = "PlayerName";
    public const string LegacyKeyPlayerName = "playerName";
    public const string KeyAvatarIndex = "profile.avatar_index";

    public static string GetPlayerName(string defaultName = "Player")
    {
        string name = PlayerPrefs.GetString(KeyPlayerName, string.Empty)?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        string legacy = PlayerPrefs.GetString(LegacyKeyPlayerName, string.Empty)?.Trim();
        if (!string.IsNullOrWhiteSpace(legacy))
        {
            SetPlayerName(legacy);
            return legacy;
        }

        return defaultName;
    }

    public static void SetPlayerName(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        PlayerPrefs.SetString(KeyPlayerName, safe);
        // Keep legacy key for old scripts/UI that still read it.
        PlayerPrefs.SetString(LegacyKeyPlayerName, safe);
        PlayerPrefs.Save();
    }

    public static int GetAvatarIndex(int avatarCount, int defaultIndex = 0)
    {
        if (avatarCount <= 0)
            return -1;

        int idx = PlayerPrefs.GetInt(KeyAvatarIndex, defaultIndex);
        return Mathf.Clamp(idx, 0, avatarCount - 1);
    }

    public static void SetAvatarIndex(int index)
    {
        PlayerPrefs.SetInt(KeyAvatarIndex, Mathf.Max(0, index));
        PlayerPrefs.Save();
    }
}
