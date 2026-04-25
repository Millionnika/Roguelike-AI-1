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

    private Vector2 velocitySmoothRef;
    private bool wasPointerPressed;
    private float pointerPressDuration;
    private Vector3 pointerPressWorld;
    private bool holdModeActive;
    private Vector3 holdWorldTarget;

    public void UpdateMovement(PlayerShip player, MovementUpdateContext context, MovementSettingsSO settings, float deltaTime)
    {
        if (player == null || player.Transform == null || settings == null)
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
            desiredDirection = GetHoldDirection(player, settings);
        }
        else
        {
            desiredDirection = GetClickDirection(player, settings);
        }

        float desiredSpeed = 0f;
        if (desiredDirection.sqrMagnitude > 0.001f)
        {
            desiredSpeed = settings.moveSpeed * player.SpeedMultiplier;
        }

        Vector2 desiredVelocity = desiredDirection * desiredSpeed;
        float smoothTime = Mathf.Clamp(1f / Mathf.Max(0.1f, player.Acceleration), 0.04f, 0.25f);
        float maxSpeed = Mathf.Max(0.1f, settings.moveSpeed * Mathf.Max(0.1f, player.SpeedMultiplier));
        player.Velocity = Vector2.SmoothDamp(player.Velocity, desiredVelocity, ref velocitySmoothRef, smoothTime, maxSpeed, deltaTime);
        player.Velocity = Vector2.Lerp(player.Velocity, Vector2.zero, Mathf.Clamp01(player.Drag * deltaTime));
        player.Transform.position += (Vector3)(player.Velocity * deltaTime);

        if (player.Velocity.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(player.Velocity.y, player.Velocity.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
            player.Transform.rotation = Quaternion.Lerp(
                player.Transform.rotation,
                targetRotation,
                settings.rotationSpeed * deltaTime);
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

    private Vector2 GetHoldDirection(PlayerShip player, MovementSettingsSO settings)
    {
        Vector2 toTarget = (Vector2)(holdWorldTarget - player.Transform.position);
        float distance = toTarget.magnitude;
        if (distance <= settings.stoppingDistance * 0.5f)
        {
            return Vector2.zero;
        }

        return distance > 0.001f ? toTarget / distance : Vector2.zero;
    }

    private static Vector2 GetClickDirection(PlayerShip player, MovementSettingsSO settings)
    {
        if (!player.MoveCommandActive)
        {
            return Vector2.zero;
        }

        Vector2 toTarget = (Vector2)(player.MoveCommandTarget - player.Transform.position);
        float distance = toTarget.magnitude;

        if (distance <= settings.stoppingDistance && player.Velocity.magnitude <= 0.2f)
        {
            player.MoveCommandActive = false;
            return Vector2.zero;
        }

        return distance > 0.001f ? toTarget / distance : Vector2.zero;
    }
}
