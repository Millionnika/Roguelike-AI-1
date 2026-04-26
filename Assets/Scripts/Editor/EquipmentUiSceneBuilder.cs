using System.Collections.Generic;
using System.IO;
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

    public static void Build()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>(true);
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

        SpaceCombatSceneController sceneController = Object.FindObjectOfType<SpaceCombatSceneController>(true);
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

            shipPrefab = PrefabUtility.SaveAsPrefabAsset(root, ExampleShipPrefabPath);
            Object.DestroyImmediate(root);
        }

        WeaponDataSO weaponData = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(ExampleWeaponPath);
        if (weaponData == null)
        {
            weaponData = ScriptableObject.CreateInstance<WeaponDataSO>();
            AssetDatabase.CreateAsset(weaponData, ExampleWeaponPath);
        }
        weaponData.damage = 34f;
        weaponData.fireRate = 0.35f;
        weaponData.projectileSpeed = 20f;
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
        shipData.weaponSlotCount = 3;
        shipData.moduleSlotCount = 4;
        shipData.damageMultiplier = 1f;
        shipData.repairMultiplier = 1f;
        shipData.shipPrefab = shipPrefab;
        EditorUtility.SetDirty(shipData);

        SpaceCombatSceneController controller = Object.FindObjectOfType<SpaceCombatSceneController>(true);
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

            SerializedProperty weaponProp = so.FindProperty("playerWeaponData");
            if (weaponProp != null)
            {
                weaponProp.objectReferenceValue = weaponData;
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

        string weaponDataPath = weaponDir + "/" + safeName + "_WeaponData.asset";
        WeaponDataSO weaponData = CreateFactoryWeaponData(
            weaponDataPath,
            safeName,
            requiredClass,
            projectilePrefab,
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
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return AssetDatabase.LoadAssetAtPath<SlotUI>(PrefabPath);
    }

    private static GameObject CreateFactoryShipPrefab(string prefabPath, string safeName, Sprite shipSprite)
    {
        GameObject root = new GameObject(safeName + "_ShipRoot");

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = shipSprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = 5;
        body.transform.localScale = new Vector3(0.42f, 0.55f, 1f);
        body.AddComponent<PolygonCollider2D>();

        GameObject aura = new GameObject("Aura");
        aura.transform.SetParent(root.transform, false);
        SpriteRenderer auraRenderer = aura.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = shipSprite;
        auraRenderer.color = new Color(0.42f, 0.9f, 1f, 0.28f);
        auraRenderer.sortingOrder = 4;
        aura.transform.localScale = new Vector3(0.66f, 0.8f, 1f);

        GameObject thruster = new GameObject("Thruster");
        thruster.transform.SetParent(root.transform, false);
        thruster.transform.localPosition = new Vector3(0f, -0.52f, 0f);
        SpriteRenderer thrusterRenderer = thruster.AddComponent<SpriteRenderer>();
        thrusterRenderer.sprite = shipSprite;
        thrusterRenderer.color = new Color(1f, 0.72f, 0.24f, 0.65f);
        thrusterRenderer.sortingOrder = 3;
        thruster.transform.localScale = new Vector3(0.22f, 0.28f, 1f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
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
        weaponData.damage = Mathf.Max(0f, damage);
        weaponData.fireRate = Mathf.Max(0.01f, fireRate);
        weaponData.projectileSpeed = Mathf.Max(0.01f, projectileSpeed);
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
