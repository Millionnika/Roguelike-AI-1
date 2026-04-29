using UnityEngine;

public sealed class ShipThrusterEffect : MonoBehaviour
{
    [Tooltip("Inspector: idle intensity")]
    [SerializeField, Range(0f, 1f)] private float idleIntensity = 0.18f;
    [Tooltip("Inspector: pulse amplitude")]
    [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.22f;
    [Tooltip("Inspector: pulse speed")]
    [SerializeField, Min(0.1f)] private float pulseSpeed = 9f;
    [Tooltip("Inspector: max scale multiplier")]
    [SerializeField, Min(1f)] private float maxScaleMultiplier = 1.35f;

    private SpriteRenderer[] fireRenderers;
    private Color[] baseColors;
    private Vector3[] baseScales;
    private float intensity = 0.5f;
    private float pulseOffset;

    private void Awake()
    {
        CacheRenderers();
    }

    private void OnEnable()
    {
        pulseOffset = Random.value * Mathf.PI * 2f;
        CacheRenderers();
    }

    public void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
    }

    private void LateUpdate()
    {
        if (fireRenderers == null || fireRenderers.Length == 0)
        {
            return;
        }

        float activeIntensity = Mathf.Max(idleIntensity, intensity);
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed + pulseOffset) * pulseAmplitude;
        float alpha = Mathf.Clamp01(activeIntensity * pulse);
        float scaleMultiplier = Mathf.Lerp(0.75f, maxScaleMultiplier, activeIntensity) * Mathf.Lerp(0.9f, 1.08f, pulse - 0.78f);

        for (int i = 0; i < fireRenderers.Length; i++)
        {
            SpriteRenderer renderer = fireRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = baseColors[i];
            color.a = Mathf.Clamp01(baseColors[i].a * alpha);
            renderer.color = color;
            renderer.transform.localScale = baseScales[i] * scaleMultiplier;
        }
    }

    private void CacheRenderers()
    {
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        int count = 0;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (IsEngineFire(allRenderers[i]))
            {
                count++;
            }
        }

        fireRenderers = new SpriteRenderer[count];
        baseColors = new Color[count];
        baseScales = new Vector3[count];

        int index = 0;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            SpriteRenderer renderer = allRenderers[i];
            if (!IsEngineFire(renderer))
            {
                continue;
            }

            fireRenderers[index] = renderer;
            baseColors[index] = renderer.color;
            baseScales[index] = renderer.transform.localScale;
            index++;
        }
    }

    private static bool IsEngineFire(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        string lowerName = renderer.name.ToLowerInvariant();
        return lowerName.Contains("engine_fire") || lowerName.Contains("enginefire") || lowerName.Contains("flame");
    }
}
