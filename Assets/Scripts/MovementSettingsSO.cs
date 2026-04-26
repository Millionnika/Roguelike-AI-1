using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Movement Settings", fileName = "MovementSettings")]
public sealed class MovementSettingsSO : ScriptableObject
{
    public float moveSpeed = 6.2f;
    public float rotationSpeed = 8f;
    public float stoppingDistance = 0.25f;
}
