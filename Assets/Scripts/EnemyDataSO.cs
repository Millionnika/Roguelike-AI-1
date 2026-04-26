using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Enemy Data", fileName = "EnemyData")]
public sealed class EnemyDataSO : ScriptableObject
{
    public float maxHealth = 100f;
    public float moveSpeed = 1.5f;
    public int scoreValue = 40;
    public GameObject prefab;
    public WeaponDataSO weaponData;
}
