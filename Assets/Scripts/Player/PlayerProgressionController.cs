using System;
using System.Collections.Generic;
using SpaceFrontier.Player;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerProgressionController : MonoBehaviour
{
    [Header("Прогресс игрока")]
    [Tooltip("Если включено, сейчас ожидается выбор улучшения и gameplay должен быть поставлен на паузу.")]
    [SerializeField] private bool levelUpPending;

    private readonly List<PerkChoice> activePerks = new List<PerkChoice>();

    private PlayerShip player;
    private PerkSelectionPresenter perkSelectionPresenter;
    private Func<string, string> localize;
    private Action<string, string> logMessage;

    public bool LevelUpPending => levelUpPending;

    public void Initialize(
        PerkSelectionPresenter presenter,
        Func<string, string> localizeCallback,
        Action<string, string> logMessageCallback)
    {
        perkSelectionPresenter = presenter;
        localize = localizeCallback;
        logMessage = logMessageCallback;

        if (perkSelectionPresenter != null)
        {
            perkSelectionPresenter.OnPerkSelected = ApplyPerk;
        }
    }

    public void SetPlayer(PlayerShip newPlayer)
    {
        player = newPlayer;
    }

    public void HandleLevelUpRequested()
    {
        BeginLevelUp();
    }

    public void AddExternalExperience(int amount)
    {
        if (player == null || amount <= 0)
        {
            return;
        }

        player.AddExperience(amount);
        while (player.Experience >= player.ExperienceToNext && player.ExperienceToNext > 0)
        {
            BeginLevelUp();
            if (levelUpPending)
            {
                break;
            }
        }
    }

    public void TickPerkSelectionInput()
    {
        perkSelectionPresenter?.TickInput();
    }

    public void ResetRunState()
    {
        levelUpPending = false;
        activePerks.Clear();
        perkSelectionPresenter?.Hide();
    }

    private void BeginLevelUp()
    {
        if (player == null)
        {
            return;
        }

        player.Level++;
        player.Experience -= player.ExperienceToNext;
        player.ExperienceToNext = Mathf.RoundToInt(player.ExperienceToNext * 1.5f);

        player.MaxShield += 50f;
        player.MaxArmor += 40f;
        player.MaxHull += 30f;
        player.Shield = player.MaxShield;
        player.Armor = player.MaxArmor;
        player.Hull = player.MaxHull;

        levelUpPending = true;
        activePerks.Clear();

        List<PerkChoice> pool = new List<PerkChoice>
        {
            new PerkChoice { Label = Localize("perk_damage"), Apply = () => player.DamageMultiplier += 0.15f },
            new PerkChoice
            {
                Label = Localize("perk_capacitor"),
                Apply = () =>
                {
                    player.MaxCapacitor = Mathf.Round(player.MaxCapacitor * 1.2f);
                    player.Capacitor = player.MaxCapacitor;
                }
            },
            new PerkChoice
            {
                Label = Localize("perk_shield"),
                Apply = () =>
                {
                    player.MaxShield = Mathf.Round(player.MaxShield * 1.25f);
                    player.Shield = player.MaxShield;
                }
            },
            new PerkChoice { Label = Localize("perk_speed"), Apply = () => player.Speed += 1.1f },
            new PerkChoice { Label = Localize("perk_repair"), Apply = () => player.RepairMultiplier += 0.3f }
        };

        while (activePerks.Count < 3 && pool.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            activePerks.Add(pool[index]);
            pool.RemoveAt(index);
        }

        perkSelectionPresenter?.Show(activePerks);
        logMessage?.Invoke(Localize("log_levelup"), "warning");
    }

    private void ApplyPerk(int index)
    {
        if (index < 0 || index >= activePerks.Count)
        {
            return;
        }

        activePerks[index].Apply?.Invoke();
        levelUpPending = false;
        perkSelectionPresenter?.Hide();
        logMessage?.Invoke(Localize("log_perk_selected") + activePerks[index].Label, "warning");
        activePerks.Clear();
    }

    private string Localize(string key)
    {
        return localize != null ? localize(key) : key;
    }
}
