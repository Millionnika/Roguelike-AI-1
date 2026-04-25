using UnityEngine;

namespace SpaceFrontier.Player
{
    public sealed class PlayerStats
    {
        public float MaxShield = 400f;
        public float Shield = 400f;
        public float MaxArmor = 300f;
        public float Armor = 300f;
        public float MaxHull = 200f;
        public float Hull = 200f;

        public float MaxCapacitor = 1000f;
        public float Capacitor = 1000f;
        public float CapacitorRechargeTime = 120f;

        public int Level = 1;
        public int Experience;
        public int ExperienceToNext = 100;

        public float DamageMultiplier = 1f;
        public float SpeedMultiplier = 1f;
        public float RepairMultiplier = 1f;

        public float CapacitorPercent => MaxCapacitor <= 0f ? 0f : Capacitor / MaxCapacitor;
        public float ShieldPercent => MaxShield <= 0f ? 0f : Shield / MaxShield;
        public float ArmorPercent => MaxArmor <= 0f ? 0f : Armor / MaxArmor;
        public float HullPercent => MaxHull <= 0f ? 0f : Hull / MaxHull;

        public void UpdateCapacitor(float deltaTime)
        {
            if (Capacitor >= MaxCapacitor)
            {
                return;
            }

            float percent = Capacitor / MaxCapacitor;
            float rechargeCurve = Mathf.Max(0.25f, 3.2f * percent * (1f - percent));
            float maxRechargePerSecond = (MaxCapacitor / CapacitorRechargeTime) * 2.55f;
            Capacitor = Mathf.Min(MaxCapacitor, Capacitor + maxRechargePerSecond * rechargeCurve * deltaTime);
        }

        public bool ConsumeCapacitor(float amount)
        {
            if (Capacitor < amount)
            {
                return false;
            }

            Capacitor -= amount;
            return true;
        }

        public void HealShield(float amount)
        {
            Shield = Mathf.Min(MaxShield, Shield + amount);
        }

        public void HealArmor(float amount)
        {
            Armor = Mathf.Min(MaxArmor, Armor + amount);
        }

        public bool IsAlive()
        {
            return Hull > 0f;
        }

        public void AddExperience(int amount)
        {
            Experience += amount;
        }
    }
}
