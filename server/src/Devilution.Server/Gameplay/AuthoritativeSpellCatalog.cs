using Devilution.Server.Content;

namespace Devilution.Server.Gameplay;

/** Declarative spell tuning consumed by authoritative cast resolution. */
public sealed record AuthoritativeSpellDefinition(
    uint SpellId,
    int ManaCost,
    int HealingAmount,
    uint StatusEffectId,
    uint StatusDuration,
    int StatusMagnitude)
{
    public int DamageAmount { get; init; }

    public int Range { get; init; } = 1;

    public int AreaRadius { get; init; }

    public AuthoritativeDamageType DamageType { get; init; } = AuthoritativeDamageType.Physical;
}

public sealed class AuthoritativeSpellCatalog
{
    private readonly Dictionary<uint, AuthoritativeSpellDefinition> spells;

    public AuthoritativeSpellCatalog(IEnumerable<AuthoritativeSpellDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        spells = new Dictionary<uint, AuthoritativeSpellDefinition>();
        foreach (var definition in definitions) {
            if (definition.SpellId == 0 || definition.ManaCost <= 0 || definition.HealingAmount < 0 || definition.DamageAmount < 0)
                throw new InvalidDataException("Spell definitions require a non-zero ID, positive mana cost, and non-negative effects.");
            if (definition.DamageAmount > 0 && definition.Range <= 0)
                throw new InvalidDataException("Damage-bearing spells require a positive range.");
            if (definition.AreaRadius < 0)
                throw new InvalidDataException("Spell area radius cannot be negative.");
            if (definition.StatusEffectId != 0 && definition.StatusDuration == 0)
                throw new InvalidDataException("Status-bearing spells require a positive status duration.");
            if (!spells.TryAdd(definition.SpellId, definition))
                throw new InvalidDataException($"Spell {definition.SpellId} is defined more than once.");
        }
        if (spells.Count == 0)
            throw new InvalidDataException("The spell catalog cannot be empty.");
    }

    public IReadOnlyList<AuthoritativeSpellDefinition> Definitions => spells.Values.OrderBy(spell => spell.SpellId).ToArray();

    public bool TryGet(uint spellId, out AuthoritativeSpellDefinition definition) => spells.TryGetValue(spellId, out definition!);

    public static AuthoritativeSpellCatalog LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeSpellCatalog(table.Rows.Select(row => new AuthoritativeSpellDefinition(
            row.RequiredUInt32("spell_id"),
            row.RequiredInt32("mana_cost"),
            row.OptionalInt32("healing_amount"),
            row.OptionalUInt32("status_effect_id"),
            row.OptionalUInt32("status_duration"),
            row.OptionalInt32("status_magnitude")) {
            DamageAmount = row.OptionalInt32("damage_amount"),
            Range = row.OptionalInt32("range", 1),
            AreaRadius = row.OptionalInt32("area_radius"),
            DamageType = ParseDamageType(row),
        }));
    }

    private static AuthoritativeDamageType ParseDamageType(TsvRow row)
    {
        if (!row.TryGet("damage_type", out var value) || string.IsNullOrWhiteSpace(value))
            return AuthoritativeDamageType.Physical;
        if (Enum.TryParse<AuthoritativeDamageType>(value, ignoreCase: true, out var damageType))
            return damageType;
        throw new InvalidDataException($"Spell row {row.LineNumber} contains an unknown damage type '{value}'.");
    }
}
