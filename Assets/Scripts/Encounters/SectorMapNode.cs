using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class SectorMapNode
{
    [Tooltip("Координата X узла на карте сектора.")]
    public int x;
    [Tooltip("Координата Y узла на карте сектора.")]
    public int y;
    [Tooltip("Мировая позиция сектора, куда летит корабль при выборе узла.")]
    public Vector3 worldPosition;
    [Tooltip("Локация, которая запускается после прибытия в этот сектор.")]
    public EncounterSO encounter;
    [Tooltip("Узел уже был посещен игроком.")]
    public bool visited;
    [Tooltip("Узел является текущей позицией игрока на карте.")]
    public bool current;
    [Tooltip("Узел доступен для следующего перехода из текущей позиции.")]
    public bool reachable;
    [Tooltip("Узел завершен. Для MVP совпадает с посещением, но хранится отдельно для UI.")]
    public bool completed;
    [Tooltip("Если включено, узел не участвует в маршруте и не должен быть кликабельным.")]
    public bool locked;
    [Tooltip("Финальный узел маршрута (верхний правый угол карты).")]
    public bool isFinish;
    [Tooltip("Координаты узлов, в которые можно идти вперед из этого узла.")]
    public List<Vector2Int> nextCoordinates = new List<Vector2Int>();

    public Vector2Int Coordinates => new Vector2Int(x, y);
    public Vector3 WorldPosition => worldPosition;

    public bool IsConnectedTo(SectorMapNode other)
    {
        if (other == null || nextCoordinates == null)
        {
            return false;
        }

        Vector2Int coordinates = other.Coordinates;
        for (int i = 0; i < nextCoordinates.Count; i++)
        {
            if (nextCoordinates[i] == coordinates)
            {
                return true;
            }
        }

        return false;
    }
}
