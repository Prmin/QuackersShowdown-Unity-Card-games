using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialSlideshowUI : MonoBehaviour
{
    [Header("Page Source")]
    [SerializeField] private Image slideImage;
    [SerializeField] private List<Sprite> slideSprites = new List<Sprite>();
    [SerializeField] private List<GameObject> slidePages = new List<GameObject>();

    [Header("Navigation")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private bool hidePreviousButtonOnFirstPage = false;

    [Header("Next Button Visual")]
    [SerializeField] private Image nextButtonIcon;
    [SerializeField] private Sprite nextArrowSprite;
    [SerializeField] private Sprite completeSprite;
    [SerializeField] private TMP_Text nextButtonText;
    [SerializeField] private string nextLabel = string.Empty;
    [SerializeField] private string completeLabel = string.Empty;

    [Header("Optional")]
    [SerializeField] private TMP_Text pageCounterText;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private int currentPageIndex;

    private void Start()
    {
        ResolveReferences();
        DisableNonInteractiveRaycasts();

        if (previousButton != null)
            previousButton.onClick.AddListener(ShowPreviousPage);
        if (nextButton != null)
            nextButton.onClick.AddListener(ShowNextPageOrFinish);
        if (backToMainMenuButton != null)
            backToMainMenuButton.onClick.AddListener(GoToMainMenu);

        Setting.ApplySavedBackgroundToActiveScene();
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveListener(ShowPreviousPage);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(ShowNextPageOrFinish);
        if (backToMainMenuButton != null)
            backToMainMenuButton.onClick.RemoveListener(GoToMainMenu);
    }

    private int GetPageCount()
    {
        if (slidePages != null && slidePages.Count > 0)
            return slidePages.Count;

        if (slideSprites != null && slideSprites.Count > 0)
            return slideSprites.Count;

        return 0;
    }

    private bool IsLastPage()
    {
        int pageCount = GetPageCount();
        return pageCount > 0 && currentPageIndex >= pageCount - 1;
    }

    private void ResolveReferences()
    {
        if (nextButtonIcon == null && nextButton != null)
            nextButtonIcon = nextButton.targetGraphic as Image;
    }

    private void DisableNonInteractiveRaycasts()
    {
        if (slideImage != null)
            slideImage.raycastTarget = false;

        if (slidePages == null || slidePages.Count == 0)
            return;

        for (int i = 0; i < slidePages.Count; i++)
        {
            GameObject page = slidePages[i];
            if (page == null)
                continue;

            Graphic[] graphics = page.GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
            {
                if (graphics[g] != null)
                    graphics[g].raycastTarget = false;
            }
        }
    }

    private void ShowPreviousPage()
    {
        if (currentPageIndex <= 0)
            return;

        UIAudioSfx.PlayButtonClick();
        currentPageIndex--;
        RefreshUI();
    }

    private void ShowNextPageOrFinish()
    {
        if (IsLastPage())
        {
            GoToMainMenu();
            return;
        }

        UIAudioSfx.PlayButtonClick();
        currentPageIndex++;
        RefreshUI();
    }

    private void GoToMainMenu()
    {
        UIAudioSfx.PlayButtonClick();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void RefreshUI()
    {
        int pageCount = GetPageCount();

        if (pageCount <= 0)
        {
            if (slideImage != null)
                slideImage.enabled = false;
            UpdateButtonsForEmptyState();
            return;
        }

        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, pageCount - 1);

        if (slideImage != null)
        {
            if (slideSprites != null && slideSprites.Count > currentPageIndex)
            {
                slideImage.enabled = true;
                slideImage.sprite = slideSprites[currentPageIndex];
            }
            else if (slidePages == null || slidePages.Count == 0)
            {
                slideImage.enabled = false;
            }
        }

        if (slidePages != null && slidePages.Count > 0)
        {
            for (int i = 0; i < slidePages.Count; i++)
            {
                if (slidePages[i] != null)
                    slidePages[i].SetActive(i == currentPageIndex);
            }
        }

        bool isFirstPage = currentPageIndex <= 0;
        bool isLastPage = currentPageIndex >= pageCount - 1;

        if (previousButton != null)
        {
            previousButton.interactable = !isFirstPage;
            if (hidePreviousButtonOnFirstPage)
                previousButton.gameObject.SetActive(!isFirstPage);
        }

        if (nextButton != null)
            nextButton.interactable = true;

        if (nextButtonIcon != null)
        {
            Sprite desiredSprite = isLastPage ? completeSprite : nextArrowSprite;
            if (desiredSprite != null)
                nextButtonIcon.sprite = desiredSprite;
        }

        if (nextButtonText != null)
        {
            nextButtonText.text = isLastPage ? completeLabel : nextLabel;
        }

        if (pageCounterText != null)
            pageCounterText.text = string.Format("{0}/{1}", currentPageIndex + 1, pageCount);
    }

    private void UpdateButtonsForEmptyState()
    {
        if (previousButton != null)
        {
            previousButton.interactable = false;
            if (hidePreviousButtonOnFirstPage)
                previousButton.gameObject.SetActive(false);
        }

        if (nextButton != null)
            nextButton.interactable = false;

        if (pageCounterText != null)
            pageCounterText.text = "0/0";
    }
}
