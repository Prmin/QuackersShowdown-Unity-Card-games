using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EditPlayerName : MonoBehaviour
{
    public static EditPlayerName Instance { get; private set; }

    public event EventHandler OnNameChanged;

    [SerializeField] private TextMeshProUGUI playerNameText;

    private string playerName = "Player";

    private void Awake()
    {
        Instance = this;
        // โหลดชื่อเดิม (ถ้ามี)
        playerName = LocalProfileData.GetPlayerName(playerName);
        playerNameText.text = playerName;

        GetComponent<Button>().onClick.AddListener(() =>
        {
            UI_InputWindow.Show_Static("Player Name", playerName,
                "", 20,
                () => { /* Cancel */ },
                (string newName) =>
                {
                    playerName = string.IsNullOrWhiteSpace(newName) ? "Player" : newName.Trim();
                    playerNameText.text = playerName;
                    OnNameChanged?.Invoke(this, EventArgs.Empty);
                });
        });
    }

    private void Start()
    {
        OnNameChanged += EditPlayerName_OnNameChanged;
    }

    private void EditPlayerName_OnNameChanged(object sender, EventArgs e)
    {
        LocalProfileData.SetPlayerName(playerName);
        var me = LobbyRoomPlayer.Local;
        if (me) me.CmdSetName(playerName);
    }

    public string GetPlayerName() => playerName;
}
