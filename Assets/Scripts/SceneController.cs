using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneController : MonoBehaviour
{
    public Button loginButton;
    public Button registerButton;
    public Button settingsButton;

    [Header("Settings Popup")]
    [SerializeField] private GameObject settingsPopupPrefab;

    void Start()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(GoToLoginScene);
        if (registerButton != null)
            registerButton.onClick.AddListener(GoToRegisterScene);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(GoToSettingsScene);
    }

    void OnDestroy()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(GoToLoginScene);
        if (registerButton != null)
            registerButton.onClick.RemoveListener(GoToRegisterScene);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(GoToSettingsScene);
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
        GameObject popup = ResolveSettingsPopupObject();
        if (popup == null)
        {
            Debug.LogWarning("[SceneController] Settings popup object not found in scene.");
            return;
        }

        popup.SetActive(true);
        BringToFront(popup);
    }

    private GameObject ResolveSettingsPopupObject()
    {
        if (settingsPopupPrefab != null && settingsPopupPrefab.scene.IsValid())
            return settingsPopupPrefab;

        if (settingsPopupPrefab != null)
        {
            GameObject byName = GameObject.Find(settingsPopupPrefab.name);
            if (byName != null)
                return byName;
        }

        return GameObject.Find("SettingsPopup");
    }

    private static void BringToFront(GameObject popup)
    {
        if (popup == null)
            return;

        RectTransform rect = popup.transform as RectTransform;
        if (rect != null)
            rect.SetAsLastSibling();
    }
}
