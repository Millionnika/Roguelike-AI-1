using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameOverPresenter : MonoBehaviour
{
    [Header("Экран поражения")]
    [Tooltip("Корневой объект панели поражения. Если не назначен, presenter создаст runtime-панель.")]
    [SerializeField] private GameObject panelObject;

    private UiButtonView retryButtonView;
    private UiButtonView menuButtonView;
    private UiButtonView exitButtonView;
    private Func<string, string> localize;

    public Action OnRetry;
    public Action OnReturnToMenuRequested;
    public Action OnExitRequested;

    public bool IsVisible => panelObject != null && panelObject.activeSelf;

    public void Initialize(Func<string, string> localizeCallback)
    {
        localize = localizeCallback;
    }

    public void Bind(Transform uiRoot)
    {
        Transform root = uiRoot != null ? uiRoot.Find("GameOverPanel") : null;
        panelObject = root != null ? root.gameObject : null;
        Transform content = root != null ? root.Find("Content") : null;
        retryButtonView = BindButton(content != null ? content.Find("gameover_retry") : null, "gameover_retry");
        menuButtonView = BindButton(content != null ? content.Find("gameover_menu") : null, "gameover_menu");
        exitButtonView = BindButton(content != null ? content.Find("gameover_exit") : null, "gameover_exit");
        Hide();
    }

    internal void Build(Transform parent, ISpaceCombatUiFactory uiFactory, Font uiFont, Sprite squareSprite)
    {
        if (panelObject != null || parent == null || uiFactory == null)
        {
            return;
        }

        panelObject = new GameObject("GameOverPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);
        RectTransform rootRect = panelObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = uiFactory.CreateImage("Dimmer", panelObject.transform, squareSprite, new Color(0f, 0f, 0f, 0.58f));
        RectTransform dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Image panel = uiFactory.CreateImage("Content", panelObject.transform, squareSprite, new Color(0.06f, 0.1f, 0.14f, 0.98f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(420f, 300f);
        uiFactory.AddOutline(panel.gameObject, new Color(0.55f, 0.18f, 0.18f, 1f));

        TMP_Text title = uiFactory.CreateText("Title", panel.transform, uiFont, Localize("status_gameover"), 28, FontStyle.Bold, new Color(1f, 0.42f, 0.36f));
        title.alignment = TextAlignmentOptions.Center;
        uiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -24f), new Vector2(-20f, -64f));

        retryButtonView = CreateButton(uiFactory, uiFont, squareSprite, panel.transform, "gameover_retry", new Vector2(0f, 40f));
        menuButtonView = CreateButton(uiFactory, uiFont, squareSprite, panel.transform, "gameover_menu", new Vector2(0f, -24f));
        exitButtonView = CreateButton(uiFactory, uiFont, squareSprite, panel.transform, "gameover_exit", new Vector2(0f, -88f));
        Hide();
    }

    public void RefreshLocalizedTexts()
    {
        if (retryButtonView != null && retryButtonView.Label != null) retryButtonView.Label.text = Localize("retry");
        if (menuButtonView != null && menuButtonView.Label != null) menuButtonView.Label.text = Localize("pause_to_menu");
        if (exitButtonView != null && exitButtonView.Label != null) exitButtonView.Label.text = Localize("menu_exit");
    }

    public void Show()
    {
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

    public bool TickInput(Vector2 screenPosition)
    {
        if (!IsVisible)
        {
            return false;
        }

        if (IsButtonClicked(retryButtonView, screenPosition))
        {
            OnRetry?.Invoke();
            return true;
        }

        if (IsButtonClicked(menuButtonView, screenPosition))
        {
            OnReturnToMenuRequested?.Invoke();
            return true;
        }

        if (IsButtonClicked(exitButtonView, screenPosition))
        {
            OnExitRequested?.Invoke();
            return true;
        }

        return false;
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
        Vector2 anchoredPosition)
    {
        Image background = uiFactory.CreateImage(id, parent, squareSprite, new Color(0.08f, 0.16f, 0.22f, 0.96f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(260f, 52f);
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

    private static void EnsureButtonScaleAnimator(GameObject buttonObject)
    {
        if (buttonObject != null && buttonObject.GetComponent<UIButtonScaleAnimator>() == null)
        {
            buttonObject.AddComponent<UIButtonScaleAnimator>();
        }
    }
}
