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
    public Transform ProjectileRoot;
    public IPoolService PoolService;
    public int Wave;
    public Func<string, string> Localize;
    public Action<string, string> LogMessage;
    public Action<ModuleState> UpdateModuleVisual;
    public Action<WeaponDataSO> PlayWeaponShot;
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

            Vector3 direction = enemy.Transform.position - playerPosition;
            float currentDistance = direction.magnitude;
            if (currentDistance > 0.01f)
            {
                Vector3 radialDirection = direction.normalized;
                float radialShift = (enemy.OrbitDistance - currentDistance) * deltaTime;
                enemy.Transform.position += radialDirection * radialShift;
            }

            enemy.OrbitAngle += enemy.OrbitSpeed * deltaTime;
            Vector3 tangent = new Vector3(-Mathf.Sin(enemy.OrbitAngle), Mathf.Cos(enemy.OrbitAngle), 0f);
            enemy.Transform.position += tangent * enemy.DriftSpeed * deltaTime;

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
            if (!weapon.CanFireAt(playerPosition))
            {
                continue;
            }

            if (!weapon.BeginFire())
            {
                continue;
            }

            Vector2 shotDirection = weapon.GetShotDirectionTo(playerPosition);
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

            if (!weaponGroupActive || context.TargetEnemy == null || !context.TargetEnemy.IsAlive())
            {
                continue;
            }

            if (!weapon.CanFireAt(context.TargetEnemy.Transform.position))
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

            Vector2 shotDirection = weapon.GetShotDirectionTo(context.TargetEnemy.Transform.position);
            float shotDamage = weapon.Data != null
                ? Mathf.Max(1f, weapon.Data.damage * context.Player.DamageMultiplier)
                : Mathf.Max(1f, weaponControlModule.Damage * context.Player.DamageMultiplier);

            bool fired = ExecuteWeaponFire(
                context,
                weapon,
                shotDirection,
                shotDamage,
                context.TargetEnemy.Transform.position,
                null);

            if (!fired)
            {
                context.EquipmentState.WeaponTimers[i] = 0f;
            }
        }
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
            fired = SpawnProjectile(context, weapon, ownerFaction, shotDirection, shotDamage);
        }
        else
        {
            fired = FireHitscan(context, weapon, ownerFaction, shotDirection, shotDamage, fallbackTargetPosition, sourceEnemyId);
        }

        if (fired)
        {
            context.PlayWeaponShot?.Invoke(weapon.Data);
        }

        return fired;
    }

    private bool SpawnProjectile(
        CombatUpdateContext context,
        WeaponInstance weapon,
        CombatFaction ownerFaction,
        Vector2 shotDirection,
        float shotDamage)
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
        projectileObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, shotDirection);

        SpriteRenderer renderer = projectileObject.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 6;
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
            projectileDamage: shotDamage,
            projectileSpeed: Mathf.Max(0.5f, weapon.Data.projectileSpeed),
            projectileMaxDistance: maxDistance,
            projectileLifetime: lifetime);

        return true;
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

            if (weapon.OwnerObject != null && collider.transform.root == weapon.OwnerObject.transform.root)
            {
                continue;
            }

            TeamMember teamMember = collider.GetComponentInParent<TeamMember>();
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

            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
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
                EnemyShip hitEnemy = FindEnemyByTransformRoot(context.Enemies, collider.transform.root);
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

    private static EnemyShip FindEnemyByTransformRoot(List<EnemyShip> enemies, Transform root)
    {
        if (enemies == null || root == null)
        {
            return null;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyShip enemy = enemies[i];
            if (enemy != null && enemy.Transform != null && enemy.Transform.root == root)
            {
                return enemy;
            }
        }

        return null;
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
