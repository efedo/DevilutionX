using Devilution.Server.Commands;
using Devilution.Server.Replay;
using Devilution.Server.Stores;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Server.Tests;

/** Executes a replay against the checked-in base store catalog. */
public sealed class BaseContentReplayTests
{
    [Fact]
    public void BaseStorePurchaseAndSaleUseTheCheckedInCatalog()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "base-content-purchase.json");
        var fixture = ReplayFixtureLoader.Load(File.ReadAllText(fixturePath));
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var items = AuthoritativeItemCatalog.LoadTsv("items.tsv", File.ReadAllText(Path.Combine(contentRoot, "items.tsv")));
        var catalog = StoreCatalog.LoadTsv("stores.tsv", File.ReadAllText(Path.Combine(contentRoot, "stores.tsv")), items);
        var executor = new StoreSimulationExecutor(
            catalog,
            fixture.InitialState.Gold,
            fixture.InitialState.Experience,
            fixture.InitialState.Life,
            fixture.InitialState.Mana,
            startingManaMaximum: fixture.InitialState.ManaMaximum);

        var result = ReplayFixtureExecutor.Execute(fixture, executor, new AuthoritativeCommandServer(executor));

        Assert.All(result.Results, command => Assert.Equal(CommandStatus.Accepted, command.Status));
        Assert.Equal(43U, Assert.Single(result.FinalSnapshot.Players).Gold);
        Assert.Equal(
            fixture.Checkpoints.Select(checkpoint => checkpoint.StateSha256),
            result.Checkpoints.Select(checkpoint => checkpoint.ActualStateSha256));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
