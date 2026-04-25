using UnityEngine;

namespace SpaceFrontier.Player
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Health & Protection")]
        public float maxShield = 400f;
        public float shield = 400f;
        public float maxArmor = 300f;
        public float armor = 300f;
        public float maxHull = 200f;
        public float hull = 200f;

        [Header("Energy System")]
        public float maxCapacitor = 1000f;
        public float capacitor = 1000f;
        public float capacitorRechargeTime = 120f;

        [Header("Progression")]
        public int level = 1;
        public int experience;
        public int experienceToNext = 100;

        [Header("Multipliers")]
        public float damageMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float repairMultiplier = 1f;

        public float CapacitorPercent => maxCapacitor <= 0f ? 0f : capacitor / maxCapacitor;
        public float ShieldPercent => maxShield <= 0f ? 0f : shield / maxShield;
        public float ArmorPercent => maxArmor <= 0f ? 0f : armor / maxArmor;
        public float HullPercent => maxHull <= 0f ? 0f : hull / maxHull;

        public void UpdateCapacitor(float deltaTime)
        {
            if (capacitor >= maxCapacitor) return;

            float percent = capacitor / maxCapacitor;
            float rechargeCurve = Mathf.Max(0.25f, 3.2f * percent * (1f - percent));
            float maxRechargePerSecond = (maxCapacitor / capacitorRechargeTime) * 2.55f;
            capacitor = Mathf.Min(maxCapacitor, capacitor + maxRechargePerSecond * rechargeCurve * deltaTime);
        }

        public bool ConsumeCapacitor(float amount)
        {
            if (capacitor < amount) return false;
            capacitor -= amount;
            return true;
        }

        public void ApplyDamage(float amount)
        {
            float remaining = amount;

            if (shield > 0f)
            {
                float absorbed = Mathf.Min(shield, remaining);
                shield -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0f && armor > 0f)
            {
                float absorbed = Mathf.Min(armor, remaining);
                armor -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0f)
            {
                hull = Mathf.Max(0f, hull - remaining);
            }
        }

        public void HealShield(float amount)
        {
            shield = Mathf.Min(maxShield, shield + amount);
        }

        public void HealArmor(float amount)
        {
            armor = Mathf.Min(maxArmor, armor + amount);
        }

        public bool IsAlive() => hull > 0f;

        public void AddExperience(int amount)
        {
            experience += amount;
            if (experience >= experienceToNext)
            {
                // Level up logic should be handled by GameManager/EventManager
            }
        }
    }
}