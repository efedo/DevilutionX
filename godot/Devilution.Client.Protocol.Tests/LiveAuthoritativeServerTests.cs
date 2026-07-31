using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeServerTests
{
    [Fact]
    public async Task GodotTransportCompletesLiveMoveAndCastSlice()
    {
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            "godot-live-test",
            "0.1.0",
            server.ContentHash,
            server.RulesetHash) {
            MinimumRetryTimeout = TimeSpan.FromMilliseconds(50),
            MaximumRetryTimeout = TimeSpan.FromMilliseconds(500),
        });
        var model = new AuthoritativeClientModel();

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
        model.Apply(initial);
        var player = model.Snapshot!.Players.Single();
        var monster = model.Snapshot.Monsters.Single(monster => monster.Alive);

        var moveSequence = client.Queue(new Command {
            MoveRequested = new MoveRequested { DirectionX = 0, DirectionY = 1 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var moveAcknowledgement = await WaitForAcknowledgementAsync(client, moveSequence);
        Assert.True(moveAcknowledgement.Status == CommandStatus.Accepted,
            $"Move was {moveAcknowledgement.Status}/{moveAcknowledgement.RejectReason} at tick {moveAcknowledgement.AppliedTick}; player was ({player.PositionX},{player.PositionY}) level {player.LevelId}.");
        var moved = await WaitForMessageAsync(client, message => message.Snapshot?.Players.Single().PositionY == player.PositionY + 1);
        model.Apply(moved);

        var castSequence = client.Queue(new Command {
            CastRequested = new CastRequested { SpellId = 4, TargetEntityId = monster.EntityId },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var castAcknowledgement = await WaitForAcknowledgementAsync(client, castSequence);
        Assert.True(castAcknowledgement.Status == CommandStatus.Accepted,
            $"Cast was {castAcknowledgement.Status}/{castAcknowledgement.RejectReason} at tick {castAcknowledgement.AppliedTick}; player was ({model.Snapshot!.Players.Single().PositionX},{model.Snapshot.Players.Single().PositionY}), target was ({monster.PositionX},{monster.PositionY}).");
    }

    [Fact]
    public async Task LiveBaseStoreUsesNormalizedShippedItemDefinitions()
    {
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken);
        await using var client = new AuthoritativeClient(new ClientConnectionOptions(
            "127.0.0.1",
            server.Port,
            "godot-store-parity-test",
            "0.1.0",
            server.ContentHash,
            server.RulesetHash));

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var initial = await WaitForMessageAsync(client, message => message.Snapshot is not null);
        var model = new AuthoritativeClientModel();
        model.Apply(initial);
        var sequence = client.Queue(new Command {
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        }, client.SuggestedCommandTick(model.CurrentTick));
        await client.PollAsync(TestContext.Current.CancellationToken);
        var acknowledgement = await WaitForAcknowledgementAsync(client, sequence);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.Status);
        var message = await WaitForMessageAsync(client, message => message.Snapshot?.ActiveStore?.StoreId == 1);
        var stock = message.Snapshot!.ActiveStore!.Items;

        Assert.Equal(2, stock.Count);
        Assert.Equal(1, stock[0].State.ItemIndex);
        Assert.Equal(4, stock[0].State.MinDamage);
        Assert.Equal(8, stock[0].State.MaxDamage);
        Assert.Equal(2, stock[1].State.ItemIndex);
        Assert.Equal(5, stock[1].State.ArmorClass);
    }

    private static async Task<AuthoritativeClientMessage> WaitForMessageAsync(
        AuthoritativeClient client,
        Func<AuthoritativeClientMessage, bool> predicate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        for (var attempt = 0; attempt < 100; attempt++) {
            foreach (var message in client.DrainMessages()) {
                if (predicate(message))
                    return message;
            }
            await client.PollAsync(cancellationToken);
            await Task.Delay(25, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for a live authoritative client message.");
    }

    private static async Task<CommandResult> WaitForAcknowledgementAsync(AuthoritativeClient client, ulong sequence)
    {
        var message = await WaitForMessageAsync(client, message =>
            message.Acknowledgement?.Results.Any(result => result.ClientSequence == sequence) == true);
        return message.Acknowledgement!.Results.Single(result => result.ClientSequence == sequence);
    }

    private sealed class StartedServer : IDisposable
    {
        private readonly Process process;
        private readonly string saveRoot;

        private StartedServer(Process process, string saveRoot, int port, string contentHash, string rulesetHash)
        {
            this.process = process;
            this.saveRoot = saveRoot;
            Port = port;
            ContentHash = contentHash;
            RulesetHash = rulesetHash;
        }

        public int Port { get; }

        public string ContentHash { get; }

        public string RulesetHash { get; }

        public static async Task<StartedServer> StartAsync(CancellationToken cancellationToken)
        {
            var repositoryRoot = FindRepositoryRoot();
            var saveRoot = Path.Combine(Path.GetTempPath(), "DevilutionY-Godot-Test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveRoot);
            var startInfo = new ProcessStartInfo {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "server/src/Devilution.Server/Devilution.Server.csproj"));
            startInfo.ArgumentList.Add("--no-build");
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("--save-root");
            startInfo.ArgumentList.Add(saveRoot);
            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the authoritative server.");
            _ = process.StandardError.ReadToEndAsync(cancellationToken);
            int? port = null;
            string? contentHash = null;
            string? rulesetHash = null;
            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line) {
                if (line.Contains("listening on", StringComparison.Ordinal)
                    && int.TryParse(line[(line.LastIndexOf(':') + 1)..], out var parsedPort))
                    port = parsedPort;
                if (line.StartsWith("Content manifest: ", StringComparison.Ordinal))
                    contentHash = line["Content manifest: ".Length..];
                if (line.StartsWith("Ruleset identity: ", StringComparison.Ordinal))
                    rulesetHash = line["Ruleset identity: ".Length..];
                if (port.HasValue && contentHash is not null && rulesetHash is not null)
                    return new StartedServer(process, saveRoot, port.Value, rulesetHash, rulesetHash);
            }
            process.Dispose();
            Directory.Delete(saveRoot, true);
            throw new InvalidOperationException("The authoritative server exited before publishing its identity.");
        }

        public void Dispose()
        {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
            process.Dispose();
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, true);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null) {
                if (File.Exists(Path.Combine(directory.FullName, "protocol/devilution.proto")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the DevilutionX repository root.");
        }
    }
}
