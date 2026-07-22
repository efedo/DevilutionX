using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class StoreSimulationExecutorTests
{
    [Fact]
    public void PurchaseDeductsGoldAndConsumesStockOnceAcrossRetries()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);

        var open = server.Process("player-a", OpenStore(1), currentTick: 10);
        var purchase = server.Process("player-a", Purchase(2, 0), currentTick: 12);
        var duplicate = server.Process("player-a", Purchase(2, 0), currentTick: 13);
        var state = executor.GetPlayerState("player-a");

        Assert.Equal(CommandStatus.Accepted, open.Status);
        Assert.Equal(CommandStatus.Accepted, purchase.Status);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(25U, state.Gold);
        var purchasedItem = Assert.Single(state.Inventory);
        Assert.Equal(42U, purchasedItem.ItemSeed);
        Assert.False(catalog.TryGetItem(1, 0, out _));
    }

    [Fact]
    public void InsufficientGoldLeavesWalletAndStockUnchanged()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 50);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), currentTick: 10);

        var result = server.Process("player-a", Purchase(2, 0), currentTick: 12);
        var duplicate = server.Process("player-a", Purchase(2, 0), currentTick: 13);
        var state = executor.GetPlayerState("player-a");

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.InsufficientResources, result.RejectReason);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(50U, state.Gold);
        Assert.Empty(state.Inventory);
        Assert.True(catalog.TryGetItem(1, 0, out _));
    }

    [Fact]
    public void PurchaseRequiresTheMatchingOpenStore()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", Purchase(1, 0), currentTick: 10);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, result.RejectReason);
        Assert.Empty(executor.GetPlayerState("player-a").Inventory);
    }

    [Fact]
    public void StockIsSharedAcrossSessions()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), currentTick: 10);
        server.Process("player-b", OpenStore(3), currentTick: 10);
        var firstPurchase = server.Process("player-a", Purchase(2, 0), currentTick: 12);
        var secondPurchase = server.Process("player-b", Purchase(4, 0), currentTick: 12);

        Assert.Equal(CommandStatus.Accepted, firstPurchase.Status);
        Assert.Equal(CommandStatus.Rejected, secondPurchase.Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, secondPurchase.RejectReason);
        Assert.Single(executor.GetPlayerState("player-a").Inventory);
        Assert.Empty(executor.GetPlayerState("player-b").Inventory);
    }

    [Fact]
    public void UnknownStoreAndUnknownSlotAreRejected()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 75));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);

        var unknownStore = server.Process("player-a", OpenStore(1, 999), currentTick: 10);
        server.Process("player-a", OpenStore(2, 1), currentTick: 11);
        var unknownSlot = server.Process("player-a", Purchase(3, 99), currentTick: 12);

        Assert.Equal(CommandRejectReason.InvalidTarget, unknownStore.RejectReason);
        Assert.Equal(CommandRejectReason.InvalidTarget, unknownSlot.RejectReason);
    }

    private static StoreCatalog CreateCatalog(params StoreItem[] items)
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, items);
        return catalog;
    }

    private static Command OpenStore(ulong clientSequence, uint storeId = 1)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = 10,
            OpenStoreRequested = new OpenStoreRequested { StoreId = storeId },
        };
    }

    private static Command Purchase(ulong clientSequence, uint slot)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = 12,
            PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = slot },
        };
    }
}
