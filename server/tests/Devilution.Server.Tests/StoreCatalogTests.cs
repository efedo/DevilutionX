using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class StoreCatalogTests
{
    [Fact]
    public void DuplicateSlotsDoNotPartiallyRegisterAStore()
    {
        var catalog = new StoreCatalog();

        Assert.Throws<ArgumentException>(() => catalog.AddStore(1, [
            new StoreItem(0, 42, 75),
            new StoreItem(0, 43, 80),
        ]));

        catalog.AddStore(1, [new StoreItem(0, 42, 75)]);
        Assert.True(catalog.TryGetItem(1, 0, out var item));
        Assert.Equal(42U, item.ItemSeed);
    }

    [Fact]
    public void ItemStateIsRetainedInAuthoritativeStock()
    {
        var state = AuthoritativeItemState.Empty with { Identified = true, ItemType = 1 };
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 75, state)]);

        Assert.True(catalog.TryGetItem(1, 0, out var item));
        Assert.Equal(state, item.State);
    }
}
