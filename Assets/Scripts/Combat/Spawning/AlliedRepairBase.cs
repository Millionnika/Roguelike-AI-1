using SpaceFrontier.Player;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AlliedRepairBase : MonoBehaviour
{
    [Header("Repair")]
    [Tooltip("Доля от максимального запаса (щит/броня/корпус), восстанавливаемая за один тик.")]
    [Range(0f, 1f)] [SerializeField] private float healStrength = 0.2f;
    [Tooltip("Перезарядка между тиками лечения в секундах.")]
    [Min(0.1f)] [SerializeField] private float healCooldownSeconds = 30f;
    [Tooltip("Радиус действия лечения.")]
    [Min(0.5f)] [SerializeField] private float healRadius = 3.2f;
    [Header("Defense")]
    [Tooltip("Оружие союзной базы. Если не задано, база не будет атаковать.")]
    [SerializeField] private WeaponDataSO defenseWeaponData;
    [Header("Visual")]
    [Tooltip("Медленное вращение станции вокруг своей оси (без перемещения).")]
    [SerializeField] private bool idleSpinEnabled = true;
    [Tooltip("Скорость вращения в градусах/сек.")]
    [SerializeField, Range(-30f, 30f)] private float idleSpinSpeed = 3f;

    private SpaceCombatSceneController sceneController;
    private float nextHealTime;

    private void Awake()
    {
        sceneController = FindFirstObjectByType<SpaceCombatSceneController>();
        EnsureTeamMember();
        EnsureDefenseBattery();
        EnsureTrigger();
    }

    private void Update()
    {
        if (idleSpinEnabled)
        {
            float spin = idleSpinSpeed * Time.deltaTime;
            if (Mathf.Abs(spin) > 0.0001f)
            {
                transform.Rotate(0f, 0f, spin, Space.Self);
            }
        }

        if (Time.time < nextHealTime || sceneController == null)
        {
            return;
        }

        PlayerShip player = sceneController.GetPlayerShipForExternalSystems();
        if (player == null || player.Transform == null)
        {
            return;
        }

        float sqrRange = healRadius * healRadius;
        if ((player.Transform.position - transform.position).sqrMagnitude > sqrRange)
        {
            return;
        }

        ApplyHeal(player);
        nextHealTime = Time.time + Mathf.Max(0.1f, healCooldownSeconds);
    }

    private void ApplyHeal(PlayerShip player)
    {
        float strength = Mathf.Clamp01(healStrength);
        if (strength <= 0f)
        {
            return;
        }

        player.HealShield(player.MaxShield * strength);
        player.HealArmor(player.MaxArmor * strength);
        player.Hull = Mathf.Min(player.MaxHull, player.Hull + player.MaxHull * strength);
    }

    public void ConfigureHealing(float strength, float cooldownSeconds, float radius)
    {
        healStrength = Mathf.Clamp01(strength);
        healCooldownSeconds = Mathf.Max(0.1f, cooldownSeconds);
        healRadius = Mathf.Max(0.5f, radius);
        EnsureTrigger();
    }

    private void EnsureTrigger()
    {
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<CircleCollider2D>();
        }

        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.5f, healRadius);
    }

    private void EnsureTeamMember()
    {
        TeamMember member = GetComponent<TeamMember>();
        if (member == null)
        {
            member = gameObject.AddComponent<TeamMember>();
        }

        member.SetFaction(CombatFaction.Player);
        CombatLayerUtility.ApplyShipLayer(gameObject, CombatFaction.Player);
    }

    private void EnsureDefenseBattery()
    {
        BaseDefenseBattery battery = GetComponent<BaseDefenseBattery>();
        if (battery == null)
        {
            battery = gameObject.AddComponent<BaseDefenseBattery>();
        }

        battery.ConfigureFaction(CombatFaction.Player);
        battery.EnsureWeapon(defenseWeaponData);
    }

    private void OnValidate()
    {
        healStrength = Mathf.Clamp01(healStrength);
        healCooldownSeconds = Mathf.Max(0.1f, healCooldownSeconds);
        healRadius = Mathf.Max(0.5f, healRadius);

        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.radius = healRadius;
        }
    }
}
