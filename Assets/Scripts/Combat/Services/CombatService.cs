using System;
using System.Collections.Generic;
using SpaceFrontier.Player;
using UnityEngine;

internal sealed class CombatUpdateContext
{
    public PlayerShip Player;
    public List<EnemyShip> Enemies;
    public List<ModuleState> Modules;
    public ShipEquipmentState EquipmentState;
    public EnemyShip TargetEnemy;
    public bool HasPlayerTarget;
    public Vector3 PlayerTargetPosition;
    public Transform ProjectileRoot;
    public IPoolService PoolService;
    public int Wave;
    public Func<string, string> Localize;
    public Action<string, string> LogMessage;
    public Action<ModuleState> UpdateModuleVisual;
    public Action<WeaponDataSO, Vector3, CombatFaction> PlayWeaponShot;
    public Action<Vector3, EnemyShip> SpawnScrapPickup;
}

internal readonly struct CombatUpdateResult
{
    public CombatUpdateResult(EnemyShip targetEnemy, bool levelUpRequested)
    {
        TargetEnemy = targetEnemy;
        LevelUpRequested = levelUpRequested;
    }

    public EnemyShip TargetEnemy { get; }
    public bool LevelUpRequested { get; }
}

internal sealed class CombatService : ICombatService
{
    private const string WeaponDebugPrefix = "[WeaponDebug]";
    private const float EnemyBaseCollisionRadiusFallback = 0.38f;
    private static readonly Collider2D[] EnemyMovementCollisionBuffer = new Collider2D[16];
    private bool levelUpRequested;

    public CombatUpdateResult UpdateFrame(CombatUpdateContext context, float deltaTime)
    {
        levelUpRequested = false;

        UpdateEnemies(context, deltaTime);
        UpdateModules(context, deltaTime);
        UpdateInstalledWeapons(context, deltaTime);
        CleanupDestroyedEnemies(context);

        return new CombatUpdateResult(context.TargetEnemy, levelUpRequested);
    }

    public void ApplyDamage(PlayerStats stats, float amount)
    {
        ShipDurabilityState state = new ShipDurabilityState
        {
            MaxShield = stats.MaxShield,
            Shield = stats.Shield,
            MaxArmor = stats.MaxArmor,
            Armor = stats.Armor,
            MaxHull = stats.MaxHull,
            Hull = stats.Hull
        };

        DamageResolutionResult result = DamageService.ResolveDamage(state, amount);
        stats.Shield = result.State.Shield;
        stats.Armor = result.State.Armor;
        stats.Hull = result.State.Hull;
    }

    public bool ApplyDamage(EnemyShip enemy, float amount)
    {
        if (enemy == null)
        {
            return false;
        }

        ShipDurabilityState state = new ShipDurabilityState
        {
            MaxShield = enemy.MaxShield,
            Shield = enemy.Shield,
            MaxArmor = enemy.MaxArmor,
            Armor = enemy.Armor,
            MaxHull = enemy.MaxHull,
            Hull = enemy.Hull
        };

        DamageResolutionResult result = DamageService.ResolveDamage(state, amount);
        enemy.Shield = result.State.Shield;
        enemy.Armor = result.State.Armor;
        enemy.Hull = result.State.Hull;
        return result.Destroyed;
    }

    private void UpdateEnemies(CombatUpdateContext context, float deltaTime)
    {
        Vector3 playerPosition = context.Player.Transform.position;

        for (int i = context.Enemies.Count - 1; i >= 0; i--)
        {
            EnemyShip enemy = context.Enemies[i];
            if (!enemy.IsAlive())
            {
                continue;
            }

            UpdateEnemyPosition(enemy, playerPosition, deltaTime);

            Vector3 toPlayer = playerPosition - enemy.Transform.position;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                float lookAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg - 90f;
                enemy.Transform.rotation = Quaternion.Euler(0f, 0f, lookAngle);
            }

            enemy.AttackTimer += deltaTime;
            enemy.AttackFlashTimer = Mathf.Max(0f, enemy.AttackFlashTimer - deltaTime * 4f);
            enemy.HitFlashTimer = Mathf.Max(0f, enemy.HitFlashTimer - deltaTime * 4.5f);

            bool fired = TryFireEnemyWeapons(context, enemy, playerPosition, deltaTime);
            if (fired)
            {
                enemy.AttackFlashTimer = 1f;
                enemy.AttackTimer = 0f;
                continue;
            }

            if (enemy.AttackTimer >= enemy.AttackCooldown && toPlayer.magnitude <= 3.8f)
            {
                enemy.AttackTimer = 0f;
                enemy.AttackFlashTimer = 1f;
                DamageInfo fallbackDamage = BuildDamageInfo(
                    amount: enemy.Damage,
                    sourceFaction: CombatFaction.Enemy,
                    sourceObject: enemy.Transform != null ? enemy.Transform.gameObject : null,
                    weaponData: null,
                    hitPoint: context.Player.Transform.position,
                    direction: toPlayer.normalized);

                ApplyDamageToPlayer(context, fallbackDamage, enemy.Id);
            }
        }
    }

    private static void UpdateEnemyPosition(EnemyShip enemy, Vector3 playerPosition, float deltaTime)
    {
        if (enemy == null || enemy.Transform == null)
        {
            return;
        }

        Vector3 awayFromPlayer = enemy.Transform.position - playerPosition;
        float currentDistance = awayFromPlayer.magnitude;
        if (currentDistance <= 0.01f)
        {
            awayFromPlayer = Quaternion.Euler(0f, 0f, enemy.OrbitAngle * Mathf.Rad2Deg) * Vector3.up;
            currentDistance = 0.01f;
        }

        Vector3 radialDirection = awayFromPlayer / currentDistance;

        float shieldMax = Mathf.Max(0f, enemy.MaxShield);
        float armorMax = Mathf.Max(0f, enemy.MaxArmor);
        float hullMax = Mathf.Max(0.01f, enemy.MaxHull);
        float durabilityNow = Mathf.Max(0f, enemy.Shield) + Mathf.Max(0f, enemy.Armor) + Mathf.Max(0f, enemy.Hull);
        float durabilityMax = Mathf.Max(0.01f, shieldMax + armorMax + hullMax);
        float durabilityPercent = durabilityNow / durabilityMax;
        bool lowDurability = durabilityPercent <= Mathf.Clamp01(enemy.LowDurabilityRetreatThreshold);

        float desiredDistance = Mathf.Max(0.1f, enemy.OrbitDistance);
        if (lowDurability)
        {
            desiredDistance += Mathf.Max(0f, enemy.LowDurabilityRetreatDistanceBonus);
        }

        float retreatDistance = Mathf.Max(0.1f, enemy.RetreatDistance);
        float reengageDistance = Mathf.Max(retreatDistance + 0.1f, enemy.ReengageDistance);
        if (currentDistance < retreatDistance || (lowDurability && currentDistance < desiredDistance))
        {
            enemy.Retreating = true;
        }
        else if (currentDistance > reengageDistance)
        {
            enemy.Retreating = false;
        }

        float holdTolerance = Mathf.Max(0.05f, enemy.HoldDistanceTolerance);
        float weaponRange = Mathf.Max(0.1f, enemy.PrimaryWeaponRange);
        float mustApproachDistance = weaponRange * Mathf.Clamp(enemy.OutOfRangeApproachFactor, 0.5f, 1.2f);
        bool outOfFireRange = currentDistance > mustApproachDistance;

        float responsiveness = Mathf.Max(0.1f, enemy.DistanceResponsiveness);
        float radialSpeed = 0f;

        if (enemy.Retreating)
        {
            float retreatBoost = lowDurability ? Mathf.Max(1f, enemy.LowDurabilityRetreatSpeedMultiplier) : 1f;
            float panicSpeed = enemy.DriftSpeed * Mathf.Max(1f, enemy.RetreatSpeedMultiplier) * retreatBoost;
            float retreatError = Mathf.Max(0f, desiredDistance - currentDistance);
            radialSpeed = Mathf.Max(panicSpeed * 0.55f, retreatError * responsiveness);
        }
        else if (outOfFireRange)
        {
            float approachError = currentDistance - desiredDistance;
            radialSpeed = -Mathf.Clamp(
                approachError * responsiveness,
                enemy.DriftSpeed * 0.35f,
                enemy.DriftSpeed * 1.25f);
        }
        else if (currentDistance < desiredDistance - holdTolerance)
        {
            float closeError = (desiredDistance - holdTolerance) - currentDistance;
            radialSpeed = Mathf.Clamp(
                closeError * responsiveness,
                enemy.DriftSpeed * 0.2f,
                enemy.DriftSpeed);
        }

        float maxRetreat = enemy.DriftSpeed * Mathf.Max(1.2f, enemy.RetreatSpeedMultiplier) * (lowDurability ? Mathf.Max(1f, enemy.LowDurabilityRetreatSpeedMultiplier) : 1f);
        float maxApproach = enemy.DriftSpeed * 1.25f;
        radialSpeed = Mathf.Clamp(radialSpeed, -maxApproach, maxRetreat);
        MoveEnemyWithBaseCollision(enemy, radialDirection * radialSpeed * deltaTime);

        float jitterAmplitude = Mathf.Max(0f, enemy.StrafeJitterAmplitude);
        float jitterFrequency = Mathf.Max(0.1f, enemy.StrafeJitterFrequency);
        float jitter = Mathf.Sin(Time.time * jitterFrequency + enemy.StrafeJitterPhase) * jitterAmplitude;
        enemy.OrbitAngle += (enemy.OrbitSpeed + jitter * 0.45f) * deltaTime;
        Vector3 tangent = new Vector3(-Mathf.Sin(enemy.OrbitAngle), Mathf.Cos(enemy.OrbitAngle), 0f);
        float tangentScale = enemy.Retreating ? 0.45f : 1f + jitter * 0.3f;
        MoveEnemyWithBaseCollision(enemy, tangent * enemy.DriftSpeed * Mathf.Max(0.2f, tangentScale) * deltaTime);
    }

    private static void MoveEnemyWithBaseCollision(EnemyShip enemy, Vector3 delta)
    {
        if (enemy == null || enemy.Transform == null || delta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Vector3 current = enemy.Transform.position;
        Vector3 target = current + delta;
        float radius = ResolveCollisionRadius(enemy.Transform);

        if (!IsBlockedByBase(target, radius, enemy.Transform))
        {
            enemy.Transform.position = target;
            return;
        }

        Vector3 xOnly = current + new Vector3(delta.x, 0f, 0f);
        Vector3 yOnly = current + new Vector3(0f, delta.y, 0f);
        bool xFree = !IsBlockedByBase(xOnly, radius, enemy.Transform);
        bool yFree = !IsBlockedByBase(yOnly, radius, enemy.Transform);

        if (xFree && yFree)
        {
            enemy.Transform.position = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? xOnly : yOnly;
            return;
        }

        if (xFree)
        {
            enemy.Transform.position = xOnly;
            return;
        }

        if (yFree)
        {
            enemy.Transform.position = yOnly;
        }
    }

    private static float ResolveCollisionRadius(Transform root)
    {
        if (root == null)
        {
            return EnemyBaseCollisionRadiusFallback;
        }

        Collider2D collider = root.GetComponentInChildren<Collider2D>();
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y);
            if (extent > 0.01f)
            {
                return extent * 0.9f;
            }
        }

        return EnemyBaseCollisionRadiusFallback;
    }

    private static bool IsBlockedByBase(Vector3 position, float radius, Transform selfRoot)
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(position, Mathf.Max(0.1f, radius), EnemyMovementCollisionBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = EnemyMovementCollisionBuffer[i];
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            Transform colliderRoot = collider.transform.root;
            if (selfRoot != null && colliderRoot == selfRoot)
            {
                continue;
            }

            EnemyBaseLair baseLair = collider.GetComponentInParent<EnemyBaseLair>();
            if (baseLair != null && baseLair.IsAlive)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFireEnemyWeapons(CombatUpdateContext context, EnemyShip enemy, Vector3 playerPosition, float deltaTime)
    {
        if (enemy.WeaponInstances == null || enemy.WeaponInstances.Count == 0)
        {
            return false;
        }

        bool firedAny = false;
        for (int i = 0; i < enemy.WeaponInstances.Count; i++)
        {
            WeaponInstance weapon = enemy.WeaponInstances[i];
            if (weapon == null)
            {
                continue;
            }

            weapon.Tick(deltaTime);
            AimWeaponAt(weapon, playerPosition, deltaTime);
            if (!weapon.CanFireAt(playerPosition))
            {
                continue;
            }

            if (!weapon.BeginFire())
            {
                continue;
            }

            Vector2 shotDirection = weapon.ApplySpread(weapon.GetForwardDirection());
            float shotDamage = weapon.Data != null
                ? Mathf.Max(1f, weapon.Data.damage * Mathf.Max(0.1f, enemy.WeaponDamageMultiplier))
                : Mathf.Max(1f, enemy.Damage);

            bool fired = ExecuteWeaponFire(
                context,
                weapon,
                shotDirection,
                shotDamage,
                playerPosition,
                enemy.Id);

            firedAny |= fired;
        }

        return firedAny;
    }

    private void UpdateModules(CombatUpdateContext context, float deltaTime)
    {
        if (context.Modules == null)
        {
            context.Player.SpeedMultiplier = 1f;
            return;
        }

        bool afterburnerActive = false;

        for (int i = 0; i < context.Modules.Count; i++)
        {
            ModuleState module = context.Modules[i];
            if (!module.Active)
            {
                continue;
            }

            if (module.Type == ModuleType.Weapon)
            {
                continue;
            }

            float capUse = module.CapPerSecond * deltaTime;
            if (!context.Player.ConsumeCapacitor(capUse))
            {
                module.Active = false;
                context.UpdateModuleVisual?.Invoke(module);
                context.LogMessage?.Invoke(context.Localize("log_cap_insufficient") + module.Name, "warning");
                continue;
            }

            if (module.Type == ModuleType.ShieldRep)
            {
                context.Player.HealShield(module.RepairPerSecond * context.Player.RepairMultiplier * deltaTime);
            }
            else if (module.Type == ModuleType.ArmorRep)
            {
                context.Player.HealArmor(module.RepairPerSecond * context.Player.RepairMultiplier * deltaTime);
            }
            else if (module.Type == ModuleType.Afterburner)
            {
                afterburnerActive = true;
                context.Player.SpeedMultiplier = module.SpeedBonus;
            }
        }

        if (!afterburnerActive)
        {
            context.Player.SpeedMultiplier = 1f;
        }
    }

    private void UpdateInstalledWeapons(CombatUpdateContext context, float deltaTime)
    {
        if (context.EquipmentState == null || context.EquipmentState.RuntimeWeapons.Count == 0)
        {
            return;
        }

        ModuleState weaponControlModule = GetWeaponControlModule(context.Modules);
        bool weaponGroupActive = weaponControlModule != null && weaponControlModule.Active;

        for (int i = 0; i < context.EquipmentState.RuntimeWeapons.Count; i++)
        {
            WeaponInstance weapon = context.EquipmentState.RuntimeWeapons[i];
            if (weapon == null)
            {
                if (i < context.EquipmentState.WeaponTimers.Count)
                {
                    context.EquipmentState.WeaponTimers[i] = 0f;
                }
                continue;
            }

            weapon.Tick(deltaTime);
            if (i < context.EquipmentState.WeaponTimers.Count)
            {
                context.EquipmentState.WeaponTimers[i] = weapon.CooldownRemaining;
            }

            if (!weaponGroupActive || !context.HasPlayerTarget)
            {
                continue;
            }

            Vector3 targetPosition = context.PlayerTargetPosition;
            AimWeaponAt(weapon, targetPosition, deltaTime);
            if (!weapon.CanFireAt(targetPosition))
            {
                continue;
            }

            float capacitorPerShot = Mathf.Max(0f, weapon.Data != null ? weapon.Data.capacitorPerShot : 0f);
            if (capacitorPerShot > 0f && !context.Player.ConsumeCapacitor(capacitorPerShot))
            {
                weaponControlModule.Active = false;
                context.UpdateModuleVisual?.Invoke(weaponControlModule);
                context.LogMessage?.Invoke(context.Localize("log_cap_dry") + weaponControlModule.Name + context.Localize("log_offline"), "critical");
                continue;
            }

            if (!weapon.BeginFire())
            {
                continue;
            }

            Vector2 shotDirection = weapon.ApplySpread(weapon.GetForwardDirection());
            float shotDamage = weapon.Data != null
                ? Mathf.Max(1f, weapon.Data.damage * context.Player.DamageMultiplier)
                : Mathf.Max(1f, weaponControlModule.Damage * context.Player.DamageMultiplier);

            bool fired = ExecuteWeaponFire(
                context,
                weapon,
                shotDirection,
                shotDamage,
                targetPosition,
                null);

            if (!fired)
            {
                context.EquipmentState.WeaponTimers[i] = 0f;
            }
        }
    }

    private static void AimWeaponAt(WeaponInstance weapon, Vector3 targetWorldPosition, float deltaTime)
    {
        if (weapon == null || weapon.Data == null || weapon.MuzzleTransform == null)
        {
            return;
        }

        Transform mount = weapon.MuzzleTransform.parent != null ? weapon.MuzzleTransform.parent : weapon.MuzzleTransform;
        Transform arcRoot = mount.parent != null ? mount.parent : weapon.OwnerTransform;
        if (mount == null || arcRoot == null)
        {
            return;
        }

        Vector2 origin = weapon.GetMuzzlePosition();
        Vector2 toTarget = (Vector2)targetWorldPosition - origin;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 centerForward = weapon.GetArcCenterDirection();

        float arc = Mathf.Clamp(weapon.Data.firingAngle, 0f, 360f);
        float signedAngle = Vector2.SignedAngle(centerForward, toTarget.normalized);
        if (arc < 359.9f)
        {
            signedAngle = Mathf.Clamp(signedAngle, -arc * 0.5f, arc * 0.5f);
        }

        Vector2 aimedForward = Quaternion.Euler(0f, 0f, signedAngle) * centerForward;
        float targetAngle = Mathf.Atan2(aimedForward.y, aimedForward.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        float turnSpeed = Mathf.Max(1f, weapon.Data.aimTurnSpeed);
        mount.rotation = Quaternion.RotateTowards(mount.rotation, targetRotation, turnSpeed * Mathf.Max(0f, deltaTime));
    }

    private bool ExecuteWeaponFire(
        CombatUpdateContext context,
        WeaponInstance weapon,
        Vector2 shotDirection,
        float shotDamage,
        Vector3 fallbackTargetPosition,
        string sourceEnemyId)
    {
        if (weapon == null || weapon.Data == null)
        {
            return false;
        }

        CombatFaction ownerFaction = ResolveOwnerFaction(weapon);
        FireMode mode = weapon.Data.fireMode;
        bool useProjectile = mode == FireMode.Projectile || mode == FireMode.Missile;
        bool fired;

        Debug.Log(
            $"{WeaponDebugPrefix} Fire request: owner={(weapon.OwnerObject != null ? weapon.OwnerObject.name : "None")} team={ownerFaction} weapon={weapon.Data.name} mode={mode}");

        if (useProjectile)
        {
            fired = SpawnProjectile(context, weapon, ownerFaction, shotDirection, shotDamage, fallbackTargetPosition);
        }
        else
        {
            fired = FireHitscan(context, weapon, ownerFaction, shotDirection, shotDamage, fallbackTargetPosition, sourceEnemyId);
        }

        if (fired)
        {
            context.PlayWeaponShot?.Invoke(weapon.Data, weapon.GetMuzzlePosition(), ownerFaction);
        }

        return fired;
    }

    private bool SpawnProjectile(
        CombatUpdateContext context,
        WeaponInstance weapon,
        CombatFaction ownerFaction,
        Vector2 shotDirection,
        float shotDamage,
        Vector3 fallbackTargetPosition)
    {
        if (context.PoolService == null || context.ProjectileRoot == null)
        {
            return false;
        }

        GameObject projectilePrefab = weapon.Data.projectilePrefab;
        if (projectilePrefab == null)
        {
            return false;
        }

        GameObject projectileObject = context.PoolService.Get(projectilePrefab, context.ProjectileRoot);
        if (projectileObject == null)
        {
            return false;
        }

        Vector3 origin = weapon.GetMuzzlePosition();
        origin.z = 0f;
        projectileObject.transform.position = origin;
        projectileObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, shotDirection) *
                                              Quaternion.Euler(0f, 0f, weapon.Data.projectileRotationOffset);
        projectileObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, weapon.Data.projectileVisualScale);

        SpriteRenderer renderer = projectileObject.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 6;
        }

        if (weapon.Data.projectileTrailPrefab == null)
        {
            ConfigureProjectileTrail(projectileObject, ownerFaction);
        }
        else
        {
            DisableFallbackProjectileTrail(projectileObject);
        }

        ProjectileBehaviour behaviour = projectileObject.GetComponent<ProjectileBehaviour>();
        if (behaviour == null)
        {
            behaviour = projectileObject.AddComponent<ProjectileBehaviour>();
        }

        float maxDistance = weapon.Data.projectileMaxDistance > 0f ? weapon.Data.projectileMaxDistance : weapon.EffectiveMaxRange;
        float lifetime = weapon.Data.projectileLifetime;
        if (lifetime <= 0f && maxDistance > 0f && weapon.Data.projectileSpeed > 0f)
        {
            lifetime = maxDistance / weapon.Data.projectileSpeed;
        }

        behaviour.Initialize(
            runtimePoolService: context.PoolService,
            sourcePrefab: projectilePrefab,
            sourceOwner: weapon.OwnerObject,
            sourceFaction: ownerFaction,
            sourceWeaponData: weapon.Data,
            startDirection: shotDirection,
            preferredTargetPoint: fallbackTargetPosition,
            projectileDamage: shotDamage,
            projectileSpeed: Mathf.Max(0.5f, weapon.Data.projectileSpeed),
            projectileMaxDistance: maxDistance,
            projectileLifetime: lifetime,
            sourceImpactVfxPrefab: weapon.Data.impactVfxPrefab,
            sourceImpactVfxLifetime: weapon.Data.impactVfxLifetime,
            sourceImpactVfxScale: weapon.Data.impactVfxScale,
            sourceProjectileTrailPrefab: weapon.Data.projectileTrailPrefab,
            sourceProjectileTrailScale: weapon.Data.projectileTrailScale,
            sourceDetachTrailOnDespawn: weapon.Data.detachTrailOnDespawn,
            sourceDetachedTrailLifetime: weapon.Data.detachedTrailLifetime);

        CombatLayerUtility.ApplyProjectileLayer(projectileObject, ownerFaction);
        Debug.Log(
            $"{WeaponDebugPrefix} Projectile spawned: muzzle={(weapon.MuzzleTransform != null ? weapon.MuzzleTransform.name : "None")} position={origin} scale={projectileObject.transform.localScale.x:0.###}");
        return true;
    }

    private static void ConfigureProjectileTrail(GameObject projectileObject, CombatFaction ownerFaction)
    {
        if (projectileObject == null)
        {
            return;
        }

        TrailRenderer trail = projectileObject.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = projectileObject.AddComponent<TrailRenderer>();
        }

        trail.time = 0.28f;
        trail.minVertexDistance = 0.02f;
        trail.widthMultiplier = 0.08f;
        trail.autodestruct = false;
        trail.enabled = true;
        trail.emitting = true;
        trail.sortingOrder = 5;
        trail.material = GetProjectileTrailMaterial();
        Color start = ownerFaction == CombatFaction.Player
            ? new Color(0.35f, 0.9f, 1f, 0.95f)
            : new Color(1f, 0.38f, 0.24f, 0.95f);
        Color end = new Color(start.r, start.g, start.b, 0f);
        trail.startColor = start;
        trail.endColor = end;
        trail.Clear();
    }

    private static void DisableFallbackProjectileTrail(GameObject projectileObject)
    {
        if (projectileObject == null)
        {
            return;
        }

        TrailRenderer trail = projectileObject.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            return;
        }

        bool looksLikeGeneratedFallback =
            trail.sharedMaterial == GetProjectileTrailMaterial() ||
            (Mathf.Approximately(trail.time, 0.28f) &&
             Mathf.Approximately(trail.widthMultiplier, 0.08f) &&
             trail.sortingOrder == 5);
        if (!looksLikeGeneratedFallback)
        {
            return;
        }

        trail.emitting = false;
        trail.enabled = false;
        trail.Clear();
    }

    private static Material projectileTrailMaterial;

    private static Material GetProjectileTrailMaterial()
    {
        if (projectileTrailMaterial != null)
        {
            return projectileTrailMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        projectileTrailMaterial = shader != null ? new Material(shader) : null;
        return projectileTrailMaterial;
    }

    private bool FireHitscan(
        CombatUpdateContext context,
        WeaponInstance weapon,
        CombatFaction ownerFaction,
        Vector2 shotDirection,
        float shotDamage,
        Vector3 fallbackTargetPosition,
        string sourceEnemyId)
    {
        Vector3 origin3 = weapon.GetMuzzlePosition();
        origin3.z = 0f;
        Vector2 origin = origin3;

        float range = weapon.EffectiveMaxRange;
        if (range <= 0f)
        {
            range = 100f;
        }

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, shotDirection, range, Physics2D.DefaultRaycastLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i].collider;
            if (collider == null)
            {
                continue;
            }

            if (IsOwnedCollider(collider, weapon.OwnerObject))
            {
                Debug.Log($"{WeaponDebugPrefix} Hitscan ignored {collider.name}: owner collider.");
                continue;
            }

            TeamMember teamMember = ResolveTeamMember(collider);
            if (teamMember == null)
            {
                Debug.Log($"{WeaponDebugPrefix} Hitscan ignored {collider.name}: no TeamMember.");
                continue;
            }

            if (ownerFaction != CombatFaction.Neutral && teamMember.Faction == ownerFaction)
            {
                Debug.Log($"{WeaponDebugPrefix} Hitscan ignored {collider.name}: same team {ownerFaction}.");
                continue;
            }

            IDamageable damageable = ResolveDamageable(collider);
            if (damageable == null)
            {
                Debug.Log($"{WeaponDebugPrefix} Hitscan ignored {collider.name}: no IDamageable.");
                continue;
            }

            DamageInfo info = BuildDamageInfo(
                amount: shotDamage,
                sourceFaction: ownerFaction,
                sourceObject: weapon.OwnerObject,
                weaponData: weapon.Data,
                hitPoint: hits[i].point,
                direction: shotDirection);

            damageable.TakeDamage(info);
            Debug.Log($"{WeaponDebugPrefix} Hitscan damage applied to {collider.name}: {shotDamage:0.##}");

            if (ownerFaction == CombatFaction.Enemy)
            {
                context.Player.HitFlashTimer = 1f;
                string sourceId = string.IsNullOrEmpty(sourceEnemyId) ? "Enemy" : sourceEnemyId;
                context.LogMessage?.Invoke(sourceId + context.Localize("log_enemy_hits") + Mathf.RoundToInt(shotDamage), "hit");
            }
            else
            {
                EnemyShip hitEnemy = FindEnemyByTeamMember(context.Enemies, teamMember);
                if (hitEnemy != null)
                {
                    hitEnemy.HitFlashTimer = 1f;
                    context.LogMessage?.Invoke(context.Localize("log_hit") + hitEnemy.Id + context.Localize("log_for") + Mathf.RoundToInt(shotDamage), "hit");
                }
                else
                {
                    context.LogMessage?.Invoke(context.Localize("log_hit") + Mathf.RoundToInt(shotDamage), "hit");
                }
            }

            return true;
        }

        if (ownerFaction == CombatFaction.Enemy)
        {
            return false;
        }

        context.LogMessage?.Invoke(context.Localize("log_shot_missed"), "miss");
        return false;
    }

    private static EnemyShip FindEnemyByTeamMember(List<EnemyShip> enemies, TeamMember teamMember)
    {
        if (enemies == null || teamMember == null)
        {
            return null;
        }

        Transform teamTransform = teamMember.transform;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyShip enemy = enemies[i];
            if (enemy == null || enemy.Transform == null)
            {
                continue;
            }

            if (teamTransform == enemy.Transform || teamTransform.IsChildOf(enemy.Transform))
            {
                return enemy;
            }
        }

        return null;
    }

    private static bool IsOwnedCollider(Collider2D collider, GameObject ownerObject)
    {
        if (collider == null || ownerObject == null)
        {
            return false;
        }

        Transform ownerTransform = ownerObject.transform;
        return collider.transform == ownerTransform || collider.transform.IsChildOf(ownerTransform);
    }

    private static TeamMember ResolveTeamMember(Collider2D collider)
    {
        if (collider == null)
        {
            return null;
        }

        Transform hierarchyRoot = collider.transform.root;
        TeamMember rootMember = hierarchyRoot != null ? hierarchyRoot.GetComponent<TeamMember>() : null;
        return rootMember != null ? rootMember : collider.GetComponentInParent<TeamMember>();
    }

    private static IDamageable ResolveDamageable(Collider2D collider)
    {
        if (collider == null)
        {
            return null;
        }

        Transform hierarchyRoot = collider.transform.root;
        IDamageable rootDamageable = hierarchyRoot != null ? hierarchyRoot.GetComponent<IDamageable>() : null;
        return rootDamageable != null ? rootDamageable : collider.GetComponentInParent<IDamageable>();
    }

    private static DamageInfo BuildDamageInfo(
        float amount,
        CombatFaction sourceFaction,
        GameObject sourceObject,
        WeaponDataSO weaponData,
        Vector2 hitPoint,
        Vector2 direction)
    {
        return new DamageInfo
        {
            Amount = Mathf.Max(0f, amount),
            SourceFaction = sourceFaction,
            Source = sourceObject,
            WeaponData = weaponData,
            HitPoint = hitPoint,
            Direction = direction
        };
    }

    private void ApplyDamageToPlayer(CombatUpdateContext context, DamageInfo info, string enemyId)
    {
        if (context.Player.DamageReceiver != null)
        {
            context.Player.DamageReceiver.TakeDamage(info);
        }
        else
        {
            ApplyDamage(context.Player.Stats, info.Amount);
        }

        context.Player.HitFlashTimer = 1f;
        context.LogMessage?.Invoke(enemyId + context.Localize("log_enemy_hits") + Mathf.RoundToInt(info.Amount), "hit");
    }

    private static ModuleState GetWeaponControlModule(List<ModuleState> modules)
    {
        if (modules == null)
        {
            return null;
        }

        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i].Type == ModuleType.Weapon)
            {
                return modules[i];
            }
        }

        return null;
    }

    private static CombatFaction ResolveOwnerFaction(WeaponInstance weapon)
    {
        if (weapon?.OwnerObject != null)
        {
            TeamMember member = weapon.OwnerObject.GetComponentInParent<TeamMember>();
            if (member != null)
            {
                return member.Faction;
            }
        }

        return weapon != null ? weapon.OwnerFaction : CombatFaction.Neutral;
    }

    private void CleanupDestroyedEnemies(CombatUpdateContext context)
    {
        for (int i = context.Enemies.Count - 1; i >= 0; i--)
        {
            EnemyShip enemy = context.Enemies[i];
            if (enemy == null || enemy.IsAlive())
            {
                continue;
            }

            HandleEnemyDestroyed(context, enemy);
        }
    }

    private void HandleEnemyDestroyed(CombatUpdateContext context, EnemyShip enemy)
    {
        context.LogMessage?.Invoke(enemy.Id + context.Localize("log_destroyed"), "warning");
        int reward = enemy.ScoreValue > 0 ? enemy.ScoreValue : 40;
        context.Player.AddExperience(reward + context.Wave * 8);
        if (enemy.Transform != null)
        {
            context.SpawnScrapPickup?.Invoke(enemy.Transform.position, enemy);
        }
        context.Enemies.Remove(enemy);

        if (enemy.Transform != null)
        {
            if (enemy.Prefab != null && context.PoolService != null)
            {
                context.PoolService.Return(enemy.Prefab, enemy.Transform.gameObject);
            }
            else
            {
                UnityEngine.Object.Destroy(enemy.Transform.gameObject);
            }
        }

        if (context.TargetEnemy == enemy)
        {
            context.TargetEnemy = context.Enemies.Count > 0 ? context.Enemies[0] : null;
        }

        if (context.Player.Experience >= context.Player.ExperienceToNext)
        {
            levelUpRequested = true;
        }
    }
}
