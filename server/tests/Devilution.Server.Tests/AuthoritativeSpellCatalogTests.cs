using Devilution.Server.Gameplay;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeSpellCatalogTests
{
    [Fact]
    public void LoadsDeclarativeHealingAndStatusTuning()
    {
        var catalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tmana_cost\thealing_amount\tstatus_effect_id\tstatus_duration\tstatus_magnitude\n"
            + "7\t4\t12\t3\t8\t2\n");

        Assert.True(catalog.TryGet(7, out var spell));
        Assert.Equal(4, spell.ManaCost);
        Assert.Equal(12, spell.HealingAmount);
        Assert.Equal(3U, spell.StatusEffectId);
        Assert.Equal(8U, spell.StatusDuration);

        var damageCatalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tmana_cost\thealing_amount\tdamage_amount\trange\tstatus_effect_id\tstatus_duration\tstatus_magnitude\n"
                + "8\t4\t0\t12\t2\t0\t0\t0\n");
        Assert.True(damageCatalog.TryGet(8, out var damageSpell));
        Assert.Equal(12, damageSpell.DamageAmount);
        Assert.Equal(2, damageSpell.Range);

        var areaCatalog = AuthoritativeSpellCatalog.LoadTsv(
            "spells.tsv",
            "spell_id\tmana_cost\tdamage_amount\trange\tarea_radius\tdamage_type\n"
                + "9\t6\t10\t4\t1\tFire\n");
        Assert.True(areaCatalog.TryGet(9, out var areaSpell));
        Assert.Equal(1, areaSpell.AreaRadius);
        Assert.Equal(AuthoritativeDamageType.Fire, areaSpell.DamageType);
    }

    [Fact]
    public void RejectsDuplicateOrInvalidSpellDefinitions()
    {
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 0, 0, 0, 0),
            new AuthoritativeSpellDefinition(1, 5, 0, 0, 0, 0),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 0, 0, 0, 0, 0),
        ]));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 0, 0, 0, 0) { DamageAmount = 10, Range = 0 },
        ]));
    }

    [Fact]
    public void LoadsProjectileTuningAndRejectsProjectileHealing()
    {
        var catalog = AuthoritativeSpellCatalog.LoadTsv("projectiles.tsv", "spell_id\tmana_cost\tdamage_amount\tprojectile_speed\tprojectile_lifetime\n7\t4\t12\t2\t8");
        var spell = Assert.Single(catalog.Definitions);
        Assert.Equal(2, spell.ProjectileSpeed);
        Assert.Equal(8, spell.ProjectileLifetime);

        Assert.Throws<InvalidDataException>(() => new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(8, 3, 10, 0, 0, 0) { ProjectileSpeed = 1 },
        ]));
    }
}
