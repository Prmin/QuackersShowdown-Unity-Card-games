using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuScene : MonoBehaviour
{
    public Button playButton;
    public Button training_modeButton;
    public Button settingsButton;

    [Header("Settings Popup")]
    [SerializeField] private GameObject settingsPopupPrefab;

    void Start()
    {
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
        if (training_modeButton != null)
            training_modeButton.onClick.AddListener(OnTrainingClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
    }

    void OnDestroy()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(OnPlayClicked);
        if (training_modeButton != null)
            training_modeButton.onClick.RemoveListener(OnTrainingClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
    }

    private void OnPlayClicked()
    {
        UIAudioSfx.PlayButtonClick();
        GoToPlayScene();
    }

    private void OnTrainingClicked()
    {
        UIAudioSfx.PlayButtonClick();
        GoToTraining_modeScene();
    }

    private void OnSettingsClicked()
    {
        UIAudioSfx.PlayButtonClick();
        GoToSettingsScene();
    }

    public void GoToPlayScene()
    {
        SceneManager.LoadScene("LobbyTutorial_Done");
    }

    public void GoToTraining_modeScene()
    {
        SceneManager.LoadScene("Training_mode");
    }

    public void GoToSettingsScene()
    {
        OpenSettingsPopup();
    }

    private void OpenSettingsPopup()
    {
        GameObject popup = ResolveSettingsPopupObject();
        if (popup == null)
        {
            Debug.LogWarning("[MainMenuScene] Settings popup object not found in scene.");
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
