using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct EncounterCompletionContext
{
    public readonly LocationNodeType completedNodeType;
    public readonly float playerHullPercent;
    public readonly float damageTaken;
    public readonly float elapsedTime;
    public readonly int enemiesKilled;
    public readonly bool timelineFinished;
    public readonly int aliveEnemyCount;

    public EncounterCompletionContext(
        LocationNodeType completedNodeType,
        float playerHullPercent,
        float damageTaken,
        float elapsedTime,
        int enemiesKilled,
        bool timelineFinished,
        int aliveEnemyCount)
    {
        this.completedNodeType = completedNodeType;
        this.playerHullPercent = playerHullPercent;
        this.damageTaken = damageTaken;
        this.elapsedTime = elapsedTime;
        this.enemiesKilled = enemiesKilled;
        this.timelineFinished = timelineFinished;
        this.aliveEnemyCount = aliveEnemyCount;
    }
}

[DisallowMultipleComponent]
public sealed class EncounterFlowController : MonoBehaviour
{
    [Header("Пресет забега")]
    [Tooltip("Единый пресет забега. Если не назначен, используются прямые ссылки RunManager/RunMapDirector и старые fallback-настройки.")]
    [SerializeField] private RunPresetSO runPreset;

    [Header("Связи забега")]
    [Tooltip("Runtime-менеджер забега. Хранит текущую выбранную локацию и счетчик завершенных локаций.")]
    [SerializeField] private RunManager runManager;
    [Tooltip("Директор генерации следующих локаций. Если не назначен или не смог создать варианты, используется резервный список.")]
    [SerializeField] private RunMapDirector runMapDirector;
    [Tooltip("Директор темпа забега. Получает результаты завершенных локаций и влияет на будущие веса выбора.")]
    [SerializeField] private RunEventDirector runEventDirector;

    [Header("UI локаций")]
    [Tooltip("Presenter панели выбора следующей локации.")]
    [SerializeField] private EncounterChoicePresenter encounterChoicePresenter;
    [Tooltip("Presenter панели небоёвой локации.")]
    [SerializeField] private NonCombatEncounterPresenter nonCombatEncounterPresenter;
    [Tooltip("Presenter панели выбора награды после завершения локации.")]
    [SerializeField] private RewardChoicePresenter rewardChoicePresenter;
    [Tooltip("Контроллер применения наград к ресурсам и параметрам игрока.")]
    [SerializeField] private RunRewardController runRewardController;

    [Header("Резервные варианты")]
    [Tooltip("Резервный список следующих локаций. Используется, если RunMapDirector отсутствует или не смог сгенерировать варианты.")]
    [SerializeField] private List<EncounterSO> fallbackNextEncounters = new List<EncounterSO>();

    [Header("Небоевые локации")]
    [Tooltip("Доля максимального корпуса, восстанавливаемая в Repair-локации. 0.2 означает 20%.")]
    [SerializeField, Range(0f, 1f)] private float repairHullPercent = 0.2f;
    [Tooltip("Доля максимального корпуса, восстанавливаемая в Rest-локации. 0.1 означает 10%.")]
    [SerializeField, Range(0f, 1f)] private float restHullPercent = 0.1f;

    private readonly List<EncounterSO> activeChoices = new List<EncounterSO>();
    private readonly List<RewardSO> activeRewardChoices = new List<RewardSO>();
    private bool encounterCompleted;
    private bool awaitingRewardSelection;
    private EncounterSO activeNonCombatEncounter;
    private Action<EncounterSO> startCombatEncounter;
    private Func<float> getPlayerHullPercent;
    private Action<float> restorePlayerHull;
    private Action<string, string> logMessage;

    public bool IsEncounterChoiceVisible => encounterChoicePresenter != null && encounterChoicePresenter.IsVisible;
    public bool IsNonCombatVisible => nonCombatEncounterPresenter != null && nonCombatEncounterPresenter.IsVisible;
    public bool IsRewardChoiceVisible => rewardChoicePresenter != null && rewardChoicePresenter.IsVisible;
    public RunManager RunManager => runManager;
    public RunPresetSO RunPreset => ActiveRunPreset;

    private RunPresetSO ActiveRunPreset
    {
        get
        {
            if (runPreset != null)
            {
                return runPreset;
            }

            EnsureRunMapDirectorReference();
            return runMapDirector != null ? runMapDirector.RunPreset : null;
        }
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnValidate()
    {
        repairHullPercent = Mathf.Clamp01(repairHullPercent);
        restHullPercent = Mathf.Clamp01(restHullPercent);
    }

    public void Initialize(
        Action<EncounterSO> startCombatCallback,
        Func<float> playerHullPercentReader,
        Action<float> restoreHullCallback,
        Action<string, string> logCallback)
    {
        startCombatEncounter = startCombatCallback;
        getPlayerHullPercent = playerHullPercentReader;
        restorePlayerHull = restoreHullCallback;
        logMessage = logCallback;
        EnsureReferences();
    }

    public void ImportFallbackEncounters(IReadOnlyList<EncounterSO> encounters)
    {
        if (encounters == null)
        {
            return;
        }

        fallbackNextEncounters ??= new List<EncounterSO>();
        for (int i = 0; i < encounters.Count; i++)
        {
            EncounterSO encounter = encounters[i];
            if (encounter != null && !fallbackNextEncounters.Contains(encounter))
            {
                fallbackNextEncounters.Add(encounter);
            }
        }
    }

    public void StartRun()
    {
        EnsureRunManagerReference();
        if (runManager == null)
        {
            return;
        }

        runManager.StartRun();
        RunPresetSO activePreset = ActiveRunPreset;
        if (runManager.CurrentEncounter == null && activePreset != null && activePreset.startingEncounter != null)
        {
            runManager.SelectEncounter(activePreset.startingEncounter);
        }
    }

    public void ResetEncounterCompletionState()
    {
        encounterCompleted = false;
        activeNonCombatEncounter = null;
        HideChoices();
        HideNonCombat();
        HideRewardChoices();
    }

    public WaveTimelineSO GetActiveTimeline(WaveTimelineSO fallbackTimeline)
    {
        EnsureRunManagerReference();
        if (runManager != null &&
            runManager.CurrentEncounter != null &&
            runManager.CurrentEncounter.waveTimeline != null)
        {
            return runManager.CurrentEncounter.waveTimeline;
        }

        RunPresetSO activePreset = ActiveRunPreset;
        if (activePreset != null &&
            activePreset.startingEncounter != null &&
            activePreset.startingEncounter.waveTimeline != null)
        {
            return activePreset.startingEncounter.waveTimeline;
        }

        return fallbackTimeline;
    }

    public void TryCompleteCombatEncounter(EncounterCompletionContext context)
    {
        if (encounterCompleted || !context.timelineFinished || context.aliveEnemyCount > 0)
        {
            return;
        }

        EnsureRunManagerReference();
        EncounterSO completedEncounter = runManager != null ? runManager.CurrentEncounter : null;
        encounterCompleted = true;

        EncounterResult result = new EncounterResult(
            context.completedNodeType,
            context.playerHullPercent,
            context.damageTaken,
            context.elapsedTime,
            context.enemiesKilled);

        CompleteEncounterResult(result);
        Log("Локация завершена.", "warning");
        ShowRewardsOrChoices(completedEncounter);
    }

    public bool TryHandleChoicePointer(Vector2 screenPosition)
    {
        return encounterChoicePresenter != null && encounterChoicePresenter.TrySelectAt(screenPosition);
    }

    public bool TrySelectChoiceIndex(int index)
    {
        return encounterChoicePresenter != null && encounterChoicePresenter.SelectByIndex(index);
    }

    public bool TryHandleNonCombatPointer(Vector2 screenPosition)
    {
        return nonCombatEncounterPresenter != null && nonCombatEncounterPresenter.TryActivateAt(screenPosition);
    }

    public bool TryHandleRewardChoicePointer(Vector2 screenPosition)
    {
        return rewardChoicePresenter != null && rewardChoicePresenter.TrySelectAt(screenPosition);
    }

    public bool TrySelectRewardChoiceIndex(int index)
    {
        return rewardChoicePresenter != null && rewardChoicePresenter.SelectByIndex(index);
    }

    public void CompleteActiveNonCombatEncounter()
    {
        EncounterSO encounter = activeNonCombatEncounter;
        if (encounter == null)
        {
            HideNonCombat();
            ShowRewardsOrChoices(null);
            return;
        }

        ApplyNonCombatPlaceholderEffect(encounter);
        EncounterResult result = new EncounterResult(
            encounter.nodeType,
            getPlayerHullPercent != null ? getPlayerHullPercent() : 0f,
            0f,
            0f,
            0);

        CompleteEncounterResult(result);
        Log("Локация завершена: " + GetNodeTypeDisplayName(encounter.nodeType), "warning");
        activeNonCombatEncounter = null;
        HideNonCombat();
        ShowRewardsOrChoices(encounter);
    }

    public void HideChoices()
    {
        encounterChoicePresenter?.Hide();
        activeChoices.Clear();
    }

    public void HideNonCombat()
    {
        nonCombatEncounterPresenter?.Hide();
        activeNonCombatEncounter = null;
    }

    public void HideRewardChoices()
    {
        rewardChoicePresenter?.Hide();
        activeRewardChoices.Clear();
        awaitingRewardSelection = false;
    }

    public void SelectEncounter(EncounterSO encounter)
    {
        if (encounter == null)
        {
            return;
        }

        EnsureRunManagerReference();
        runManager?.SelectEncounter(encounter);

        HideChoices();
        HideRewardChoices();
        if (ShouldHandleAsNonCombatPlaceholder(encounter))
        {
            ShowNonCombatEncounter(encounter);
            return;
        }

        startCombatEncounter?.Invoke(encounter);
    }

    public void SelectReward(RewardSO reward)
    {
        if (!awaitingRewardSelection || reward == null)
        {
            return;
        }

        EnsureReferences();
        runRewardController?.ApplyReward(reward);
        HideRewardChoices();
        ShowChoices();
    }

    private void ShowRewardsOrChoices(EncounterSO completedEncounter)
    {
        if (TryShowRewardChoices(completedEncounter))
        {
            return;
        }

        ShowChoices();
    }

    private bool TryShowRewardChoices(EncounterSO completedEncounter)
    {
        HideRewardChoices();
        if (completedEncounter == null || completedEncounter.rewardTable == null)
        {
            return false;
        }

        EnsureReferences();
        if (rewardChoicePresenter == null)
        {
            Debug.LogWarning("EncounterFlowController: у локации назначена таблица наград, но RewardChoicePresenter не найден.", this);
            return false;
        }

        List<RewardSO> generatedChoices = completedEncounter.rewardTable.GenerateChoices();
        if (generatedChoices == null || generatedChoices.Count == 0)
        {
            Debug.LogWarning("EncounterFlowController: таблица наград пуста или не смогла создать варианты. Переход к выбору следующей локации.", this);
            return false;
        }

        activeRewardChoices.Clear();
        for (int i = 0; i < generatedChoices.Count && activeRewardChoices.Count < 3; i++)
        {
            RewardSO reward = generatedChoices[i];
            if (reward != null)
            {
                activeRewardChoices.Add(reward);
            }
        }

        if (activeRewardChoices.Count == 0)
        {
            return false;
        }

        awaitingRewardSelection = true;
        rewardChoicePresenter.Show(activeRewardChoices, SelectReward);
        return true;
    }

    private void ShowChoices()
    {
        EnsureReferences();
        activeChoices.Clear();

        if (!TryGenerateRunMapChoices(activeChoices) && fallbackNextEncounters != null)
        {
            for (int i = 0; i < fallbackNextEncounters.Count && activeChoices.Count < 3; i++)
            {
                EncounterSO encounter = fallbackNextEncounters[i];
                if (encounter != null)
                {
                    activeChoices.Add(encounter);
                }
            }
        }

        if (activeChoices.Count == 0)
        {
            Debug.LogWarning("EncounterFlowController: не настроены варианты следующих локаций. Заполните fallback-список или RunMapDirector.", this);
            return;
        }

        encounterChoicePresenter?.ShowChoices(activeChoices, SelectEncounter);
    }

    private void ShowNonCombatEncounter(EncounterSO encounter)
    {
        if (encounter == null)
        {
            return;
        }

        EnsureReferences();
        activeNonCombatEncounter = encounter;
        nonCombatEncounterPresenter?.Show(encounter, BuildNonCombatDescription(encounter), CompleteActiveNonCombatEncounter);
    }

    private bool TryGenerateRunMapChoices(List<EncounterSO> results)
    {
        EnsureRunManagerReference();
        EnsureRunMapDirectorReference();
        if (runMapDirector == null)
        {
            return false;
        }

        int completedEncounterCount = runManager != null ? runManager.CompletedEncounterCount : 0;
        bool generated = runMapDirector.TryGenerateNextChoices(completedEncounterCount, results);
        if (!generated)
        {
            Debug.LogWarning("EncounterFlowController: RunMapDirector не смог сгенерировать варианты. Используется резервный список.", this);
        }

        return generated;
    }

    private void CompleteEncounterResult(EncounterResult result)
    {
        EnsureRunManagerReference();
        runManager?.CompleteCurrentEncounter(result);

        EnsureRunEventDirectorReference();
        runEventDirector?.OnEncounterCompleted(result);
    }

    private void ApplyNonCombatPlaceholderEffect(EncounterSO encounter)
    {
        if (encounter == null)
        {
            return;
        }

        switch (encounter.nodeType)
        {
            case LocationNodeType.Repair:
                // TODO: заменить бесплатный ремонт системой цены и ресурсов.
                restorePlayerHull?.Invoke(repairHullPercent);
                Log("Ремонт: корпус частично восстановлен.", "warning");
                break;
            case LocationNodeType.Rest:
                restorePlayerHull?.Invoke(restHullPercent);
                Log("Отдых: корпус немного восстановлен.", "warning");
                break;
            case LocationNodeType.Resource:
                Debug.Log("EncounterFlowController: ресурсная локация завершена. Экономика будет добавлена позже.", this);
                Log("Ресурсы собраны. Полная награда будет подключена позже.", "warning");
                break;
            case LocationNodeType.Shop:
                Debug.Log("EncounterFlowController: магазин открыт как временная заглушка.", this);
                Log("Магазин пока работает как заглушка.", "warning");
                break;
            case LocationNodeType.Event:
                Debug.Log("EncounterFlowController: событие показано как временная заглушка.", this);
                Log("Событие обработано как заглушка.", "warning");
                break;
        }
    }

    private string BuildNonCombatDescription(EncounterSO encounter)
    {
        string description = encounter != null ? encounter.shortDescription : string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            description = "Временная небоевая локация. Полная механика будет добавлена позже.";
        }

        switch (encounter.nodeType)
        {
            case LocationNodeType.Repair:
                return description + "\n\nДействие: восстановить корпус на " + Mathf.RoundToInt(repairHullPercent * 100f) + "%.";
            case LocationNodeType.Rest:
                return description + "\n\nДействие: восстановить корпус на " + Mathf.RoundToInt(restHullPercent * 100f) + "%.";
            case LocationNodeType.Resource:
                return description + "\n\nРесурсная награда будет подключена позже.";
            case LocationNodeType.Shop:
                return description + "\n\nИнвентарь магазина будет подключен позже.";
            case LocationNodeType.Event:
                return description + "\n\nВарианты события будут подключены позже.";
            default:
                return description;
        }
    }

    private static bool ShouldHandleAsNonCombatPlaceholder(EncounterSO encounter)
    {
        return encounter != null &&
               encounter.waveTimeline == null &&
               IsNonCombatNode(encounter.nodeType);
    }

    private static bool IsNonCombatNode(LocationNodeType nodeType)
    {
        return nodeType == LocationNodeType.Shop ||
               nodeType == LocationNodeType.Repair ||
               nodeType == LocationNodeType.Event ||
               nodeType == LocationNodeType.Rest ||
               nodeType == LocationNodeType.Resource;
    }

    private static string GetNodeTypeDisplayName(LocationNodeType nodeType)
    {
        switch (nodeType)
        {
            case LocationNodeType.Combat:
                return "Бой";
            case LocationNodeType.Elite:
                return "Элита";
            case LocationNodeType.Shop:
                return "Магазин";
            case LocationNodeType.Repair:
                return "Ремонт";
            case LocationNodeType.Event:
                return "Событие";
            case LocationNodeType.Rest:
                return "Отдых";
            case LocationNodeType.Resource:
                return "Ресурсы";
            case LocationNodeType.Boss:
                return "Босс";
            default:
                return nodeType.ToString();
        }
    }

    private void EnsureReferences()
    {
        EnsureRunManagerReference();
        EnsureRunMapDirectorReference();
        EnsureRunEventDirectorReference();
        EnsureEncounterChoicePresenterReference();
        EnsureNonCombatPresenterReference();
        EnsureRewardChoicePresenterReference();
        EnsureRunRewardControllerReference();
    }

    private void EnsureRunManagerReference()
    {
        if (runManager != null)
        {
            return;
        }

        runManager = FindAnyObjectByType<RunManager>(FindObjectsInactive.Include);
        if (runManager != null)
        {
            return;
        }

        GameObject runManagerObject = new GameObject("RunManager");
        runManager = runManagerObject.AddComponent<RunManager>();
    }

    private void EnsureRunMapDirectorReference()
    {
        if (runMapDirector == null)
        {
            runMapDirector = FindAnyObjectByType<RunMapDirector>(FindObjectsInactive.Include);
        }
    }

    private void EnsureRunEventDirectorReference()
    {
        if (runEventDirector == null)
        {
            runEventDirector = FindAnyObjectByType<RunEventDirector>(FindObjectsInactive.Include);
        }
    }

    private void EnsureEncounterChoicePresenterReference()
    {
        if (encounterChoicePresenter == null)
        {
            encounterChoicePresenter = FindAnyObjectByType<EncounterChoicePresenter>(FindObjectsInactive.Include);
        }
    }

    private void EnsureNonCombatPresenterReference()
    {
        if (nonCombatEncounterPresenter == null)
        {
            nonCombatEncounterPresenter = FindAnyObjectByType<NonCombatEncounterPresenter>(FindObjectsInactive.Include);
        }
    }

    private void EnsureRewardChoicePresenterReference()
    {
        if (rewardChoicePresenter == null)
        {
            rewardChoicePresenter = FindAnyObjectByType<RewardChoicePresenter>(FindObjectsInactive.Include);
        }
    }

    private void EnsureRunRewardControllerReference()
    {
        if (runRewardController == null)
        {
            runRewardController = FindAnyObjectByType<RunRewardController>(FindObjectsInactive.Include);
        }
    }

    private void Log(string message, string kind)
    {
        logMessage?.Invoke(message, kind);
    }
}
