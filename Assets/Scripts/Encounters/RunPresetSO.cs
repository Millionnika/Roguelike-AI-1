using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Run Preset", fileName = "RunPreset")]
public sealed class RunPresetSO : ScriptableObject
{
    [Header("Описание пресета")]
    [Tooltip("Понятное имя пресета забега для дизайнера. Не влияет на механику, используется только для навигации по ассетам.")]
    public string displayName = "Новый пресет забега";

    [Tooltip("Пул доступных локаций. Обязателен для генерации вариантов следующего прыжка через RunMapDirector.")]
    public EncounterPoolSO encounterPool;

    [Tooltip("Настройки генерации маршрута: сколько вариантов показывать, какие типы локаций чаще встречаются и какой диапазон опасности разрешен.")]
    public RunMapConfigSO runMapConfig;

    [Tooltip("Настройки директора темпа. Можно оставить пустым, тогда RunEventDirector использует свою прямую ссылку или отключит адаптацию сложности.")]
    public EventDirectorConfigSO eventDirectorConfig;

    [Tooltip("Стартовая локация забега. Необязательно. Если назначена, первый бой может брать таймлайн отсюда вместо ручного currentTimeline.")]
    public EncounterSO startingEncounter;

    [Header("Простой режим настройки")]
    [Tooltip("Включает простой дизайнерский режим: пресет становится главным местом, где видно таймлайны боев и базовые небоевые локации. Текущая генерация все равно использует EncounterPoolSO и RunMapConfigSO.")]
    public bool useSimpleMode = true;

    [Tooltip("Боевые таймлайны для быстрого понимания состава сектора. Эти ссылки не заменяют EncounterSO автоматически, но помогают держать все настройки забега в одном ассете.")]
    public List<WaveTimelineSO> combatTimelines = new List<WaveTimelineSO>();

    [Tooltip("Таймлайны элитных боев. На текущем этапе можно оставить пустым и поставить вес Elite равным 0.")]
    public List<WaveTimelineSO> eliteTimelines = new List<WaveTimelineSO>();

    [Tooltip("Ремонтные локации, которые можно добавлять в EncounterPoolSO. На текущем этапе это placeholder-локации без WaveTimeline.")]
    public List<EncounterSO> repairEncounters = new List<EncounterSO>();

    [Tooltip("Локации отдыха, которые можно добавлять в EncounterPoolSO. На текущем этапе это placeholder-локации без WaveTimeline.")]
    public List<EncounterSO> restEncounters = new List<EncounterSO>();

    [Tooltip("Событийные локации. Используйте только если достаточно текущей placeholder-панели события. Иначе оставьте пустым и поставьте вес Event равным 0.")]
    public List<EncounterSO> eventEncounters = new List<EncounterSO>();

    private void OnValidate()
    {
        ValidatePreset();
    }

    [ContextMenu("Настроить простой тестовый забег")]
    public void ConfigureSimpleTestRun()
    {
        useSimpleMode = true;

        if (runMapConfig == null)
        {
            Debug.LogWarning("RunPresetSO: RunMapConfigSO не назначен. Создайте или назначьте конфиг, затем повторите настройку простого забега.", this);
            return;
        }

        runMapConfig.choicesPerStep = 2;
        runMapConfig.forceBossAtEnd = false;
        runMapConfig.minDangerLevel = 0;
        runMapConfig.maxDangerLevel = Mathf.Max(runMapConfig.maxDangerLevel, 10);

        SetWeight(runMapConfig, LocationNodeType.Combat, 1f);
        SetWeight(runMapConfig, LocationNodeType.Repair, 0.25f);
        SetWeight(runMapConfig, LocationNodeType.Rest, 0.15f);
        SetWeight(runMapConfig, LocationNodeType.Event, HasEncountersOfType(LocationNodeType.Event) ? 0.15f : 0f);
        SetWeight(runMapConfig, LocationNodeType.Elite, HasEncountersOfType(LocationNodeType.Elite) ? 0.2f : 0f);
        SetWeight(runMapConfig, LocationNodeType.Shop, HasEncountersOfType(LocationNodeType.Shop) ? 0.1f : 0f);
        SetWeight(runMapConfig, LocationNodeType.Resource, HasEncountersOfType(LocationNodeType.Resource) ? 0.1f : 0f);
        SetWeight(runMapConfig, LocationNodeType.Boss, HasEncountersOfType(LocationNodeType.Boss) ? 0.1f : 0f);

        Debug.Log("RunPresetSO: простой тестовый забег настроен. Рекомендуемые активные типы сейчас: Combat, Repair, Rest.", this);
        ValidatePreset();
    }

    [ContextMenu("Проверить пресет забега")]
    public void CheckRunPreset()
    {
        ValidatePreset();
    }

    [ContextMenu("Исправить веса по пулу")]
    public void FixWeightsByPool()
    {
        if (runMapConfig == null)
        {
            Debug.LogWarning("RunPresetSO: нельзя исправить веса, потому что RunMapConfigSO не назначен.", this);
            return;
        }

        if (encounterPool == null)
        {
            Debug.LogWarning("RunPresetSO: нельзя исправить веса, потому что EncounterPoolSO не назначен.", this);
            return;
        }

        if (runMapConfig.baseNodeWeights == null)
        {
            runMapConfig.baseNodeWeights = new List<NodeTypeWeight>();
            Debug.LogWarning("RunPresetSO: список весов был пуст. Нечего исправлять.", this);
            return;
        }

        int fixedCount = 0;
        for (int i = 0; i < runMapConfig.baseNodeWeights.Count; i++)
        {
            NodeTypeWeight entry = runMapConfig.baseNodeWeights[i];
            if (entry == null || entry.weight <= 0f)
            {
                continue;
            }

            if (!HasEncountersOfType(entry.nodeType))
            {
                entry.weight = 0f;
                fixedCount++;
            }
        }

        Debug.Log("RunPresetSO: исправление весов завершено. Отключено типов без EncounterSO: " + fixedCount + ".", this);
        ValidatePreset();
    }

    public bool HasEncountersOfType(LocationNodeType nodeType)
    {
        if (encounterPool == null || encounterPool.encounters == null)
        {
            return false;
        }

        for (int i = 0; i < encounterPool.encounters.Count; i++)
        {
            EncounterSO encounter = encounterPool.encounters[i];
            if (encounter != null && encounter.nodeType == nodeType)
            {
                return true;
            }
        }

        return false;
    }

    public float GetNodeWeight(LocationNodeType nodeType)
    {
        if (runMapConfig == null || runMapConfig.baseNodeWeights == null)
        {
            return 0f;
        }

        for (int i = 0; i < runMapConfig.baseNodeWeights.Count; i++)
        {
            NodeTypeWeight entry = runMapConfig.baseNodeWeights[i];
            if (entry != null && entry.nodeType == nodeType)
            {
                return Mathf.Max(0f, entry.weight);
            }
        }

        return 0f;
    }

    public void ValidatePreset()
    {
        if (encounterPool == null)
        {
            Debug.LogWarning("RunPresetSO: не назначен EncounterPoolSO. Генератор не сможет выбрать следующие локации через этот пресет.", this);
        }

        if (runMapConfig == null)
        {
            Debug.LogWarning("RunPresetSO: не назначен RunMapConfigSO. Генератор не сможет понять веса типов локаций и количество вариантов.", this);
        }

        if (eventDirectorConfig == null)
        {
            Debug.LogWarning("RunPresetSO: не назначен EventDirectorConfigSO. Это допустимо, но адаптация вариантов под состояние игрока будет отключена или возьмется из прямой ссылки RunEventDirector.", this);
        }

        if (startingEncounter == null)
        {
            Debug.LogWarning("RunPresetSO: не назначен Starting Encounter. Это допустимо для ручного теста через currentTimeline, но для полноценного забега лучше указать стартовую локацию.", this);
        }

        if (encounterPool != null && !HasEncountersOfType(LocationNodeType.Combat))
        {
            Debug.LogWarning("RunPresetSO: в пуле нет боевых EncounterSO. Для текущего этапа нужен хотя бы один Combat encounter.", this);
        }

        if (runMapConfig != null && GetNodeWeight(LocationNodeType.Combat) <= 0f)
        {
            Debug.LogWarning("RunPresetSO: вес Combat равен 0. Для текущего этапа рекомендуется оставить Combat включенным.", this);
        }

        ValidateWeightedTypes();
        ValidateCombatTimelines();
    }

    private void ValidateWeightedTypes()
    {
        if (runMapConfig == null || runMapConfig.baseNodeWeights == null || encounterPool == null)
        {
            return;
        }

        for (int i = 0; i < runMapConfig.baseNodeWeights.Count; i++)
        {
            NodeTypeWeight entry = runMapConfig.baseNodeWeights[i];
            if (entry == null || entry.weight <= 0f)
            {
                continue;
            }

            if (!HasEncountersOfType(entry.nodeType))
            {
                Debug.LogWarning("RunPresetSO: тип " + entry.nodeType + " имеет вес больше 0, но в EncounterPoolSO нет подходящих EncounterSO. Такой вес будет тратить попытки генерации впустую.", this);
            }
        }
    }

    private void ValidateCombatTimelines()
    {
        if (encounterPool == null || encounterPool.encounters == null)
        {
            return;
        }

        for (int i = 0; i < encounterPool.encounters.Count; i++)
        {
            EncounterSO encounter = encounterPool.encounters[i];
            if (encounter == null || !IsCombatNode(encounter.nodeType))
            {
                continue;
            }

            if (encounter.waveTimeline == null)
            {
                Debug.LogWarning("RunPresetSO: боевой encounter '" + encounter.name + "' не имеет WaveTimelineSO. Такой бой не сможет запуститься.", this);
            }
        }
    }

    private static bool IsCombatNode(LocationNodeType nodeType)
    {
        return nodeType == LocationNodeType.Combat ||
               nodeType == LocationNodeType.Elite ||
               nodeType == LocationNodeType.Boss;
    }

    private static void SetWeight(RunMapConfigSO config, LocationNodeType nodeType, float weight)
    {
        if (config.baseNodeWeights == null)
        {
            config.baseNodeWeights = new List<NodeTypeWeight>();
        }

        NodeTypeWeight target = null;
        for (int i = config.baseNodeWeights.Count - 1; i >= 0; i--)
        {
            NodeTypeWeight entry = config.baseNodeWeights[i];
            if (entry == null)
            {
                config.baseNodeWeights.RemoveAt(i);
                continue;
            }

            if (entry.nodeType != nodeType)
            {
                continue;
            }

            if (target == null)
            {
                target = entry;
            }
            else
            {
                config.baseNodeWeights.RemoveAt(i);
            }
        }

        if (target == null)
        {
            config.baseNodeWeights.Add(new NodeTypeWeight { nodeType = nodeType, weight = Mathf.Max(0f, weight) });
            return;
        }

        target.weight = Mathf.Max(0f, weight);
    }
}
