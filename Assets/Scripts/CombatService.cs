using System;
using System.Collections.Generic;
using SpaceFrontier.Player;
using UnityEngine;

internal sealed class CombatUpdateContext
{
    public PlayerShip Player;
    public List<EnemyShip> Enemies;
    public List<Projectile> Projectiles;
    public List<ModuleState> Modules;
    public EnemyShip TargetEnemy;
    public Transform ProjectileRoot;
    public GameObject ProjectilePrefab;
    public IPoolService PoolService;
    public int Wave;
    public Func<string, string> Localize;
    public Action<string, string> LogMessage;
    public Action<ModuleState> UpdateModuleVisual;
    public Action<Vector3, Vector3, Color> CreateAttackBeam;
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
    private bool levelUpRequested;

    public CombatUpdateResult UpdateFrame(CombatUpdateContext context, float deltaTime)
    {
        levelUpRequested = false;

        UpdateEnemies(context, deltaTime);
        UpdateModules(context, deltaTime);
        UpdateProjectiles(context, deltaTime);

        return new CombatUpdateResult(context.TargetEnemy, levelUpRequested);
    }

    public void ApplyDamage(PlayerStats stats, float amount)
    {
        float remaining = amount;

        if (stats.Shield > 0f)
        {
            float absorbed = Mathf.Min(stats.Shield, remaining);
            stats.Shield -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0f && stats.Armor > 0f)
        {
            float absorbed = Mathf.Min(stats.Armor, remaining);
            stats.Armor -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0f)
        {
            stats.Hull = Mathf.Max(0f, stats.Hull - remaining);
        }
    }

    public bool ApplyDamage(EnemyShip enemy, float amount)
    {
        float remaining = amount;

        if (enemy.Shield > 0f)
        {
            float absorbed = Mathf.Min(enemy.Shield, remaining);
            enemy.Shield -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0f && enemy.Armor > 0f)
        {
            float absorbed = Mathf.Min(enemy.Armor, remaining);
            enemy.Armor -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0f)
        {
            enemy.Hull = Mathf.Max(0f, enemy.Hull - remaining);
        }

        return enemy.Hull <= 0f;
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
            if (enemy.AttackTimer >= enemy.AttackCooldown && toPlayer.magnitude <= 3.8f)
            {
                enemy.AttackTimer = 0f;
                enemy.AttackFlashTimer = 1f;
                ApplyDamage(context.Player.Stats, enemy.Damage);
                context.Player.HitFlashTimer = 1f;
                context.CreateAttackBeam?.Invoke(enemy.Transform.position, context.Player.Transform.position, new Color(1f, 0.32f, 0.24f, 0.95f));
                context.LogMessage?.Invoke(enemy.Id + context.Localize("log_enemy_hits") + Mathf.RoundToInt(enemy.Damage), "hit");
            }
        }
    }

    private void UpdateModules(CombatUpdateContext context, float deltaTime)
    {
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
                module.WeaponTimer += deltaTime;
                while (module.WeaponTimer >= module.RateOfFire)
                {
                    module.WeaponTimer -= module.RateOfFire;
                    if (context.TargetEnemy == null || !context.TargetEnemy.IsAlive())
                    {
                        break;
                    }

                    if (!context.Player.ConsumeCapacitor(module.CapPerShot))
                    {
                        module.Active = false;
                        context.UpdateModuleVisual?.Invoke(module);
                        context.LogMessage?.Invoke(context.Localize("log_cap_dry") + module.Name + context.Localize("log_offline"), "critical");
                        break;
                    }

                    FireWeapon(context, module);
                }
            }
            else
            {
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
        }

        if (!afterburnerActive)
        {
            context.Player.SpeedMultiplier = 1f;
        }
    }

    private void FireWeapon(CombatUpdateContext context, ModuleState module)
    {
        if (context.TargetEnemy == null || !context.TargetEnemy.IsAlive())
        {
            return;
        }

        if (context.PoolService == null)
        {
            return;
        }

        if (context.ProjectilePrefab == null)
        {
            return;
        }

        float distance = Vector3.Distance(context.Player.Transform.position, context.TargetEnemy.Transform.position);
        float hitChance = 1f;
        if (distance > module.OptimalRange)
        {
            float exponent = Mathf.Pow((distance - module.OptimalRange) / Mathf.Max(0.5f, module.FalloffRange), 2f);
            hitChance = Mathf.Pow(0.5f, exponent);
        }

        if (UnityEngine.Random.value > hitChance)
        {
            context.LogMessage?.Invoke(context.Localize("log_shot_missed") + context.TargetEnemy.Id, "miss");
            return;
        }

        GameObject projectileObject = context.PoolService.Get(context.ProjectilePrefab, context.ProjectileRoot);
        if (projectileObject == null)
        {
            return;
        }
        projectileObject.transform.position = context.Player.Transform.position;

        SpriteRenderer renderer = projectileObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = projectileObject.AddComponent<SpriteRenderer>();
        }
        renderer.color = new Color(1f, 0.87f, 0.4f, 1f);
        renderer.sortingOrder = 6;
        projectileObject.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

        context.Projectiles.Add(new Projectile
        {
            Transform = projectileObject.transform,
            Renderer = renderer,
            Target = context.TargetEnemy,
            Damage = module.Damage * context.Player.DamageMultiplier
        });
    }

    private void UpdateProjectiles(CombatUpdateContext context, float deltaTime)
    {
        for (int i = context.Projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = context.Projectiles[i];
            if (projectile.Target == null || !projectile.Target.IsAlive())
            {
                context.PoolService.Return(context.ProjectilePrefab, projectile.Transform.gameObject);
                context.Projectiles.RemoveAt(i);
                continue;
            }

            Vector3 toTarget = projectile.Target.Transform.position - projectile.Transform.position;
            float moveDistance = projectile.Speed * deltaTime;
            if (toTarget.magnitude <= moveDistance + 0.2f)
            {
                bool destroyed = ApplyDamage(projectile.Target, projectile.Damage);
                projectile.Target.HitFlashTimer = 1f;
                context.LogMessage?.Invoke(context.Localize("log_hit") + projectile.Target.Id + context.Localize("log_for") + Mathf.RoundToInt(projectile.Damage), "hit");
                context.PoolService.Return(context.ProjectilePrefab, projectile.Transform.gameObject);
                context.Projectiles.RemoveAt(i);

                if (destroyed)
                {
                    HandleEnemyDestroyed(context, projectile.Target);
                }

                continue;
            }

            projectile.Lifetime += deltaTime;
            float pulse = 1f + Mathf.Sin(projectile.Lifetime * 20f) * 0.15f;
            projectile.Transform.localScale = new Vector3(0.12f * pulse, 0.12f * pulse, 1f);
            projectile.Transform.position += toTarget.normalized * moveDistance;
        }
    }

    private void HandleEnemyDestroyed(CombatUpdateContext context, EnemyShip enemy)
    {
        context.LogMessage?.Invoke(enemy.Id + context.Localize("log_destroyed"), "warning");
        context.Player.AddExperience(40 + context.Wave * 8);
        context.Enemies.Remove(enemy);

        if (enemy.Transform != null)
        {
            UnityEngine.Object.Destroy(enemy.Transform.gameObject);
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
