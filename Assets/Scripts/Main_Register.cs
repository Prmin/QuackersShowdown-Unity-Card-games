using UnityEngine; // ต้องแน่ใจว่ามีการนำเข้า UnityEngine

public class Main_Register : MonoBehaviour
{
    public static Main_Register Instance; 
    public Register register;

    void Awake()
    {
        // ตั้งค่า Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // ค้นหา Register component ใน Input GameObject
        if (register == null)
        {
            GameObject inputObject = GameObject.Find("Input");
            if (inputObject != null)
            {
                register = inputObject.GetComponent<Register>();
            }

            if (register == null)
            {
                Debug.LogError("ไม่พบ Register component ใน Input GameObject.");
            }
        }
    }
}
