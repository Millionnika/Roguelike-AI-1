using System.Collections.Generic;
using UnityEngine;

public static class CombatLayerUtility
{
    public const string PlayerShipLayerName = "PlayerShip";
    public const string EnemyShipLayerName = "EnemyShip";
    public const string PlayerProjectileLayerName = "PlayerProjectile";
    public const string EnemyProjectileLayerName = "EnemyProjectile";

    private static readonly HashSet<string> MissingLayerErrors = new HashSet<string>();

    public static void ApplyShipLayer(GameObject root, CombatFaction faction)
    {
        if (root == null)
        {
            return;
        }

        string layerName = faction switch
        {
            CombatFaction.Player => PlayerShipLayerName,
            CombatFaction.Enemy => EnemyShipLayerName,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(layerName))
        {
            return;
        }

        int layerId = ResolveLayerId(layerName);
        if (layerId < 0)
        {
            return;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            collider.gameObject.layer = layerId;
        }
    }

    public static void ApplyProjectileLayer(GameObject projectile, CombatFaction ownerFaction)
    {
        if (projectile == null)
        {
            return;
        }

        string layerName = ownerFaction switch
        {
            CombatFaction.Player => PlayerProjectileLayerName,
            CombatFaction.Enemy => EnemyProjectileLayerName,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(layerName))
        {
            return;
        }

        int layerId = ResolveLayerId(layerName);
        if (layerId < 0)
        {
            return;
        }

        projectile.layer = layerId;

        Collider2D[] colliders = projectile.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            collider.gameObject.layer = layerId;
        }
    }

    private static int ResolveLayerId(string layerName)
    {
        int layerId = LayerMask.NameToLayer(layerName);
        if (layerId >= 0)
        {
            return layerId;
        }

        if (MissingLayerErrors.Add(layerName))
        {
            Debug.LogError("CombatLayerUtility: Missing Unity layer '" + layerName + "'. Add it in Project Settings > Tags and Layers.");
        }

        return -1;
    }
}
