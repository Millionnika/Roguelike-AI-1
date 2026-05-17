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
    [Header("Визуал")]
    [Tooltip("Медленное вращение базы вокруг своей оси (без перемещения).")]
    [SerializeField] private bool idleSpinEnabled = true;
    [Tooltip("Скорость вращения базы в градусах/сек.")]
    [SerializeField, Range(-30f, 30f)] private float idleSpinSpeed = 4f;

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
    [Tooltip("Если включено, база будет периодически спавнить врагов, пока жива.")]
    [SerializeField] private bool continuousSpawnEnabled;
    [Tooltip("Сколько врагов спавнить за один цикл непрерывного спавна.")]
    [Min(1)] [SerializeField] private int continuousSpawnCount = 2;
    [Tooltip("Пауза между циклами непрерывного спавна.")]
    [Min(0.25f)] [SerializeField] private float continuousSpawnIntervalSeconds = 8f;
    [Tooltip("Стартовая задержка перед первым циклом непрерывного спавна.")]
    [Min(0f)] [SerializeField] private float continuousSpawnStartDelay = 2f;

    [Header("Кого спавнить")]
    [Tooltip("ShipData врага. Приоритетнее prefab, потому что создаёт полноценного врага через контроллер сцены.")]
    [SerializeField] private ShipDataSO enemyShipData;
    [Tooltip("Префаб врага. Используется, если ShipData не задан.")]
    [SerializeField] private GameObject enemyPrefab;
    [Tooltip("Оружие базы. Если не задано, будет использовано первое оружие из enemyShipData.")]
    [SerializeField] private WeaponDataSO baseWeaponData;

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
    private Coroutine continuousSpawnRoutine;

    public bool IsAlive => !destroyedHandled && durabilityState.Hull > 0.01f;
    public ShipDurabilityState CurrentDurability => durabilityState;

    private void Awake()
    {
        InitializeDurability();
        CombatLayerUtility.ApplyShipLayer(gameObject, CombatFaction.Enemy);
        EnsureTeamMember();
        EnsureDamageReceiver();
        EnsureDefenseBattery();
        ResolveSceneController();
        TryStartContinuousSpawn();
    }

    private void OnDestroy()
    {
        if (damageReceiver != null)
        {
            damageReceiver.DamageApplied -= OnDamageApplied;
        }

        StopContinuousSpawn();
    }

    private void Update()
    {
        if (!idleSpinEnabled)
        {
            return;
        }

        float spin = idleSpinSpeed * Time.deltaTime;
        if (Mathf.Abs(spin) > 0.0001f)
        {
            transform.Rotate(0f, 0f, spin, Space.Self);
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

    private void EnsureTeamMember()
    {
        TeamMember member = GetComponent<TeamMember>();
        if (member == null)
        {
            member = gameObject.AddComponent<TeamMember>();
        }

        member.SetFaction(CombatFaction.Enemy);
    }

    private void EnsureDefenseBattery()
    {
        BaseDefenseBattery battery = GetComponent<BaseDefenseBattery>();
        if (battery == null)
        {
            battery = gameObject.AddComponent<BaseDefenseBattery>();
        }

        battery.ConfigureFaction(CombatFaction.Enemy);
        battery.EnsureWeapon(ResolveBaseWeapon());
    }

    private WeaponDataSO ResolveBaseWeapon()
    {
        if (baseWeaponData != null)
        {
            return baseWeaponData;
        }

        if (enemyShipData != null && enemyShipData.startingWeapons != null)
        {
            for (int i = 0; i < enemyShipData.startingWeapons.Count; i++)
            {
                WeaponDataSO weapon = enemyShipData.startingWeapons[i];
                if (weapon != null)
                {
                    return weapon;
                }
            }
        }

        return null;
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

    public void ConfigureContinuousSpawn(bool enabled, int countPerCycle, float intervalSeconds, float startDelaySeconds)
    {
        continuousSpawnEnabled = enabled;
        continuousSpawnCount = Mathf.Max(1, countPerCycle);
        continuousSpawnIntervalSeconds = Mathf.Max(0.25f, intervalSeconds);
        continuousSpawnStartDelay = Mathf.Max(0f, startDelaySeconds);

        if (!continuousSpawnEnabled)
        {
            StopContinuousSpawn();
            return;
        }

        TryStartContinuousSpawn();
    }

    public void EnableContinuousSpawn()
    {
        continuousSpawnEnabled = true;
        TryStartContinuousSpawn();
    }

    private void TryStartContinuousSpawn()
    {
        if (!continuousSpawnEnabled || destroyedHandled || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (continuousSpawnRoutine != null)
        {
            return;
        }

        continuousSpawnRoutine = StartCoroutine(ContinuousSpawnRoutine());
    }

    private void StopContinuousSpawn()
    {
        if (continuousSpawnRoutine == null)
        {
            return;
        }

        StopCoroutine(continuousSpawnRoutine);
        continuousSpawnRoutine = null;
    }

    private IEnumerator ContinuousSpawnRoutine()
    {
        if (continuousSpawnStartDelay > 0f)
        {
            yield return new WaitForSeconds(continuousSpawnStartDelay);
        }

        while (!destroyedHandled && IsAlive)
        {
            int spawnCount = Mathf.Max(1, continuousSpawnCount);
            for (int i = 0; i < spawnCount; i++)
            {
                SpawnOneEnemy(i);
            }

            float wait = Mathf.Max(0.25f, continuousSpawnIntervalSeconds);
            yield return new WaitForSeconds(wait);
        }

        continuousSpawnRoutine = null;
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
        StopContinuousSpawn();
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
        continuousSpawnCount = Mathf.Max(1, continuousSpawnCount);
        continuousSpawnIntervalSeconds = Mathf.Max(0.25f, continuousSpawnIntervalSeconds);
        continuousSpawnStartDelay = Mathf.Max(0f, continuousSpawnStartDelay);
        fallbackSpawnRadius = Mathf.Max(0.2f, fallbackSpawnRadius);
    }
}
