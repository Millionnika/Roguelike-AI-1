using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TimelineSpawnController : MonoBehaviour
{
    private sealed class SpawnEventRuntimeState
    {
        public bool oneShotExecuted;
        public float continuousAccumulator;
    }

    [Header("Спавн таймлайна")]
    [Tooltip("Отступ за границей viewport камеры, где появляются враги. 0.1 означает 10% размера экрана за пределами видимой области.")]
    [SerializeField, Range(0.01f, 0.5f)] private float offscreenViewportMargin = 0.1f;
    [Tooltip("Длительность фазы таймлайна в секундах. Каждая новая фаза увеличивает номер волны и масштаб сложности.")]
    [SerializeField, Min(1f)] private float timelinePhaseDuration = 30f;
    [Tooltip("Прирост сложности за каждую фазу таймлайна. LevelScale считается как 1 + (wave - 1) * это значение.")]
    [SerializeField, Min(0f)] private float timelineDifficultyPerPhase = 0.14f;

    private readonly List<SpawnEventRuntimeState> spawnEventStates = new List<SpawnEventRuntimeState>();
    private Func<ShipDataSO, Vector3, float, bool> spawnEnemyCallback;
    private Camera spawnCamera;

    public float GameTimer { get; private set; }
    public int CurrentWave { get; private set; } = 1;
    public bool IsTimelineFinished { get; private set; }
    public int SpawnedThisFrame { get; private set; }
    public float NextEventTime { get; private set; } = -1f;

    private void OnValidate()
    {
        offscreenViewportMargin = Mathf.Clamp(offscreenViewportMargin, 0.01f, 0.5f);
        timelinePhaseDuration = Mathf.Max(1f, timelinePhaseDuration);
        timelineDifficultyPerPhase = Mathf.Max(0f, timelineDifficultyPerPhase);
    }

    public void SetCamera(Camera camera)
    {
        spawnCamera = camera;
    }

    public void SetSpawnEnemyCallback(Func<ShipDataSO, Vector3, float, bool> callback)
    {
        spawnEnemyCallback = callback;
    }

    public void ResetRuntime()
    {
        GameTimer = 0f;
        CurrentWave = 1;
        IsTimelineFinished = false;
        SpawnedThisFrame = 0;
        NextEventTime = -1f;
        spawnEventStates.Clear();
    }

    public void Tick(float deltaTime, WaveTimelineSO activeTimeline, Vector3 playerPosition)
    {
        SpawnedThisFrame = 0;
        float previousTime = GameTimer;
        GameTimer += Mathf.Max(0f, deltaTime);
        CurrentWave = Mathf.Max(1, 1 + Mathf.FloorToInt(GameTimer / Mathf.Max(1f, timelinePhaseDuration)));

        EnsureSpawnEventRuntimeStates(activeTimeline);
        if (activeTimeline == null || activeTimeline.events == null || activeTimeline.events.Count == 0)
        {
            NextEventTime = -1f;
            IsTimelineFinished = false;
            return;
        }

        for (int i = 0; i < activeTimeline.events.Count; i++)
        {
            SpawnEvent spawnEvent = activeTimeline.events[i];
            SpawnEventRuntimeState state = spawnEventStates[i];
            if (spawnEvent == null || spawnEvent.shipData == null || spawnEvent.shipData.shipPrefab == null)
            {
                continue;
            }

            float startTime = Mathf.Max(0f, spawnEvent.startTime);
            int count = Mathf.Max(0, spawnEvent.count);
            if (count <= 0)
            {
                continue;
            }

            if (spawnEvent.pattern == SpawnPatternType.Continuous)
            {
                TickContinuousEvent(spawnEvent, state, previousTime, startTime);
                continue;
            }

            if (state.oneShotExecuted || GameTimer < startTime)
            {
                continue;
            }

            SpawnedThisFrame += ExecuteOneShotPattern(spawnEvent, count, playerPosition);
            state.oneShotExecuted = true;
        }

        NextEventTime = GetNextTimelineEventTime(activeTimeline, GameTimer);
        IsTimelineFinished = NextEventTime < 0f;
    }

    public float GetLevelScale()
    {
        return 1f + (CurrentWave - 1) * timelineDifficultyPerPhase;
    }

    public float GetNextTimelineEventTime(WaveTimelineSO timeline)
    {
        return GetNextTimelineEventTime(timeline, GameTimer);
    }

    private void TickContinuousEvent(SpawnEvent spawnEvent, SpawnEventRuntimeState state, float previousTime, float startTime)
    {
        float duration = Mathf.Max(0f, spawnEvent.duration);
        if (duration <= 0f)
        {
            return;
        }

        float endTime = startTime + duration;
        float activeStart = Mathf.Max(previousTime, startTime);
        float activeEnd = Mathf.Min(GameTimer, endTime);
        if (activeEnd <= activeStart)
        {
            return;
        }

        state.continuousAccumulator += (activeEnd - activeStart) * Mathf.Max(0, spawnEvent.count);
        int spawnCount = Mathf.FloorToInt(state.continuousAccumulator);
        if (spawnCount <= 0)
        {
            return;
        }

        state.continuousAccumulator -= spawnCount;
        for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
        {
            if (SpawnEnemy(spawnEvent.shipData, GetRandomOffscreenSpawnPosition()))
            {
                SpawnedThisFrame++;
            }
        }
    }

    private int ExecuteOneShotPattern(SpawnEvent spawnEvent, int count, Vector3 playerPosition)
    {
        switch (spawnEvent.pattern)
        {
            case SpawnPatternType.Burst:
                return ExecuteBurstPattern(spawnEvent.shipData, count);
            case SpawnPatternType.Ring:
                return ExecuteRingPattern(spawnEvent.shipData, count, playerPosition);
            case SpawnPatternType.Wall:
                return ExecuteWallPattern(spawnEvent.shipData, count);
            case SpawnPatternType.Continuous:
            default:
                return 0;
        }
    }

    private int ExecuteBurstPattern(ShipDataSO shipData, int count)
    {
        int spawned = 0;
        Vector3 center = GetRandomOffscreenSpawnPosition();
        const float scatterRadius = 1.1f;
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * scatterRadius;
            if (SpawnEnemy(shipData, center + new Vector3(offset.x, offset.y, 0f)))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private int ExecuteRingPattern(ShipDataSO shipData, int count, Vector3 playerPosition)
    {
        Camera camera = GetSpawnCamera();
        if (camera == null)
        {
            return 0;
        }

        int spawned = 0;
        float depth = Mathf.Abs(camera.transform.position.z);
        Vector3 min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        float radius = Vector2.Distance(min, max) * 0.55f;
        float startAngle = UnityEngine.Random.value * Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + ((Mathf.PI * 2f) * i / Mathf.Max(1, count));
            Vector3 position = playerPosition + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            if (SpawnEnemy(shipData, position))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private int ExecuteWallPattern(ShipDataSO shipData, int count)
    {
        int spawned = 0;
        int side = UnityEngine.Random.Range(0, 4);
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            if (SpawnEnemy(shipData, GetOffscreenSpawnPoint(side, t, offscreenViewportMargin)))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private void EnsureSpawnEventRuntimeStates(WaveTimelineSO activeTimeline)
    {
        if (activeTimeline == null || activeTimeline.events == null)
        {
            spawnEventStates.Clear();
            return;
        }

        while (spawnEventStates.Count < activeTimeline.events.Count)
        {
            spawnEventStates.Add(new SpawnEventRuntimeState());
        }

        while (spawnEventStates.Count > activeTimeline.events.Count)
        {
            spawnEventStates.RemoveAt(spawnEventStates.Count - 1);
        }
    }

    private Vector3 GetRandomOffscreenSpawnPosition()
    {
        int side = UnityEngine.Random.Range(0, 4);
        float t = UnityEngine.Random.value;
        return GetOffscreenSpawnPoint(side, t, offscreenViewportMargin);
    }

    private Vector3 GetOffscreenSpawnPoint(int side, float edgeLerp, float viewportMargin)
    {
        Camera camera = GetSpawnCamera();
        if (camera == null)
        {
            return Vector3.zero;
        }

        float margin = Mathf.Max(0.01f, viewportMargin);
        float t = Mathf.Clamp01(edgeLerp);
        float depth = Mathf.Abs(camera.transform.position.z);

        float x;
        float y;
        switch (Mathf.Abs(side) % 4)
        {
            case 0:
                x = Mathf.Lerp(-margin, 1f + margin, t);
                y = 1f + margin;
                break;
            case 1:
                x = Mathf.Lerp(-margin, 1f + margin, t);
                y = -margin;
                break;
            case 2:
                x = -margin;
                y = Mathf.Lerp(-margin, 1f + margin, t);
                break;
            default:
                x = 1f + margin;
                y = Mathf.Lerp(-margin, 1f + margin, t);
                break;
        }

        Vector3 world = camera.ViewportToWorldPoint(new Vector3(x, y, depth));
        world.z = 0f;
        return world;
    }

    private float GetNextTimelineEventTime(WaveTimelineSO timeline, float now)
    {
        if (timeline == null || timeline.events == null || timeline.events.Count == 0)
        {
            return -1f;
        }

        float nextTime = float.MaxValue;
        for (int i = 0; i < timeline.events.Count; i++)
        {
            SpawnEvent spawnEvent = timeline.events[i];
            if (spawnEvent == null)
            {
                continue;
            }

            float startTime = Mathf.Max(0f, spawnEvent.startTime);
            if (spawnEvent.pattern == SpawnPatternType.Continuous)
            {
                float duration = Mathf.Max(0f, spawnEvent.duration);
                if (duration <= 0f)
                {
                    continue;
                }

                float endTime = startTime + duration;
                if (now <= endTime)
                {
                    if (now < startTime)
                    {
                        nextTime = Mathf.Min(nextTime, startTime);
                    }
                    else
                    {
                        return now;
                    }
                }
                continue;
            }

            if (i < spawnEventStates.Count && spawnEventStates[i].oneShotExecuted)
            {
                continue;
            }

            if (now < startTime)
            {
                nextTime = Mathf.Min(nextTime, startTime);
            }
            else
            {
                return now;
            }
        }

        return nextTime == float.MaxValue ? -1f : nextTime;
    }

    private bool SpawnEnemy(ShipDataSO shipData, Vector3 position)
    {
        return spawnEnemyCallback != null && spawnEnemyCallback(shipData, position, GetLevelScale());
    }

    private Camera GetSpawnCamera()
    {
        if (spawnCamera == null)
        {
            spawnCamera = Camera.main;
        }

        return spawnCamera;
    }
}
