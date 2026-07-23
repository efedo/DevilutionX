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
        registry.Load([DiabloGameplayModule.Instance], new GameplayModuleContext(contentManifest, clock));
        var ruleset = new GameplayRulesetIdentity(contentManifest, registry.Modules.Select(module => module.Identity));

        var storeTablePath = Path.Combine(contentRoot, "stores.tsv");
        var catalog = StoreCatalog.LoadTsv(storeTablePath, await File.ReadAllTextAsync(storeTablePath));
        var executor = new StoreSimulationExecutor(catalog, options.StartingGold, gameplayRules: registry.StoreRules);
        var commandServer = new AuthoritativeCommandServer(executor);
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
