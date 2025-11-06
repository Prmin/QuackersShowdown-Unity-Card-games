using Mirror;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    [Header("Scene References")]
    public Transform duckZone;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}