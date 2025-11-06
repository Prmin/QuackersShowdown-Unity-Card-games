using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UI;

public class TargetCoverFollow : NetworkBehaviour
{
    [SyncVar] public uint netIdA;
    [SyncVar] public uint netIdB;

    RectTransform coverRect;
    RectTransform zoneRect;
    Canvas canvas;

    void Awake()
    {
        coverRect = GetComponent<RectTransform>();
        var zoneObj = GameObject.Find("TargetCoverZone");
        zoneRect = zoneObj.GetComponent<RectTransform>();
        canvas = zoneRect.GetComponentInParent<Canvas>();
    }

    void Update()
    {
        if (netIdA == 0 || netIdB == 0) return;
        if (!NetworkClient.spawned.TryGetValue(netIdA, out var aNi)) return;
        if (!NetworkClient.spawned.TryGetValue(netIdB, out var bNi)) return;

        var aRT = aNi.GetComponent<RectTransform>();
        var bRT = bNi.GetComponent<RectTransform>();
        if (aRT == null || bRT == null) return;

        // คำนวณ midpoint
        Vector3 midWorld = (aRT.position + bRT.position) * 0.5f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            zoneRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, midWorld),
            canvas.worldCamera,
            out Vector2 localPoint
        );

        coverRect.anchoredPosition = localPoint;
    }
}
