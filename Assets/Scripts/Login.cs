using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // เพิ่มเพื่อใช้งาน Button

public class Login : MonoBehaviour
{
    // ฟิลด์สำหรับการกรอกข้อมูลจาก UI
    public TMP_InputField emailField;
    public TMP_InputField passwordField;

    // ปุ่ม Login
    public Button loginButton; // เพิ่ม Button สำหรับ Login
    public Button backButton;

    // URL สำหรับส่งข้อมูลการเข้าสู่ระบบไปยังเซิร์ฟเวอร์ PHP
    private string loginURL = "http://localhost/backend_Quackers_Showdown/process_login.php";

    // เริ่มต้นเชื่อมโยงปุ่มกับฟังก์ชัน
    void Start()
    {
        loginButton.onClick.AddListener(OnLoginButtonClick);
        backButton.onClick.AddListener(OnBackButtonClick);
    }

    // ฟังก์ชันที่เรียกเมื่อกดปุ่มเข้าสู่ระบบ
    public void OnLoginButtonClick()
    {
        StartCoroutine(LoginUser());
    }
    public void OnBackButtonClick()
    {
        // กลับไปที่หน้า Login
        SceneManager.LoadScene("First_Sceme");
    }

    // Coroutine สำหรับส่งข้อมูลการเข้าสู่ระบบ
    IEnumerator LoginUser()
    {
        WWWForm form = new WWWForm();
        form.AddField("email", emailField.text);
        form.AddField("password", passwordField.text);

        UnityWebRequest www = UnityWebRequest.Post(loginURL, form);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(www.error);
        }
        else
        {
            Debug.Log(www.downloadHandler.text);

            LoginResponse response = JsonUtility.FromJson<LoginResponse>(www.downloadHandler.text);

            if (response.success)
            {
                Debug.Log("Login successful!");
                // ถ้าเข้าสู่ระบบสำเร็จ ไปที่หน้า MainMenu
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                Debug.Log("Login failed: " + response.message);
            }
        }
    }
}

// โครงสร้างข้อมูลที่ใช้รับข้อมูล JSON จากเซิร์ฟเวอร์
[System.Serializable]
public class LoginResponse
{
    public bool success;
    public string message;
}
