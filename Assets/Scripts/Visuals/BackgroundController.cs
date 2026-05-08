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
    private GameObject runtimeAsteroidLayerPrefab;
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

        if (runtimeAsteroidLayerPrefab != null)
        {
            Destroy(runtimeAsteroidLayerPrefab);
            runtimeAsteroidLayerPrefab = null;
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
            for (int i = 0; i < backgroundLayers.Count; i++)
            {
                BackgroundLayerConfig layer = backgroundLayers[i];
                if (layer != null && layer.prefab != null)
                {
                    effectiveLayers.Add(layer);
                }
            }

            if (effectiveLayers.Count > 0)
            {
                return;
            }
        }

        if (fallbackLayers != null && fallbackLayers.Count > 0)
        {
            for (int i = 0; i < fallbackLayers.Count; i++)
            {
                if (fallbackLayers[i] != null && fallbackLayers[i].prefab != null)
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
        effectiveLayers.Add(new BackgroundLayerConfig
        {
            prefab = GetRuntimeAsteroidLayerPrefab(),
            parallaxFactor = 0.32f,
            tileSize = 44f,
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

        CreateLayerSprite(runtimeStarLayerPrefab.transform, "StarA", new Vector3(-9f, 7f, 0f), 0.08f, new Color(0.75f, 0.88f, 1f, 0.85f), -20);
        CreateLayerSprite(runtimeStarLayerPrefab.transform, "StarB", new Vector3(12f, 5f, 0f), 0.06f, new Color(0.95f, 0.95f, 1f, 0.8f), -20);
        CreateLayerSprite(runtimeStarLayerPrefab.transform, "StarC", new Vector3(-4f, -11f, 0f), 0.07f, new Color(0.7f, 0.82f, 1f, 0.82f), -20);
        CreateLayerSprite(runtimeStarLayerPrefab.transform, "StarD", new Vector3(8f, -6f, 0f), 0.05f, new Color(1f, 0.98f, 0.86f, 0.7f), -20);
        CreateLayerSprite(runtimeStarLayerPrefab.transform, "StarE", new Vector3(-15f, -2f, 0f), 0.04f, new Color(0.8f, 0.9f, 1f, 0.62f), -20);

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

    private GameObject GetRuntimeAsteroidLayerPrefab()
    {
        if (runtimeAsteroidLayerPrefab != null)
        {
            return runtimeAsteroidLayerPrefab;
        }

        runtimeAsteroidLayerPrefab = new GameObject("RuntimeAsteroidLayerPrefab");
        runtimeAsteroidLayerPrefab.SetActive(false);
        runtimeAsteroidLayerPrefab.hideFlags = HideFlags.DontSave;

        CreateLayerSprite(runtimeAsteroidLayerPrefab.transform, "AsteroidA", new Vector3(-11f, 8f, 0f), 0.42f, new Color(0.38f, 0.42f, 0.48f, 0.42f), -12);
        CreateLayerSprite(runtimeAsteroidLayerPrefab.transform, "AsteroidB", new Vector3(10f, -5f, 0f), 0.3f, new Color(0.42f, 0.44f, 0.5f, 0.35f), -12);
        CreateLayerSprite(runtimeAsteroidLayerPrefab.transform, "AsteroidC", new Vector3(2f, 12f, 0f), 0.24f, new Color(0.34f, 0.39f, 0.44f, 0.3f), -12);

        return runtimeAsteroidLayerPrefab;
    }

    private void CreateLayerSprite(Transform parent, string objectName, Vector3 localPosition, float uniformScale, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = fallbackCircleSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
    }
}
