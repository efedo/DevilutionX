using Devilution.Server.Content;
using Devilution.Server.Gameplay;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Smoke-tests the checked-in base content through the same loaders used by the host. */
public sealed class ShippedContentTests
{
    [Fact]
    public void BaseContentManifestAndAuthoritativeCatalogsLoadDeterministically()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var firstManifest = new ContentManifest("base", "1", [ContentPackLoader.LoadDirectory("base", "1", contentRoot)]);
        var secondManifest = new ContentManifest("base", "1", [ContentPackLoader.LoadDirectory("base", "1", contentRoot)]);

        Assert.Equal(firstManifest.Sha256, secondManifest.Sha256);
        Assert.Equal(64, firstManifest.Sha256.Length);
        Assert.True(firstManifest.Packs.Single().Tables.Count >= 15);

        var items = AuthoritativeItemCatalog.LoadTsv("items.tsv", File.ReadAllText(Path.Combine(contentRoot, "items.tsv")));
        var stores = StoreCatalog.LoadTsv("stores.tsv", File.ReadAllText(Path.Combine(contentRoot, "stores.tsv")), items);
        var monsters = AuthoritativeMonsterCatalog.LoadTsv("monsters.tsv", File.ReadAllText(Path.Combine(contentRoot, "monsters.tsv")), items);
        var spells = AuthoritativeSpellCatalog.LoadTsv("spells.tsv", File.ReadAllText(Path.Combine(contentRoot, "spells.tsv")));

        Assert.Equal(2, stores.GetItems(1).Count);
        Assert.Equal(1, stores.GetItems(1)[0].State.ItemIndex);
        Assert.NotEmpty(monsters.Targets);
        Assert.True(spells.TryGet(3, out var damageSpell));
        Assert.Equal(8, damageSpell.DamageAmount);

        var legacyItems = AuthoritativeItemCatalog.LoadLegacyTsv("itemdat.tsv", File.ReadAllText(Path.Combine(contentRoot, "itemdat.tsv")));
        Assert.NotEqual(0U, legacyItems.ResolveLegacySymbol("IDI_WARRIOR"));
        Assert.Equal(6, legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_WARRIOR"), 0x12345678).MaxDamage);
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
