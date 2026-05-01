using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunEventDirector : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Конфиг темпа забега. Хранит все пороги и множители весов, которые влияют на будущие варианты локаций.")]
    [SerializeField] private EventDirectorConfigSO config;

    [Header("Отладка / состояние забега")]
    [Tooltip("Текущее напряжение забега: низкое повышает шанс опасных узлов, высокое повышает шанс восстановления.")]
    [SerializeField] private Tension currentTension = Tension.Normal;
    [Tooltip("Сколько боевых локаций подряд завершено. При достижении лимита директор снижает шанс обычного и элитного боя.")]
    [SerializeField, Min(0)] private int combatStreak;
    [Tooltip("Сколько локаций завершено в текущем забеге.")]
    [SerializeField, Min(0)] private int completedNodeCount;
    [Tooltip("Доля корпуса игрока после последней завершенной локации.")]
    [SerializeField, Range(0f, 1f)] private float lastPlayerHullPercent = 1f;
    [Tooltip("Доля корпуса, потерянная за последнюю локацию. Сейчас считается контроллером боя на основе процента корпуса в начале и конце локации.")]
    [SerializeField, Range(0f, 1f)] private float lastDamageTaken;
    [Tooltip("Тип последней завершенной локации.")]
    [SerializeField] private LocationNodeType lastCompletedNodeType = LocationNodeType.Combat;

    private readonly HashSet<LocationNodeType> loggedWeightNodesThisFrame = new HashSet<LocationNodeType>();
    private int loggedWeightFrame = -1;

    public bool IsConfigured => config != null;
    public int CombatStreak => combatStreak;
    public int CompletedNodeCount => completedNodeCount;
    public float LastPlayerHullPercent => lastPlayerHullPercent;
    public float LastDamageTaken => lastDamageTaken;
    public LocationNodeType LastCompletedNodeType => lastCompletedNodeType;

    public void OnEncounterCompleted(EncounterResult result)
    {
        if (config == null)
        {
            return;
        }

        completedNodeCount++;
        lastCompletedNodeType = result.completedNodeType;
        lastPlayerHullPercent = Mathf.Clamp01(result.playerHullPercent);
        lastDamageTaken = Mathf.Clamp01(result.damageTaken);

        int previousStreak = combatStreak;
        combatStreak = IsCombatNode(result.completedNodeType) ? combatStreak + 1 : 0;
        if (combatStreak != previousStreak)
        {
            Debug.Log("RunEventDirector: серия боевых локаций изменена: " + combatStreak + ".", this);
        }

        Tension previousTension = currentTension;
        currentTension = CalculateTension(lastPlayerHullPercent, lastDamageTaken);
        if (currentTension != previousTension)
        {
            Debug.Log("RunEventDirector: напряжение изменено: " + GetTensionDisplayName(previousTension) + " -> " + GetTensionDisplayName(currentTension) + ".", this);
        }
    }

    public Tension GetCurrentTension()
    {
        return currentTension;
    }

    [ContextMenu("Показать состояние директора")]
    private void PrintCurrentDirectorState()
    {
        Debug.Log(
            "RunEventDirector: состояние - напряжение: " + GetTensionDisplayName(currentTension) +
            ", серия боев: " + combatStreak +
            ", завершено локаций: " + completedNodeCount +
            ", корпус: " + Mathf.RoundToInt(lastPlayerHullPercent * 100f) + "%" +
            ", полученный урон: " + Mathf.RoundToInt(lastDamageTaken * 100f) + "%.",
            this);
    }

    public float ModifyNodeWeight(LocationNodeType nodeType, float baseWeight)
    {
        if (config == null || baseWeight <= 0f)
        {
            return Mathf.Max(0f, baseWeight);
        }

        float modifiedWeight = baseWeight;
        if (currentTension == Tension.High)
        {
            modifiedWeight *= GetHighTensionMultiplier(nodeType);
        }
        else if (currentTension == Tension.Low)
        {
            modifiedWeight *= GetLowTensionMultiplier(nodeType);
        }

        if (combatStreak >= config.combatStreakLimit)
        {
            modifiedWeight *= GetCombatStreakMultiplier(nodeType);
        }

        modifiedWeight = Mathf.Max(0f, modifiedWeight);
        LogSignificantWeightChange(nodeType, baseWeight, modifiedWeight);
        return modifiedWeight;
    }

    public float GetDangerLevelModifier()
    {
        if (config == null)
        {
            return 0f;
        }

        return completedNodeCount * config.difficultyGrowthPerCompletedNode;
    }

    private Tension CalculateTension(float hullPercent, float damageTaken)
    {
        if (hullPercent <= config.lowHullThreshold || damageTaken >= config.heavyDamageThreshold)
        {
            return Tension.High;
        }

        float strongHullThreshold = Mathf.Clamp01(1f - config.lowHullThreshold);
        float lowDamageThreshold = config.heavyDamageThreshold * 0.5f;
        if (hullPercent >= strongHullThreshold && damageTaken <= lowDamageThreshold)
        {
            return Tension.Low;
        }

        return Tension.Normal;
    }

    private float GetHighTensionMultiplier(LocationNodeType nodeType)
    {
        switch (nodeType)
        {
            case LocationNodeType.Repair:
                return config.repairWeightBoostWhenLowHull;
            case LocationNodeType.Shop:
                return config.shopWeightBoostWhenLowHull;
            case LocationNodeType.Resource:
                return config.resourceWeightBoostWhenLowHull;
            case LocationNodeType.Rest:
                return config.restWeightBoostWhenHighTension;
            case LocationNodeType.Elite:
                return config.eliteWeightPenaltyWhenLowHull;
            default:
                return 1f;
        }
    }

    private float GetLowTensionMultiplier(LocationNodeType nodeType)
    {
        switch (nodeType)
        {
            case LocationNodeType.Combat:
                return config.combatWeightBoostWhenPlayerStrong;
            case LocationNodeType.Elite:
                return config.eliteWeightBoostWhenPlayerStrong;
            default:
                return 1f;
        }
    }

    private float GetCombatStreakMultiplier(LocationNodeType nodeType)
    {
        switch (nodeType)
        {
            case LocationNodeType.Combat:
            case LocationNodeType.Elite:
                return config.combatWeightPenaltyAfterCombatStreak;
            case LocationNodeType.Repair:
                return Mathf.Max(1f, config.repairWeightBoostWhenLowHull);
            case LocationNodeType.Shop:
                return Mathf.Max(1f, config.shopWeightBoostWhenLowHull);
            case LocationNodeType.Rest:
                return Mathf.Max(1f, config.restWeightBoostWhenHighTension);
            case LocationNodeType.Event:
                return 1.25f;
            default:
                return 1f;
        }
    }

    private void LogSignificantWeightChange(LocationNodeType nodeType, float baseWeight, float modifiedWeight)
    {
        if (baseWeight <= 0f)
        {
            return;
        }

        float ratio = modifiedWeight / baseWeight;
        if (Mathf.Abs(ratio - 1f) < 0.25f)
        {
            return;
        }

        if (Time.frameCount != loggedWeightFrame)
        {
            loggedWeightFrame = Time.frameCount;
            loggedWeightNodesThisFrame.Clear();
        }

        if (!loggedWeightNodesThisFrame.Add(nodeType))
        {
            return;
        }

        Debug.Log(
            "RunEventDirector: вес " + nodeType + " изменен " + baseWeight.ToString("0.##") +
            " -> " + modifiedWeight.ToString("0.##") + " (" + GetTensionDisplayName(currentTension) + ").",
            this);
    }

    private static string GetTensionDisplayName(Tension tension)
    {
        switch (tension)
        {
            case Tension.Low:
                return "низкое";
            case Tension.High:
                return "высокое";
            case Tension.Normal:
            default:
                return "нормальное";
        }
    }

    private static bool IsCombatNode(LocationNodeType nodeType)
    {
        return nodeType == LocationNodeType.Combat ||
               nodeType == LocationNodeType.Elite ||
               nodeType == LocationNodeType.Boss;
    }
}
