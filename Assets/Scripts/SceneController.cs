using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // สำหรับ Button

public class SceneController : MonoBehaviour
{
    // อ้างอิงถึงปุ่มต่างๆ
    public Button loginButton;
    public Button registerButton;
    public Button settingsButton;

    void Start()
    {
        // ผูกฟังก์ชันให้กับการคลิกของปุ่ม
        loginButton.onClick.AddListener(GoToLoginScene);
        registerButton.onClick.AddListener(GoToRegisterScene);
        settingsButton.onClick.AddListener(GoToSettingsScene);
    }

    // ฟังก์ชันสำหรับการเปลี่ยน Scene
    public void GoToLoginScene()
    {
        SceneManager.LoadScene("Login_Scene");
    }

    public void GoToRegisterScene()
    {
        SceneManager.LoadScene("Register_Scene");
    }

    public void GoToSettingsScene()
    {
        SceneManager.LoadScene("Setting_Scene");
    }
}
