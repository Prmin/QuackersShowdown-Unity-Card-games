using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class LobbyPlayerSingleUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Image characterImage;
    [SerializeField] private Button kickPlayerButton;

    // ✅ รูปสถานะ Ready
    [Header("Ready State Display")]
    [SerializeField] private Image readyStateImage;     // ใส่ Image บน template ไว้
    [SerializeField] private Sprite readySprite;        // รูปตอน Ready
    [SerializeField] private Sprite notReadySprite;     // รูปตอน Not Ready

    // ✅ ตั้งค่าว่าจะ tint สีสไปรต์ไหม และกำหนดสี
    [SerializeField] private bool tintSprite = true;
    [SerializeField] private Color readyTint = new Color32(46, 204, 113, 255); // เขียว
    [SerializeField] private Color notReadyTint = new Color32(231, 76, 60, 255);  // แดง

    private uint targetNetId;

    private void Awake()
    {
        if (kickPlayerButton)
            kickPlayerButton.onClick.AddListener(KickPlayer);
    }

    public void SetKickPlayerButtonVisible(bool visible)
    {
        if (kickPlayerButton)
            kickPlayerButton.gameObject.SetActive(visible);
    }

    public void UpdatePlayer(LobbyRoomPlayer lp)
    {
        if (!lp) return;

        // จำเป้าหมายไว้สำหรับปุ่ม Kick
        targetNetId = lp.netId;

        // ชื่อ
        var name = string.IsNullOrWhiteSpace(lp.displayName) ? "Player" : lp.displayName;
        if (playerNameText) playerNameText.text = name;

        // เป็ด (สี)
        if (characterImage)
            characterImage.sprite = LobbyAssets.Instance ? LobbyAssets.Instance.GetDuckSpriteByIndex(lp.duckColorIndex) : null;

        // สถานะ Ready
        ApplyReadyVisual(lp.readyToBegin);

        // ถ้าเป็นโฮสต์ → ซ่อนไอคอนสถานะ
        if (readyStateImage)
            readyStateImage.gameObject.SetActive(!lp.isHost);
    }

    void ApplyReadyVisual(bool isReady)
    {
        if (!readyStateImage) return;

        // 1) มีสไปรต์ให้สลับ → ใช้สไปรต์ตามสถานะ
        if (readySprite && notReadySprite)
        {
            readyStateImage.sprite = isReady ? readySprite : notReadySprite;
            readyStateImage.enabled = true;

            // จะ tint เพิ่มด้วยไหม
            readyStateImage.color = tintSprite
                ? (isReady ? readyTint : notReadyTint)
                : Color.white; // ไม่ tint ก็ปล่อยเป็นขาว (ไม่ปรับสี)
        }
        // 2) ไม่มีสไปรต์ → ใช้การย้อมสีเป็นตัวบอกสถานะ
        else
        {
            readyStateImage.enabled = true;
            readyStateImage.sprite = readyStateImage.sprite; // คงสไปรต์เดิมไว้ (ถ้ามี)
            readyStateImage.color = isReady ? readyTint : notReadyTint;
        }
    }


    private void KickPlayer()
    {
        if (targetNetId == 0) return;
        var me = LobbyRoomPlayer.Local;
        if (me) me.CmdKickPlayer(targetNetId);
    }
}
