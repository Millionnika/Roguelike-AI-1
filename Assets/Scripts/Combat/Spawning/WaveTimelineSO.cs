using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum SpawnPatternType
{
    Continuous,
    Burst,
    Ring,
    Wall
}

[Serializable]
public sealed class SpawnEvent
{
    [Tooltip("Момент начала события на таймлайне (сек от старта волны).")]
    [Min(0f)] public float startTime;
    [Tooltip("Длительность события (сек).")]
    [Min(0f)] public float duration;
    [Tooltip("Тип паттерна спавна.")]
    public SpawnPatternType pattern = SpawnPatternType.Continuous;
    [Tooltip("Какой корабль/юнит спавнится в этом событии.")]
    [FormerlySerializedAs("enemyData")] public ShipDataSO shipData;
    [Tooltip("Количество юнитов в событии/пачке.")]
    [Min(0)] public int count = 1;
}

[CreateAssetMenu(menuName = "Roguelike/Wave Timeline", fileName = "WaveTimeline")]
public sealed class WaveTimelineSO : ScriptableObject
{
    [Tooltip("Список событий спавна для этой волны.")]
    public List<SpawnEvent> events = new List<SpawnEvent>();
}
