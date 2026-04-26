using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Ship Data", fileName = "ShipData")]
public sealed class ShipDataSO : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Aegis";
    public string role = "Balanced Frigate";
    [TextArea(2, 5)] public string description = "Universal hull profile.";
    public string roleRu = "РЎР±Р°Р»Р°РЅСЃРёСЂРѕРІР°РЅРЅС‹Р№ С„СЂРµРіР°С‚";
    [TextArea(2, 5)] public string descriptionRu = "РЈРЅРёРІРµСЂСЃР°Р»СЊРЅС‹Р№ РєРѕСЂРїСѓСЃ.";
    public ShipClass shipClass = ShipClass.Medium;

    [Header("Movement")]
    public float maxSpeed = 6.5f;
    public float acceleration = 11f;
    public float rotationSpeed = 8.5f;
    public float drag = 1.6f;

    [Header("Survivability")]
    public float maxShield = 430f;
    public float maxArmor = 320f;
    public float maxHull = 220f;
    public float capacitor = 1200f;
    public float capacitorRechargeTime = 92f;
    [Min(0)] public int scoreReward = 40;

    [Header("Loadout")]
    public int weaponSlotCount = 2;
    public int moduleSlotCount = 4;
    public float damageMultiplier = 1f;
    public float repairMultiplier = 1f;
    public GameObject shipPrefab;
    public List<WeaponDataSO> startingWeapons = new List<WeaponDataSO>();
    public List<ModuleDataSO> startingModules = new List<ModuleDataSO>();

    [Header("Visual")]
    public Color accentColor = new Color(0.28f, 0.6f, 0.94f, 1f);
    public Color auraColor = new Color(0.38f, 0.76f, 1f, 0.72f);
    public Sprite shipIcon;

    private void OnValidate()
    {
        weaponSlotCount = Mathf.Max(0, weaponSlotCount);
        moduleSlotCount = Mathf.Max(0, moduleSlotCount);
        startingWeapons ??= new List<WeaponDataSO>();
        startingModules ??= new List<ModuleDataSO>();
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
