using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameplayPauseMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button exitMatchButton;
    [SerializeField] private Button confirmExitYesButton;
    [SerializeField] private Button confirmExitNoButton;

    [Header("Popups")]
    [SerializeField] private GameObject settingsPopup;
    [SerializeField] private GameObject exitConfirmPopup;

    [Header("Behavior")]
    [SerializeField] private bool overrideExitButtonListeners = true;
    [SerializeField] private bool closeSettingsWhenAskingConfirm = true;
    [SerializeField] private string lobbySceneName = "LobbyTutorial_Done";

    private bool _isLeaving;
    private bool _sceneLoadHooked;

    private void Awake()
    {
        if (openSettingsButton != null)
            openSettingsButton.onClick.AddListener(OpenSettingsPopup);

        if (exitMatchButton != null)
        {
            if (overrideExitButtonListeners)
                exitMatchButton.onClick.RemoveAllListeners();

            exitMatchButton.onClick.AddListener(OpenExitConfirmPopup);
        }

        if (confirmExitYesButton != null)
            confirmExitYesButton.onClick.AddListener(ConfirmExitMatch);

        if (confirmExitNoButton != null)
            confirmExitNoButton.onClick.AddListener(CancelExitMatch);

        if (exitConfirmPopup != null)
            exitConfirmPopup.SetActive(false);
    }

    private void OnDestroy()
    {
        if (openSettingsButton != null)
            openSettingsButton.onClick.RemoveListener(OpenSettingsPopup);

        if (exitMatchButton != null)
            exitMatchButton.onClick.RemoveListener(OpenExitConfirmPopup);

        if (confirmExitYesButton != null)
            confirmExitYesButton.onClick.RemoveListener(ConfirmExitMatch);

        if (confirmExitNoButton != null)
            confirmExitNoButton.onClick.RemoveListener(CancelExitMatch);

        if (_sceneLoadHooked)
        {
            SceneManager.sceneLoaded -= OnSceneLoadedShowLobbyList;
            _sceneLoadHooked = false;
        }
    }

    private void OpenSettingsPopup()
    {
        UIAudioSfx.PlayButtonClick();

        GameObject popup = ResolveSettingsPopupObject();
        if (popup == null)
        {
            Debug.LogWarning("[GameplayPauseMenuUI] Settings popup not found.");
            return;
        }

        popup.SetActive(true);
        BringToFront(popup);
    }

    private void OpenExitConfirmPopup()
    {
        UIAudioSfx.PlayButtonClick();

        if (closeSettingsWhenAskingConfirm && settingsPopup != null)
            settingsPopup.SetActive(false);

        if (exitConfirmPopup == null)
        {
            Debug.LogWarning("[GameplayPauseMenuUI] Exit confirm popup not assigned.");
            return;
        }

        exitConfirmPopup.SetActive(true);
        BringToFront(exitConfirmPopup);
    }

    private void CancelExitMatch()
    {
        UIAudioSfx.PlayButtonClick();

        if (exitConfirmPopup != null)
            exitConfirmPopup.SetActive(false);
    }

    private void ConfirmExitMatch()
    {
        if (_isLeaving)
            return;

        UIAudioSfx.PlayButtonClick();
        _isLeaving = true;

        if (exitConfirmPopup != null)
            exitConfirmPopup.SetActive(false);

        UIFlow.I?.HideAllForGameplay();

        NetworkManager manager = NetworkManager.singleton;
        if (manager != null)
        {
            if (NetworkServer.active && NetworkClient.isConnected)
                manager.StopHost();
            else if (NetworkClient.isConnected)
                manager.StopClient();
            else if (NetworkServer.active)
                manager.StopServer();
        }

        if (!_sceneLoadHooked)
        {
            SceneManager.sceneLoaded += OnSceneLoadedShowLobbyList;
            _sceneLoadHooked = true;
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    private void OnSceneLoadedShowLobbyList(Scene scene, LoadSceneMode mode)
    {
        string expectedName = lobbySceneName;
        int slash = expectedName.LastIndexOf('/');
        if (slash >= 0 && slash < expectedName.Length - 1)
            expectedName = expectedName.Substring(slash + 1);

        int dot = expectedName.LastIndexOf('.');
        if (dot > 0)
            expectedName = expectedName.Substring(0, dot);

        if (!string.Equals(scene.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
            return;

        SceneManager.sceneLoaded -= OnSceneLoadedShowLobbyList;
        _sceneLoadHooked = false;
        UIFlow.I?.ShowLobbyList();
    }

    private GameObject ResolveSettingsPopupObject()
    {
        if (settingsPopup != null && settingsPopup.scene.IsValid())
            return settingsPopup;

        if (settingsPopup != null)
        {
            GameObject byName = GameObject.Find(settingsPopup.name);
            if (byName != null)
            {
                settingsPopup = byName;
                return settingsPopup;
            }
        }

        GameObject found = GameObject.Find("SettingsPopup");
        if (found != null)
        {
            settingsPopup = found;
            return settingsPopup;
        }

        found = GameObject.Find("SettingsPopup 1");
        if (found != null)
        {
            settingsPopup = found;
            return settingsPopup;
        }

        return null;
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
