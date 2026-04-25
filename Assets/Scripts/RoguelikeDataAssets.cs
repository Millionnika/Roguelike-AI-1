using UnityEngine;

public enum ShipClass
{
    Light,
    Medium,
    Heavy
}

[CreateAssetMenu(menuName = "Roguelike/Movement Settings", fileName = "MovementSettings")]
public sealed class MovementSettingsSO : ScriptableObject
{
    public float moveSpeed = 6.2f;
    public float rotationSpeed = 8f;
    public float stoppingDistance = 0.25f;
}

[CreateAssetMenu(menuName = "Roguelike/Weapon Data", fileName = "WeaponData")]
public sealed class WeaponDataSO : ScriptableObject
{
    public float damage = 28f;
    public float fireRate = 0.45f;
    public float projectileSpeed = 18f;
    public float capacitorPerShot = 9f;
    public ShipClass requiredClass = ShipClass.Light;
    public Sprite icon;
    public GameObject projectilePrefab;
    public AudioClip fireSound;
}

[CreateAssetMenu(menuName = "Roguelike/Enemy Data", fileName = "EnemyData")]
public sealed class EnemyDataSO : ScriptableObject
{
    public float maxHealth = 100f;
    public float moveSpeed = 1.5f;
    public int scoreValue = 40;
    public GameObject prefab;
    public WeaponDataSO weaponData;
}

[CreateAssetMenu(menuName = "Roguelike/Module Data", fileName = "ModuleData")]
public sealed class ModuleDataSO : ScriptableObject
{
    public string displayName = "Module";
    public Sprite icon;
}

[CreateAssetMenu(menuName = "Roguelike/Ship Data", fileName = "ShipData")]
public sealed class ShipDataSO : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Aegis";
    public string role = "Balanced Frigate";
    [TextArea(2, 5)] public string description = "Universal hull profile.";
    public string roleRu = "Сбалансированный фрегат";
    [TextArea(2, 5)] public string descriptionRu = "Универсальный корпус.";
    public ShipClass shipClass = ShipClass.Medium;

    [Header("Movement")]
    public float maxSpeed = 6.5f;
    public float acceleration = 11f;
    public float rotationSpeed = 8.5f;
    public float drag = 1.6f;

    [Header("Survivability")]
    public float maxShield = 430f;
    public float maxArmor = 320f;
    public float maxHull = 220f;
    public float capacitor = 1200f;
    public float capacitorRechargeTime = 92f;

    [Header("Loadout")]
    public int weaponSlotCount = 2;
    public int moduleSlotCount = 4;
    public float damageMultiplier = 1f;
    public float repairMultiplier = 1f;

    [Header("Visual")]
    public Color accentColor = new Color(0.28f, 0.6f, 0.94f, 1f);
    public Color auraColor = new Color(0.38f, 0.76f, 1f, 0.72f);
    public Sprite shipIcon;
}
