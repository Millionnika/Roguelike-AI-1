using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ConfirmationDialogPresenter : MonoBehaviour
{
    [Header("Диалог подтверждения")]
    [Tooltip("Корневой объект панели подтверждения. Если не назначен, presenter создаст runtime-панель.")]
    [SerializeField] private GameObject panelObject;
    [Tooltip("Текст заголовка окна подтверждения.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Основной текст вопроса в окне подтверждения.")]
    [SerializeField] private TMP_Text bodyText;

    private UiButtonView yesButtonView;
    private UiButtonView noButtonView;
    private Action onYes;
    private Action onNo;
    private Func<string, string> localize;

    public bool IsVisible => panelObject != null && panelObject.activeSelf;

    public void Initialize(Func<string, string> localizeCallback)
    {
        localize = localizeCallback;
    }

    public void Bind(Transform uiRoot)
    {
        Transform root = uiRoot != null ? uiRoot.Find("ConfirmationPanel") : null;
        if (root == null)
        {
            return;
        }

        panelObject = root.gameObject;
        Transform panel = root.Find("Panel");
        titleText = FindText(panel, "Title");
        bodyText = FindText(panel, "Body");
        yesButtonView = BindButton(panel != null ? panel.Find("confirm_yes") : null, "confirm_yes");
        noButtonView = BindButton(panel != null ? panel.Find("confirm_no") : null, "confirm_no");
        Hide();
    }

    internal void Build(Transform parent, ISpaceCombatUiFactory uiFactory, Font uiFont, Sprite squareSprite)
    {
        if (panelObject != null || parent == null || uiFactory == null)
        {
            return;
        }

        panelObject = new GameObject("ConfirmationPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);
        StretchToParent(panelObject.GetComponent<RectTransform>());

        Image dim = uiFactory.CreateImage("Dimmer", panelObject.transform, squareSprite, new Color(0f, 0f, 0f, 0.58f));
        StretchToParent(dim.rectTransform);

        Image panel = uiFactory.CreateImage("Panel", panelObject.transform, squareSprite, new Color(0.05f, 0.1f, 0.14f, 0.98f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460f, 230f);
        uiFactory.AddOutline(panel.gameObject, new Color(0.22f, 0.42f, 0.58f, 1f));

        titleText = uiFactory.CreateText("Title", panel.transform, uiFont, Localize("confirm_title"), 28, FontStyle.Bold, Color.white);
        titleText.alignment = TextAlignmentOptions.Center;
        uiFactory.SetAnchoredRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -24f), new Vector2(-20f, -66f));

        bodyText = uiFactory.CreateText("Body", panel.transform, uiFont, string.Empty, 18, FontStyle.Normal, new Color(0.88f, 0.94f, 1f));
        bodyText.alignment = TextAlignmentOptions.Center;
        uiFactory.SetAnchoredRect(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -72f), new Vector2(-26f, -132f));

        yesButtonView = CreateButton(uiFactory, uiFont, squareSprite, panel.transform, "confirm_yes", new Vector2(-90f, -72f), new Vector2(150f, 48f));
        noButtonView = CreateButton(uiFactory, uiFont, squareSprite, panel.transform, "confirm_no", new Vector2(90f, -72f), new Vector2(150f, 48f));
        Hide();
    }

    public void RefreshLocalizedTexts()
    {
        if (titleText != null) titleText.text = Localize("confirm_title");
        if (yesButtonView != null && yesButtonView.Label != null) yesButtonView.Label.text = Localize("confirm_yes");
        if (noButtonView != null && noButtonView.Label != null) noButtonView.Label.text = Localize("confirm_no");
    }

    public void Show(string title, string body, Action yesCallback, Action noCallback)
    {
        onYes = yesCallback;
        onNo = noCallback;
        RefreshLocalizedTexts();
        if (titleText != null) titleText.text = string.IsNullOrEmpty(title) ? Localize("confirm_title") : title;
        if (bodyText != null) bodyText.text = body ?? string.Empty;
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

    public bool HandlePointerDown(Vector2 screenPosition)
    {
        if (!IsVisible)
        {
            return false;
        }

        if (IsButtonClicked(noButtonView, screenPosition))
        {
            Hide();
            onNo?.Invoke();
            return true;
        }

        if (IsButtonClicked(yesButtonView, screenPosition))
        {
            Hide();
            onYes?.Invoke();
            return true;
        }

        return true;
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
        Vector2 anchoredPosition,
        Vector2 size)
    {
        Image background = uiFactory.CreateImage(id, parent, squareSprite, new Color(0.08f, 0.16f, 0.22f, 0.96f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
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
