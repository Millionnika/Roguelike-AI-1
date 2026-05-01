using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunMapDirector : MonoBehaviour
{
    [Header("Пресет забега")]
    [Tooltip("Единый пресет забега. Если назначен, EncounterPoolSO и RunMapConfigSO берутся из него. Прямые ссылки ниже остаются fallback для старых сцен.")]
    [SerializeField] private RunPresetSO runPreset;

    [Header("Настройки генерации")]
    [Tooltip("Базовая конфигурация карты забега: длина, количество вариантов, веса типов узлов, настройки босса и диапазон опасности.")]
    [SerializeField] private RunMapConfigSO config;
    [Tooltip("Пул доступных локаций. Директор выбирает только ассеты из этого списка и не хранит прогресс в самом пуле.")]
    [SerializeField] private EncounterPoolSO encounterPool;
    [Tooltip("Опциональный директор темпа. Если назначен и имеет конфиг, он меняет веса будущих вариантов до генерации выбора. Уже выбранная игроком локация не заменяется.")]
    [SerializeField] private RunEventDirector eventDirector;

    private readonly List<EncounterSO> candidateBuffer = new List<EncounterSO>();
    private readonly List<NodeTypeWeight> modifiedWeightBuffer = new List<NodeTypeWeight>();
    private readonly List<EncounterSO> testChoiceBuffer = new List<EncounterSO>();

    public RunPresetSO RunPreset => runPreset;
    public RunMapConfigSO Config => ActiveConfig;
    public EncounterPoolSO EncounterPool => ActiveEncounterPool;
    public RunEventDirector EventDirector => eventDirector;
    public bool IsConfigured => ActiveConfig != null && ActiveEncounterPool != null;

    private RunMapConfigSO ActiveConfig => runPreset != null && runPreset.runMapConfig != null ? runPreset.runMapConfig : config;
    private EncounterPoolSO ActiveEncounterPool => runPreset != null && runPreset.encounterPool != null ? runPreset.encounterPool : encounterPool;

    private void Awake()
    {
        EnsureEventDirectorReference();
    }

    public bool TryGenerateNextChoices(int completedEncounterCount, List<EncounterSO> results)
    {
        if (results == null)
        {
            return false;
        }

        results.Clear();
        if (!IsConfigured)
        {
            Debug.LogWarning("RunMapDirector: не назначены RunMapConfigSO или EncounterPoolSO. Генерация вариантов невозможна.", this);
            return false;
        }

        RunMapConfigSO activeConfig = ActiveConfig;
        EncounterPoolSO activePool = ActiveEncounterPool;

        if (activePool.encounters == null || activePool.encounters.Count == 0)
        {
            Debug.LogWarning("RunMapDirector: пул локаций пуст. Добавьте EncounterSO в пул или используйте резервный список Test Next Encounters.", this);
            return false;
        }

        if (!activeConfig.HasUsableWeights())
        {
            Debug.LogWarning("RunMapDirector: в RunMapConfigSO нет рабочих весов. Все веса равны нулю или список пуст.", this);
            return false;
        }

        int choiceCount = Mathf.Clamp(activeConfig.choicesPerStep, 1, 3);
        if (ShouldForceBoss(completedEncounterCount))
        {
            FillChoicesOfType(LocationNodeType.Boss, choiceCount, results);
            return results.Count > 0;
        }

        BuildModifiedWeights();
        int maxAttempts = choiceCount * 12;
        for (int attempt = 0; attempt < maxAttempts && results.Count < choiceCount; attempt++)
        {
            LocationNodeType nodeType = PickWeightedNodeType(modifiedWeightBuffer);
            EncounterSO encounter = PickEncounterOfType(nodeType, results);
            if (encounter != null)
            {
                results.Add(encounter);
            }
        }

        if (results.Count < choiceCount)
        {
            FillAnyValidChoices(choiceCount, results);
        }

        return results.Count > 0;
    }

    [ContextMenu("Сгенерировать тестовые варианты")]
    private void GenerateTestChoices()
    {
        testChoiceBuffer.Clear();
        bool generated = TryGenerateNextChoices(0, testChoiceBuffer);
        if (!generated)
        {
            Debug.LogWarning("RunMapDirector: тестовая генерация не дала вариантов. Проверьте конфиг, пул локаций, веса и диапазон опасности.", this);
            return;
        }

        for (int i = 0; i < testChoiceBuffer.Count; i++)
        {
            EncounterSO encounter = testChoiceBuffer[i];
            string encounterName = encounter != null && !string.IsNullOrWhiteSpace(encounter.displayName)
                ? encounter.displayName
                : encounter != null ? encounter.name : "Пусто";
            Debug.Log("RunMapDirector: тестовый вариант " + (i + 1) + " - " + encounterName + ".", this);
        }
    }

    private void EnsureEventDirectorReference()
    {
        if (eventDirector != null)
        {
            return;
        }

        eventDirector = FindAnyObjectByType<RunEventDirector>(FindObjectsInactive.Include);
    }

    private void BuildModifiedWeights()
    {
        modifiedWeightBuffer.Clear();
        RunMapConfigSO activeConfig = ActiveConfig;
        if (activeConfig == null || activeConfig.baseNodeWeights == null)
        {
            return;
        }

        EnsureEventDirectorReference();
        for (int i = 0; i < activeConfig.baseNodeWeights.Count; i++)
        {
            NodeTypeWeight source = activeConfig.baseNodeWeights[i];
            if (source == null)
            {
                continue;
            }

            float baseWeight = Mathf.Max(0f, source.weight);
            float modifiedWeight = eventDirector != null && eventDirector.IsConfigured
                ? eventDirector.ModifyNodeWeight(source.nodeType, baseWeight)
                : baseWeight;

            modifiedWeightBuffer.Add(new NodeTypeWeight
            {
                nodeType = source.nodeType,
                weight = modifiedWeight
            });
        }
    }

    private bool ShouldForceBoss(int completedEncounterCount)
    {
        RunMapConfigSO activeConfig = ActiveConfig;
        return activeConfig != null && activeConfig.forceBossAtEnd && completedEncounterCount >= activeConfig.bossNodeIndex;
    }

    private void FillChoicesOfType(LocationNodeType nodeType, int choiceCount, List<EncounterSO> results)
    {
        candidateBuffer.Clear();
        RunMapConfigSO activeConfig = ActiveConfig;
        EncounterPoolSO activePool = ActiveEncounterPool;
        if (activeConfig == null || activePool == null)
        {
            return;
        }

        List<EncounterSO> candidates = activePool.GetValidEncounters(nodeType, activeConfig.minDangerLevel, activeConfig.maxDangerLevel);
        AddCandidates(candidates);
        AddRandomUniqueChoices(choiceCount, results);
    }

    private EncounterSO PickEncounterOfType(LocationNodeType nodeType, List<EncounterSO> existingChoices)
    {
        candidateBuffer.Clear();
        RunMapConfigSO activeConfig = ActiveConfig;
        EncounterPoolSO activePool = ActiveEncounterPool;
        if (activeConfig == null || activePool == null)
        {
            return null;
        }

        List<EncounterSO> candidates = activePool.GetValidEncounters(nodeType, activeConfig.minDangerLevel, activeConfig.maxDangerLevel);
        for (int i = 0; i < candidates.Count; i++)
        {
            EncounterSO candidate = candidates[i];
            if (candidate != null && !ContainsEncounter(existingChoices, candidate))
            {
                candidateBuffer.Add(candidate);
            }
        }

        return PickRandomFromBuffer();
    }

    private void FillAnyValidChoices(int choiceCount, List<EncounterSO> results)
    {
        candidateBuffer.Clear();
        RunMapConfigSO activeConfig = ActiveConfig;
        EncounterPoolSO activePool = ActiveEncounterPool;
        if (activeConfig == null || activePool == null)
        {
            return;
        }

        List<EncounterSO> candidates = activePool.GetValidEncounters(activeConfig.minDangerLevel, activeConfig.maxDangerLevel);
        for (int i = 0; i < candidates.Count; i++)
        {
            EncounterSO candidate = candidates[i];
            if (candidate != null && !ContainsEncounter(results, candidate))
            {
                candidateBuffer.Add(candidate);
            }
        }

        AddRandomUniqueChoices(choiceCount, results);
    }

    private void AddCandidates(List<EncounterSO> candidates)
    {
        if (candidates == null)
        {
            return;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            EncounterSO candidate = candidates[i];
            if (candidate != null)
            {
                candidateBuffer.Add(candidate);
            }
        }
    }

    private void AddRandomUniqueChoices(int choiceCount, List<EncounterSO> results)
    {
        while (results.Count < choiceCount && candidateBuffer.Count > 0)
        {
            int index = Random.Range(0, candidateBuffer.Count);
            EncounterSO encounter = candidateBuffer[index];
            candidateBuffer.RemoveAt(index);
            if (encounter != null && !ContainsEncounter(results, encounter))
            {
                results.Add(encounter);
            }
        }
    }

    private EncounterSO PickRandomFromBuffer()
    {
        if (candidateBuffer.Count == 0)
        {
            return null;
        }

        return candidateBuffer[Random.Range(0, candidateBuffer.Count)];
    }

    private static LocationNodeType PickWeightedNodeType(List<NodeTypeWeight> weights)
    {
        if (weights == null || weights.Count == 0)
        {
            return LocationNodeType.Combat;
        }

        float totalWeight = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            NodeTypeWeight entry = weights[i];
            if (entry != null)
            {
                totalWeight += Mathf.Max(0f, entry.weight);
            }
        }

        if (totalWeight <= 0f)
        {
            return LocationNodeType.Combat;
        }

        float roll = Random.value * totalWeight;
        for (int i = 0; i < weights.Count; i++)
        {
            NodeTypeWeight entry = weights[i];
            if (entry == null || entry.weight <= 0f)
            {
                continue;
            }

            roll -= entry.weight;
            if (roll <= 0f)
            {
                return entry.nodeType;
            }
        }

        return LocationNodeType.Combat;
    }

    private static bool ContainsEncounter(List<EncounterSO> encounters, EncounterSO target)
    {
        if (encounters == null || target == null)
        {
            return false;
        }

        for (int i = 0; i < encounters.Count; i++)
        {
            if (encounters[i] == target)
            {
                return true;
            }
        }

        return false;
    }
}
