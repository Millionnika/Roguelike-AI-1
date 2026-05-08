using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SectorMapController : MonoBehaviour
{
    private const float RareSingleChoiceChance = 0.08f;
    private const int DesiredOutgoingChoices = 2;

    [Header("Карта сектора")]
    [Tooltip("Конфигурация карты сектора: размеры, генерация маршрутов и мировая сетка для warp.")]
    [SerializeField] private SectorMapConfigSO config;
    [Tooltip("Пресет текущего забега. Используется для стартовой локации и fallback-пула encounter.")]
    [SerializeField] private RunPresetSO runPreset;

    private readonly List<SectorMapNode> nodes = new List<SectorMapNode>();
    private readonly List<SectorMapNode> reachableBuffer = new List<SectorMapNode>();
    private readonly Dictionary<Vector2Int, SectorMapNode> nodeByCoordinates = new Dictionary<Vector2Int, SectorMapNode>();
    private readonly Dictionary<int, List<SectorMapNode>> nodesByRow = new Dictionary<int, List<SectorMapNode>>();
    private readonly HashSet<int> usedRowX = new HashSet<int>();
    private readonly HashSet<SectorMapNode> reachableFromStart = new HashSet<SectorMapNode>();
    private readonly HashSet<SectorMapNode> canReachFinish = new HashSet<SectorMapNode>();
    private readonly HashSet<SectorMapNode> activeNodes = new HashSet<SectorMapNode>();
    private readonly Dictionary<SectorMapNode, int> incomingCount = new Dictionary<SectorMapNode, int>();
    private readonly Dictionary<SectorMapNode, int> outgoingCount = new Dictionary<SectorMapNode, int>();
    private readonly List<SectorMapNode> removeBuffer = new List<SectorMapNode>();

    private SectorMapNode currentNode;
    private int activeSeed;

    public EncounterSO CurrentEncounter => currentNode != null ? currentNode.encounter : null;
    public SectorMapNode CurrentNode => currentNode;
    public IReadOnlyList<SectorMapNode> Nodes => nodes;
    public int ActiveSeed => activeSeed;

    public void Initialize(SectorMapConfigSO mapConfig, RunPresetSO preset)
    {
        bool configChanged = config != mapConfig || runPreset != preset;
        config = mapConfig;
        runPreset = preset;

        if (nodes.Count == 0 || currentNode == null || configChanged)
        {
            GenerateMap();
        }
    }

    public void GenerateMap()
    {
        ClearMap();
        if (config == null)
        {
            Debug.LogWarning("SectorMapController: не назначен SectorMapConfigSO. Карта сектора не создана.", this);
            return;
        }

        EncounterPoolSO pool = ResolveEncounterPool();
        if (pool == null || pool.encounters == null || pool.encounters.Count == 0)
        {
            Debug.LogWarning("SectorMapController: пул локаций пуст. Карта сектора не создана.", this);
            return;
        }

        activeSeed = ResolveSeed();
        Random.State oldState = Random.state;
        Random.InitState(activeSeed);

        int width = Mathf.Max(5, config.width);
        int height = Mathf.Max(5, config.height);
        int finishX = width - 1;
        int finishY = height - 1;

        EncounterSO fallbackCombat = FindFirstByType(pool, LocationNodeType.Combat) ?? FindFirstEncounter(pool.encounters);
        EncounterSO startEncounter = runPreset != null && runPreset.startingEncounter != null
            ? runPreset.startingEncounter
            : fallbackCombat;
        EncounterSO finishEncounter = ResolveFinishEncounter(pool, fallbackCombat);

        SectorMapNode start = AddOrGetNode(0, 0, startEncounter);
        start.isHome = true;
        SectorMapNode finish = AddOrGetNode(finishX, finishY, finishEncounter);
        finish.isFinish = true;

        BuildGuaranteedRoutes(start, finish, width, height, pool, fallbackCombat);
        EnsureIntermediateRowsHaveNodes(width, height, pool, fallbackCombat);
        BuildAdditionalConnections(height);

        PruneToActiveRouteGraph(start, finish);
        EnsureRouteVariety(finish);
        EnsureDegreesAndConnectivity(start, finish);

        SetCurrent(start);
        UpdateReachableNodes();

        Random.state = oldState;
    }

    public IReadOnlyList<SectorMapNode> GetReachableNextNodes()
    {
        reachableBuffer.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode node = nodes[i];
            if (node != null && node.reachable)
            {
                reachableBuffer.Add(node);
            }
        }

        return reachableBuffer;
    }

    public bool SelectNode(SectorMapNode node)
    {
        if (node == null || !node.reachable || node.locked || node.visited || node.completed || node.encounter == null)
        {
            return false;
        }

        if (currentNode != null && !currentNode.IsConnectedTo(node))
        {
            return false;
        }

        if (currentNode != null)
        {
            currentNode.visited = true;
            currentNode.completed = true;
            currentNode.current = false;
            currentNode.reachable = false;
        }

        SetCurrent(node);
        if (!node.isFinish)
        {
            UpdateReachableNodes();
        }

        return true;
    }

    public void MarkCurrentCompleted()
    {
        if (currentNode != null)
        {
            currentNode.visited = true;
            currentNode.completed = true;
        }
    }

    public bool HasAvailableNextNodes()
    {
        if (currentNode != null && currentNode.isFinish)
        {
            return false;
        }

        IReadOnlyList<SectorMapNode> reachable = GetReachableNextNodes();
        return reachable != null && reachable.Count > 0;
    }

    public void ResetMap()
    {
        GenerateMap();
    }

    private int ResolveSeed()
    {
        if (config == null)
        {
            return 0;
        }

        if (!config.useRandomSeedPerRun)
        {
            return config.fixedSeed;
        }

        int seed = System.Environment.TickCount ^ Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);
        return seed == 0 ? 1 : seed;
    }

    private void BuildGuaranteedRoutes(SectorMapNode start, SectorMapNode finish, int width, int height, EncounterPoolSO pool, EncounterSO fallbackCombat)
    {
        int routeCount = Mathf.Clamp(config.guaranteedRouteCount, 1, 8);
        for (int route = 0; route < routeCount; route++)
        {
            SectorMapNode from = start;
            int currentX = 0;
            float tRoute = routeCount <= 1 ? 0.5f : route / (float)(routeCount - 1);
            int laneTarget = Mathf.RoundToInt(Mathf.Lerp(0f, width - 1, tRoute));

            for (int y = 1; y < height - 1; y++)
            {
                float tY = y / (float)(height - 1);
                int idealX = Mathf.RoundToInt(Mathf.Lerp(laneTarget, width - 1, tY));
                int minX = Mathf.Max(0, currentX - 1);
                int maxX = Mathf.Min(width - 1, currentX + 2);
                int targetX = Mathf.Clamp(idealX + Random.Range(-1, 2), minX, maxX);
                if (y >= height - 2)
                {
                    targetX = Mathf.Clamp(targetX, currentX, width - 1);
                }

                SectorMapNode step = AddOrGetNode(targetX, y, PickEncounterForRow(pool, fallbackCombat, y, height));
                Connect(from, step);
                from = step;
                currentX = targetX;
            }

            Connect(from, finish);
        }
    }

    private void EnsureIntermediateRowsHaveNodes(int width, int height, EncounterPoolSO pool, EncounterSO fallbackCombat)
    {
        for (int y = 1; y < height - 1; y++)
        {
            List<SectorMapNode> row = GetOrCreateRow(y);
            int desiredCount = Random.Range(config.minNodesPerRow, config.maxNodesPerRow + 1);
            desiredCount = Mathf.Clamp(desiredCount, 1, width);

            int toAdd = Mathf.Max(0, desiredCount - row.Count);
            for (int i = 0; i < toAdd; i++)
            {
                int x = FindFreeXInRow(y, width);
                if (x < 0)
                {
                    break;
                }

                AddOrGetNode(x, y, PickEncounterForRow(pool, fallbackCombat, y, height));
            }
        }
    }

    private void BuildAdditionalConnections(int height)
    {
        for (int y = 0; y < height - 1; y++)
        {
            List<SectorMapNode> row = GetOrCreateRow(y);
            List<SectorMapNode> nextRow = GetOrCreateRow(y + 1);
            if (row.Count == 0 || nextRow.Count == 0)
            {
                continue;
            }

            for (int i = 0; i < row.Count; i++)
            {
                SectorMapNode from = row[i];
                if (from == null || from.isFinish)
                {
                    continue;
                }

                int maxLinks = Mathf.Clamp(config.maxConnectionsPerNode, 1, 4);
                EnsureAtLeastOneForwardLink(from, nextRow);

                while (GetOutgoingCountCached(from) < maxLinks && Random.value <= config.extraRouteChance)
                {
                    SectorMapNode alt = FindAlternativeForwardTarget(from, nextRow);
                    if (alt == null)
                    {
                        break;
                    }

                    Connect(from, alt);
                }
            }
        }
    }

    private void EnsureAtLeastOneForwardLink(SectorMapNode from, List<SectorMapNode> nextRow)
    {
        if (from == null || nextRow == null || nextRow.Count == 0)
        {
            return;
        }

        if (from.nextCoordinates != null && from.nextCoordinates.Count > 0)
        {
            return;
        }

        SectorMapNode best = FindBestForwardTarget(from, nextRow);
        if (best != null)
        {
            Connect(from, best);
        }
    }

    private void PruneToActiveRouteGraph(SectorMapNode start, SectorMapNode finish)
    {
        BuildReachableSet(start, reachableFromStart);
        BuildReverseReachableSet(finish, canReachFinish);
        activeNodes.Clear();

        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode node = nodes[i];
            if (node == null)
            {
                continue;
            }

            bool isActive = reachableFromStart.Contains(node) && canReachFinish.Contains(node);
            if (isActive)
            {
                activeNodes.Add(node);
                node.locked = false;
            }
            else
            {
                node.locked = true;
            }
        }

        // Remove references to inactive nodes from active nodes.
        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode node = nodes[i];
            if (node == null || !activeNodes.Contains(node) || node.nextCoordinates == null)
            {
                continue;
            }

            for (int c = node.nextCoordinates.Count - 1; c >= 0; c--)
            {
                SectorMapNode target = GetNode(node.nextCoordinates[c]);
                if (target == null || !activeNodes.Contains(target))
                {
                    node.nextCoordinates.RemoveAt(c);
                }
            }
        }

        RemoveNodesNotInActiveSet();
    }

    private void EnsureDegreesAndConnectivity(SectorMapNode start, SectorMapNode finish)
    {
        bool changed;
        do
        {
            changed = false;
            RecalculateDegrees();
            removeBuffer.Clear();

            for (int i = 0; i < nodes.Count; i++)
            {
                SectorMapNode node = nodes[i];
                if (node == null || node == start || node == finish)
                {
                    continue;
                }

                int incoming = GetIncoming(node);
                int outgoing = GetOutgoing(node);
                if (incoming <= 0 || outgoing <= 0)
                {
                    removeBuffer.Add(node);
                }
            }

            if (removeBuffer.Count > 0)
            {
                changed = true;
                for (int i = 0; i < removeBuffer.Count; i++)
                {
                    RemoveNode(removeBuffer[i]);
                }
            }
        } while (changed);

        // Final safety: keep only nodes reachable from start and leading to finish.
        BuildReachableSet(start, reachableFromStart);
        BuildReverseReachableSet(finish, canReachFinish);
        activeNodes.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode node = nodes[i];
            if (node != null && reachableFromStart.Contains(node) && canReachFinish.Contains(node))
            {
                activeNodes.Add(node);
            }
        }

        RemoveNodesNotInActiveSet();
    }

    private void EnsureRouteVariety(SectorMapNode finish)
    {
        if (finish == null)
        {
            return;
        }

        int finishY = finish.y;
        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode from = nodes[i];
            if (from == null || from.isFinish || from.locked || from.encounter == null)
            {
                continue;
            }

            // Перед финишем допускаем сужение маршрута, раньше - стараемся держать минимум 2 варианта.
            bool isNearFinish = from.y >= finishY - 1;
            int desired = isNearFinish ? 1 : (Random.value < RareSingleChoiceChance ? 1 : DesiredOutgoingChoices);
            EnsureOutgoingChoices(from, desired);
        }
    }

    private void EnsureOutgoingChoices(SectorMapNode from, int desiredCount)
    {
        if (from == null || desiredCount <= 0)
        {
            return;
        }

        from.nextCoordinates ??= new List<Vector2Int>();
        int guard = 0;
        while (from.nextCoordinates.Count < desiredCount && guard < 32)
        {
            guard++;
            SectorMapNode candidate = FindAdditionalForwardCandidate(from);
            if (candidate == null)
            {
                break;
            }

            Connect(from, candidate);
        }
    }

    private SectorMapNode FindAdditionalForwardCandidate(SectorMapNode from)
    {
        if (from == null)
        {
            return null;
        }

        // Предпочитаем следующий ряд для читаемости маршрутов.
        List<SectorMapNode> nextRow = GetOrCreateRow(from.y + 1);
        SectorMapNode candidate = PickBestUnusedTarget(from, nextRow, true);
        if (candidate != null)
        {
            return candidate;
        }

        // Фолбэк: если в следующем ряду вариантов нет, берем любой более дальний активный узел впереди.
        SectorMapNode fallback = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode node = nodes[i];
            if (node == null || node == from || node.locked || node.encounter == null)
            {
                continue;
            }

            if (node.y <= from.y || from.IsConnectedTo(node))
            {
                continue;
            }

            float score = Mathf.Abs(node.x - from.x) + (node.y - from.y) * 0.35f;
            if (score < bestScore)
            {
                bestScore = score;
                fallback = node;
            }
        }

        return fallback;
    }

    private static SectorMapNode PickBestUnusedTarget(SectorMapNode from, List<SectorMapNode> candidates, bool preferHorizontalCloseness)
    {
        if (from == null || candidates == null || candidates.Count == 0)
        {
            return null;
        }

        SectorMapNode best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            SectorMapNode candidate = candidates[i];
            if (candidate == null || candidate.locked || candidate.encounter == null || from.IsConnectedTo(candidate))
            {
                continue;
            }

            float score = preferHorizontalCloseness ? Mathf.Abs(candidate.x - from.x) : Random.value;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private void RemoveNodesNotInActiveSet()
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            SectorMapNode node = nodes[i];
            if (node == null || activeNodes.Contains(node))
            {
                continue;
            }

            RemoveNode(node);
        }

        // Cleanup any dangling references.
        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode node = nodes[i];
            if (node == null || node.nextCoordinates == null)
            {
                continue;
            }

            for (int c = node.nextCoordinates.Count - 1; c >= 0; c--)
            {
                if (GetNode(node.nextCoordinates[c]) == null)
                {
                    node.nextCoordinates.RemoveAt(c);
                }
            }
        }
    }

    private void RemoveNode(SectorMapNode node)
    {
        if (node == null)
        {
            return;
        }

        Vector2Int key = node.Coordinates;
        nodeByCoordinates.Remove(key);
        nodes.Remove(node);
        if (nodesByRow.TryGetValue(node.y, out List<SectorMapNode> row))
        {
            row.Remove(node);
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode other = nodes[i];
            if (other == null || other.nextCoordinates == null)
            {
                continue;
            }

            for (int c = other.nextCoordinates.Count - 1; c >= 0; c--)
            {
                if (other.nextCoordinates[c] == key)
                {
                    other.nextCoordinates.RemoveAt(c);
                }
            }
        }
    }

    private SectorMapNode AddOrGetNode(int x, int y, EncounterSO encounter)
    {
        Vector2Int key = new Vector2Int(x, y);
        if (nodeByCoordinates.TryGetValue(key, out SectorMapNode existing))
        {
            if (existing.encounter == null)
            {
                existing.encounter = encounter;
            }

            return existing;
        }

        SectorMapNode node = new SectorMapNode
        {
            x = x,
            y = y,
            encounter = encounter,
            worldPosition = CalculateWorldPosition(x, y),
            mapPosition = CalculateMapPosition(x, y),
            locked = encounter == null
        };

        nodes.Add(node);
        nodeByCoordinates[key] = node;
        GetOrCreateRow(y).Add(node);
        return node;
    }

    private Vector2 CalculateMapPosition(int x, int y)
    {
        float jitterX = config != null ? config.positionJitterX : 0f;
        float jitterY = config != null ? config.positionJitterY : 0f;
        float offsetX = (Random.value * 2f - 1f) * jitterX;
        float offsetY = (Random.value * 2f - 1f) * jitterY;
        return new Vector2(x + offsetX, y + offsetY);
    }

    private static void Connect(SectorMapNode from, SectorMapNode to)
    {
        if (from == null || to == null || to.y <= from.y)
        {
            return;
        }

        from.nextCoordinates ??= new List<Vector2Int>();
        Vector2Int target = to.Coordinates;
        if (!from.nextCoordinates.Contains(target))
        {
            from.nextCoordinates.Add(target);
        }
    }

    private SectorMapNode GetNode(Vector2Int coordinates)
    {
        return nodeByCoordinates.TryGetValue(coordinates, out SectorMapNode node) ? node : null;
    }

    private EncounterPoolSO ResolveEncounterPool()
    {
        if (config != null && config.encounterPool != null)
        {
            return config.encounterPool;
        }

        return runPreset != null ? runPreset.encounterPool : null;
    }

    private EncounterSO ResolveFinishEncounter(EncounterPoolSO pool, EncounterSO fallbackCombat)
    {
        if (config != null && config.forceBossOrCombatEnd)
        {
            EncounterSO boss = FindFirstByType(pool, LocationNodeType.Boss);
            if (boss != null)
            {
                return boss;
            }
        }

        return fallbackCombat;
    }

    private EncounterSO PickEncounterForRow(EncounterPoolSO pool, EncounterSO fallbackCombat, int y, int height)
    {
        if (pool == null || pool.encounters == null || pool.encounters.Count == 0)
        {
            return fallbackCombat;
        }

        float progress = height <= 1 ? 1f : y / (float)(height - 1);
        if (progress < 0.2f)
        {
            return PickByPriority(pool, fallbackCombat, LocationNodeType.Combat, LocationNodeType.Repair, LocationNodeType.Rest);
        }

        if (progress < 0.7f)
        {
            return PickByPriority(pool, fallbackCombat, LocationNodeType.Combat, LocationNodeType.Event, LocationNodeType.Rest, LocationNodeType.Repair);
        }

        return PickByPriority(pool, fallbackCombat, LocationNodeType.Combat, LocationNodeType.Elite, LocationNodeType.Rest);
    }

    private static EncounterSO PickByPriority(EncounterPoolSO pool, EncounterSO fallback, params LocationNodeType[] order)
    {
        for (int i = 0; i < order.Length; i++)
        {
            EncounterSO pick = PickRandomByType(pool, order[i]);
            if (pick != null)
            {
                return pick;
            }
        }

        return fallback;
    }

    private static EncounterSO PickRandomByType(EncounterPoolSO pool, LocationNodeType nodeType)
    {
        if (pool == null || pool.encounters == null)
        {
            return null;
        }

        int count = 0;
        for (int i = 0; i < pool.encounters.Count; i++)
        {
            EncounterSO encounter = pool.encounters[i];
            if (encounter != null && encounter.nodeType == nodeType)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        int target = Random.Range(0, count);
        for (int i = 0; i < pool.encounters.Count; i++)
        {
            EncounterSO encounter = pool.encounters[i];
            if (encounter == null || encounter.nodeType != nodeType)
            {
                continue;
            }

            if (target == 0)
            {
                return encounter;
            }

            target--;
        }

        return null;
    }

    private static EncounterSO FindFirstByType(EncounterPoolSO pool, LocationNodeType nodeType)
    {
        if (pool == null || pool.encounters == null)
        {
            return null;
        }

        for (int i = 0; i < pool.encounters.Count; i++)
        {
            EncounterSO encounter = pool.encounters[i];
            if (encounter != null && encounter.nodeType == nodeType)
            {
                return encounter;
            }
        }

        return null;
    }

    private static EncounterSO FindFirstEncounter(IReadOnlyList<EncounterSO> source)
    {
        if (source == null)
        {
            return null;
        }

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
            {
                return source[i];
            }
        }

        return null;
    }

    private Vector3 CalculateWorldPosition(int x, int y)
    {
        if (config == null)
        {
            return new Vector3(x * 80f, y * 80f, 0f);
        }

        Vector2 origin = config.worldOrigin;
        Vector2 size = config.sectorWorldSize;
        return new Vector3(origin.x + x * size.x, origin.y + y * size.y, 0f);
    }

    private void SetCurrent(SectorMapNode node)
    {
        currentNode = node;
        if (currentNode != null)
        {
            currentNode.current = true;
            currentNode.reachable = false;
            currentNode.locked = false;
        }
    }

    private void UpdateReachableNodes()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode node = nodes[i];
            if (node != null && !node.current)
            {
                node.reachable = false;
            }
        }

        if (currentNode == null || currentNode.isFinish || currentNode.nextCoordinates == null)
        {
            return;
        }

        for (int i = 0; i < currentNode.nextCoordinates.Count; i++)
        {
            SectorMapNode candidate = GetNode(currentNode.nextCoordinates[i]);
            if (candidate == null || candidate.encounter == null || candidate.visited || candidate.completed || candidate.locked)
            {
                continue;
            }

            candidate.reachable = true;
        }
    }

    private void ClearMap()
    {
        nodes.Clear();
        reachableBuffer.Clear();
        nodeByCoordinates.Clear();
        nodesByRow.Clear();
        usedRowX.Clear();
        reachableFromStart.Clear();
        canReachFinish.Clear();
        activeNodes.Clear();
        incomingCount.Clear();
        outgoingCount.Clear();
        removeBuffer.Clear();
        currentNode = null;
        activeSeed = 0;
    }

    private List<SectorMapNode> GetOrCreateRow(int y)
    {
        if (!nodesByRow.TryGetValue(y, out List<SectorMapNode> row))
        {
            row = new List<SectorMapNode>();
            nodesByRow[y] = row;
        }

        return row;
    }

    private int FindFreeXInRow(int y, int width)
    {
        usedRowX.Clear();
        List<SectorMapNode> row = GetOrCreateRow(y);
        for (int i = 0; i < row.Count; i++)
        {
            usedRowX.Add(row[i].x);
        }

        int attempts = width * 2;
        for (int i = 0; i < attempts; i++)
        {
            int x = Random.Range(0, width);
            if (!usedRowX.Contains(x))
            {
                return x;
            }
        }

        for (int x = 0; x < width; x++)
        {
            if (!usedRowX.Contains(x))
            {
                return x;
            }
        }

        return -1;
    }

    private void RecalculateDegrees()
    {
        incomingCount.Clear();
        outgoingCount.Clear();

        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode node = nodes[i];
            if (node == null)
            {
                continue;
            }

            incomingCount[node] = 0;
            outgoingCount[node] = 0;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode from = nodes[i];
            if (from == null || from.nextCoordinates == null)
            {
                continue;
            }

            for (int c = 0; c < from.nextCoordinates.Count; c++)
            {
                SectorMapNode to = GetNode(from.nextCoordinates[c]);
                if (to == null)
                {
                    continue;
                }

                outgoingCount[from] = outgoingCount[from] + 1;
                incomingCount[to] = incomingCount[to] + 1;
            }
        }
    }

    private int GetIncoming(SectorMapNode node)
    {
        return node != null && incomingCount.TryGetValue(node, out int value) ? value : 0;
    }

    private int GetOutgoing(SectorMapNode node)
    {
        return node != null && outgoingCount.TryGetValue(node, out int value) ? value : 0;
    }

    private int GetOutgoingCountCached(SectorMapNode node)
    {
        if (node == null || node.nextCoordinates == null)
        {
            return 0;
        }

        return node.nextCoordinates.Count;
    }

    private static SectorMapNode FindBestForwardTarget(SectorMapNode from, List<SectorMapNode> nextRow)
    {
        SectorMapNode best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < nextRow.Count; i++)
        {
            SectorMapNode candidate = nextRow[i];
            if (candidate == null || from.IsConnectedTo(candidate))
            {
                continue;
            }

            float score = Mathf.Abs(candidate.x - from.x);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static SectorMapNode FindAlternativeForwardTarget(SectorMapNode from, List<SectorMapNode> nextRow)
    {
        SectorMapNode best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < nextRow.Count; i++)
        {
            SectorMapNode candidate = nextRow[i];
            if (candidate == null || from.IsConnectedTo(candidate))
            {
                continue;
            }

            float score = Mathf.Abs(candidate.x - from.x) + Random.value;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private void BuildReachableSet(SectorMapNode start, HashSet<SectorMapNode> destination)
    {
        destination.Clear();
        if (start == null)
        {
            return;
        }

        Queue<SectorMapNode> queue = new Queue<SectorMapNode>();
        queue.Enqueue(start);
        destination.Add(start);

        while (queue.Count > 0)
        {
            SectorMapNode node = queue.Dequeue();
            if (node.nextCoordinates == null)
            {
                continue;
            }

            for (int i = 0; i < node.nextCoordinates.Count; i++)
            {
                SectorMapNode next = GetNode(node.nextCoordinates[i]);
                if (next == null || destination.Contains(next))
                {
                    continue;
                }

                destination.Add(next);
                queue.Enqueue(next);
            }
        }
    }

    private void BuildReverseReachableSet(SectorMapNode finish, HashSet<SectorMapNode> destination)
    {
        destination.Clear();
        if (finish == null)
        {
            return;
        }

        Queue<SectorMapNode> queue = new Queue<SectorMapNode>();
        queue.Enqueue(finish);
        destination.Add(finish);

        while (queue.Count > 0)
        {
            SectorMapNode node = queue.Dequeue();
            for (int i = 0; i < nodes.Count; i++)
            {
                SectorMapNode candidate = nodes[i];
                if (candidate == null || destination.Contains(candidate) || candidate.nextCoordinates == null)
                {
                    continue;
                }

                for (int c = 0; c < candidate.nextCoordinates.Count; c++)
                {
                    if (candidate.nextCoordinates[c] != node.Coordinates)
                    {
                        continue;
                    }

                    destination.Add(candidate);
                    queue.Enqueue(candidate);
                    break;
                }
            }
        }
    }
}
