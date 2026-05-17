using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EnemyBasePrefabBuilder
{
    private const string RootDir = "Assets/Content/Bases";
    private const string SharedDir = RootDir + "/Shared";
    private const string RaiderDir = RootDir + "/RaiderLair";
    private const string PirateDir = RootDir + "/PirateLair";
    private const string AlliedDir = RootDir + "/AlliedRepairBase";
    private const string BaseWeaponsDir = RootDir + "/Weapons";

    private const string RaiderPrefabPath = RaiderDir + "/RaiderLair_Prefab.prefab";
    private const string PiratePrefabPath = PirateDir + "/PirateLair_Prefab.prefab";
    private const string AlliedPrefabPath = AlliedDir + "/AlliedRepairBase_Prefab.prefab";
    private const string PirateBaseWeaponPath = BaseWeaponsDir + "/Base_Pirate_Missile_360.asset";
    private const string RaiderBaseWeaponPath = BaseWeaponsDir + "/Base_Raider_Railgun_360.asset";
    private const string AlliedBaseWeaponPath = BaseWeaponsDir + "/Base_Allied_Railgun_360.asset";

    private const string RaiderBodySpritePath = SharedDir + "/RaiderLair_Body.png";
    private const string PirateBodySpritePath = SharedDir + "/PirateLair_Body.png";
    private const string AlliedBodySpritePath = SharedDir + "/AlliedRepairBase_Body.png";
    private const string GlowSpritePath = SharedDir + "/Lair_Glow.png";

    private const string E1ShipDataPath = "Assets/Content/Ships/E1/E1_ShipData.asset";
    private const string PirateShipDataPath = "Assets/Content/Ships/AeXia/AeXia_ShipData.asset";
    private const string ShieldSpritePath = "Assets/Sprites/Shield.png";
    private const string SourceMissileWeaponPath = "Assets/Content/Weapons/Missile/Missile_WeaponData.asset";
    private const string SourceRailgunWeaponPath = "Assets/Content/Weapons/Рельсотрончик/Рельсотрончик_WeaponData.asset";
    private const string SourceMinigunWeaponPath = "Assets/Content/Weapons/Minigan505/Minigan505_WeaponData.asset";
    private const int EnemyBaseWeaponSlotCount = 4;
    private const int AlliedBaseWeaponSlotCount = 3;

    [MenuItem("Tools/Roguelike/Bases/Create Base Weapons (360)")]
    public static void CreateBaseWeapons()
    {
        EnsureFolder("Assets/Content");
        EnsureFolder(RootDir);
        EnsureFolder(BaseWeaponsDir);

        WeaponDataSO sourceMissile = LoadWeaponTemplate(SourceMissileWeaponPath);
        WeaponDataSO sourceRailgun = LoadWeaponTemplate(SourceRailgunWeaponPath);
        WeaponDataSO sourceMinigun = LoadWeaponTemplate(SourceMinigunWeaponPath);

        CreateOrUpdateBaseWeapon(
            PirateBaseWeaponPath,
            "Base_Pirate_Missile_360",
            sourceMissile != null ? sourceMissile : sourceMinigun,
            FireMode.Missile,
            damage: 22f,
            cooldown: 0.95f,
            range: 18f,
            aimTurn: 360f);

        CreateOrUpdateBaseWeapon(
            RaiderBaseWeaponPath,
            "Base_Raider_Railgun_360",
            sourceRailgun != null ? sourceRailgun : sourceMinigun,
            FireMode.Hitscan,
            damage: 34f,
            cooldown: 0.75f,
            range: 20f,
            aimTurn: 340f);

        CreateOrUpdateBaseWeapon(
            AlliedBaseWeaponPath,
            "Base_Allied_Railgun_360",
            sourceRailgun != null ? sourceRailgun : sourceMinigun,
            FireMode.Hitscan,
            damage: 28f,
            cooldown: 0.85f,
            range: 19f,
            aimTurn: 300f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EnemyBasePrefabBuilder: base weapons created/updated in " + BaseWeaponsDir);
    }

    [MenuItem("Tools/Roguelike/Bases/Create Base Prefabs")]
    public static void CreateBasePrefabs()
    {
        EnsureFolder("Assets/Content");
        EnsureFolder(RootDir);
        EnsureFolder(SharedDir);
        EnsureFolder(RaiderDir);
        EnsureFolder(PirateDir);
        EnsureFolder(AlliedDir);
        EnsureFolder(BaseWeaponsDir);

        CreateBaseWeapons();
        WeaponDataSO pirateBaseWeapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(PirateBaseWeaponPath);
        WeaponDataSO raiderBaseWeapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(RaiderBaseWeaponPath);
        WeaponDataSO alliedBaseWeapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(AlliedBaseWeaponPath);

        Sprite raiderBody = CreateSolidSprite(RaiderBodySpritePath, new Color(0.44f, 0.24f, 0.22f, 1f), 192);
        Sprite pirateBody = CreateSolidSprite(PirateBodySpritePath, new Color(0.2f, 0.28f, 0.42f, 1f), 192);
        Sprite alliedBody = CreateSolidSprite(AlliedBodySpritePath, new Color(0.18f, 0.42f, 0.34f, 1f), 192);
        Sprite glowSprite = CreateSolidSprite(GlowSpritePath, new Color(0.35f, 0.85f, 0.95f, 1f), 192);
        Sprite shieldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShieldSpritePath);

        ShipDataSO e1Ship = AssetDatabase.LoadAssetAtPath<ShipDataSO>(E1ShipDataPath);
        ShipDataSO pirateShip = AssetDatabase.LoadAssetAtPath<ShipDataSO>(PirateShipDataPath);

        BuildBasePrefab(
            RaiderPrefabPath,
            "RaiderLair",
            raiderBody,
            glowSprite,
            shieldSprite,
            e1Ship,
            maxShield: 1800f,
            maxArmor: 1400f,
            maxHull: 2600f,
            experienceReward: 480,
            spawnTriggerPercent: 10f,
            spawnCount: 4,
            spawnInterval: 1f,
            spawnMode: 1,
            baseWeaponData: raiderBaseWeapon);

        BuildBasePrefab(
            PiratePrefabPath,
            "PirateLair",
            pirateBody,
            glowSprite,
            shieldSprite,
            pirateShip,
            maxShield: 2400f,
            maxArmor: 1900f,
            maxHull: 3200f,
            experienceReward: 700,
            spawnTriggerPercent: 10f,
            spawnCount: 6,
            spawnInterval: 0.85f,
            spawnMode: 1,
            baseWeaponData: pirateBaseWeapon);

        BuildAlliedRepairBasePrefab(
            AlliedPrefabPath,
            alliedBody,
            glowSprite,
            alliedBaseWeapon);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EnemyBasePrefabBuilder: base prefabs created in " + RootDir);
    }

    [MenuItem("Tools/Roguelike/Bases/Rebuild Weapons + Base Prefabs")]
    public static void RebuildWeaponsAndBasePrefabs()
    {
        CreateBaseWeapons();
        CreateBasePrefabs();
    }

    private static void BuildAlliedRepairBasePrefab(string prefabPath, Sprite bodySprite, Sprite glowSprite, WeaponDataSO defenseWeaponData)
    {
        GameObject root = new GameObject("AlliedRepairBase_Prefab");

        CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 3.2f;

        AlliedRepairBase repairBase = root.AddComponent<AlliedRepairBase>();
        SerializedObject repairSo = new SerializedObject(repairBase);
        repairSo.FindProperty("healStrength").floatValue = 0.2f;
        repairSo.FindProperty("healCooldownSeconds").floatValue = 30f;
        repairSo.FindProperty("healRadius").floatValue = 3.2f;
        repairSo.FindProperty("defenseWeaponData").objectReferenceValue = defenseWeaponData;
        repairSo.ApplyModifiedPropertiesWithoutUndo();

        BaseDefenseBattery battery = root.AddComponent<BaseDefenseBattery>();
        battery.ConfigureFaction(CombatFaction.Player);
        Transform[] weaponMuzzles = CreateWeaponSlots(root.transform, AlliedBaseWeaponSlotCount, radius: 1.35f, arcStartDeg: -120f, arcEndDeg: 120f);
        List<WeaponDataSO> loadout = BuildRepeatedLoadout(defenseWeaponData, weaponMuzzles.Length);
        battery.ConfigureLoadout(loadout, weaponMuzzles);
        AttachWeaponVisuals(weaponMuzzles, defenseWeaponData);

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = bodySprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = 5;
        body.transform.localScale = new Vector3(1.8f, 1.8f, 1f);

        GameObject aura = new GameObject("Aura");
        aura.transform.SetParent(root.transform, false);
        SpriteRenderer auraRenderer = aura.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = glowSprite;
        auraRenderer.color = new Color(0.42f, 1f, 0.86f, 0.36f);
        auraRenderer.sortingOrder = 4;
        aura.transform.localScale = new Vector3(2.7f, 2.7f, 1f);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void BuildBasePrefab(
        string prefabPath,
        string baseName,
        Sprite bodySprite,
        Sprite glowSprite,
        Sprite shieldSprite,
        ShipDataSO enemyShipData,
        float maxShield,
        float maxArmor,
        float maxHull,
        int experienceReward,
        float spawnTriggerPercent,
        int spawnCount,
        float spawnInterval,
        int spawnMode,
        WeaponDataSO baseWeaponData)
    {
        GameObject root = new GameObject(baseName + "_Prefab");

        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = false;
        collider.radius = 1.25f;

        EnemyBaseLair lair = root.AddComponent<EnemyBaseLair>();
        BaseDefenseBattery battery = root.AddComponent<BaseDefenseBattery>();
        battery.ConfigureFaction(CombatFaction.Enemy);
        Transform[] weaponMuzzles = CreateWeaponSlots(root.transform, EnemyBaseWeaponSlotCount, radius: 1.6f, arcStartDeg: -160f, arcEndDeg: 160f);
        List<WeaponDataSO> loadout = BuildRepeatedLoadout(baseWeaponData, weaponMuzzles.Length);
        battery.ConfigureLoadout(loadout, weaponMuzzles);
        AttachWeaponVisuals(weaponMuzzles, baseWeaponData);

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = bodySprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = 5;
        body.transform.localScale = new Vector3(2.2f, 2.2f, 1f);

        GameObject core = new GameObject("Core");
        core.transform.SetParent(root.transform, false);
        SpriteRenderer coreRenderer = core.AddComponent<SpriteRenderer>();
        coreRenderer.sprite = glowSprite;
        coreRenderer.color = new Color(0.7f, 0.95f, 1f, 0.7f);
        coreRenderer.sortingOrder = 6;
        core.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

        GameObject aura = new GameObject("Aura");
        aura.transform.SetParent(root.transform, false);
        SpriteRenderer auraRenderer = aura.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = shieldSprite != null ? shieldSprite : glowSprite;
        auraRenderer.color = new Color(0.45f, 0.9f, 1f, 0.32f);
        auraRenderer.sortingOrder = 4;
        aura.transform.localScale = new Vector3(2.8f, 2.8f, 1f);

        GameObject spawnRoot = new GameObject("SpawnPoints");
        spawnRoot.transform.SetParent(root.transform, false);

        Transform[] points = new Transform[4];
        points[0] = CreateSpawnPoint(spawnRoot.transform, "Point_1", new Vector3(-2.4f, 1.7f, 0f));
        points[1] = CreateSpawnPoint(spawnRoot.transform, "Point_2", new Vector3(2.4f, 1.7f, 0f));
        points[2] = CreateSpawnPoint(spawnRoot.transform, "Point_3", new Vector3(-2.4f, -1.7f, 0f));
        points[3] = CreateSpawnPoint(spawnRoot.transform, "Point_4", new Vector3(2.4f, -1.7f, 0f));

        SerializedObject lairSo = new SerializedObject(lair);
        lairSo.FindProperty("maxShield").floatValue = maxShield;
        lairSo.FindProperty("maxArmor").floatValue = maxArmor;
        lairSo.FindProperty("maxHull").floatValue = maxHull;
        lairSo.FindProperty("experienceReward").intValue = experienceReward;
        lairSo.FindProperty("spawnTriggerDamagePercent").floatValue = spawnTriggerPercent;
        lairSo.FindProperty("spawnMode").enumValueIndex = spawnMode;
        lairSo.FindProperty("spawnEnemyCount").intValue = spawnCount;
        lairSo.FindProperty("spawnIntervalSeconds").floatValue = spawnInterval;
        lairSo.FindProperty("enemyShipData").objectReferenceValue = enemyShipData;
        lairSo.FindProperty("baseWeaponData").objectReferenceValue = baseWeaponData;
        lairSo.FindProperty("enemyPrefab").objectReferenceValue = null;
        lairSo.FindProperty("fallbackSpawnRadius").floatValue = 6f;
        lairSo.FindProperty("continuousSpawnEnabled").boolValue = true;
        lairSo.FindProperty("continuousSpawnCount").intValue = Mathf.Max(1, spawnCount / 2);
        lairSo.FindProperty("continuousSpawnIntervalSeconds").floatValue = Mathf.Max(2f, spawnInterval * 6f);
        lairSo.FindProperty("continuousSpawnStartDelay").floatValue = 2f;
        SerializedProperty pointsProperty = lairSo.FindProperty("spawnPoints");
        pointsProperty.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
        {
            pointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        }
        lairSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static WeaponDataSO LoadWeaponTemplate(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<WeaponDataSO>(path);
    }

    private static List<WeaponDataSO> BuildRepeatedLoadout(WeaponDataSO weapon, int count)
    {
        List<WeaponDataSO> loadout = new List<WeaponDataSO>(Mathf.Max(0, count));
        for (int i = 0; i < count; i++)
        {
            loadout.Add(weapon);
        }

        return loadout;
    }

    private static Transform[] CreateWeaponSlots(Transform root, int slotCount, float radius, float arcStartDeg, float arcEndDeg)
    {
        GameObject slotsRoot = new GameObject("WeaponSlots");
        slotsRoot.transform.SetParent(root, false);

        int safeCount = Mathf.Max(1, slotCount);
        Transform[] muzzles = new Transform[safeCount];
        for (int i = 0; i < safeCount; i++)
        {
            float t = safeCount == 1 ? 0.5f : i / (float)(safeCount - 1);
            float angle = Mathf.Lerp(arcStartDeg, arcEndDeg, t);
            Vector3 dir = Quaternion.Euler(0f, 0f, angle) * Vector3.up;

            GameObject slot = new GameObject("WeaponSlot_" + (i + 1));
            slot.transform.SetParent(slotsRoot.transform, false);
            slot.transform.localPosition = dir * radius;
            slot.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            GameObject mount = new GameObject("WeaponMount_" + (i + 1));
            mount.transform.SetParent(slot.transform, false);
            mount.transform.localPosition = Vector3.zero;
            mount.transform.localRotation = Quaternion.identity;

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(mount.transform, false);
            muzzle.transform.localPosition = Vector3.up * 0.32f;
            muzzle.transform.localRotation = Quaternion.identity;
            muzzles[i] = muzzle.transform;
        }

        return muzzles;
    }

    private static void AttachWeaponVisuals(IReadOnlyList<Transform> muzzles, WeaponDataSO weaponData)
    {
        if (muzzles == null || weaponData == null || weaponData.visualPrefab == null)
        {
            return;
        }

        for (int i = 0; i < muzzles.Count; i++)
        {
            Transform muzzle = muzzles[i];
            if (muzzle == null)
            {
                continue;
            }

            Transform mount = muzzle.parent != null ? muzzle.parent : muzzle;
            Transform existing = mount.Find("WeaponVisualInstance");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject visual = PrefabUtility.InstantiatePrefab(weaponData.visualPrefab) as GameObject;
            if (visual == null)
            {
                continue;
            }

            visual.name = "WeaponVisualInstance";
            visual.transform.SetParent(mount, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
        }
    }

    private static void CreateOrUpdateBaseWeapon(
        string path,
        string assetName,
        WeaponDataSO template,
        FireMode fireMode,
        float damage,
        float cooldown,
        float range,
        float aimTurn)
    {
        WeaponDataSO weapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(path);
        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponDataSO>();
            AssetDatabase.CreateAsset(weapon, path);
        }

        Undo.RecordObject(weapon, "Update Base Weapon");

        if (template != null)
        {
            EditorUtility.CopySerialized(template, weapon);
        }

        weapon.name = assetName;
        weapon.fireMode = fireMode;
        weapon.damage = Mathf.Max(1f, damage);
        weapon.cooldown = Mathf.Max(0.05f, cooldown);
        weapon.fireRate = weapon.cooldown;
        weapon.maxRange = Mathf.Max(2f, range);
        weapon.projectileMaxDistance = Mathf.Max(weapon.maxRange, weapon.projectileMaxDistance);
        weapon.firingAngle = 360f;
        weapon.spreadAngle = Mathf.Clamp(weapon.spreadAngle, 0f, 8f);
        weapon.aimTurnSpeed = Mathf.Max(60f, aimTurn);

        if (weapon.fireMode == FireMode.Missile)
        {
            weapon.missileSeekRadius = Mathf.Max(weapon.missileSeekRadius, weapon.maxRange + 3f);
            weapon.missileTurnSpeed = Mathf.Max(weapon.missileTurnSpeed, 220f);
        }

        EditorUtility.SetDirty(weapon);
    }

    private static Transform CreateSpawnPoint(Transform parent, string pointName, Vector3 localPosition)
    {
        GameObject point = new GameObject(pointName);
        point.transform.SetParent(parent, false);
        point.transform.localPosition = localPosition;
        return point.transform;
    }

    private static Sprite CreateSolidSprite(string path, Color color, int size)
    {
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent))
        {
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
