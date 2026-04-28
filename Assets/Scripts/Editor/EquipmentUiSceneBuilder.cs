using System.Collections.Generic;
using System.IO;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class EquipmentUiSceneBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/SlotUI.prefab";
    private const string ExampleShipPrefabPath = "Assets/Prefabs/Ships/PlayerShip_Example.prefab";
    private const string ExampleWeaponPath = "Assets/Data/Weapons/WD_ExamplePulse.asset";
    private const string ExampleShipDataPath = "Assets/Data/Ships/SD_ExampleVanguard.asset";
    private const string FactoryRootDir = "Assets/Content/Ships";
    private const string WeaponFactoryRootDir = "Assets/Content/Weapons";
    private const string RailgunVisualSpritePath = "Assets/Content/Weapons/Рельсотрончик/RailgunWeaponVisual.png";

    public static void Build()
    {
        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1400f, 900f);
            scaler.matchWidthOrHeight = 0.6f;
        }

        RectTransform panel = GetOrCreateRect("EquipmentPanel", canvas.transform);
        Image panelBg = panel.GetComponent<Image>();
        if (panelBg == null)
        {
            panelBg = panel.gameObject.AddComponent<Image>();
        }
        panelBg.color = new Color(0.03f, 0.07f, 0.1f, 0.86f);
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(-120f, 145f);
        panel.sizeDelta = new Vector2(420f, 130f);

        VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter panelFitter = panel.GetComponent<ContentSizeFitter>();
        if (panelFitter == null)
        {
            panelFitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        }
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        panelFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        RectTransform weaponsRow = GetOrCreateRect("WeaponsRow", panel);
        ConfigureRow(weaponsRow);

        RectTransform modulesRow = GetOrCreateRect("ModulesRow", panel);
        ConfigureRow(modulesRow);

        SlotUI slotPrefab = EnsureSlotPrefab();

        EquipmentUIController equipmentUi = panel.GetComponent<EquipmentUIController>();
        if (equipmentUi == null)
        {
            equipmentUi = panel.gameObject.AddComponent<EquipmentUIController>();
        }

        SpaceCombatSceneController sceneController = UnityEngine.Object.FindObjectOfType<SpaceCombatSceneController>(true);
        if (sceneController == null)
        {
            Debug.LogError("EquipmentUiSceneBuilder: SpaceCombatSceneController not found in scene.");
            return;
        }

        SerializedObject uiSo = new SerializedObject(equipmentUi);
        uiSo.FindProperty("sceneController").objectReferenceValue = sceneController;
        uiSo.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
        uiSo.FindProperty("weaponSlotsContainer").objectReferenceValue = weaponsRow;
        uiSo.FindProperty("moduleSlotsContainer").objectReferenceValue = modulesRow;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerSo = new SerializedObject(sceneController);
        controllerSo.FindProperty("equipmentUiController").objectReferenceValue = equipmentUi;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(sceneController.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("Equipment UI assembled in scene and references are wired.");
    }

    [MenuItem("Tools/Roguelike/Create New Ship")]
    public static void OpenShipFactoryWizard()
    {
        ScriptableWizard.DisplayWizard<ShipFactoryWizard>("Create New Ship", "Build Ship");
    }

    [MenuItem("Tools/Roguelike/Create New Weapon")]
    public static void OpenWeaponFactoryWizard()
    {
        ScriptableWizard.DisplayWizard<WeaponFactoryWizard>("Create New Weapon", "Build Weapon");
    }

    [MenuItem("Tools/Roguelike/Repair Existing Ship Prefabs")]
    public static void RepairExistingShipPrefabs()
    {
        Sprite railgunVisualSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RailgunVisualSpritePath);
        string[] guids = AssetDatabase.FindAssets("t:ShipDataSO", new[] { FactoryRootDir });
        int repairedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string dataPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            ShipDataSO shipData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(dataPath);
            if (shipData == null)
            {
                continue;
            }

            string prefabPath = shipData.shipPrefab != null
                ? AssetDatabase.GetAssetPath(shipData.shipPrefab)
                : GetExpectedShipPrefabPath(dataPath, shipData);
            if (string.IsNullOrEmpty(prefabPath))
            {
                continue;
            }

            RepairShipPrefab(prefabPath, shipData, railgunVisualSprite);
            repairedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EquipmentUiSceneBuilder: repaired " + repairedCount + " ship prefab(s).");
    }

    [MenuItem("Tools/Roguelike/Fix Ship Visual Helpers")]
    public static void FixShipVisualHelpers()
    {
        string[] guids = AssetDatabase.FindAssets("t:ShipDataSO", new[] { FactoryRootDir });
        int fixedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            ShipDataSO shipData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (shipData == null || shipData.shipPrefab == null)
            {
                continue;
            }

            string prefabPath = AssetDatabase.GetAssetPath(shipData.shipPrefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                continue;
            }

            FixShipVisualHelperPrefab(prefabPath, shipData);
            fixedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EquipmentUiSceneBuilder: fixed visual helpers for " + fixedCount + " ship prefab(s).");
    }

    private static string GetExpectedShipPrefabPath(string shipDataPath, ShipDataSO shipData)
    {
        string directory = Path.GetDirectoryName(shipDataPath)?.Replace("\\", "/");
        string safeName = SanitizeName(!string.IsNullOrWhiteSpace(shipData.displayName) ? shipData.displayName : shipData.name.Replace("_ShipData", string.Empty));
        return string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(safeName)
            ? string.Empty
            : directory + "/" + safeName + "_Prefab.prefab";
    }

    internal static void BuildShipFromFactory(string sourceName, Sprite shipSprite)
    {
        string safeName = SanitizeName(sourceName);
        if (string.IsNullOrWhiteSpace(safeName) || shipSprite == null)
        {
            EditorUtility.DisplayDialog("Ship Factory", "Enter valid ship name and sprite.", "OK");
            return;
        }

        EnsureFolder("Assets/Content");
        EnsureFolder(FactoryRootDir);
        string shipDir = FactoryRootDir + "/" + safeName;
        EnsureFolder(shipDir);

        string prefabPath = shipDir + "/" + safeName + "_Prefab.prefab";
        string shipDataPath = shipDir + "/" + safeName + "_ShipData.asset";

        GameObject prefab = CreateFactoryShipPrefab(prefabPath, safeName, shipSprite);
        ShipDataSO shipData = CreateFactoryShipData(shipDataPath, safeName, shipSprite, prefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = shipData;
        Debug.Log("Ship Factory: created ship at " + shipDir);
    }

    public static void CreateExampleShipWithWeapon()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Ships");
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/Ships");
        EnsureFolder("Assets/Data/Weapons");

        GameObject shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExampleShipPrefabPath);
        if (shipPrefab == null)
        {
            GameObject root = new GameObject("PlayerShip_Example");

            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.color = new Color(0.36f, 0.72f, 1f, 1f);
            bodyRenderer.sortingOrder = 5;
            body.transform.localScale = new Vector3(0.42f, 0.55f, 1f);

            GameObject aura = new GameObject("Aura");
            aura.transform.SetParent(root.transform, false);
            SpriteRenderer auraRenderer = aura.AddComponent<SpriteRenderer>();
            auraRenderer.color = new Color(0.36f, 1f, 0.9f, 0.45f);
            auraRenderer.sortingOrder = 4;
            aura.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

            GameObject thruster = new GameObject("Thruster");
            thruster.transform.SetParent(root.transform, false);
            thruster.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            SpriteRenderer thrusterRenderer = thruster.AddComponent<SpriteRenderer>();
            thrusterRenderer.color = new Color(1f, 0.72f, 0.2f, 0.85f);
            thrusterRenderer.sortingOrder = 3;
            thruster.transform.localScale = new Vector3(0.24f, 0.42f, 1f);

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.28f;
            EnsureShipPrefabPhysics(root);

            shipPrefab = PrefabUtility.SaveAsPrefabAsset(root, ExampleShipPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        WeaponDataSO weaponData = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(ExampleWeaponPath);
        if (weaponData == null)
        {
            weaponData = ScriptableObject.CreateInstance<WeaponDataSO>();
            AssetDatabase.CreateAsset(weaponData, ExampleWeaponPath);
        }
        weaponData.damage = 34f;
        weaponData.fireMode = FireMode.Projectile;
        weaponData.cooldown = 0.35f;
        weaponData.maxRange = 7f;
        weaponData.firingAngle = 360f;
        weaponData.spreadAngle = 0f;
        weaponData.fireRate = 0.35f;
        weaponData.projectileSpeed = 20f;
        weaponData.projectileMaxDistance = 8f;
        weaponData.projectileLifetime = 1.5f;
        weaponData.capacitorPerShot = 8f;
        weaponData.requiredClass = ShipClass.Light;
        EditorUtility.SetDirty(weaponData);

        ShipDataSO shipData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ExampleShipDataPath);
        if (shipData == null)
        {
            shipData = ScriptableObject.CreateInstance<ShipDataSO>();
            AssetDatabase.CreateAsset(shipData, ExampleShipDataPath);
        }
        shipData.displayName = "Vanguard";
        shipData.role = "Assault Frigate";
        shipData.roleRu = "Штурмовой фрегат";
        shipData.description = "Balanced combat hull with 3 gun slots.";
        shipData.descriptionRu = "Сбалансированный боевой корпус с 3 слотами оружия.";
        shipData.shipClass = ShipClass.Light;
        shipData.maxSpeed = 7.2f;
        shipData.acceleration = 12f;
        shipData.rotationSpeed = 9.5f;
        shipData.drag = 1.3f;
        shipData.maxShield = 420f;
        shipData.maxArmor = 280f;
        shipData.maxHull = 240f;
        shipData.capacitor = 1200f;
        shipData.capacitorRechargeTime = 85f;
        shipData.capacitorRechargeRate = 1.2f;
        shipData.scoreReward = 40;
        shipData.weaponSlotCount = 3;
        shipData.moduleSlotCount = 4;
        shipData.damageMultiplier = 1f;
        shipData.repairMultiplier = 1f;
        shipData.shipPrefab = shipPrefab;
        EditorUtility.SetDirty(shipData);

        SpaceCombatSceneController controller = UnityEngine.Object.FindObjectOfType<SpaceCombatSceneController>(true);
        if (controller != null)
        {
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty ships = so.FindProperty("availableShips");
            bool exists = false;
            for (int i = 0; i < ships.arraySize; i++)
            {
                if (ships.GetArrayElementAtIndex(i).objectReferenceValue == shipData)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                ships.InsertArrayElementAtIndex(ships.arraySize);
                ships.GetArrayElementAtIndex(ships.arraySize - 1).objectReferenceValue = shipData;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Example ship + weapon created. ShipData: " + ExampleShipDataPath);
    }

    internal static void BuildWeaponFromFactory(
        string sourceName,
        ShipClass requiredClass,
        GameObject projectilePrefab,
        Sprite icon,
        AudioClip fireSound,
        float damage,
        float fireRate,
        float projectileSpeed,
        float capacitorPerShot)
    {
        string safeName = SanitizeName(sourceName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            EditorUtility.DisplayDialog("Weapon Factory", "Enter valid weapon name.", "OK");
            return;
        }

        EnsureFolder("Assets/Content");
        EnsureFolder(WeaponFactoryRootDir);
        string weaponDir = WeaponFactoryRootDir + "/" + safeName;
        EnsureFolder(weaponDir);

        GameObject preparedProjectilePrefab = EnsureFactoryProjectilePrefab(weaponDir, safeName, projectilePrefab, icon);

        string weaponDataPath = weaponDir + "/" + safeName + "_WeaponData.asset";
        WeaponDataSO weaponData = CreateFactoryWeaponData(
            weaponDataPath,
            safeName,
            requiredClass,
            preparedProjectilePrefab,
            icon,
            fireSound,
            damage,
            fireRate,
            projectileSpeed,
            capacitorPerShot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = weaponData;
        Debug.Log("Weapon Factory: created weapon at " + weaponDir);
    }

    private static void ConfigureRow(RectTransform row)
    {
        row.anchorMin = new Vector2(0f, 0.5f);
        row.anchorMax = new Vector2(1f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(0f, 52f);

        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
        {
            hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = row.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static SlotUI EnsureSlotPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath) ?? "Assets/Prefabs/UI");

        SlotUI existing = AssetDatabase.LoadAssetAtPath<SlotUI>(PrefabPath);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject("SlotUI", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(SlotUI));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(72f, 72f);
        Image rootImage = root.GetComponent<Image>();
        rootImage.color = new Color(0.05f, 0.1f, 0.14f, 0.92f);
        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = 72f;
        layout.preferredHeight = 72f;
        layout.minWidth = 72f;
        layout.minHeight = 72f;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(36f, 36f);
        iconRect.anchoredPosition = new Vector2(0f, 4f);
        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.color = new Color(1f, 1f, 1f, 0.25f);

        GameObject keyGo = new GameObject("KeyBind", typeof(RectTransform), typeof(Text));
        keyGo.transform.SetParent(root.transform, false);
        RectTransform keyRect = keyGo.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0f, 1f);
        keyRect.anchorMax = new Vector2(1f, 1f);
        keyRect.pivot = new Vector2(0.5f, 1f);
        keyRect.anchoredPosition = new Vector2(0f, -4f);
        keyRect.sizeDelta = new Vector2(0f, 18f);
        Text keyText = keyGo.GetComponent<Text>();
        keyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        keyText.fontSize = 12;
        keyText.alignment = TextAnchor.MiddleCenter;
        keyText.color = Color.white;
        keyText.text = "1";

        GameObject cooldownGo = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(Image));
        cooldownGo.transform.SetParent(root.transform, false);
        RectTransform cooldownRect = cooldownGo.GetComponent<RectTransform>();
        cooldownRect.anchorMin = Vector2.zero;
        cooldownRect.anchorMax = Vector2.one;
        cooldownRect.offsetMin = Vector2.zero;
        cooldownRect.offsetMax = Vector2.zero;
        Image cooldownImage = cooldownGo.GetComponent<Image>();
        cooldownImage.color = new Color(0f, 0f, 0f, 0.55f);
        cooldownImage.type = Image.Type.Filled;
        cooldownImage.fillMethod = Image.FillMethod.Vertical;
        cooldownImage.fillOrigin = (int)Image.OriginVertical.Top;
        cooldownImage.fillAmount = 0f;
        cooldownGo.SetActive(false);

        SlotUI slotUi = root.GetComponent<SlotUI>();
        SerializedObject slotSo = new SerializedObject(slotUi);
        slotSo.FindProperty("iconImage").objectReferenceValue = iconImage;
        slotSo.FindProperty("keyBindText").objectReferenceValue = keyText;
        slotSo.FindProperty("cooldownOverlay").objectReferenceValue = cooldownImage;
        slotSo.FindProperty("backgroundImage").objectReferenceValue = rootImage;
        slotSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return AssetDatabase.LoadAssetAtPath<SlotUI>(PrefabPath);
    }

    private static GameObject CreateFactoryShipPrefab(string prefabPath, string safeName, Sprite shipSprite)
    {
        Sprite railgunVisualSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RailgunVisualSpritePath);
        GameObject root = new GameObject(safeName + "_ShipRoot");
        BuildCompleteShipHierarchy(root.transform, shipSprite, 2, railgunVisualSprite);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject EnsureFactoryProjectilePrefab(string weaponDir, string safeName, GameObject sourcePrefab, Sprite fallbackSprite)
    {
        string projectilePath = weaponDir + "/" + safeName + "_Projectile.prefab";
        string sourcePath = sourcePrefab != null ? AssetDatabase.GetAssetPath(sourcePrefab) : string.Empty;

        if (sourcePrefab == null)
        {
            return CreateOrUpdateProjectilePrefab(projectilePath, safeName, fallbackSprite);
        }

        if (!string.IsNullOrEmpty(sourcePath) && sourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            EnsureProjectilePrefabPhysics(root, fallbackSprite);
            PrefabUtility.SaveAsPrefabAsset(root, sourcePath);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        }

        GameObject instance = UnityEngine.Object.Instantiate(sourcePrefab);
        instance.name = safeName + "_Projectile";
        EnsureProjectilePrefabPhysics(instance, fallbackSprite);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, projectilePath);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    private static GameObject CreateOrUpdateProjectilePrefab(string projectilePath, string safeName, Sprite fallbackSprite)
    {
        bool exists = File.Exists(projectilePath);
        GameObject root = exists
            ? PrefabUtility.LoadPrefabContents(projectilePath)
            : new GameObject(safeName + "_Projectile");

        EnsureProjectilePrefabPhysics(root, fallbackSprite);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, projectilePath);

        if (exists)
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return prefab;
    }

    private static void EnsureProjectilePrefabPhysics(GameObject projectileRoot, Sprite fallbackSprite)
    {
        if (projectileRoot == null)
        {
            return;
        }

        SpriteRenderer renderer = projectileRoot.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = projectileRoot.AddComponent<SpriteRenderer>();
        }

        if (renderer.sprite == null && fallbackSprite != null)
        {
            renderer.sprite = fallbackSprite;
        }

        renderer.sortingOrder = 6;

        Rigidbody2D body = projectileRoot.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = projectileRoot.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        Collider2D collider = projectileRoot.GetComponent<Collider2D>();
        if (collider == null)
        {
            CircleCollider2D circle = projectileRoot.AddComponent<CircleCollider2D>();
            circle.radius = 0.08f;
            collider = circle;
        }

        collider.isTrigger = true;

        if (projectileRoot.transform.localScale == Vector3.zero)
        {
            projectileRoot.transform.localScale = Vector3.one;
        }
    }

    private static void EnsureShipPrefabPhysics(GameObject shipRoot)
    {
        if (shipRoot == null)
        {
            return;
        }

        Rigidbody2D body = shipRoot.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = shipRoot.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        Collider2D collider = shipRoot.GetComponentInChildren<Collider2D>(true);
        if (collider == null)
        {
            CircleCollider2D fallbackCollider = shipRoot.AddComponent<CircleCollider2D>();
            fallbackCollider.radius = 0.24f;
        }
    }

    private static void RepairShipPrefab(string prefabPath, ShipDataSO shipData, Sprite weaponVisualSprite)
    {
        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
        Sprite shipSprite = shipData != null ? shipData.shipIcon : null;
        GameObject root = new GameObject(prefabName);
        BuildCompleteShipHierarchy(root.transform, shipSprite, Mathf.Max(0, shipData.weaponSlotCount), weaponVisualSprite);
        AssetDatabase.DeleteAsset(prefabPath);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        if (shipData != null)
        {
            shipData.shipPrefab = prefab;
            Resize(shipData.startingWeapons, Mathf.Max(0, shipData.weaponSlotCount));
            Resize(shipData.startingModules, Mathf.Max(0, shipData.moduleSlotCount));
            EditorUtility.SetDirty(shipData);
        }
    }

    private static void BuildCompleteShipHierarchy(Transform root, Sprite shipSprite, int weaponSlotCount, Sprite weaponVisualSprite)
    {
        GameObject body = new GameObject("Body");
        body.transform.SetParent(root, false);
        body.transform.localScale = new Vector3(0.42f, 0.55f, 1f);
        SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = shipSprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = 5;
        body.AddComponent<PolygonCollider2D>();

        GameObject aura = new GameObject("Aura");
        aura.transform.SetParent(root, false);
        aura.transform.localScale = new Vector3(0.66f, 0.8f, 1f);
        SpriteRenderer auraRenderer = aura.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = shipSprite;
        auraRenderer.color = new Color(0.42f, 0.9f, 1f, 0.28f);
        auraRenderer.sortingOrder = 4;

        GameObject thruster = new GameObject("Thruster");
        thruster.transform.SetParent(root, false);
        thruster.transform.localPosition = new Vector3(0f, -0.52f, 0f);
        thruster.transform.localScale = new Vector3(0.22f, 0.28f, 1f);
        SpriteRenderer thrusterRenderer = thruster.AddComponent<SpriteRenderer>();
        thrusterRenderer.sprite = shipSprite;
        thrusterRenderer.color = new Color(1f, 0.72f, 0.24f, 0.65f);
        thrusterRenderer.sortingOrder = 3;

        CreateEngineFire(thruster.transform, "Engine_Fire_L", new Vector3(-0.22f, -0.58f, 0f));
        CreateEngineFire(thruster.transform, "Engine_Fire_R", new Vector3(0.22f, -0.58f, 0f));
        CreateWeaponSlots(root, weaponSlotCount, weaponVisualSprite);
    }

    private static void FixShipVisualHelperPrefab(string prefabPath, ShipDataSO shipData)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Sprite shipSprite = ResolveShipSpriteForPrefab(root.transform, shipData);
            EnsureSpriteRenderer(root.transform.Find("Aura"), shipSprite, new Color(0.42f, 0.9f, 1f, 0.28f), 4);
            EnsureSpriteRenderer(root.transform.Find("Thruster"), shipSprite, new Color(1f, 0.72f, 0.24f, 0.65f), 3);
            Transform thruster = root.transform.Find("Thruster");
            if (thruster != null)
            {
                EnsureEngineFireVisual(thruster, "Engine_Fire_L", new Vector3(-0.76f, 0f, 0f), shipSprite);
                EnsureEngineFireVisual(thruster, "Engine_Fire_R", new Vector3(0.76f, 0f, 0f), shipSprite);
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == "WeaponVisualInstance")
                {
                    children[i].localRotation = Quaternion.identity;
                    SpriteRenderer renderer = children[i].GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        renderer.sortingOrder = 9;
                    }
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Sprite ResolveShipSpriteForPrefab(Transform root, ShipDataSO shipData)
    {
        Transform body = root.Find("Body");
        SpriteRenderer bodyRenderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
        if (bodyRenderer != null && bodyRenderer.sprite != null)
        {
            return bodyRenderer.sprite;
        }

        return shipData != null ? shipData.shipIcon : null;
    }

    private static void EnsureSpriteRenderer(Transform transform, Sprite sprite, Color color, int sortingOrder)
    {
        if (transform == null)
        {
            return;
        }

        SpriteRenderer renderer = transform.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = transform.gameObject.AddComponent<SpriteRenderer>();
        }

        if (renderer.sprite == null)
        {
            renderer.sprite = sprite;
        }

        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
    }

    private static void CreateEngineFire(Transform parent, string name, Vector3 localPosition)
    {
        GameObject fire = new GameObject(name);
        fire.transform.SetParent(parent, false);
        fire.transform.localPosition = localPosition;
        fire.transform.localScale = new Vector3(0.22f, 0.42f, 1f);
        SpriteRenderer renderer = fire.AddComponent<SpriteRenderer>();
        SpriteRenderer parentRenderer = parent.GetComponent<SpriteRenderer>();
        renderer.sprite = parentRenderer != null ? parentRenderer.sprite : null;
        renderer.color = new Color(1f, 0.48f, 0.08f, 0.72f);
        renderer.sortingOrder = 2;
    }

    private static void EnsureEngineFireVisual(Transform parent, string name, Vector3 localPosition, Sprite sprite)
    {
        Transform fire = parent.Find(name);
        if (fire == null)
        {
            fire = new GameObject(name).transform;
            fire.SetParent(parent, false);
        }

        fire.localPosition = localPosition;
        fire.localRotation = Quaternion.identity;
        fire.localScale = new Vector3(0.22f, 0.42f, 1f);
        SpriteRenderer renderer = fire.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = fire.gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sprite;
        renderer.color = new Color(1f, 0.48f, 0.08f, 0.72f);
        renderer.sortingOrder = 2;
    }

    private static void CreateWeaponSlots(Transform root, int weaponSlotCount, Sprite weaponVisualSprite)
    {
        GameObject slotsRoot = new GameObject("WeaponSlots");
        slotsRoot.transform.SetParent(root, false);

        for (int i = 0; i < weaponSlotCount; i++)
        {
            float lerp = weaponSlotCount <= 1 ? 0.5f : i / (float)(weaponSlotCount - 1);
            float x = Mathf.Lerp(-0.38f, 0.38f, lerp);
            float y = Mathf.Lerp(0.58f, 0.66f, 1f - Mathf.Abs(lerp - 0.5f) * 2f);

            GameObject slot = new GameObject("WeaponSlot_" + (i + 1));
            slot.transform.SetParent(slotsRoot.transform, false);
            slot.transform.localPosition = new Vector3(x, y, 0f);

            GameObject mount = new GameObject("WeaponMount_" + (i + 1));
            mount.transform.SetParent(slot.transform, false);

            GameObject visual = new GameObject("WeaponVisualInstance");
            visual.transform.SetParent(mount.transform, false);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            SpriteRenderer visualRenderer = visual.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = weaponVisualSprite;
            visualRenderer.sortingOrder = 9;

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(mount.transform, false);
        }
    }

    private static ShipDataSO CreateFactoryShipData(string shipDataPath, string safeName, Sprite shipSprite, GameObject prefab)
    {
        ShipDataSO shipData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(shipDataPath);
        if (shipData == null)
        {
            shipData = ScriptableObject.CreateInstance<ShipDataSO>();
            AssetDatabase.CreateAsset(shipData, shipDataPath);
        }

        shipData.displayName = safeName;
        shipData.role = "Custom Hull";
        shipData.description = "Created by Ship Factory.";
        shipData.roleRu = "Пользовательский корпус";
        shipData.descriptionRu = "Создано через Ship Factory.";
        shipData.shipClass = ShipClass.Light;
        shipData.shipPrefab = prefab;
        shipData.shipIcon = shipSprite;
        shipData.scoreReward = shipData.scoreReward > 0 ? shipData.scoreReward : 40;
        shipData.weaponSlotCount = Mathf.Max(1, shipData.weaponSlotCount);
        shipData.moduleSlotCount = Mathf.Max(1, shipData.moduleSlotCount);
        shipData.startingWeapons ??= new List<WeaponDataSO>();
        shipData.startingModules ??= new List<ModuleDataSO>();
        Resize(shipData.startingWeapons, shipData.weaponSlotCount);
        Resize(shipData.startingModules, shipData.moduleSlotCount);
        EditorUtility.SetDirty(shipData);
        return shipData;
    }

    private static WeaponDataSO CreateFactoryWeaponData(
        string weaponDataPath,
        string safeName,
        ShipClass requiredClass,
        GameObject projectilePrefab,
        Sprite icon,
        AudioClip fireSound,
        float damage,
        float fireRate,
        float projectileSpeed,
        float capacitorPerShot)
    {
        WeaponDataSO weaponData = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(weaponDataPath);
        if (weaponData == null)
        {
            weaponData = ScriptableObject.CreateInstance<WeaponDataSO>();
            AssetDatabase.CreateAsset(weaponData, weaponDataPath);
        }

        weaponData.name = safeName + "_WeaponData";
        weaponData.requiredClass = requiredClass;
        weaponData.projectilePrefab = projectilePrefab;
        weaponData.icon = icon;
        weaponData.fireSound = fireSound;
        weaponData.fireMode = FireMode.Projectile;
        weaponData.damage = Mathf.Max(0f, damage);
        weaponData.cooldown = Mathf.Max(0.01f, fireRate);
        weaponData.fireRate = Mathf.Max(0.01f, fireRate);
        weaponData.maxRange = Mathf.Max(0.5f, weaponData.maxRange > 0f ? weaponData.maxRange : 8f);
        weaponData.firingAngle = Mathf.Clamp(weaponData.firingAngle <= 0f ? 360f : weaponData.firingAngle, 0f, 360f);
        weaponData.projectileSpeed = Mathf.Max(0.01f, projectileSpeed);
        weaponData.projectileMaxDistance = Mathf.Max(0.1f, weaponData.projectileMaxDistance > 0f ? weaponData.projectileMaxDistance : weaponData.maxRange);
        weaponData.projectileLifetime = Mathf.Max(0.1f, weaponData.projectileLifetime > 0f ? weaponData.projectileLifetime : weaponData.projectileMaxDistance / weaponData.projectileSpeed);
        weaponData.capacitorPerShot = Mathf.Max(0f, capacitorPerShot);
        EditorUtility.SetDirty(weaponData);
        return weaponData;
    }

    private static RectTransform GetOrCreateRect(string name, Transform parent)
    {
        Transform found = parent.Find(name);
        if (found != null)
        {
            return found as RectTransform;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string leaf = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    private static string SanitizeName(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        string value = source.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c.ToString(), string.Empty);
        }

        return value.Replace(" ", string.Empty);
    }

    private static void Resize<T>(List<T> list, int targetCount)
    {
        while (list.Count < targetCount)
        {
            list.Add(default(T));
        }

        while (list.Count > targetCount)
        {
            list.RemoveAt(list.Count - 1);
        }
    }
}

public sealed class ShipFactoryWizard : ScriptableWizard
{
    public string shipName = "NewShip";
    public Sprite shipSprite;

    private void OnWizardCreate()
    {
        EquipmentUiSceneBuilder.BuildShipFromFactory(shipName, shipSprite);
    }
}

public sealed class WeaponFactoryWizard : ScriptableWizard
{
    public string weaponName = "NewWeapon";
    public ShipClass requiredClass = ShipClass.Light;
    public GameObject projectilePrefab;
    public Sprite icon;
    public AudioClip fireSound;
    public float damage = 28f;
    public float fireRate = 0.45f;
    public float projectileSpeed = 18f;
    public float capacitorPerShot = 9f;

    private void OnWizardCreate()
    {
        EquipmentUiSceneBuilder.BuildWeaponFromFactory(
            weaponName,
            requiredClass,
            projectilePrefab,
            icon,
            fireSound,
            damage,
            fireRate,
            projectileSpeed,
            capacitorPerShot);
    }
}
