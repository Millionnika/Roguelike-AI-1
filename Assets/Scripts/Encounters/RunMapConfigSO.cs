using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NodeTypeWeight
{
    [Tooltip("Тип локации, шанс которой настраивается этим весом.")]
    public LocationNodeType nodeType = LocationNodeType.Combat;
    [Tooltip("Базовый вес выбора этого типа. 0 выключает тип. Значения 0.1-3 обычно достаточно для тонкой настройки.")]
    [Min(0f)] public float weight = 1f;
}

[CreateAssetMenu(menuName = "Roguelike/Run Map Config", fileName = "RunMapConfig")]
public sealed class RunMapConfigSO : ScriptableObject
{
    [Header("Длина забега")]
    [Tooltip("Сколько локаций планируется пройти в забеге. Сейчас используется для определения позиции босса, полноценная карта появится позже.")]
    [Min(1)] public int runLength = 8;
    [Tooltip("Сколько вариантов следующей локации показывать игроку. Поддерживается от 1 до 3.")]
    [Range(1, 3)] public int choicesPerStep = 3;

    [Header("Веса типов локаций")]
    [Tooltip("Базовые веса типов локаций. RunMapDirector выбирает тип по этим весам, а RunEventDirector может дополнительно менять их по состоянию игрока.")]
    public List<NodeTypeWeight> baseNodeWeights = new List<NodeTypeWeight>
    {
        new NodeTypeWeight { nodeType = LocationNodeType.Combat, weight = 1f },
        new NodeTypeWeight { nodeType = LocationNodeType.Elite, weight = 0.2f },
        new NodeTypeWeight { nodeType = LocationNodeType.Resource, weight = 0.3f },
        new NodeTypeWeight { nodeType = LocationNodeType.Repair, weight = 0.15f },
        new NodeTypeWeight { nodeType = LocationNodeType.Event, weight = 0.25f }
    };

    [Header("Босс")]
    [Tooltip("Если включено, после указанного шага директор будет пытаться предлагать локации босса.")]
    public bool forceBossAtEnd = true;
    [Tooltip("Индекс шага, начиная с которого нужно форсировать босса. Например, 7 для финала забега длиной 8.")]
    [Min(0)] public int bossNodeIndex = 7;

    [Header("Диапазон опасности")]
    [Tooltip("Минимальный dangerLevel у EncounterSO, который можно предложить игроку.")]
    [Min(0)] public int minDangerLevel = 0;
    [Tooltip("Максимальный dangerLevel у EncounterSO, который можно предложить игроку.")]
    [Min(0)] public int maxDangerLevel = 99;

    private void OnValidate()
    {
        runLength = Mathf.Max(1, runLength);
        choicesPerStep = Mathf.Clamp(choicesPerStep, 1, 3);
        bossNodeIndex = Mathf.Clamp(bossNodeIndex, 0, Mathf.Max(0, runLength - 1));
        minDangerLevel = Mathf.Max(0, minDangerLevel);
        maxDangerLevel = Mathf.Max(minDangerLevel, maxDangerLevel);

        if (baseNodeWeights == null)
        {
            baseNodeWeights = new List<NodeTypeWeight>();
        }

        for (int i = 0; i < baseNodeWeights.Count; i++)
        {
            if (baseNodeWeights[i] != null)
            {
                baseNodeWeights[i].weight = Mathf.Max(0f, baseNodeWeights[i].weight);
            }
        }

        if (!HasUsableWeights())
        {
            Debug.LogWarning("RunMapConfigSO: нет ни одного веса больше нуля. RunMapDirector будет вынужден использовать резервную логику.", this);
        }
    }

    public bool HasUsableWeights()
    {
        if (baseNodeWeights == null)
        {
            return false;
        }

        for (int i = 0; i < baseNodeWeights.Count; i++)
        {
            NodeTypeWeight entry = baseNodeWeights[i];
            if (entry != null && entry.weight > 0f)
            {
                return true;
            }
        }

        return false;
    }
}
