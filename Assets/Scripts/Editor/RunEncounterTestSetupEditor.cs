using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RunEncounterTestSetupEditor
{
    private const string RootFolder = "Assets/ScriptableObjects/Roguelike/Test";
    private const string EncountersFolder = RootFolder + "/Encounters";
    private const string ConfigsFolder = RootFolder + "/Configs";
    private const string PoolsFolder = RootFolder + "/Pools";

    private const string Combat1Path = EncountersFolder + "/Encounter_Combat_1.asset";
    private const string Combat2Path = EncountersFolder + "/Encounter_Combat_2.asset";
    private const string RepairPath = EncountersFolder + "/Encounter_Repair_Test.asset";
    private const string RestPath = EncountersFolder + "/Encounter_Rest_Test.asset";
    private const string EventPath = EncountersFolder + "/Encounter_Event_Test.asset";
    private const string PoolPath = PoolsFolder + "/EncounterPool_Test.asset";
    private const string RunMapConfigPath = ConfigsFolder + "/RunMapConfig_Test.asset";
    private const string EventDirectorConfigPath = ConfigsFolder + "/EventDirectorConfig_Test.asset";

    [MenuItem("Tools/Roguelike/Setup Test Run Encounter System")]
    public static void SetupTestRunEncounterSystem()
    {
        EnsureFolders();

        List<WaveTimelineSO> timelines = FindWaveTimelines();
        if (timelines.Count == 0)
        {
            Debug.LogWarning("WaveTimelineSO не найден. Назначьте таймлайн вручную в тестовых боевых EncounterSO.");
        }

        WaveTimelineSO firstTimeline = timelines.Count > 0 ? timelines[0] : null;
        WaveTimelineSO secondTimeline = timelines.Count > 1 ? timelines[1] : firstTimeline;

        EncounterSO combat1 = CreateOrUpdateEncounter(
            Combat1Path,
            "Encounter_Combat_1",
            "Тестовый бой 1",
            LocationNodeType.Combat,
            firstTimeline,
            1,
            "Простая тестовая боевая локация.");

        EncounterSO combat2 = CreateOrUpdateEncounter(
            Combat2Path,
            "Encounter_Combat_2",
            "Тестовый бой 2",
            LocationNodeType.Combat,
            secondTimeline,
            2,
            "Вторая тестовая боевая локация.");

        EncounterSO repair = CreateOrUpdateEncounter(
            RepairPath,
            "Encounter_Repair_Test",
            "Ремонтная станция",
            LocationNodeType.Repair,
            null,
            0,
            "Восстанавливает часть корпуса.");

        EncounterSO rest = CreateOrUpdateEncounter(
            RestPath,
            "Encounter_Rest_Test",
            "Тихий сектор",
            LocationNodeType.Rest,
            null,
            0,
            "Позволяет немного восстановиться перед следующим прыжком.");

        EncounterSO eventEncounter = CreateOrUpdateEncounter(
            EventPath,
            "Encounter_Event_Test",
            "Неизвестная аномалия",
            LocationNodeType.Event,
            null,
            0,
            "Тестовое событие без боя.");

        EncounterPoolSO pool = CreateOrUpdateEncounterPool(combat1, combat2, repair, rest, eventEncounter);
        RunMapConfigSO runMapConfig = CreateOrUpdateRunMapConfig();
        EventDirectorConfigSO eventDirectorConfig = CreateOrUpdateEventDirectorConfig();

        SetupSceneReferences(runMapConfig, pool, eventDirectorConfig, combat1, combat2, repair);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SaveActiveSceneIfPossible();

        Debug.Log("Тестовая система Run/Encounter настроена. Можно запускать сцену.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(EncountersFolder);
        EnsureFolder(ConfigsFolder);
        EnsureFolder(PoolsFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static List<WaveTimelineSO> FindWaveTimelines()
    {
        List<WaveTimelineSO> timelines = new List<WaveTimelineSO>();
        string[] guids = AssetDatabase.FindAssets("t:WaveTimelineSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            WaveTimelineSO timeline = AssetDatabase.LoadAssetAtPath<WaveTimelineSO>(path);
            if (timeline != null)
            {
                timelines.Add(timeline);
            }
        }

        return timelines;
    }

    private static EncounterSO CreateOrUpdateEncounter(
        string path,
        string assetName,
        string displayName,
        LocationNodeType nodeType,
        WaveTimelineSO timeline,
        int dangerLevel,
        string shortDescription)
    {
        EncounterSO encounter = LoadOrFindAsset<EncounterSO>(path, assetName);
        if (encounter == null)
        {
            encounter = ScriptableObject.CreateInstance<EncounterSO>();
            AssetDatabase.CreateAsset(encounter, path);
        }

        encounter.displayName = displayName;
        encounter.nodeType = nodeType;
        encounter.waveTimeline = timeline;
        encounter.dangerLevel = Mathf.Max(0, dangerLevel);
        encounter.shortDescription = shortDescription;
        EditorUtility.SetDirty(encounter);
        return encounter;
    }

    private static EncounterPoolSO CreateOrUpdateEncounterPool(params EncounterSO[] encounters)
    {
        EncounterPoolSO pool = LoadOrFindAsset<EncounterPoolSO>(PoolPath, "EncounterPool_Test");
        if (pool == null)
        {
            pool = ScriptableObject.CreateInstance<EncounterPoolSO>();
            AssetDatabase.CreateAsset(pool, PoolPath);
        }

        if (pool.encounters == null)
        {
            pool.encounters = new List<EncounterSO>();
        }

        for (int i = 0; i < encounters.Length; i++)
        {
            AddUnique(pool.encounters, encounters[i]);
        }

        EditorUtility.SetDirty(pool);
        return pool;
    }

    private static RunMapConfigSO CreateOrUpdateRunMapConfig()
    {
        RunMapConfigSO config = LoadOrFindAsset<RunMapConfigSO>(RunMapConfigPath, "RunMapConfig_Test");
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<RunMapConfigSO>();
            AssetDatabase.CreateAsset(config, RunMapConfigPath);
        }

        config.runLength = 10;
        config.choicesPerStep = 3;
        config.forceBossAtEnd = false;
        config.minDangerLevel = 0;
        config.maxDangerLevel = 10;
        SetNodeWeight(config, LocationNodeType.Combat, 70f);
        SetNodeWeight(config, LocationNodeType.Repair, 20f);
        SetNodeWeight(config, LocationNodeType.Rest, 10f);
        SetNodeWeight(config, LocationNodeType.Event, 10f);
        EditorUtility.SetDirty(config);
        return config;
    }

    private static EventDirectorConfigSO CreateOrUpdateEventDirectorConfig()
    {
        EventDirectorConfigSO config = LoadOrFindAsset<EventDirectorConfigSO>(EventDirectorConfigPath, "EventDirectorConfig_Test");
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<EventDirectorConfigSO>();
            AssetDatabase.CreateAsset(config, EventDirectorConfigPath);
        }

        config.lowHullThreshold = 0.35f;
        config.heavyDamageThreshold = 0.35f;
        config.combatStreakLimit = 3;
        config.repairWeightBoostWhenLowHull = 2f;
        config.shopWeightBoostWhenLowHull = 1.5f;
        config.resourceWeightBoostWhenLowHull = 1.5f;
        config.restWeightBoostWhenHighTension = 1.5f;
        config.eliteWeightBoostWhenPlayerStrong = 1.3f;
        config.combatWeightBoostWhenPlayerStrong = 1.2f;
        config.combatWeightPenaltyAfterCombatStreak = 0.5f;
        config.eliteWeightPenaltyWhenLowHull = 0.4f;
        config.difficultyGrowthPerCompletedNode = 0.1f;
        EditorUtility.SetDirty(config);
        return config;
    }

    private static void SetupSceneReferences(
        RunMapConfigSO runMapConfig,
        EncounterPoolSO pool,
        EventDirectorConfigSO eventDirectorConfig,
        EncounterSO combat1,
        EncounterSO combat2,
        EncounterSO repair)
    {
        SpaceCombatSceneController controller = UnityEngine.Object.FindAnyObjectByType<SpaceCombatSceneController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogWarning("SpaceCombatSceneController не найден в текущей сцене. Ассеты созданы, но ссылки в сцене не назначены.");
            return;
        }

        GameObject managersObject = FindSceneGameObject("GameManagers");
        if (managersObject == null)
        {
            managersObject = new GameObject("GameManagers");
            Undo.RegisterCreatedObjectUndo(managersObject, "Создать GameManagers");
        }

        RunManager runManager = GetOrAddComponent<RunManager>(managersObject);
        RunMapDirector runMapDirector = GetOrAddComponent<RunMapDirector>(managersObject);
        RunEventDirector runEventDirector = GetOrAddComponent<RunEventDirector>(managersObject);

        SerializedObject mapDirectorObject = new SerializedObject(runMapDirector);
        SetObjectReference(mapDirectorObject, "config", runMapConfig);
        SetObjectReference(mapDirectorObject, "encounterPool", pool);
        SetObjectReference(mapDirectorObject, "eventDirector", runEventDirector);
        mapDirectorObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject eventDirectorObject = new SerializedObject(runEventDirector);
        SetObjectReference(eventDirectorObject, "config", eventDirectorConfig);
        eventDirectorObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerObject = new SerializedObject(controller);
        SetObjectReference(controllerObject, "runManager", runManager);
        SetObjectReference(controllerObject, "runMapDirector", runMapDirector);
        SetObjectReference(controllerObject, "runEventDirector", runEventDirector);
        AddUniqueToSerializedObjectArray(controllerObject, "testNextEncounters", combat1);
        AddUniqueToSerializedObjectArray(controllerObject, "testNextEncounters", combat2);
        AddUniqueToSerializedObjectArray(controllerObject, "testNextEncounters", repair);
        controllerObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(runManager);
        EditorUtility.SetDirty(runMapDirector);
        EditorUtility.SetDirty(runEventDirector);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }

    private static T LoadOrFindAsset<T>(string path, string assetName) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        string[] guids = AssetDatabase.FindAssets(assetName + " t:" + typeof(T).Name);
        for (int i = 0; i < guids.Length; i++)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            T candidate = AssetDatabase.LoadAssetAtPath<T>(candidatePath);
            if (candidate != null && candidate.name == assetName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static void SetNodeWeight(RunMapConfigSO config, LocationNodeType type, float weight)
    {
        if (config.baseNodeWeights == null)
        {
            config.baseNodeWeights = new List<NodeTypeWeight>();
        }

        NodeTypeWeight firstEntry = null;
        for (int i = config.baseNodeWeights.Count - 1; i >= 0; i--)
        {
            NodeTypeWeight entry = config.baseNodeWeights[i];
            if (entry == null)
            {
                config.baseNodeWeights.RemoveAt(i);
                continue;
            }

            if (entry.nodeType != type)
            {
                continue;
            }

            if (firstEntry == null)
            {
                firstEntry = entry;
                continue;
            }

            config.baseNodeWeights.RemoveAt(i);
        }

        if (firstEntry == null)
        {
            config.baseNodeWeights.Add(new NodeTypeWeight
            {
                nodeType = type,
                weight = Mathf.Max(0f, weight)
            });
            return;
        }

        firstEntry.weight = Mathf.Max(0f, weight);
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning("Не найдено serialized-поле '" + propertyName + "' у объекта " + serializedObject.targetObject.name + ".");
            return;
        }

        property.objectReferenceValue = value;
    }

    private static void AddUniqueToSerializedObjectArray(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        if (value == null)
        {
            return;
        }

        SerializedProperty array = serializedObject.FindProperty(propertyName);
        if (array == null || !array.isArray)
        {
            Debug.LogWarning("Не найден список '" + propertyName + "' у объекта " + serializedObject.targetObject.name + ".");
            return;
        }

        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == value)
            {
                return;
            }
        }

        array.InsertArrayElementAtIndex(array.arraySize);
        array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = value;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return Undo.AddComponent<T>(target);
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].scene.IsValid() && objects[i].name == objectName)
            {
                return objects[i];
            }
        }

        return null;
    }

    private static void AddUnique<T>(List<T> list, T item) where T : UnityEngine.Object
    {
        if (list == null || item == null || list.Contains(item))
        {
            return;
        }

        list.Add(item);
    }

    private static void SaveActiveSceneIfPossible()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogWarning("Активная сцена невалидна. Сохраните сцену вручную после настройки.");
            return;
        }

        if (string.IsNullOrEmpty(activeScene.path))
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.LogWarning("Текущая сцена еще не сохранена в файл. Сохраните сцену вручную, чтобы сохранить ссылки на менеджеры.");
            return;
        }

        EditorSceneManager.SaveScene(activeScene);
    }
}
