using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Module Data", fileName = "ModuleData")]
public sealed class ModuleDataSO : ScriptableObject
{
    public string displayName = "Module";
    public Sprite icon;
}
