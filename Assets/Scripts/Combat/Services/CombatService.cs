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
    private const float MinAttackPhaseDuration = 1.4f;
    private const float MaxFlankingPhaseDuration = 3.2f;
    private const float AttackToFlankChancePerSecond = 0.45f;
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

            // РћР±РЅРѕРІР»СЏРµРј РїРѕР·РёС†РёСЋ Рё РїРѕР»СѓС‡Р°РµРј С„Р°РєС‚РёС‡РµСЃРєРѕРµ РїРµСЂРµРјРµС‰РµРЅРёРµ
            UpdateEnemyPosition(enemy, playerPosition, deltaTime, out Vector3 appliedDelta);

            // РџРµСЂРµС…РѕРґ РІ СЂРµР¶РёРј РѕР±Р»С‘С‚Р° С‚РѕР»СЊРєРѕ РµСЃР»Рё РІСЂР°Рі РЅР°С…РѕРґРёС‚СЃСЏ РЅР° Р¶РµР»Р°РµРјРѕР№ РґРёСЃС‚Р°РЅС†РёРё (РІ РїСЂРµРґРµР»Р°С… РґРѕРїСѓСЃРєР°)
            float distanceToPlayer = Vector3.Distance(enemy.Transform.position, playerPosition);
            float orbitDistance = Mathf.Max(0.1f, enemy.OrbitDistance);
            float tolerance = Mathf.Max(0.05f, enemy.HoldDistanceTolerance);
            bool isAtOptimalDistance = Mathf.Abs(distanceToPlayer - orbitDistance) <= tolerance;
            enemy.PhaseTimer += deltaTime;

            // РЈРїСЂР°РІР»РµРЅРёРµ С„Р°Р·Р°РјРё
            if (enemy.CombatPhase == EnemyCombatPhase.Attack)
            {
                // Р•СЃР»Рё РЅР° РѕРїС‚РёРјР°Р»СЊРЅРѕР№ РґРёСЃС‚Р°РЅС†РёРё Рё РЅРµ РѕС‚СЃС‚СѓРїР°РµРј, СЃ РІРµСЂРѕСЏС‚РЅРѕСЃС‚СЊСЋ РїРµСЂРµС…РѕРґРёРј РІ РѕР±Р»С‘С‚
                bool canStartFlanking = enemy.PhaseTimer >= MinAttackPhaseDuration;
                float flankChanceThisFrame = AttackToFlankChancePerSecond * deltaTime;
                if (canStartFlanking && isAtOptimalDistance && !enemy.Retreating && UnityEngine.Random.value < flankChanceThisFrame)
                {
                    enemy.CombatPhase = EnemyCombatPhase.Flanking;
                    enemy.FlankDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                    enemy.PhaseTimer = 0f; // СЃР±СЂРѕСЃ С‚Р°Р№РјРµСЂР° РїСЂРё РІС…РѕРґРµ РІ Flanking
                }
                // Р•СЃР»Рё СЃР»РёС€РєРѕРј РґР°Р»РµРєРѕ РёР»Рё СЃР»РёС€РєРѕРј Р±Р»РёР·РєРѕ, РѕСЃС‚Р°С‘РјСЃСЏ РІ Р°С‚Р°РєРµ (РґРІРёРіР°РµРјСЃСЏ Рє РѕРїС‚РёРјР°Р»СЊРЅРѕР№ РґРёСЃС‚Р°РЅС†РёРё)
            }
            else // Flanking
            {
                // РЈРІРµР»РёС‡РёРІР°РµРј С‚Р°Р№РјРµСЂ С„Р°Р·С‹
                // РњР°РєСЃРёРјР°Р»СЊРЅРѕРµ РІСЂРµРјСЏ РѕР±Р»С‘С‚Р° (СЃРµРєСѓРЅРґС‹)
                float maxFlankingDuration = MaxFlankingPhaseDuration;
                // Р•СЃР»Рё РІС‹С€РµР» Р·Р° РїСЂРµРґРµР»С‹ РѕРїС‚РёРјР°Р»СЊРЅРѕР№ РґРёСЃС‚Р°РЅС†РёРё, РёР»Рё РѕС‚СЃС‚СѓРїР°РµРј, РёР»Рё С‚Р°Р№РјРµСЂ РїСЂРµРІС‹СЃРёР» Р»РёРјРёС‚, РІРѕР·РІСЂР°С‰Р°РµРјСЃСЏ РІ Р°С‚Р°РєСѓ
                if (!isAtOptimalDistance || enemy.Retreating || enemy.PhaseTimer >= maxFlankingDuration)
                {
                    enemy.CombatPhase = EnemyCombatPhase.Attack;
                    enemy.PhaseTimer = 0f;
                }
            }

            // РћСЂРёРµРЅС‚Р°С†РёСЏ РІСЂР°РіР° РїРѕ РЅР°РїСЂР°РІР»РµРЅРёСЋ С„Р°РєС‚РёС‡РµСЃРєРѕРіРѕ РїРµСЂРµРјРµС‰РµРЅРёСЏ (РєР°Рє Сѓ РёРіСЂРѕРєР°)
            // Р’ С„Р°Р·Рµ Flanking РѕСЂРёРµРЅС‚Р°С†РёСЏ СѓР¶Рµ РІС‹РїРѕР»РЅРµРЅР° РІ UpdateEnemyPosition, РЅРѕ РјРѕР¶РЅРѕ РґРѕРїРѕР»РЅРёС‚РµР»СЊРЅРѕ СЃРєРѕСЂСЂРµРєС‚РёСЂРѕРІР°С‚СЊ
            if (enemy.CombatPhase == EnemyCombatPhase.Attack)
            {
                Vector3 toPlayerDir = playerPosition - enemy.Transform.position;
                if (toPlayerDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.Euler(0f, 0f, Mathf.Atan2(toPlayerDir.y, toPlayerDir.x) * Mathf.Rad2Deg - 90f);
                    float turnSpeedDeg = Mathf.Max(1f, enemy.DistanceResponsiveness) * 620f;
                    enemy.Transform.rotation = Quaternion.RotateTowards(
                        enemy.Transform.rotation,
                        targetRot,
                        turnSpeedDeg * deltaTime);
                }
            }
            else if (appliedDelta.sqrMagnitude > 0.0001f)
            {
                Vector3 moveDir = appliedDelta.normalized;
                Quaternion targetRot = Quaternion.Euler(0f, 0f, Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg - 90f);
                float turnSpeedDeg = Mathf.Max(1f, enemy.DistanceResponsiveness) * 360f;
                enemy.Transform.rotation = Quaternion.RotateTowards(
                    enemy.Transform.rotation,
                    targetRot,
                    turnSpeedDeg * deltaTime);
            }

            Vector3 toPlayer = playerPosition - enemy.Transform.position;
            enemy.AttackTimer += deltaTime;
            enemy.AttackFlashTimer = Mathf.Max(0f, enemy.AttackFlashTimer - deltaTime * 4f);
            enemy.HitFlashTimer = Mathf.Max(0f, enemy.HitFlashTimer - deltaTime * 4f);

            bool canFire = enemy.CombatPhase == EnemyCombatPhase.Attack || UnityEngine.Random.value < 0.3f;
            bool fired = canFire && TryFireEnemyWeapons(context, enemy, playerPosition, deltaTime);
            if (fired)
            {
                enemy.AttackFlashTimer = 1f;
                enemy.AttackTimer = 0f;
            }
            else if (enemy.CombatPhase == EnemyCombatPhase.Attack && enemy.AttackTimer >= enemy.AttackCooldown && toPlayer.magnitude <= 3.8f)
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

    private static void UpdateEnemyPosition(EnemyShip enemy, Vector3 playerPosition, float deltaTime, out Vector3 appliedDelta)
    {
        appliedDelta = Vector3.zero;
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

        // Р•СЃР»Рё РІСЂР°Рі С‚РѕР»СЊРєРѕ С‡С‚Рѕ Р·Р°СЃРїР°РІРЅРµРЅ Рё РЅРµ РїРѕРІС‘СЂРЅСѓС‚, СЃСЂР°Р·Сѓ РѕСЂРёРµРЅС‚РёСЂСѓРµРј РµРіРѕ РЅР° РёРіСЂРѕРєР°
        if (enemy.Transform.rotation == Quaternion.identity)
        {
            Vector3 toPlayer = playerPosition - enemy.Transform.position;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg - 90f;
                enemy.Transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        float shieldMax = Mathf.Max(0f, enemy.MaxShield);
        float armorMax = Mathf.Max(0f, enemy.MaxArmor);
        float hullMax = Mathf.Max(0.01f, enemy.MaxHull);
        float durabilityNow = Mathf.Max(0f, enemy.Shield) + Mathf.Max(0f, enemy.Armor) + Mathf.Max(0f, enemy.Hull);
        float durabilityMax = Mathf.Max(0.01f, shieldMax + armorMax + hullMax);
        float durabilityPercent = durabilityNow / durabilityMax;
        bool lowDurability = durabilityPercent <= Mathf.Clamp01(enemy.LowDurabilityRetreatThreshold);

        // Р–РµР»Р°РµРјР°СЏ РґРёСЃС‚Р°РЅС†РёСЏ Р·Р°РІРёСЃРёС‚ РѕС‚ С„Р°Р·С‹ Р±РѕСЏ
        float desiredDistance;
        if (enemy.CombatPhase == EnemyCombatPhase.Attack)
        {
            // Р’ Р°С‚Р°РєРµ СЃС‚СЂРµРјРёРјСЃСЏ Рє РґРёСЃС‚Р°РЅС†РёРё СЌС„С„РµРєС‚РёРІРЅРѕР№ СЃС‚СЂРµР»СЊР±С‹ (РЅРѕ РЅРµ РґР°Р»СЊС€Рµ РѕСЂР±РёС‚Р°Р»СЊРЅРѕР№)
            desiredDistance = Mathf.Min(enemy.OrbitDistance, enemy.PrimaryWeaponRange * 0.8f);
        }
        else
        {
            // Р’ РѕР±Р»С‘С‚Рµ РёСЃРїРѕР»СЊР·СѓРµРј РѕСЂР±РёС‚Р°Р»СЊРЅСѓСЋ РґРёСЃС‚Р°РЅС†РёСЋ
            desiredDistance = enemy.OrbitDistance;
        }
        desiredDistance = Mathf.Max(0.1f, desiredDistance);
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
            // РњРёРЅРёРјР°Р»СЊРЅР°СЏ СЃРєРѕСЂРѕСЃС‚СЊ РѕС‚СЃС‚СѓРїР»РµРЅРёСЏ, С‡С‚РѕР±С‹ РЅРµ РѕСЃС‚Р°РЅР°РІР»РёРІР°С‚СЊСЃСЏ
            radialSpeed = Mathf.Max(radialSpeed, enemy.DriftSpeed * 0.3f);
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
        else if (currentDistance > desiredDistance + holdTolerance)
        {
            // Р’СЂР°Рі РґР°Р»СЊС€Рµ Р¶РµР»Р°РµРјРѕР№ РґРёСЃС‚Р°РЅС†РёРё, РЅРѕ РІРЅСѓС‚СЂРё РґРёСЃС‚Р°РЅС†РёРё Р°С‚Р°РєРё
            float farError = currentDistance - (desiredDistance + holdTolerance);
            radialSpeed = -Mathf.Clamp(
                farError * responsiveness,
                enemy.DriftSpeed * 0.2f,
                enemy.DriftSpeed * 0.8f);
        }
        else
        {
            // Р’СЂР°Рі РЅР°С…РѕРґРёС‚СЃСЏ РІ РїСЂРµРґРµР»Р°С… holdTolerance РѕС‚ Р¶РµР»Р°РµРјРѕР№ РґРёСЃС‚Р°РЅС†РёРё.
            // Р”РѕР±Р°РІРёРј РЅРµР±РѕР»СЊС€РѕРµ РєРѕР»РµР±Р°С‚РµР»СЊРЅРѕРµ РґРІРёР¶РµРЅРёРµ РІРїРµСЂС‘Рґ/РЅР°Р·Р°Рґ, С‡С‚РѕР±С‹ РЅРµ СЃС‚РѕСЏС‚СЊ РЅР° РјРµСЃС‚Рµ.
            float oscillation = Mathf.Sin(Time.time * 2f + enemy.StrafeJitterPhase) * 0.5f;
            radialSpeed = oscillation * enemy.DriftSpeed * 0.15f;
        }

        float maxRetreat = enemy.DriftSpeed * Mathf.Max(1.2f, enemy.RetreatSpeedMultiplier) * (lowDurability ? Mathf.Max(1f, enemy.LowDurabilityRetreatSpeedMultiplier) : 1f);
        // РћРіСЂР°РЅРёС‡РёРј РјР°РєСЃРёРјР°Р»СЊРЅСѓСЋ СЃРєРѕСЂРѕСЃС‚СЊ РѕС‚СЃС‚СѓРїР»РµРЅРёСЏ, С‡С‚РѕР±С‹ РёРіСЂРѕРє РјРѕРі РґРѕРіРЅР°С‚СЊ
        maxRetreat = Mathf.Min(maxRetreat, enemy.DriftSpeed * 1.5f);
        float maxApproach = enemy.DriftSpeed * 1.25f;
        radialSpeed = Mathf.Clamp(radialSpeed, -maxApproach, maxRetreat);

        float jitterAmplitude = Mathf.Max(0f, enemy.StrafeJitterAmplitude);
        float jitterFrequency = Mathf.Max(0.1f, enemy.StrafeJitterFrequency);
        float jitter = Mathf.Sin(Time.time * jitterFrequency + enemy.StrafeJitterPhase) * jitterAmplitude;
        
        // Р‘Р°Р·РѕРІР°СЏ СЃРєРѕСЂРѕСЃС‚СЊ РѕСЂР±РёС‚С‹ (РёСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ РІ С„Р°Р·Рµ Flanking)
        float orbitSpeed = enemy.OrbitSpeed * enemy.FlankDirection;
        // Р•СЃР»Рё РІСЂР°Рі РІ С„Р°Р·Рµ Flanking, СѓРІРµР»РёС‡РёРІР°РµРј СЃРєРѕСЂРѕСЃС‚СЊ РѕСЂР±РёС‚С‹ РґР»СЏ Р°РєС‚РёРІРЅРѕРіРѕ РѕР±Р»С‘С‚Р°
        if (enemy.CombatPhase == EnemyCombatPhase.Flanking)
        {
            orbitSpeed *= 2.5f;
        }
        
        // Р’РѕР·РІСЂР°С‰Р°РµРј РЅРѕСЂРјР°Р»СЊРЅРѕРµ РґРІРёР¶РµРЅРёРµ
        Vector3 tangent = new Vector3(-radialDirection.y, radialDirection.x, 0f);
        
        Vector3 moveDelta;
        if (enemy.Retreating)
        {
            // РЈР¶Рµ РѕРіСЂР°РЅРёС‡РёР»Рё СЃРєРѕСЂРѕСЃС‚СЊ РІС‹С€Рµ, РїСЂРѕСЃС‚Рѕ РїСЂРёРјРµРЅСЏРµРј
            moveDelta = radialDirection * radialSpeed * deltaTime;
        }
        else if (enemy.CombatPhase == EnemyCombatPhase.Attack)
        {
            // Р’ С„Р°Р·Рµ Р°С‚Р°РєРё РґРІРёР¶РµРјСЃСЏ РїСЂСЏРјРѕ Рє РёРіСЂРѕРєСѓ (СЂР°РґРёР°Р»СЊРЅРѕ), РґРѕР±Р°РІР»СЏРµРј Р±РѕРєРѕРІРѕРµ РґРІРёР¶РµРЅРёРµ РґР»СЏ Р¶РёРІРѕСЃС‚Рё
            float attackSway = Mathf.Sin(Time.time * (jitterFrequency * 0.7f) + enemy.StrafeJitterPhase * 1.7f) * 0.22f;
            float tangentScale = enemy.FlankDirection * 0.16f + jitter * 0.28f + attackSway; // выраженный, но контролируемый стрейф
            // РњРёРЅРёРјР°Р»СЊРЅР°СЏ СЂР°РґРёР°Р»СЊРЅР°СЏ СЃРєРѕСЂРѕСЃС‚СЊ, С‡С‚РѕР±С‹ РЅРµ СЃС‚РѕСЏС‚СЊ РЅР° РјРµСЃС‚Рµ
            float minRadialSpeed = enemy.DriftSpeed * 0.1f;
            float effectiveRadialSpeed = Mathf.Abs(radialSpeed) < minRadialSpeed ? Mathf.Sign(radialSpeed) * minRadialSpeed : radialSpeed;
            moveDelta = (radialDirection * effectiveRadialSpeed + tangent * enemy.DriftSpeed * tangentScale) * deltaTime;
        }
        else
        {
            // Р’ РѕР±Р»РµС‚Рµ РґРІРёР¶РµРјСЃСЏ РїРѕ РґСѓРіРµ, СЃРѕС…СЂР°РЅСЏСЏ РѕСЂР±РёС‚Р°Р»СЊРЅСѓСЋ РґРёСЃС‚Р°РЅС†РёСЋ, РёСЃРїРѕР»СЊР·СѓРµРј orbitSpeed
            float distError = currentDistance - enemy.OrbitDistance;
            Vector3 radialCorrection = radialDirection * (-distError * responsiveness * 0.2f);
            moveDelta = (tangent * orbitSpeed + radialCorrection) * deltaTime;
            
            // РњРµРґР»РµРЅРЅРѕ РјРµРЅСЏРµРј OrbitAngle, С‡С‚РѕР±С‹ РІСЂР°РіРё СЂР°СЃРїСЂРµРґРµР»СЏР»РёСЃСЊ РїРѕ РѕРєСЂСѓР¶РЅРѕСЃС‚Рё
            enemy.OrbitAngle += orbitSpeed * 0.5f * deltaTime;
        }

        // Р—Р°РїРѕРјРёРЅР°РµРј РїРѕР·РёС†РёСЋ РґРѕ РїРµСЂРµРјРµС‰РµРЅРёСЏ
        Vector3 beforePos = enemy.Transform.position;
        // РџСЂРёРјРµРЅСЏРµРј РїРµСЂРµРјРµС‰РµРЅРёРµ СЃ СѓС‡С‘С‚РѕРј РєРѕР»Р»РёР·РёР№
        MoveEnemyWithBaseCollision(enemy, moveDelta);
        // Р’С‹С‡РёСЃР»СЏРµРј С„Р°РєС‚РёС‡РµСЃРєРѕРµ РїРµСЂРµРјРµС‰РµРЅРёРµ
        appliedDelta = enemy.Transform.position - beforePos;
    }

    private static void MoveEnemyWithBaseCollision(EnemyShip enemy, Vector3 delta)
    {
        if (enemy == null || enemy.Transform == null || delta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Vector3 current = enemy.Transform.position;
        float radius = ResolveCollisionRadius(enemy.Transform);
        if (TryResolveBaseOverlap(current, radius, enemy.Transform, out Vector3 resolved))
        {
            enemy.Transform.position = resolved;
            current = resolved;
        }

        Vector3 target = current + delta;

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
            return;
        }

        if (TryMoveAlongBaseEdge(current, delta, radius, enemy.Transform, out Vector3 slidePosition))
        {
            enemy.Transform.position = slidePosition;
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

    private static bool TryResolveBaseOverlap(Vector3 current, float radius, Transform selfRoot, out Vector3 resolvedPosition)
    {
        resolvedPosition = current;
        if (!IsBlockedByBase(current, radius, selfRoot))
        {
            return false;
        }

        float step = Mathf.Max(0.2f, radius * 0.55f);
        float maxDistance = Mathf.Max(step * 2f, radius * 4f);
        Vector3[] directions =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down,
            (Vector3.right + Vector3.up).normalized,
            (Vector3.right + Vector3.down).normalized,
            (Vector3.left + Vector3.up).normalized,
            (Vector3.left + Vector3.down).normalized
        };

        for (float distance = step; distance <= maxDistance; distance += step)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 candidate = current + directions[i] * distance;
                if (!IsBlockedByBase(candidate, radius, selfRoot))
                {
                    resolvedPosition = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryMoveAlongBaseEdge(Vector3 current, Vector3 delta, float radius, Transform selfRoot, out Vector3 slidePosition)
    {
        slidePosition = current;
        if (delta.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Vector3 dir = delta.normalized;
        Vector3 tangentA = new Vector3(-dir.y, dir.x, 0f);
        Vector3 tangentB = -tangentA;
        float length = delta.magnitude;
        Vector3 candidateA = current + tangentA * length;
        Vector3 candidateB = current + tangentB * length;

        bool freeA = !IsBlockedByBase(candidateA, radius, selfRoot);
        bool freeB = !IsBlockedByBase(candidateB, radius, selfRoot);

        if (freeA && freeB)
        {
            slidePosition = candidateA;
            return true;
        }

        if (freeA)
        {
            slidePosition = candidateA;
            return true;
        }

        if (freeB)
        {
            slidePosition = candidateB;
            return true;
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

