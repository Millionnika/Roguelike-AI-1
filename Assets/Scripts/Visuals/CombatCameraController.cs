using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class CombatCameraController : MonoBehaviour
{
    [Header("Камера боя")]
    [Tooltip("Камера, которая используется как основная камера боя. Если поле пустое, компонент найдет Camera.main или создаст новую Main Camera.")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Стартовый orthographic size основной камеры. Определяет базовый масштаб обзора при запуске боя.")]
    [SerializeField, Min(1f)] private float defaultOrthographicSize = 9f;
    [Tooltip("Минимально допустимый zoom камеры. Чем меньше значение, тем ближе камера может приблизиться к кораблю.")]
    [SerializeField, Min(1f)] private float minOrthographicSize = 5f;
    [Tooltip("Максимально допустимый zoom камеры. Чем больше значение, тем дальше камера может отдалиться от корабля.")]
    [SerializeField, Min(1f)] private float maxOrthographicSize = 16f;
    [Tooltip("Шаг изменения zoom от колесика мыши. Рекомендуемый диапазон: 0.5-2.")]
    [SerializeField, Min(0.1f)] private float zoomStep = 1.2f;
    [Tooltip("Сглаживание изменения zoom. Больше значение - быстрее камера приходит к новому масштабу.")]
    [SerializeField, Min(0.1f)] private float zoomSmoothing = 10f;
    [Tooltip("Сглаживание следования камеры за игроком. Больше значение - камера быстрее догоняет цель.")]
    [SerializeField, Min(0.1f)] private float followSmoothing = 6f;
    [Tooltip("Упреждение камеры по скорости цели. 0 = камера смотрит точно на корабль, 1 = сильнее смещается по направлению движения.")]
    [SerializeField, Range(0f, 1f)] private float velocityLookAhead = 0.15f;

    private Transform target;
    private float targetOrthographicSize;

    public Camera CurrentCamera => targetCamera;

    private void Awake()
    {
        Initialize(targetCamera);
    }

    private void OnValidate()
    {
        minOrthographicSize = Mathf.Max(1f, minOrthographicSize);
        maxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
        defaultOrthographicSize = Mathf.Clamp(defaultOrthographicSize, minOrthographicSize, maxOrthographicSize);
        zoomStep = Mathf.Max(0.1f, zoomStep);
        zoomSmoothing = Mathf.Max(0.1f, zoomSmoothing);
        followSmoothing = Mathf.Max(0.1f, followSmoothing);
    }

    public void Initialize(Camera camera)
    {
        targetCamera = camera != null ? camera : targetCamera;
        EnsureCamera();
        ResetCamera();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void Tick(float deltaTime)
    {
        if (targetCamera == null)
        {
            return;
        }

        float scrollY = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            targetOrthographicSize -= Mathf.Sign(scrollY) * zoomStep;
            targetOrthographicSize = Mathf.Clamp(targetOrthographicSize, minOrthographicSize, maxOrthographicSize);
        }

        targetCamera.orthographicSize = Mathf.Lerp(
            targetCamera.orthographicSize,
            targetOrthographicSize,
            zoomSmoothing * Mathf.Max(0f, deltaTime));
    }

    public void LateTick(float deltaTime, Vector2 targetVelocity)
    {
        if (target == null || targetCamera == null)
        {
            return;
        }

        Vector3 current = target.position;
        Vector3 lookAhead = new Vector3(targetVelocity.x, targetVelocity.y, 0f) * velocityLookAhead;
        Vector3 targetPosition = new Vector3(current.x, current.y, -10f) + new Vector3(lookAhead.x, lookAhead.y, 0f);
        targetCamera.transform.position = Vector3.Lerp(
            targetCamera.transform.position,
            targetPosition,
            followSmoothing * Mathf.Max(0f, deltaTime));
    }

    public void ResetCamera()
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.orthographic = true;
        targetOrthographicSize = Mathf.Clamp(defaultOrthographicSize, minOrthographicSize, maxOrthographicSize);
        targetCamera.orthographicSize = targetOrthographicSize;
        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = new Color(0.01f, 0.03f, 0.05f);
    }

    private void EnsureCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        targetCamera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
    }
}
