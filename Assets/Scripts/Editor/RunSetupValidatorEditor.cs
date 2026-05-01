using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RunSetupValidatorEditor
{
    [MenuItem("Tools/Roguelike/Validate Run Setup")]
    public static void ValidateRunSetup()
    {
        int warningCount = 0;

        RunMapDirector mapDirector = UnityEngine.Object.FindAnyObjectByType<RunMapDirector>(FindObjectsInactive.Include);
        RunEventDirector eventDirector = UnityEngine.Object.FindAnyObjectByType<RunEventDirector>(FindObjectsInactive.Include);
        EncounterFlowController flowController = UnityEngine.Object.FindAnyObjectByType<EncounterFlowController>(FindObjectsInactive.Include);

        if (mapDirector == null)
        {
            Warn(ref warningCount, "Run setup: в сцене не найден RunMapDirector. Генерация вариантов будет использовать fallback-список, если он есть.");
        }
        else
        {
            ValidatePoolAndConfig(ref warningCount, mapDirector.EncounterPool, mapDirector.Config, "RunMapDirector");
        }

        if (eventDirector == null)
        {
            Warn(ref warningCount, "Run setup: в сцене не найден RunEventDirector. Адаптация весов под состояние игрока будет отключена.");
        }
        else if (!eventDirector.IsConfigured)
        {
            Warn(ref warningCount, "Run setup: RunEventDirector найден, но EventDirectorConfigSO не назначен ни напрямую, ни через RunPresetSO.");
        }

        if (flowController == null)
        {
            Warn(ref warningCount, "Run setup: в сцене не найден EncounterFlowController. Цепочка encounter -> choices может не работать.");
        }
        else if (flowController.RunPreset != null)
        {
            ValidatePreset(ref warningCount, flowController.RunPreset);
        }

        ValidateAllRunPresets(ref warningCount);

        if (warningCount == 0)
        {
            Debug.Log("Проверка Run Setup завершена: критичных проблем настройки не найдено.");
        }
        else
        {
            Debug.LogWarning("Проверка Run Setup завершена. Количество предупреждений: " + warningCount + ".");
        }
    }

    private static void ValidateAllRunPresets(ref int warningCount)
    {
        string[] guids = AssetDatabase.FindAssets("t:RunPresetSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RunPresetSO preset = AssetDatabase.LoadAssetAtPath<RunPresetSO>(path);
            if (preset != null)
            {
                ValidatePreset(ref warningCount, preset);
            }
        }
    }

    private static void ValidatePreset(ref int warningCount, RunPresetSO preset)
    {
        if (preset == null)
        {
            return;
        }

        if (preset.encounterPool == null)
        {
            Warn(ref warningCount, "RunPreset '" + preset.name + "': не назначен EncounterPoolSO.");
        }

        if (preset.runMapConfig == null)
        {
            Warn(ref warningCount, "RunPreset '" + preset.name + "': не назначен RunMapConfigSO.");
        }

        if (preset.eventDirectorConfig == null)
        {
            Warn(ref warningCount, "RunPreset '" + preset.name + "': не назначен EventDirectorConfigSO. Это допустимо, если адаптация темпа пока не нужна.");
        }

        ValidatePoolAndConfig(ref warningCount, preset.encounterPool, preset.runMapConfig, "RunPreset '" + preset.name + "'");
    }

    private static void ValidatePoolAndConfig(ref int warningCount, EncounterPoolSO pool, RunMapConfigSO config, string ownerName)
    {
        if (pool == null)
        {
            Warn(ref warningCount, ownerName + ": не назначен EncounterPoolSO.");
            return;
        }

        if (pool.encounters == null || pool.encounters.Count == 0)
        {
            Warn(ref warningCount, ownerName + ": EncounterPoolSO пуст. Добавьте хотя бы один Combat encounter.");
            return;
        }

        bool hasCombatEncounter = HasEncounterOfType(pool, LocationNodeType.Combat);
        if (!hasCombatEncounter)
        {
            Warn(ref warningCount, ownerName + ": в EncounterPoolSO нет Combat encounter. Для текущего этапа нужен хотя бы один бой.");
        }

        ValidateCombatTimelines(ref warningCount, pool, ownerName);

        if (config == null)
        {
            Warn(ref warningCount, ownerName + ": не назначен RunMapConfigSO.");
            return;
        }

        if (GetWeight(config, LocationNodeType.Combat) <= 0f)
        {
            Warn(ref warningCount, ownerName + ": вес Combat равен 0. Сейчас рекомендуется использовать Combat, Repair и Rest.");
        }

        if (config.baseNodeWeights == null)
        {
            Warn(ref warningCount, ownerName + ": список весов типов локаций пуст.");
            return;
        }

        HashSet<LocationNodeType> checkedTypes = new HashSet<LocationNodeType>();
        for (int i = 0; i < config.baseNodeWeights.Count; i++)
        {
            NodeTypeWeight entry = config.baseNodeWeights[i];
            if (entry == null || entry.weight <= 0f || !checkedTypes.Add(entry.nodeType))
            {
                continue;
            }

            if (!HasEncounterOfType(pool, entry.nodeType))
            {
                Warn(ref warningCount, ownerName + ": тип " + entry.nodeType + " имеет вес " + entry.weight.ToString("0.##") + ", но в пуле нет EncounterSO этого типа.");
            }
        }
    }

    private static void ValidateCombatTimelines(ref int warningCount, EncounterPoolSO pool, string ownerName)
    {
        for (int i = 0; i < pool.encounters.Count; i++)
        {
            EncounterSO encounter = pool.encounters[i];
            if (encounter == null || !IsCombatNode(encounter.nodeType))
            {
                continue;
            }

            if (encounter.waveTimeline == null)
            {
                Warn(ref warningCount, ownerName + ": боевой encounter '" + encounter.name + "' не имеет WaveTimelineSO. Он не сможет запустить бой.");
            }
        }
    }

    private static bool HasEncounterOfType(EncounterPoolSO pool, LocationNodeType nodeType)
    {
        if (pool == null || pool.encounters == null)
        {
            return false;
        }

        for (int i = 0; i < pool.encounters.Count; i++)
        {
            EncounterSO encounter = pool.encounters[i];
            if (encounter != null && encounter.nodeType == nodeType)
            {
                return true;
            }
        }

        return false;
    }

    private static float GetWeight(RunMapConfigSO config, LocationNodeType nodeType)
    {
        if (config == null || config.baseNodeWeights == null)
        {
            return 0f;
        }

        for (int i = 0; i < config.baseNodeWeights.Count; i++)
        {
            NodeTypeWeight entry = config.baseNodeWeights[i];
            if (entry != null && entry.nodeType == nodeType)
            {
                return Mathf.Max(0f, entry.weight);
            }
        }

        return 0f;
    }

    private static bool IsCombatNode(LocationNodeType nodeType)
    {
        return nodeType == LocationNodeType.Combat ||
               nodeType == LocationNodeType.Elite ||
               nodeType == LocationNodeType.Boss;
    }

    private static void Warn(ref int warningCount, string message)
    {
        warningCount++;
        Debug.LogWarning(message);
    }
}
