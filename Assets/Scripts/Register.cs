using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class Register : MonoBehaviour
{
    // ฟิลด์สำหรับการกรอกข้อมูลจาก UI (เช่น InputField)
    public TMP_InputField usernameField;
    public TMP_InputField emailField;
    public TMP_InputField passwordField;
    // ปุ่มที่ใช้สำหรับสมัคร
    public Button registerButton;
    public Button backButton;
    // URL สำหรับส่งข้อมูลการลงทะเบียนไปยังเซิร์ฟเวอร์ PHP
    private string registerURL = "http://localhost/backend_Quackers_Showdown/process_register.php";
    void Start()
    {
        // ตั้งค่าให้ปุ่มเรียกฟังก์ชัน OnRegisterButtonClick เมื่อกด
        registerButton.onClick.AddListener(OnRegisterButtonClick);
        backButton.onClick.AddListener(OnBackButtonClick);
    }
    // ฟังก์ชันที่เรียกเมื่อกดปุ่มสมัคร
    public void OnRegisterButtonClick()
    {
        // เรียกใช้ Coroutine เพื่อส่งข้อมูลไปยังเซิร์ฟเวอร์
        StartCoroutine(RegisterUser());
    }
    public void OnBackButtonClick()
    {
        // กลับไปที่หน้า Login
        SceneManager.LoadScene("First_Sceme");
    }
    // Coroutine สำหรับส่งข้อมูลการลงทะเบียน
    IEnumerator RegisterUser()
    {
        // สร้าง WWWForm เพื่อเก็บข้อมูลที่จะส่ง
        WWWForm form = new WWWForm();
        form.AddField("username", usernameField.text); // เพิ่มข้อมูลชื่อผู้ใช้
        form.AddField("email", emailField.text); // เพิ่มข้อมูลอีเมล
        form.AddField("password", passwordField.text); // เพิ่มข้อมูลรหัสผ่าน

        // ส่งข้อมูลไปยังเซิร์ฟเวอร์ด้วย UnityWebRequest (POST method)
        UnityWebRequest www = UnityWebRequest.Post(registerURL, form);

        // รอผลการส่งคำขอ
        yield return www.SendWebRequest();

        // ตรวจสอบผลลัพธ์จากเซิร์ฟเวอร์
        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            // แสดงข้อผิดพลาดหากเกิดปัญหาการเชื่อมต่อหรือข้อผิดพลาดในการประมวลผลคำขอ
            Debug.LogError(www.error);
        }
        else
        {
            // แสดงผลลัพธ์ที่ได้รับจากเซิร์ฟเวอร์
            ;

            // แปลงผลลัพธ์ที่ได้จาก JSON (เช่น การตอบกลับ success, message)
            RegistrationResponse response = JsonUtility.FromJson<RegistrationResponse>(www.downloadHandler.text);

            // ตรวจสอบผลการลงทะเบียนจากเซิร์ฟเวอร์
            if (response.success)
            {
                // การลงทะเบียนสำเร็จ
                ;
                SceneManager.LoadScene("Login_Scene");
            }
            else
            {
                // การลงทะเบียนล้มเหลว แสดงข้อความที่ได้รับจากเซิร์ฟเวอร์
                ;
            }
        }
    }
}

// โครงสร้างข้อมูลที่ใช้รับข้อมูล JSON จากเซิร์ฟเวอร์
[System.Serializable]
public class RegistrationResponse
{
    public bool success;
    public string message;
}

