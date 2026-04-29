using UnityEngine;
using System.Collections.Generic;

public enum CombatFaction
{
    Neutral = 0,
    Player = 1,
    Enemy = 2
}

public enum FireMode
{
    Projectile = 0,
    Hitscan = 1,
    Beam = 2,
    Missile = 3
}

public struct DamageInfo
{
    public float Amount;
    public CombatFaction SourceFaction;
    public GameObject Source;
    public WeaponDataSO WeaponData;
    public Vector2 HitPoint;
    public Vector2 Direction;
}

public enum DamageLayerType
{
    Shield = 0,
    Armor = 1,
    Hull = 2
}

[System.Serializable]
public struct DamageLayerShare
{
    [Tooltip("Тип слоя защиты, которому назначается процент урона.")]
    public DamageLayerType layer;
    [Tooltip("Доля урона в процентах для указанного слоя.")]
    [Range(0f, 100f)] public float percent;
}

public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}

public struct ShipDurabilityState
{
    public float MaxShield;
    public float Shield;
    public float MaxArmor;
    public float Armor;
    public float MaxHull;
    public float Hull;
}

public struct DamageResolutionResult
{
    public ShipDurabilityState State;
    public float AppliedShieldDamage;
    public float AppliedArmorDamage;
    public float AppliedHullDamage;
    public bool Destroyed;
}

public static class DamageService
{
    private static readonly DamageLayerType[] LayerPriority =
    {
        DamageLayerType.Shield,
        DamageLayerType.Armor,
        DamageLayerType.Hull
    };

    public static DamageResolutionResult ResolveDamage(ShipDurabilityState currentState, float incomingDamage)
    {
        DamageLayerShare[] fallbackShares =
        {
            new DamageLayerShare { layer = DamageLayerType.Shield, percent = 100f },
            new DamageLayerShare { layer = DamageLayerType.Armor, percent = 0f },
            new DamageLayerShare { layer = DamageLayerType.Hull, percent = 0f }
        };
        return ApplyDamage(currentState, incomingDamage, fallbackShares);
    }

    public static DamageResolutionResult ResolveDamage(ShipDurabilityState currentState, DamageInfo info)
    {
        DamageDistributionProfileSO profile = info.WeaponData != null ? info.WeaponData.damageProfile : null;
        if (profile == null || profile.shares == null || profile.shares.Count == 0)
        {
            return ResolveDamage(currentState, info.Amount);
        }

        return ApplyDamage(currentState, info.Amount, profile.shares);
    }

    public static DamageResolutionResult ApplyDamage(
        ShipDurabilityState currentState,
        float totalDamage,
        IReadOnlyList<DamageLayerShare> distribution)
    {
        ShipDurabilityState state = currentState;
        float incomingDamage = Mathf.Max(0f, totalDamage);
        DamageResolutionResult result = new DamageResolutionResult { State = state };

        if (incomingDamage <= 0f || state.Hull <= 0f)
        {
            return result;
        }

        float normalizedSum = 0f;
        if (distribution != null)
        {
            for (int i = 0; i < distribution.Count; i++)
            {
                normalizedSum += Mathf.Max(0f, distribution[i].percent);
            }
        }

        if (normalizedSum <= 0.0001f)
        {
            normalizedSum = 100f;
            DamageLayerShare[] fallbackShares =
            {
                new DamageLayerShare { layer = DamageLayerType.Shield, percent = 100f },
                new DamageLayerShare { layer = DamageLayerType.Armor, percent = 0f },
                new DamageLayerShare { layer = DamageLayerType.Hull, percent = 0f }
            };
            distribution = fallbackShares;
        }

        for (int i = 0; i < distribution.Count; i++)
        {
            DamageLayerShare share = distribution[i];
            float sharePercent = Mathf.Max(0f, share.percent) / normalizedSum;
            float shareDamage = incomingDamage * sharePercent;
            ApplyShareWithOverflow(ref state, ref result, share.layer, shareDamage);
        }

        ClampState(ref state);
        result.State = state;
        result.Destroyed = state.Hull <= 0f;
        return result;
    }

    private static void ApplyShareWithOverflow(
        ref ShipDurabilityState state,
        ref DamageResolutionResult result,
        DamageLayerType initialLayer,
        float shareDamage)
    {
        float remaining = Mathf.Max(0f, shareDamage);
        int startIndex = GetPriorityIndex(initialLayer);

        for (int i = startIndex; i < LayerPriority.Length && remaining > 0.0001f; i++)
        {
            DamageLayerType layer = LayerPriority[i];
            float available = GetLayerValue(state, layer);
            if (available <= 0f)
            {
                continue;
            }

            float applied = Mathf.Min(available, remaining);
            SetLayerValue(ref state, layer, available - applied);
            AccumulateApplied(ref result, layer, applied);
            remaining -= applied;
        }
    }

    private static int GetPriorityIndex(DamageLayerType layer)
    {
        for (int i = 0; i < LayerPriority.Length; i++)
        {
            if (LayerPriority[i] == layer)
            {
                return i;
            }
        }

        return 0;
    }

    private static float GetLayerValue(in ShipDurabilityState state, DamageLayerType layer)
    {
        switch (layer)
        {
            case DamageLayerType.Shield: return state.Shield;
            case DamageLayerType.Armor: return state.Armor;
            default: return state.Hull;
        }
    }

    private static void SetLayerValue(ref ShipDurabilityState state, DamageLayerType layer, float value)
    {
        float clamped = Mathf.Max(0f, value);
        switch (layer)
        {
            case DamageLayerType.Shield:
                state.Shield = clamped;
                break;
            case DamageLayerType.Armor:
                state.Armor = clamped;
                break;
            default:
                state.Hull = clamped;
                break;
        }
    }

    private static void AccumulateApplied(ref DamageResolutionResult result, DamageLayerType layer, float value)
    {
        if (value <= 0f)
        {
            return;
        }

        switch (layer)
        {
            case DamageLayerType.Shield:
                result.AppliedShieldDamage += value;
                break;
            case DamageLayerType.Armor:
                result.AppliedArmorDamage += value;
                break;
            default:
                result.AppliedHullDamage += value;
                break;
        }
    }

    private static void ClampState(ref ShipDurabilityState state)
    {
        state.Shield = Mathf.Clamp(state.Shield, 0f, Mathf.Max(0f, state.MaxShield));
        state.Armor = Mathf.Clamp(state.Armor, 0f, Mathf.Max(0f, state.MaxArmor));
        state.Hull = Mathf.Clamp(state.Hull, 0f, Mathf.Max(0f, state.MaxHull));
    }
}
