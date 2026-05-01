using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class UIButtonScaleAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField, Range(1f, 1.2f)] private float hoverScale = 1.06f;
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.94f;
    [SerializeField, Min(1f)] private float scaleLerpSpeed = 16f;

    private Vector3 baseScale = Vector3.one;
    private float targetScale = 1f;
    private bool hovered;
    private bool pressed;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        baseScale = transform.localScale;
        targetScale = 1f;
        hovered = false;
        pressed = false;
    }

    private void Update()
    {
        float current = baseScale.x != 0f ? transform.localScale.x / baseScale.x : 1f;
        float next = Mathf.Lerp(current, targetScale, scaleLerpSpeed * Time.unscaledDeltaTime);
        transform.localScale = baseScale * next;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        UpdateTargetScale();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        UpdateTargetScale();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        UpdateTargetScale();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        UpdateTargetScale();
    }

    private void UpdateTargetScale()
    {
        if (pressed)
        {
            targetScale = pressedScale;
            return;
        }

        targetScale = hovered ? hoverScale : 1f;
    }
}
