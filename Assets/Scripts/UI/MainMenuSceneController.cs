using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuSceneController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Optional References")]
    [SerializeField] private Canvas menuCanvas;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button exitButton;

    private GameObject confirmPanel;
    private Button confirmYesButton;
    private Button confirmNoButton;

    private void Awake()
    {
        EnsureRuntimeMenu();
        BindButtons();
    }

    private void BindButtons()
    {
        BindButton(newGameButton, StartNewGame);
        BindButton(continueButton, ContinueGame);
        BindButton(exitButton, RequestExit);
        BindButton(confirmYesButton, ExitGame);
        BindButton(confirmNoButton, CloseConfirm);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void StartNewGame()
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            return;
        }

        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    private void ContinueGame()
    {
        StartNewGame();
    }

    private void RequestExit()
    {
        if (confirmPanel == null)
        {
            ExitGame();
            return;
        }

        confirmPanel.SetActive(true);
    }

    private void CloseConfirm()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    private static void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureRuntimeMenu()
    {
        if (menuCanvas != null && newGameButton != null && continueButton != null && exitButton != null)
        {
            if (titleText != null)
            {
                titleText.text = "SPACE FRONTIER";
            }
            return;
        }

        Canvas existingCanvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (existingCanvas != null && existingCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            menuCanvas = existingCanvas;
        }
        else
        {
            GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            menuCanvas = canvasObject.GetComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        EventSystem eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        if (eventSystem != null)
        {
            StandaloneInputModule oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Destroy(oldModule);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        Image backdrop = CreateImage("Backdrop", menuCanvas.transform, new Color(0.02f, 0.07f, 0.12f, 1f));
        RectTransform backdropRect = backdrop.rectTransform;
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        titleText = CreateText("Title", menuCanvas.transform, "SPACE FRONTIER", 68, FontStyles.Bold);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -150f);
        titleRect.sizeDelta = new Vector2(920f, 120f);
        titleText.alignment = TextAlignmentOptions.Center;

        newGameButton = CreateButton("NewGameButton", menuCanvas.transform, "NEW GAME", new Vector2(0f, 40f));
        continueButton = CreateButton("ContinueButton", menuCanvas.transform, "CONTINUE", new Vector2(0f, -40f));
        exitButton = CreateButton("ExitButton", menuCanvas.transform, "EXIT", new Vector2(0f, -120f));

        BuildConfirmPanel();
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string content, int size, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(360f, 62f);
        rect.anchoredPosition = anchoredPosition;

        Image image = root.GetComponent<Image>();
        image.color = new Color(0.1f, 0.24f, 0.35f, 0.95f);
        if (root.GetComponent<UIButtonScaleAnimator>() == null)
        {
            root.AddComponent<UIButtonScaleAnimator>();
        }

        TMP_Text text = CreateText("Label", root.transform, label, 28, FontStyles.Bold);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;

        return root.GetComponent<Button>();
    }

    private void BuildConfirmPanel()
    {
        confirmPanel = new GameObject("ExitConfirmPanel", typeof(RectTransform));
        confirmPanel.transform.SetParent(menuCanvas.transform, false);
        RectTransform rootRect = confirmPanel.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = CreateImage("Dimmer", confirmPanel.transform, new Color(0f, 0f, 0f, 0.58f));
        RectTransform dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Image panel = CreateImage("Panel", confirmPanel.transform, new Color(0.05f, 0.12f, 0.18f, 0.98f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(430f, 210f);

        TMP_Text text = CreateText("Text", panel.transform, "Are you sure you want to exit?", 26, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -26f);
        textRect.sizeDelta = new Vector2(-30f, 90f);

        confirmYesButton = CreateButton("YesButton", panel.transform, "YES", new Vector2(-90f, -56f));
        confirmNoButton = CreateButton("NoButton", panel.transform, "NO", new Vector2(90f, -56f));
        confirmPanel.SetActive(false);
    }
}
