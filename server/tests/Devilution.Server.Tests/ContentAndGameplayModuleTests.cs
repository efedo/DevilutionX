using Devilution.Protocol.V1;
using Devilution.Server.Content;
using Devilution.Server.Gameplay;
using Devilution.Server.Protocol;
using Devilution.Server.Simulation;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ContentAndGameplayModuleTests
{
    [Fact]
    public void TsvLoaderPreservesRowsAndRejectsRaggedRows()
    {
        var table = TsvTable.Parse("items.tsv", "\uFEFFid\tvalue\n# comment\na\t42\r\nb\t7\n");

        Assert.Equal(["id", "value"], table.Columns);
        Assert.Equal(["a", "b"], table.Rows.Select(row => row.Required("id")));
        Assert.Equal(42, table.Rows[0].RequiredInt32("value"));
        Assert.Throws<InvalidDataException>(() => TsvTable.Parse("bad.tsv", "id\tvalue\na\n"));
    }

    [Fact]
    public void ContentManifestHashMatchesNativeGoldenVectors()
    {
        var items = TsvTable.Parse("items.tsv", "id\tname\nshort-sword\tShort Sword\nbuckler\tBuckler\n");
        var monsters = TsvTable.Parse("monsters.tsv", "id\tname\ngoat\tGoatman\n");
        var baseline = new ContentManifest("baseline", "1", [new ContentPack("core", "1", [items]), new ContentPack("expansion", "2", [monsters])]);
        var reordered = new ContentManifest("baseline", "1", [new ContentPack("expansion", "2", [monsters]), new ContentPack("core", "1", [items])]);
        var changedItems = TsvTable.Parse("items.tsv", "id\tname\nshort-sword\tShort Sword\nbuckler\tTower Shield\n");
        var changed = new ContentManifest("baseline", "1", [new ContentPack("core", "1", [changedItems]), new ContentPack("expansion", "2", [monsters])]);

        Assert.Equal("40d13766d2726d36216120e9cd36e064c72fee0395a46d23abcf6d316c9ff34c", baseline.Sha256);
        Assert.Equal("5c536ffd249de81cefd8d74bcf35b4738f129d950a89e339f521c0f1994ee35f", reordered.Sha256);
        Assert.Equal("5d12a34dddd0fdaa81382946736e6637afc5e319e5228a89571144da2a24cf59", changed.Sha256);
    }

    [Fact]
    public void ContentPackDirectoryLoaderUsesStableRelativeTableOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devilution-content-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        try {
            File.WriteAllText(Path.Combine(root, "z.tsv"), "id\nlast\n");
            File.WriteAllText(Path.Combine(root, "nested", "a.tsv"), "id\nfirst\n");

            var pack = ContentPackLoader.LoadDirectory("base", "1", root);

            Assert.Equal(["nested/a.tsv", "z.tsv"], pack.Tables.Select(table => table.SourcePath));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RulesetIdentityIsUsedAsTheHandshakeContentIdentity()
    {
        var content = new ContentManifest("base", "1", [new ContentPack("base", "1", [TsvTable.Parse("items.tsv", "id\n1\n")])]);
        var module = DiabloGameplayModule.Instance;
        var ruleset = new GameplayRulesetIdentity(content, [module.Identity]);

        var identity = ProtocolServerIdentity.FromRuleset("server", "0.1.0", 20, ruleset);

        Assert.Equal(ruleset.CombinedSha256, identity.ContentManifestHash);
    }

    [Fact]
    public void GameplayModulesLoadDependenciesInDeclaredOrder()
    {
        var content = new ContentManifest("base", "1", [new ContentPack("base", "1", [TsvTable.Parse("empty.tsv", "id\nvalue\n")])]);
        var clock = new DelegateAuthoritativeClock(() => 0);
        var registry = new GameplayModuleRegistry();
        var dependency = new TestGameplayModule(new GameplayModuleIdentity("base.rules", "1", "hash-a"));
        var dependent = new TestGameplayModule(new GameplayModuleIdentity("diablo.rules", "1", "hash-b", ["base.rules"]));

        registry.Load([dependent, dependency], new GameplayModuleContext(content, clock));

        Assert.Equal(["base.rules", "diablo.rules"], registry.Modules.Select(module => module.Identity.Id));
        Assert.Equal(["base.rules", "diablo.rules"], dependent.RegistrationOrder);
    }

    [Fact]
    public void MissingGameplayModuleDependencyIsRejected()
    {
        var content = new ContentManifest("base", "1", [new ContentPack("base", "1", [TsvTable.Parse("empty.tsv", "id\nvalue\n")])]);
        var registry = new GameplayModuleRegistry();
        var module = new TestGameplayModule(new GameplayModuleIdentity("diablo.rules", "1", "hash", ["missing"]));

        Assert.Throws<InvalidOperationException>(() => registry.Load([module], new GameplayModuleContext(content, new DelegateAuthoritativeClock(() => 0))));
    }

    [Fact]
    public void StoreCatalogLoadsExternalDefinitionsAndDiabloModuleOwnsPurchaseValidation()
    {
        var catalog = StoreCatalog.LoadTsv(
            "stores.tsv",
            "store_id\tstore_slot\titem_seed\tprice\titem_type\tidentified\n1\t0\t42\t75\t1\t1\n");
        var executor = new StoreSimulationExecutor(catalog, 100, gameplayRules: DiabloGameplayModule.Instance);
        var server = new Commands.AuthoritativeCommandServer(executor);

        Assert.Equal(CommandStatus.Accepted, server.Process("player", new Command {
            ClientSequence = 1,
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, currentTick: 0).Status);
        var purchase = server.Process("player", new Command {
            ClientSequence = 2,
            RequestedTick = 1,
            PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
        }, currentTick: 1);

        Assert.Equal(CommandStatus.Accepted, purchase.Status);
        var item = Assert.Single(executor.GetPlayerState("player").Inventory);
        Assert.Equal(1, item.State.ItemType);
        Assert.True(item.State.Identified);
    }

    private sealed class TestGameplayModule(GameplayModuleIdentity identity) : IGameplayModule
    {
        public GameplayModuleIdentity Identity { get; } = identity;

        public List<string> RegistrationOrder { get; } = [];

        public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
        {
            RegistrationOrder.AddRange(registry.Modules.Select(module => module.Identity.Id));
            RegistrationOrder.Add(Identity.Id);
        }
    }
}
