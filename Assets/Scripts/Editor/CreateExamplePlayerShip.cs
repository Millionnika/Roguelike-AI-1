using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateExamplePlayerShip
{
    private const string GeneratedArtDir = "Assets/Art/Generated";
    private const string PrefabDir = "Assets/Prefabs/Ships";
    private const string WeaponDir = "Assets/Data/Weapons";
    private const string ShipDir = "Assets/Data/Ships";

    private const string HullSpritePath = GeneratedArtDir + "/PlayerExample_Hull.png";
    private const string AuraSpritePath = GeneratedArtDir + "/PlayerExample_Aura.png";
    private const string ThrusterSpritePath = GeneratedArtDir + "/PlayerExample_Thruster.png";
    private const string IconSpritePath = GeneratedArtDir + "/PlayerExample_Icon.png";

    private const string ShipPrefabPath = PrefabDir + "/PlayerShip_Example.prefab";
    private const string WeaponAssetPath = WeaponDir + "/WD_ExamplePulse.asset";
    private const string ShipAssetPath = ShipDir + "/SD_ExampleVanguard.asset";

    public static void Create()
    {
        EnsureDirectory(GeneratedArtDir);
        EnsureDirectory(PrefabDir);
        EnsureDirectory(WeaponDir);
        EnsureDirectory(ShipDir);

        Sprite hullSprite = CreateSolidSprite(HullSpritePath, new Color(0.36f, 0.72f, 1f, 1f), 128);
        Sprite auraSprite = CreateSolidSprite(AuraSpritePath, new Color(0.36f, 1f, 0.9f, 0.45f), 128);
        Sprite thrusterSprite = CreateSolidSprite(ThrusterSpritePath, new Color(1f, 0.72f, 0.2f, 0.85f), 128);
        Sprite iconSprite = CreateSolidSprite(IconSpritePath, new Color(0.36f, 0.72f, 1f, 1f), 128);

        GameObject shipPrefab = CreateShipPrefab(hullSprite, auraSprite, thrusterSprite);
        WeaponDataSO weaponData = CreateOrUpdateWeaponData();
        ShipDataSO shipData = CreateOrUpdateShipData(shipPrefab, iconSprite);

        WireIntoScene(shipData, weaponData);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Example player ship created: " + ShipAssetPath);
    }

    private static GameObject CreateShipPrefab(Sprite hullSprite, Sprite auraSprite, Sprite thrusterSprite)
    {
        GameObject root = new GameObject("PlayerShip_Example");

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = hullSprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = 5;
        body.transform.localScale = new Vector3(0.42f, 0.55f, 1f);

        GameObject aura = new GameObject("Aura");
        aura.transform.SetParent(root.transform, false);
        SpriteRenderer auraRenderer = aura.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = auraSprite;
        auraRenderer.color = Color.white;
        auraRenderer.sortingOrder = 4;
        aura.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

        GameObject thruster = new GameObject("Thruster");
        thruster.transform.SetParent(root.transform, false);
        thruster.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        SpriteRenderer thrusterRenderer = thruster.AddComponent<SpriteRenderer>();
        thrusterRenderer.sprite = thrusterSprite;
        thrusterRenderer.color = Color.white;
        thrusterRenderer.sortingOrder = 3;
        thruster.transform.localScale = new Vector3(0.24f, 0.42f, 1f);

        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.radius = 0.28f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ShipPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static WeaponDataSO CreateOrUpdateWeaponData()
    {
        WeaponDataSO weapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(WeaponAssetPath);
        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponDataSO>();
            AssetDatabase.CreateAsset(weapon, WeaponAssetPath);
        }

        weapon.damage = 34f;
        weapon.fireMode = FireMode.Projectile;
        weapon.cooldown = 0.35f;
        weapon.maxRange = 7f;
        weapon.firingAngle = 360f;
        weapon.spreadAngle = 0f;
        weapon.fireRate = 0.35f;
        weapon.projectileSpeed = 20f;
        weapon.projectileMaxDistance = 8f;
        weapon.projectileLifetime = 1.5f;
        weapon.capacitorPerShot = 8f;
        weapon.requiredClass = ShipClass.Light;
        EditorUtility.SetDirty(weapon);
        return weapon;
    }

    private static ShipDataSO CreateOrUpdateShipData(GameObject shipPrefab, Sprite icon)
    {
        ShipDataSO ship = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ShipAssetPath);
        if (ship == null)
        {
            ship = ScriptableObject.CreateInstance<ShipDataSO>();
            AssetDatabase.CreateAsset(ship, ShipAssetPath);
        }

        ship.displayName = "Vanguard";
        ship.role = "Assault Frigate";
        ship.roleRu = "Штурмовой фрегат";
        ship.description = "Balanced combat hull with 3 gun slots.";
        ship.descriptionRu = "Сбалансированный боевой корпус с 3 слотами оружия.";
        ship.shipClass = ShipClass.Light;
        ship.maxSpeed = 7.2f;
        ship.acceleration = 12f;
        ship.rotationSpeed = 9.5f;
        ship.drag = 1.3f;
        ship.maxShield = 420f;
        ship.maxArmor = 280f;
        ship.maxHull = 240f;
        ship.capacitor = 1200f;
        ship.capacitorRechargeTime = 85f;
        ship.scoreReward = 40;
        ship.weaponSlotCount = 3;
        ship.moduleSlotCount = 4;
        ship.damageMultiplier = 1f;
        ship.repairMultiplier = 1f;
        ship.shipPrefab = shipPrefab;
        ship.shipIcon = icon;
        ship.accentColor = new Color(0.36f, 0.72f, 1f, 1f);
        ship.auraColor = new Color(0.36f, 1f, 0.9f, 0.65f);
        EditorUtility.SetDirty(ship);
        return ship;
    }

    private static void WireIntoScene(ShipDataSO shipData, WeaponDataSO weaponData)
    {
        SpaceCombatSceneController controller = Object.FindObjectOfType<SpaceCombatSceneController>(true);
        if (controller == null)
        {
            return;
        }

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

    private static void EnsureDirectory(string path)
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

        EnsureDirectory(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
