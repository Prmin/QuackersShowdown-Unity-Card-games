using UnityEngine;

public enum MatchResult
{
    Win,
    Loss,
    Draw
}

public static class LocalMatchStats
{
    private const string KeyPlayed = "stats.played";
    private const string KeyWin = "stats.win";
    private const string KeyLoss = "stats.loss";
    private const string KeyDraw = "stats.draw";
    private const string KeyDuckShots = "stats.duck_shots";

    public struct Snapshot
    {
        public int played;
        public int win;
        public int loss;
        public int draw;
        public int duckShots;
    }

    public static Snapshot Get()
    {
        return new Snapshot
        {
            played = PlayerPrefs.GetInt(KeyPlayed, 0),
            win = PlayerPrefs.GetInt(KeyWin, 0),
            loss = PlayerPrefs.GetInt(KeyLoss, 0),
            draw = PlayerPrefs.GetInt(KeyDraw, 0),
            duckShots = PlayerPrefs.GetInt(KeyDuckShots, 0)
        };
    }

    public static void Record(MatchResult result)
    {
        PlayerPrefs.SetInt(KeyPlayed, PlayerPrefs.GetInt(KeyPlayed, 0) + 1);

        switch (result)
        {
            case MatchResult.Win:
                PlayerPrefs.SetInt(KeyWin, PlayerPrefs.GetInt(KeyWin, 0) + 1);
                break;
            case MatchResult.Loss:
                PlayerPrefs.SetInt(KeyLoss, PlayerPrefs.GetInt(KeyLoss, 0) + 1);
                break;
            case MatchResult.Draw:
                PlayerPrefs.SetInt(KeyDraw, PlayerPrefs.GetInt(KeyDraw, 0) + 1);
                break;
        }

        PlayerPrefs.Save();
    }

    public static void RecordDuckShots(int amount)
    {
        if (amount <= 0)
            return;

        PlayerPrefs.SetInt(KeyDuckShots, PlayerPrefs.GetInt(KeyDuckShots, 0) + amount);
        PlayerPrefs.Save();
    }
}
