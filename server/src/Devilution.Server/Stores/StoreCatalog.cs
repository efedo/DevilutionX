namespace Devilution.Server.Stores;

/** A deterministic item offered in one authoritative store slot. */
public sealed record StoreItem(uint StoreSlot, uint ItemSeed, uint Price);

/**
 * Immutable-by-convention store stock owned by the authoritative simulation.
 * The executor serializes mutations through its own state lock.
 */
public sealed class StoreCatalog
{
    private readonly Dictionary<uint, Dictionary<uint, StoreItem>> stores = new();

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
}
