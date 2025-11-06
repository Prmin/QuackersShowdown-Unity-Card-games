using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class TargetFollow : NetworkBehaviour
{


    [SyncVar(hook = nameof(OnTargetNetIdChanged))] 
    public uint targetNetId;
    private RectTransform targetRect;
    private Transform targetTransform;

    void Start()
    {
        targetRect = GetComponent<RectTransform>();
        if (targetRect == null)
        {
            Debug.LogError("[TargetFollow] No RectTransform found!");
        }
    }

    // เมื่อค่า targetNetId เปลี่ยน ทั้งฝั่งเซิร์ฟกับ Client จะเรียก OnTargetNetIdChanged
    void OnTargetNetIdChanged(uint oldValue, uint newValue)
    {
        // รีเซ็ต
        targetTransform = null;
        // Debug.Log($"[TargetFollow] OnTargetNetIdChanged => {oldValue} => {newValue}");
    }

    void Update()
    {
        if (targetTransform == null && targetNetId != 0)
        {
            FindTarget();
        }

        // อัปเดตตำแหน่ง UI
        if (targetTransform != null)
        {
            RectTransform rt = GetComponent<RectTransform>();
            RectTransform cardRt = targetTransform.GetComponent<RectTransform>();
            if (rt != null && cardRt != null)
            {
                rt.anchoredPosition = cardRt.anchoredPosition + new Vector2(0, 150);
            }
        }
    }

    public void ResetTargetTransform()
    {
        targetTransform = null;
    }


    void FindTarget()
    {
        if (targetNetId == 0) return;

        if (!NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
        {
            Debug.LogWarning($"[TargetFollow] Could not find target with netId={targetNetId}");
            return;
        }

        targetTransform = targetIdentity.transform;
        Debug.Log($"[TargetFollow] Found target {targetIdentity.gameObject.name}");
    }
}
