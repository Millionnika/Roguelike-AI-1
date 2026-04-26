using UnityEngine;

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
