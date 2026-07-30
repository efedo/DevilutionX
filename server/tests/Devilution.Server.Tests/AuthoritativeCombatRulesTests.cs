using Devilution.Server.Gameplay;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeCombatRulesTests
{
    [Fact]
    public void LoadsExternalCombatConstants()
    {
        var rules = AuthoritativeCombatRules.LoadTsv(
            "combat.tsv",
            "base_attack_damage\tminimum_damage\tdefeat_experience\thit_chance_percent\tcritical_chance_percent\tcritical_multiplier\n12\t2\t250\t80\t25\t3\n");

        Assert.Equal(12, rules.BaseAttackDamage);
        Assert.Equal(2, rules.MinimumDamage);
        Assert.Equal(250U, rules.DefeatExperience);
        Assert.Equal(80, rules.HitChancePercent);
        Assert.Equal(25, rules.CriticalChancePercent);
        Assert.Equal(3, rules.CriticalMultiplier);
    }

    [Fact]
    public void RejectsInvalidDamageBounds()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeCombatRules(1, 2, 0));
    }

    [Fact]
    public void ResolvesArmorAndElementalResistanceDeterministically()
    {
        var rules = new AuthoritativeCombatRules(10, 1, 100);
        var target = new AuthoritativeCombatTarget(9, 0, 0, 20, armorClass: 3, fireResistance: 50);

        Assert.Equal(7, rules.ResolveDamage(10, target.ArmorClass, AuthoritativeDamageType.Physical, target));
        Assert.Equal(5, rules.ResolveDamage(10, target.ArmorClass, AuthoritativeDamageType.Fire, target));
    }

    [Fact]
    public void ResolvesDeterministicMissAndCriticalAttackOutcomes()
    {
        var target = new AuthoritativeCombatTarget(9, 0, 0, 100, armorClass: 0);
        var misses = new AuthoritativeCombatRules(10, 1, 100, hitChancePercent: 0);
        Assert.Equal(0, misses.ResolveAttackDamage(10, target, 1));

        var criticals = new AuthoritativeCombatRules(10, 1, 100, criticalChancePercent: 100, criticalMultiplier: 3);
        Assert.Equal(30, criticals.ResolveAttackDamage(10, target, 1));
    }

    [Fact]
    public void ResolvesAutonomousMonsterAttacksThroughTheSameRules()
    {
        var rules = new AuthoritativeCombatRules(3, 1, 100, hitChancePercent: 100, criticalChancePercent: 100, criticalMultiplier: 2);

        Assert.Equal(4, rules.ResolveAttackDamage(3, armorClass: 2, seed: 1));
    }
}
