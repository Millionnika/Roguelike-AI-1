using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class SectorMapNode
{
    [Tooltip("Координата X узла в логической сетке карты сектора.")]
    public int x;
    [Tooltip("Координата Y узла в логической сетке карты сектора.")]
    public int y;
    [Tooltip("Позиция узла на визуальной карте сектора (для отрисовки точек и линий).")]
    public Vector2 mapPosition;
    [Tooltip("Мировая позиция сектора, куда летит корабль при выборе узла.")]
    public Vector3 worldPosition;
    [Tooltip("Локация, которая запускается после прибытия в этот сектор.")]
    public EncounterSO encounter;
    [Tooltip("Узел уже был посещен игроком.")]
    public bool visited;
    [Tooltip("Узел является текущей позицией игрока на карте.")]
    public bool current;
    [Tooltip("Узел доступен для следующего перехода.")]
    public bool reachable;
    [Tooltip("Узел завершен.")]
    public bool completed;
    [Tooltip("Если включено, узел не участвует в маршруте.")]
    public bool locked;
    [Tooltip("Финальный узел маршрута.")]
    public bool isFinish;
    [Tooltip("Стартовый узел маршрута.")]
    public bool isHome;
    [Tooltip("Координаты узлов, в которые разрешен переход из текущего узла.")]
    public List<Vector2Int> nextCoordinates = new List<Vector2Int>();

    public Vector2Int Coordinates => new Vector2Int(x, y);
    public Vector3 WorldPosition => worldPosition;

    public bool IsConnectedTo(SectorMapNode other)
    {
        if (other == null || nextCoordinates == null)
        {
            return false;
        }

        Vector2Int target = other.Coordinates;
        for (int i = 0; i < nextCoordinates.Count; i++)
        {
            if (nextCoordinates[i] == target)
            {
                return true;
            }
        }

        return false;
    }
}
