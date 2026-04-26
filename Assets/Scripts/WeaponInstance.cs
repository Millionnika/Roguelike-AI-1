using System;
using UnityEngine;

public sealed class WeaponInstance
{
    public WeaponDataSO Data { get; }
    public Transform OwnerTransform { get; }
    public Transform MuzzleTransform { get; set; }
    public CombatFaction OwnerFaction { get; }
    public GameObject OwnerObject { get; }

    public float CooldownRemaining { get; private set; }

    public WeaponInstance(
        WeaponDataSO data,
        Transform ownerTransform,
        Transform muzzleTransform,
        CombatFaction ownerFaction,
        GameObject ownerObject)
    {
        Data = data;
        OwnerTransform = ownerTransform;
        MuzzleTransform = muzzleTransform != null ? muzzleTransform : ownerTransform;
        OwnerFaction = ownerFaction;
        OwnerObject = ownerObject;
    }

    public float EffectiveCooldown
    {
        get
        {
            if (Data == null)
            {
                return 0f;
            }

            float cooldown = Data.cooldown > 0f ? Data.cooldown : Data.fireRate;
            return Mathf.Max(0.01f, cooldown);
        }
    }

    public float EffectiveMaxRange
    {
        get
        {
            if (Data == null)
            {
                return 0f;
            }

            if (Data.maxRange > 0f)
            {
                return Data.maxRange;
            }

            if (Data.projectileMaxDistance > 0f)
            {
                return Data.projectileMaxDistance;
            }

            return 6f;
        }
    }

    public void Tick(float deltaTime)
    {
        CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Mathf.Max(0f, deltaTime));
    }

    public bool CanFireAt(Vector3 targetWorldPosition)
    {
        if (Data == null || OwnerTransform == null || CooldownRemaining > 0f)
        {
            return false;
        }

        Vector3 origin = GetMuzzlePosition();
        Vector2 toTarget = (Vector2)(targetWorldPosition - origin);
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float range = EffectiveMaxRange;
        if (range > 0f && toTarget.sqrMagnitude > range * range)
        {
            return false;
        }

        float clampedArc = Mathf.Clamp(Data.firingAngle, 0f, 360f);
        if (clampedArc < 359.9f)
        {
            Vector2 forward = GetForwardDirection();
            if (Vector2.Angle(forward, toTarget.normalized) > clampedArc * 0.5f)
            {
                return false;
            }
        }

        return true;
    }

    public bool BeginFire()
    {
        if (CooldownRemaining > 0f || Data == null)
        {
            return false;
        }

        CooldownRemaining = EffectiveCooldown;
        return true;
    }

    public bool FireAt(Vector3 targetWorldPosition, Func<Vector2, bool> fireAction)
    {
        if (!CanFireAt(targetWorldPosition) || !BeginFire())
        {
            return false;
        }

        Vector2 direction = GetShotDirectionTo(targetWorldPosition);
        return fireAction != null && fireAction(direction);
    }

    public bool FireDirection(Vector2 direction, Func<Vector2, bool> fireAction)
    {
        if (Data == null || CooldownRemaining > 0f || fireAction == null)
        {
            return false;
        }

        if (!BeginFire())
        {
            return false;
        }

        Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : GetForwardDirection();
        return fireAction(ApplySpread(normalized));
    }

    public Vector3 GetMuzzlePosition()
    {
        Transform muzzle = MuzzleTransform != null ? MuzzleTransform : OwnerTransform;
        return muzzle != null ? muzzle.position : Vector3.zero;
    }

    public Vector2 GetForwardDirection()
    {
        Transform muzzle = MuzzleTransform != null ? MuzzleTransform : OwnerTransform;
        if (muzzle == null)
        {
            return Vector2.up;
        }

        Vector2 forward = muzzle.up;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector2.up;
        }

        return forward.normalized;
    }

    public Vector2 GetShotDirectionTo(Vector3 targetWorldPosition)
    {
        Vector2 origin = GetMuzzlePosition();
        Vector2 toTarget = (Vector2)targetWorldPosition - origin;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            toTarget = GetForwardDirection();
        }

        Vector2 baseDirection = toTarget.normalized;
        return ApplySpread(baseDirection);
    }

    public Vector2 ApplySpread(Vector2 baseDirection)
    {
        if (Data == null)
        {
            return baseDirection;
        }

        float spread = Mathf.Max(0f, Data.spreadAngle);
        if (spread <= 0f)
        {
            return baseDirection;
        }

        float halfSpread = spread * 0.5f;
        float offset = UnityEngine.Random.Range(-halfSpread, halfSpread);
        return (Quaternion.Euler(0f, 0f, offset) * baseDirection).normalized;
    }
}
