using Devilution.Server.Content;

namespace Devilution.Server.Stores;

/** Core authoritative item fields shared by store, inventory, and equipment projections. */
public sealed record AuthoritativeItemState(
    uint CreateInfo,
    int ItemType,
    int PositionX,
    int PositionY,
    bool Deleted,
    bool Identified,
    int Magical,
    int EquipLocation,
    int ItemClass,
    int Value,
    int IdentifiedValue,
    int MinDamage,
    int MaxDamage,
    int ArmorClass,
    uint Flags,
    int MiscId,
    int SpellId,
    int ItemIndex,
    int Charges,
    int MaxCharges,
    int Durability,
    int MaxDurability)
{
    public int PlusDamage { get; init; }

    public int PlusToHit { get; init; }

    public int PlusArmorClass { get; init; }

    public int PlusStrength { get; init; }

    public int PlusMagic { get; init; }

    public int PlusDexterity { get; init; }

    public int PlusVitality { get; init; }

    public int PlusFireResistance { get; init; }

    public int PlusLightningResistance { get; init; }

    public int PlusMagicResistance { get; init; }

    public int PlusMana { get; init; }

    public int PlusHitPoints { get; init; }

    public int PlusDamageModifier { get; init; }

    public int PlusGetHit { get; init; }

    public int PlusLight { get; init; }

    public int SpellLevelAdd { get; init; }

    public int UniqueId { get; init; }

    public int FireMinDamage { get; init; }

    public int FireMaxDamage { get; init; }

    public int LightningMinDamage { get; init; }

    public int LightningMaxDamage { get; init; }

    public int PlusEnemyArmorClass { get; init; }

    public int PrefixPower { get; init; } = -1;

    public int SuffixPower { get; init; } = -1;

    public int ValueAdd1 { get; init; }

    public int ValueMultiply1 { get; init; }

    public int ValueAdd2 { get; init; }

    public int ValueMultiply2 { get; init; }

    public int MinimumStrength { get; init; }

    public int MinimumMagic { get; init; }

    public int MinimumDexterity { get; init; }

    public bool StatFlag { get; init; }

    public int HellfireDamageArmorFlags { get; init; }

    public uint Buff { get; init; }

    public static AuthoritativeItemState Empty { get; } = new(
        0, -1, 0, 0, false, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0);
}

/** A deterministic item offered in one authoritative store slot. */
public sealed record StoreItem(uint StoreSlot, uint ItemSeed, uint Price)
{
    public AuthoritativeItemState State { get; init; } = AuthoritativeItemState.Empty;

    public StoreItem(uint storeSlot, uint itemSeed, uint price, AuthoritativeItemState state)
        : this(storeSlot, itemSeed, price)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }
}

/**
 * Immutable-by-convention store stock owned by the authoritative simulation.
 * The executor serializes mutations through its own state lock.
 */
public sealed class StoreCatalog
{
    private readonly Dictionary<uint, Dictionary<uint, StoreItem>> stores = new();

    /** Loads the first store slice from an external TSV table. */
    public static StoreCatalog LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var catalog = new StoreCatalog();
        var grouped = table.Rows.GroupBy(row => row.RequiredUInt32("store_id"));
        foreach (var store in grouped) {
            catalog.AddStore(store.Key, store.Select(row => new StoreItem(
                row.RequiredUInt32("store_slot"),
                row.RequiredUInt32("item_seed"),
                row.RequiredUInt32("price"),
                ParseItemState(row))));
        }
        if (!catalog.stores.Any())
            throw new InvalidDataException($"Store table '{sourcePath}' does not contain any stores.");
        return catalog;
    }

    public void AddStore(uint storeId, IEnumerable<StoreItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (stores.ContainsKey(storeId))
            throw new ArgumentException($"Store {storeId} has already been registered.", nameof(storeId));

        var stock = new Dictionary<uint, StoreItem>();
        foreach (var item in items) {
            if (!stock.TryAdd(item.StoreSlot, item))
                throw new ArgumentException($"Store {storeId} contains duplicate slot {item.StoreSlot}.", nameof(items));
        }

        stores.Add(storeId, stock);
    }

    public bool ContainsStore(uint storeId)
    {
        return stores.ContainsKey(storeId);
    }

    public bool TryGetItem(uint storeId, uint storeSlot, out StoreItem item)
    {
        if (stores.TryGetValue(storeId, out var stock) && stock.TryGetValue(storeSlot, out item!))
            return true;

        item = null!;
        return false;
    }

    public bool RemoveItem(uint storeId, uint storeSlot)
    {
        return stores.TryGetValue(storeId, out var stock) && stock.Remove(storeSlot);
    }

    /** Returns a stable copy of the remaining items in one store. */
    public IReadOnlyList<StoreItem> GetItems(uint storeId)
    {
        if (!stores.TryGetValue(storeId, out var stock))
            return [];

        return stock.Values.OrderBy(item => item.StoreSlot).ToArray();
    }

    private static AuthoritativeItemState ParseItemState(TsvRow row)
    {
        return AuthoritativeItemState.Empty with {
            CreateInfo = row.OptionalUInt32("create_info"),
            ItemType = row.OptionalInt32("item_type", -1),
            PositionX = row.OptionalInt32("position_x"),
            PositionY = row.OptionalInt32("position_y"),
            Deleted = row.OptionalInt32("deleted") != 0,
            Identified = row.OptionalInt32("identified") != 0,
            Magical = row.OptionalInt32("magical"),
            EquipLocation = row.OptionalInt32("equip_location"),
            ItemClass = row.OptionalInt32("item_class"),
            Value = row.OptionalInt32("value"),
            IdentifiedValue = row.OptionalInt32("identified_value"),
            MinDamage = row.OptionalInt32("min_damage"),
            MaxDamage = row.OptionalInt32("max_damage"),
            ArmorClass = row.OptionalInt32("armor_class"),
            Flags = row.OptionalUInt32("flags"),
            MiscId = row.OptionalInt32("misc_id"),
            SpellId = row.OptionalInt32("spell_id"),
            ItemIndex = row.OptionalInt32("item_index", -1),
            Charges = row.OptionalInt32("charges"),
            MaxCharges = row.OptionalInt32("max_charges"),
            Durability = row.OptionalInt32("durability"),
            MaxDurability = row.OptionalInt32("max_durability"),
        };
    }
}
