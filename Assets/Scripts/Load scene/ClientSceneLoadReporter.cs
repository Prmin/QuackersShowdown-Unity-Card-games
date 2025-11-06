using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerManager))]
public class ClientSceneLoadReporter : NetworkBehaviour
{
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        // แสดง overlay ทันที
        SceneLoadingOverlay.EnsureShown("Loading...");
        StartCoroutine(ReportAfterSettle());
    }

    private IEnumerator ReportAfterSettle()
    {
        // เว้นเฟรมให้ทุกอย่างในซีนลงตัว (UI/Spawner/etc.)
        yield return null;
        yield return new WaitForSeconds(0.05f);

        // ขอให้เซิร์ฟเวอร์บันทึกว่า "ผู้เล่นนี้พร้อมแล้ว"
        CmdReportReady();
    }

    [Command]
    private void CmdReportReady()
    {
        var coord = GameplayLoadCoordinator.Instance;
        if (coord != null)
        {
            coord.ServerMarkReady(connectionToClient.identity);
        }
    }
}
