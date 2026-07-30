using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Replay;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ReplayFixtureTests
{
    [Fact]
    public void BasicBuyFixtureLoadsAndSortsByAuthoritativeOrder()
    {
        var fixture = LoadFixture();

        Assert.Equal("stores/basic-buy", fixture.FixtureId);
        Assert.Equal(305419896UL, fixture.RngSeed);
        Assert.Equal([1UL, 2UL], fixture.OrderedCommands.Select(command => command.ClientSequence));
        Assert.Equal("OpenStore", fixture.OrderedCommands[0].Kind);
        Assert.Equal("BuyItem", fixture.OrderedCommands[1].Kind);
        Assert.Equal(0U, fixture.OrderedCommands[1].StoreSlot);
    }

    [Fact]
    public void BasicBuyFixtureExecutesCommandsAndMatchesLegacyInitialHash()
    {
        var fixture = LoadFixture();
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 75)]);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum);
        var commandServer = new AuthoritativeCommandServer(executor);

        var result = ReplayFixtureExecutor.Execute(fixture, executor, commandServer);

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal([1UL, 2UL], result.Results.Select(command => command.ServerReceiptSequence));
        Assert.Equal(25U, Assert.Single(result.FinalSnapshot.Players).Gold);
        var checkpoint = Assert.Single(fixture.Checkpoints);
        Assert.Equal(0UL, checkpoint.Tick);
        Assert.Equal(checkpoint.StateSha256, LegacyReplayStateHasher.Compute(fixture));
        Assert.NotEqual(checkpoint.StateSha256, result.InitialSnapshot.StateSha256);
    }

    [Fact]
    public void TransactionParityFixtureExecutesPurchaseSaleAndManaRefill()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "transaction-parity.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 75, AuthoritativeItemState.Empty with { ItemType = 1, Value = 75, IdentifiedValue = 75, Durability = 1, MaxDurability = 1 })]);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum);
        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal(33U, player.Gold);
        Assert.Equal(640, player.Mana);
        Assert.Empty(player.Inventory);
    }

    [Fact]
    public void GameplayFixtureExecutesMovementAndCombatTransitions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-movement-combat.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLifeMaximum: fixture.InitialState.Life,
            startingCharacterLevel: fixture.InitialState.CharacterLevel,
            startingCombatTargets: [new AuthoritativeCombatTarget(9, 2, 0, 11, 2)]);
        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(1, player.PositionX);
        Assert.Equal(100U, player.Experience);
        Assert.Equal(40, player.Life);
    }

    [Fact]
    public void GameplaySpellFixtureExecutesDataDrivenHealingTransition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-spell-cast.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingLifeMaximum: 40);

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(5, player.Mana);
        Assert.Equal(40, player.Life);
    }

    [Fact]
    public void GameplayPortalFixtureExecutesLevelTransition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-portal-transition.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 40, 40, []));
        world.AddLevel(new AuthoritativeLevel(2, 40, 40, []));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorld: world,
            startingPortals: [new AuthoritativePortal(5, 1, 1, 1, 2, 3, 4)]);

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);
        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(2U, player.LevelId);
        Assert.Equal(3, player.PositionX);
        Assert.Equal(4, player.PositionY);
    }

    [Fact]
    public void GameplayMultiLevelFixtureRetainsEntitiesOnTheirOriginalLevel()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-multi-level-occupancy.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 40, 40, []));
        world.AddLevel(new AuthoritativeLevel(2, 40, 40, []));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingWorld: world,
            startingWorldItems: [new AuthoritativeWorldItem(
                fixture.InitialState.WorldItemEntityId,
                fixture.InitialState.LevelId,
                2,
                0,
                fixture.InitialState.WorldItemSeed,
                fixture.InitialState.WorldItemPrice,
                AuthoritativeItemState.Empty)],
            startingObjects: [new AuthoritativeWorldObject(
                fixture.InitialState.ObjectEntityId,
                fixture.InitialState.ObjectId,
                fixture.InitialState.LevelId,
                fixture.InitialState.ObjectPositionX,
                fixture.InitialState.ObjectPositionY)],
            startingPortals: [new AuthoritativePortal(5, 1, 0, 0, 2, 3, 4)]);

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));

        Assert.Equal(CommandStatus.Accepted, Assert.Single(result.Results).Status);
        Assert.Equal(2U, Assert.Single(result.FinalSnapshot.Players).LevelId);
        Assert.Equal(20U, Assert.Single(result.FinalSnapshot.WorldItems).EntityId);
        Assert.Equal(1U, Assert.Single(result.FinalSnapshot.WorldItems).LevelId);
        Assert.Equal(30U, Assert.Single(result.FinalSnapshot.Objects).EntityId);
        Assert.Equal(1U, Assert.Single(result.FinalSnapshot.Objects).LevelId);
    }

    [Fact]
    public void GameplayWorldItemFixtureExecutesPickupTransition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-world-item-pickup.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var item = new AuthoritativeWorldItem(
            fixture.InitialState.WorldItemEntityId,
            fixture.InitialState.LevelId,
            fixture.InitialState.PositionX + 1,
            fixture.InitialState.PositionY,
            fixture.InitialState.WorldItemSeed,
            fixture.InitialState.WorldItemPrice,
            AuthoritativeItemState.Empty);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            startingWorldItems: [item]);

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        Assert.Equal([CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Empty(result.FinalSnapshot.WorldItems);
        Assert.Equal(fixture.InitialState.WorldItemSeed, Assert.Single(result.FinalSnapshot.Players[0].Inventory).ItemSeed);
        Assert.Equal(fixture.Checkpoints[0].StateSha256, result.Checkpoints[0].ActualStateSha256);
    }

    [Fact]
    public void GameplayObjectQuestFixtureExecutesAuthoritativeWorldTransitions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-object-quest.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingPositionX: fixture.InitialState.PositionX,
            startingPositionY: fixture.InitialState.PositionY,
            startingLevelId: fixture.InitialState.LevelId,
            startingObjects: [new AuthoritativeWorldObject(
                fixture.InitialState.ObjectEntityId,
                fixture.InitialState.ObjectId,
                fixture.InitialState.LevelId,
                fixture.InitialState.ObjectPositionX,
                fixture.InitialState.ObjectPositionY)],
            startingQuests: [new AuthoritativeQuestState(
                fixture.InitialState.QuestId,
                fixture.InitialState.LevelId,
                fixture.InitialState.QuestRequiredProgress)]);

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.True(Assert.Single(result.FinalSnapshot.Objects).Activated);
        var quest = Assert.Single(result.FinalSnapshot.Quests);
        Assert.Equal(1U, quest.Progress);
        Assert.True(quest.Completed);
    }

    [Fact]
    public void GameplayStatusExpiryFixtureAdvancesEffectsByAuthoritativeTick()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "gameplay-status-expiry.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(path));
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(
                2,
                3,
                0,
                fixture.InitialState.StatusEffectId,
                fixture.InitialState.StatusDuration,
                fixture.InitialState.StatusMagnitude),
        ]);
        var executor = new StoreSimulationExecutor(
            new StoreCatalog(),
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum,
            startingLifeMaximum: fixture.InitialState.Life,
            startingSpells: spells);

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));
        var player = Assert.Single(result.FinalSnapshot.Players);

        Assert.Equal([CommandStatus.Accepted, CommandStatus.Accepted], result.Results.Select(command => command.Status));
        Assert.Equal(2, result.Checkpoints.Count);
        Assert.Empty(player.StatusEffects);
    }

    [Fact]
    public void StructuredContentManifestLoadsAndRetainsEveryCheckpoint()
    {
        var fixture = ReplayFixtureLoader.Load(CreateStructuredFixture(
            """
            [
              { "tick": 1, "state_sha256": "legacy-open" },
              { "tick": 2, "state_sha256": "legacy-buy" }
            ]
            """));

        var contentManifestProperty = fixture.GetType().GetProperty("ContentManifest");
        Assert.NotNull(contentManifestProperty);
        var contentManifest = contentManifestProperty.GetValue(fixture);
        Assert.NotNull(contentManifest);
        Assert.Equal("test-content", contentManifest.GetType().GetProperty("Id")?.GetValue(contentManifest));
        Assert.Equal("1", contentManifest.GetType().GetProperty("Version")?.GetValue(contentManifest));
        Assert.Equal("a4f2d11cb54e18c5df07eae670a7ab9ab739f5efbd83dc84d5fcefe956315e97", contentManifest.GetType().GetProperty("Sha256")?.GetValue(contentManifest));
        Assert.Equal([1UL, 2UL], fixture.Checkpoints.Select(checkpoint => checkpoint.Tick));
        Assert.Equal(["legacy-open", "legacy-buy"], fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256));
    }

    [Fact]
    public void StructuredFixtureWithoutACheckpointForEachCommandTickIsRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReplayFixtureLoader.Load(CreateStructuredFixture(
            """
            [
              { "tick": 1, "state_sha256": "legacy-open" }
            ]
            """)));

        Assert.Equal("Replay fixture must include a checkpoint at command tick 2.", exception.Message);
    }

    [Fact]
    public void FinalSnapshotHashMismatchIsRejected()
    {
        var fixture = ReplayFixtureLoader.Load(CreateStructuredFixture(
            """
            [
              { "tick": 1, "state_sha256": "legacy-open" },
              { "tick": 2, "state_sha256": "legacy-buy" }
            ]
            """,
            "not-the-final-snapshot-hash"));
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 75)]);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana);
        var commandServer = new AuthoritativeCommandServer(executor);

        var exception = Assert.Throws<InvalidDataException>(() => ReplayFixtureExecutor.Execute(fixture, executor, commandServer));

        Assert.Equal("Replay final snapshot hash mismatch.", exception.Message);
    }

    [Fact]
    public void MalformedFixtureIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ReplayFixtureLoader.Load("{\"format_version\":1}"));
    }

    private static ReplayFixture LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic-buy.json");
        return ReplayFixtureLoader.Load(File.ReadAllText(path));
    }

    private static string CreateStructuredFixture(string checkpoints, string? finalStateSha256 = null)
    {
        var finalState = finalStateSha256 is null
            ? string.Empty
            : $",\n  \"final_state_sha256\": \"{finalStateSha256}\"";
        return $$"""
        {
          "format_version": 1,
          "fixture_id": "stores/structured-buy",
          "protocol_schema_version": "0.1.0",
          "content_manifest": {
            "id": "test-content",
            "version": "1",
            "sha256": "a4f2d11cb54e18c5df07eae670a7ab9ab739f5efbd83dc84d5fcefe956315e97"
          },
          "tick_rate_hz": 20,
          "rng_seed": 1,
          "initial_state": {
            "player": "A",
            "gold": 100,
            "experience": 200,
            "life": 640
          },
          "commands": [
            {
              "client_sequence": 1,
              "target_tick": 1,
              "server_receipt_sequence": 1,
              "kind": "OpenStore"
            },
            {
              "client_sequence": 2,
              "target_tick": 2,
              "server_receipt_sequence": 2,
              "kind": "BuyItem",
              "payload": { "store_id": 1, "store_slot": 0 }
            }
          ],
          "checkpoints": {{checkpoints}}{{finalState}}
        }
        """;
    }
}
