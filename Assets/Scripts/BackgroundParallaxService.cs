using System.Collections.Generic;
using UnityEngine;

internal sealed class BackgroundParallaxService : IBackgroundParallaxService
{
    private sealed class LayerState
    {
        public BackgroundLayerConfig Config;
        public readonly List<GameObject> Tiles = new List<GameObject>();
    }

    private readonly List<LayerState> layers = new List<LayerState>();
    private IPoolService poolService;

    public void Initialize(Transform parent, List<BackgroundLayerConfig> layerConfigs, IPoolService pool)
    {
        Dispose();
        poolService = pool;

        if (parent == null || poolService == null || layerConfigs == null)
        {
            return;
        }

        for (int i = 0; i < layerConfigs.Count; i++)
        {
            BackgroundLayerConfig config = layerConfigs[i];
            if (config == null || config.prefab == null)
            {
                continue;
            }

            int radius = Mathf.Clamp(config.gridRadius, 1, 3);
            int tileCount = (radius * 2 + 1) * (radius * 2 + 1);
            poolService.InitializePool(config.prefab, tileCount);

            LayerState layer = new LayerState
            {
                Config = config
            };

            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                GameObject tile = poolService.Get(config.prefab, parent);
                if (tile == null)
                {
                    continue;
                }

                layer.Tiles.Add(tile);
            }

            if (layer.Tiles.Count > 0)
            {
                layers.Add(layer);
            }
        }

        Update(Vector3.zero);
    }

    public void Update(Vector3 focusPosition)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            LayerState layer = layers[i];
            BackgroundLayerConfig config = layer.Config;
            float tileSize = Mathf.Max(8f, config.tileSize);
            int radius = Mathf.Clamp(config.gridRadius, 1, 3);

            Vector3 parallaxCenter = new Vector3(
                focusPosition.x * config.parallaxFactor,
                focusPosition.y * config.parallaxFactor,
                0f);

            int gridCenterX = Mathf.FloorToInt(parallaxCenter.x / tileSize);
            int gridCenterY = Mathf.FloorToInt(parallaxCenter.y / tileSize);

            int cursor = 0;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (cursor >= layer.Tiles.Count)
                    {
                        break;
                    }

                    GameObject tile = layer.Tiles[cursor++];
                    if (tile == null)
                    {
                        continue;
                    }

                    Vector3 worldPosition = new Vector3(
                        (gridCenterX + x) * tileSize,
                        (gridCenterY + y) * tileSize,
                        0f);
                    tile.transform.position = worldPosition;
                }
            }
        }
    }

    public void Dispose()
    {
        if (poolService == null)
        {
            layers.Clear();
            return;
        }

        for (int i = 0; i < layers.Count; i++)
        {
            LayerState layer = layers[i];
            if (layer == null || layer.Config == null || layer.Config.prefab == null)
            {
                continue;
            }

            for (int j = 0; j < layer.Tiles.Count; j++)
            {
                GameObject tile = layer.Tiles[j];
                if (tile != null)
                {
                    poolService.Return(layer.Config.prefab, tile);
                }
            }
        }

        layers.Clear();
    }
}
