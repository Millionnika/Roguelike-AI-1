using System;
using System.Collections.Generic;
using UnityEngine;

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
    public EnemyDataSO enemyData;
    [Min(0)] public int count = 1;
}

[CreateAssetMenu(menuName = "Roguelike/Wave Timeline", fileName = "WaveTimeline")]
public sealed class WaveTimelineSO : ScriptableObject
{
    public List<SpawnEvent> events = new List<SpawnEvent>();
}
