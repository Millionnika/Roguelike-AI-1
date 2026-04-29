using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Encounter Pool", fileName = "EncounterPool")]
public sealed class EncounterPoolSO : ScriptableObject
{
    [Header("Пул локаций")]
    [Tooltip("Все доступные локации для генерации следующих вариантов. Добавляйте сюда EncounterSO через drag-and-drop.")]
    public List<EncounterSO> encounters = new List<EncounterSO>();

    private void OnValidate()
    {
        if (encounters == null)
        {
            encounters = new List<EncounterSO>();
        }

        if (encounters.Count == 0)
        {
            Debug.LogWarning("EncounterPoolSO: пул локаций пуст. RunMapDirector не сможет сгенерировать варианты без EncounterSO.", this);
        }
    }

    public List<EncounterSO> GetEncountersByType(LocationNodeType type)
    {
        List<EncounterSO> results = new List<EncounterSO>();
        if (encounters == null)
        {
            return results;
        }

        for (int i = 0; i < encounters.Count; i++)
        {
            EncounterSO encounter = encounters[i];
            if (encounter != null && encounter.nodeType == type)
            {
                results.Add(encounter);
            }
        }

        return results;
    }

    public List<EncounterSO> GetValidEncounters(LocationNodeType type, int minDangerLevel, int maxDangerLevel)
    {
        List<EncounterSO> results = new List<EncounterSO>();
        if (encounters == null)
        {
            return results;
        }

        int minDanger = Mathf.Max(0, minDangerLevel);
        int maxDanger = Mathf.Max(minDanger, maxDangerLevel);
        for (int i = 0; i < encounters.Count; i++)
        {
            EncounterSO encounter = encounters[i];
            if (IsValidEncounter(encounter, type, minDanger, maxDanger))
            {
                results.Add(encounter);
            }
        }

        return results;
    }

    public List<EncounterSO> GetValidEncounters(int minDangerLevel, int maxDangerLevel)
    {
        List<EncounterSO> results = new List<EncounterSO>();
        if (encounters == null)
        {
            return results;
        }

        int minDanger = Mathf.Max(0, minDangerLevel);
        int maxDanger = Mathf.Max(minDanger, maxDangerLevel);
        for (int i = 0; i < encounters.Count; i++)
        {
            EncounterSO encounter = encounters[i];
            if (encounter != null && encounter.dangerLevel >= minDanger && encounter.dangerLevel <= maxDanger)
            {
                results.Add(encounter);
            }
        }

        return results;
    }

    public EncounterSO GetRandomEncounter(List<EncounterSO> source)
    {
        if (source == null || source.Count == 0)
        {
            return null;
        }

        return source[Random.Range(0, source.Count)];
    }

    private static bool IsValidEncounter(EncounterSO encounter, LocationNodeType type, int minDangerLevel, int maxDangerLevel)
    {
        return encounter != null &&
               encounter.nodeType == type &&
               encounter.dangerLevel >= minDangerLevel &&
               encounter.dangerLevel <= maxDangerLevel;
    }
}
