using System;
using SpaceFrontier.Player;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SectorWarpController : MonoBehaviour
{
    [Header("Параметры варпа")]
    [Tooltip("Базовая скорость автоматического перелета между секторами.")]
    [SerializeField, Min(1f)] private float warpSpeed = 50f;
    [Tooltip("Дистанция до точки, на которой перелет считается завершенным.")]
    [SerializeField, Min(0.05f)] private float arrivalDistance = 1f;
    [Tooltip("Кривая множителя скорости по прогрессу перелета (0..1).")]
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [Tooltip("Отключать ручное управление кораблем на время варпа.")]
    [SerializeField] private bool disablePlayerInputDuringWarp = true;
    [Tooltip("Сбрасывать текущую скорость корабля в начале варпа.")]
    [SerializeField] private bool clearVelocityOnStart = true;
    [Tooltip("Сбрасывать скорость корабля после прибытия.")]
    [SerializeField] private bool clearVelocityOnArrival = true;
    [Tooltip("Префаб визуального эффекта варпа (необязательно).")]
    [SerializeField] private GameObject warpVfxPrefab;
    [Tooltip("Масштаб визуального эффекта варпа.")]
    [SerializeField, Min(0.01f)] private float warpVfxScale = 1f;

    private PlayerShip activePlayer;
    private Vector3 targetPosition;
    private Action onArrived;
    private GameObject runtimeWarpVfx;
    private Vector3 warpStartPosition;
    private bool hasStartPosition;

    public bool IsWarping { get; private set; }

    public bool StartWarp(PlayerShip player, Vector3 target, Action arrivedCallback)
    {
        if (player == null || player.Transform == null)
        {
            Debug.LogWarning("SectorWarpController: не удалось начать варп, потому что корабль игрока не найден.", this);
            arrivedCallback?.Invoke();
            return false;
        }

        activePlayer = player;
        targetPosition = new Vector3(target.x, target.y, player.Transform.position.z);
        onArrived = arrivedCallback;
        IsWarping = true;
        warpStartPosition = player.Transform.position;
        hasStartPosition = true;

        if (clearVelocityOnStart)
        {
            ClearPlayerVelocity(player);
        }

        SpawnWarpVfxIfNeeded();
        return true;
    }

    private void Update()
    {
        if (!IsWarping || activePlayer == null || activePlayer.Transform == null)
        {
            return;
        }

        Vector3 current = activePlayer.Transform.position;
        Vector3 toTarget = targetPosition - current;
        float distance = toTarget.magnitude;
        if (distance <= arrivalDistance)
        {
            CompleteWarp();
            return;
        }

        float speedMultiplier = EvaluateSpeedMultiplier(current);
        float step = Mathf.Max(0.01f, warpSpeed * speedMultiplier) * Time.deltaTime;
        Vector3 nextPosition = Vector3.MoveTowards(current, targetPosition, step);
        activePlayer.Transform.position = nextPosition;

        Vector2 dir = distance > 0.001f ? ((Vector2)toTarget).normalized : Vector2.zero;
        activePlayer.Velocity = dir * Mathf.Max(0.01f, warpSpeed * speedMultiplier);
        
        if (dir != Vector2.zero)
        {
            activePlayer.Transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);
        }

        if (disablePlayerInputDuringWarp)
        {
            activePlayer.MoveCommandActive = false;
        }

        if (runtimeWarpVfx != null)
        {
            runtimeWarpVfx.transform.position = nextPosition;
        }
    }

    private float EvaluateSpeedMultiplier(Vector3 current)
    {
        if (!hasStartPosition || speedCurve == null)
        {
            return 1f;
        }

        float totalDistance = Vector3.Distance(warpStartPosition, targetPosition);
        if (totalDistance <= 0.001f)
        {
            return 1f;
        }

        float coveredDistance = Vector3.Distance(warpStartPosition, current);
        float t = Mathf.Clamp01(coveredDistance / totalDistance);
        return Mathf.Max(0.05f, speedCurve.Evaluate(t));
    }

    private void CompleteWarp()
    {
        if (activePlayer != null && activePlayer.Transform != null)
        {
            activePlayer.Transform.position = targetPosition;
            if (clearVelocityOnArrival)
            {
                ClearPlayerVelocity(activePlayer);
            }
            else
            {
                // Сбрасываем только линейную скорость, сохраняя текущий поворот
                activePlayer.Velocity = Vector2.zero;
                if (activePlayer.Transform != null)
                {
                    Rigidbody2D body = activePlayer.Transform.GetComponent<Rigidbody2D>();
                    if (body != null)
                    {
                        body.linearVelocity = Vector2.zero;
                    }
                }
            }
        }

        CleanupVfx();
        IsWarping = false;
        hasStartPosition = false;
        PlayerShip finishedPlayer = activePlayer;
        Action callback = onArrived;
        activePlayer = null;
        onArrived = null;

        if (finishedPlayer != null && disablePlayerInputDuringWarp)
        {
            finishedPlayer.MoveCommandActive = false;
        }

        callback?.Invoke();
    }

    private void SpawnWarpVfxIfNeeded()
    {
        CleanupVfx();
        if (warpVfxPrefab == null || activePlayer == null || activePlayer.Transform == null)
        {
            return;
        }

        runtimeWarpVfx = Instantiate(warpVfxPrefab, activePlayer.Transform.position, Quaternion.identity);
        runtimeWarpVfx.transform.localScale = Vector3.one * warpVfxScale;
    }

    private void CleanupVfx()
    {
        if (runtimeWarpVfx != null)
        {
            Destroy(runtimeWarpVfx);
            runtimeWarpVfx = null;
        }
    }

    private static void ClearPlayerVelocity(PlayerShip player)
    {
        if (player == null)
        {
            return;
        }

        player.Velocity = Vector2.zero;
        player.MoveCommandActive = false;
        if (player.Transform != null)
        {
            Rigidbody2D body = player.Transform.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }
    }
}
