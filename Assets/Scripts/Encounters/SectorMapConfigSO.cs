using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Sector Map Config", fileName = "SectorMapConfig")]
public sealed class SectorMapConfigSO : ScriptableObject
{
    [Header("Размер карты")]
    [Tooltip("Ширина сектора в логических координатах. Старт создается в (0,0), финиш — в (width-1,height-1).")]
    [Min(5)] public int width = 8;
    [Tooltip("Высота сектора в логических координатах.")]
    [Min(5)] public int height = 7;

    [Header("Генерация маршрутов")]
    [Tooltip("Минимальное число узлов в промежуточном ряду (между стартом и финишем).")]
    [Range(1, 12)] public int minNodesPerRow = 2;
    [Tooltip("Максимальное число узлов в промежуточном ряду.")]
    [Range(1, 12)] public int maxNodesPerRow = 4;
    [Tooltip("Сколько гарантированных независимых маршрутов строить от старта к финишу.")]
    [Range(1, 8)] public int guaranteedRouteCount = 3;
    [Tooltip("Шанс добавить дополнительную связь между узлами соседних рядов.")]
    [Range(0f, 1f)] public float extraRouteChance = 0.35f;
    [Tooltip("Максимум исходящих связей из одного узла.")]
    [Range(1, 4)] public int maxConnectionsPerNode = 2;

    [Header("Визуальный разброс")]
    [Tooltip("Горизонтальный случайный разброс точки узла, чтобы карта не выглядела как ровная таблица.")]
    [Range(0f, 0.45f)] public float positionJitterX = 0.28f;
    [Tooltip("Вертикальный случайный разброс точки узла.")]
    [Range(0f, 0.45f)] public float positionJitterY = 0.18f;

    [Header("Случайность")]
    [Tooltip("Если включено, карта генерируется новым случайным образом на каждый новый забег.")]
    public bool useRandomSeedPerRun = true;
    [Tooltip("Если случайный seed выключен, используется этот фиксированный seed для повторяемой карты.")]
    public int fixedSeed = 12345;

    [Header("Локации")]
    [Tooltip("Пул EncounterSO для заполнения узлов карты.")]
    public EncounterPoolSO encounterPool;
    [Tooltip("Пытаться поставить Boss Encounter в финальный узел. Если босса нет, используется Combat fallback.")]
    public bool forceBossOrCombatEnd = true;

    [Header("Мировые координаты секторов")]
    [Tooltip("Расстояние между соседними секторами в мире (используется для warp-перелета).")]
    public Vector2 sectorWorldSize = new Vector2(80f, 80f);
    [Tooltip("Мировая позиция узла (0,0).")]
    public Vector2 worldOrigin = Vector2.zero;

    private void OnValidate()
    {
        width = Mathf.Max(5, width);
        height = Mathf.Max(5, height);
        minNodesPerRow = Mathf.Clamp(minNodesPerRow, 1, Mathf.Max(1, width - 1));
        maxNodesPerRow = Mathf.Clamp(maxNodesPerRow, minNodesPerRow, Mathf.Max(minNodesPerRow, width));
        guaranteedRouteCount = Mathf.Clamp(guaranteedRouteCount, 1, 8);
        extraRouteChance = Mathf.Clamp01(extraRouteChance);
        maxConnectionsPerNode = Mathf.Clamp(maxConnectionsPerNode, 1, 4);
        positionJitterX = Mathf.Clamp(positionJitterX, 0f, 0.45f);
        positionJitterY = Mathf.Clamp(positionJitterY, 0f, 0.45f);
        sectorWorldSize.x = Mathf.Max(1f, sectorWorldSize.x);
        sectorWorldSize.y = Mathf.Max(1f, sectorWorldSize.y);
    }
}
