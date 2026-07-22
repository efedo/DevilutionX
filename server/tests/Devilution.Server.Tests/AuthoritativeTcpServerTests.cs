using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeTcpServerTests
{
    [Fact]
    public async Task HandshakeAndCommandBatchRoundTripOverLoopback()
    {
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10);
        server.Start();

        using var client = new TcpClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
            },
        }, cancellationToken);

        var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(hello);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);

        var command = new Command {
            ClientSequence = 1,
            RequestedTick = 10,
            AttackRequested = new AttackRequested { TargetEntityId = 1 },
        };
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch { Commands = { command } },
        }, cancellationToken);

        var acknowledgement = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(acknowledgement);
        Assert.Equal(Envelope.PayloadOneofCase.CommandAck, acknowledgement!.PayloadCase);
        Assert.Equal(CommandStatus.Accepted, acknowledgement.CommandAck.Results.Single().Status);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch { Commands = { command } },
        }, cancellationToken);
        var duplicateAcknowledgement = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(duplicateAcknowledgement);
        Assert.Equal(CommandStatus.Duplicate, duplicateAcknowledgement!.CommandAck.Results.Single().Status);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task StoreSnapshotsAreSentAfterHandshakeAndCommands()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, new[] { new StoreItem(0, 42, 75) });
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100);
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10, snapshotProvider: executor);
        server.Start();

        using var client = new TcpClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        await client.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var stream = client.GetStream();
        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
            },
        }, cancellationToken);

        var hello = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(hello);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, hello!.PayloadCase);

        var initialSnapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(initialSnapshot);
        Assert.Equal(Envelope.PayloadOneofCase.Snapshot, initialSnapshot!.PayloadCase);
        var initialPlayer = Assert.Single(initialSnapshot.Snapshot.Players);
        Assert.Equal(100U, initialPlayer.Gold);
        Assert.Equal(0U, initialPlayer.ActiveStoreId);
        Assert.Empty(initialPlayer.Inventory);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
                    },
                },
            },
        }, cancellationToken);
        var openAck = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(openAck);
        Assert.Equal(CommandStatus.Accepted, openAck!.CommandAck.Results.Single().Status);
        var openSnapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(openSnapshot);
        Assert.Equal(1U, Assert.Single(openSnapshot!.Snapshot.Players).ActiveStoreId);

        await EnvelopeCodec.WriteAsync(stream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = {
                    new Command {
                        ClientSequence = 2,
                        RequestedTick = 10,
                        PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
                    },
                },
            },
        }, cancellationToken);
        var purchaseAck = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(purchaseAck);
        Assert.Equal(CommandStatus.Accepted, purchaseAck!.CommandAck.Results.Single().Status);
        var purchaseSnapshot = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
        Assert.NotNull(purchaseSnapshot);
        var purchasePlayer = Assert.Single(purchaseSnapshot!.Snapshot.Players);
        Assert.Equal(25U, purchasePlayer.Gold);
        Assert.Equal(42U, Assert.Single(purchasePlayer.Inventory).ItemSeed);
    }

    private sealed class RecordingExecutor : IAuthoritativeCommandExecutor
    {
        public int CallCount { get; private set; }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            CallCount++;
            return CommandExecutionResult.Accepted;
        }
    }
}
