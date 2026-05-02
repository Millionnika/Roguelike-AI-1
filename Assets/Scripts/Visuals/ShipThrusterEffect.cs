using System.Collections.Generic;
using UnityEngine;

public sealed class ShipThrusterEffect : MonoBehaviour
{
    [Header("Старый спрайтовый выхлоп")]
    [Tooltip("Минимальная яркость старых SpriteRenderer-объектов выхлопа, даже когда корабль почти не движется. Рекомендуемый диапазон: 0.05-0.3.")]
    [SerializeField, Range(0f, 1f)] private float idleIntensity = 0.18f;
    [Tooltip("Амплитуда мягкого мерцания старого спрайтового выхлопа. Не влияет на новые ParticleSystem-эффекты.")]
    [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.22f;
    [Tooltip("Скорость мерцания старого спрайтового выхлопа. Рекомендуемый диапазон: 4-14.")]
    [SerializeField, Min(0.1f)] private float pulseSpeed = 9f;
    [Tooltip("Максимальный множитель масштаба старого спрайтового выхлопа при высокой тяге.")]
    [SerializeField, Min(1f)] private float maxScaleMultiplier = 1.35f;

    [Header("Particle VFX двигателя")]
    [Tooltip("Префаб эффекта двигателя. Создаётся на точках Engine_Fire_L и Engine_Fire_R. Если не назначен, используется простой fallback-эффект.")]
    [SerializeField] private GameObject engineVfxPrefab;
    [Tooltip("Включает легкий ParticleSystem-выхлоп на якорях Engine_Fire_L и Engine_Fire_R. Не добавляет SpriteRenderer на якоря.")]
    [SerializeField] private bool enableParticleVfx = true;
    [Tooltip("Базовая эмиссия частиц на каждый двигатель в состоянии покоя. Рекомендуемый диапазон: 0.1-2.")]
    [SerializeField, Min(0f)] private float idleEmissionRate = 0.5f;
    [Tooltip("Эмиссия частиц на каждый двигатель при движении корабля. Рекомендуемый диапазон: 8-24.")]
    [SerializeField, Min(0f)] private float movingEmissionRate = 18f;
    [Tooltip("Множитель эмиссии при форсаже. Сейчас используется как запас для внешней логики, если она передаст интенсивность выше обычной.")]
    [SerializeField, Min(1f)] private float afterburnerEmissionMultiplier = 2f;
    [Tooltip("Скорость сглаживания эмиссии между покоем и движением. Чем выше значение, тем быстрее двигатель реагирует на изменение скорости.")]
    [SerializeField, Min(0.1f)] private float emissionLerpSpeed = 8f;
    [Tooltip("Размер частиц огня двигателя. Рекомендуемый диапазон: 0.04-0.16.")]
    [SerializeField, Min(0.01f)] private float particleStartSize = 0.12f;
    [Tooltip("Время жизни частиц двигателя в секундах. Короткие значения дешевле и выглядят резче. Рекомендуемый диапазон: 0.2-0.5.")]
    [SerializeField, Min(0.05f)] private float particleLifetime = 0.35f;
    [Tooltip("Цвет огня двигателя. Альфа управляет прозрачностью частиц.")]
    [SerializeField] private Color engineColor = new Color(0.25f, 0.85f, 1f, 0.85f);
    [Tooltip("Порядок сортировки ParticleSystem. Значение ниже Body помогает держать выхлоп за корпусом.")]
    [SerializeField] private int sortingOrder = -3;
    [Tooltip("Масштаб создаваемого префаба эффекта двигателя на точках двигателя.")]
    [SerializeField, Min(0.01f)] private float engineVfxScale = 1f;
    [Tooltip("Debug-режим: создавать простой кодовый fallback, если Engine VFX Prefab не назначен. По умолчанию выключено, чтобы VFX настраивался через ShipDataSO.")]
    [SerializeField] private bool useFallbackWhenPrefabMissing = false;

    private const string ThrusterRootName = "Thruster";
    private const string PrefabInstanceChildName = "EngineVFX_Instance";
    private const string FallbackChildName = "EngineVFX_Fallback";
    private const string DefaultEngineVfxPath = "Assets/Content/VFX/Engine/EngineVFX_Default.prefab";

    private SpriteRenderer[] fireRenderers;
    private Color[] baseColors;
    private Vector3[] baseScales;
    private readonly List<ParticleSystem> engineParticles = new List<ParticleSystem>(4);
    private readonly List<ParticleSystem> particleBuffer = new List<ParticleSystem>(8);
    private float intensity;
    private float currentEmissionRate;
    private float pulseOffset;
    private bool missingAnchorsWarningLogged;
    private bool missingPrefabWarningLogged;
    private bool usingPrefabInstances;
    private GameObject activeEngineVfxPrefab;
    private bool recreatePrefabInstances;

    private void Awake()
    {
        CacheRenderers();
    }

    private void OnEnable()
    {
        pulseOffset = Random.value * Mathf.PI * 2f;
        CacheRenderers();
        if (engineVfxPrefab != null || useFallbackWhenPrefabMissing)
        {
            RebuildParticleEffects();
        }
    }

    public void ConfigureFromShipData(ShipDataSO shipData)
    {
        if (shipData == null)
        {
            Configure(null, 0f, movingEmissionRate, afterburnerEmissionMultiplier, emissionLerpSpeed, 1f);
            return;
        }

        Configure(
            shipData.engineVfxPrefab,
            shipData.engineIdleEmissionRate,
            shipData.engineMovingEmissionRate,
            shipData.engineAfterburnerEmissionMultiplier,
            shipData.engineEmissionLerpSpeed,
            shipData.engineVfxScale);
    }

    public void Configure(
        GameObject vfxPrefab,
        float idleEmission,
        float movingEmission,
        float afterburnerMultiplier,
        float lerpSpeed,
        float vfxScale)
    {
        bool prefabChanged = activeEngineVfxPrefab != vfxPrefab || !Mathf.Approximately(engineVfxScale, vfxScale);
        engineVfxPrefab = vfxPrefab;
        idleEmissionRate = Mathf.Max(0f, idleEmission);
        movingEmissionRate = Mathf.Max(0f, movingEmission);
        afterburnerEmissionMultiplier = Mathf.Max(1f, afterburnerMultiplier);
        emissionLerpSpeed = Mathf.Max(0f, lerpSpeed);
        engineVfxScale = Mathf.Max(0.01f, vfxScale);
        recreatePrefabInstances = prefabChanged;
        activeEngineVfxPrefab = engineVfxPrefab;
        currentEmissionRate = idleEmissionRate;
        missingPrefabWarningLogged = false;

        if (prefabChanged)
        {
            RebuildParticleEffects();
        }
        else
        {
            RebuildParticleEffects();
        }
    }

    public void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
    }

    private void LateUpdate()
    {
        float activeIntensity = Mathf.Max(idleIntensity, intensity);
        UpdateSpriteThrusters(activeIntensity);
        UpdateParticleThrusters(intensity);
    }

    private void UpdateSpriteThrusters(float activeIntensity)
    {
        if (fireRenderers == null || fireRenderers.Length == 0)
        {
            return;
        }

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

    private void UpdateParticleThrusters(float activeIntensity)
    {
        if (!enableParticleVfx || engineParticles.Count == 0)
        {
            return;
        }

        float emission = Mathf.Lerp(idleEmissionRate, movingEmissionRate, Mathf.Clamp01(activeIntensity));
        if (intensity >= 0.98f)
        {
            emission *= afterburnerEmissionMultiplier;
        }

        float lerpFactor = 1f - Mathf.Exp(-emissionLerpSpeed * Time.deltaTime);
        currentEmissionRate = Mathf.Lerp(currentEmissionRate, emission, lerpFactor);

        for (int i = 0; i < engineParticles.Count; i++)
        {
            ParticleSystem particleSystem = engineParticles[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
            emissionModule.rateOverTime = currentEmissionRate;
            if (!particleSystem.isPlaying)
            {
                particleSystem.Play();
            }
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

    private void RebuildParticleEffects()
    {
        if (!enableParticleVfx)
        {
            engineParticles.Clear();
            return;
        }

        Transform[] anchors = FindEngineAnchors();
        if (anchors.Length == 0)
        {
            if (!missingAnchorsWarningLogged)
            {
                Debug.LogWarning(
                    "Точки двигателя не найдены. Создайте дочерние объекты Thruster/Engine_Fire_1, Engine_Fire_2 и т.п.",
                    this);
                missingAnchorsWarningLogged = true;
            }

            engineParticles.Clear();
            return;
        }

        if (engineVfxPrefab == null && !useFallbackWhenPrefabMissing)
        {
            engineParticles.Clear();
            for (int i = 0; i < anchors.Length; i++)
            {
                RemoveGeneratedChild(anchors[i], PrefabInstanceChildName);
                RemoveGeneratedChild(anchors[i], FallbackChildName);
            }

            if (!missingPrefabWarningLogged)
            {
                Debug.LogWarning("Engine VFX Prefab не назначен в ShipDataSO. Эффект двигателя не будет создан.", this);
                missingPrefabWarningLogged = true;
            }

            return;
        }

        engineParticles.Clear();
        currentEmissionRate = idleEmissionRate;
        usingPrefabInstances = engineVfxPrefab != null;
        for (int i = 0; i < anchors.Length; i++)
        {
            RegisterParticleSystem(anchors[i]);
        }

        recreatePrefabInstances = false;
    }

    private void RegisterParticleSystem(Transform anchor)
    {
        ParticleSystem particleSystem = usingPrefabInstances
            ? EnsurePrefabParticleSystem(anchor)
            : EnsureFallbackParticleSystem(anchor);

        if (particleSystem == null)
        {
            return;
        }

        if (usingPrefabInstances)
        {
            CachePrefabParticleSystems(particleSystem);
            return;
        }

        ConfigureFallbackParticleSystem(particleSystem);
        engineParticles.Add(particleSystem);
    }

    private ParticleSystem EnsurePrefabParticleSystem(Transform anchor)
    {
        RemoveGeneratedChild(anchor, FallbackChildName);
        if (recreatePrefabInstances)
        {
            RemoveGeneratedChild(anchor, PrefabInstanceChildName);
        }

        Transform instance = anchor.Find(PrefabInstanceChildName);
        if (instance == null)
        {
            GameObject created = Instantiate(engineVfxPrefab, anchor);
            created.name = PrefabInstanceChildName;
            instance = created.transform;
        }

        instance.localPosition = Vector3.zero;
        instance.localRotation = Quaternion.identity;
        instance.localScale = Vector3.one * engineVfxScale;

        ParticleSystem particleSystem = instance.GetComponentInChildren<ParticleSystem>(true);
        if (particleSystem == null)
        {
            Debug.LogWarning(
                "ShipThrusterEffect: в назначенном Engine VFX prefab нет ParticleSystem. Эффект двигателя не будет виден на '" + name + "'.",
                this);
        }

        return particleSystem;
    }

    private ParticleSystem EnsureFallbackParticleSystem(Transform anchor)
    {
        RemoveGeneratedChild(anchor, PrefabInstanceChildName);

        Transform fallback = anchor.Find(FallbackChildName);
        if (fallback == null)
        {
            GameObject particleObject = new GameObject(FallbackChildName);
            particleObject.transform.SetParent(anchor, false);
            particleObject.transform.localPosition = Vector3.zero;
            particleObject.transform.localRotation = Quaternion.identity;
            particleObject.transform.localScale = Vector3.one;
            fallback = particleObject.transform;
        }

        ParticleSystem particleSystem = fallback.GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            particleSystem = fallback.gameObject.AddComponent<ParticleSystem>();
        }

        return particleSystem;
    }

    private void CachePrefabParticleSystems(ParticleSystem rootParticleSystem)
    {
        Transform instanceRoot = rootParticleSystem.transform;
        while (instanceRoot.parent != null && !instanceRoot.parent.name.StartsWith("Engine_Fire"))
        {
            instanceRoot = instanceRoot.parent;
        }

        particleBuffer.Clear();
        instanceRoot.GetComponentsInChildren(true, particleBuffer);
        if (particleBuffer.Count == 0)
        {
            engineParticles.Add(rootParticleSystem);
            ApplyPrefabRendererSettings(rootParticleSystem);
            return;
        }

        for (int i = 0; i < particleBuffer.Count; i++)
        {
            ParticleSystem particleSystem = particleBuffer[i];
            if (particleSystem == null)
            {
                continue;
            }

            ApplyPrefabRendererSettings(particleSystem);
            engineParticles.Add(particleSystem);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = idleEmissionRate;
            if (!particleSystem.isPlaying)
            {
                particleSystem.Play();
            }
        }
    }

    private void ApplyPrefabRendererSettings(ParticleSystem particleSystem)
    {
        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
        }
    }

    private void ConfigureFallbackParticleSystem(ParticleSystem particleSystem)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 32;
        main.startLifetime = particleLifetime;
        main.startSize = particleStartSize;
        main.startSpeed = 0.32f;
        main.startColor = engineColor;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = idleEmissionRate;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.015f;
        shape.rotation = new Vector3(180f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = false;

        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = particleSystem.limitVelocityOverLifetime;
        limitVelocity.enabled = false;

        ParticleSystem.CollisionModule collision = particleSystem.collision;
        collision.enabled = false;

        ParticleSystem.LightsModule lights = particleSystem.lights;
        lights.enabled = false;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = sortingOrder;
        renderer.maxParticleSize = 0.25f;

        if (!particleSystem.isPlaying)
        {
            particleSystem.Play();
        }
    }

    private static void RemoveGeneratedChild(Transform anchor, string childName)
    {
        Transform child = anchor != null ? anchor.Find(childName) : null;
        if (child == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(child.gameObject);
        }
        else
        {
            DestroyImmediate(child.gameObject);
        }
    }

    private Transform[] FindEngineAnchors()
    {
        Transform thrusterRoot = FindChildRecursive(transform, ThrusterRootName);
        if (thrusterRoot == null)
        {
            return new Transform[0];
        }

        List<Transform> anchors = new List<Transform>();
        for (int i = 0; i < thrusterRoot.childCount; i++)
        {
            Transform child = thrusterRoot.GetChild(i);
            if (child != null && child.name.StartsWith("Engine_Fire"))
            {
                anchors.Add(child);
            }
        }

        return anchors.ToArray();
    }

    private void OnValidate()
    {
        idleEmissionRate = Mathf.Max(0f, idleEmissionRate);
        movingEmissionRate = Mathf.Max(0f, movingEmissionRate);
        afterburnerEmissionMultiplier = Mathf.Max(1f, afterburnerEmissionMultiplier);
        emissionLerpSpeed = Mathf.Max(0.1f, emissionLerpSpeed);
        particleStartSize = Mathf.Max(0.01f, particleStartSize);
        particleLifetime = Mathf.Max(0.05f, particleLifetime);
        engineVfxScale = Mathf.Max(0.01f, engineVfxScale);
    }

#if UNITY_EDITOR
    [ContextMenu("Назначить стандартный Engine VFX")]
    private void AssignDefaultEngineVfx()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultEngineVfxPath);
        if (prefab == null)
        {
            Debug.LogWarning(
                "Стандартный Engine VFX prefab не найден. Создайте его через Tools -> Roguelike -> VFX -> Create Engine VFX Prefab.",
                this);
            return;
        }

        engineVfxPrefab = prefab;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("ShipThrusterEffect: стандартный Engine VFX prefab назначен.", this);
    }
#endif

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
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
