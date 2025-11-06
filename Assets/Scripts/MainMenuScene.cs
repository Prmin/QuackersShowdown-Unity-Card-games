using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // สำหรับ Button

public class MainMenuScene : MonoBehaviour
{
    // อ้างอิงถึงปุ่มต่างๆ
    public Button playButton;
    public Button training_modeButton;
    public Button settingsButton;

    void Start()
    {
        // ผูกฟังก์ชันให้กับการคลิกของปุ่ม
        playButton.onClick.AddListener(GoToPlayScene);
        training_modeButton.onClick.AddListener(GoToTraining_modeScene);
        settingsButton.onClick.AddListener(GoToSettingsScene);
    }

    // ฟังก์ชันสำหรับการเปลี่ยน Scene
    public void GoToPlayScene()
    {
        SceneManager.LoadScene("PlayScene");
    }

    public void GoToTraining_modeScene()
    {
        SceneManager.LoadScene("Training_mode");
    }

    public void GoToSettingsScene()
    {
        SceneManager.LoadScene("Setting_Scene");
    }
}
