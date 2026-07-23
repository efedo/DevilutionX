using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Snapshots;
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

    [Fact]
    public void SnapshotContainsAuthoritativeWalletStoreAndInventory()
    {
        var itemState = new AuthoritativeItemState(
            123,
            1,
            4,
            -2,
            false,
            true,
            1,
            2,
            3,
            100,
            80,
            1,
            3,
            4,
            7,
            5,
            6,
            9,
            2,
            4,
            10,
            20);
        var catalog = CreateCatalog(new StoreItem(0, 42, 75, itemState));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), currentTick: 10);
        server.Process("player-a", Purchase(2, 0), currentTick: 12);

        var snapshot = executor.CreateSnapshot("player-a", entityId: 7, tick: 12);
        var player = Assert.Single(snapshot.Players);
        var item = Assert.Single(player.Inventory);

        Assert.Equal(12UL, snapshot.Tick);
        Assert.Equal(7U, player.EntityId);
        Assert.Equal(25U, player.Gold);
        Assert.Equal(1U, player.ActiveStoreId);
        Assert.Equal("b4ca80780c2ea293cfa2cc09277eab13688e158e4f120adc9819a0d787368dd9", snapshot.StateSha256);
        Assert.Equal(SnapshotStateHasher.Compute(snapshot), snapshot.StateSha256);
        Assert.Equal(1U, item.StoreId);
        Assert.Equal(0U, item.StoreSlot);
        Assert.Equal(42U, item.ItemSeed);
        Assert.Equal(75U, item.Price);
        Assert.Equal(12UL, item.PurchasedAtTick);
        Assert.Equal(123U, item.State.CreateInfo);
        Assert.Equal(1, item.State.ItemType);
        Assert.Equal(4, item.State.PositionX);
        Assert.Equal(-2, item.State.PositionY);
        Assert.True(item.State.Identified);
        Assert.Equal(100, item.State.Value);
        Assert.Equal(3, item.State.MaxDamage);
        Assert.Equal(7U, item.State.Flags);
        Assert.Equal(20, item.State.MaxDurability);
    }

    [Fact]
    public void SnapshotIncludesConfiguredBaselinePlayerResources()
    {
        var attributes = new PlayerAttributesState(
            new PlayerAttributeState(10, 12),
            new PlayerAttributeState(8, 9),
            new PlayerAttributeState(15, 16),
            new PlayerAttributeState(20, 21));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            startingGold: 100,
            startingExperience: 200,
            startingLife: 640,
            startingMana: 32,
            startingAttributes: attributes,
            startingEquipment: [new EquippedStoreItem(0, 77)],
            startingInventoryGrid: [0, -1, 2]);

        var state = executor.GetPlayerState("player-a");
        var snapshot = executor.CreateSnapshot("player-a", entityId: 7, tick: 0);
        var player = Assert.Single(snapshot.Players);

        Assert.Equal(200U, state.Experience);
        Assert.Equal(640, state.Life);
        Assert.Equal(32, state.Mana);
        Assert.Equal(200U, player.Experience);
        Assert.Equal(640, player.Life);
        Assert.Equal(32, player.Mana);
        Assert.Equal(12, player.Attributes.Strength.Current);
        Assert.Equal(9, player.Attributes.Magic.Current);
        Assert.Equal(16, player.Attributes.Dexterity.Current);
        Assert.Equal(21, player.Attributes.Vitality.Current);
        Assert.Equal(77U, Assert.Single(player.Equipment).ItemSeed);
        Assert.Equal(new[] { 0, -1, 2 }, player.InventoryGrid);

        var changed = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = player.EntityId,
                    Experience = player.Experience + 1,
                    Life = player.Life,
                    Mana = player.Mana,
                    Gold = player.Gold,
                },
            },
        };
        Assert.NotEqual(snapshot.StateSha256, SnapshotStateHasher.Compute(changed));
    }

    [Fact]
    public void ModuleOwnedStoreTransactionsSupportIdentificationRepairRechargeMoveAndSale()
    {
        var itemState = new AuthoritativeItemState(
            1,
            1,
            0,
            0,
            false,
            false,
            1,
            0,
            1,
            100,
            100,
            1,
            2,
            5,
            0,
            0,
            0,
            1,
            1,
            3,
            10,
            20);
        var catalog = CreateCatalog(new StoreItem(0, 42, 75, itemState));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 1000, startingInventoryGrid: [-1, -1]);
        var server = new AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1), 0).Status);
        var purchase = Purchase(2, 0);
        purchase.RequestedTick = 1;
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", purchase, 1).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 2,
            IdentifyItemRequested = new IdentifyItemRequested { InventoryIndex = 0 },
        }, 2).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 4,
            RequestedTick = 3,
            RepairItemRequested = new RepairItemRequested { InventoryIndex = 0 },
        }, 3).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 5,
            RequestedTick = 4,
            RechargeItemRequested = new RechargeItemRequested { InventoryIndex = 0 },
        }, 4).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 6,
            RequestedTick = 5,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 1 },
        }, 5).Status);

        var state = executor.GetPlayerState("player-a");
        var item = Assert.Single(state.Inventory);
        Assert.Equal(0, state.InventoryGrid[1]);
        Assert.True(item.State.Identified);
        Assert.Equal(item.State.MaxDurability, item.State.Durability);
        Assert.Equal(item.State.MaxCharges, item.State.Charges);

        var sale = server.Process("player-a", new Command {
            ClientSequence = 7,
            RequestedTick = 6,
            SellItemRequested = new SellItemRequested { InventoryIndex = 0 },
        }, 6);

        Assert.Equal(CommandStatus.Accepted, sale.Status);
        Assert.Empty(executor.GetPlayerState("player-a").Inventory);
        Assert.Equal(835U, executor.GetPlayerState("player-a").Gold);
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
