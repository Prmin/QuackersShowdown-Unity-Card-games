using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // สำหรับ Button

public class PlayScene : MonoBehaviour
{
    // อ้างอิงถึงปุ่มต่างๆ
    public Button CreateButton;
    public Button SearchButton;
    public Button backButton;

    void Start()
    {
        // ผูกฟังก์ชันให้กับการคลิกของปุ่ม
        CreateButton.onClick.AddListener(GoToCreate);
        SearchButton.onClick.AddListener(GoToSearch);
        backButton.onClick.AddListener(GoToMainMenuScene);
    }

    // ฟังก์ชันสำหรับการเปลี่ยน Scene
    public void GoToCreate()
    {
        SceneManager.LoadScene("Create");
    }

    public void GoToSearch()
    {
        SceneManager.LoadScene("Search");
    }

    public void GoToMainMenuScene()
    {   
        SceneManager.LoadScene("MainMenu");
    }
    
}
