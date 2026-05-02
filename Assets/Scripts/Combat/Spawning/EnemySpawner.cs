using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    [Header("Настройка визуала врагов")]
    [Tooltip("Материал для визуала щита врага. Если не назначен, будет использован материал SpriteRenderer из префаба корабля.")]
    [SerializeField] private Material shieldHitMaterial;
    [Tooltip("Спрайт короткой вспышки/кольца при попадании по щиту врага. Используется только визуальным компонентом ShipShieldVisual.")]
    [SerializeField] private Sprite shieldImpactSprite;

    private IPoolService poolService;
    private Transform enemyRoot;
    private Action<EnemyShip, DamageInfo, DamageResolutionResult> damageAppliedCallback;
    private Action<WeaponDataSO, Transform> attachWeaponVisualCallback;
    private readonly HashSet<string> warningKeys = new HashSet<string>();

    internal void Initialize(
        IPoolService pool,
        Transform root,
        Material shieldMaterial,
        Sprite impactSprite,
        Action<EnemyShip, DamageInfo, DamageResolutionResult> onDamageApplied,
        Action<WeaponDataSO, Transform> onAttachWeaponVisual)
    {
        poolService = pool;
        enemyRoot = root;
        damageAppliedCallback = onDamageApplied;
        attachWeaponVisualCallback = onAttachWeaponVisual;

        if (shieldHitMaterial == null)
        {
            shieldHitMaterial = shieldMaterial;
        }

        if (shieldImpactSprite == null)
        {
            shieldImpactSprite = impactSprite;
        }
    }

    public bool IsReadyToSpawn()
    {
        return poolService != null && enemyRoot != null;
    }

    internal EnemyShip SpawnEnemy(
        string enemyId,
        ShipDataSO shipData,
        Vector3 position,
        float levelScale,
        int enemyIndexForVisualOffset)
    {
        if (poolService == null)
        {
            WarnOnce("missing_pool", "EnemySpawner: не назначен IPoolService. Враг не создан.");
            return null;
        }

        if (enemyRoot == null)
        {
            WarnOnce("missing_root", "EnemySpawner: не назначен корневой объект врагов. Враг не создан.");
            return null;
        }

        if (shipData == null)
        {
            WarnOnce("missing_ship_data_" + enemyId, "EnemySpawner: отсутствует ShipDataSO для врага '" + enemyId + "'. Враг не создан.");
            return null;
        }

        if (shipData.shipPrefab == null)
        {
            WarnOnce(
                "missing_ship_prefab_" + shipData.GetInstanceID(),
                "EnemySpawner: у ShipDataSO '" + shipData.name + "' не назначен shipPrefab. Враг не создан.");
            return null;
        }

        string typeName = string.IsNullOrEmpty(shipData.displayName) ? shipData.name : shipData.displayName;
        GameObject enemyPrefab = shipData.shipPrefab;
        GameObject enemyObject = poolService.Get(enemyPrefab, enemyRoot);
        if (enemyObject == null)
        {
            return null;
        }

        enemyObject.name = enemyId;
        enemyObject.transform.position = position;
        AssignEnemyIdentity(enemyObject);

        SpriteRenderer bodyRenderer = enemyObject.GetComponentInChildren<SpriteRenderer>(true);
        SpriteRenderer shieldRenderer = FindChildSpriteRenderer(enemyObject.transform, "Shield");
        if (shieldRenderer == null)
        {
            shieldRenderer = FindChildSpriteRendererContaining(enemyObject.transform, "shield");
        }
        if (shieldRenderer == null)
        {
            shieldRenderer = FindChildSpriteRendererContaining(enemyObject.transform, "aura");
        }

        SpriteRenderer targetRenderer = FindChildSpriteRenderer(enemyObject.transform, "TargetRing");
        if (targetRenderer != null)
        {
            targetRenderer.gameObject.SetActive(false);
        }

        SpriteRenderer thrusterRenderer = FindChildSpriteRenderer(enemyObject.transform, "Thruster");
        ShipThrusterEffect thrusterEffect = EnsureThrusterEffect(enemyObject);
        if (thrusterEffect != null)
        {
            thrusterEffect.ConfigureFromShipData(shipData);
        }

        float sanitizedLevelScale = Mathf.Max(0.1f, levelScale);
        float shieldValue = Mathf.Max(1f, shipData.maxShield * sanitizedLevelScale);
        float armorValue = Mathf.Max(1f, shipData.maxArmor * sanitizedLevelScale);
        float hullValue = Mathf.Max(1f, shipData.maxHull * sanitizedLevelScale);
        float enemyMoveSpeed = Mathf.Max(0.5f, shipData.maxSpeed * 0.22f) + sanitizedLevelScale * 0.2f;

        List<WeaponDataSO> compatibleWeapons = GetCompatibleStartingWeapons(shipData);
        WeaponDataSO enemyWeapon = compatibleWeapons.Count > 0 ? compatibleWeapons[0] : GetPrimaryWeapon(shipData);
        if (compatibleWeapons.Count == 0)
        {
            WarnNoCompatibleWeapons(shipData, enemyWeapon);
        }

        float enemyDamage = enemyWeapon != null
            ? Mathf.Max(1f, enemyWeapon.damage * Mathf.Max(0.1f, shipData.damageMultiplier) * sanitizedLevelScale)
            : Mathf.Max(6f, 10f * sanitizedLevelScale);

        float weaponCooldown = enemyWeapon != null
            ? Mathf.Max(0.05f, enemyWeapon.cooldown > 0f ? enemyWeapon.cooldown : enemyWeapon.fireRate)
            : Random.Range(1.15f, 1.8f);
        float weaponRange = enemyWeapon != null
            ? Mathf.Max(enemyWeapon.maxRange, enemyWeapon.projectileMaxDistance)
            : 5.2f;
        weaponRange = Mathf.Max(4.5f, weaponRange);

        float preferredDistanceBase = shipData.enemyPreferredDistance > 0f
            ? shipData.enemyPreferredDistance
            : weaponRange * shipData.enemyPreferredDistanceFromRange;
        float preferredDistance = preferredDistanceBase + Random.Range(-shipData.enemyPreferredDistanceVariance, shipData.enemyPreferredDistanceVariance);
        preferredDistance = Mathf.Clamp(preferredDistance, 2.4f, weaponRange * 0.94f);
        float distanceTolerance = Mathf.Max(0.05f, shipData.enemyDistanceTolerance);
        float retreatDistance = Mathf.Max(1.6f, preferredDistance - distanceTolerance * 1.35f);
        float reengageDistance = Mathf.Max(retreatDistance + 0.2f, preferredDistance + distanceTolerance);

        EnemyShip enemy = new EnemyShip
        {
            Id = enemyId,
            Type = typeName,
            Transform = enemyObject.transform,
            BodyRenderer = bodyRenderer,
            ShieldRenderer = shieldRenderer,
            TargetRenderer = targetRenderer,
            ThrusterRenderer = thrusterRenderer,
            ThrusterEffect = thrusterEffect,
            OrbitDistance = preferredDistance,
            OrbitAngle = Random.Range(0f, Mathf.PI * 2f),
            OrbitSpeed = Random.Range(0.4f, 0.95f),
            RetreatDistance = retreatDistance,
            ReengageDistance = reengageDistance,
            DistanceResponsiveness = Random.Range(1.25f, 1.75f),
            RetreatSpeedMultiplier = Random.Range(1.8f, 2.35f),
            PrimaryWeaponRange = weaponRange,
            HoldDistanceTolerance = distanceTolerance,
            OutOfRangeApproachFactor = shipData.enemyOutOfRangeApproachFactor,
            LowDurabilityRetreatThreshold = shipData.enemyLowDurabilityRetreatThreshold,
            LowDurabilityRetreatDistanceBonus = shipData.enemyLowDurabilityRetreatDistanceBonus,
            LowDurabilityRetreatSpeedMultiplier = shipData.enemyLowDurabilityRetreatSpeedMultiplier,
            StrafeJitterAmplitude = shipData.enemyStrafeJitterAmplitude,
            StrafeJitterFrequency = shipData.enemyStrafeJitterFrequency,
            StrafeJitterPhase = Random.Range(0f, Mathf.PI * 2f),
            AttackCooldown = weaponCooldown,
            AttackTimer = Random.Range(0f, 0.7f),
            Damage = enemyDamage,
            ScoreValue = shipData.scoreReward > 0 ? shipData.scoreReward : 40,
            DriftSpeed = enemyMoveSpeed,
            MaxShield = shieldValue,
            Shield = shieldValue,
            MaxArmor = armorValue,
            Armor = armorValue,
            MaxHull = hullValue,
            Hull = hullValue,
            WeaponDamageMultiplier = Mathf.Max(0.1f, shipData.damageMultiplier) * sanitizedLevelScale,
            Prefab = enemyPrefab,
            BaseBodyColor = bodyRenderer != null ? bodyRenderer.color : Color.white,
            BaseShieldColor = shieldRenderer != null && shieldRenderer.color.a > 0.001f ? shieldRenderer.color : shipData.auraColor
        };

        enemy.ShieldVisual = EnsureShieldVisual(enemyObject, enemy.ShieldRenderer, enemy.BaseShieldColor, enemyIndexForVisualOffset * 0.47f);

        if (compatibleWeapons.Count == 0 && enemyWeapon != null)
        {
            compatibleWeapons.Add(enemyWeapon);
        }

        ShipDamageReceiver receiver = enemyObject.GetComponent<ShipDamageReceiver>();
        if (receiver == null)
        {
            receiver = enemyObject.AddComponent<ShipDamageReceiver>();
        }
        if (receiver == null)
        {
            WarnOnce(
                "missing_damage_receiver_" + enemyObject.GetInstanceID(),
                "EnemySpawner: не удалось добавить ShipDamageReceiver на '" + enemyObject.name + "'. Враг не создан.");
            return null;
        }

        receiver.Initialize(
            CombatFaction.Enemy,
            () => new ShipDurabilityState
            {
                MaxShield = enemy.MaxShield,
                Shield = enemy.Shield,
                MaxArmor = enemy.MaxArmor,
                Armor = enemy.Armor,
                MaxHull = enemy.MaxHull,
                Hull = enemy.Hull
            },
            state =>
            {
                enemy.MaxShield = state.MaxShield;
                enemy.Shield = state.Shield;
                enemy.MaxArmor = state.MaxArmor;
                enemy.Armor = state.Armor;
                enemy.MaxHull = state.MaxHull;
                enemy.Hull = state.Hull;
            });
        receiver.DamageApplied += (info, result) => damageAppliedCallback?.Invoke(enemy, info, result);
        enemy.DamageReceiver = receiver;
        enemy.TeamMember = enemyObject.GetComponent<TeamMember>();

        ClearRuntimeWeaponVisuals(enemyObject.transform);
        for (int i = 0; i < compatibleWeapons.Count; i++)
        {
            WeaponDataSO weapon = compatibleWeapons[i];
            if (weapon == null)
            {
                continue;
            }

            WeaponInstance instance = new WeaponInstance(
                weapon,
                enemyObject.transform,
                FindWeaponMuzzle(enemyObject.transform, i),
                CombatFaction.Enemy,
                enemyObject);

            if (instance.BeginFire())
            {
                // Сохраняет прежнее поведение: враги появляются с небольшой случайной готовностью оружия.
                instance.Tick(Random.Range(0f, instance.EffectiveCooldown));
            }

            enemy.WeaponInstances.Add(instance);
            attachWeaponVisualCallback?.Invoke(weapon, instance.MuzzleTransform);
        }

        return enemy;
    }

    private void WarnNoCompatibleWeapons(ShipDataSO shipData, WeaponDataSO fallbackWeapon)
    {
        if (shipData == null)
        {
            return;
        }

        string fallbackText = fallbackWeapon != null
            ? " Будет использовано первое стартовое оружие как резерв: '" + fallbackWeapon.name + "'."
            : " Враг будет создан без установленного оружия и будет использовать только резервные боевые параметры.";
        WarnOnce(
            "no_compatible_weapon_" + shipData.GetInstanceID(),
            "EnemySpawner: у корабля '" + shipData.name + "' нет совместимого стартового оружия для класса " + shipData.shipClass + "." + fallbackText);
    }

    private void WarnOnce(string key, string message)
    {
        if (string.IsNullOrEmpty(key) || warningKeys.Contains(key))
        {
            return;
        }

        warningKeys.Add(key);
        Debug.LogWarning(message, this);
    }

    private static List<WeaponDataSO> GetCompatibleStartingWeapons(ShipDataSO shipData)
    {
        List<WeaponDataSO> compatibleWeapons = new List<WeaponDataSO>();
        if (shipData == null || shipData.startingWeapons == null)
        {
            return compatibleWeapons;
        }

        for (int i = 0; i < shipData.startingWeapons.Count; i++)
        {
            WeaponDataSO slotWeapon = shipData.startingWeapons[i];
            if (slotWeapon == null || !CanShipUseWeapon(shipData.shipClass, slotWeapon))
            {
                continue;
            }

            compatibleWeapons.Add(slotWeapon);
        }

        return compatibleWeapons;
    }

    private static ShipThrusterEffect EnsureThrusterEffect(GameObject shipObject)
    {
        if (shipObject == null)
        {
            return null;
        }

        ShipThrusterEffect effect = shipObject.GetComponent<ShipThrusterEffect>();
        if (effect == null)
        {
            effect = shipObject.AddComponent<ShipThrusterEffect>();
        }

        return effect;
    }

    private ShipShieldVisual EnsureShieldVisual(GameObject owner, SpriteRenderer renderer, Color baseColor, float pulseOffset)
    {
        if (owner == null || renderer == null)
        {
            return null;
        }

        ShipShieldVisual shieldVisual = owner.GetComponentInChildren<ShipShieldVisual>(true);
        if (shieldVisual == null)
        {
            shieldVisual = owner.AddComponent<ShipShieldVisual>();
        }

        shieldVisual.Initialize(renderer, shieldHitMaterial, shieldImpactSprite, baseColor, pulseOffset);
        return shieldVisual;
    }

    private void ClearRuntimeWeaponVisuals(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = children.Length - 1; i >= 0; i--)
        {
            Transform child = children[i];
            if (child == null || !string.Equals(child.name, "WeaponVisualInstance", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private static void AssignEnemyIdentity(GameObject enemyObject)
    {
        if (enemyObject == null)
        {
            return;
        }

        try
        {
            enemyObject.tag = "Enemy";
        }
        catch (UnityException)
        {
            // Тег Enemy может отсутствовать в настройках проекта; команда и слой все равно назначаются ниже.
        }

        TeamMember teamMember = enemyObject.GetComponent<TeamMember>();
        if (teamMember == null)
        {
            teamMember = enemyObject.AddComponent<TeamMember>();
        }
        teamMember.SetFaction(CombatFaction.Enemy);

        CombatLayerUtility.ApplyShipLayer(enemyObject, CombatFaction.Enemy);
    }

    private static Transform FindWeaponMuzzle(Transform root, int index)
    {
        if (root == null)
        {
            return null;
        }

        Transform slotsRoot = FindDirectChild(root, "WeaponSlots");
        if (slotsRoot == null)
        {
            slotsRoot = root;
        }

        Transform indexedSlot = FindDirectChild(slotsRoot, "WeaponSlot_" + (index + 1));
        if (indexedSlot != null)
        {
            Transform muzzle = FindMuzzleTransform(indexedSlot, index);
            return muzzle != null ? muzzle : indexedSlot;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            string name = children[i].name;
            if (name.IndexOf("muzzle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("weaponslot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return children[i];
            }
        }

        return root;
    }

    private static Transform FindMuzzleTransform(Transform slot, int index)
    {
        if (slot == null)
        {
            return null;
        }

        string indexedMountName = "WeaponMount_" + (index + 1);
        Transform indexedMount = FindDirectChild(slot, indexedMountName);
        if (indexedMount != null)
        {
            Transform indexedMountMuzzle = FindDirectChild(indexedMount, "Muzzle");
            if (indexedMountMuzzle != null)
            {
                return indexedMountMuzzle;
            }
        }

        Transform directMuzzle = FindDirectChild(slot, "Muzzle");
        if (directMuzzle != null)
        {
            return directMuzzle;
        }

        Transform[] children = slot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            string childName = children[i].name;
            if (childName.IndexOf("muzzle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                childName.IndexOf("firepoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                childName.IndexOf("projectileorigin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return children[i];
            }
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private static SpriteRenderer FindChildSpriteRenderer(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform child = FindDirectChild(root, childName);
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private static SpriteRenderer FindChildSpriteRendererContaining(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrEmpty(namePart))
        {
            return null;
        }

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return renderers[i];
            }
        }

        return null;
    }

    private static bool CanShipUseWeapon(ShipClass shipClass, WeaponDataSO weaponData)
    {
        if (weaponData == null)
        {
            return false;
        }

        return GetShipClassRank(shipClass) >= GetShipClassRank(weaponData.requiredClass);
    }

    private static int GetShipClassRank(ShipClass shipClass)
    {
        switch (shipClass)
        {
            case ShipClass.Light: return 0;
            case ShipClass.Medium: return 1;
            case ShipClass.Heavy: return 2;
            default: return 0;
        }
    }

    private static WeaponDataSO GetPrimaryWeapon(ShipDataSO ship)
    {
        if (ship == null || ship.startingWeapons == null)
        {
            return null;
        }

        for (int i = 0; i < ship.startingWeapons.Count; i++)
        {
            if (ship.startingWeapons[i] != null)
            {
                return ship.startingWeapons[i];
            }
        }

        return null;
    }
}
