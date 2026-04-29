using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBaseLair : MonoBehaviour
{
    private enum SpawnMode
    {
        Burst = 0,
        Timed = 1
    }

    [Header("Прочность базы")]
    [Tooltip("Максимальное значение щита базы.")]
    [Min(0f)] [SerializeField] private float maxShield = 1200f;
    [Tooltip("Максимальное значение брони базы.")]
    [Min(0f)] [SerializeField] private float maxArmor = 900f;
    [Tooltip("Максимальное значение корпуса базы.")]
    [Min(1f)] [SerializeField] private float maxHull = 1800f;

    [Header("Награда")]
    [Tooltip("Сколько опыта получит игрок после уничтожения базы.")]
    [Min(0)] [SerializeField] private int experienceReward = 260;

    [Header("Запуск спавна")]
    [Tooltip("Процент полученного урона от общей прочности, после которого начинается спавн врагов.")]
    [Range(0f, 100f)] [SerializeField] private float spawnTriggerDamagePercent = 10f;
    [Tooltip("Режим спавна врагов: сразу пачкой или по одному с интервалом.")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.Timed;
    [Tooltip("Количество врагов, которое будет заспавнено после срабатывания триггера.")]
    [Min(0)] [SerializeField] private int spawnEnemyCount = 4;
    [Tooltip("Интервал между спавном врагов в режиме Timed.")]
    [Min(0.01f)] [SerializeField] private float spawnIntervalSeconds = 1f;

    [Header("Кого спавнить")]
    [Tooltip("ShipData врага. Приоритетнее prefab, потому что создаёт полноценного врага через контроллер сцены.")]
    [SerializeField] private ShipDataSO enemyShipData;
    [Tooltip("Префаб врага. Используется, если ShipData не задан.")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Где спавнить")]
    [Tooltip("Точки спавна врагов. Если список пуст, враги появляются случайно вокруг базы.")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Радиус случайного спавна вокруг базы, если точки не заданы.")]
    [Min(0.2f)] [SerializeField] private float fallbackSpawnRadius = 5.5f;

    [Header("Ссылки")]
    [Tooltip("Контроллер боевой сцены. Если не задан, найдётся автоматически.")]
    [SerializeField] private SpaceCombatSceneController sceneController;

    private ShipDamageReceiver damageReceiver;
    private ShipDurabilityState durabilityState;
    private float initialTotalDurability;
    private bool spawnTriggered;
    private bool destroyedHandled;

    public bool IsAlive => !destroyedHandled && durabilityState.Hull > 0.01f;
    public ShipDurabilityState CurrentDurability => durabilityState;

    private void Awake()
    {
        InitializeDurability();
        CombatLayerUtility.ApplyShipLayer(gameObject, CombatFaction.Enemy);
        EnsureDamageReceiver();
        ResolveSceneController();
    }

    private void OnDestroy()
    {
        if (damageReceiver != null)
        {
            damageReceiver.DamageApplied -= OnDamageApplied;
        }
    }

    private void InitializeDurability()
    {
        durabilityState = new ShipDurabilityState
        {
            MaxShield = Mathf.Max(0f, maxShield),
            Shield = Mathf.Max(0f, maxShield),
            MaxArmor = Mathf.Max(0f, maxArmor),
            Armor = Mathf.Max(0f, maxArmor),
            MaxHull = Mathf.Max(1f, maxHull),
            Hull = Mathf.Max(1f, maxHull)
        };

        initialTotalDurability = Mathf.Max(1f, durabilityState.MaxShield + durabilityState.MaxArmor + durabilityState.MaxHull);
        spawnTriggered = false;
        destroyedHandled = false;
    }

    private void EnsureDamageReceiver()
    {
        damageReceiver = GetComponent<ShipDamageReceiver>();
        if (damageReceiver == null)
        {
            damageReceiver = gameObject.AddComponent<ShipDamageReceiver>();
        }

        damageReceiver.Initialize(
            CombatFaction.Enemy,
            ReadDurability,
            WriteDurability,
            OnDestroyedByDamage);
        damageReceiver.DamageApplied -= OnDamageApplied;
        damageReceiver.DamageApplied += OnDamageApplied;
    }

    private void ResolveSceneController()
    {
        if (sceneController == null)
        {
            sceneController = FindFirstObjectByType<SpaceCombatSceneController>();
        }
    }

    private ShipDurabilityState ReadDurability()
    {
        return durabilityState;
    }

    private void WriteDurability(ShipDurabilityState state)
    {
        durabilityState = state;
    }

    private void OnDamageApplied(DamageInfo info, DamageResolutionResult result)
    {
        if (spawnTriggered || destroyedHandled)
        {
            return;
        }

        float currentTotal = Mathf.Max(0f, result.State.Shield) + Mathf.Max(0f, result.State.Armor) + Mathf.Max(0f, result.State.Hull);
        float lostRatio = 1f - (currentTotal / Mathf.Max(1f, initialTotalDurability));
        float triggerRatio = Mathf.Clamp01(spawnTriggerDamagePercent / 100f);
        if (lostRatio + 0.0001f < triggerRatio)
        {
            return;
        }

        spawnTriggered = true;
        StartCoroutine(SpawnEnemiesRoutine());
    }

    private IEnumerator SpawnEnemiesRoutine()
    {
        int count = Mathf.Max(0, spawnEnemyCount);
        if (count <= 0)
        {
            yield break;
        }

        if (spawnMode == SpawnMode.Burst)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnOneEnemy(i);
            }
            yield break;
        }

        float interval = Mathf.Max(0.01f, spawnIntervalSeconds);
        for (int i = 0; i < count; i++)
        {
            SpawnOneEnemy(i);
            if (i < count - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }
    }

    private void SpawnOneEnemy(int index)
    {
        ResolveSceneController();
        if (sceneController == null)
        {
            return;
        }

        Vector3 spawnPosition = ResolveSpawnPosition(index);
        bool spawned = false;

        if (enemyShipData != null)
        {
            spawned = sceneController.SpawnEnemyFromExternalShipData(enemyShipData, spawnPosition);
        }
        else if (enemyPrefab != null)
        {
            spawned = sceneController.SpawnEnemyFromExternalPrefab(enemyPrefab, spawnPosition);
        }

        if (!spawned)
        {
            Debug.LogWarning(
                "[EnemyBaseLair] Не удалось заспавнить врага. Проверь поля enemyShipData/enemyPrefab и список availableShips в SpaceCombatSceneController.",
                this);
        }
    }

    private Vector3 ResolveSpawnPosition(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int safeIndex = Mathf.Abs(index) % spawnPoints.Length;
            Transform point = spawnPoints[safeIndex];
            if (point != null)
            {
                Vector3 pointPosition = point.position;
                pointPosition.z = 0f;
                return pointPosition;
            }
        }

        Vector2 offset = Random.insideUnitCircle * Mathf.Max(0.2f, fallbackSpawnRadius);
        Vector3 randomPosition = transform.position + new Vector3(offset.x, offset.y, 0f);
        randomPosition.z = 0f;
        return randomPosition;
    }

    private void OnDestroyedByDamage()
    {
        if (destroyedHandled)
        {
            return;
        }

        destroyedHandled = true;
        ResolveSceneController();
        if (sceneController != null && experienceReward > 0)
        {
            sceneController.AddExternalExperience(experienceReward);
        }

        Destroy(gameObject);
    }

    private void OnValidate()
    {
        maxShield = Mathf.Max(0f, maxShield);
        maxArmor = Mathf.Max(0f, maxArmor);
        maxHull = Mathf.Max(1f, maxHull);
        experienceReward = Mathf.Max(0, experienceReward);
        spawnTriggerDamagePercent = Mathf.Clamp(spawnTriggerDamagePercent, 0f, 100f);
        spawnEnemyCount = Mathf.Max(0, spawnEnemyCount);
        spawnIntervalSeconds = Mathf.Max(0.01f, spawnIntervalSeconds);
        fallbackSpawnRadius = Mathf.Max(0.2f, fallbackSpawnRadius);
    }
}
