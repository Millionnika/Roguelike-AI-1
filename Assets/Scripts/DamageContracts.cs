using UnityEngine;

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
    // Shield absorbs first, armor reduces hull damage and degrades under fire.
    public static DamageResolutionResult ResolveDamage(ShipDurabilityState currentState, float incomingDamage)
    {
        ShipDurabilityState state = currentState;
        float damage = Mathf.Max(0f, incomingDamage);

        DamageResolutionResult result = new DamageResolutionResult
        {
            State = state
        };

        if (damage <= 0f || state.Hull <= 0f)
        {
            return result;
        }

        if (state.Shield > 0f)
        {
            float absorbed = Mathf.Min(state.Shield, damage);
            state.Shield -= absorbed;
            damage -= absorbed;
            result.AppliedShieldDamage = absorbed;
        }

        if (damage > 0f)
        {
            float mitigation = state.Armor > 0f
                ? Mathf.Clamp01(state.Armor / (state.Armor + 100f))
                : 0f;

            float hullDamage = damage * (1f - mitigation);
            float armorWear = damage * 0.35f;

            if (state.Armor > 0f && armorWear > 0f)
            {
                float appliedArmor = Mathf.Min(state.Armor, armorWear);
                state.Armor -= appliedArmor;
                result.AppliedArmorDamage = appliedArmor;
            }

            if (hullDamage > 0f)
            {
                float appliedHull = Mathf.Min(state.Hull, hullDamage);
                state.Hull -= appliedHull;
                result.AppliedHullDamage = appliedHull;
            }
        }

        state.Shield = Mathf.Clamp(state.Shield, 0f, Mathf.Max(0f, state.MaxShield));
        state.Armor = Mathf.Clamp(state.Armor, 0f, Mathf.Max(0f, state.MaxArmor));
        state.Hull = Mathf.Clamp(state.Hull, 0f, Mathf.Max(0f, state.MaxHull));

        result.State = state;
        result.Destroyed = state.Hull <= 0f;
        return result;
    }
}
