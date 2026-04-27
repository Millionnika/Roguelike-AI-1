using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Weapon Data", fileName = "WeaponData")]
public sealed class WeaponDataSO : ScriptableObject
{
    [Header("Core")]
    public FireMode fireMode = FireMode.Projectile;
    [Min(0f)] public float damage = 28f;
    [Min(0f)] public float cooldown = 0.45f;
    [Min(0f)] public float maxRange = 6f;
    [Range(0f, 360f)] public float firingAngle = 360f;
    [Range(0f, 45f)] public float spreadAngle = 0f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    [Min(0f)] public float projectileSpeed = 18f;
    [Min(0f)] public float projectileMaxDistance = 8f;
    [Min(0f)] public float projectileLifetime = 2f;

    [Header("Legacy Compatibility")]
    public float fireRate = 0.45f;
    public float capacitorPerShot = 9f;
    public ShipClass requiredClass = ShipClass.Light;

    [Header("Visual")]
    public Sprite icon;

    [Header("Audio")]
    public AudioClip fireSound;
}
