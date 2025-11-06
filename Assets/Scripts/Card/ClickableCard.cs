using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Mirror;
using System;  


public class ClickableCard : MonoBehaviour
{
    public bool IsInteractable { get; private set; } = false;
    public event Action<GameObject> OnCardClicked;

    void Start()
    {
        IsInteractable = false;
        // หรือเริ่มให้ false
    }

    public void EnableInteraction()
    {
        IsInteractable = true;
        Debug.Log($"Interaction enabled for card: {this.name}");
    }

    public void DisableInteraction()
    {
        IsInteractable = false;
        Debug.Log($"Interaction disabled for card: {this.name}");
    }

    // ตัวอย่าง OnMouseDown (หรือจะใช้ PointerDown ก็ได้)
    void OnMouseDown()
    {
        if (!IsInteractable)
        {
            Debug.Log("Card is not interactable.");
            return;
        }
        // หา PlayerManager ของ local player
        if (NetworkClient.localPlayer == null)
        {
            Debug.LogWarning("No local player found!");
            return;
        }
        PlayerManager localPM = NetworkClient.localPlayer.GetComponent<PlayerManager>();
        if (localPM == null)
        {
            Debug.LogWarning("No PlayerManager in local player!");
            return;
        }
        Debug.Log($"Clicked card: {this.name}");
        // เรียก OnDuckCardClicked ใน "local" PlayerManager
        OnCardClicked?.Invoke(this.gameObject);
    }
}
