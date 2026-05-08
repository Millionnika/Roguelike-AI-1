using SpaceFrontier.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MinimapController : MonoBehaviour
{
    [Header("Миникарта")]
    [Tooltip("Включает или отключает систему миникарты. Если выключено, панель скрывается и камера миникарты не создается.")]
    [SerializeField] private bool minimapEnabled = true;
    [Tooltip("Размер области обзора миникарты (ортографический размер камеры). Больше значение = дальше «отъезд» камеры.")]
    [SerializeField, Min(2f)] private float minimapOrthoSize = 20f;
    [Tooltip("Минимальный zoom миникарты (меньше = ближе).")]
    [SerializeField, Min(2f)] private float minimapMinOrthoSize = 12f;
    [Tooltip("Максимальный zoom миникарты (больше = дальше).")]
    [SerializeField, Min(2f)] private float minimapMaxOrthoSize = 48f;
    [Tooltip("Шаг zoom миникарты для кнопок +/- .")]
    [SerializeField, Min(0.5f)] private float minimapZoomStep = 4f;
    [Tooltip("Базовый размер панели миникарты в пикселях. Используется для расчета разрешения RenderTexture.")]
    [SerializeField, Min(96f)] private float minimapPanelSize = 200f;
    [Tooltip("Цвет фона миникарты (используется как цвет очистки камеры).")]
    [SerializeField] private Color minimapBackgroundColor = new Color(0.03f, 0.08f, 0.14f, 0.82f);

    private PlayerShip player;
    private Camera minimapCamera;
    private RenderTexture minimapRenderTexture;
    private RawImage minimapRawImage;
    private RectTransform minimapPanelRect;
    private Button zoomInButton;
    private Button zoomOutButton;
    private TMP_Text zoomInLabel;
    private TMP_Text zoomOutLabel;

    public void Initialize(PlayerShip playerShip, Transform uiRoot)
    {
        player = playerShip;
        BindUi(uiRoot);
    }

    public void SetPlayer(PlayerShip playerShip)
    {
        player = playerShip;
    }

    public void BindUi(Transform uiRoot)
    {
        EnsureMinimap(uiRoot);
    }

    public void Tick()
    {
        if (!minimapEnabled || minimapCamera == null || player == null || player.Transform == null)
        {
            return;
        }

        HandleManualZoomInput();
        minimapCamera.orthographicSize = minimapOrthoSize;
        Vector3 playerPosition = player.Transform.position;
        minimapCamera.transform.position = new Vector3(playerPosition.x, playerPosition.y, -40f);
        minimapCamera.transform.rotation = Quaternion.identity;
    }

    public void ZoomIn()
    {
        minimapOrthoSize = Mathf.Max(minimapMinOrthoSize, minimapOrthoSize - Mathf.Max(0.1f, minimapZoomStep));
        ApplyZoomToCamera();
    }

    public void ZoomOut()
    {
        minimapOrthoSize = Mathf.Min(minimapMaxOrthoSize, minimapOrthoSize + Mathf.Max(0.1f, minimapZoomStep));
        ApplyZoomToCamera();
    }

    public void Cleanup()
    {
        if (minimapRenderTexture != null)
        {
            if (minimapRawImage != null && minimapRawImage.texture == minimapRenderTexture)
            {
                minimapRawImage.texture = null;
            }
            minimapRenderTexture.Release();
            Destroy(minimapRenderTexture);
            minimapRenderTexture = null;
        }

        if (minimapCamera != null)
        {
            Destroy(minimapCamera.gameObject);
            minimapCamera = null;
        }
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void EnsureMinimap(Transform uiRoot)
    {
        if (!minimapEnabled || uiRoot == null)
        {
            if (minimapPanelRect != null)
            {
                minimapPanelRect.gameObject.SetActive(false);
            }
            return;
        }

        Transform minimapPanel = uiRoot.Find("MinimapPanel");
        if (minimapPanel == null)
        {
            minimapPanelRect = null;
            minimapRawImage = null;
            return;
        }

        minimapPanelRect = minimapPanel.GetComponent<RectTransform>();
        if (minimapPanelRect != null)
        {
            minimapPanelRect.gameObject.SetActive(true);
        }
        minimapRawImage = minimapPanel.GetComponentInChildren<RawImage>(true);

        if (minimapCamera == null)
        {
            GameObject minimapCameraObject = new GameObject("MinimapCamera");
            minimapCameraObject.transform.SetParent(transform, false);
            minimapCamera = minimapCameraObject.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = minimapOrthoSize;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = minimapBackgroundColor;
            minimapCamera.cullingMask = ~0;
            minimapCamera.nearClipPlane = 0.1f;
            minimapCamera.farClipPlane = 200f;
            minimapCamera.depth = -50f;
        }

        int rtSize = Mathf.RoundToInt(Mathf.Clamp(minimapPanelSize * 1.2f, 128f, 1024f));
        if (minimapRenderTexture == null || minimapRenderTexture.width != rtSize || minimapRenderTexture.height != rtSize)
        {
            if (minimapRenderTexture != null)
            {
                minimapRenderTexture.Release();
                Destroy(minimapRenderTexture);
            }

            minimapRenderTexture = new RenderTexture(rtSize, rtSize, 16, RenderTextureFormat.ARGB32);
            minimapRenderTexture.name = "Minimap_RT";
            minimapRenderTexture.Create();
        }

        minimapCamera.targetTexture = minimapRenderTexture;
        if (minimapRawImage != null)
        {
            minimapRawImage.texture = minimapRenderTexture;
            minimapRawImage.raycastTarget = false;
        }

        EnsureZoomUi(minimapPanel);
        ClampZoomBounds();
        ApplyZoomToCamera();
    }

    private void EnsureZoomUi(Transform minimapPanel)
    {
        if (minimapPanel == null)
        {
            return;
        }

        zoomInButton = EnsureZoomButton(minimapPanel, "ZoomInButton", "+", new Vector2(1f, 1f), new Vector2(-20f, -20f), true);
        zoomOutButton = EnsureZoomButton(minimapPanel, "ZoomOutButton", "-", new Vector2(1f, 1f), new Vector2(-20f, -68f), false);
    }

    private Button EnsureZoomButton(Transform parent, string objectName, string label, Vector2 anchor, Vector2 anchoredPosition, bool zoomIn)
    {
        Transform buttonTransform = parent.Find(objectName);
        Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        if (button == null)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(36f, 36f);
            rect.anchoredPosition = anchoredPosition;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.06f, 0.12f, 0.19f, 0.92f);

            button = buttonObject.GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TMP_Text tmp = labelObject.GetComponent<TMP_Text>();
            tmp.text = label;
            tmp.fontSize = 30f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        button.transform.SetAsLastSibling();

        button.onClick.RemoveAllListeners();
        if (zoomIn)
        {
            button.onClick.AddListener(ZoomIn);
            zoomInLabel = button.GetComponentInChildren<TMP_Text>(true);
        }
        else
        {
            button.onClick.AddListener(ZoomOut);
            zoomOutLabel = button.GetComponentInChildren<TMP_Text>(true);
        }

        return button;
    }

    private void ClampZoomBounds()
    {
        if (minimapMaxOrthoSize < minimapMinOrthoSize)
        {
            minimapMaxOrthoSize = minimapMinOrthoSize;
        }

        minimapOrthoSize = Mathf.Clamp(minimapOrthoSize, minimapMinOrthoSize, minimapMaxOrthoSize);
    }

    private void ApplyZoomToCamera()
    {
        ClampZoomBounds();
        if (minimapCamera != null)
        {
            minimapCamera.orthographicSize = minimapOrthoSize;
        }

        bool canZoomIn = minimapOrthoSize > minimapMinOrthoSize + 0.01f;
        bool canZoomOut = minimapOrthoSize < minimapMaxOrthoSize - 0.01f;
        if (zoomInButton != null)
        {
            zoomInButton.interactable = canZoomIn;
        }

        if (zoomOutButton != null)
        {
            zoomOutButton.interactable = canZoomOut;
        }

        Color enabledColor = Color.white;
        Color disabledColor = new Color(1f, 1f, 1f, 0.35f);
        if (zoomInLabel != null)
        {
            zoomInLabel.color = canZoomIn ? enabledColor : disabledColor;
        }

        if (zoomOutLabel != null)
        {
            zoomOutLabel.color = canZoomOut ? enabledColor : disabledColor;
        }
    }

    private void HandleManualZoomInput()
    {
        if (!TryGetPointerDown(out Vector2 screenPosition))
        {
            return;
        }

        if (zoomInButton != null && zoomInButton.interactable)
        {
            RectTransform zoomInRect = zoomInButton.transform as RectTransform;
            if (zoomInRect != null && RectTransformUtility.RectangleContainsScreenPoint(zoomInRect, screenPosition, null))
            {
                ZoomIn();
                return;
            }
        }

        if (zoomOutButton != null && zoomOutButton.interactable)
        {
            RectTransform zoomOutRect = zoomOutButton.transform as RectTransform;
            if (zoomOutRect != null && RectTransformUtility.RectangleContainsScreenPoint(zoomOutRect, screenPosition, null))
            {
                ZoomOut();
            }
        }
    }

    private static bool TryGetPointerDown(out Vector2 screenPosition)
    {
        if (Touchscreen.current != null)
        {
            if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
        }

        screenPosition = Vector2.zero;
        return false;
    }
}
