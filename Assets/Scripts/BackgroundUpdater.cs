using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundUpdater : MonoBehaviour
{
    public Settings_Manager settingsManager; // เชื่อมโยง Settings_Manager
    public Image backgroundImage; // อ้างอิงไปยัง Image component

    void Start()
    {
        UpdateBackground();
    }

    public void UpdateBackground()
    {
        if (settingsManager != null)
        {
            settingsManager.ApplyBackground(backgroundImage);
        }
        else
        {
            Debug.LogError("Settings_Manager is not assigned in BackgroundUpdater.");
        }
    }
}
