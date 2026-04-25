using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class PoolService : IPoolService
{
    private sealed class Pool
    {
        public GameObject Prefab;
        public Transform Root;
        public readonly Stack<GameObject> Inactive = new Stack<GameObject>();
    }

    private readonly Dictionary<int, Pool> pools = new Dictionary<int, Pool>();
    private Transform globalRoot;

    public void InitializePool(GameObject prefab, int initialCount)
    {
        if (prefab == null)
        {
            Debug.LogError("PoolService.InitializePool called with null prefab.");
            return;
        }

        Pool pool = GetOrCreatePool(prefab);
        for (int i = 0; i < initialCount; i++)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, pool.Root, false);
            instance.SetActive(false);
            pool.Inactive.Push(instance);
        }
    }

    public GameObject Get(GameObject prefab, Transform parent)
    {
        if (prefab == null)
        {
            Debug.LogError("PoolService.Get called with null prefab.");
            return null;
        }

        Pool pool = GetOrCreatePool(prefab);

        while (pool.Inactive.Count > 0)
        {
            GameObject instance = pool.Inactive.Pop();
            if (instance == null)
            {
                continue;
            }

            instance.transform.SetParent(parent, false);
            instance.SetActive(true);
            return instance;
        }

        return UnityEngine.Object.Instantiate(prefab, parent, false);
    }

    public void Return(GameObject prefab, GameObject instance)
    {
        if (prefab == null)
        {
            Debug.LogError("PoolService.Return called with null prefab.");
            return;
        }

        if (instance == null)
        {
            return;
        }

        Pool pool = GetOrCreatePool(prefab);
        instance.SetActive(false);
        instance.transform.SetParent(pool.Root, false);
        pool.Inactive.Push(instance);
    }

    private Pool GetOrCreatePool(GameObject prefab)
    {
        int key = prefab.GetInstanceID();
        if (pools.TryGetValue(key, out Pool pool))
        {
            return pool;
        }

        if (globalRoot == null)
        {
            GameObject rootObject = new GameObject("PoolService");
            rootObject.hideFlags = HideFlags.DontSave;
            globalRoot = rootObject.transform;
        }

        GameObject poolRootObject = new GameObject("Pool_" + prefab.name);
        poolRootObject.hideFlags = HideFlags.DontSave;
        poolRootObject.transform.SetParent(globalRoot, false);

        pool = new Pool
        {
            Prefab = prefab,
            Root = poolRootObject.transform
        };
        pools[key] = pool;
        return pool;
    }
}
