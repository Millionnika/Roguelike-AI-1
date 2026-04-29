using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Module Data", fileName = "ModuleData")]
public sealed class ModuleDataSO : ScriptableObject
{
    [Tooltip("Название модуля для UI.")]
    public string displayName = "Module";
    [Tooltip("Иконка модуля для UI.")]
    public Sprite icon;
}
