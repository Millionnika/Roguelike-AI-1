using System.IO;
using UnityEditor;
using UnityEngine;

public static class EnemyBasePrefabBuilder
{
    private const string RootDir = "Assets/Content/Bases";
    private const string SharedDir = RootDir + "/Shared";
    private const string RaiderDir = RootDir + "/RaiderLair";
    private const string PirateDir = RootDir + "/PirateLair";

    private const string RaiderPrefabPath = RaiderDir + "/RaiderLair_Prefab.prefab";
    private const string PiratePrefabPath = PirateDir + "/PirateLair_Prefab.prefab";

    private const string RaiderBodySpritePath = SharedDir + "/RaiderLair_Body.png";
    private const string PirateBodySpritePath = SharedDir + "/PirateLair_Body.png";
    private const string GlowSpritePath = SharedDir + "/Lair_Glow.png";

    private const string E1ShipDataPath = "Assets/Content/Ships/E1/E1_ShipData.asset";
    private const string ZoozShipDataPath = "Assets/Content/Ships/Zooz/Zooz_ShipData.asset";
    private const string ShieldSpritePath = "Assets/Sprites/Shield.png";

    [MenuItem("Tools/Roguelike/Create Base Prefabs")]
    public static void CreateBasePrefabs()
    {
        EnsureFolder("Assets/Content");
        EnsureFolder(RootDir);
        EnsureFolder(SharedDir);
        EnsureFolder(RaiderDir);
        EnsureFolder(PirateDir);

        Sprite raiderBody = CreateSolidSprite(RaiderBodySpritePath, new Color(0.44f, 0.24f, 0.22f, 1f), 192);
        Sprite pirateBody = CreateSolidSprite(PirateBodySpritePath, new Color(0.2f, 0.28f, 0.42f, 1f), 192);
        Sprite glowSprite = CreateSolidSprite(GlowSpritePath, new Color(0.35f, 0.85f, 0.95f, 1f), 192);
        Sprite shieldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShieldSpritePath);

        ShipDataSO e1Ship = AssetDatabase.LoadAssetAtPath<ShipDataSO>(E1ShipDataPath);
        ShipDataSO zoozShip = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ZoozShipDataPath);

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
            spawnMode: 1);

        BuildBasePrefab(
            PiratePrefabPath,
            "PirateLair",
            pirateBody,
            glowSprite,
            shieldSprite,
            zoozShip,
            maxShield: 2400f,
            maxArmor: 1900f,
            maxHull: 3200f,
            experienceReward: 700,
            spawnTriggerPercent: 10f,
            spawnCount: 6,
            spawnInterval: 0.85f,
            spawnMode: 1);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EnemyBasePrefabBuilder: base prefabs created in " + RootDir);
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
        int spawnMode)
    {
        GameObject root = new GameObject(baseName + "_Prefab");

        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = false;
        collider.radius = 1.25f;

        EnemyBaseLair lair = root.AddComponent<EnemyBaseLair>();

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
        lairSo.FindProperty("enemyPrefab").objectReferenceValue = null;
        lairSo.FindProperty("fallbackSpawnRadius").floatValue = 6f;
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
