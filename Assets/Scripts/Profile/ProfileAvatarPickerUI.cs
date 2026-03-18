using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ProfileAvatarPickerUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Layout")]
    [SerializeField] private RectTransform dialogRoot;
    [SerializeField] private Transform avatarButtonContainer;
    [SerializeField] private Button avatarButtonTemplate;
    [SerializeField] private Button closeButton;

    [Header("Avatar Render")]
    [SerializeField] private string avatarImageChildName = "Image";
    [SerializeField] private bool hideTemplateLabelText = true;

    [Header("Visual")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.65f, 1f, 0.65f, 1f);

    private readonly List<Button> _spawnedButtons = new List<Button>();
    private Action<int> _onSelect;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (avatarButtonTemplate != null)
            avatarButtonTemplate.gameObject.SetActive(false);
    }

    public void Open(Sprite[] avatars, int selectedIndex, Action<int> onSelect)
    {
        _onSelect = onSelect;
        RebuildButtons(avatars, selectedIndex);
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dialogRoot == null)
        {
            Close();
            return;
        }

        bool clickInsideDialog = RectTransformUtility.RectangleContainsScreenPoint(
            dialogRoot,
            eventData.position,
            eventData.pressEventCamera);

        if (!clickInsideDialog)
            Close();
    }

    private void RebuildButtons(Sprite[] avatars, int selectedIndex)
    {
        ClearButtons();

        if (avatarButtonTemplate == null || avatarButtonContainer == null)
        {
            Debug.LogWarning("[ProfileAvatarPickerUI] Missing button template/container.");
            return;
        }

        if (avatars == null || avatars.Length == 0)
            return;

        for (int i = 0; i < avatars.Length; i++)
        {
            int index = i;
            Button btn = Instantiate(avatarButtonTemplate, avatarButtonContainer);
            btn.gameObject.SetActive(true);

            ApplyAvatarSprite(btn, avatars[index]);
            if (hideTemplateLabelText)
                HideTemplateLabel(btn);

            ColorBlock colors = btn.colors;
            Color tint = (index == selectedIndex) ? selectedColor : normalColor;
            colors.normalColor = tint;
            colors.selectedColor = tint;
            btn.colors = colors;

            btn.onClick.AddListener(() =>
            {
                _onSelect?.Invoke(index);
                Close();
            });

            _spawnedButtons.Add(btn);
        }
    }

    private void ClearButtons()
    {
        for (int i = 0; i < _spawnedButtons.Count; i++)
        {
            if (_spawnedButtons[i] != null)
                Destroy(_spawnedButtons[i].gameObject);
        }

        _spawnedButtons.Clear();
    }

    private void ApplyAvatarSprite(Button button, Sprite sprite)
    {
        Image img = ResolveAvatarImage(button);
        if (img == null)
        {
            Debug.LogWarning("[ProfileAvatarPickerUI] Avatar image target not found on button template.");
            return;
        }

        img.sprite = sprite;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }

    private Image ResolveAvatarImage(Button button)
    {
        if (button == null)
            return null;

        if (!string.IsNullOrWhiteSpace(avatarImageChildName))
        {
            Transform child = button.transform.Find(avatarImageChildName);
            if (child != null)
            {
                Image childImage = child.GetComponent<Image>();
                if (childImage != null)
                    return childImage;
            }
        }

        Image own = button.targetGraphic as Image;
        if (own != null)
            return own;

        return button.GetComponentInChildren<Image>(true);
    }

    private static void HideTemplateLabel(Button button)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;

        label.text = string.Empty;
        label.gameObject.SetActive(false);
    }
}
