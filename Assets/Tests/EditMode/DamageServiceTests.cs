using NUnit.Framework;

public sealed class DamageServiceTests
{
    [Test]
    public void Railgun_Profile_Preserves_Full_Damage_With_Shield_Overflow()
    {
        ShipDurabilityState state = new ShipDurabilityState
        {
            MaxShield = 10f,
            Shield = 10f,
            MaxArmor = 100f,
            Armor = 100f,
            MaxHull = 100f,
            Hull = 100f
        };

        DamageLayerShare[] railgun =
        {
            new DamageLayerShare { layer = DamageLayerType.Shield, percent = 20f },
            new DamageLayerShare { layer = DamageLayerType.Armor, percent = 70f },
            new DamageLayerShare { layer = DamageLayerType.Hull, percent = 10f }
        };

        DamageResolutionResult result = DamageService.ApplyDamage(state, 100f, railgun);

        Assert.That(result.AppliedShieldDamage, Is.EqualTo(10f).Within(0.001f));
        Assert.That(result.AppliedArmorDamage, Is.EqualTo(80f).Within(0.001f));
        Assert.That(result.AppliedHullDamage, Is.EqualTo(10f).Within(0.001f));
        Assert.That(result.AppliedShieldDamage + result.AppliedArmorDamage + result.AppliedHullDamage, Is.EqualTo(100f).Within(0.001f));
    }

    [Test]
    public void Missile_Profile_Redistributes_When_Armor_Is_Empty()
    {
        ShipDurabilityState state = new ShipDurabilityState
        {
            MaxShield = 100f,
            Shield = 100f,
            MaxArmor = 0f,
            Armor = 0f,
            MaxHull = 100f,
            Hull = 100f
        };

        DamageLayerShare[] missile =
        {
            new DamageLayerShare { layer = DamageLayerType.Shield, percent = 40f },
            new DamageLayerShare { layer = DamageLayerType.Armor, percent = 40f },
            new DamageLayerShare { layer = DamageLayerType.Hull, percent = 20f }
        };

        DamageResolutionResult result = DamageService.ApplyDamage(state, 50f, missile);

        Assert.That(result.AppliedShieldDamage, Is.EqualTo(20f).Within(0.001f));
        Assert.That(result.AppliedArmorDamage, Is.EqualTo(0f).Within(0.001f));
        Assert.That(result.AppliedHullDamage, Is.EqualTo(30f).Within(0.001f));
        Assert.That(result.AppliedShieldDamage + result.AppliedArmorDamage + result.AppliedHullDamage, Is.EqualTo(50f).Within(0.001f));
    }
}
