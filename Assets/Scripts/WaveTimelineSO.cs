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
    [Min(0f)] public float startTime;
    [Min(0f)] public float duration;
    public SpawnPatternType pattern = SpawnPatternType.Continuous;
    [FormerlySerializedAs("enemyData")] public ShipDataSO shipData;
    [Min(0)] public int count = 1;
}

[CreateAssetMenu(menuName = "Roguelike/Wave Timeline", fileName = "WaveTimeline")]
public sealed class WaveTimelineSO : ScriptableObject
{
    public List<SpawnEvent> events = new List<SpawnEvent>();
}
