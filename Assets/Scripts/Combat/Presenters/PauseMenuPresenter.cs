using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuPresenter : MonoBehaviour
{
    [Header("Пауза")]
    [Tooltip("Корневой объект меню паузы. Если не назначен, presenter создаст runtime-меню.")]
    [SerializeField] private GameObject pauseMenuObject;
    [Tooltip("Кнопка HUD для открытия паузы во время боя.")]
    [SerializeField] private RectTransform pauseHudButtonRect;

    private UiButtonView pauseHudButtonView;
    private UiButtonView pauseResumeButtonView;
    private UiButtonView pauseSettingsButtonView;
    private UiButtonView pauseMenuButtonView;
    private UiButtonView pauseExitButtonView;
    private Func<string, string> localize;

    public Action OnResume;
    public Action OnOpenSettings;
    public Action OnReturnToMenuRequested;
    public Action OnExitRequested;

    public bool IsMenuVisible => pauseMenuObject != null && pauseMenuObject.activeSelf;

    public void Initialize(Func<string, string> localizeCallback)
    {
        localize = localizeCallback;
    }

    public void Bind(Transform uiRoot)
    {
        pauseHudButtonView = BindButton(uiRoot != null ? uiRoot.Find("PauseButton") : null, "PauseButton");
        pauseHudButtonRect = pauseHudButtonView != null ? pauseHudButtonView.Rect : null;

        Transform root = uiRoot != null ? uiRoot.Find("PauseMenu") : null;
        pauseMenuObject = root != null ? root.gameObject : null;
        Transform panel = root != null ? root.Find("Panel") : null;
        pauseResumeButtonView = BindButton(panel != null ? panel.Find("pause_resume") : null, "pause_resume");
        pauseSettingsButtonView = BindButton(panel != null ? panel.Find("pause_settings") : null, "pause_settings");
        pauseMenuButtonView = BindButton(panel != null ? panel.Find("pause_menu") : null, "pause_menu");
        pauseExitButtonView = BindButton(panel != null ? panel.Find("pause_exit") : null, "pause_exit");
        Hide();
    }

    internal void Build(Transform parent, ISpaceCombatUiFactory uiFactory, Font uiFont, Sprite squareSprite)
    {
        if (parent == null || uiFactory == null)
        {
            return;
        }

        if (pauseHudButtonView == null)
        {
            pauseHudButtonView = CreateButton(uiFactory, uiFont, squareSprite, parent, "PauseButton", new Vector2(0f, 1f), new Vector2(46f, -34f), new Vector2(76f, 38f));
            pauseHudButtonRect = pauseHudButtonView.Rect;
            if (pauseHudButtonView.Label != null)
            {
                pauseHudButtonView.Label.fontSize = 14f;
            }
        }

        if (pauseMenuObject != null)
        {
            return;
        }

        pauseMenuObject = new GameObject("PauseMenu", typeof(RectTransform));
        pauseMenuObject.transform.SetParent(parent, false);
        StretchToParent(pauseMenuObject.GetComponent<RectTransform>());

        Image dim = uiFactory.CreateImage("Dimmer", pauseMenuObject.transform, squareSprite, new Color(0f, 0f, 0f, 0.42f));
        StretchToParent(dim.rectTransform);

        Image panel = uiFactory.CreateImage("Panel", pauseMenuObject.transform, squareSprite, new Color(0.04f, 0.08f, 0.12f, 0.97f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(360f, 340f);
        uiFactory.AddOutline(panel.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));

        TMP_Text title = uiFactory.CreateText("Title", panel.transform, uiFont, "ПАУЗА", 28, FontStyle.Bold, new Color(0.87f, 0.95f, 1f));
        title.alignment = TextAlignmentOptions.Center;
        uiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -22f), new Vector2(-18f, -62f));

        pauseResumeButtonView = CreateButton(uiFactory, uiFont, squareSprite, panel.transform, "pause_resume", new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(260f, 52f));
        pauseSettingsButtonView = CreateButton(uiFactory, uiFont, squareSprite, panel.transform, "pause_settings", new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(260f, 52f));
        pauseMenuButtonView = CreateButton(uiFactory, uiFont, squareSprite, panel.transform, "pause_menu", new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(260f, 52f));
        pauseExitButtonView = CreateButton(uiFactory, uiFont, squareSprite, panel.transform, "pause_exit", new Vector2(0.5f, 0.5f), new Vector2(0f, -148f), new Vector2(260f, 52f));
        Hide();
    }

    public void RefreshLocalizedTexts()
    {
        if (pauseHudButtonView != null && pauseHudButtonView.Label != null) pauseHudButtonView.Label.text = Localize("menu_short");
        if (pauseResumeButtonView != null && pauseResumeButtonView.Label != null) pauseResumeButtonView.Label.text = Localize("menu_continue");
        if (pauseSettingsButtonView != null && pauseSettingsButtonView.Label != null) pauseSettingsButtonView.Label.text = Localize("menu_settings");
        if (pauseMenuButtonView != null && pauseMenuButtonView.Label != null) pauseMenuButtonView.Label.text = Localize("pause_to_menu");
        if (pauseExitButtonView != null && pauseExitButtonView.Label != null) pauseExitButtonView.Label.text = Localize("menu_exit");
    }

    public void Show()
    {
        if (pauseMenuObject != null)
        {
            pauseMenuObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (pauseMenuObject != null)
        {
            pauseMenuObject.SetActive(false);
        }
    }

    public void SetHudButtonVisible(bool visible)
    {
        if (pauseHudButtonView != null && pauseHudButtonView.Rect != null)
        {
            pauseHudButtonView.Rect.gameObject.SetActive(visible);
        }
    }

    public bool IsHudButtonClicked(Vector2 screenPosition)
    {
        return IsButtonClicked(pauseHudButtonView, screenPosition);
    }

    public bool HandlePointerDown(Vector2 screenPosition)
    {
        if (IsButtonClicked(pauseResumeButtonView, screenPosition))
        {
            OnResume?.Invoke();
            return true;
        }

        if (IsButtonClicked(pauseSettingsButtonView, screenPosition))
        {
            OnOpenSettings?.Invoke();
            return true;
        }

        if (IsButtonClicked(pauseMenuButtonView, screenPosition))
        {
            OnReturnToMenuRequested?.Invoke();
            return true;
        }

        if (IsButtonClicked(pauseExitButtonView, screenPosition))
        {
            OnExitRequested?.Invoke();
            return true;
        }

        return false;
    }

    public bool IsPointerOverPauseUi(Vector2 screenPosition)
    {
        if (IsButtonClicked(pauseHudButtonView, screenPosition))
        {
            return true;
        }

        return pauseMenuObject != null &&
               pauseMenuObject.activeSelf &&
               RectTransformUtility.RectangleContainsScreenPoint(pauseMenuObject.GetComponent<RectTransform>(), screenPosition, null);
    }

    private string Localize(string key)
    {
        return localize != null ? localize(key) : key;
    }

    private static UiButtonView BindButton(Transform buttonTransform, string id)
    {
        if (buttonTransform == null)
        {
            return null;
        }

        if (buttonTransform.GetComponent<Button>() == null)
        {
            buttonTransform.gameObject.AddComponent<Button>();
        }

        EnsureButtonScaleAnimator(buttonTransform.gameObject);
        return new UiButtonView
        {
            Id = id,
            Rect = buttonTransform.GetComponent<RectTransform>(),
            Background = buttonTransform.GetComponent<Image>(),
            Label = FindText(buttonTransform, "Label")
        };
    }

    private static UiButtonView CreateButton(
        ISpaceCombatUiFactory uiFactory,
        Font uiFont,
        Sprite squareSprite,
        Transform parent,
        string id,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        Image background = uiFactory.CreateImage(id, parent, squareSprite, new Color(0.08f, 0.16f, 0.22f, 0.96f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        uiFactory.AddOutline(background.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));
        background.gameObject.AddComponent<Button>();
        EnsureButtonScaleAnimator(background.gameObject);

        TMP_Text label = uiFactory.CreateText("Label", background.transform, uiFont, string.Empty, 16, FontStyle.Bold, Color.white);
        label.alignment = TextAlignmentOptions.Center;
        uiFactory.SetAnchoredRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return new UiButtonView
        {
            Id = id,
            Rect = rect,
            Background = background,
            Label = label
        };
    }

    private static TMP_Text FindText(Transform root, string path)
    {
        Transform child = root != null ? root.Find(path) : null;
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static bool IsButtonClicked(UiButtonView button, Vector2 screenPosition)
    {
        return button != null && button.Rect != null && RectTransformUtility.RectangleContainsScreenPoint(button.Rect, screenPosition, null);
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

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
