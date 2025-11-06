using UnityEngine;
using UnityEngine.UI;

public class AuthenticateUI : MonoBehaviour
{
    [SerializeField] private Button authenticateButton;

    private void Awake()
    {
        authenticateButton.onClick.AddListener(() =>
        {
            var name = EditPlayerName.Instance.GetPlayerName();
            PlayerPrefs.SetString(LobbyManager.KEY_PLAYER_NAME, name);

            // ถ้าเราอยู่ในห้องอยู่แล้ว ให้ตั้งชื่อด้วย
            var me = LobbyRoomPlayer.Local;
            if (me) me.CmdSetName(name);

            UIFlow.I?.ShowLobbyList();
        });
    }
}
