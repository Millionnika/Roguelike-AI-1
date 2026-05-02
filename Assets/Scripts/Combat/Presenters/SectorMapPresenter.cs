using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SectorMapPresenter : MonoBehaviour
{
    [Header("Панель карты сектора")]
    [Tooltip("Корневой объект панели карты сектора. Если не назначен, создается автоматически.")]
    [SerializeField] private GameObject panelObject;
    [Tooltip("Заголовок карты сектора.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Подсказка под заголовком карты.")]
    [SerializeField] private TMP_Text hintText;
    [Tooltip("Контейнер, внутри которого размещаются линии и узлы маршрута.")]
    [SerializeField] private RectTransform mapRoot;

    [Header("Геометрия карты")]
    [Tooltip("Размер круглой кнопки узла на карте.")]
    [SerializeField, Min(24f)] private float nodeSize = 58f;
    [Tooltip("Горизонтальный шаг между координатами карты.")]
    [SerializeField, Min(40f)] private float spacingX = 130f;
    [Tooltip("Вертикальный шаг между координатами карты.")]
    [SerializeField, Min(40f)] private float spacingY = 92f;
    [Tooltip("Толщина линий между связанными узлами.")]
    [SerializeField, Min(1f)] private float lineWidth = 5f;
    [Tooltip("Размер шрифта подписи внутри узла.")]
    [SerializeField, Min(8)] private int fontSize = 12;
    [Tooltip("Показывать координаты узлов для отладки маршрута.")]
    [SerializeField] private bool showDebugCoordinates;

    [Header("Цвета карты")]
    [Tooltip("Цвет фона панели карты сектора.")]
    [SerializeField] private Color panelBackgroundColor = new Color(0.035f, 0.075f, 0.12f, 0.98f);
    [Tooltip("Цвет обычного будущего узла.")]
    [SerializeField] private Color normalNodeColor = new Color(0.18f, 0.24f, 0.32f, 0.95f);
    [Tooltip("Цвет текущего узла игрока.")]
    [SerializeField] private Color currentNodeColor = new Color(1f, 0.78f, 0.18f, 1f);
    [Tooltip("Цвет доступного для выбора следующего узла.")]
    [SerializeField] private Color reachableNodeColor = new Color(0.22f, 0.72f, 0.34f, 1f);
    [Tooltip("Цвет посещенного или завершенного узла.")]
    [SerializeField] private Color visitedNodeColor = new Color(0.28f, 0.46f, 0.68f, 0.92f);
    [Tooltip("Цвет недоступного или заблокированного узла.")]
    [SerializeField] private Color lockedNodeColor = new Color(0.08f, 0.09f, 0.11f, 0.68f);
    [Tooltip("Цвет стартового узла.")]
    [SerializeField] private Color startNodeColor = new Color(0.35f, 0.85f, 0.42f, 1f);
    [Tooltip("Цвет финального или boss-узла.")]
    [SerializeField] private Color bossNodeColor = new Color(0.82f, 0.18f, 0.24f, 1f);
    [Tooltip("Цвет линии к доступному узлу.")]
    [SerializeField] private Color reachableLineColor = new Color(0.28f, 0.84f, 0.42f, 0.95f);
    [Tooltip("Цвет уже пройденной линии маршрута.")]
    [SerializeField] private Color visitedLineColor = new Color(0.3f, 0.58f, 0.86f, 0.85f);
    [Tooltip("Цвет линии будущего или недоступного маршрута.")]
    [SerializeField] private Color lockedLineColor = new Color(0.18f, 0.22f, 0.28f, 0.6f);

    [Header("Иконки узлов")]
    [Tooltip("Иконка боевого узла. Если не назначена, используется текст.")]
    [SerializeField] private Sprite combatIcon;
    [Tooltip("Иконка ремонтного узла. Если не назначена, используется текст.")]
    [SerializeField] private Sprite repairIcon;
    [Tooltip("Иконка узла отдыха. Если не назначена, используется текст.")]
    [SerializeField] private Sprite restIcon;
    [Tooltip("Иконка события. Если не назначена, используется текст.")]
    [SerializeField] private Sprite eventIcon;
    [Tooltip("Иконка элитного боя. Если не назначена, используется текст.")]
    [SerializeField] private Sprite eliteIcon;
    [Tooltip("Иконка ресурсного узла. Если не назначена, используется текст.")]
    [SerializeField] private Sprite resourceIcon;
    [Tooltip("Иконка boss-узла. Если не назначена, используется текст.")]
    [SerializeField] private Sprite bossIcon;
    [Tooltip("Иконка стартового узла. Если не назначена, используется текст.")]
    [SerializeField] private Sprite startIcon;

    private readonly List<GameObject> runtimeObjects = new List<GameObject>();
    private readonly List<SectorMapNode> currentNodes = new List<SectorMapNode>();
    private readonly HashSet<SectorMapNode> reachableSet = new HashSet<SectorMapNode>();
    private readonly Dictionary<Vector2Int, SectorMapNode> nodeByCoordinates = new Dictionary<Vector2Int, SectorMapNode>();
    private readonly Dictionary<SectorMapNode, Vector2> nodePositions = new Dictionary<SectorMapNode, Vector2>();
    private Action<SectorMapNode> onSelected;
    private Sprite runtimeCircleSprite;
    private Sprite runtimeLineSprite;

    public bool HasPanel => panelObject != null;
    public bool IsVisible => panelObject != null && panelObject.activeSelf;

    public void Show(IReadOnlyList<SectorMapNode> nodes, IReadOnlyList<SectorMapNode> reachableNodes, Action<SectorMapNode> onSelectedCallback)
    {
        EnsureUi();
        if (panelObject == null || mapRoot == null)
        {
            return;
        }

        onSelected = onSelectedCallback;
        PrepareNodeLists(nodes, reachableNodes);
        ClearRuntimeObjects();

        if (currentNodes.Count == 0)
        {
            Hide();
            return;
        }

        titleText.text = "Карта сектора";
        hintText.text = "Выберите следующий сектор";

        CalculateNodePositions();
        BuildRouteLines();
        BuildNodeButtons();
        panelObject.SetActive(true);
    }

    public void Hide()
    {
        ClearRuntimeObjects();
        currentNodes.Clear();
        reachableSet.Clear();
        nodeByCoordinates.Clear();
        nodePositions.Clear();
        onSelected = null;
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }
    }

    private void PrepareNodeLists(IReadOnlyList<SectorMapNode> nodes, IReadOnlyList<SectorMapNode> reachableNodes)
    {
        currentNodes.Clear();
        reachableSet.Clear();
        nodeByCoordinates.Clear();

        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                SectorMapNode node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                currentNodes.Add(node);
                nodeByCoordinates[node.Coordinates] = node;
            }
        }

        if (reachableNodes != null)
        {
            for (int i = 0; i < reachableNodes.Count; i++)
            {
                if (reachableNodes[i] != null)
                {
                    reachableSet.Add(reachableNodes[i]);
                }
            }
        }
    }

    private void CalculateNodePositions()
    {
        nodePositions.Clear();

        int maxX = 0;
        int maxY = 0;
        for (int i = 0; i < currentNodes.Count; i++)
        {
            maxX = Mathf.Max(maxX, currentNodes[i].x);
            maxY = Mathf.Max(maxY, currentNodes[i].y);
        }

        float totalWidth = maxX * spacingX;
        float totalHeight = maxY * spacingY;
        for (int i = 0; i < currentNodes.Count; i++)
        {
            SectorMapNode node = currentNodes[i];
            nodePositions[node] = new Vector2(
                -totalWidth * 0.5f + node.x * spacingX,
                -totalHeight * 0.5f + node.y * spacingY);
        }
    }

    private void BuildRouteLines()
    {
        RectTransform lineRoot = new GameObject("RouteLines", typeof(RectTransform)).GetComponent<RectTransform>();
        lineRoot.SetParent(mapRoot, false);
        Stretch(lineRoot);
        runtimeObjects.Add(lineRoot.gameObject);

        for (int i = 0; i < currentNodes.Count; i++)
        {
            SectorMapNode from = currentNodes[i];
            if (from == null || from.nextCoordinates == null)
            {
                continue;
            }

            for (int c = 0; c < from.nextCoordinates.Count; c++)
            {
                if (!nodeByCoordinates.TryGetValue(from.nextCoordinates[c], out SectorMapNode to))
                {
                    continue;
                }

                CreateRouteLine(lineRoot, from, to);
            }
        }
    }

    private void CreateRouteLine(Transform parent, SectorMapNode from, SectorMapNode to)
    {
        if (!nodePositions.TryGetValue(from, out Vector2 start) || !nodePositions.TryGetValue(to, out Vector2 end))
        {
            return;
        }

        Image line = CreateImage("Line_" + from.x + "_" + from.y + "_to_" + to.x + "_" + to.y, parent, ResolveLineColor(from, to));
        line.sprite = GetLineSprite();
        RectTransform rect = line.rectTransform;
        Vector2 delta = end - start;
        rect.sizeDelta = new Vector2(delta.magnitude, lineWidth);
        rect.anchoredPosition = (start + end) * 0.5f;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private void BuildNodeButtons()
    {
        RectTransform nodeRoot = new GameObject("RouteNodes", typeof(RectTransform)).GetComponent<RectTransform>();
        nodeRoot.SetParent(mapRoot, false);
        Stretch(nodeRoot);
        runtimeObjects.Add(nodeRoot.gameObject);

        for (int i = 0; i < currentNodes.Count; i++)
        {
            SectorMapNode node = currentNodes[i];
            if (node == null || !nodePositions.TryGetValue(node, out Vector2 position))
            {
                continue;
            }

            GameObject buttonObject = new GameObject("Node_" + node.x + "_" + node.y, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(nodeRoot, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(nodeSize, nodeSize);
            rect.anchoredPosition = position;

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = GetCircleSprite();
            image.color = ResolveNodeColor(node);

            Button button = buttonObject.GetComponent<Button>();
            bool interactable = reachableSet.Contains(node) && node.encounter != null && !node.visited && !node.completed && !node.locked;
            button.interactable = interactable;
            if (interactable)
            {
                button.onClick.AddListener(() => onSelected?.Invoke(node));
            }

            BuildNodeContent(buttonObject.transform, node);
        }
    }

    private void BuildNodeContent(Transform parent, SectorMapNode node)
    {
        Sprite icon = ResolveIcon(node);
        if (icon != null)
        {
            Image iconImage = CreateImage("Icon", parent, Color.white);
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(nodeSize * 0.46f, nodeSize * 0.46f);
            iconRect.anchoredPosition = Vector2.zero;
        }

        TMP_Text label = CreateText("Label", parent, BuildNodeLabel(node), fontSize, FontStyle.Bold, Color.white);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        Stretch(label.rectTransform);
    }

    private Color ResolveNodeColor(SectorMapNode node)
    {
        if (node == null || node.locked || node.encounter == null)
        {
            return lockedNodeColor;
        }

        if (node.encounter.nodeType == LocationNodeType.Boss)
        {
            return node.current ? currentNodeColor : bossNodeColor;
        }

        if (node.x == 0 && node.y == 0)
        {
            return node.current ? currentNodeColor : startNodeColor;
        }

        if (node.current)
        {
            return currentNodeColor;
        }

        if (reachableSet.Contains(node))
        {
            return reachableNodeColor;
        }

        if (node.visited || node.completed)
        {
            return visitedNodeColor;
        }

        return normalNodeColor;
    }

    private Color ResolveLineColor(SectorMapNode from, SectorMapNode to)
    {
        if (from != null && to != null && (from.visited || from.completed) && (to.visited || to.current || to.completed))
        {
            return visitedLineColor;
        }

        if (from != null && from.current && to != null && to.reachable)
        {
            return reachableLineColor;
        }

        return lockedLineColor;
    }

    private string BuildNodeLabel(SectorMapNode node)
    {
        if (node == null || node.encounter == null)
        {
            return "-";
        }

        string statePrefix = string.Empty;
        if (node.current)
        {
            statePrefix = "●\n";
        }
        else if (node.visited || node.completed)
        {
            statePrefix = "✓\n";
        }

        string label = statePrefix + GetNodeTypeDisplayName(node);
        if (showDebugCoordinates)
        {
            label += "\n" + node.x + "," + node.y;
        }

        return label;
    }

    private Sprite ResolveIcon(SectorMapNode node)
    {
        if (node == null || node.encounter == null)
        {
            return null;
        }

        if (node.x == 0 && node.y == 0 && startIcon != null)
        {
            return startIcon;
        }

        switch (node.encounter.nodeType)
        {
            case LocationNodeType.Combat:
                return combatIcon;
            case LocationNodeType.Repair:
                return repairIcon;
            case LocationNodeType.Rest:
                return restIcon;
            case LocationNodeType.Event:
                return eventIcon;
            case LocationNodeType.Elite:
                return eliteIcon;
            case LocationNodeType.Resource:
                return resourceIcon;
            case LocationNodeType.Boss:
                return bossIcon;
            default:
                return null;
        }
    }

    private static string GetNodeTypeDisplayName(SectorMapNode node)
    {
        if (node != null && node.x == 0 && node.y == 0)
        {
            return "Старт";
        }

        return node != null && node.encounter != null ? GetNodeTypeDisplayName(node.encounter.nodeType) : "-";
    }

    private static string GetNodeTypeDisplayName(LocationNodeType nodeType)
    {
        switch (nodeType)
        {
            case LocationNodeType.Combat:
                return "Бой";
            case LocationNodeType.Elite:
                return "Элита";
            case LocationNodeType.Repair:
                return "Ремонт";
            case LocationNodeType.Rest:
                return "Отдых";
            case LocationNodeType.Event:
                return "Событие";
            case LocationNodeType.Resource:
                return "Ресурсы";
            case LocationNodeType.Boss:
                return "Босс";
            case LocationNodeType.Shop:
                return "Магазин";
            default:
                return nodeType.ToString();
        }
    }

    private void EnsureUi()
    {
        if (panelObject != null && mapRoot != null)
        {
            return;
        }

        Canvas parentCanvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (parentCanvas == null)
        {
            GameObject canvasObject = new GameObject("SectorMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            parentCanvas = canvas;
        }

        EnsureEventSystem();
        BuildRuntimePanel(parentCanvas.transform);
    }

    private void BuildRuntimePanel(Transform parent)
    {
        panelObject = new GameObject("SectorMapPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);
        RectTransform rootRect = panelObject.GetComponent<RectTransform>();
        Stretch(rootRect);

        Image dim = CreateImage("Dimmer", panelObject.transform, new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);

        Image panel = CreateImage("Panel", panelObject.transform, panelBackgroundColor);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(820f, 640f);

        titleText = CreateText("Title", panel.transform, "Карта сектора", 32, FontStyle.Bold, Color.white);
        titleText.alignment = TextAlignmentOptions.Center;
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -24f), new Vector2(-24f, -72f));

        hintText = CreateText("Hint", panel.transform, "Выберите следующий сектор", 20, FontStyle.Normal, new Color(0.84f, 0.92f, 1f, 1f));
        hintText.alignment = TextAlignmentOptions.Center;
        SetRect(hintText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -76f), new Vector2(-24f, -118f));

        mapRoot = new GameObject("RouteMap", typeof(RectTransform)).GetComponent<RectTransform>();
        mapRoot.SetParent(panel.transform, false);
        SetRect(mapRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -245f), new Vector2(330f, 205f));

        panelObject.SetActive(false);
    }

    private void ClearRuntimeObjects()
    {
        for (int i = runtimeObjects.Count - 1; i >= 0; i--)
        {
            if (runtimeObjects[i] != null)
            {
                Destroy(runtimeObjects[i]);
            }
        }

        runtimeObjects.Clear();
    }

    private Sprite GetCircleSprite()
    {
        if (runtimeCircleSprite != null)
        {
            return runtimeCircleSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "SectorMapNodeCircle";
        float radius = (size - 2) * 0.5f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = distance <= radius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        runtimeCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return runtimeCircleSprite;
    }

    private Sprite GetLineSprite()
    {
        if (runtimeLineSprite != null)
        {
            return runtimeLineSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "SectorMapLine";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        runtimeLineSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return runtimeLineSprite;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetAsLastSibling();
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string content, int size, FontStyle style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
