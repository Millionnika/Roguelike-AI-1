using UnityEngine;

[DisallowMultipleComponent]
public sealed class ObjectiveMarker : MonoBehaviour
{
    [Header("Параметры маркера")]
    [Tooltip("Радиус активной зоны вокруг маркера.")]
    [SerializeField, Min(0.1f)] private float radius = 4f;

    public float Radius => radius;

    public void SetRadius(float value)
    {
        radius = Mathf.Max(0.1f, value);
        UpdateScale();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);
        UpdateScale();
    }

    private void Awake()
    {
        UpdateScale();
    }

    private void UpdateScale()
    {
        float diameter = radius * 2f;
        transform.localScale = new Vector3(diameter, diameter, 1f);
    }
}
