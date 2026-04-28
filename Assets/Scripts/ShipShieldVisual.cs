using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShipShieldVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("SpriteRenderer щита (обычно Shield_0 или Aura), у которого будет меняться прозрачность и цвет.")]
    [SerializeField] private SpriteRenderer shieldRenderer;
    [Tooltip("Материал щита. Оставьте пустым, если хотите использовать материал со SpriteRenderer.")]
    [SerializeField] private Material shieldMaterial;
    [Tooltip("Спрайт для короткой вспышки/кольца в точке попадания по щиту.")]
    [SerializeField] private Sprite impactSprite;

    [Header("Shield")]
    [Tooltip("Базовый цвет щита. По этому цвету считается итоговая прозрачность.")]
    [SerializeField] private Color baseColor = Color.white;
    [Tooltip("Если включено, базовый цвет автоматически берется из текущего цвета SpriteRenderer.")]
    [SerializeField] private bool readBaseColorFromRenderer = true;
    [Tooltip("Сила дыхания щита (амплитуда пульсации прозрачности).")]
    [SerializeField, Range(0f, 0.6f)] private float pulseAlpha = 0.12f;
    [Tooltip("Скорость дыхания щита.")]
    [SerializeField, Min(0.1f)] private float pulseSpeed = 3.2f;
    [Tooltip("Насколько щит становится ярче в момент попадания.")]
    [SerializeField, Range(0f, 2f)] private float hitAlphaBoost = 0.55f;
    [Tooltip("Сила подкрашивания щита в цвет попадания.")]
    [SerializeField, Range(0f, 1f)] private float hitTintStrength = 0.65f;
    [Tooltip("Цвет подсветки щита в момент попадания.")]
    [SerializeField] private Color hitTint = new Color(0.72f, 0.95f, 1f, 1f);

    [Header("Hit Ripple")]
    [Tooltip("Длительность вспышки/кольца попадания по щиту (секунды).")]
    [SerializeField, Min(0.05f)] private float impactDuration = 0.24f;
    [Tooltip("Начальный масштаб кольца попадания.")]
    [SerializeField, Min(0f)] private float impactStartScale = 0.08f;
    [Tooltip("Конечный масштаб кольца попадания.")]
    [SerializeField, Min(0.01f)] private float impactEndScale = 0.52f;
    [Tooltip("Максимальная прозрачность вспышки/кольца попадания.")]
    [SerializeField, Range(0f, 2f)] private float impactAlpha = 1.2f;
    [Tooltip("Как сильно урон щиту влияет на яркость вспышки.")]
    [SerializeField, Range(0f, 3f)] private float impactDamageScale = 0.025f;
    [Tooltip("Цвет вспышки/кольца попадания.")]
    [SerializeField] private Color impactColor = new Color(0.78f, 0.97f, 1f, 1f);
    [Tooltip("Смещение sorting order относительно спрайта щита.")]
    [SerializeField] private int impactSortingOrderOffset = 1;

    private readonly List<ImpactRipple> activeRipples = new List<ImpactRipple>();
    private float pulseOffset;

    private sealed class ImpactRipple
    {
        public SpriteRenderer Renderer;
        public float Timer;
        public float Intensity;
    }

    public void Initialize(SpriteRenderer renderer, Material material, Sprite rippleSprite, Color fallbackBaseColor, float randomPulseOffset)
    {
        if (shieldRenderer == null)
        {
            shieldRenderer = renderer;
        }

        if (shieldMaterial == null)
        {
            shieldMaterial = material;
        }

        if (impactSprite == null)
        {
            impactSprite = rippleSprite;
        }

        pulseOffset = randomPulseOffset;

        if (shieldRenderer == null)
        {
            return;
        }

        if (shieldMaterial != null)
        {
            shieldRenderer.sharedMaterial = shieldMaterial;
        }

        Color rendererColor = shieldRenderer.color;
        if (readBaseColorFromRenderer && rendererColor.a > 0.001f)
        {
            baseColor = rendererColor;
        }
        else if (baseColor.a <= 0f)
        {
            baseColor = fallbackBaseColor.a > 0.001f ? fallbackBaseColor : Color.white;
        }
    }

    public void SetShieldState(float shieldPercent, float hitFlash)
    {
        if (shieldRenderer == null)
        {
            return;
        }

        float percent = Mathf.Clamp01(shieldPercent);
        float hit = Mathf.Clamp01(hitFlash);
        float pulse = 1f + Mathf.Sin((Time.time + pulseOffset) * pulseSpeed) * pulseAlpha;
        float alpha = baseColor.a * percent * pulse * (1f + hit * hitAlphaBoost);

        Color color = Color.Lerp(baseColor, hitTint, hit * hitTintStrength);
        color.a = Mathf.Clamp01(alpha);
        shieldRenderer.color = color;
        shieldRenderer.enabled = color.a > 0.001f;
    }

    public void PlayImpact(Vector2 worldPoint, float absorbedShieldDamage)
    {
        if (shieldRenderer == null || impactSprite == null)
        {
            return;
        }

        GameObject rippleObject = new GameObject("ShieldImpactRipple");
        rippleObject.transform.SetParent(transform, true);
        rippleObject.transform.position = new Vector3(worldPoint.x, worldPoint.y, shieldRenderer.transform.position.z);
        rippleObject.transform.rotation = Quaternion.identity;

        SpriteRenderer rippleRenderer = rippleObject.AddComponent<SpriteRenderer>();
        rippleRenderer.sprite = impactSprite;
        rippleRenderer.sortingLayerID = shieldRenderer.sortingLayerID;
        rippleRenderer.sortingOrder = shieldRenderer.sortingOrder + impactSortingOrderOffset;
        rippleRenderer.sharedMaterial = shieldRenderer.sharedMaterial;

        float intensity = Mathf.Clamp01(absorbedShieldDamage * impactDamageScale);
        activeRipples.Add(new ImpactRipple
        {
            Renderer = rippleRenderer,
            Timer = 0f,
            Intensity = Mathf.Max(0.15f, intensity)
        });
    }

    private void Update()
    {
        UpdateRipples(Time.deltaTime);
    }

    private void OnDisable()
    {
        ClearRipples();
    }

    private void OnDestroy()
    {
        ClearRipples();
    }

    private void UpdateRipples(float deltaTime)
    {
        for (int i = activeRipples.Count - 1; i >= 0; i--)
        {
            ImpactRipple ripple = activeRipples[i];
            if (ripple == null || ripple.Renderer == null)
            {
                activeRipples.RemoveAt(i);
                continue;
            }

            ripple.Timer += Mathf.Max(0f, deltaTime);
            float duration = Mathf.Max(0.05f, impactDuration);
            float t = Mathf.Clamp01(ripple.Timer / duration);
            float fadeOut = 1f - t;
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            float scale = Mathf.Lerp(Mathf.Max(0f, impactStartScale), Mathf.Max(impactStartScale, impactEndScale), eased);

            ripple.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
            Color color = impactColor;
            color.a = Mathf.Clamp01(impactAlpha * ripple.Intensity * fadeOut);
            ripple.Renderer.color = color;

            if (t >= 1f)
            {
                Destroy(ripple.Renderer.gameObject);
                activeRipples.RemoveAt(i);
            }
        }
    }

    private void ClearRipples()
    {
        for (int i = 0; i < activeRipples.Count; i++)
        {
            ImpactRipple ripple = activeRipples[i];
            if (ripple != null && ripple.Renderer != null)
            {
                Destroy(ripple.Renderer.gameObject);
            }
        }

        activeRipples.Clear();
    }
}
