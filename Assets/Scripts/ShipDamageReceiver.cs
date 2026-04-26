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
        DamageResolutionResult result = DamageService.ResolveDamage(current, info.Amount);
        writeState(result.State);

        if (result.Destroyed)
        {
            isDestroyed = true;
            onDestroyed?.Invoke();
        }
    }
}
