using UnityEngine;
using UnityEngine.UI;

public class AuthenticateUI : MonoBehaviour
{
    [SerializeField] private Button authenticateButton;

    private void Awake()
    {
        authenticateButton.onClick.AddListener(() =>
        {
            string name = LocalProfileData.GetPlayerName("Player");
            LocalProfileData.SetPlayerName(name);

            // ถ้าเราอยู่ในห้องอยู่แล้ว ให้ตั้งชื่อด้วย
            var me = LobbyRoomPlayer.Local;
            if (me) me.CmdSetName(name);

            UIFlow.I?.ShowLobbyList();
        });
    }
}
