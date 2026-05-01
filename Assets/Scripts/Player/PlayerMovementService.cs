using SpaceFrontier.Player;
using UnityEngine;
using UnityEngine.InputSystem;

internal readonly struct PointerInputState
{
    public PointerInputState(bool hasPointer, Vector2 screenPosition, bool primaryPressed)
    {
        HasPointer = hasPointer;
        ScreenPosition = screenPosition;
        PrimaryPressed = primaryPressed;
    }

    public bool HasPointer { get; }
    public Vector2 ScreenPosition { get; }
    public bool PrimaryPressed { get; }
}

internal readonly struct MovementUpdateContext
{
    public MovementUpdateContext(
        Vector2 manualInput,
        bool pointerBlocked,
        PointerInputState pointerState,
        Vector3 pointerWorldPosition)
    {
        ManualInput = manualInput;
        PointerBlocked = pointerBlocked;
        PointerState = pointerState;
        PointerWorldPosition = pointerWorldPosition;
    }

    public Vector2 ManualInput { get; }
    public bool PointerBlocked { get; }
    public PointerInputState PointerState { get; }
    public Vector3 PointerWorldPosition { get; }
}

internal sealed class PlayerInputService : IInputService
{
    public PointerInputState ReadPointerState()
    {
        if (Touchscreen.current != null)
        {
            return new PointerInputState(
                true,
                Touchscreen.current.primaryTouch.position.ReadValue(),
                Touchscreen.current.primaryTouch.press.isPressed);
        }

        if (Mouse.current != null)
        {
            return new PointerInputState(
                true,
                Mouse.current.position.ReadValue(),
                Mouse.current.leftButton.isPressed);
        }

        return new PointerInputState(false, Vector2.zero, false);
    }
}

internal sealed class PlayerMovementService : IMovementService
{
    private const float ClickThresholdSeconds = 0.16f;
    private const float ClickMaxTravel = 0.45f;
    private const float ClickStopDistance = 0.25f;
    private const float RotationVelocityThreshold = 0.18f;
    private const float BaseCollisionRadiusFallback = 0.42f;
    private static readonly Collider2D[] BaseCollisionBuffer = new Collider2D[16];

    private Vector2 velocitySmoothRef;
    private bool wasPointerPressed;
    private float pointerPressDuration;
    private Vector3 pointerPressWorld;
    private bool holdModeActive;
    private Vector3 holdWorldTarget;

    public void UpdateMovement(PlayerShip player, MovementUpdateContext context, float deltaTime)
    {
        if (player == null || player.Transform == null)
        {
            return;
        }

        HandlePointerMode(player, context, deltaTime);

        Vector2 manualInput = context.ManualInput;
        if (manualInput.sqrMagnitude > 0.01f)
        {
            player.MoveCommandActive = false;
        }

        Vector2 desiredDirection;
        if (manualInput.sqrMagnitude > 0.01f)
        {
            desiredDirection = manualInput.normalized;
        }
        else if (holdModeActive)
        {
            desiredDirection = GetHoldDirection(player);
        }
        else
        {
            desiredDirection = GetClickDirection(player);
        }

        float desiredSpeed = 0f;
        if (desiredDirection.sqrMagnitude > 0.001f)
        {
            desiredSpeed = player.Speed * player.SpeedMultiplier;
        }

        Vector2 desiredVelocity = desiredDirection * desiredSpeed;
        float smoothTime = Mathf.Clamp(1f / Mathf.Max(0.1f, player.Acceleration), 0.04f, 0.25f);
        float maxSpeed = Mathf.Max(0.1f, player.Speed * Mathf.Max(0.1f, player.SpeedMultiplier));
        player.Velocity = Vector2.SmoothDamp(player.Velocity, desiredVelocity, ref velocitySmoothRef, smoothTime, maxSpeed, deltaTime);
        player.Velocity = Vector2.Lerp(player.Velocity, Vector2.zero, Mathf.Clamp01(player.Drag * deltaTime));
        Vector3 movementDelta = (Vector3)(player.Velocity * deltaTime);
        MoveWithBaseCollision(player, movementDelta);

        if (player.Velocity.sqrMagnitude > RotationVelocityThreshold * RotationVelocityThreshold)
        {
            float angle = Mathf.Atan2(player.Velocity.y, player.Velocity.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
            player.Transform.rotation = Quaternion.Lerp(
                player.Transform.rotation,
                targetRotation,
                player.RotationResponsiveness * deltaTime);
        }
    }

    private void HandlePointerMode(PlayerShip player, MovementUpdateContext context, float deltaTime)
    {
        bool pointerPressed = context.PointerState.HasPointer && context.PointerState.PrimaryPressed;

        if (pointerPressed && !context.PointerBlocked)
        {
            if (!wasPointerPressed)
            {
                pointerPressDuration = 0f;
                pointerPressWorld = context.PointerWorldPosition;
            }

            pointerPressDuration += deltaTime;
            holdModeActive = true;
            holdWorldTarget = context.PointerWorldPosition;
            player.MoveCommandActive = false;
            player.MoveCommandTarget = context.PointerWorldPosition;
        }
        else if (!pointerPressed && wasPointerPressed && !context.PointerBlocked)
        {
            float traveled = Vector3.Distance(pointerPressWorld, context.PointerWorldPosition);
            bool isClick = pointerPressDuration <= ClickThresholdSeconds && traveled <= ClickMaxTravel;
            if (isClick)
            {
                player.MoveCommandActive = true;
                player.MoveCommandTarget = context.PointerWorldPosition;
            }
        }
        else if (pointerPressed && context.PointerBlocked)
        {
            holdModeActive = false;
        }

        if (!pointerPressed)
        {
            holdModeActive = false;
        }

        wasPointerPressed = pointerPressed;
    }

    private Vector2 GetHoldDirection(PlayerShip player)
    {
        Vector2 toTarget = (Vector2)(holdWorldTarget - player.Transform.position);
        float distance = toTarget.magnitude;
        if (distance <= ClickStopDistance * 0.5f)
        {
            return Vector2.zero;
        }

        return distance > 0.001f ? toTarget / distance : Vector2.zero;
    }

    private Vector2 GetClickDirection(PlayerShip player)
    {
        if (!player.MoveCommandActive)
        {
            return Vector2.zero;
        }

        Vector2 toTarget = (Vector2)(player.MoveCommandTarget - player.Transform.position);
        float distance = toTarget.magnitude;

        if (distance <= ClickStopDistance)
        {
            StopClickMovement(player);
            return Vector2.zero;
        }

        return distance > 0.001f ? toTarget / distance : Vector2.zero;
    }

    private void StopClickMovement(PlayerShip player)
    {
        player.MoveCommandActive = false;
        player.Velocity = Vector2.zero;
        velocitySmoothRef = Vector2.zero;
    }

    private static void MoveWithBaseCollision(PlayerShip player, Vector3 delta)
    {
        if (player == null || player.Transform == null || delta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Vector3 current = player.Transform.position;
        Vector3 target = current + delta;
        float radius = ResolveCollisionRadius(player.Transform);

        if (!IsBlockedByBase(target, radius, player.Transform))
        {
            player.Transform.position = target;
            return;
        }

        Vector3 xOnly = current + new Vector3(delta.x, 0f, 0f);
        Vector3 yOnly = current + new Vector3(0f, delta.y, 0f);
        bool xFree = !IsBlockedByBase(xOnly, radius, player.Transform);
        bool yFree = !IsBlockedByBase(yOnly, radius, player.Transform);

        if (xFree && yFree)
        {
            player.Transform.position = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? xOnly : yOnly;
            return;
        }

        if (xFree)
        {
            player.Transform.position = xOnly;
            return;
        }

        if (yFree)
        {
            player.Transform.position = yOnly;
            return;
        }

        player.Velocity = Vector2.zero;
    }

    private static float ResolveCollisionRadius(Transform root)
    {
        if (root == null)
        {
            return BaseCollisionRadiusFallback;
        }

        Collider2D collider = root.GetComponentInChildren<Collider2D>();
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y);
            if (extent > 0.01f)
            {
                return extent * 0.9f;
            }
        }

        return BaseCollisionRadiusFallback;
    }

    private static bool IsBlockedByBase(Vector3 position, float radius, Transform selfRoot)
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(position, Mathf.Max(0.1f, radius), BaseCollisionBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = BaseCollisionBuffer[i];
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            if (selfRoot != null && collider.transform.root == selfRoot)
            {
                continue;
            }

            EnemyBaseLair baseLair = collider.GetComponentInParent<EnemyBaseLair>();
            if (baseLair != null && baseLair.IsAlive)
            {
                return true;
            }
        }

        return false;
    }
}
