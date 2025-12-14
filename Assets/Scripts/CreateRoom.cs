using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;

public class CreateRoom : MonoBehaviour
{
    public TMP_InputField roomNameInput;
    public Toggle passwordToggle;
    public TMP_Dropdown playerCountDropdown;
    public Button createRoomButton;
    private string generatedPassword;
    void Start()
    {
        createRoomButton.onClick.AddListener(CreateRoomMethod); 
    }

    void CreateRoomMethod()
    {
        if (roomNameInput == null || passwordToggle == null || playerCountDropdown == null || createRoomButton == null)
        {
            Debug.LogError("One or more references are not set in the inspector!");
            return; 
        }
        string roomName = roomNameInput.text;
        int playerCount = playerCountDropdown.value + 3; // Dropdown starts from 0 (3 players)
        if (passwordToggle.isOn)
        {
            generatedPassword = GenerateRandomPassword();
            ;
        }
        else
        {
            generatedPassword = null; // ไม่มีรหัสผ่าน
        }
   // เรียกใช้ฟังก์ชันการสร้างห้องจากฝั่งเซิร์ฟเวอร์
        CreateRoomOnServer(roomName, generatedPassword, playerCount);
    }
    string GenerateRandomPassword(int length = 10)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] stringChars = new char[length];
        System.Random random = new System.Random();

        for (int i = 0; i < stringChars.Length; i++)
        {
            stringChars[i] = chars[random.Next(chars.Length)];
        }
        return new string(stringChars);
    }
    void CreateRoomOnServer(string roomName, string password, int playerCount)
    {
        int hostUserId = 1; // เปลี่ยนให้เป็น ID ของผู้เล่นที่สร้างห้อง
        StartCoroutine(SendRoomDataToServer(roomName, password, playerCount, hostUserId));
    }
    IEnumerator SendRoomDataToServer(string roomName, string password, int playerCount, int hostUserId)
    {
        WWWForm form = new WWWForm();
        form.AddField("roomName", roomName);
        form.AddField("roomCode", password); // ใช้รหัสผ่านเป็น roomCode
        form.AddField("hostUserId", hostUserId);
        form.AddField("playerCount", playerCount); // ส่งจำนวนผู้เล่น
        using (UnityWebRequest www = UnityWebRequest.Post("http://localhost/backend_Quackers_Showdown/create_room.php", form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error while creating room: {www.error}");
            }
            else
            {
                ;
                ; // แสดงผลตอบกลับจากเซิร์ฟเวอร์
            }
        }
    }
}

