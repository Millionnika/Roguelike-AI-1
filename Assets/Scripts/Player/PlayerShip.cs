using UnityEngine;

namespace SpaceFrontier.Player
{
    public sealed class PlayerShip
    {
        public Transform Transform;
        public float Speed = 6.2f;
        public float Acceleration = 10f;
        public float Drag = 1.15f;
        public float RotationResponsiveness = 8f;
        public Vector2 Velocity;
        public bool MoveCommandActive;
        public Vector3 MoveCommandTarget;

        public PlayerStats Stats { get; } = new PlayerStats();

        public SpriteRenderer BodyRenderer { get; set; }
        public SpriteRenderer AuraRenderer { get; set; }
        public SpriteRenderer ThrusterRenderer { get; set; }
        public ShipThrusterEffect ThrusterEffect { get; set; }
        public ShipDamageReceiver DamageReceiver { get; set; }
        public TeamMember TeamMember { get; set; }
        public Color BaseBodyColor { get; set; } = Color.white;
        public Color BaseAuraColor { get; set; } = Color.white;
        public float HitFlashTimer { get; set; }
        public float ThrusterPulse { get; set; }

        public float DamageMultiplier
        {
            get => Stats.DamageMultiplier;
            set => Stats.DamageMultiplier = value;
        }

        public float SpeedMultiplier
        {
            get => Stats.SpeedMultiplier;
            set => Stats.SpeedMultiplier = value;
        }

        public float RepairMultiplier
        {
            get => Stats.RepairMultiplier;
            set => Stats.RepairMultiplier = value;
        }

        public float MaxShield
        {
            get => Stats.MaxShield;
            set => Stats.MaxShield = value;
        }

        public float Shield
        {
            get => Stats.Shield;
            set => Stats.Shield = value;
        }

        public float MaxArmor
        {
            get => Stats.MaxArmor;
            set => Stats.MaxArmor = value;
        }

        public float Armor
        {
            get => Stats.Armor;
            set => Stats.Armor = value;
        }

        public float MaxHull
        {
            get => Stats.MaxHull;
            set => Stats.MaxHull = value;
        }

        public float Hull
        {
            get => Stats.Hull;
            set => Stats.Hull = value;
        }

        public float MaxCapacitor
        {
            get => Stats.MaxCapacitor;
            set => Stats.MaxCapacitor = value;
        }

        public float Capacitor
        {
            get => Stats.Capacitor;
            set => Stats.Capacitor = value;
        }

        public float CapacitorRechargeTime
        {
            get => Stats.CapacitorRechargeTime;
            set => Stats.CapacitorRechargeTime = value;
        }

        public float CapacitorRechargeRate
        {
            get => Stats.CapacitorRechargeRate;
            set => Stats.CapacitorRechargeRate = value;
        }

        public int Level
        {
            get => Stats.Level;
            set => Stats.Level = value;
        }

        public int Experience
        {
            get => Stats.Experience;
            set => Stats.Experience = value;
        }

        public int ExperienceToNext
        {
            get => Stats.ExperienceToNext;
            set => Stats.ExperienceToNext = value;
        }

        public float CapacitorPercent => Stats.CapacitorPercent;
        public float ShieldPercent => Stats.ShieldPercent;
        public float ArmorPercent => Stats.ArmorPercent;
        public float HullPercent => Stats.HullPercent;

        public void SetVisuals(Color bodyColor, Color auraColor)
        {
            BaseBodyColor = bodyColor;
            BaseAuraColor = auraColor;

            if (BodyRenderer != null)
            {
                BodyRenderer.color = bodyColor;
            }

            if (AuraRenderer != null)
            {
                AuraRenderer.color = auraColor;
            }
        }

        public void UpdateCapacitor(float deltaTime)
        {
            Stats.UpdateCapacitor(deltaTime);
        }

        public bool ConsumeCapacitor(float amount)
        {
            return Stats.ConsumeCapacitor(amount);
        }

        public void HealShield(float amount)
        {
            Stats.HealShield(amount);
        }

        public void HealArmor(float amount)
        {
            Stats.HealArmor(amount);
        }

        public bool IsAlive()
        {
            return Stats.IsAlive();
        }

        public void AddExperience(int amount)
        {
            Stats.AddExperience(amount);
        }
    }
}
