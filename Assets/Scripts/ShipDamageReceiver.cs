using System;
using UnityEngine;

public sealed class ShipDamageReceiver : MonoBehaviour, IDamageable
{
    private CombatFaction faction = CombatFaction.Neutral;
    private Func<ShipDurabilityState> readState;
    private Action<ShipDurabilityState> writeState;
    private Action onDestroyed;
    private bool isDestroyed;

    public CombatFaction Faction => faction;
    public event Action<DamageInfo, DamageResolutionResult> DamageApplied;

    public void Initialize(
        CombatFaction ownerFaction,
        Func<ShipDurabilityState> stateReader,
        Action<ShipDurabilityState> stateWriter,
        Action destroyedCallback = null)
    {
        faction = ownerFaction;
        readState = stateReader;
        writeState = stateWriter;
        onDestroyed = destroyedCallback;
        isDestroyed = false;
        DamageApplied = null;

        TeamMember teamMember = GetComponent<TeamMember>();
        if (teamMember == null)
        {
            teamMember = gameObject.AddComponent<TeamMember>();
        }

        teamMember.SetFaction(ownerFaction);
    }

    public void TakeDamage(DamageInfo info)
    {
        if (isDestroyed || readState == null || writeState == null)
        {
            return;
        }

        if (info.Source != null && info.Source == gameObject)
        {
            return;
        }

        if (info.SourceFaction != CombatFaction.Neutral && info.SourceFaction == faction)
        {
            return;
        }

        ShipDurabilityState current = readState();
        DamageResolutionResult result = DamageService.ResolveDamage(current, info);
        writeState(result.State);
        string weaponName = info.WeaponData != null ? info.WeaponData.name : "Fallback";
        Debug.Log(
            "[WeaponDebug] Damage resolved: weapon=" + weaponName +
            " total=" + info.Amount.ToString("0.##") +
            " shield=" + result.AppliedShieldDamage.ToString("0.##") +
            " armor=" + result.AppliedArmorDamage.ToString("0.##") +
            " hull=" + result.AppliedHullDamage.ToString("0.##"));
        DamageApplied?.Invoke(info, result);

        if (result.Destroyed)
        {
            isDestroyed = true;
            onDestroyed?.Invoke();
        }
    }
}
