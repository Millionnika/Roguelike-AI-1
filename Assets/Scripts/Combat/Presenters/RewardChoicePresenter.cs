using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RewardChoicePresenter : MonoBehaviour
{
    private const int MaxChoices = 3;

    [Header("Выбор награды")]
    [Tooltip("Корневой объект панели выбора награды.")]
    [SerializeField] private GameObject panelObject;
    [Tooltip("Заголовок панели выбора награды.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Список кнопок выбора награды (до 3).")]
    [SerializeField] private List<Button> choiceButtons = new List<Button>();
    [Tooltip("Подписи кнопок выбора награды.")]
    [SerializeField] private List<TMP_Text> choiceLabels = new List<TMP_Text>();

    private readonly List<RewardSO> currentChoices = new List<RewardSO>();
    private Action<RewardSO> onSelected;

    public bool HasPanel => panelObject != null;
    public bool IsVisible => panelObject != null && panelObject.activeSelf;

    public void Configure(GameObject panel, TMP_Text title, IReadOnlyList<Button> buttons, IReadOnlyList<TMP_Text> labels)
    {
        panelObject = panel;
        titleText = title;
        choiceButtons.Clear();
        choiceLabels.Clear();

        if (buttons != null)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                choiceButtons.Add(buttons[i]);
            }
        }

        if (labels != null)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                choiceLabels.Add(labels[i]);
            }
        }

        Hide();
    }

    public void Bind(Transform uiRoot)
    {
        Transform root = uiRoot != null ? uiRoot.Find("RewardChoicePanel") : null;
        panelObject = root != null ? root.gameObject : null;
        titleText = FindText(root, "Panel/Title");

        choiceButtons.Clear();
        choiceLabels.Clear();

        for (int i = 0; i < MaxChoices; i++)
        {
            Transform buttonRoot = root != null ? root.Find("Panel/RewardOption_" + i) : null;
            Button button = buttonRoot != null ? buttonRoot.GetComponent<Button>() : null;
            TMP_Text label = FindText(buttonRoot, "Label");
            if (button != null)
            {
                choiceButtons.Add(button);
                choiceLabels.Add(label);
            }
        }

        Hide();
    }

    internal void Build(Transform parent, ISpaceCombatUiFactory uiFactory, Font uiFont, Sprite squareSprite)
    {
        if (panelObject != null || parent == null || uiFactory == null)
        {
            return;
        }

        panelObject = new GameObject("RewardChoicePanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);
        RectTransform rootRect = panelObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = uiFactory.CreateImage("Dimmer", panelObject.transform, squareSprite, new Color(0f, 0f, 0f, 0.45f));
        Stretch(dim.rectTransform);

        Image panel = uiFactory.CreateImage("Panel", panelObject.transform, squareSprite, new Color(0.06f, 0.12f, 0.18f, 0.97f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(620f, 300f);
        uiFactory.AddOutline(panel.gameObject, new Color(0.26f, 0.54f, 0.74f, 1f));

        titleText = uiFactory.CreateText("Title", panel.transform, uiFont, "Выберите награду", 28, FontStyle.Bold, new Color(0.96f, 0.9f, 0.6f));
        uiFactory.SetAnchoredRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -18f), new Vector2(-20f, -58f));

        choiceButtons.Clear();
        choiceLabels.Clear();
        for (int i = 0; i < MaxChoices; i++)
        {
            Image option = uiFactory.CreateImage("RewardOption_" + i, panel.transform, squareSprite, new Color(0.08f, 0.17f, 0.24f, 0.97f));
            Button button = option.gameObject.GetComponent<Button>();
            if (button == null)
            {
                button = option.gameObject.AddComponent<Button>();
            }

            EnsureButtonScaleAnimator(option.gameObject);
            RectTransform optionRect = option.rectTransform;
            optionRect.anchorMin = new Vector2(0f, 1f);
            optionRect.anchorMax = new Vector2(1f, 1f);
            optionRect.pivot = new Vector2(0.5f, 1f);
            optionRect.sizeDelta = new Vector2(-40f, 62f);
            optionRect.anchoredPosition = new Vector2(0f, -72f - i * 72f);
            uiFactory.AddOutline(option.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));

            TMP_Text label = uiFactory.CreateText("Label", option.transform, uiFont, string.Empty, 18, FontStyle.Bold, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            uiFactory.SetAnchoredRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 8f), new Vector2(-12f, -8f));

            choiceButtons.Add(button);
            choiceLabels.Add(label);
        }

        Hide();
    }

    public void Show(IReadOnlyList<RewardSO> choices, Action<RewardSO> selectedCallback)
    {
        currentChoices.Clear();
        onSelected = selectedCallback;

        if (choices != null)
        {
            for (int i = 0; i < choices.Count && i < MaxChoices; i++)
            {
                if (choices[i] != null)
                {
                    currentChoices.Add(choices[i]);
                }
            }
        }

        if (currentChoices.Count == 0)
        {
            Hide();
            return;
        }

        if (titleText != null)
        {
            titleText.text = "Выберите награду";
        }

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            Button button = choiceButtons[i];
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            bool isActive = i < currentChoices.Count;
            button.gameObject.SetActive(isActive);
            if (!isActive)
            {
                continue;
            }

            int index = i;
            button.onClick.AddListener(() => Select(index));

            TMP_Text label = i < choiceLabels.Count ? choiceLabels[i] : button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                RewardSO reward = currentChoices[index];
                string name = string.IsNullOrWhiteSpace(reward.displayName) ? reward.name : reward.displayName;
                string description = string.IsNullOrWhiteSpace(reward.description) ? string.Empty : reward.description;
                label.text = name + "\n" + description;
            }
        }

        if (panelObject != null)
        {
            panelObject.SetActive(true);
        }
    }

    public void Hide()
    {
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].onClick.RemoveAllListeners();
            }
        }

        currentChoices.Clear();
        onSelected = null;
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }
    }

    public bool TrySelectAt(Vector2 screenPosition)
    {
        if (!IsVisible)
        {
            return false;
        }

        for (int i = 0; i < currentChoices.Count && i < choiceButtons.Count; i++)
        {
            Button button = choiceButtons[i];
            RectTransform rect = button != null ? button.GetComponent<RectTransform>() : null;
            if (rect != null && rect.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null))
            {
                Select(i);
                return true;
            }
        }

        return false;
    }

    public bool SelectByIndex(int index)
    {
        if (!IsVisible || index < 0 || index >= currentChoices.Count)
        {
            return false;
        }

        Select(index);
        return true;
    }

    public void TickInput()
    {
        if (!IsVisible)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
        else if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
        else if (keyboard.digit3Key.wasPressedThisFrame) Select(2);
    }

    private void Select(int index)
    {
        if (index < 0 || index >= currentChoices.Count)
        {
            return;
        }

        onSelected?.Invoke(currentChoices[index]);
    }

    private static TMP_Text FindText(Transform root, string path)
    {
        Transform child = root != null ? root.Find(path) : null;
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureButtonScaleAnimator(GameObject buttonObject)
    {
        if (buttonObject != null && buttonObject.GetComponent<UIButtonScaleAnimator>() == null)
        {
            buttonObject.AddComponent<UIButtonScaleAnimator>();
        }
    }
}
