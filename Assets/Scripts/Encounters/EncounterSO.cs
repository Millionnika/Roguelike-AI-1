using UnityEngine;

public enum LocationNodeType
{
    Combat,
    Elite,
    Shop,
    Repair,
    Event,
    Rest,
    Resource,
    Boss
}

[CreateAssetMenu(menuName = "Roguelike/Encounter", fileName = "Encounter")]
public sealed class EncounterSO : ScriptableObject
{
    [Header("Описание локации")]
    [Tooltip("Название локации, которое игрок увидит на кнопке выбора следующего маршрута.")]
    public string displayName = "Новая локация";
    [Tooltip("Тип локации. Бой, элита и босс запускают боевой сценарий. Магазин, ремонт, событие, отдых и ресурсы обрабатываются как простые небоевые заглушки, если у них не назначен боевой таймлайн.")]
    public LocationNodeType nodeType = LocationNodeType.Combat;
    [Tooltip("Иконка локации для будущей визуальной карты. На текущем этапе простая панель выбора ее еще не использует.")]
    public Sprite icon;

    [Header("Боевой таймлайн")]
    [Tooltip("Таймлайн спавна врагов для боевой локации. Для небоевых локаций оставьте пустым, если они должны открывать временную панель без боя.")]
    public WaveTimelineSO waveTimeline;
    [Tooltip("Уровень опасности локации. Используется RunMapDirector для фильтрации вариантов по диапазону сложности. Рекомендуемый стартовый диапазон: 1-3.")]
    [Min(0)] public int dangerLevel = 1;

    [Header("Текст для игрока")]
    [Tooltip("Краткое описание локации. Показывается во временной панели небоевых узлов и может использоваться в выборе маршрута.")]
    [TextArea(2, 4)] public string shortDescription;
    [Tooltip("Таблица наград, из которой выбираются варианты после завершения этой локации.")]
    public RewardTableSO rewardTable;

    private void OnValidate()
    {
        dangerLevel = Mathf.Max(0, dangerLevel);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            Debug.LogWarning("EncounterSO: у локации не задано название. Заполните поле названия, чтобы игрок видел понятный выбор маршрута.", this);
        }

        if (IsCombatNode(nodeType) && waveTimeline == null)
        {
            Debug.LogWarning("EncounterSO: боевая локация '" + name + "' не имеет таймлайна волн. Она не сможет запустить бой.", this);
        }
    }

    private static bool IsCombatNode(LocationNodeType type)
    {
        return type == LocationNodeType.Combat ||
               type == LocationNodeType.Elite ||
               type == LocationNodeType.Boss;
    }
}
