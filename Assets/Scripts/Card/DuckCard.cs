using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mirror;

public class DuckCard : NetworkBehaviour, IPointerClickHandler
{
    [SyncVar(hook = nameof(OnOwnerChanged))] public uint ownerNetId;
    [SyncVar(hook = nameof(OnZoneChanged))] public ZoneKind zone = ZoneKind.None;
    [SyncVar(hook = nameof(OnZoneIndexChanged))] public int zoneIndex = -1;

    // Board positioning that is still logical-only; clients layout from these values.
    [SyncVar(hook = nameof(OnRowChanged))] public int RowNet;
    [SyncVar(hook = nameof(OnColChanged))] public int ColNet;

    private Coroutine _layoutCoroutine;
    private const float ManualSpacingX = 150f;

    private PlayerManager GetOwnerPlayerManager()
    {
        if (ownerNetId == 0) return null;
        return NetworkClient.spawned.TryGetValue(ownerNetId, out var ownerIdentity)
            ? ownerIdentity.GetComponent<PlayerManager>()
            : null;
    }

    private bool IsOwnedByLocalPlayer()
    {
        uint localNetId = PlayerManager.LocalPlayerNetId;
        return localNetId != 0 && ownerNetId == localNetId;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[DuckCard] Spawned name={name} netId={netId} owner={ownerNetId} zone={zone} index={zoneIndex}");
        TryApplyLayout("OnStartClient");
    }

    // SyncVar hooks -------------------------------------------------------
    private void OnOwnerChanged(uint oldValue, uint newValue)
    {
        Debug.Log($"[DuckCard] Owner changed {oldValue}->{newValue} for {name}");
        HandleStateChanged("OwnerChanged");
    }

    private void OnZoneChanged(ZoneKind oldZone, ZoneKind newZone)
    {
        Debug.Log($"[DuckCard] Zone changed {oldZone}->{newZone} for {name}");
        HandleStateChanged("ZoneChanged");
    }

    private void OnZoneIndexChanged(int oldIndex, int newIndex)
    {
        Debug.Log($"[DuckCard] ZoneIndex changed {oldIndex}->{newIndex} for {name}");
        HandleStateChanged("ZoneIndexChanged");
    }

    private void OnRowChanged(int oldRow, int newRow)
    {
        HandleStateChanged("RowChanged");
    }

    private void OnColChanged(int oldCol, int newCol)
    {
        if (isServer && zoneIndex != newCol)
            zoneIndex = newCol; // keep logical order in sync with column updates
        HandleStateChanged("ColChanged");
    }

    private void HandleStateChanged(string reason)
    {
        if (!NetworkClient.active) return;
        TryApplyLayout(reason);
    }

    // Layout --------------------------------------------------------------
    private void TryApplyLayout(string reason)
    {
        if (_layoutCoroutine != null)
        {
            StopCoroutine(_layoutCoroutine);
            _layoutCoroutine = null;
        }

        if (!ApplyLayout(reason))
            _layoutCoroutine = StartCoroutine(ApplyLayoutRoutine(reason));
    }

    private IEnumerator ApplyLayoutRoutine(string reason)
    {
        for (int i = 0; i < 20; i++)
        {
            yield return new WaitForSeconds(0.05f);
            if (ApplyLayout($"{reason} retry#{i + 1}"))
                yield break;
        }

        Debug.LogError($"[DuckCard] {name} (netId={netId}) could not layout for zone={zone} owner={ownerNetId}");
    }

    private bool ApplyLayout(string reason)
    {
        var rect = transform as RectTransform;
        if (rect == null) return false;

        var parent = ResolveZoneParent();
        if (parent == null)
        {
            Debug.LogWarning($"[DuckCard] Parent missing for {name} zone={zone} owner={ownerNetId} reason={reason}");
            return false;
        }

        rect.SetParent(parent, false);

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        bool parentHasLayout = parent.GetComponent<LayoutGroup>() != null;

        rect.anchoredPosition = Vector2.zero;
        rect.anchoredPosition3D = new Vector3(rect.anchoredPosition3D.x, rect.anchoredPosition3D.y, 0f);

        if (!parentHasLayout && (zone == ZoneKind.DuckZone || zone == ZoneKind.DropZone || zone == ZoneKind.TargetZone))
        {
            rect.anchoredPosition3D = new Vector3(ColNet * ManualSpacingX, 0f, 0f);
        }

        int targetIndex = zoneIndex;
        if (targetIndex < 0 || targetIndex > parent.childCount)
            targetIndex = parent.childCount;
        transform.SetSiblingIndex(targetIndex);

        LogLayout(reason, parent, rect);
        return true;
    }

    private void LogLayout(string reason, Transform parent, RectTransform rect)
    {
        Debug.Log($"[DuckCard] Layout {reason} | name={name} netId={netId} owner={ownerNetId} zone={zone} index={zoneIndex} parent={(parent != null ? parent.name : "NULL")} anchored={rect.anchoredPosition} sibling={transform.GetSiblingIndex()}");
    }

    private Transform ResolveZoneParent()
    {
        var localPM = PlayerManager.localInstance;
        bool ownedByLocal = IsOwnedByLocalPlayer();

        switch (zone)
        {
            case ZoneKind.PlayerArea:
                if (ownedByLocal)
                    return localPM?.PlayerArea?.transform ?? FindZoneRecursive(ZoneKind.PlayerArea);

                var mapped = PlayerManager.TryGetEnemySlotForNetId(ownerNetId);
                if (mapped != null) return mapped;

                var ownerPM = GetOwnerPlayerManager();
                if (ownerPM != null && ownerPM.EnemyArea != null)
                    return ownerPM.EnemyArea.transform;

                return localPM?.EnemyArea?.transform ?? FindZoneRecursive(ZoneKind.PlayerArea, "EnemyArea");

            case ZoneKind.DuckZone:
                return localPM?.DuckZone?.transform ?? FindZoneRecursive(ZoneKind.DuckZone);

            case ZoneKind.DropZone:
                return localPM?.DropZone?.transform ?? FindZoneRecursive(ZoneKind.DropZone);

            case ZoneKind.TargetZone:
                return localPM?.TargetZone?.transform ?? FindZoneRecursive(ZoneKind.TargetZone);

            default:
                return null;
        }
    }

    private Transform FindZoneRecursive(ZoneKind zoneKind, string overrideName = null)
    {
        Transform mainCanvas = GameObject.Find("Main Canvas")?.transform ?? GameObject.Find("Canvas")?.transform;
        if (mainCanvas == null) return null;

        Transform root = FindChildRecursive(mainCanvas, "Image") ?? mainCanvas;
        string zoneName = overrideName ?? zoneKind.ToString();
        return FindChildRecursive(root, zoneName);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            var found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    // Server-side assignment is purely logical; clients recompute layout locally.
    [Server]
    public void ServerAssignToZone(ZoneKind newZone, int row, int col)
    {
        zone = newZone;
        RowNet = row;
        ColNet = col;
        zoneIndex = col;
    }

    public int Row
    {
        get => RowNet;
        set
        {
            if (!isServer) { Debug.LogWarning("[DuckCard] Row set on client ignored"); return; }
            RowNet = value;
        }
    }

    public int Column
    {
        get => ColNet;
        set
        {
            if (!isServer) { Debug.LogWarning("[DuckCard] Column set on client ignored"); return; }
            ColNet = value;
            zoneIndex = value;
        }
    }

    // Interaction ---------------------------------------------------------
    public void OnPointerClick(PointerEventData eventData)
    {
        var localPM = NetworkClient.connection?.identity?.GetComponent<PlayerManager>() ?? PlayerManager.localInstance;
        if (localPM == null)
        {
            Debug.LogWarning("[DuckCard] No local PlayerManager found, can't click.");
            return;
        }

        localPM.HandleDuckCardClick(this);
    }
}
