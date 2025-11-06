using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour
{
    public static Main Instance; // ใช้ static เพื่อเข้าถึงจากที่อื่น
    public Web Web;

    void Awake()
    {
        // กำหนด Instance
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // ตรวจสอบให้แน่ใจว่า Web ไม่เป็น null
        if (Web == null)
        {
            Web = GetComponent<Web>();
            if (Web == null)
            {
                Debug.LogError("Web component not found on this GameObject.");
            }
        }
    }
}
