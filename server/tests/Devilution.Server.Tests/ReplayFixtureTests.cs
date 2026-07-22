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
    public void MalformedFixtureIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ReplayFixtureLoader.Load("{\"format_version\":1}"));
    }

    private static ReplayFixture LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic-buy.json");
        return ReplayFixtureLoader.Load(File.ReadAllText(path));
    }
}
