using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SectorObjectiveController : MonoBehaviour
{
    [Header("Настройка контроллера целей")]
    [Tooltip("Корневой объект, под которым создаются runtime-объекты целей сектора.")]
    [SerializeField] private Transform objectiveRoot;
    [Tooltip("Показывать предупреждение, если цель не может быть запущена из-за отсутствия корабля игрока.")]
    [SerializeField] private bool warnWhenPlayerMissing = true;

    [Header("Fallback-визуал цели")]
    [Tooltip("Цвет fallback-объекта для цели сбора контейнеров.")]
    [SerializeField] private Color collectColor = new Color(1f, 0.75f, 0.2f, 0.95f);
    [Tooltip("Цвет fallback-зоны удержания.")]
    [SerializeField] private Color holdZoneColor = new Color(0.35f, 0.9f, 1f, 0.35f);
    [Tooltip("Цвет fallback-аномалии для сканирования.")]
    [SerializeField] private Color anomalyColor = new Color(0.7f, 0.55f, 1f, 0.45f);
    [Tooltip("Порядок отрисовки fallback-визуалов цели.")]
    [SerializeField] private int fallbackSortingOrder = 7;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private readonly List<ObjectiveCollectible> activeCollectibles = new List<ObjectiveCollectible>();

    private Transform worldRoot;
    private Transform playerTransform;
    private Sprite fallbackSprite;
    private Action<int> addScrap;
    private Action<string, string> logMessage;
    private Sprite runtimeFallbackSprite;

    private SectorObjectiveSO activeObjective;
    private ObjectiveMarker activeMarker;
    private int collectedCount;
    private float objectiveTimer;
    private bool completionRewardGranted;

    public bool HasActiveObjective => activeObjective != null && activeObjective.objectiveType != SectorObjectiveType.None;
    public bool IsObjectiveCompleted { get; private set; }

    public float Progress01
    {
        get
        {
            if (!HasActiveObjective)
            {
                return 0f;
            }

            switch (activeObjective.objectiveType)
            {
                case SectorObjectiveType.CollectContainers:
                    return Mathf.Clamp01(collectedCount / (float)Mathf.Max(1, activeObjective.requiredAmount));
                case SectorObjectiveType.HoldZone:
                case SectorObjectiveType.ScanAnomaly:
                    return Mathf.Clamp01(objectiveTimer / Mathf.Max(0.1f, activeObjective.duration));
                default:
                    return IsObjectiveCompleted ? 1f : 0f;
            }
        }
    }

    public string ProgressText
    {
        get
        {
            if (!HasActiveObjective)
            {
                return string.Empty;
            }

            switch (activeObjective.objectiveType)
            {
                case SectorObjectiveType.CollectContainers:
                    return $"Цель: Соберите контейнеры {Mathf.Clamp(collectedCount, 0, Mathf.Max(1, activeObjective.requiredAmount))} / {Mathf.Max(1, activeObjective.requiredAmount)}";
                case SectorObjectiveType.HoldZone:
                    return $"Цель: Удерживайте зону {objectiveTimer:0.0} / {Mathf.Max(0.1f, activeObjective.duration):0.0} сек";
                case SectorObjectiveType.ScanAnomaly:
                    return $"Цель: Сканирование аномалии {Mathf.RoundToInt(Progress01 * 100f)}%";
                default:
                    return "Цель: выполните задачу сектора";
            }
        }
    }

    public void Initialize(Transform runtimeWorldRoot, Sprite fallbackObjectiveSprite, Action<int> addScrapCallback, Action<string, string> logCallback)
    {
        worldRoot = runtimeWorldRoot;
        fallbackSprite = fallbackObjectiveSprite;
        addScrap = addScrapCallback;
        logMessage = logCallback;

        if (objectiveRoot == null && worldRoot != null)
        {
            Transform existingRoot = worldRoot.Find("SectorObjectiveRoot");
            if (existingRoot != null)
            {
                objectiveRoot = existingRoot;
            }
            else
            {
                GameObject rootObject = new GameObject("SectorObjectiveRoot");
                objectiveRoot = rootObject.transform;
                objectiveRoot.SetParent(worldRoot, false);
            }
        }
    }

    public void BeginObjective(SectorObjectiveSO objective, Transform player)
    {
        CleanupObjective();
        playerTransform = player;
        activeObjective = objective;
        collectedCount = 0;
        objectiveTimer = 0f;
        completionRewardGranted = false;

        if (objective == null || objective.objectiveType == SectorObjectiveType.None)
        {
            return;
        }

        if (playerTransform == null)
        {
            if (warnWhenPlayerMissing)
            {
                Debug.LogWarning("SectorObjectiveController: цель сектора не запущена, потому что не найден корабль игрока.", this);
            }
            activeObjective = null;
            return;
        }

        switch (objective.objectiveType)
        {
            case SectorObjectiveType.CollectContainers:
                SpawnCollectibles();
                break;
            case SectorObjectiveType.HoldZone:
                SpawnObjectiveMarker("HoldZone", holdZoneColor);
                break;
            case SectorObjectiveType.ScanAnomaly:
                SpawnObjectiveMarker("ScanAnomaly", anomalyColor);
                break;
        }
    }

    public void Tick(float deltaTime)
    {
        if (!HasActiveObjective || IsObjectiveCompleted)
        {
            return;
        }

        switch (activeObjective.objectiveType)
        {
            case SectorObjectiveType.HoldZone:
            case SectorObjectiveType.ScanAnomaly:
                UpdateTimedMarkerObjective(deltaTime);
                break;
        }
    }

    public void CleanupObjective()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
            {
                Destroy(spawnedObjects[i]);
            }
        }

        spawnedObjects.Clear();
        activeCollectibles.Clear();
        activeMarker = null;
        activeObjective = null;
        playerTransform = null;
        collectedCount = 0;
        objectiveTimer = 0f;
        completionRewardGranted = false;
        IsObjectiveCompleted = false;
    }

    private void SpawnCollectibles()
    {
        int count = Mathf.Max(1, activeObjective.requiredAmount);
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = GetRandomObjectivePosition();
            GameObject collectibleObject = SpawnObjectiveObject("Container_" + (i + 1), spawnPosition, collectColor);
            if (collectibleObject == null)
            {
                continue;
            }

            ObjectiveCollectible collectible = collectibleObject.GetComponent<ObjectiveCollectible>();
            if (collectible == null)
            {
                collectible = collectibleObject.AddComponent<ObjectiveCollectible>();
            }

            collectible.Initialize(OnCollectibleCollected);
            activeCollectibles.Add(collectible);
        }
    }

    private void SpawnObjectiveMarker(string markerName, Color fallbackColor)
    {
        Vector3 position = GetRandomObjectivePosition();
        GameObject markerObject = SpawnObjectiveObject(markerName, position, fallbackColor);
        if (markerObject == null)
        {
            return;
        }

        activeMarker = markerObject.GetComponent<ObjectiveMarker>();
        if (activeMarker == null)
        {
            activeMarker = markerObject.AddComponent<ObjectiveMarker>();
        }

        activeMarker.SetRadius(Mathf.Max(0.1f, activeObjective.radius));
    }

    private void UpdateTimedMarkerObjective(float deltaTime)
    {
        if (activeMarker == null || playerTransform == null)
        {
            return;
        }

        float distance = Vector2.Distance(playerTransform.position, activeMarker.transform.position);
        if (distance <= activeMarker.Radius)
        {
            objectiveTimer = Mathf.Min(Mathf.Max(0.1f, activeObjective.duration), objectiveTimer + Mathf.Max(0f, deltaTime));
        }

        if (objectiveTimer >= Mathf.Max(0.1f, activeObjective.duration))
        {
            CompleteObjective();
        }
    }

    private void OnCollectibleCollected(ObjectiveCollectible collectible)
    {
        if (!HasActiveObjective || IsObjectiveCompleted || activeObjective.objectiveType != SectorObjectiveType.CollectContainers)
        {
            return;
        }

        if (collectible != null)
        {
            activeCollectibles.Remove(collectible);
            spawnedObjects.Remove(collectible.gameObject);
        }

        collectedCount = Mathf.Min(Mathf.Max(1, activeObjective.requiredAmount), collectedCount + 1);
        if (collectedCount >= Mathf.Max(1, activeObjective.requiredAmount))
        {
            CompleteObjective();
        }
    }

    private void CompleteObjective()
    {
        if (!HasActiveObjective || IsObjectiveCompleted)
        {
            return;
        }

        IsObjectiveCompleted = true;
        string objectiveName = string.IsNullOrWhiteSpace(activeObjective.displayName) ? activeObjective.name : activeObjective.displayName;
        Log("Цель сектора выполнена: " + objectiveName + ".");
        if (!completionRewardGranted && activeObjective.scrapReward > 0)
        {
            completionRewardGranted = true;
            addScrap?.Invoke(Mathf.Max(0, activeObjective.scrapReward));
            Log($"Цель выполнена: получено {Mathf.Max(0, activeObjective.scrapReward)} лома.");
        }
    }

    private GameObject SpawnObjectiveObject(string objectName, Vector3 position, Color fallbackColor)
    {
        if (objectiveRoot == null && worldRoot != null)
        {
            Initialize(worldRoot, fallbackSprite, addScrap, logMessage);
        }

        Transform parent = objectiveRoot != null ? objectiveRoot : worldRoot;
        if (parent == null)
        {
            return null;
        }

        GameObject objectiveObject = null;
        if (activeObjective != null && activeObjective.objectivePrefab != null)
        {
            objectiveObject = Instantiate(activeObjective.objectivePrefab, position, Quaternion.identity, parent);
            objectiveObject.name = objectName;
        }
        else
        {
            objectiveObject = new GameObject(objectName);
            objectiveObject.transform.SetParent(parent, false);
            objectiveObject.transform.position = position;

            SpriteRenderer renderer = objectiveObject.AddComponent<SpriteRenderer>();
            renderer.sprite = fallbackSprite != null ? fallbackSprite : GetRuntimeFallbackSprite();
            renderer.color = fallbackColor;
            renderer.sortingOrder = fallbackSortingOrder;
        }

        spawnedObjects.Add(objectiveObject);
        return objectiveObject;
    }

    private Vector3 GetRandomObjectivePosition()
    {
        Vector3 center = playerTransform != null ? playerTransform.position : Vector3.zero;
        float minRadius = activeObjective != null ? Mathf.Max(0f, activeObjective.spawnRadiusMin) : 0f;
        float maxRadius = activeObjective != null ? Mathf.Max(minRadius, activeObjective.spawnRadiusMax) : minRadius;

        Vector2 direction = UnityEngine.Random.insideUnitCircle;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.right;
        }

        float distance = maxRadius <= minRadius
            ? minRadius
            : UnityEngine.Random.Range(minRadius, maxRadius);

        Vector2 offset = direction.normalized * distance;
        return new Vector3(center.x + offset.x, center.y + offset.y, 0f);
    }

    private void Log(string message)
    {
        logMessage?.Invoke(message, "warning");
    }

    private Sprite GetRuntimeFallbackSprite()
    {
        if (runtimeFallbackSprite != null)
        {
            return runtimeFallbackSprite;
        }

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        float feather = 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - distance) / Mathf.Max(0.0001f, feather));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        runtimeFallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        runtimeFallbackSprite.name = "ObjectiveFallbackSprite";
        return runtimeFallbackSprite;
    }
}
