using System;
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
    public SpriteRenderer TargetRenderer;
    public SpriteRenderer ThrusterRenderer;
    public float OrbitDistance;
    public float OrbitAngle;
    public float OrbitSpeed;
    public float AttackCooldown;
    public float AttackTimer;
    public float Damage;
    public int ScoreValue;
    public float DriftSpeed;
    public float HitFlashTimer;
    public float AttackFlashTimer;
    public Color BaseBodyColor = Color.white;
    public Color BaseShieldColor = Color.white;

    public float MaxShield;
    public float Shield;
    public float MaxArmor;
    public float Armor;
    public float MaxHull;
    public float Hull;
    public WeaponDataSO WeaponData;
    public GameObject Prefab;

    public float ShieldPercent => MaxShield <= 0f ? 0f : Shield / MaxShield;
    public float ArmorPercent => MaxArmor <= 0f ? 0f : Armor / MaxArmor;
    public float HullPercent => MaxHull <= 0f ? 0f : Hull / MaxHull;

    public bool IsAlive()
    {
        return Hull > 0f;
    }

}

internal sealed class Projectile
{
    public Transform Transform;
    public SpriteRenderer Renderer;
    public EnemyShip Target;
    public GameObject Prefab;
    public float Damage;
    public float Speed = 18f;
    public float Lifetime;
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
    public Text SlotTitle;
    public Text SlotKey;
}

internal sealed class PerkChoice
{
    public string Label;
    public Action Apply;
}

internal sealed class EnemyRow
{
    public Text RootText;
    public Image ShieldFill;
    public Image ArmorFill;
    public Image HullFill;
    public RectTransform RootTransform;
    public EnemyShip Enemy;
}

internal sealed class ShipDefinition
{
    public string Name;
    public string Role;
    public string Description;
    public float Speed;
    public float Acceleration;
    public float Drag;
    public float RotationResponsiveness;
    public float MaxShield;
    public float MaxArmor;
    public float MaxHull;
    public float MaxCapacitor;
    public float CapacitorRechargeTime;
    public float DamageMultiplier;
    public float RepairMultiplier;
    public Color AccentColor;
    public Color AuraColor;
}

internal sealed class ShipCardView
{
    public RectTransform Rect;
    public Image Background;
    public Text Title;
    public Text Stats;
}

internal sealed class UiButtonView
{
    public string Id;
    public RectTransform Rect;
    public Image Background;
    public Text Label;
}

internal sealed class AttackBeamEffect
{
    public Transform Transform;
    public SpriteRenderer Renderer;
    public float Lifetime;
    public float Duration;
}

internal sealed class EngineParticle
{
    public Transform Transform;
    public SpriteRenderer Renderer;
    public Vector3 Velocity;
    public float Lifetime;
    public float Duration;
    public Color BaseColor;
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

[Serializable]
public sealed class WaveSpawnSettings
{
    [Min(1)] public int enemiesPerWave = 5;
    [Min(0f)] public float initialWaveDelay = 3f;
    [Min(0f)] public float timeBetweenWaves = 3f;
    [Min(0f)] public float spawnOffscreenMargin = 2f;
}
