using System.Collections.Generic;
using SpaceFrontier.Player;
using UnityEngine;

public sealed class BackgroundController : MonoBehaviour
{
    [Header("Фон и параллакс")]
    [Tooltip("Слои бесконечного фона. Если список пустой, компонент использует старые настройки из SpaceCombatSceneController или создаст простой runtime-fallback из звезд и туманности.")]
    [SerializeField] private List<BackgroundLayerConfig> backgroundLayers = new List<BackgroundLayerConfig>();

    private readonly List<BackgroundLayerConfig> effectiveLayers = new List<BackgroundLayerConfig>();
    private Transform starRoot;
    private Transform playerTransform;
    private IPoolService poolService;
    private IBackgroundParallaxService backgroundParallaxService;
    private Sprite fallbackCircleSprite;
    private GameObject runtimeStarLayerPrefab;
    private GameObject runtimeNebulaLayerPrefab;
    private bool warnedMissingLayers;

    internal void Initialize(
        Transform worldRoot,
        PlayerShip player,
        IPoolService newPoolService,
        IBackgroundParallaxService newBackgroundParallaxService,
        Sprite newFallbackCircleSprite,
        IReadOnlyList<BackgroundLayerConfig> fallbackLayers)
    {
        poolService = newPoolService;
        backgroundParallaxService = newBackgroundParallaxService;
        fallbackCircleSprite = newFallbackCircleSprite;
        SetPlayer(player);

        if (worldRoot == null)
        {
            Debug.LogWarning("BackgroundController: корневой объект мира не назначен, фон не будет создан.", this);
            return;
        }

        if (starRoot == null)
        {
            starRoot = new GameObject("Stars").transform;
            starRoot.SetParent(worldRoot, false);
        }
        else if (starRoot.parent != worldRoot)
        {
            starRoot.SetParent(worldRoot, false);
        }

        BuildStarfield(fallbackLayers);
    }

    public void SetPlayer(PlayerShip player)
    {
        playerTransform = player != null ? player.Transform : null;
    }

    public void Tick()
    {
        if (playerTransform == null || backgroundParallaxService == null)
        {
            return;
        }

        backgroundParallaxService.Update(playerTransform.position);
    }

    public void Cleanup()
    {
        backgroundParallaxService?.Dispose();

        if (runtimeStarLayerPrefab != null)
        {
            Destroy(runtimeStarLayerPrefab);
            runtimeStarLayerPrefab = null;
        }

        if (runtimeNebulaLayerPrefab != null)
        {
            Destroy(runtimeNebulaLayerPrefab);
            runtimeNebulaLayerPrefab = null;
        }
    }

    private void BuildStarfield(IReadOnlyList<BackgroundLayerConfig> fallbackLayers)
    {
        EnsureBackgroundLayers(fallbackLayers);

        if (backgroundParallaxService == null)
        {
            Debug.LogWarning("BackgroundController: сервис параллакса не назначен, фон не будет обновляться.", this);
            return;
        }

        backgroundParallaxService.Dispose();
        backgroundParallaxService.Initialize(starRoot, effectiveLayers, poolService);
    }

    private void EnsureBackgroundLayers(IReadOnlyList<BackgroundLayerConfig> fallbackLayers)
    {
        effectiveLayers.Clear();

        if (backgroundLayers != null && backgroundLayers.Count > 0)
        {
            effectiveLayers.AddRange(backgroundLayers);
            return;
        }

        if (fallbackLayers != null && fallbackLayers.Count > 0)
        {
            for (int i = 0; i < fallbackLayers.Count; i++)
            {
                if (fallbackLayers[i] != null)
                {
                    effectiveLayers.Add(fallbackLayers[i]);
                }
            }

            if (effectiveLayers.Count > 0)
            {
                return;
            }
        }

        if (fallbackCircleSprite == null)
        {
            if (!warnedMissingLayers)
            {
                Debug.LogWarning("BackgroundController: слои фона не настроены, а fallback-спрайт не передан. Назначьте слой фона в инспекторе.", this);
                warnedMissingLayers = true;
            }
            return;
        }

        effectiveLayers.Add(new BackgroundLayerConfig
        {
            prefab = GetRuntimeNebulaLayerPrefab(),
            parallaxFactor = 0.08f,
            tileSize = 48f,
            gridRadius = 2
        });
        effectiveLayers.Add(new BackgroundLayerConfig
        {
            prefab = GetRuntimeStarLayerPrefab(),
            parallaxFactor = 0.18f,
            tileSize = 36f,
            gridRadius = 2
        });
    }

    private GameObject GetRuntimeStarLayerPrefab()
    {
        if (runtimeStarLayerPrefab != null)
        {
            return runtimeStarLayerPrefab;
        }

        runtimeStarLayerPrefab = new GameObject("RuntimeStarLayerPrefab");
        runtimeStarLayerPrefab.SetActive(false);
        runtimeStarLayerPrefab.hideFlags = HideFlags.DontSave;
        SpriteRenderer renderer = runtimeStarLayerPrefab.AddComponent<SpriteRenderer>();
        renderer.sprite = fallbackCircleSprite;
        renderer.color = new Color(0.7f, 0.85f, 1f, 0.85f);
        renderer.sortingOrder = -20;
        runtimeStarLayerPrefab.transform.localScale = new Vector3(0.08f, 0.08f, 1f);
        return runtimeStarLayerPrefab;
    }

    private GameObject GetRuntimeNebulaLayerPrefab()
    {
        if (runtimeNebulaLayerPrefab != null)
        {
            return runtimeNebulaLayerPrefab;
        }

        runtimeNebulaLayerPrefab = new GameObject("RuntimeNebulaLayerPrefab");
        runtimeNebulaLayerPrefab.SetActive(false);
        runtimeNebulaLayerPrefab.hideFlags = HideFlags.DontSave;
        SpriteRenderer renderer = runtimeNebulaLayerPrefab.AddComponent<SpriteRenderer>();
        renderer.sprite = fallbackCircleSprite;
        renderer.color = new Color(0.1f, 0.24f, 0.35f, 0.16f);
        renderer.sortingOrder = -30;
        runtimeNebulaLayerPrefab.transform.localScale = new Vector3(5.5f, 3.8f, 1f);
        return runtimeNebulaLayerPrefab;
    }
}
