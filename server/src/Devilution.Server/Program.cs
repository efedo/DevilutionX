using System.Net;
using Devilution.Server.Commands;
using Devilution.Server.Content;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Simulation;
using Devilution.Server.Stores;

namespace Devilution.Server;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (!ServerHostOptions.TryParse(args, out var options, out var error)) {
            if (!string.IsNullOrEmpty(error))
                Console.Error.WriteLine(error);
            Console.WriteLine(ServerHostOptions.Usage);
            return string.IsNullOrEmpty(error) ? 0 : 2;
        }

        try {
            return await RunAsync(options);
        } catch (Exception exception) {
            Console.Error.WriteLine($"Server startup failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunAsync(ServerHostOptions options)
    {
        var contentRoot = Path.GetFullPath(options.ContentRoot);
        var contentPack = ContentPackLoader.LoadDirectory(options.ContentId, options.ContentVersion, contentRoot);
        var contentManifest = new ContentManifest(options.ContentId, options.ContentVersion, [contentPack]);
        var registry = new GameplayModuleRegistry();
        var clock = new RealtimeAuthoritativeClock(options.TickRateHz);
        var pricingTablePath = Path.Combine(contentRoot, "store_services.tsv");
        var pricing = File.Exists(pricingTablePath)
            ? StoreServicePricing.LoadTsv(pricingTablePath, await File.ReadAllTextAsync(pricingTablePath))
            : StoreServicePricing.Default;
        var generationTablePath = Path.Combine(contentRoot, "item_generation.tsv");
        var generationRules = File.Exists(generationTablePath)
            ? AuthoritativeItemGenerationRules.LoadTsv(generationTablePath, await File.ReadAllTextAsync(generationTablePath))
            : AuthoritativeItemGenerationRules.Default;
        registry.Load([new DiabloGameplayModule(pricing)], new GameplayModuleContext(contentManifest, clock));
        var ruleset = new GameplayRulesetIdentity(contentManifest, registry.Modules.Select(module => module.Identity));

        var storeTablePath = Path.Combine(contentRoot, "stores.tsv");
        var itemTablePath = Path.Combine(contentRoot, "items.tsv");
        var legacyPrefixPath = Path.Combine(contentRoot, "item_prefixes.tsv");
        var legacySuffixPath = Path.Combine(contentRoot, "item_suffixes.tsv");
        var affixTablePath = Path.Combine(contentRoot, "item_affixes.tsv");
        var affixes = File.Exists(legacyPrefixPath) && File.Exists(legacySuffixPath)
            ? AuthoritativeItemAffixCatalog.LoadLegacyTsv(
                legacyPrefixPath,
                await File.ReadAllTextAsync(legacyPrefixPath),
                legacySuffixPath,
                await File.ReadAllTextAsync(legacySuffixPath),
                generationRules)
            : File.Exists(affixTablePath)
                ? AuthoritativeItemAffixCatalog.LoadTsv(affixTablePath, await File.ReadAllTextAsync(affixTablePath), generationRules)
                : null;
        var itemCatalog = File.Exists(itemTablePath)
            ? AuthoritativeItemCatalog.LoadTsv(itemTablePath, await File.ReadAllTextAsync(itemTablePath), affixes)
            : null;
        // The normalized catalog owns stable IDs used by stores, monsters, and
        // protocol snapshots. Legacy itemdat remains a compatibility fallback;
        // loading it here would replace those IDs with row-order IDs.
        var legacyItemTablePath = Path.Combine(contentRoot, "itemdat.tsv");
        if (itemCatalog is null && File.Exists(legacyItemTablePath))
            itemCatalog = AuthoritativeItemCatalog.LoadLegacyTsv(legacyItemTablePath, await File.ReadAllTextAsync(legacyItemTablePath), affixes);
        var uniqueTablePath = Path.Combine(contentRoot, "unique_items.tsv");
        var legacyUniqueTablePath = Path.Combine(contentRoot, "unique_itemdat.tsv");
        var uniqueItems = File.Exists(uniqueTablePath)
                ? AuthoritativeUniqueItemCatalog.LoadTsv(uniqueTablePath, await File.ReadAllTextAsync(uniqueTablePath))
                : itemCatalog is not null && File.Exists(legacyUniqueTablePath)
                    ? AuthoritativeUniqueItemCatalog.LoadLegacyTsv(legacyUniqueTablePath, await File.ReadAllTextAsync(legacyUniqueTablePath), itemCatalog)
                : null;
        if (itemCatalog is not null && uniqueItems is not null)
            itemCatalog.AttachUniqueCatalog(uniqueItems);
        var catalog = StoreCatalog.LoadTsv(storeTablePath, await File.ReadAllTextAsync(storeTablePath), itemCatalog, uniqueItems);
        var stableIdsTablePath = Path.Combine(contentRoot, "content_ids.tsv");
        var stableIds = File.Exists(stableIdsTablePath)
            ? StableContentIdCatalog.LoadTsv(stableIdsTablePath, await File.ReadAllTextAsync(stableIdsTablePath))
            : null;
        var levelsTablePath = Path.Combine(contentRoot, "levels.tsv");
        var world = File.Exists(levelsTablePath)
            ? AuthoritativeWorld.LoadTsv(levelsTablePath, await File.ReadAllTextAsync(levelsTablePath))
            : null;
        var spellsTablePath = Path.Combine(contentRoot, "spells.tsv");
        var spells = File.Exists(spellsTablePath)
            ? AuthoritativeSpellCatalog.LoadTsv(spellsTablePath, await File.ReadAllTextAsync(spellsTablePath))
            : null;
        var combatTablePath = Path.Combine(contentRoot, "combat.tsv");
        var combatRules = File.Exists(combatTablePath)
            ? AuthoritativeCombatRules.LoadTsv(combatTablePath, await File.ReadAllTextAsync(combatTablePath))
            : null;
        var monstersTablePath = Path.Combine(contentRoot, "monsters.tsv");
        var monsters = File.Exists(monstersTablePath)
            ? AuthoritativeMonsterCatalog.LoadTsv(monstersTablePath, await File.ReadAllTextAsync(monstersTablePath), itemCatalog, uniqueItems)
            : null;
        var portalsTablePath = Path.Combine(contentRoot, "portals.tsv");
        var portals = File.Exists(portalsTablePath)
            ? AuthoritativePortalCatalog.LoadTsv(portalsTablePath, await File.ReadAllTextAsync(portalsTablePath))
            : null;
        var objectsTablePath = Path.Combine(contentRoot, "objects.tsv");
        var objects = File.Exists(objectsTablePath)
            ? AuthoritativeWorldObjectCatalog.LoadTsv(objectsTablePath, await File.ReadAllTextAsync(objectsTablePath))
            : null;
        var questsTablePath = Path.Combine(contentRoot, "quests.tsv");
        var quests = File.Exists(questsTablePath)
            ? AuthoritativeQuestCatalog.LoadTsv(questsTablePath, await File.ReadAllTextAsync(questsTablePath))
            : null;
        var executor = new StoreSimulationExecutor(
            catalog,
            options.StartingGold,
            startingLife: options.StartingLife,
            startingMana: options.StartingMana,
            startingLevelId: world?.Levels.FirstOrDefault()?.LevelId
                ?? stableIds?.Resolve("level", "town")
                ?? 0,
            startingWorld: world,
            startingSpells: spells,
            startingCombatRules: combatRules,
            startingCombatTargets: monsters?.Targets,
            startingPortals: portals,
            startingObjects: objects,
            startingQuests: quests,
            startingInventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            gameplayRules: registry.StoreRules);
        var commandServer = new AuthoritativeCommandServer(executor);
        var saveStore = new AuthoritativeSaveStore(options.SaveRoot);
        var handshake = new ProtocolHandshake(ProtocolServerIdentity.FromRuleset(
            options.BuildId,
            options.ProtocolSchemaVersion,
            options.TickRateHz,
            ruleset));

        await using var server = new AuthoritativeTcpServer(
            commandServer,
            handshake,
            clock,
            options.Port,
            options.BindAddress,
            executor,
            executor,
            saveStore,
            executor);
        server.Start();
        Console.WriteLine($"Devilution authoritative server listening on {options.BindAddress}:{server.Port}");
        Console.WriteLine($"Content manifest: {contentManifest.Sha256}");
        Console.WriteLine($"Ruleset identity: {ruleset.CombinedSha256}");
        Console.WriteLine("Press Ctrl+C to stop.");

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        try {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        }
        return 0;
    }
}
