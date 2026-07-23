using Devilution.Protocol.V1;
using Devilution.Server.Commands;
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
            fixture.InitialState.Mana);
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
