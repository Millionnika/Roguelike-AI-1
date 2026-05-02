using UnityEngine;

public enum RewardType
{
    Scrap,
    HullRepairPercent,
    ShieldRestorePercent,
    DamageMultiplierPercent,
    FireRatePercent,
    CapacitorMaxPercent
}

public enum RewardRarity
{
    Common,
    Rare,
    Epic
}

[CreateAssetMenu(menuName = "Roguelike/Reward", fileName = "Reward")]
public sealed class RewardSO : ScriptableObject
{
    [Header("Параметры награды")]
    [Tooltip("Название награды, отображаемое в окне выбора.")]
    public string displayName = "Награда";
    [Tooltip("Краткое описание эффекта награды.")]
    [TextArea(2, 4)] public string description;
    [Tooltip("Иконка награды для UI (опционально).")]
    public Sprite icon;
    [Tooltip("Тип эффекта награды. Определяет, как она применяется к игроку и ресурсам.")]
    public RewardType rewardType = RewardType.Scrap;
    [Tooltip("Числовая сила награды. Для процентов используйте значения в процентах, например 20 = 20%.")]
    public float amount = 10f;
    [Tooltip("Вес для случайного выбора из таблицы. Чем выше значение, тем чаще награда выпадает.")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("Редкость награды для визуальной маркировки и будущего баланса.")]
    public RewardRarity rarity = RewardRarity.Common;

    private void OnValidate()
    {
        weight = Mathf.Max(0f, weight);
    }
}
