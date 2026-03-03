using System;
using System.Collections.Generic;
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

            Image img = ResolveImage(btn);
            if (img != null)
                img.sprite = avatars[index];

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

    private static Image ResolveImage(Button button)
    {
        if (button == null)
            return null;

        Image own = button.targetGraphic as Image;
        if (own != null)
            return own;

        return button.GetComponentInChildren<Image>(true);
    }
}
