using UnityEngine;

[RequireComponent(typeof(DragDrop))]   // ใช้ DragDrop เดิมของนายเลย
public class LocalHandCard : MonoBehaviour
{
    PlayerManager _owner;
    string _cardKey;
    DragDrop _dragDrop;

    public void Initialize(PlayerManager owner, string cardKey)
    {
        _owner   = owner;
        _cardKey = cardKey;

        _dragDrop = GetComponent<DragDrop>();
        _dragDrop.SetLocalHandMode(this);
    }

    // เรียกจาก DragDrop ตอน "เล่นการ์ดสำเร็จ"
    public void OnPlayedFromHand()
    {
        if (_owner != null)
        {
            _owner.CmdPlayActionCard(_cardKey);
        }

        Destroy(gameObject); // หายจากมือเรา
    }
}
