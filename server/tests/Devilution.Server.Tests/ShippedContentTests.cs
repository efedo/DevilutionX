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
        var firstStoreItem = stores.GetItems(1)[0];
        Assert.Equal(42U, firstStoreItem.ItemSeed);
        Assert.Equal(1, firstStoreItem.State.ItemIndex);
        Assert.Equal(4, firstStoreItem.State.MinDamage);
        Assert.Equal(8, firstStoreItem.State.MaxDamage);
        Assert.True(firstStoreItem.State.Identified);
        var secondStoreItem = stores.GetItems(1)[1];
        Assert.Equal(43U, secondStoreItem.ItemSeed);
        Assert.Equal(2, secondStoreItem.State.ItemIndex);
        Assert.Equal(5, secondStoreItem.State.ArmorClass);
        Assert.NotEmpty(monsters.Targets);
        Assert.True(spells.TryGet(3, out var damageSpell));
        Assert.Equal(8, damageSpell.DamageAmount);

        var legacyItems = AuthoritativeItemCatalog.LoadLegacyTsv("itemdat.tsv", File.ReadAllText(Path.Combine(contentRoot, "itemdat.tsv")));
        Assert.NotEqual(0U, legacyItems.ResolveLegacySymbol("IDI_WARRIOR"));
        Assert.Equal(6, legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_WARRIOR"), 0x12345678).MaxDamage);
    }

    [Fact]
    public void BaseContentGenerationAndServicePricingCoverTheShippedSources()
    {
        var contentRoot = Path.Combine(FindRepositoryRoot(), "server", "content", "base");
        var rules = AuthoritativeItemGenerationRules.LoadTsv(
            "item_generation.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_generation.tsv")));
        var affixes = AuthoritativeItemAffixCatalog.LoadLegacyTsv(
            "item_prefixes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_prefixes.tsv")),
            "item_suffixes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "item_suffixes.tsv")),
            rules);
        var items = AuthoritativeItemCatalog.LoadTsv(
            "items.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "items.tsv")),
            affixes);
        var uniques = AuthoritativeUniqueItemCatalog.LoadTsv(
            "unique_items.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "unique_items.tsv")));
        items.AttachUniqueCatalog(uniques);

        var weapon = items.Generate(1, 42, 10);
        Assert.Equal(weapon, items.Generate(1, 42, 10));
        Assert.Equal(1, weapon.ItemIndex);
        Assert.Equal(4, weapon.MinDamage);
        Assert.Equal(8, weapon.MaxDamage);
        var unique = items.GenerateUnique(1001, 42, 1);
        Assert.Equal(1001, unique.UniqueId);
        Assert.Equal(3650, unique.Value);
        Assert.Equal(10, unique.PlusToHit);
        Assert.Equal(1, unique.FireMinDamage);
        Assert.Equal(10, unique.FireMaxDamage);

        var legacyItems = AuthoritativeItemCatalog.LoadLegacyTsv(
            "itemdat.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "itemdat.tsv")));
        var warrior = legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_WARRIOR"), 0x12345678);
        var cleaver = legacyItems.Generate(legacyItems.ResolveLegacySymbol("IDI_CLEAVER"), 0x12345678);
        Assert.Equal(6, warrior.MaxDamage);
        Assert.Equal(24, cleaver.MaxDamage);
        Assert.Equal(2, cleaver.ItemType);

        var pricing = StoreServicePricing.LoadTsv(
            "store_services.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "store_services.tsv")));
        Assert.Equal(4, pricing.SaleDivisor);
        Assert.Equal(2, pricing.NormalRepairDivisor);
        Assert.Equal(30, pricing.MagicalRepairPercent);
        Assert.Equal(100U, pricing.IdentificationPrice);
        Assert.Equal(64, pricing.ManaChunk);
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
