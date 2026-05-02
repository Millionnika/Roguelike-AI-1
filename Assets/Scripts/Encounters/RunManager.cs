using UnityEngine;

public readonly struct EncounterResult
{
    public readonly LocationNodeType completedNodeType;
    public readonly float playerHullPercent;
    public readonly float damageTaken;
    public readonly float elapsedTime;
    public readonly int enemiesKilled;

    public EncounterResult(
        LocationNodeType completedNodeType,
        float playerHullPercent,
        float damageTaken,
        float elapsedTime,
        int enemiesKilled)
    {
        this.completedNodeType = completedNodeType;
        this.playerHullPercent = playerHullPercent;
        this.damageTaken = damageTaken;
        this.elapsedTime = elapsedTime;
        this.enemiesKilled = enemiesKilled;
    }
}

[DisallowMultipleComponent]
public sealed class RunManager : MonoBehaviour
{
    [Header("Состояние забега")]
    [Tooltip("Текущая выбранная локация. Заполняется при выборе игроком следующего маршрута и очищается после завершения локации.")]
    [SerializeField] private EncounterSO currentEncounter;
    [Tooltip("Последняя выбранная игроком локация. Нужна для отладки текущего маршрута.")]
    [SerializeField] private EncounterSO lastSelectedEncounter;
    [Tooltip("Сколько локаций завершено в текущем забеге.")]
    [SerializeField, Min(0)] private int completedEncounterCount;
    [Tooltip("Показывает, что runtime-забег уже был запущен.")]
    [SerializeField] private bool runStarted;
    [Tooltip("Runtime-ресурсы текущего забега (Scrap и другие простые валюты MVP).")]
    [SerializeField] private RunResources runResources;

    private EncounterResult lastEncounterResult;

    public EncounterSO CurrentEncounter => currentEncounter;
    public EncounterSO LastSelectedEncounter => lastSelectedEncounter;
    public int CompletedEncounterCount => completedEncounterCount;
    public bool RunStarted => runStarted;
    public EncounterResult LastEncounterResult => lastEncounterResult;
    public RunResources Resources => runResources;

    private void Awake()
    {
        if (runResources == null)
        {
            runResources = GetComponent<RunResources>();
        }

        if (runResources == null)
        {
            runResources = gameObject.AddComponent<RunResources>();
        }

        DontDestroyOnLoad(gameObject);
    }

    public void StartRun()
    {
        runStarted = true;
        completedEncounterCount = 0;
        lastEncounterResult = default;
        runResources?.ResetRunResources();
    }

    public void SelectEncounter(EncounterSO encounter)
    {
        if (encounter == null)
        {
            Debug.LogWarning("RunManager: нельзя выбрать пустую локацию.", this);
            return;
        }

        runStarted = true;
        currentEncounter = encounter;
        lastSelectedEncounter = encounter;
    }

    public void CompleteCurrentEncounter(EncounterResult result)
    {
        lastEncounterResult = result;
        completedEncounterCount++;
        currentEncounter = null;
    }

    public void AddScrap(int amount)
    {
        runResources?.AddScrap(amount);
    }

    public bool SpendScrap(int amount)
    {
        return runResources != null && runResources.SpendScrap(amount);
    }

    public void ResetRunResources()
    {
        runResources?.ResetRunResources();
    }
}
