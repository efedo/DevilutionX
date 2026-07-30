using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
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
        Assert.Equal("f44870bf6d2003b08092b991adc473c757b0548b6744fb48831cd70027baf877", snapshot.StateSha256);
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
            10,
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
        Assert.Equal(810U, executor.GetPlayerState("player-a").Gold);
    }

    [Fact]
    public void ServiceCommandsCanTargetBeltAndEquipmentSlots()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingBelt: [new BeltStoreItem(2, 101)],
            startingEquipment: [new EquippedStoreItem(3, 102)]);
        var server = new AuthoritativeCommandServer(executor);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1), 0).Status);

        var beltSale = new Command {
            ClientSequence = 2,
            RequestedTick = 1,
            SellItemRequested = new SellItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Belt, Slot = 2 },
            },
        };
        var equipmentSale = new Command {
            ClientSequence = 3,
            RequestedTick = 2,
            SellItemRequested = new SellItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Equipment, Slot = 3 },
            },
        };

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", beltSale, 1).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", equipmentSale, 2).Status);
        var state = executor.GetPlayerState("player-a");
        Assert.Empty(state.Belt);
        Assert.Empty(state.Equipment);
        Assert.Equal(102U, state.Gold);
    }

    [Fact]
    public void ManaRefillUsesConfiguredMaximumAndDeductsDeterministicPrice()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 100, startingMana: 32, startingManaMaximum: 640);
        var server = new AuthoritativeCommandServer(executor);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1, 10), 0).Status);

        var refill = new Command {
            ClientSequence = 2,
            RequestedTick = 10,
            RefillManaRequested = new RefillManaRequested(),
        };
        var result = server.Process("player-a", refill, 10);
        var state = executor.GetPlayerState("player-a");

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(640, state.Mana);
        Assert.Equal(90U, state.Gold);
        Assert.Equal(640, executor.CreateSnapshot("player-a", 1, 10).Players[0].ManaMaximum);

        var alreadyFull = server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 11,
            RefillManaRequested = new RefillManaRequested(),
        }, 11);
        Assert.Equal(CommandStatus.Rejected, alreadyFull.Status);
        Assert.Equal(CommandRejectReason.NotAllowed, alreadyFull.RejectReason);
    }

    [Fact]
    public void RemovingInventoryItemCompactsGridReferences()
    {
        var catalog = CreateCatalog(new StoreItem(0, 42, 10), new StoreItem(1, 43, 10));
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100, startingInventoryGrid: [0, 1, 1, -1]);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 0);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(2, 0), 1).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(3, 1), 2).Status);

        var sale = new Command {
            ClientSequence = 4,
            RequestedTick = 3,
            SellItemRequested = new SellItemRequested { InventoryIndex = 0 },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", sale, 3).Status);

        Assert.Equal(new[] { -1, 0, 0, -1 }, executor.GetPlayerState("player-a").InventoryGrid);
    }

    [Fact]
    public void ExplicitMoveTransfersInventoryAndBeltItemsAuthoritatively()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(new StoreItem(0, 42, 10)), startingGold: 100, startingInventoryGrid: [0]);
        var server = new AuthoritativeCommandServer(executor);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", OpenStore(1), 10).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(2, 0), 12).Status);

        var toBelt = new Command {
            ClientSequence = 3,
            RequestedTick = 13,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 0 },
                Destination = new PlayerItemReference { Location = PlayerItemLocation.Belt, Slot = 0 },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", toBelt, 13).Status);
        Assert.Empty(executor.GetPlayerState("player-a").Inventory);
        Assert.Equal(42U, Assert.Single(executor.GetPlayerState("player-a").Belt).ItemSeed);

        var toInventory = new Command {
            ClientSequence = 4,
            RequestedTick = 14,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Belt, Slot = 0 },
                Destination = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 0 },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", toInventory, 14).Status);
        Assert.Equal(42U, Assert.Single(executor.GetPlayerState("player-a").Inventory).ItemSeed);
        Assert.Equal(new[] { 0 }, executor.GetPlayerState("player-a").InventoryGrid);
        Assert.Empty(executor.GetPlayerState("player-a").Belt);
    }

    [Fact]
    public void ExplicitMoveSwapsInventoryAndEquipmentItemsAtomically()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10)),
            startingGold: 100,
            startingInventoryGrid: [0],
            startingEquipment: [new EquippedStoreItem(3, 99)]);
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 10);
        server.Process("player-a", Purchase(2, 0), 12);

        var swap = new Command {
            ClientSequence = 3,
            RequestedTick = 13,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 0 },
                Destination = new PlayerItemReference { Location = PlayerItemLocation.Equipment, Slot = 3 },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", swap, 13).Status);
        Assert.Equal(99U, Assert.Single(executor.GetPlayerState("player-a").Inventory).ItemSeed);
        Assert.Equal(42U, Assert.Single(executor.GetPlayerState("player-a").Equipment).ItemSeed);
        Assert.Equal(new[] { 0 }, executor.GetPlayerState("player-a").InventoryGrid);
    }

    [Fact]
    public void InventoryPlacementHonorsMultiCellItemFootprints()
    {
        var itemState = AuthoritativeItemState.Empty with { ItemType = 1, InventoryWidth = 2, InventoryHeight = 2 };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10, itemState)),
            startingGold: 100,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 10);
        server.Process("player-a", Purchase(2, 0), 12);

        var move = new Command {
            ClientSequence = 3,
            RequestedTick = 13,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 8 },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", move, 13).Status);
        var grid = executor.GetPlayerState("player-a").InventoryGrid;
        Assert.Equal(0, grid[8]);
        Assert.Equal(0, grid[9]);
        Assert.Equal(0, grid[18]);
        Assert.Equal(0, grid[19]);
    }

    [Fact]
    public void MultiCellPlacementRejectsEdgeAndCollisionTargets()
    {
        var large = AuthoritativeItemState.Empty with { ItemType = 1, InventoryWidth = 2, InventoryHeight = 1 };
        var small = AuthoritativeItemState.Empty with { ItemType = 2 };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10, large), new StoreItem(1, 43, 10, small)),
            startingGold: 100,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 10);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(2, 0), 12).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", Purchase(3, 1), 12).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 4,
            RequestedTick = 14,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 8 },
        }, 14).Status);

        Assert.Equal(CommandStatus.Rejected, server.Process("player-a", new Command {
            ClientSequence = 5,
            RequestedTick = 15,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 9 },
        }, 15).Status);
        Assert.Equal(CommandStatus.Rejected, server.Process("player-a", new Command {
            ClientSequence = 6,
            RequestedTick = 16,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 1, TargetCell = 9 },
        }, 16).Status);
    }

    [Fact]
    public void MultiCellInventoryItemsSwapOnlyWhenBothFootprintsFit()
    {
        var large = AuthoritativeItemState.Empty with { ItemType = 1, InventoryWidth = 2, InventoryHeight = 1 };
        var small = AuthoritativeItemState.Empty with { ItemType = 2 };
        var executor = new StoreSimulationExecutor(
            CreateCatalog(new StoreItem(0, 42, 10, large), new StoreItem(1, 43, 10, small)),
            startingGold: 100,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray());
        var server = new AuthoritativeCommandServer(executor);
        server.Process("player-a", OpenStore(1), 10);
        server.Process("player-a", Purchase(2, 0), 12);
        server.Process("player-a", Purchase(3, 1), 12);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 4,
            RequestedTick = 14,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 0, TargetCell = 8 },
        }, 14).Status);
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 5,
            RequestedTick = 15,
            MoveInventoryItemRequested = new MoveInventoryItemRequested { InventoryIndex = 1, TargetCell = 20 },
        }, 15).Status);

        var swap = new Command {
            ClientSequence = 6,
            RequestedTick = 16,
            MoveItemRequested = new MoveItemRequested {
                Item = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 1 },
                Destination = new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = 8 },
            },
        };
        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", swap, 16).Status);
        var state = executor.GetPlayerState("player-a");
        Assert.Equal(0, state.InventoryGrid[8]);
        Assert.Equal(-1, state.InventoryGrid[9]);
        Assert.Equal(1, state.InventoryGrid[20]);
        Assert.Equal(1, state.InventoryGrid[21]);
    }

    [Fact]
    public void MovementIsValidatedAndIncludedInTheAuthoritativeSnapshot()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            startingLife: 40,
            startingLifeMaximum: 60,
            startingCharacterLevel: 3);
        var server = new AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 5,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = -1 },
        }, 5).Status);
        var snapshot = executor.CreateSnapshot("player-a", 7, 5);
        var player = Assert.Single(snapshot.Players);
        Assert.Equal(2, player.PositionX);
        Assert.Equal(0, player.PositionY);
        Assert.Equal(60, player.LifeMaximum);
        Assert.Equal(3U, player.CharacterLevel);

        var invalidDirection = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 6,
            MoveRequested = new MoveRequested(),
        }, 6);
        Assert.Equal(CommandRejectReason.Malformed, invalidDirection.RejectReason);
        var outOfBounds = server.Process("player-a", new Command {
            ClientSequence = 3,
            RequestedTick = 7,
            MoveRequested = new MoveRequested { DirectionX = -1, DirectionY = -1 },
        }, 7);
        Assert.Equal(CommandRejectReason.InvalidTarget, outOfBounds.RejectReason);
    }

    [Fact]
    public void HealingCastConsumesManaAndCannotExceedLifeMaximum()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingLife: 10,
            startingLifeMaximum: 25,
            startingMana: 10,
            startingManaMaximum: 10);
        var server = new AuthoritativeCommandServer(executor);
        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested { SpellId = 1 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        var state = executor.GetPlayerState("player-a");
        Assert.Equal(25, state.Life);
        Assert.Equal(5, state.Mana);

        var full = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            CastRequested = new CastRequested { SpellId = 1 },
        }, 2);
        Assert.Equal(CommandRejectReason.NotAllowed, full.RejectReason);
    }

    [Fact]
    public void AdjacentAttackDamagesTargetsAndAwardsExperienceOnDefeat()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, hitPoints: 11, armorClass: 2);
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 0,
            startingPositionY: 0,
            startingCharacterLevel: 1,
            startingCombatTargets: [target]);
        var server = new AuthoritativeCommandServer(executor);

        var first = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            AttackRequested = new AttackRequested { TargetEntityId = 9 },
        }, 1);
        Assert.Equal(CommandStatus.Accepted, first.Status);
        Assert.Equal(3, target.HitPoints);
        Assert.Equal(0U, executor.GetPlayerState("player-a").Experience);
        var hitEvents = executor.DrainEvents("player-a", 7, 1);
        Assert.NotNull(hitEvents);
        Assert.Equal(8, Assert.Single(hitEvents.Events).Damage.Amount);

        var second = server.Process("player-a", new Command {
            ClientSequence = 2,
            RequestedTick = 2,
            AttackRequested = new AttackRequested { TargetEntityId = 9 },
        }, 2);
        Assert.Equal(CommandStatus.Accepted, second.Status);
        Assert.Equal(0, target.HitPoints);
        Assert.Equal(100U, executor.GetPlayerState("player-a").Experience);
        var defeatEvents = executor.DrainEvents("player-a", 7, 2);
        Assert.NotNull(defeatEvents);
        Assert.Equal(2, defeatEvents.Events.Count);
        Assert.Equal(100U, defeatEvents.Events.Single(gameEvent => gameEvent.Experience is not null).Experience.Amount);

        var distant = new AuthoritativeCombatTarget(10, 4, 4, 10);
        var distantExecutor = new StoreSimulationExecutor(CreateCatalog(), 100, startingCombatTargets: [distant]);
        var distantResult = new AuthoritativeCommandServer(distantExecutor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            AttackRequested = new AttackRequested { TargetEntityId = 10 },
        }, 1);
        Assert.Equal(CommandRejectReason.InvalidTarget, distantResult.RejectReason);
    }

    [Fact]
    public void MovementRejectsBlockedCellsWithoutChangingPosition()
    {
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            worldWidth: 4,
            worldHeight: 4,
            startingBlockedCells: [1 * 4 + 2]);
        var result = new AuthoritativeCommandServer(executor).Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            MoveRequested = new MoveRequested { DirectionX = 1, DirectionY = 0 },
        }, 1);

        Assert.Equal(CommandRejectReason.InvalidTarget, result.RejectReason);
        var state = executor.GetPlayerState("player-a");
        Assert.Equal(1, state.PositionX);
        Assert.Equal(1, state.PositionY);
    }

    [Fact]
    public void PortalRequiresSourceCellAndProjectsDestinationLevel()
    {
        var portal = new AuthoritativePortal(5, 2, 1, 1, 7, 3, 4);
        var executor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            startingLevelId: 2,
            startingPortals: [portal]);
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            UsePortalRequested = new UsePortalRequested { PortalId = 5 },
        }, 1);
        var snapshot = executor.CreateSnapshot("player-a", 7, 1);
        var player = Assert.Single(snapshot.Players);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(7U, player.LevelId);
        Assert.Equal(3, player.PositionX);
        Assert.Equal(4, player.PositionY);

        var invalidExecutor = new StoreSimulationExecutor(
            CreateCatalog(),
            startingGold: 100,
            startingPositionX: 1,
            startingPositionY: 1,
            startingLevelId: 1,
            startingPortals: [portal]);
        var invalid = new AuthoritativeCommandServer(invalidExecutor).Process("player-b", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            UsePortalRequested = new UsePortalRequested { PortalId = 5 },
        }, 1);
        Assert.Equal(CommandRejectReason.InvalidTarget, invalid.RejectReason);
    }

    [Fact]
    public void HasteStatusIsAuthoritativeAndExpiresByAppliedTick()
    {
        var executor = new StoreSimulationExecutor(CreateCatalog(), startingGold: 100, startingMana: 10, startingManaMaximum: 10);
        var server = new AuthoritativeCommandServer(executor);
        var result = server.Process("player-a", new Command {
            ClientSequence = 1,
            RequestedTick = 1,
            CastRequested = new CastRequested { SpellId = 2 },
        }, 1);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(7, executor.GetPlayerState("player-a").Mana);
        var active = executor.CreateSnapshot("player-a", 7, 1).Players[0];
        var effect = Assert.Single(active.StatusEffects);
        Assert.Equal(1U, effect.EffectId);
        Assert.Equal(10U, effect.RemainingTicks);

        var expired = executor.CreateSnapshot("player-a", 7, 11).Players[0];
        Assert.Empty(expired.StatusEffects);
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
