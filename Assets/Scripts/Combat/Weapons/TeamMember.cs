using UnityEngine;

public sealed class TeamMember : MonoBehaviour
{
    [Tooltip("Inspector: faction")]
    [SerializeField] private CombatFaction faction = CombatFaction.Neutral;

    public CombatFaction Faction => faction;

    public void SetFaction(CombatFaction value)
    {
        faction = value;
    }
}
