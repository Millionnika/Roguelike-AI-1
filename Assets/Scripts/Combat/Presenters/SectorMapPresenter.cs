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
    [Tooltip("Текст заголовка карты.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Текст подсказки под заголовком.")]
    [SerializeField] private TMP_Text hintText;
    [Tooltip("Контейнер, в котором рисуются фон, линии и узлы.")]
    [SerializeField] private RectTransform mapRoot;

    [Header("Визуал фона")]
    [Tooltip("Фон карты сектора. Если не назначен, используется однотонный фон.")]
    [SerializeField] private Sprite mapBackgroundSprite;
    [Tooltip("Цвет/прозрачность фона карты сектора.")]
    [SerializeField] private Color panelBackgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.95f);
    [Tooltip("Цвет затемнения за панелью карты.")]
    [SerializeField] private Color dimBackgroundColor = new Color(0f, 0f, 0f, 0.86f);

    [Header("Геометрия карты")]
    [Tooltip("Размер узла на карте.")]
    [SerializeField, Min(14f)] private float nodeSize = 30f;
    [Tooltip("Масштаб по X между логическими позициями узлов.")]
    [SerializeField, Min(24f)] private float spacingX = 88f;
    [Tooltip("Масштаб по Y между логическими позициями узлов.")]
    [SerializeField, Min(24f)] private float spacingY = 78f;
    [Tooltip("Толщина линии маршрута.")]
    [SerializeField, Min(1f)] private float lineWidth = 3.5f;
    [Tooltip("Размер шрифта подписи узла.")]
    [SerializeField, Min(8)] private int fontSize = 13;
    [Tooltip("Внутренний отступ карты от границ панели.")]
    [SerializeField, Min(0f)] private float mapPadding = 34f;
    [Tooltip("Показывать координаты узлов для отладки.")]
    [SerializeField] private bool showDebugCoordinates;
    [Tooltip("Показывать подписи только для важных узлов (Старт, Выход, Элита, Событие, Ремонт, Отдых).")]
    [SerializeField] private bool showOnlyImportantLabels = true;

    [Header("Цвета узлов")]
    [Tooltip("Цвет обычного узла.")]
    [SerializeField] private Color normalNodeColor = new Color(0.7f, 0.78f, 0.92f, 1f);
    [Tooltip("Цвет текущего узла игрока.")]
    [SerializeField] private Color currentNodeColor = new Color(1f, 0.9f, 0.22f, 1f);
    [Tooltip("Цвет доступного для выбора узла.")]
    [SerializeField] private Color reachableNodeColor = new Color(0.26f, 0.92f, 0.42f, 1f);
    [Tooltip("Цвет посещенного узла.")]
    [SerializeField] private Color visitedNodeColor = new Color(0.42f, 0.62f, 0.86f, 1f);
    [Tooltip("Цвет недоступного/заблокированного узла.")]
    [SerializeField] private Color lockedNodeColor = new Color(0.22f, 0.24f, 0.3f, 0.48f);
    [Tooltip("Цвет стартового узла.")]
    [SerializeField] private Color startNodeColor = new Color(0.5f, 0.96f, 0.48f, 1f);
    [Tooltip("Цвет финального узла.")]
    [SerializeField] private Color bossNodeColor = new Color(0.88f, 0.24f, 0.3f, 1f);

    [Header("Цвета линий")]
    [Tooltip("Показывать постоянные линии маршрутов. Для чистого вида карты можно выключить и использовать только предпросмотр при наведении.")]
    [SerializeField] private bool showStaticConnections;
    [Tooltip("Цвет линии к доступному следующему узлу.")]
    [SerializeField] private Color reachableLineColor = new Color(0.18f, 0.95f, 0.48f, 1f);
    [Tooltip("Цвет пройденной линии.")]
    [SerializeField] private Color visitedLineColor = new Color(0.42f, 0.7f, 1f, 0.95f);
    [Tooltip("Цвет недоступной линии.")]
    [SerializeField] private Color lockedLineColor = new Color(0.33f, 0.38f, 0.48f, 0.28f);
    [Tooltip("Цвет пунктирного предпросмотра маршрута при наведении на узел.")]
    [SerializeField] private Color hoverPreviewLineColor = new Color(0.48f, 0.88f, 1f, 0.95f);
    [Tooltip("Толщина пунктирной линии предпросмотра.")]
    [SerializeField, Min(1f)] private float hoverPreviewLineWidth = 3f;
    [Tooltip("Длина штриха пунктирной линии предпросмотра.")]
    [SerializeField, Min(2f)] private float hoverDashLength = 12f;
    [Tooltip("Промежуток между штрихами пунктирной линии предпросмотра.")]
    [SerializeField, Min(1f)] private float hoverDashGap = 8f;

    [Header("Иконки узлов (опционально)")]
    [Tooltip("Иконка боевого узла.")]
    [SerializeField] private Sprite combatIcon;
    [Tooltip("Иконка ремонтного узла.")]
    [SerializeField] private Sprite repairIcon;
    [Tooltip("Иконка узла отдыха.")]
    [SerializeField] private Sprite restIcon;
    [Tooltip("Иконка узла события.")]
    [SerializeField] private Sprite eventIcon;
    [Tooltip("Иконка элитного узла.")]
    [SerializeField] private Sprite eliteIcon;
    [Tooltip("Иконка ресурсного узла.")]
    [SerializeField] private Sprite resourceIcon;
    [Tooltip("Иконка узла босса.")]
    [SerializeField] private Sprite bossIcon;
    [Tooltip("Иконка стартового узла.")]
    [SerializeField] private Sprite startIcon;

    private readonly List<GameObject> runtimeObjects = new List<GameObject>();
    private readonly List<SectorMapNode> currentNodes = new List<SectorMapNode>();
    private readonly HashSet<SectorMapNode> reachableSet = new HashSet<SectorMapNode>();
    private readonly Dictionary<Vector2Int, SectorMapNode> nodeByCoordinates = new Dictionary<Vector2Int, SectorMapNode>();
    private readonly Dictionary<SectorMapNode, Vector2> nodeAnchors = new Dictionary<SectorMapNode, Vector2>();
    private readonly List<GameObject> hoverPreviewObjects = new List<GameObject>();
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

        BuildBackground();
        CalculateNodeAnchors();
        if (showStaticConnections)
        {
            BuildRouteLines();
        }
        BuildNodeButtons();
        panelObject.SetActive(true);
    }

    public void Hide()
    {
        ClearRuntimeObjects();
        currentNodes.Clear();
        reachableSet.Clear();
        nodeByCoordinates.Clear();
        nodeAnchors.Clear();
        ClearHoverPreview();
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
                if (!IsRenderableGameplayNode(node))
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
                SectorMapNode node = reachableNodes[i];
                if (IsRenderableGameplayNode(node))
                {
                    reachableSet.Add(node);
                }
            }
        }
    }

    private static bool IsRenderableGameplayNode(SectorMapNode node)
    {
        if (node == null || node.encounter == null || node.locked)
        {
            return false;
        }

        if (node.isHome || node.isFinish)
        {
            return true;
        }

        bool hasOutgoing = node.nextCoordinates != null && node.nextCoordinates.Count > 0;
        bool hasIncomingHint = node.current || node.reachable || node.visited || node.completed;
        return hasOutgoing || hasIncomingHint;
    }

    private void BuildBackground()
    {
        Image bg = CreateImage("MapBackground", mapRoot, panelBackgroundColor);
        Stretch(bg.rectTransform);
        bg.raycastTarget = false;
        if (mapBackgroundSprite != null)
        {
            bg.sprite = mapBackgroundSprite;
            bg.type = Image.Type.Sliced;
            bg.preserveAspect = false;
        }

        runtimeObjects.Add(bg.gameObject);
    }

    private void CalculateNodeAnchors()
    {
        nodeAnchors.Clear();
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < currentNodes.Count; i++)
        {
            Vector2 p = currentNodes[i].mapPosition;
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        float spanX = Mathf.Max(0.001f, maxX - minX);
        float spanY = Mathf.Max(0.001f, maxY - minY);
        float viewW = Mathf.Max(64f, mapRoot.rect.width - mapPadding * 2f);
        float viewH = Mathf.Max(64f, mapRoot.rect.height - mapPadding * 2f);
        float scale = Mathf.Min(viewW / (spanX * spacingX), viewH / (spanY * spacingY));

        for (int i = 0; i < currentNodes.Count; i++)
        {
            SectorMapNode node = currentNodes[i];
            float nx = (node.mapPosition.x - minX) * spacingX * scale;
            float ny = (node.mapPosition.y - minY) * spacingY * scale;
            float centeredX = nx - (spanX * spacingX * scale * 0.5f);
            float centeredY = ny - (spanY * spacingY * scale * 0.5f);
            nodeAnchors[node] = new Vector2(centeredX, centeredY);
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

                CreateLine(lineRoot, from, to);
            }
        }
    }

    private void CreateLine(Transform parent, SectorMapNode from, SectorMapNode to)
    {
        if (!nodeAnchors.TryGetValue(from, out Vector2 start) || !nodeAnchors.TryGetValue(to, out Vector2 end))
        {
            return;
        }

        Image line = CreateImage($"Line_{from.x}_{from.y}_to_{to.x}_{to.y}", parent, ResolveLineColor(from, to));
        line.sprite = GetLineSprite();
        line.type = Image.Type.Sliced;
        RectTransform rect = line.rectTransform;
        Vector2 delta = end - start;
        rect.sizeDelta = new Vector2(delta.magnitude, lineWidth);
        rect.anchoredPosition = (start + end) * 0.5f;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        line.raycastTarget = false;
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
            if (node == null || !nodeAnchors.TryGetValue(node, out Vector2 pos))
            {
                continue;
            }

            GameObject buttonObject = new GameObject($"Node_{node.x}_{node.y}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(nodeRoot, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(nodeSize, nodeSize);
            rect.anchoredPosition = pos;

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = ResolveNodeIcon(node) ?? GetCircleSprite();
            image.color = ResolveNodeColor(node);

            Button button = buttonObject.GetComponent<Button>();
            bool clickable = reachableSet.Contains(node) && node.encounter != null && !node.visited && !node.completed && !node.locked;
            button.interactable = clickable;
            if (clickable)
            {
                button.onClick.AddListener(() => onSelected?.Invoke(node));
            }

            AddHoverPreviewHandlers(buttonObject, node, clickable);

            string labelText = BuildNodeLabel(node);
            if (string.IsNullOrEmpty(labelText))
            {
                continue;
            }

            TMP_Text label = CreateText("Label", buttonObject.transform, labelText, fontSize, FontStyle.Bold, Color.white);
            label.alignment = TextAlignmentOptions.Bottom;
            label.overflowMode = TextOverflowModes.Overflow;
            RectTransform lRect = label.rectTransform;
            lRect.anchorMin = new Vector2(0.5f, 0f);
            lRect.anchorMax = new Vector2(0.5f, 0f);
            lRect.pivot = new Vector2(0.5f, 1f);
            lRect.sizeDelta = new Vector2(nodeSize * 4f, 38f);
            lRect.anchoredPosition = new Vector2(0f, -nodeSize * 0.55f);
        }
    }

    private Color ResolveNodeColor(SectorMapNode node)
    {
        if (node == null || node.locked || node.encounter == null)
        {
            return lockedNodeColor;
        }

        if (node.current)
        {
            return currentNodeColor;
        }

        if (node.isFinish)
        {
            return bossNodeColor;
        }

        if (node.isHome)
        {
            return startNodeColor;
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
        if (node == null)
        {
            return string.Empty;
        }

        string state = node.current ? "● " : node.visited || node.completed ? "✓ " : string.Empty;
        if (showOnlyImportantLabels && !ShouldShowLabel(node))
        {
            if (showDebugCoordinates)
            {
                return "(" + node.x + "," + node.y + ")";
            }

            return string.Empty;
        }

        string label = state + GetNodeDisplayName(node);
        if (showDebugCoordinates)
        {
            label += $" ({node.x},{node.y})";
        }

        return label;
    }

    private static bool ShouldShowLabel(SectorMapNode node)
    {
        if (node == null)
        {
            return false;
        }

        if (node.isHome || node.isFinish || node.current || node.reachable)
        {
            return true;
        }

        if (node.encounter == null)
        {
            return false;
        }

        switch (node.encounter.nodeType)
        {
            case LocationNodeType.Event:
            case LocationNodeType.Elite:
            case LocationNodeType.Repair:
            case LocationNodeType.Rest:
            case LocationNodeType.Boss:
                return true;
            default:
                return false;
        }
    }

    private static string GetNodeDisplayName(SectorMapNode node)
    {
        if (node == null)
        {
            return "-";
        }

        if (node.isHome)
        {
            return "Старт";
        }

        if (node.isFinish)
        {
            return "Выход";
        }

        return node.encounter != null ? GetNodeTypeName(node.encounter.nodeType) : "-";
    }

    private static string GetNodeTypeName(LocationNodeType type)
    {
        switch (type)
        {
            case LocationNodeType.Combat: return "Бой";
            case LocationNodeType.Elite: return "Элита";
            case LocationNodeType.Repair: return "Ремонт";
            case LocationNodeType.Rest: return "Отдых";
            case LocationNodeType.Event: return "Событие";
            case LocationNodeType.Resource: return "Ресурсы";
            case LocationNodeType.Boss: return "Босс";
            case LocationNodeType.Shop: return "Магазин";
            default: return type.ToString();
        }
    }

    private Sprite ResolveNodeIcon(SectorMapNode node)
    {
        if (node == null)
        {
            return null;
        }

        if (node.isHome && startIcon != null)
        {
            return startIcon;
        }

        if (node.isFinish && bossIcon != null)
        {
            return bossIcon;
        }

        if (node.encounter == null)
        {
            return null;
        }

        switch (node.encounter.nodeType)
        {
            case LocationNodeType.Combat: return combatIcon;
            case LocationNodeType.Repair: return repairIcon;
            case LocationNodeType.Rest: return restIcon;
            case LocationNodeType.Event: return eventIcon;
            case LocationNodeType.Elite: return eliteIcon;
            case LocationNodeType.Resource: return resourceIcon;
            case LocationNodeType.Boss: return bossIcon;
            default: return null;
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

        Image dim = CreateImage("Dimmer", panelObject.transform, dimBackgroundColor);
        Stretch(dim.rectTransform);

        Image panel = CreateImage("Panel", panelObject.transform, panelBackgroundColor);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1120f, 760f);

        titleText = CreateText("Title", panel.transform, "Карта сектора", 34, FontStyle.Bold, Color.white);
        titleText.alignment = TextAlignmentOptions.Center;
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -20f), new Vector2(-20f, -72f));

        hintText = CreateText("Hint", panel.transform, "Выберите следующий сектор", 20, FontStyle.Normal, new Color(0.85f, 0.93f, 1f, 1f));
        hintText.alignment = TextAlignmentOptions.Center;
        SetRect(hintText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -74f), new Vector2(-20f, -118f));

        mapRoot = new GameObject("RouteMap", typeof(RectTransform)).GetComponent<RectTransform>();
        mapRoot.SetParent(panel.transform, false);
        SetRect(mapRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, -300f), new Vector2(520f, 260f));

        panelObject.SetActive(false);
    }

    private void ClearRuntimeObjects()
    {
        ClearHoverPreview();
        for (int i = runtimeObjects.Count - 1; i >= 0; i--)
        {
            if (runtimeObjects[i] != null)
            {
                Destroy(runtimeObjects[i]);
            }
        }

        runtimeObjects.Clear();
    }

    private void AddHoverPreviewHandlers(GameObject target, SectorMapNode node, bool enabled)
    {
        if (target == null || node == null || !enabled)
        {
            return;
        }

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = target.AddComponent<EventTrigger>();
        }

        trigger.triggers ??= new List<EventTrigger.Entry>();
        trigger.triggers.Clear();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowHoverPreview(node));
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => ClearHoverPreview());
        trigger.triggers.Add(exit);
    }

    private void ShowHoverPreview(SectorMapNode hoveredNode)
    {
        ClearHoverPreview();
        if (hoveredNode == null || !nodeAnchors.TryGetValue(hoveredNode, out Vector2 hoveredPos))
        {
            return;
        }

        RectTransform hoverRoot = new GameObject("HoverPreviewLines", typeof(RectTransform)).GetComponent<RectTransform>();
        hoverRoot.SetParent(mapRoot, false);
        Stretch(hoverRoot);
        hoverPreviewObjects.Add(hoverRoot.gameObject);

        SectorMapNode current = FindCurrentNode();
        if (current != null && nodeAnchors.TryGetValue(current, out Vector2 currentPos))
        {
            CreateDashedLine(hoverRoot, currentPos, hoveredPos, hoverPreviewLineColor, hoverPreviewLineWidth);
        }

        if (hoveredNode.nextCoordinates == null)
        {
            return;
        }

        for (int i = 0; i < hoveredNode.nextCoordinates.Count; i++)
        {
            if (!nodeByCoordinates.TryGetValue(hoveredNode.nextCoordinates[i], out SectorMapNode next))
            {
                continue;
            }

            if (!nodeAnchors.TryGetValue(next, out Vector2 nextPos))
            {
                continue;
            }

            CreateDashedLine(hoverRoot, hoveredPos, nextPos, hoverPreviewLineColor, hoverPreviewLineWidth);
        }
    }

    private SectorMapNode FindCurrentNode()
    {
        for (int i = 0; i < currentNodes.Count; i++)
        {
            if (currentNodes[i] != null && currentNodes[i].current)
            {
                return currentNodes[i];
            }
        }

        return null;
    }

    private void CreateDashedLine(Transform parent, Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        Vector2 dir = delta / distance;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float step = Mathf.Max(1f, hoverDashLength + hoverDashGap);
        int count = Mathf.CeilToInt(distance / step);

        for (int i = 0; i < count; i++)
        {
            float segmentStart = i * step;
            float segmentLength = Mathf.Min(hoverDashLength, distance - segmentStart);
            if (segmentLength <= 0.01f)
            {
                continue;
            }

            Vector2 mid = start + dir * (segmentStart + segmentLength * 0.5f);
            Image dash = CreateImage("Dash", parent, color);
            dash.sprite = GetLineSprite();
            dash.type = Image.Type.Sliced;
            dash.raycastTarget = false;
            RectTransform rect = dash.rectTransform;
            rect.sizeDelta = new Vector2(segmentLength, width);
            rect.anchoredPosition = mid;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            hoverPreviewObjects.Add(dash.gameObject);
        }
    }

    private void ClearHoverPreview()
    {
        for (int i = hoverPreviewObjects.Count - 1; i >= 0; i--)
        {
            if (hoverPreviewObjects[i] != null)
            {
                Destroy(hoverPreviewObjects[i]);
            }
        }

        hoverPreviewObjects.Clear();
    }

    private Sprite GetCircleSprite()
    {
        if (runtimeCircleSprite != null)
        {
            return runtimeCircleSprite;
        }

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "SectorMapNodeCircle";
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = d <= radius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
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

        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.transform.SetAsLastSibling();
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
