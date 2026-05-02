using System.IO;
using UnityEditor;
using UnityEngine;

public static class EngineVfxPrefabFactory
{
    internal const string VfxFolder = "Assets/Content/VFX";
    internal const string EngineFolder = "Assets/Content/VFX/Engine";

    [MenuItem("Tools/Roguelike/VFX/Create Engine VFX Prefab")]
    public static void OpenWizard()
    {
        ScriptableWizard.DisplayWizard<EngineVfxPrefabWizard>("Create Engine VFX Prefab", "Create Prefab");
    }

    internal static void CreateEngineVfxPrefab(EngineVfxPrefabWizard settings)
    {
        string safeName = SanitizeName(settings.vfxName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            EditorUtility.DisplayDialog("Engine VFX", "Укажите VFX Name.", "OK");
            return;
        }

        EnsureFolder("Assets/Content", "VFX");
        EnsureFolder(VfxFolder, "Engine");

        string prefabPath = EngineFolder + "/" + safeName + ".prefab";
        string materialPath = EngineFolder + "/" + safeName + "_Material.mat";
        bool prefabExists = File.Exists(prefabPath);
        bool materialExists = File.Exists(materialPath);
        if ((prefabExists || materialExists) && !EditorUtility.DisplayDialog(
                "Обновить Engine VFX?",
                "Prefab или material с именем '" + safeName + "' уже существует. Обновить их? Если вы вручную настраивали эффект, изменения могут быть перезаписаны.",
                "Обновить",
                "Отмена"))
        {
            Debug.Log("Создание Engine VFX prefab отменено. Существующие ассеты не изменены.");
            return;
        }

        Material material = CreateOrUpdateMaterial(materialPath, safeName, settings.color);
        GameObject root = new GameObject(safeName);
        ParticleSystem particleSystem = root.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(particleSystem, settings);

        ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = settings.sortingOrder;
        renderer.maxParticleSize = 0.25f;
        renderer.material = material;

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Префаб эффекта двигателя " + safeName + " создан. Назначьте его в ShipDataSO -> Эффекты двигателя -> Engine VFX Prefab.");
    }

    private static Material CreateOrUpdateMaterial(string materialPath, string safeName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        material.name = safeName + "_Material";
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureParticleSystem(ParticleSystem particleSystem, EngineVfxPrefabWizard settings)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = Mathf.Max(1, settings.maxParticles);
        main.startLifetime = Mathf.Max(0.01f, settings.startLifetime);
        main.startSpeed = Mathf.Max(0f, settings.startSpeed);
        main.startSize = Mathf.Max(0.01f, settings.startSize);
        main.startColor = settings.color;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, settings.emissionRate);

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.015f;
        shape.rotation = new Vector3(180f, 0f, 0f);

        ParticleSystem.CollisionModule collision = particleSystem.collision;
        collision.enabled = false;

        ParticleSystem.LightsModule lights = particleSystem.lights;
        lights.enabled = false;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c.ToString(), string.Empty);
        }

        return value.Replace(" ", string.Empty);
    }
}

public sealed class EngineVfxPrefabWizard : ScriptableWizard
{
    public string vfxName = "EngineVFX_Default";
    public Color color = new Color(0.25f, 0.85f, 1f, 0.85f);
    public float startSize = 0.12f;
    public float startLifetime = 0.35f;
    public float startSpeed = 0.6f;
    public float emissionRate = 15f;
    public int maxParticles = 40;
    public int sortingOrder = -3;

    private void OnWizardUpdate()
    {
        if (string.IsNullOrWhiteSpace(vfxName))
        {
            errorString = "Укажите VFX Name.";
            isValid = false;
            return;
        }

        startSize = Mathf.Max(0.01f, startSize);
        startLifetime = Mathf.Max(0.01f, startLifetime);
        startSpeed = Mathf.Max(0f, startSpeed);
        emissionRate = Mathf.Max(0f, emissionRate);
        maxParticles = Mathf.Max(1, maxParticles);

        errorString = string.Empty;
        helpString = "Создаёт редактируемый prefab эффекта двигателя в Assets/Content/VFX/Engine/<VFXName>.prefab.";
        isValid = true;
    }

    private void OnWizardCreate()
    {
        EngineVfxPrefabFactory.CreateEngineVfxPrefab(this);
    }
}
