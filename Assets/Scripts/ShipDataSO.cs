using System.Collections.Generic;
using UnityEngine;

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
    public string roleRu = "Р РЋР В±Р В°Р В»Р В°Р Р…РЎРѓР С‘РЎР‚Р С•Р Р†Р В°Р Р…Р Р…РЎвЂ№Р в„– РЎвЂћРЎР‚Р ВµР С–Р В°РЎвЂљ";
    [Tooltip("Описание корабля (RU).")]
    [TextArea(2, 5)] public string descriptionRu = "Р Р€Р Р…Р С‘Р Р†Р ВµРЎР‚РЎРѓР В°Р В»РЎРЉР Р…РЎвЂ№Р в„– Р С”Р С•РЎР‚Р С—РЎС“РЎРѓ.";
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

    [Header("Visual")]
    [Tooltip("Базовый акцентный цвет корабля.")]
    public Color accentColor = new Color(0.28f, 0.6f, 0.94f, 1f);
    [Tooltip("Базовый цвет/прозрачность ауры (щита).")]
    public Color auraColor = new Color(0.38f, 0.76f, 1f, 0.72f);
    [Tooltip("Иконка корабля для UI.")]
    public Sprite shipIcon;

    private void OnValidate()
    {
        weaponSlotCount = Mathf.Max(0, weaponSlotCount);
        moduleSlotCount = Mathf.Max(0, moduleSlotCount);
        startingWeapons ??= new List<WeaponDataSO>();
        startingModules ??= new List<ModuleDataSO>();
        capacitorRechargeRate = Mathf.Max(0.1f, capacitorRechargeRate);
        Resize(startingWeapons, weaponSlotCount);
        Resize(startingModules, moduleSlotCount);
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
