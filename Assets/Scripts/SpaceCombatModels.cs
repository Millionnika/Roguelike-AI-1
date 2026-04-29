using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal enum ModuleType
{
    Weapon,
    ShieldRep,
    ArmorRep,
    Afterburner
}

internal sealed class EnemyShip
{
    public string Id;
    public string Type;
    public Transform Transform;
    public SpriteRenderer BodyRenderer;
    public SpriteRenderer ShieldRenderer;
    public ShipShieldVisual ShieldVisual;
    public SpriteRenderer TargetRenderer;
    public SpriteRenderer ThrusterRenderer;
    public ShipThrusterEffect ThrusterEffect;
    public float OrbitDistance;
    public float OrbitAngle;
    public float OrbitSpeed;
    public float RetreatDistance;
    public float ReengageDistance;
    public float DistanceResponsiveness;
    public float RetreatSpeedMultiplier;
    public float PrimaryWeaponRange;
    public float HoldDistanceTolerance;
    public float OutOfRangeApproachFactor = 0.95f;
    public float LowDurabilityRetreatThreshold = 0.35f;
    public float LowDurabilityRetreatDistanceBonus = 1.2f;
    public float LowDurabilityRetreatSpeedMultiplier = 1.35f;
    public float StrafeJitterAmplitude = 0.22f;
    public float StrafeJitterFrequency = 1.35f;
    public float StrafeJitterPhase;
    public float AttackCooldown;
    public float AttackTimer;
    public float Damage;
    public int ScoreValue;
    public float DriftSpeed;
    public float HitFlashTimer;
    public float AttackFlashTimer;
    public bool Retreating;
    public Color BaseBodyColor = Color.white;
    public Color BaseShieldColor = Color.white;

    public float MaxShield;
    public float Shield;
    public float MaxArmor;
    public float Armor;
    public float MaxHull;
    public float Hull;
    public ShipDamageReceiver DamageReceiver;
    public TeamMember TeamMember;
    public float WeaponDamageMultiplier = 1f;
    public readonly List<WeaponInstance> WeaponInstances = new List<WeaponInstance>();
    public GameObject Prefab;

    public float ShieldPercent => MaxShield <= 0f ? 0f : Shield / MaxShield;
    public float ArmorPercent => MaxArmor <= 0f ? 0f : Armor / MaxArmor;
    public float HullPercent => MaxHull <= 0f ? 0f : Hull / MaxHull;

    public bool IsAlive()
    {
        return Hull > 0f;
    }

}

internal sealed class ModuleState
{
    public string Name;
    public string KeyLabel;
    public ModuleType Type;
    public bool Active;
    public float WeaponTimer;
    public float CapPerSecond;
    public float CapPerShot;
    public float RateOfFire;
    public float Damage;
    public float OptimalRange;
    public float FalloffRange;
    public float RepairPerSecond;
    public float SpeedBonus;
    public WeaponDataSO WeaponData;

    public Image SlotImage;
    public TMP_Text SlotTitle;
    public TMP_Text SlotKey;
}

public sealed class ShipEquipmentState
{
    public ShipDataSO ShipData;
    public readonly List<WeaponDataSO> InstalledWeapons = new List<WeaponDataSO>();
    public readonly List<ModuleDataSO> InstalledModules = new List<ModuleDataSO>();
    public readonly List<Transform> WeaponMuzzles = new List<Transform>();
    public readonly List<WeaponInstance> RuntimeWeapons = new List<WeaponInstance>();
    public readonly List<float> WeaponTimers = new List<float>();

    public void ConfigureSlots(int weaponSlotCount, int moduleSlotCount)
    {
        int sanitizedWeaponSlots = Mathf.Max(0, weaponSlotCount);
        int sanitizedModuleSlots = Mathf.Max(0, moduleSlotCount);

        Resize(InstalledWeapons, sanitizedWeaponSlots);
        Resize(WeaponMuzzles, sanitizedWeaponSlots);
        Resize(RuntimeWeapons, sanitizedWeaponSlots);
        Resize(WeaponTimers, sanitizedWeaponSlots);
        Resize(InstalledModules, sanitizedModuleSlots);
    }

    private static void Resize<T>(List<T> list, int targetCount)
    {
        while (list.Count < targetCount)
        {
            list.Add(default(T));
        }

        while (list.Count > targetCount)
        {
            list.RemoveAt(list.Count - 1);
        }
    }
}

internal sealed class PerkChoice
{
    public string Label;
    public Action Apply;
}

internal sealed class EnemyRow
{
    public TMP_Text RootText;
    public Image ShieldFill;
    public Image ArmorFill;
    public Image HullFill;
    public RectTransform RootTransform;
    public EnemyShip Enemy;
}

internal sealed class ShipCardView
{
    public RectTransform Rect;
    public Image Background;
    public TMP_Text Title;
    public TMP_Text Stats;
}

internal sealed class UiButtonView
{
    public string Id;
    public RectTransform Rect;
    public Image Background;
    public TMP_Text Label;
}

internal sealed class StarVisual
{
    public Transform Transform;
    public SpriteRenderer Renderer;
    public float BaseAlpha;
    public float TwinkleSpeed;
    public float TwinkleOffset;
}

[Serializable]
public sealed class BackgroundLayerConfig
{
    public GameObject prefab;
    [Range(0f, 1f)] public float parallaxFactor = 0.2f;
    [Min(8f)] public float tileSize = 36f;
    [Range(1, 3)] public int gridRadius = 1;
}
