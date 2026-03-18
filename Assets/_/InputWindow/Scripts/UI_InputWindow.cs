/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CodeMonkey.Utils;
using TMPro;
using UnityEngine.SceneManagement;

public class UI_InputWindow : MonoBehaviour
{

    private static UI_InputWindow instance;

    private Button_UI okBtn;
    private Button_UI cancelBtn;
    private TextMeshProUGUI titleText;
    private TMP_InputField inputField;
    private RectTransform windowRectTransform;
    private Canvas parentCanvas;
    private Action cancelAction;
    private Action<string> okAction;
    private int ignoreOutsideClickUntilFrame = -1;

    private void Awake()
    {
        // Keep scene-local behavior: whichever scene instance wakes last becomes active.
        instance = this;

        okBtn = transform.Find("okBtn").GetComponent<Button_UI>();
        cancelBtn = transform.Find("cancelBtn").GetComponent<Button_UI>();
        titleText = transform.Find("titleText").GetComponent<TextMeshProUGUI>();
        inputField = transform.Find("inputField").GetComponent<TMP_InputField>();
        windowRectTransform = transform as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
        cancelAction = null;
        okAction = null;
        ignoreOutsideClickUntilFrame = -1;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Confirm();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
        }

        if (Time.frameCount <= ignoreOutsideClickUntilFrame)
        {
            return;
        }

        if (TryGetPointerDownPosition(out Vector2 screenPosition) && !IsInsideWindow(screenPosition))
        {
            Cancel();
        }
    }

    private void Show(string titleString, string inputString, string validCharacters, int characterLimit, Action onCancel, Action<string> onOk)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        titleText.text = titleString;

        inputField.characterLimit = characterLimit;
        if (string.IsNullOrEmpty(validCharacters))
        {
            inputField.onValidateInput = null;
        }
        else
        {
            inputField.onValidateInput = (string text, int charIndex, char addedChar) =>
            {
                return ValidateChar(validCharacters, addedChar);
            };
        }

        inputField.text = inputString;
        inputField.Select();
        inputField.ActivateInputField();
        ignoreOutsideClickUntilFrame = Time.frameCount + 1;

        cancelAction = onCancel;
        okAction = onOk;

        okBtn.ClickFunc = Confirm;
        cancelBtn.ClickFunc = Cancel;
    }

    private void Hide()
    {
        cancelAction = null;
        okAction = null;
        ignoreOutsideClickUntilFrame = -1;
        gameObject.SetActive(false);
    }

    private void Confirm()
    {
        string value = inputField != null ? inputField.text : string.Empty;
        Action<string> callback = okAction;
        Hide();
        callback?.Invoke(value);
    }

    private void Cancel()
    {
        Action callback = cancelAction;
        Hide();
        callback?.Invoke();
    }

    private bool TryGetPointerDownPosition(out Vector2 screenPosition)
    {
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    screenPosition = touch.position;
                    return true;
                }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        screenPosition = Vector2.zero;
        return false;
    }

    private bool IsInsideWindow(Vector2 screenPosition)
    {
        if (windowRectTransform == null)
        {
            return false;
        }

        Camera eventCamera = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = parentCanvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(windowRectTransform, screenPosition, eventCamera);
    }

    private char ValidateChar(string validCharacters, char addedChar)
    {
        // Allow all characters when no whitelist is provided.
        if (string.IsNullOrEmpty(validCharacters))
        {
            return addedChar;
        }

        // For non-numeric whitelist inputs, always allow Thai characters.
        // This avoids blocking Thai typing when callers still provide legacy ASCII whitelists.
        if (!IsNumericWhitelist(validCharacters) && IsThaiCharacter(addedChar))
        {
            return addedChar;
        }

        if (validCharacters.IndexOf(addedChar) != -1)
        {
            // Valid
            return addedChar;
        }
        else
        {
            // Invalid
            return '\0';
        }
    }

    private static bool IsThaiCharacter(char c)
    {
        return c >= '\u0E00' && c <= '\u0E7F';
    }

    private static bool IsNumericWhitelist(string validCharacters)
    {
        if (string.IsNullOrEmpty(validCharacters)) return false;

        for (int i = 0; i < validCharacters.Length; i++)
        {
            char c = validCharacters[i];
            if (!char.IsDigit(c) && c != '-' && c != '+')
            {
                return false;
            }
        }

        return true;
    }

    public static void Show_Static(string titleString, string inputString, string validCharacters, int characterLimit, Action onCancel, Action<string> onOk)
    {
        UI_InputWindow target = ResolveInstance();
        if (target == null)
        {
            Debug.LogError("[UI_InputWindow] No active instance found in scene.");
            onCancel?.Invoke();
            return;
        }

        target.Show(titleString, inputString, validCharacters, characterLimit, onCancel, onOk);
    }

    public static void Show_Static(string titleString, int defaultInt, Action onCancel, Action<int> onOk)
    {
        UI_InputWindow target = ResolveInstance();
        if (target == null)
        {
            Debug.LogError("[UI_InputWindow] No active instance found in scene.");
            onCancel?.Invoke();
            return;
        }

        target.Show(titleString, defaultInt.ToString(), "0123456789-", 20, onCancel,
            (string inputText) =>
            {
                // Try to Parse input string
                if (int.TryParse(inputText, out int _i))
                {
                    onOk(_i);
                }
                else
                {
                    onOk(defaultInt);
                }
            }
        );
    }

    private static UI_InputWindow ResolveInstance()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (instance != null)
        {
            if (instance.gameObject.scene.IsValid() && instance.gameObject.scene == activeScene)
                return instance;
        }

        UI_InputWindow[] all = Resources.FindObjectsOfTypeAll<UI_InputWindow>();

        // 1) Prefer an instance that belongs to the active scene.
        for (int i = 0; i < all.Length; i++)
        {
            UI_InputWindow candidate = all[i];
            if (candidate == null)
                continue;

            if (!candidate.gameObject.scene.IsValid())
                continue;

            if (candidate.gameObject.scene == activeScene)
            {
                instance = candidate;
                break;
            }
        }

        if (instance != null)
            return instance;

        // 2) Fallback to any valid scene object (including DontDestroyOnLoad).
        for (int i = 0; i < all.Length; i++)
        {
            UI_InputWindow candidate = all[i];
            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                instance = candidate;
                break;
            }
        }

        return instance;
    }
}
