using Devilution.Server.Content;

namespace Devilution.Server.Gameplay;

/** Externalized constants for the initial authoritative combat boundary. */
public sealed class AuthoritativeCombatRules
{
    public AuthoritativeCombatRules(
        int baseAttackDamage,
        int minimumDamage,
        uint defeatExperience,
        int hitChancePercent = 100,
        int criticalChancePercent = 0,
        int criticalMultiplier = 2)
    {
        if (baseAttackDamage <= 0 || minimumDamage <= 0 || minimumDamage > baseAttackDamage
            || hitChancePercent is < 0 or > 100 || criticalChancePercent is < 0 or > 100 || criticalMultiplier < 1)
            throw new InvalidDataException("Combat damage bounds are invalid.");
        BaseAttackDamage = baseAttackDamage;
        MinimumDamage = minimumDamage;
        DefeatExperience = defeatExperience;
        HitChancePercent = hitChancePercent;
        CriticalChancePercent = criticalChancePercent;
        CriticalMultiplier = criticalMultiplier;
    }

    public int BaseAttackDamage { get; }

    public int MinimumDamage { get; }

    public uint DefeatExperience { get; }

    public int HitChancePercent { get; }

    public int CriticalChancePercent { get; }

    public int CriticalMultiplier { get; }

    public int ResolveAttackDamage(int baseDamage, AuthoritativeCombatTarget target, uint seed)
    {
        return ResolveAttackDamage(baseDamage, target.ArmorClass, seed);
    }

    public int ResolveAttackDamage(int baseDamage, int armorClass, uint seed)
    {
        if (baseDamage <= 0)
            return 0;
        var hitRoll = unchecked(seed * 1664525U + 1013904223U) % 100U;
        if (hitRoll >= HitChancePercent)
            return 0;
        var damage = baseDamage;
        var criticalRoll = unchecked(seed * 1103515245U + 12345U) % 100U;
        if (criticalRoll < CriticalChancePercent)
            damage = checked(damage * CriticalMultiplier);
        return Math.Max(0, Math.Max(MinimumDamage, damage - Math.Max(0, armorClass)));
    }

    public int ResolveDamage(int baseDamage, int armorClass, AuthoritativeDamageType damageType, AuthoritativeCombatTarget target)
    {
        if (baseDamage <= 0)
            return 0;
        var postArmor = damageType == AuthoritativeDamageType.Physical
            ? Math.Max(MinimumDamage, baseDamage - Math.Max(0, armorClass))
            : baseDamage;
        var resistance = damageType switch {
            AuthoritativeDamageType.Fire => target.FireResistance,
            AuthoritativeDamageType.Lightning => target.LightningResistance,
            AuthoritativeDamageType.Magic => target.MagicResistance,
            _ => 0,
        };
        return Math.Max(0, postArmor * (100 - Math.Clamp(resistance, -100, 100)) / 100);
    }

    public static AuthoritativeCombatRules LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var row = table.Rows.Single();
        return new AuthoritativeCombatRules(
            row.RequiredInt32("base_attack_damage"),
            row.RequiredInt32("minimum_damage"),
            row.RequiredUInt32("defeat_experience"),
            row.OptionalInt32("hit_chance_percent", 100),
            row.OptionalInt32("critical_chance_percent"),
            row.OptionalInt32("critical_multiplier", 2));
    }
}
