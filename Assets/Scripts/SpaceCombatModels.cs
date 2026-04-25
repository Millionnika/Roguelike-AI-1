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

    public float ShieldPercent => MaxShield <= 0f ? 0f : Shield / MaxShield;
    public float ArmorPercent => MaxArmor <= 0f ? 0f : Armor / MaxArmor;
    public float HullPercent => MaxHull <= 0f ? 0f : Hull / MaxHull;

    public bool IsAlive()
    {
        return Hull > 0f;
    }

    public bool TakeDamage(float amount)
    {
        float remaining = amount;

        if (Shield > 0f)
        {
            float absorbed = Mathf.Min(Shield, remaining);
            Shield -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0f && Armor > 0f)
        {
            float absorbed = Mathf.Min(Armor, remaining);
            Armor -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0f)
        {
            Hull = Mathf.Max(0f, Hull - remaining);
        }

        return Hull <= 0f;
    }
}

internal sealed class Projectile
{
    public Transform Transform;
    public SpriteRenderer Renderer;
    public EnemyShip Target;
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
