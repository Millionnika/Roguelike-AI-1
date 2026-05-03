using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SectorMapController : MonoBehaviour
{
    [Header("Карта сектора")]
    [Tooltip("Конфигурация карты сектора: размер, пул локаций и мировые координаты.")]
    [SerializeField] private SectorMapConfigSO config;
    [Tooltip("Пресет текущего забега. Используется для стартовой локации и общего пула EncounterSO.")]
    [SerializeField] private RunPresetSO runPreset;

    private readonly List<SectorMapNode> nodes = new List<SectorMapNode>();
    private readonly List<SectorMapNode> reachableBuffer = new List<SectorMapNode>();
    private SectorMapNode currentNode;

    public EncounterSO CurrentEncounter => currentNode != null ? currentNode.encounter : null;
    public SectorMapNode CurrentNode => currentNode;
    public IReadOnlyList<SectorMapNode> Nodes => nodes;

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
        nodes.Clear();
        currentNode = null;

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

        EncounterSO fallbackCombat = FindFirstByType(pool, LocationNodeType.Combat) ?? FindFirstEncounter(pool.encounters);
        EncounterSO startEncounter = runPreset != null && runPreset.startingEncounter != null
            ? runPreset.startingEncounter
            : fallbackCombat;
        EncounterSO finishEncounter = FindFirstByType(pool, LocationNodeType.Boss) ?? fallbackCombat;

        // Гарантированно связный route graph: 8 узлов, все соединены в единый маршрут
        // (0,0) -> (0,1) -> (1,2) -> (2,3) -> (4,4)
        // (0,0) -> (1,1) -> (1,2) -> (2,2) -> (2,3) -> (4,4)
        //                    (1,1) -> (2,2) -> (3,3) -> (4,4)
        SectorMapNode start = AddNode(0, 0, startEncounter);
        SectorMapNode n01 = AddNode(0, 1, PickEncounter(pool, LocationNodeType.Repair, fallbackCombat));
        SectorMapNode n11 = AddNode(1, 1, PickEncounter(pool, LocationNodeType.Combat, fallbackCombat));
        SectorMapNode n12 = AddNode(1, 2, PickEncounter(pool, LocationNodeType.Rest, fallbackCombat));
        SectorMapNode n22 = AddNode(2, 2, PickEncounter(pool, LocationNodeType.Combat, fallbackCombat));
        SectorMapNode n23 = AddNode(2, 3, PickEncounter(pool, LocationNodeType.Event, fallbackCombat));
        SectorMapNode n33 = AddNode(3, 3, PickEncounter(pool, LocationNodeType.Combat, fallbackCombat));
        SectorMapNode finish = AddNode(4, 4, finishEncounter);
        finish.isFinish = true;

        // Слой 1: от старта
        Connect(start, n01);
        Connect(start, n11);

        // Слой 2: сходятся в (1,2)
        Connect(n01, n12);
        Connect(n11, n12);
        Connect(n11, n22);

        // Слой 3: расходятся
        Connect(n12, n23);
        Connect(n22, n23);
        Connect(n22, n33);

        // Финиш
        Connect(n23, finish);
        Connect(n33, finish);

        SetCurrent(start);
        UpdateReachableNodes();
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
        if (node == null || !node.reachable || node.locked || node.visited || node.encounter == null)
        {
            return false;
        }

        if (currentNode != null && !currentNode.IsConnectedTo(node))
        {
            Debug.Log("SectorMapController: выбранный сектор не связан с текущим маршрутом.", this);
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

        // Если это финиш — не обновляем reachable, карта завершена
        if (node.isFinish)
        {
            return true;
        }

        UpdateReachableNodes();
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

        // Если текущий узел — финиш, дальше идти некуда
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

    private SectorMapNode AddNode(int x, int y, EncounterSO encounter)
    {
        SectorMapNode existing = GetNode(new Vector2Int(x, y));
        if (existing != null)
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
            locked = encounter == null
        };
        nodes.Add(node);
        return node;
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
        for (int i = 0; i < nodes.Count; i++)
        {
            SectorMapNode node = nodes[i];
            if (node != null && node.x == coordinates.x && node.y == coordinates.y)
            {
                return node;
            }
        }

        return null;
    }

    private EncounterPoolSO ResolveEncounterPool()
    {
        if (config != null && config.encounterPool != null)
        {
            return config.encounterPool;
        }

        return runPreset != null ? runPreset.encounterPool : null;
    }

    private static EncounterSO PickEncounter(EncounterPoolSO pool, LocationNodeType preferredType, EncounterSO fallback)
    {
        EncounterSO preferred = PickRandomByType(pool, preferredType);
        if (preferred != null)
        {
            return preferred;
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
}
