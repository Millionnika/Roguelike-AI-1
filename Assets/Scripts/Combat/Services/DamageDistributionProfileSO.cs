using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Combat/Damage Distribution Profile", fileName = "DamageDistributionProfile")]
public sealed class DamageDistributionProfileSO : ScriptableObject
{
    [Tooltip("Распределение урона по слоям в процентах. Если сумма не 100, будет выполнена нормализация.")]
    public List<DamageLayerShare> shares = new List<DamageLayerShare>
    {
        new DamageLayerShare { layer = DamageLayerType.Shield, percent = 100f },
        new DamageLayerShare { layer = DamageLayerType.Armor, percent = 0f },
        new DamageLayerShare { layer = DamageLayerType.Hull, percent = 0f }
    };
}
