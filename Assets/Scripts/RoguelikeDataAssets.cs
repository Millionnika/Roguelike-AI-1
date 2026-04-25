using UnityEngine;

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
