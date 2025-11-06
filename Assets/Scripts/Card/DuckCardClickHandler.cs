using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class DuckCardClickHandler : MonoBehaviour
{
    public event Action<GameObject> OnDuckCardClicked;

    private void OnMouseDown()
    {
        // เมื่อการ์ดถูกคลิก เรียกใช้อีเวนต์
        OnDuckCardClicked?.Invoke(gameObject);
    }
}
