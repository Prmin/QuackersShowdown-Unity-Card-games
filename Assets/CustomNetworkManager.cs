using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class CustomNetworkManager : NetworkManager
{
    public GameObject duckCardPrefab; // Assign DuckCard Prefab in Inspector

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // เรียกใช้งานฟังก์ชันของคลาสแม่เพื่อเพิ่ม Player
        base.OnServerAddPlayer(conn);

        // Spawn DuckCard เมื่อผู้เล่นถูกเพิ่ม
        if (duckCardPrefab != null)
        {
            GameObject duckCard = Instantiate(duckCardPrefab);
            NetworkServer.Spawn(duckCard);
            ;
        }
        else
        {
            Debug.LogError("DuckCard Prefab is not assigned!");
        }
    }
}
