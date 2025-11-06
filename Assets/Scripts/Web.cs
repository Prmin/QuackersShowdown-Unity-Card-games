using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Web : MonoBehaviour
{
    // ฟังก์ชัน public ที่ถูกต้อง
    public IEnumerator Login(string email, string password)
    {
        WWWForm form = new WWWForm();
        form.AddField("email", email);
        form.AddField("password", password);

        // URL สำหรับการโพสต์ข้อมูล
        UnityWebRequest www = UnityWebRequest.Post("http://localhost/backend_Quackers_Showdown/process_login.php", form);

        // ส่งคำขอไปยังเซิร์ฟเวอร์
        yield return www.SendWebRequest();

        // ตรวจสอบว่าคำขอเสร็จสมบูรณ์หรือไม่
        if (www.result == UnityWebRequest.Result.Success)
        {
            // แสดงผลลัพธ์จากเซิร์ฟเวอร์ (ในกรณีนี้คือ JSON)
            Debug.Log("Response: " + www.downloadHandler.text);

            // สามารถนำ JSON ที่ได้รับมาตรวจสอบได้
            // เช่น {"success": false, "message": "Invalid password"}
        }
        else
        {
            // แสดงข้อผิดพลาดกรณีคำขอล้มเหลว
            Debug.LogError("Error: " + www.error);
        }


    }
}
