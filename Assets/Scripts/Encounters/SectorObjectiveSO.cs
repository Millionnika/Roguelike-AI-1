using UnityEngine;

public enum SectorObjectiveType
{
    None,
    CollectContainers,
    HoldZone,
    ScanAnomaly
}

[CreateAssetMenu(menuName = "Roguelike/Sector Objective", fileName = "SectorObjective")]
public sealed class SectorObjectiveSO : ScriptableObject
{
    [Header("Описание цели")]
    [Tooltip("Название цели сектора.")]
    public string displayName = "Новая цель";

    [Tooltip("Описание цели, которое будет показано игроку.")]
    [TextArea(2, 4)] public string description;

    [Tooltip("Тип цели сектора.")]
    public SectorObjectiveType objectiveType = SectorObjectiveType.CollectContainers;

    [Header("Параметры выполнения")]
    [Tooltip("Сколько объектов нужно собрать или активировать.")]
    [Min(1)] public int requiredAmount = 3;

    [Tooltip("Сколько секунд нужно удерживать зону или сканировать объект.")]
    [Min(0.1f)] public float duration = 10f;

    [Tooltip("Радиус взаимодействия с целью.")]
    [Min(0.1f)] public float radius = 4f;

    [Header("Параметры спавна")]
    [Tooltip("Минимальная дистанция появления цели от игрока.")]
    [Min(0f)] public float spawnRadiusMin = 8f;

    [Tooltip("Максимальная дистанция появления цели от игрока.")]
    [Min(0f)] public float spawnRadiusMax = 20f;

    [Tooltip("Префаб объекта цели. Если не назначен, будет создан простой runtime-визуал.")]
    public GameObject objectivePrefab;

    [Header("Визуал и бонус")]
    [Tooltip("Иконка цели для UI (опционально).")]
    public Sprite icon;

    [Tooltip("Дополнительный лом за выполнение цели.")]
    [Min(0)] public int scrapReward = 0;

    private void OnValidate()
    {
        requiredAmount = Mathf.Max(1, requiredAmount);
        duration = Mathf.Max(0.1f, duration);
        radius = Mathf.Max(0.1f, radius);
        spawnRadiusMin = Mathf.Max(0f, spawnRadiusMin);
        spawnRadiusMax = Mathf.Max(spawnRadiusMin, spawnRadiusMax);
        scrapReward = Mathf.Max(0, scrapReward);
    }
}
