using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;


public class TargetDebug : NetworkBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // Debug.Log($"[TargetDebug] {gameObject.name} netId={GetComponent<NetworkIdentity>().netId} in Start()");

    }
    void Awake()
    {
        // Debug.Log($"[TargetDebug] Awake on {gameObject.name}");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Debug.Log($"[TargetDebug] OnStartClient: {gameObject.name}, netId={netId}, isOwned={isOwned}");
    }

}
