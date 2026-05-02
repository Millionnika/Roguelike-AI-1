using System;
using SpaceFrontier.Player;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunRewardController : MonoBehaviour
{
    [Header("Контроллер наград")]
    [Tooltip("Менеджер забега, через который применяется Scrap-награда.")]
    [SerializeField] private RunManager runManager;

    private Func<PlayerShip> playerGetter;
    private Action<string, string> logMessage;

    public void Initialize(RunManager manager, Func<PlayerShip> getPlayer, Action<string, string> logCallback)
    {
        runManager = manager;
        playerGetter = getPlayer;
        logMessage = logCallback;
    }

    public bool ApplyReward(RewardSO reward)
    {
        if (reward == null)
        {
            return false;
        }

        PlayerShip player = playerGetter != null ? playerGetter() : null;
        float amount = reward.amount;

        switch (reward.rewardType)
        {
            case RewardType.Scrap:
                runManager?.AddScrap(Mathf.RoundToInt(Mathf.Max(0f, amount)));
                Log("Получен Scrap: +" + Mathf.RoundToInt(Mathf.Max(0f, amount)), "warning");
                return true;

            case RewardType.HullRepairPercent:
                if (player == null)
                {
                    return false;
                }
                ApplyHullPercent(player, amount);
                Log("Награда применена: ремонт корпуса +" + Mathf.RoundToInt(amount) + "%.", "warning");
                return true;

            case RewardType.ShieldRestorePercent:
                if (player == null)
                {
                    return false;
                }
                ApplyShieldPercent(player, amount);
                Log("Награда применена: восстановление щита +" + Mathf.RoundToInt(amount) + "%.", "warning");
                return true;

            case RewardType.DamageMultiplierPercent:
                if (player == null)
                {
                    return false;
                }
                player.DamageMultiplier += Mathf.Max(0f, amount) / 100f;
                Log("Награда применена: урон +" + Mathf.RoundToInt(amount) + "%.", "warning");
                return true;

            case RewardType.FireRatePercent:
                Log("Награда 'Скорострельность' пока пропущена: в текущем MVP нет общего стат-параметра fire rate.", "warning");
                return false;

            case RewardType.CapacitorMaxPercent:
                if (player == null)
                {
                    return false;
                }
                ApplyCapacitorPercent(player, amount);
                Log("Награда применена: ёмкость конденсатора +" + Mathf.RoundToInt(amount) + "%.", "warning");
                return true;

            default:
                return false;
        }
    }

    private static void ApplyHullPercent(PlayerShip player, float percent)
    {
        float clamped = Mathf.Max(0f, percent) / 100f;
        float heal = player.MaxHull * clamped;
        player.Hull = Mathf.Min(player.MaxHull, player.Hull + heal);
    }

    private static void ApplyShieldPercent(PlayerShip player, float percent)
    {
        float clamped = Mathf.Max(0f, percent) / 100f;
        float restore = player.MaxShield * clamped;
        player.Shield = Mathf.Min(player.MaxShield, player.Shield + restore);
    }

    private static void ApplyCapacitorPercent(PlayerShip player, float percent)
    {
        float factor = 1f + Mathf.Max(0f, percent) / 100f;
        player.MaxCapacitor = Mathf.Max(1f, player.MaxCapacitor * factor);
        player.Capacitor = player.MaxCapacitor;
    }

    private void Log(string message, string kind)
    {
        logMessage?.Invoke(message, kind);
    }
}
