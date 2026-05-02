using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Sector Map Config", fileName = "SectorMapConfig")]
public sealed class SectorMapConfigSO : ScriptableObject
{
    [Header("Размер карты")]
    [Tooltip("Ширина карты сектора в координатах. Для MVP рекомендуется 5.")]
    [Min(3)] public int width = 5;
    [Tooltip("Высота карты сектора в координатах. Для MVP рекомендуется 5.")]
    [Min(2)] public int height = 5;

    [Header("Генерация маршрута")]
    [Tooltip("Сколько узлов в следующем ряду желательно оставлять доступными. В route graph используется как мягкая настройка для будущих вариантов.")]
    [Range(2, 3)] public int choicesPerRow = 3;
    [Tooltip("Пул EncounterSO для заполнения узлов карты сектора.")]
    public EncounterPoolSO encounterPool;
    [Tooltip("Если стартовая локация не назначена в RunPreset, стартовый узел берет первый Combat Encounter из пула.")]
    public bool forceCombatStart = true;
    [Tooltip("Если включено, финальный узел пытается использовать Boss Encounter. Если Boss нет, используется Combat fallback.")]
    public bool forceBossOrCombatEnd = true;

    [Header("Мировые координаты секторов")]
    [Tooltip("Расстояние между соседними секторами в мире. Используется SectorWarpController для перелета к выбранному узлу.")]
    public Vector2 sectorWorldSize = new Vector2(80f, 80f);
    [Tooltip("Мировая позиция нижнего левого сектора карты.")]
    public Vector2 worldOrigin = Vector2.zero;

    private void OnValidate()
    {
        width = Mathf.Max(3, width);
        height = Mathf.Max(2, height);
        choicesPerRow = Mathf.Clamp(choicesPerRow, 2, 3);
        sectorWorldSize.x = Mathf.Max(1f, sectorWorldSize.x);
        sectorWorldSize.y = Mathf.Max(1f, sectorWorldSize.y);
    }
}
