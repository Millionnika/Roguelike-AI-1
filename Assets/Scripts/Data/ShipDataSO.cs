using System.Collections.Generic;
using UnityEngine;

public enum EnemyBehaviorPreset
{
    Custom = 0,
    Aggressive = 1,
    Balanced = 2,
    Cautious = 3
}

[CreateAssetMenu(menuName = "Roguelike/Ship Data", fileName = "ShipData")]
public sealed class ShipDataSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Имя корабля для UI.")]
    public string displayName = "Aegis";
    [Tooltip("Роль/класс корабля для UI.")]
    public string role = "Balanced Frigate";
    [Tooltip("Описание корабля (EN).")]
    [TextArea(2, 5)] public string description = "Universal hull profile.";
    [Tooltip("Роль/класс корабля для русской локализации.")]
    public string roleRu = "Пользовательский корпус";
    [Tooltip("Описание корабля (RU).")]
    [TextArea(2, 5)] public string descriptionRu = "Создано через Ship Factory.";
    [Tooltip("Ограничение по классу корабля для совместимости вооружения.")]
    public ShipClass shipClass = ShipClass.Medium;

    [Header("Movement")]
    [Tooltip("Максимальная скорость.")]
    public float maxSpeed = 6.5f;
    [Tooltip("Ускорение.")]
    public float acceleration = 11f;
    [Tooltip("Скорость поворота.")]
    public float rotationSpeed = 8.5f;
    [Tooltip("Коэффициент торможения/сопротивления.")]
    public float drag = 1.6f;

    [Header("Enemy AI Distance")]
    [Tooltip("Готовый пресет поведения врага. Custom = использовать ручные поля ниже.")]
    public EnemyBehaviorPreset enemyBehaviorPreset = EnemyBehaviorPreset.Custom;
    [Tooltip("Желаемая дистанция боя для врагов этого корпуса. 0 = авто от дальности оружия.")]
    [Min(0f)] public float enemyPreferredDistance = 0f;
    [Tooltip("Доля от дальности оружия для авто-дистанции (если enemyPreferredDistance = 0).")]
    [Range(0.3f, 0.98f)] public float enemyPreferredDistanceFromRange = 0.78f;
    [Tooltip("Случайное отклонение желаемой дистанции (живость поведения).")]
    [Min(0f)] public float enemyPreferredDistanceVariance = 0.45f;
    [Tooltip("Допуск удержания дистанции: в пределах этого окна враг не дергается вперед.")]
    [Min(0.05f)] public float enemyDistanceTolerance = 0.35f;
    [Tooltip("Порог (в доле дальности оружия), после которого враг обязан сближаться для выстрела.")]
    [Range(0.5f, 1.2f)] public float enemyOutOfRangeApproachFactor = 0.95f;
    [Tooltip("Порог суммарной прочности (щит+броня+корпус), ниже которого враг начинает осторожно отступать.")]
    [Range(0f, 1f)] public float enemyLowDurabilityRetreatThreshold = 0.35f;
    [Tooltip("Дополнительная дистанция отступления при низкой прочности.")]
    [Min(0f)] public float enemyLowDurabilityRetreatDistanceBonus = 1.2f;
    [Tooltip("Дополнительный множитель скорости отступления при низкой прочности.")]
    [Range(1f, 3f)] public float enemyLowDurabilityRetreatSpeedMultiplier = 1.35f;
    [Tooltip("Сила микроколебаний орбиты для более живого движения.")]
    [Range(0f, 1f)] public float enemyStrafeJitterAmplitude = 0.22f;
    [Tooltip("Частота микроколебаний орбиты.")]
    [Range(0.1f, 4f)] public float enemyStrafeJitterFrequency = 1.35f;

    [Header("Survivability")]
    [Tooltip("Максимум щита.")]
    public float maxShield = 430f;
    [Tooltip("Максимум брони.")]
    public float maxArmor = 320f;
    [Tooltip("Максимум корпуса.")]
    public float maxHull = 220f;
    [Tooltip("Емкость энергии (capacitor).")]
    public float capacitor = 1200f;
    [Tooltip("Время до начала/полного цикла регенерации энергии (legacy логика).")]
    public float capacitorRechargeTime = 92f;
    [Tooltip("Скорость регенерации энергии.")]
    [Min(0.1f)] public float capacitorRechargeRate = 1.2f;
    [Tooltip("Очки за уничтожение этого корабля.")]
    [Min(0)] public int scoreReward = 40;
    [Tooltip("Количество Scrap, которое выпадет из этого корабля при уничтожении. Если 0, используется случайное значение 1-3.")]
    [Min(0)] public int scrapDropAmount = 0;

    [Header("Loadout")]
    [Tooltip("Количество слотов оружия.")]
    public int weaponSlotCount = 2;
    [Tooltip("Количество слотов модулей.")]
    public int moduleSlotCount = 4;
    [Tooltip("Множитель урона от оружия.")]
    public float damageMultiplier = 1f;
    [Tooltip("Множитель эффективности ремонта/репа.")]
    public float repairMultiplier = 1f;
    [Tooltip("Префаб визуала/модели корабля.")]
    public GameObject shipPrefab;
    [Tooltip("Стартовое оружие по слотам.")]
    public List<WeaponDataSO> startingWeapons = new List<WeaponDataSO>();
    [Tooltip("Стартовые модули по слотам.")]
    public List<ModuleDataSO> startingModules = new List<ModuleDataSO>();

    [Header("Эффекты двигателя")]
    [Tooltip("Префаб эффекта двигателя для этого корабля. Создаётся на всех точках двигателя внутри объекта Thruster.")]
    public GameObject engineVfxPrefab;
    [Tooltip("Интенсивность эффекта двигателя, когда корабль почти не движется. 0 = двигатель визуально выключен.")]
    [Min(0f)] public float engineIdleEmissionRate = 0f;
    [Tooltip("Интенсивность эффекта двигателя при движении корабля.")]
    [Min(0f)] public float engineMovingEmissionRate = 18f;
    [Tooltip("Множитель интенсивности эффекта двигателя при форсаже или максимальной тяге.")]
    [Min(1f)] public float engineAfterburnerEmissionMultiplier = 2f;
    [Tooltip("Скорость плавного изменения интенсивности эффекта двигателя.")]
    [Min(0f)] public float engineEmissionLerpSpeed = 8f;
    [Tooltip("Масштаб создаваемого префаба эффекта двигателя на точках двигателя.")]
    [Min(0.01f)] public float engineVfxScale = 1f;

    [Header("Visual")]
    [Tooltip("Базовый акцентный цвет корабля.")]
    public Color accentColor = new Color(0.28f, 0.6f, 0.94f, 1f);
    [Tooltip("Базовый цвет/прозрачность ауры (щита).")]
    public Color auraColor = new Color(0.38f, 0.76f, 1f, 0.72f);
    [Tooltip("Иконка корабля для UI.")]
    public Sprite shipIcon;

    private void OnValidate()
    {
        ApplyEnemyBehaviorPreset();

        weaponSlotCount = Mathf.Max(0, weaponSlotCount);
        moduleSlotCount = Mathf.Max(0, moduleSlotCount);
        startingWeapons ??= new List<WeaponDataSO>();
        startingModules ??= new List<ModuleDataSO>();
        capacitorRechargeRate = Mathf.Max(0.1f, capacitorRechargeRate);
        engineIdleEmissionRate = Mathf.Max(0f, engineIdleEmissionRate);
        engineMovingEmissionRate = Mathf.Max(0f, engineMovingEmissionRate);
        engineAfterburnerEmissionMultiplier = Mathf.Max(1f, engineAfterburnerEmissionMultiplier);
        engineEmissionLerpSpeed = Mathf.Max(0f, engineEmissionLerpSpeed);
        engineVfxScale = Mathf.Max(0.01f, engineVfxScale);

        enemyPreferredDistance = Mathf.Max(0f, enemyPreferredDistance);
        enemyPreferredDistanceFromRange = Mathf.Clamp(enemyPreferredDistanceFromRange, 0.3f, 0.98f);
        enemyPreferredDistanceVariance = Mathf.Max(0f, enemyPreferredDistanceVariance);
        enemyDistanceTolerance = Mathf.Max(0.05f, enemyDistanceTolerance);
        enemyOutOfRangeApproachFactor = Mathf.Clamp(enemyOutOfRangeApproachFactor, 0.5f, 1.2f);
        enemyLowDurabilityRetreatThreshold = Mathf.Clamp01(enemyLowDurabilityRetreatThreshold);
        enemyLowDurabilityRetreatDistanceBonus = Mathf.Max(0f, enemyLowDurabilityRetreatDistanceBonus);
        enemyLowDurabilityRetreatSpeedMultiplier = Mathf.Clamp(enemyLowDurabilityRetreatSpeedMultiplier, 1f, 3f);
        enemyStrafeJitterAmplitude = Mathf.Clamp01(enemyStrafeJitterAmplitude);
        enemyStrafeJitterFrequency = Mathf.Clamp(enemyStrafeJitterFrequency, 0.1f, 4f);

        Resize(startingWeapons, weaponSlotCount);
        Resize(startingModules, moduleSlotCount);
    }

    private void ApplyEnemyBehaviorPreset()
    {
        switch (enemyBehaviorPreset)
        {
            case EnemyBehaviorPreset.Aggressive:
                enemyPreferredDistanceFromRange = 0.68f;
                enemyPreferredDistanceVariance = 0.35f;
                enemyDistanceTolerance = 0.28f;
                enemyOutOfRangeApproachFactor = 0.98f;
                enemyLowDurabilityRetreatThreshold = 0.18f;
                enemyLowDurabilityRetreatDistanceBonus = 0.65f;
                enemyLowDurabilityRetreatSpeedMultiplier = 1.15f;
                enemyStrafeJitterAmplitude = 0.26f;
                enemyStrafeJitterFrequency = 1.7f;
                break;

            case EnemyBehaviorPreset.Balanced:
                enemyPreferredDistanceFromRange = 0.82f;
                enemyPreferredDistanceVariance = 0.55f;
                enemyDistanceTolerance = 0.45f;
                enemyOutOfRangeApproachFactor = 0.92f;
                enemyLowDurabilityRetreatThreshold = 0.38f;
                enemyLowDurabilityRetreatDistanceBonus = 1.6f;
                enemyLowDurabilityRetreatSpeedMultiplier = 1.45f;
                enemyStrafeJitterAmplitude = 0.24f;
                enemyStrafeJitterFrequency = 1.4f;
                break;

            case EnemyBehaviorPreset.Cautious:
                enemyPreferredDistanceFromRange = 0.9f;
                enemyPreferredDistanceVariance = 0.42f;
                enemyDistanceTolerance = 0.52f;
                enemyOutOfRangeApproachFactor = 0.86f;
                enemyLowDurabilityRetreatThreshold = 0.52f;
                enemyLowDurabilityRetreatDistanceBonus = 2.2f;
                enemyLowDurabilityRetreatSpeedMultiplier = 1.7f;
                enemyStrafeJitterAmplitude = 0.2f;
                enemyStrafeJitterFrequency = 1.15f;
                break;

            case EnemyBehaviorPreset.Custom:
            default:
                break;
        }
    }

    private static void Resize<T>(List<T> list, int targetCount)
    {
        while (list.Count < targetCount)
        {
            list.Add(default);
        }

        while (list.Count > targetCount)
        {
            list.RemoveAt(list.Count - 1);
        }
    }
}
