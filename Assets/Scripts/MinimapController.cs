using SpaceFrontier.Player;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MinimapController : MonoBehaviour
{
    [Header("Миникарта")]
    [Tooltip("Включает или отключает систему миникарты. Если выключено, панель скрывается и камера миникарты не создается.")]
    [SerializeField] private bool minimapEnabled = true;
    [Tooltip("Размер области обзора миникарты (ортографический размер камеры). Больше значение = дальше «отъезд» камеры.")]
    [SerializeField, Min(2f)] private float minimapOrthoSize = 20f;
    [Tooltip("Базовый размер панели миникарты в пикселях. Используется для расчета разрешения RenderTexture.")]
    [SerializeField, Min(96f)] private float minimapPanelSize = 200f;
    [Tooltip("Цвет фона миникарты (используется как цвет очистки камеры).")]
    [SerializeField] private Color minimapBackgroundColor = new Color(0.03f, 0.08f, 0.14f, 0.82f);

    private PlayerShip player;
    private Camera minimapCamera;
    private RenderTexture minimapRenderTexture;
    private RawImage minimapRawImage;
    private RectTransform minimapPanelRect;

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

        minimapCamera.orthographicSize = minimapOrthoSize;
        Vector3 playerPosition = player.Transform.position;
        minimapCamera.transform.position = new Vector3(playerPosition.x, playerPosition.y, -40f);
        minimapCamera.transform.rotation = Quaternion.identity;
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
        }
    }
}
