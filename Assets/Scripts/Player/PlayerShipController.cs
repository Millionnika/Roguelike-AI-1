using System;
using System.Collections.Generic;
using SpaceFrontier.Player;
using UnityEngine;

public sealed class PlayerShipController : MonoBehaviour
{
    private PlayerShip player;
    private ShipEquipmentState equipmentState;
    private Transform weaponSlotsRoot;
    private GameObject playerVisualInstance;
    private Material shieldHitMaterial;
    private Sprite shieldFallbackSprite;
    private Action<ShipEquipmentState> equipmentChanged;

    public PlayerShip Player => player;
    public ShipEquipmentState EquipmentState => equipmentState;

    internal void Initialize(
        ShipEquipmentState sharedEquipmentState,
        Material newShieldHitMaterial,
        Sprite newShieldFallbackSprite,
        Action<ShipEquipmentState> equipmentChangedCallback)
    {
        equipmentState = sharedEquipmentState ?? new ShipEquipmentState();
        shieldHitMaterial = newShieldHitMaterial;
        shieldFallbackSprite = newShieldFallbackSprite;
        equipmentChanged = equipmentChangedCallback;
    }

    public PlayerShip SpawnPlayer(Transform worldRoot)
    {
        if (worldRoot == null)
        {
            Debug.LogWarning("PlayerShipController: корневой объект мира не назначен, корабль игрока не создан.", this);
            return null;
        }

        if (player != null && player.Transform != null)
        {
            player.Transform.SetParent(worldRoot, false);
            return player;
        }

        GameObject playerObject = new GameObject("PlayerShip");
        playerObject.transform.SetParent(worldRoot, false);

        TeamMember playerTeam = playerObject.GetComponent<TeamMember>();
        if (playerTeam == null)
        {
            playerTeam = playerObject.AddComponent<TeamMember>();
        }

        playerTeam.SetFaction(CombatFaction.Player);

        player = new PlayerShip
        {
            Transform = playerObject.transform,
            TeamMember = playerTeam
        };

        return player;
    }

    public void ApplyShipDefinition(ShipDataSO ship, bool resetProgress)
    {
        if (player == null || player.Transform == null || ship == null)
        {
            return;
        }

        player.Speed = Mathf.Max(0.1f, ship.maxSpeed);
        player.Acceleration = Mathf.Max(0.1f, ship.acceleration);
        player.Drag = Mathf.Max(0f, ship.drag);
        player.RotationResponsiveness = Mathf.Max(0.1f, ship.rotationSpeed);
        player.SpeedMultiplier = 1f;
        player.DamageMultiplier = Mathf.Max(0.1f, ship.damageMultiplier);
        player.RepairMultiplier = Mathf.Max(0.1f, ship.repairMultiplier);
        player.MaxShield = Mathf.Max(1f, ship.maxShield);
        player.Shield = player.MaxShield;
        player.MaxArmor = Mathf.Max(1f, ship.maxArmor);
        player.Armor = player.MaxArmor;
        player.MaxHull = Mathf.Max(1f, ship.maxHull);
        player.Hull = player.MaxHull;
        player.MaxCapacitor = Mathf.Max(1f, ship.capacitor);
        player.Capacitor = player.MaxCapacitor;
        player.CapacitorRechargeTime = Mathf.Max(1f, ship.capacitorRechargeTime);
        player.CapacitorRechargeRate = Mathf.Max(0.1f, ship.capacitorRechargeRate);
        PrepareForNextEncounter();

        ApplyShipVisualFromPrefab(ship);
        ConfigureEquipment(ship);
        ConfigurePlayerDamageReceiver();

        if (resetProgress)
        {
            player.Level = 1;
            player.Experience = 0;
            player.ExperienceToNext = 100;
        }
    }

    public void PrepareForNextEncounter()
    {
        if (player == null || player.Transform == null)
        {
            return;
        }

        player.Transform.position = Vector3.zero;
        // player.Transform.rotation = Quaternion.identity; // Убрали сброс поворота
        player.Velocity = Vector2.zero;
        player.MoveCommandActive = false;
    }

    public void RestoreHull(float percent)
    {
        if (player == null)
        {
            return;
        }

        float amount = Mathf.Max(0f, percent) * player.MaxHull * Mathf.Max(0.1f, player.RepairMultiplier);
        player.Hull = Mathf.Min(player.MaxHull, player.Hull + amount);
    }

    public float GetHullPercent()
    {
        return player != null ? player.HullPercent : 0f;
    }

    private void ConfigureEquipment(ShipDataSO ship)
    {
        if (player == null || player.Transform == null || ship == null || equipmentState == null)
        {
            return;
        }

        equipmentState.ShipData = ship;
        equipmentState.ConfigureSlots(Mathf.Max(0, ship.weaponSlotCount), Mathf.Max(0, ship.moduleSlotCount));
        RebuildWeaponSlots(ship.weaponSlotCount);

        for (int i = 0; i < equipmentState.InstalledWeapons.Count; i++)
        {
            WeaponDataSO configuredWeapon = ship.startingWeapons != null && i < ship.startingWeapons.Count
                ? ship.startingWeapons[i]
                : null;
            if (configuredWeapon != null && !CanShipUseWeapon(ship.shipClass, configuredWeapon))
            {
                Debug.LogWarning(
                    "PlayerShipController: оружие '" + configuredWeapon.name + "' в корабле '" + ship.displayName +
                    "' в слоте " + (i + 1) + " не совместимо с классом " + ship.shipClass + ".", this);
                configuredWeapon = null;
            }

            equipmentState.InstalledWeapons[i] = configuredWeapon;
            equipmentState.WeaponTimers[i] = 0f;
            equipmentState.RuntimeWeapons[i] = configuredWeapon != null
                ? new WeaponInstance(
                    configuredWeapon,
                    player.Transform,
                    i < equipmentState.WeaponMuzzles.Count ? equipmentState.WeaponMuzzles[i] : player.Transform,
                    CombatFaction.Player,
                    player.Transform.gameObject)
                : null;
        }

        RefreshWeaponVisuals(equipmentState.InstalledWeapons, equipmentState.WeaponMuzzles);

        for (int i = 0; i < equipmentState.InstalledModules.Count; i++)
        {
            ModuleDataSO moduleData = ship.startingModules != null && i < ship.startingModules.Count
                ? ship.startingModules[i]
                : null;
            equipmentState.InstalledModules[i] = moduleData;
        }

        equipmentChanged?.Invoke(equipmentState);
    }

    private void ConfigurePlayerDamageReceiver()
    {
        if (player == null || player.Transform == null)
        {
            return;
        }

        ShipDamageReceiver receiver = player.Transform.GetComponent<ShipDamageReceiver>();
        if (receiver == null)
        {
            receiver = player.Transform.gameObject.AddComponent<ShipDamageReceiver>();
        }

        receiver.Initialize(
            CombatFaction.Player,
            ReadPlayerDurability,
            WritePlayerDurability);
        receiver.DamageApplied += OnPlayerDamageApplied;

        TeamMember teamMember = player.Transform.GetComponent<TeamMember>();
        if (teamMember == null)
        {
            teamMember = player.Transform.gameObject.AddComponent<TeamMember>();
        }

        teamMember.SetFaction(CombatFaction.Player);
        CombatLayerUtility.ApplyShipLayer(player.Transform.gameObject, CombatFaction.Player);

        player.DamageReceiver = receiver;
        player.TeamMember = teamMember;
    }

    private ShipDurabilityState ReadPlayerDurability()
    {
        return new ShipDurabilityState
        {
            MaxShield = player.MaxShield,
            Shield = player.Shield,
            MaxArmor = player.MaxArmor,
            Armor = player.Armor,
            MaxHull = player.MaxHull,
            Hull = player.Hull
        };
    }

    private void WritePlayerDurability(ShipDurabilityState state)
    {
        player.MaxShield = state.MaxShield;
        player.Shield = state.Shield;
        player.MaxArmor = state.MaxArmor;
        player.Armor = state.Armor;
        player.MaxHull = state.MaxHull;
        player.Hull = state.Hull;
    }

    private void OnPlayerDamageApplied(DamageInfo info, DamageResolutionResult result)
    {
        if (result.AppliedShieldDamage <= 0f)
        {
            return;
        }

        if (player != null && player.ShieldVisual != null)
        {
            player.ShieldVisual.PlayImpact(info.HitPoint, result.AppliedShieldDamage);
        }
    }

    private void ApplyShipVisualFromPrefab(ShipDataSO ship)
    {
        if (player == null || player.Transform == null)
        {
            return;
        }

        if (playerVisualInstance != null)
        {
            Destroy(playerVisualInstance);
            playerVisualInstance = null;
        }

        player.BodyRenderer = null;
        player.AuraRenderer = null;
        player.ThrusterRenderer = null;
        player.ShieldVisual = null;
        player.ThrusterEffect = null;

        if (ship == null || ship.shipPrefab == null)
        {
            Debug.LogError("PlayerShipController: у корабля '" + (ship != null ? ship.displayName : "null") + "' не назначен shipPrefab.", this);
            return;
        }

        playerVisualInstance = Instantiate(ship.shipPrefab, player.Transform);
        playerVisualInstance.name = "PlayerVisual";
        playerVisualInstance.transform.localPosition = Vector3.zero;
        playerVisualInstance.transform.localRotation = Quaternion.identity;
        playerVisualInstance.transform.localScale = Vector3.one;

        ResolvePlayerVisualRenderers(playerVisualInstance.transform, out SpriteRenderer body, out SpriteRenderer aura, out SpriteRenderer thruster);
        player.BodyRenderer = body;
        player.AuraRenderer = aura;
        player.ThrusterRenderer = thruster;
        player.ThrusterEffect = EnsureThrusterEffect(playerVisualInstance);
        if (player.ThrusterEffect != null)
        {
            player.ThrusterEffect.ConfigureFromShipData(ship);
        }

        player.BaseBodyColor = body != null ? body.color : ship.accentColor;
        player.BaseAuraColor = aura != null && aura.color.a > 0.001f ? aura.color : ship.auraColor;
        player.ShieldVisual = EnsureShieldVisual(playerVisualInstance, player.AuraRenderer, player.BaseAuraColor, 0f);
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

    private static void ResolvePlayerVisualRenderers(Transform visualRoot, out SpriteRenderer body, out SpriteRenderer aura, out SpriteRenderer thruster)
    {
        body = null;
        aura = null;
        thruster = null;
        SpriteRenderer shieldCandidate = null;
        SpriteRenderer auraCandidate = null;

        if (visualRoot == null)
        {
            return;
        }

        SpriteRenderer[] renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            string lowerName = renderers[i].name.ToLowerInvariant();
            if (body == null && (lowerName.Contains("body") || lowerName.Contains("hull")))
            {
                body = renderers[i];
            }
            else if (shieldCandidate == null && lowerName.Contains("shield"))
            {
                shieldCandidate = renderers[i];
            }
            else if (auraCandidate == null && lowerName.Contains("aura"))
            {
                auraCandidate = renderers[i];
            }
            else if (thruster == null && (lowerName.Contains("thruster") || lowerName.Contains("engine")))
            {
                thruster = renderers[i];
            }
        }

        aura = shieldCandidate != null ? shieldCandidate : auraCandidate;

        if (body == null && renderers.Length > 0)
        {
            body = renderers[0];
        }
    }

    private void RebuildWeaponSlots(int weaponSlotCount)
    {
        int slotCount = Mathf.Max(0, weaponSlotCount);
        Transform prefabSlotsRoot = playerVisualInstance != null ? FindDirectChild(playerVisualInstance.transform, "WeaponSlots") : null;

        if (prefabSlotsRoot != null)
        {
            weaponSlotsRoot = prefabSlotsRoot;
            for (int i = 0; i < slotCount; i++)
            {
                Transform prefabSlot = FindWeaponMuzzle(prefabSlotsRoot, i);
                if (i < equipmentState.WeaponMuzzles.Count)
                {
                    equipmentState.WeaponMuzzles[i] = prefabSlot != null ? prefabSlot : player.Transform;
                }
            }

            return;
        }

        if (weaponSlotsRoot == null || weaponSlotsRoot == prefabSlotsRoot)
        {
            weaponSlotsRoot = new GameObject("WeaponSlots").transform;
            weaponSlotsRoot.SetParent(player.Transform, false);
        }

        for (int i = 0; i < slotCount; i++)
        {
            Transform slotTransform = FindDirectChild(weaponSlotsRoot, "WeaponSlot_" + (i + 1));
            if (slotTransform == null)
            {
                GameObject slotObject = new GameObject("WeaponSlot_" + (i + 1));
                slotObject.transform.SetParent(weaponSlotsRoot, false);
                slotTransform = slotObject.transform;
            }

            float lerp = slotCount <= 1 ? 0.5f : i / (float)(slotCount - 1);
            float x = Mathf.Lerp(-0.38f, 0.38f, lerp);
            float y = Mathf.Lerp(0.58f, 0.66f, 1f - Mathf.Abs(lerp - 0.5f) * 2f);
            slotTransform.localPosition = new Vector3(x, y, 0f);
            slotTransform.localRotation = Quaternion.identity;
            Transform muzzleTransform = EnsureWeaponMount(slotTransform, i);

            if (i < equipmentState.WeaponMuzzles.Count)
            {
                equipmentState.WeaponMuzzles[i] = muzzleTransform != null ? muzzleTransform : slotTransform;
            }
        }
    }

    private static Transform EnsureWeaponMount(Transform slotTransform, int index)
    {
        if (slotTransform == null)
        {
            return null;
        }

        string mountName = "WeaponMount_" + (index + 1);
        Transform mountTransform = FindDirectChild(slotTransform, mountName);
        if (mountTransform == null)
        {
            GameObject mountObject = new GameObject(mountName);
            mountObject.transform.SetParent(slotTransform, false);
            mountTransform = mountObject.transform;
        }

        Transform muzzleTransform = FindDirectChild(mountTransform, "Muzzle");
        if (muzzleTransform == null)
        {
            GameObject muzzleObject = new GameObject("Muzzle");
            muzzleObject.transform.SetParent(mountTransform, false);
            muzzleTransform = muzzleObject.transform;
        }

        muzzleTransform.localPosition = Vector3.zero;
        muzzleTransform.localRotation = Quaternion.identity;
        return muzzleTransform;
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

    private static void RefreshWeaponVisuals(List<WeaponDataSO> weapons, List<Transform> muzzles)
    {
        if (muzzles == null)
        {
            return;
        }

        for (int i = 0; i < muzzles.Count; i++)
        {
            WeaponDataSO weapon = weapons != null && i < weapons.Count ? weapons[i] : null;
            AttachWeaponVisual(weapon, muzzles[i]);
        }
    }

    private static void AttachWeaponVisual(WeaponDataSO weapon, Transform muzzleTransform)
    {
        Transform mountTransform = GetWeaponMountTransform(muzzleTransform);
        if (mountTransform == null)
        {
            return;
        }

        GameObject existingVisual = FindExistingWeaponVisual(mountTransform);
        if (existingVisual != null)
        {
            Destroy(existingVisual);
        }

        if (weapon == null || weapon.visualPrefab == null)
        {
            return;
        }

        if (weapon.projectilePrefab != null && weapon.visualPrefab == weapon.projectilePrefab)
        {
            return;
        }

        GameObject visual = Instantiate(weapon.visualPrefab, mountTransform);
        visual.name = "WeaponVisualInstance";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = weapon.visualPrefab.transform.localRotation;
        visual.transform.localScale = Vector3.one;
    }

    private static Transform GetWeaponMountTransform(Transform muzzleTransform)
    {
        if (muzzleTransform == null)
        {
            return null;
        }

        return muzzleTransform.parent != null ? muzzleTransform.parent : muzzleTransform;
    }

    private static GameObject FindExistingWeaponVisual(Transform mountTransform)
    {
        if (mountTransform == null)
        {
            return null;
        }

        for (int i = 0; i < mountTransform.childCount; i++)
        {
            Transform child = mountTransform.GetChild(i);
            if (string.Equals(child.name, "WeaponVisualInstance", StringComparison.OrdinalIgnoreCase))
            {
                return child.gameObject;
            }
        }

        return null;
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

        shieldVisual.Initialize(renderer, shieldHitMaterial, shieldFallbackSprite, baseColor, pulseOffset);
        return shieldVisual;
    }
}
