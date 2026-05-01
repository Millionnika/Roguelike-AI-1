using SpaceFrontier.Player;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MoveCommandVisualController : MonoBehaviour
{
    [Header("Визуал команды движения")]
    [Tooltip("Цвет линии команды движения от корабля к целевой точке.")]
    [SerializeField] private Color moveCommandLineColor = new Color(0.55f, 0.9f, 1f, 0.45f);
    [Tooltip("Толщина линии команды движения в мировых единицах. Обычно 0.02-0.06.")]
    [SerializeField, Min(0.01f)] private float moveCommandLineWidth = 0.03f;
    [Tooltip("Sorting Order линии команды движения. Увеличьте, если линия рисуется под объектами.")]
    [SerializeField] private int moveCommandLineSortingOrder;
    [Tooltip("Если включено, линия команды движения отображается пунктиром.")]
    [SerializeField] private bool moveCommandLineDashed = true;
    [Tooltip("Длина штриха пунктира для линии команды движения.")]
    [SerializeField, Min(0.02f)] private float moveCommandLineDashSize = 0.26f;
    [Tooltip("Длина промежутка между штрихами пунктира.")]
    [SerializeField, Min(0.01f)] private float moveCommandLineGapSize = 0.17f;
    [Tooltip("Спрайт маркера целевой точки. Если пусто, используется fallback-спрайт из контроллера сцены.")]
    [SerializeField] private Sprite moveCommandMarkerSprite;
    [Tooltip("Цвет маркера целевой точки.")]
    [SerializeField] private Color moveCommandMarkerColor = new Color(0.78f, 0.95f, 1f, 0.95f);
    [Tooltip("Размер маркера целевой точки в мировых единицах.")]
    [SerializeField, Min(0.05f)] private float moveCommandMarkerSize = 0.28f;

    private PlayerShip player;
    private Transform worldRoot;
    private Sprite fallbackMarkerSprite;
    private LineRenderer moveCommandLineRenderer;
    private GameObject moveCommandMarkerObject;
    private SpriteRenderer moveCommandMarkerRenderer;
    private Material lineMaterial;
    private Texture2D dashedLineTexture;

    public void Initialize(PlayerShip playerShip, Transform worldRootTransform, Sprite fallbackSprite)
    {
        player = playerShip;
        worldRoot = worldRootTransform;
        fallbackMarkerSprite = fallbackSprite;
        EnsureMoveCommandVisuals();
    }

    public void SetPlayer(PlayerShip playerShip)
    {
        player = playerShip;
    }

    public void SetWorldRoot(Transform worldRootTransform)
    {
        worldRoot = worldRootTransform;
        Transform parent = worldRoot != null ? worldRoot : transform;
        if (moveCommandLineRenderer != null)
        {
            moveCommandLineRenderer.transform.SetParent(parent, true);
        }
        if (moveCommandMarkerObject != null)
        {
            moveCommandMarkerObject.transform.SetParent(parent, true);
        }
    }

    public void SetFallbackMarkerSprite(Sprite fallbackSprite)
    {
        fallbackMarkerSprite = fallbackSprite;
    }

    public void Tick()
    {
        if (player == null || player.Transform == null)
        {
            return;
        }

        EnsureMoveCommandVisuals();

        bool show = player.MoveCommandActive;
        if (!show)
        {
            if (moveCommandLineRenderer != null)
            {
                moveCommandLineRenderer.gameObject.SetActive(false);
            }
            if (moveCommandMarkerObject != null)
            {
                moveCommandMarkerObject.SetActive(false);
            }
            return;
        }

        Vector3 start = player.Transform.position;
        Vector3 end = player.MoveCommandTarget;
        start.z = 0f;
        end.z = 0f;

        if (moveCommandLineRenderer != null)
        {
            ConfigureLineRenderer(
                moveCommandLineRenderer,
                moveCommandLineColor,
                moveCommandLineWidth,
                moveCommandLineSortingOrder,
                moveCommandLineDashed,
                moveCommandLineDashSize,
                moveCommandLineGapSize,
                start,
                end);
        }

        if (moveCommandMarkerObject != null && moveCommandMarkerRenderer != null)
        {
            moveCommandMarkerObject.SetActive(true);
            moveCommandMarkerObject.transform.position = end;
            moveCommandMarkerObject.transform.rotation = Quaternion.identity;

            Sprite markerSprite = moveCommandMarkerSprite != null ? moveCommandMarkerSprite : fallbackMarkerSprite;
            moveCommandMarkerRenderer.sprite = markerSprite;
            moveCommandMarkerRenderer.color = moveCommandMarkerColor;
            moveCommandMarkerRenderer.sortingOrder = moveCommandLineSortingOrder + 1;

            float scale = moveCommandMarkerSize;
            if (markerSprite != null)
            {
                Vector3 spriteSize = markerSprite.bounds.size;
                float baseSize = Mathf.Max(spriteSize.x, spriteSize.y, 0.001f);
                scale = moveCommandMarkerSize / baseSize;
            }

            moveCommandMarkerObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
        if (dashedLineTexture != null)
        {
            Destroy(dashedLineTexture);
        }
    }

    private void EnsureMoveCommandVisuals()
    {
        Transform parent = worldRoot != null ? worldRoot : transform;
        if (moveCommandLineRenderer == null)
        {
            GameObject lineObject = new GameObject("MoveCommandLine");
            lineObject.transform.SetParent(parent, false);
            moveCommandLineRenderer = lineObject.AddComponent<LineRenderer>();
            moveCommandLineRenderer.positionCount = 2;
            moveCommandLineRenderer.useWorldSpace = true;
            moveCommandLineRenderer.alignment = LineAlignment.View;
            moveCommandLineRenderer.textureMode = LineTextureMode.Stretch;
            moveCommandLineRenderer.numCapVertices = 4;
            moveCommandLineRenderer.sortingOrder = moveCommandLineSortingOrder;
            moveCommandLineRenderer.material = GetLineMaterial();
            moveCommandLineRenderer.gameObject.SetActive(false);
        }

        if (moveCommandMarkerObject == null)
        {
            moveCommandMarkerObject = new GameObject("MoveCommandMarker");
            moveCommandMarkerObject.transform.SetParent(parent, false);
            moveCommandMarkerRenderer = moveCommandMarkerObject.AddComponent<SpriteRenderer>();
            moveCommandMarkerRenderer.sortingOrder = moveCommandLineSortingOrder + 1;
            moveCommandMarkerObject.SetActive(false);
        }
    }

    private void ConfigureLineRenderer(
        LineRenderer renderer,
        Color color,
        float width,
        int sortingOrder,
        bool dashed,
        float dashSize,
        float gapSize,
        Vector3 start,
        Vector3 end)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.gameObject.SetActive(true);
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.sortingOrder = sortingOrder;
        renderer.SetPosition(0, start);
        renderer.SetPosition(1, end);

        if (renderer.material == null)
        {
            renderer.material = GetLineMaterial();
        }

        if (renderer.material == null)
        {
            return;
        }

        if (dashed)
        {
            renderer.textureMode = LineTextureMode.Tile;
            renderer.material.mainTexture = GetDashedLineTexture();
            float segment = Mathf.Max(0.01f, dashSize + gapSize);
            float lineLength = Vector3.Distance(start, end);
            renderer.material.mainTextureScale = new Vector2(Mathf.Max(1f, lineLength / segment), 1f);
        }
        else
        {
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.material.mainTexture = null;
            renderer.material.mainTextureScale = Vector2.one;
        }
    }

    private Material GetLineMaterial()
    {
        if (lineMaterial != null)
        {
            return lineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        lineMaterial = shader != null ? new Material(shader) : null;
        return lineMaterial;
    }

    private Texture2D GetDashedLineTexture()
    {
        if (dashedLineTexture != null)
        {
            return dashedLineTexture;
        }

        dashedLineTexture = new Texture2D(2, 1, TextureFormat.RGBA32, false);
        dashedLineTexture.filterMode = FilterMode.Point;
        dashedLineTexture.wrapMode = TextureWrapMode.Repeat;
        dashedLineTexture.SetPixel(0, 0, Color.white);
        dashedLineTexture.SetPixel(1, 0, new Color(1f, 1f, 1f, 0f));
        dashedLineTexture.Apply();
        return dashedLineTexture;
    }
}
