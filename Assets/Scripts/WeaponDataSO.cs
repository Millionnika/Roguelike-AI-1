using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Weapon Data", fileName = "WeaponData")]
public sealed class WeaponDataSO : ScriptableObject
{
    [Header("Core")]
    [Tooltip("Weapon fire mode (projectile, hitscan, beam, missile).")]
    public FireMode fireMode = FireMode.Projectile;
    [Tooltip("Damage split profile. If null, legacy cascade is used: Shield -> Armor -> Hull.")]
    public DamageDistributionProfileSO damageProfile;
    [Tooltip("Base damage per shot before ship multipliers.")]
    [Min(0f)] public float damage = 28f;
    [Tooltip("Cooldown between shots in seconds.")]
    [Min(0f)] public float cooldown = 0.45f;
    [Tooltip("Primary effective range.")]
    [Min(0f)] public float maxRange = 6f;
    [Tooltip("Allowed aiming sector in degrees.")]
    [Range(0f, 360f)] public float firingAngle = 360f;
    [Tooltip("Random spread angle in degrees.")]
    [Range(0f, 45f)] public float spreadAngle = 0f;
    [Tooltip("Turret/mount turn speed in degrees per second.")]
    [Min(1f)] public float aimTurnSpeed = 720f;
    [Tooltip("Visual projectile rotation offset relative to flight direction.")]
    [Range(-180f, 180f)] public float projectileRotationOffset = 90f;

    [Header("Projectile")]
    [Tooltip("Prefab spawned as the flying projectile.")]
    public GameObject projectilePrefab;
    [Tooltip("Projectile speed.")]
    [Min(0f)] public float projectileSpeed = 18f;
    [Tooltip("Projectile max travel distance.")]
    [Min(0f)] public float projectileMaxDistance = 8f;
    [Tooltip("Projectile lifetime in seconds.")]
    [Min(0f)] public float projectileLifetime = 2f;
    [Tooltip("Projectile visual scale multiplier.")]
    [Min(0.01f)] public float projectileVisualScale = 0.16f;

    [Header("Missile")]
    [Tooltip("Missile turn speed toward target.")]
    [Min(1f)] public float missileTurnSpeed = 240f;
    [Tooltip("Missile target acquisition radius.")]
    [Min(0.1f)] public float missileSeekRadius = 18f;
    [Tooltip("Missile side wobble amplitude.")]
    [Min(0f)] public float missileWobbleAmplitude = 0.08f;
    [Tooltip("Missile side wobble frequency.")]
    [Min(0f)] public float missileWobbleFrequency = 8f;
    [Tooltip("Missile acceleration over time.")]
    [Min(0f)] public float missileAcceleration = 0.6f;

    [Header("Legacy Compatibility")]
    [Tooltip("Legacy fire rate field kept for compatibility.")]
    public float fireRate = 0.45f;
    [Tooltip("Capacitor cost per shot.")]
    public float capacitorPerShot = 9f;
    [Tooltip("Minimum ship class required to equip this weapon.")]
    public ShipClass requiredClass = ShipClass.Light;

    [Header("Visual")]
    [Tooltip("Icon used in UI.")]
    public Sprite icon;
    [Tooltip("Static weapon model/prefab placed on weapon mount (not the projectile prefab).")]
    public GameObject visualPrefab;

    [Header("Audio")]
    [Tooltip("Shot sound clip.")]
    public AudioClip fireSound;

    private void OnValidate()
    {
        aimTurnSpeed = Mathf.Max(1f, aimTurnSpeed);
    }
}
