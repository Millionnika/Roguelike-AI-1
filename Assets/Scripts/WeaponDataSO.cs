using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Weapon Data", fileName = "WeaponData")]
public sealed class WeaponDataSO : ScriptableObject
{
    [Header("Core")]
    [Tooltip("Режим стрельбы: снаряды или hitscan.")]
    public FireMode fireMode = FireMode.Projectile;
    [Tooltip("Базовый урон за выстрел.")]
    [Min(0f)] public float damage = 28f;
    [Tooltip("Перезарядка между выстрелами (сек).")]
    [Min(0f)] public float cooldown = 0.45f;
    [Tooltip("Оптимальная/основная дальность применения.")]
    [Min(0f)] public float maxRange = 6f;
    [Tooltip("Сектор автонаведения оружия в градусах.")]
    [Range(0f, 360f)] public float firingAngle = 360f;
    [Tooltip("Случайный разброс выстрела в градусах.")]
    [Range(0f, 45f)] public float spreadAngle = 0f;
    [Tooltip("Скорость доворота ствола к цели (град/сек).")]
    [Min(1f)] public float aimTurnSpeed = 720f;
    [Tooltip("Дополнительный поворот снаряда относительно вектора полета.")]
    [Range(-180f, 180f)] public float projectileRotationOffset = 90f;

    [Header("Projectile")]
    [Tooltip("Префаб снаряда.")]
    public GameObject projectilePrefab;
    [Tooltip("Скорость полета снаряда.")]
    [Min(0f)] public float projectileSpeed = 18f;
    [Tooltip("Максимальная дистанция полета снаряда.")]
    [Min(0f)] public float projectileMaxDistance = 8f;
    [Tooltip("Время жизни снаряда (сек).")]
    [Min(0f)] public float projectileLifetime = 2f;
    [Tooltip("Визуальный масштаб снаряда.")]
    [Min(0.01f)] public float projectileVisualScale = 0.16f;

    [Header("Legacy Compatibility")]
    [Tooltip("Устаревший параметр: скорострельность (для совместимости).")]
    public float fireRate = 0.45f;
    [Tooltip("Затраты энергии за выстрел.")]
    public float capacitorPerShot = 9f;
    [Tooltip("Минимальный класс корабля для установки оружия.")]
    public ShipClass requiredClass = ShipClass.Light;

    [Header("Visual")]
    [Tooltip("Иконка оружия для UI.")]
    public Sprite icon;
    [Tooltip("Префаб визуала ствола/турели.")]
    public GameObject visualPrefab;

    [Header("Audio")]
    [Tooltip("Звук выстрела.")]
    public AudioClip fireSound;

    private void OnValidate()
    {
        aimTurnSpeed = Mathf.Max(1f, aimTurnSpeed);
    }
}
