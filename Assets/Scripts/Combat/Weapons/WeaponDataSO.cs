using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Weapon Data", fileName = "WeaponData")]
public sealed class WeaponDataSO : ScriptableObject
{
    [Header("Основное")]
    [Tooltip("Режим стрельбы оружия: снаряд, hitscan, beam или ракета.")]
    public FireMode fireMode = FireMode.Projectile;
    [Tooltip("Профиль распределения урона по слоям защиты. Если не задан, применяется каскад Shield -> Armor -> Hull.")]
    public DamageDistributionProfileSO damageProfile;
    [Tooltip("Базовый урон за один выстрел до модификаторов корабля.")]
    [Min(0f)] public float damage = 28f;
    [Tooltip("Перезарядка между выстрелами в секундах.")]
    [Min(0f)] public float cooldown = 0.45f;
    [Tooltip("Базовая эффективная дальность оружия.")]
    [Min(0f)] public float maxRange = 6f;
    [Tooltip("Разрешенный сектор наведения в градусах.")]
    [Range(0f, 360f)] public float firingAngle = 360f;
    [Tooltip("Случайный разброс выстрела в градусах.")]
    [Range(0f, 45f)] public float spreadAngle = 0f;
    [Tooltip("Скорость поворота орудия в градусах в секунду.")]
    [Min(1f)] public float aimTurnSpeed = 720f;
    [Tooltip("Визуальный сдвиг поворота снаряда относительно направления полета.")]
    [Range(-180f, 180f)] public float projectileRotationOffset = 90f;

    [Header("Снаряд")]
    [Tooltip("Префаб, который спавнится как летящий снаряд.")]
    public GameObject projectilePrefab;
    [Tooltip("Базовая скорость снаряда.")]
    [Min(0f)] public float projectileSpeed = 18f;
    [Tooltip("Максимальная дистанция полета снаряда.")]
    [Min(0f)] public float projectileMaxDistance = 8f;
    [Tooltip("Время жизни снаряда в секундах.")]
    [Min(0f)] public float projectileLifetime = 2f;
    [Tooltip("Визуальный масштаб снаряда.")]
    [Min(0.01f)] public float projectileVisualScale = 0.16f;

    [Header("Ракета")]
    [Tooltip("Скорость поворота ракеты к цели (град/сек).")]
    [Min(1f)] public float missileTurnSpeed = 240f;
    [Tooltip("Радиус поиска цели для самонаведения.")]
    [Min(0.1f)] public float missileSeekRadius = 18f;
    [Tooltip("Амплитуда бокового колебания траектории.")]
    [Min(0f)] public float missileWobbleAmplitude = 0.08f;
    [Tooltip("Частота бокового колебания траектории.")]
    [Min(0f)] public float missileWobbleFrequency = 8f;
    [Tooltip("Обычное ускорение ракеты в полете (для фазы разгона/поддержания).")]
    [Min(0f)] public float missileAcceleration = 0.6f;
    [Tooltip("Множитель скорости на старте ракеты (медленный вылет).")]
    [Min(0.01f)] public float missileLaunchSpeedMultiplier = 0.35f;
    [Tooltip("Длительность медленного вылета после запуска, сек.")]
    [Min(0f)] public float missileLaunchDuration = 0.2f;
    [Tooltip("Длительность фазы почти остановки для доводки на цель, сек.")]
    [Min(0f)] public float missileAimPauseDuration = 0.12f;
    [Tooltip("Множитель скорости в фазе доводки (0 = полная остановка).")]
    [Min(0f)] public float missileAimPauseSpeedMultiplier = 0.05f;
    [Tooltip("Множитель максимальной скорости в фазе резкого ускорения.")]
    [Min(0.1f)] public float missileBoostSpeedMultiplier = 2.8f;
    [Tooltip("Ускорение в фазе резкого рывка после доводки.")]
    [Min(0f)] public float missileBoostAcceleration = 42f;

    [Header("Совместимость")]
    [Tooltip("Устаревшее поле скорострельности, оставлено для обратной совместимости.")]
    public float fireRate = 0.45f;
    [Tooltip("Расход энергии (capacitor) за один выстрел.")]
    public float capacitorPerShot = 9f;
    [Tooltip("Минимальный класс корабля для установки оружия.")]
    public ShipClass requiredClass = ShipClass.Light;

    [Header("Визуал")]
    [Tooltip("Иконка оружия для UI.")]
    public Sprite icon;
    [Tooltip("Статичный визуал на слоте оружия (не префаб летящего снаряда).")]
    public GameObject visualPrefab;
    [Tooltip("Префаб эффекта попадания. Создаётся в точке успешного попадания снаряда.")]
    public GameObject impactVfxPrefab;
    [Tooltip("Сколько секунд эффект попадания живёт перед удалением или возвратом в пул.")]
    [Min(0f)] public float impactVfxLifetime = 0.75f;
    [Tooltip("Масштаб эффекта попадания при создании.")]
    [Min(0.01f)] public float impactVfxScale = 1f;
    [Tooltip("Префаб следа снаряда. Создаётся на снаряде при выстреле и летит вместе с ним.")]
    public GameObject projectileTrailPrefab;
    [Tooltip("Масштаб следа снаряда.")]
    [Min(0.01f)] public float projectileTrailScale = 1f;
    [Tooltip("Если включено, след отделяется от снаряда при исчезновении и плавно догорает.")]
    public bool detachTrailOnDespawn = true;
    [Tooltip("Сколько секунд отделённый след живёт после исчезновения снаряда.")]
    [Min(0f)] public float detachedTrailLifetime = 0.4f;

    [Header("Звук")]
    [Tooltip("Звуковой клип выстрела.")]
    public AudioClip fireSound;

    private void OnValidate()
    {
        aimTurnSpeed = Mathf.Max(1f, aimTurnSpeed);
        missileLaunchSpeedMultiplier = Mathf.Max(0.01f, missileLaunchSpeedMultiplier);
        missileLaunchDuration = Mathf.Max(0f, missileLaunchDuration);
        missileAimPauseDuration = Mathf.Max(0f, missileAimPauseDuration);
        missileAimPauseSpeedMultiplier = Mathf.Max(0f, missileAimPauseSpeedMultiplier);
        missileBoostSpeedMultiplier = Mathf.Max(0.1f, missileBoostSpeedMultiplier);
        missileBoostAcceleration = Mathf.Max(0f, missileBoostAcceleration);
        impactVfxLifetime = Mathf.Max(0f, impactVfxLifetime);
        impactVfxScale = Mathf.Max(0.01f, impactVfxScale);
        projectileTrailScale = Mathf.Max(0.01f, projectileTrailScale);
        detachedTrailLifetime = Mathf.Max(0f, detachedTrailLifetime);
    }
}
