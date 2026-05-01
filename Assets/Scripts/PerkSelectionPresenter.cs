using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PerkSelectionPresenter : MonoBehaviour
{
    private const int MaxOptions = 3;

    [Header("Выбор улучшения")]
    [Tooltip("Корневой объект панели выбора улучшения. Если не назначен, presenter создаст runtime-панель.")]
    [SerializeField] private GameObject panelObject;
    [Tooltip("Заголовок панели выбора улучшения.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Подсказка под списком вариантов улучшения.")]
    [SerializeField] private TMP_Text hintText;

    private readonly TMP_Text[] optionTexts = new TMP_Text[MaxOptions];
    private readonly RectTransform[] optionRects = new RectTransform[MaxOptions];
    private int visibleChoiceCount;
    private Func<string, string> localize;

    public Action<int> OnPerkSelected;

    public bool IsVisible => panelObject != null && panelObject.activeSelf;

    public void Initialize(Func<string, string> localizeCallback)
    {
        localize = localizeCallback;
    }

    public void Bind(Transform uiRoot)
    {
        Transform root = uiRoot != null ? uiRoot.Find("PerkPanel") : null;
        panelObject = root != null ? root.gameObject : null;
        Transform content = root != null ? root.Find("Content") : null;
        titleText = FindText(content, "Title");
        hintText = FindText(content, "Choices");

        for (int i = 0; i < optionTexts.Length; i++)
        {
            Transform option = content != null ? content.Find("PerkOption_" + i) : null;
            optionRects[i] = option != null ? option.GetComponent<RectTransform>() : null;
            optionTexts[i] = FindText(option, "Label");
            EnsureButton(option);
        }

        Hide();
    }

    internal void Build(Transform parent, ISpaceCombatUiFactory uiFactory, Font uiFont, Sprite squareSprite)
    {
        if (panelObject != null || parent == null || uiFactory == null)
        {
            return;
        }

        panelObject = new GameObject("PerkPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);
        RectTransform rootRect = panelObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = uiFactory.CreateImage("Dimmer", panelObject.transform, squareSprite, new Color(0f, 0f, 0f, 0.45f));
        RectTransform dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Image panel = uiFactory.CreateImage("Content", panelObject.transform, squareSprite, new Color(0.05f, 0.11f, 0.16f, 0.97f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 250f);
        uiFactory.AddOutline(panel.gameObject, new Color(0.32f, 0.64f, 0.8f, 1f));

        titleText = uiFactory.CreateText("Title", panel.transform, uiFont, Localize("perk_title"), 28, FontStyle.Bold, new Color(1f, 0.87f, 0.38f));
        uiFactory.SetAnchoredRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, -56f));

        hintText = uiFactory.CreateText("Choices", panel.transform, uiFont, string.Empty, 18, FontStyle.Bold, new Color(0.88f, 0.94f, 1f));
        hintText.alignment = TextAlignmentOptions.Center;
        uiFactory.SetAnchoredRect(hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 18f), new Vector2(-24f, 44f));

        for (int i = 0; i < optionTexts.Length; i++)
        {
            Image option = uiFactory.CreateImage("PerkOption_" + i, panel.transform, squareSprite, new Color(0.08f, 0.16f, 0.22f, 0.96f));
            option.gameObject.AddComponent<Button>();
            EnsureButtonScaleAnimator(option.gameObject);
            RectTransform optionRect = option.rectTransform;
            optionRect.anchorMin = new Vector2(0f, 1f);
            optionRect.anchorMax = new Vector2(1f, 1f);
            optionRect.pivot = new Vector2(0.5f, 1f);
            optionRect.sizeDelta = new Vector2(-48f, 42f);
            optionRect.anchoredPosition = new Vector2(0f, -72f - i * 50f);
            uiFactory.AddOutline(option.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));

            TMP_Text optionText = uiFactory.CreateText("Label", option.transform, uiFont, string.Empty, 17, FontStyle.Bold, Color.white);
            optionText.alignment = TextAlignmentOptions.Center;
            uiFactory.SetAnchoredRect(optionText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            optionRects[i] = optionRect;
            optionTexts[i] = optionText;
        }

        Hide();
    }

    public void RefreshLocalizedTexts()
    {
        if (titleText != null) titleText.text = Localize("perk_title");
        if (hintText != null && IsVisible) hintText.text = Localize("perk_pick");
    }

    internal void Show(IReadOnlyList<PerkChoice> choices)
    {
        visibleChoiceCount = choices != null ? Mathf.Min(choices.Count, optionTexts.Length) : 0;

        for (int i = 0; i < visibleChoiceCount; i++)
        {
            if (optionTexts[i] == null)
            {
                continue;
            }

            optionTexts[i].text = (i + 1) + ". " + choices[i].Label;
            optionTexts[i].transform.parent.gameObject.SetActive(true);
        }

        for (int i = visibleChoiceCount; i < optionTexts.Length; i++)
        {
            if (optionTexts[i] != null)
            {
                optionTexts[i].text = string.Empty;
                optionTexts[i].transform.parent.gameObject.SetActive(false);
            }
        }

        if (hintText != null) hintText.text = Localize("perk_pick");
        RefreshLocalizedTexts();

        if (panelObject != null)
        {
            panelObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }
    }

    public void TickInput()
    {
        if (!IsVisible)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
            if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
            if (keyboard.digit3Key.wasPressedThisFrame) Select(2);
        }

        Vector2 pointerPosition;
        if (TryGetPrimaryPointerDown(out pointerPosition))
        {
            SelectFromPointer(pointerPosition);
        }
    }

    private void SelectFromPointer(Vector2 screenPosition)
    {
        for (int i = 0; i < visibleChoiceCount && i < optionRects.Length; i++)
        {
            RectTransform optionRect = optionRects[i];
            if (optionRect != null && RectTransformUtility.RectangleContainsScreenPoint(optionRect, screenPosition, null))
            {
                Select(i);
                return;
            }
        }
    }

    private void Select(int index)
    {
        if (index < 0 || index >= visibleChoiceCount)
        {
            return;
        }

        OnPerkSelected?.Invoke(index);
    }

    private string Localize(string key)
    {
        return localize != null ? localize(key) : key;
    }

    private static TMP_Text FindText(Transform root, string path)
    {
        Transform child = root != null ? root.Find(path) : null;
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static void EnsureButton(Transform buttonTransform)
    {
        if (buttonTransform == null)
        {
            return;
        }

        if (buttonTransform.GetComponent<Button>() == null)
        {
            buttonTransform.gameObject.AddComponent<Button>();
        }

        EnsureButtonScaleAnimator(buttonTransform.gameObject);
    }

    private static void EnsureButtonScaleAnimator(GameObject buttonObject)
    {
        if (buttonObject != null && buttonObject.GetComponent<UIButtonScaleAnimator>() == null)
        {
            buttonObject.AddComponent<UIButtonScaleAnimator>();
        }
    }

    private static bool TryGetPrimaryPointerDown(out Vector2 screenPosition)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var touch = touchscreen.touches[i];
                if (touch.press.wasPressedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }
        }

        screenPosition = Vector2.zero;
        return false;
    }
}
