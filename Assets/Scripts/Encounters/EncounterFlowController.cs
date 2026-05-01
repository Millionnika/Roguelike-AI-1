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
    [Tooltip("Единый пресет забега. Используется для стартовой локации и как понятная точка входа настройки. Если поле пустое, работают старые прямые ссылки.")]
    [SerializeField] private RunPresetSO runPreset;

    [Header("Связи забега")]
    [Tooltip("Runtime-менеджер забега. Хранит текущую выбранную локацию и счетчик завершенных локаций.")]
    [SerializeField] private RunManager runManager;
    [Tooltip("Директор генерации следующих локаций. Если не назначен или не сможет сгенерировать варианты, используется резервный список.")]
    [SerializeField] private RunMapDirector runMapDirector;
    [Tooltip("Директор темпа забега. Получает результаты завершенных локаций и влияет на будущие веса выбора.")]
    [SerializeField] private RunEventDirector runEventDirector;

    [Header("UI локаций")]
    [Tooltip("Presenter панели выбора следующей локации. Отвечает за показ вариантов и клики по кнопкам.")]
    [SerializeField] private EncounterChoicePresenter encounterChoicePresenter;
    [Tooltip("Presenter панели небоевой локации. Отвечает за показ заглушки Shop/Repair/Event/Rest/Resource.")]
    [SerializeField] private NonCombatEncounterPresenter nonCombatEncounterPresenter;

    [Header("Резервные варианты")]
    [Tooltip("Резервный список следующих локаций. Используется, если RunMapDirector отсутствует или не смог сгенерировать варианты.")]
    [SerializeField] private List<EncounterSO> fallbackNextEncounters = new List<EncounterSO>();

    [Header("Небоевые локации")]
    [Tooltip("Доля максимального корпуса, восстанавливаемая в Repair-локации. 0.2 означает 20%. Позже будет заменено системой цены ремонта.")]
    [SerializeField, Range(0f, 1f)] private float repairHullPercent = 0.2f;
    [Tooltip("Доля максимального корпуса, восстанавливаемая в Rest-локации. 0.1 означает 10%.")]
    [SerializeField, Range(0f, 1f)] private float restHullPercent = 0.1f;

    private readonly List<EncounterSO> activeChoices = new List<EncounterSO>();
    private bool encounterCompleted;
    private EncounterSO activeNonCombatEncounter;
    private Action<EncounterSO> startCombatEncounter;
    private Func<float> getPlayerHullPercent;
    private Action<float> restorePlayerHull;
    private Action<string, string> logMessage;

    public bool IsEncounterChoiceVisible => encounterChoicePresenter != null && encounterChoicePresenter.IsVisible;
    public bool IsNonCombatVisible => nonCombatEncounterPresenter != null && nonCombatEncounterPresenter.IsVisible;
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

        if (fallbackNextEncounters == null)
        {
            fallbackNextEncounters = new List<EncounterSO>();
        }

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
        if (runManager != null)
        {
            runManager.StartRun();
            RunPresetSO activePreset = ActiveRunPreset;
            if (runManager.CurrentEncounter == null && activePreset != null && activePreset.startingEncounter != null)
            {
                runManager.SelectEncounter(activePreset.startingEncounter);
            }
        }
    }

    public void ResetEncounterCompletionState()
    {
        encounterCompleted = false;
        activeNonCombatEncounter = null;
        HideChoices();
        HideNonCombat();
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

        encounterCompleted = true;
        EncounterResult result = new EncounterResult(
            context.completedNodeType,
            context.playerHullPercent,
            context.damageTaken,
            context.elapsedTime,
            context.enemiesKilled);

        CompleteEncounterResult(result);
        Log("Локация завершена.", "warning");
        ShowChoices();
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

    public void CompleteActiveNonCombatEncounter()
    {
        EncounterSO encounter = activeNonCombatEncounter;
        if (encounter == null)
        {
            HideNonCombat();
            ShowChoices();
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
        ShowChoices();
    }

    public void HideChoices()
    {
        if (encounterChoicePresenter != null)
        {
            encounterChoicePresenter.Hide();
        }

        activeChoices.Clear();
    }

    public void HideNonCombat()
    {
        if (nonCombatEncounterPresenter != null)
        {
            nonCombatEncounterPresenter.Hide();
        }

        activeNonCombatEncounter = null;
    }

    public void SelectEncounter(EncounterSO encounter)
    {
        if (encounter == null)
        {
            return;
        }

        EnsureRunManagerReference();
        if (runManager != null)
        {
            runManager.SelectEncounter(encounter);
        }

        HideChoices();
        if (ShouldHandleAsNonCombatPlaceholder(encounter))
        {
            ShowNonCombatEncounter(encounter);
            return;
        }

        startCombatEncounter?.Invoke(encounter);
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
            Debug.LogWarning("EncounterFlowController: не настроены варианты следующих локаций. Заполните резервный список или настройте RunMapDirector.", this);
            return;
        }

        if (encounterChoicePresenter != null)
        {
            encounterChoicePresenter.ShowChoices(activeChoices, SelectEncounter);
        }
    }

    private void ShowNonCombatEncounter(EncounterSO encounter)
    {
        if (encounter == null)
        {
            return;
        }

        EnsureReferences();
        activeNonCombatEncounter = encounter;
        if (nonCombatEncounterPresenter != null)
        {
            nonCombatEncounterPresenter.Show(encounter, BuildNonCombatDescription(encounter), CompleteActiveNonCombatEncounter);
        }
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
        if (runManager != null)
        {
            runManager.CompleteCurrentEncounter(result);
        }

        EnsureRunEventDirectorReference();
        if (runEventDirector != null)
        {
            runEventDirector.OnEncounterCompleted(result);
        }
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
                Log("Ресурсы собраны. Награда будет подключена позже.", "warning");
                break;
            case LocationNodeType.Shop:
                Debug.Log("EncounterFlowController: магазин открыт как временная заглушка. Инвентарь магазина будет добавлен позже.", this);
                Log("Магазин пока работает как заглушка.", "warning");
                break;
            case LocationNodeType.Event:
                Debug.Log("EncounterFlowController: событие показано как временная заглушка. Варианты событий будут добавлены позже.", this);
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

    private void Log(string message, string kind)
    {
        logMessage?.Invoke(message, kind);
    }
}
