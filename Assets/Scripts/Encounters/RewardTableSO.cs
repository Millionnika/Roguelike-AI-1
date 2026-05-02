using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Reward Table", fileName = "RewardTable")]
public sealed class RewardTableSO : ScriptableObject
{
    [Header("Таблица наград")]
    [Tooltip("Список всех возможных наград для этой таблицы.")]
    [SerializeField] private List<RewardSO> rewards = new List<RewardSO>();
    [Tooltip("Сколько вариантов наград показывать игроку после завершения локации.")]
    [SerializeField, Min(1)] private int choicesCount = 3;

    public IReadOnlyList<RewardSO> Rewards => rewards;
    public int ChoicesCount => choicesCount;

    public List<RewardSO> GenerateChoices()
    {
        List<RewardSO> result = new List<RewardSO>();
        if (rewards == null || rewards.Count == 0)
        {
            return result;
        }

        List<RewardSO> pool = new List<RewardSO>();
        for (int i = 0; i < rewards.Count; i++)
        {
            RewardSO reward = rewards[i];
            if (reward != null)
            {
                pool.Add(reward);
            }
        }

        int targetCount = Mathf.Clamp(choicesCount, 1, 3);
        while (result.Count < targetCount && pool.Count > 0)
        {
            RewardSO selected = PickWeighted(pool);
            if (selected == null)
            {
                break;
            }

            result.Add(selected);
            pool.Remove(selected);
        }

        return result;
    }

    private static RewardSO PickWeighted(List<RewardSO> pool)
    {
        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            RewardSO reward = pool[i];
            if (reward != null)
            {
                totalWeight += Mathf.Max(0f, reward.weight);
            }
        }

        if (totalWeight <= 0f)
        {
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        float roll = Random.value * totalWeight;
        float accum = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            RewardSO reward = pool[i];
            if (reward == null)
            {
                continue;
            }

            accum += Mathf.Max(0f, reward.weight);
            if (roll <= accum)
            {
                return reward;
            }
        }

        return pool[pool.Count - 1];
    }

    private void OnValidate()
    {
        choicesCount = Mathf.Max(1, choicesCount);
        rewards ??= new List<RewardSO>();
    }
}
