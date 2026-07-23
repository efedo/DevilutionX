using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
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
        catalog.AddStore(1, new[] {
            new StoreItem(0, 42, 75, AuthoritativeItemState.Empty with { Identified = true, ItemType = 1 }),
        });
        var executor = new StoreSimulationExecutor(
            catalog,
            startingGold: 100,
            startingExperience: 200,
            startingLife: 640,
            startingMana: 32,
            startingAttributes: new PlayerAttributesState(
                new PlayerAttributeState(10, 12),
                new PlayerAttributeState(8, 9),
                new PlayerAttributeState(15, 16),
                new PlayerAttributeState(20, 21)),
            startingEquipment: [new EquippedStoreItem(0, 77)],
            startingInventoryGrid: [0, -1, 2]);
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
        Assert.Equal(200U, initialPlayer.Experience);
        Assert.Equal(640, initialPlayer.Life);
        Assert.Equal(32, initialPlayer.Mana);
        Assert.Equal(0U, initialPlayer.ActiveStoreId);
        Assert.Equal(12, initialPlayer.Attributes.Strength.Current);
        Assert.Equal(77U, Assert.Single(initialPlayer.Equipment).ItemSeed);
        Assert.Equal(new[] { 0, -1, 2 }, initialPlayer.InventoryGrid);
        Assert.Empty(initialPlayer.Inventory);
        Assert.Equal(SnapshotStateHasher.Compute(initialSnapshot.Snapshot), initialSnapshot.Snapshot.StateSha256);

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
        var purchaseItem = Assert.Single(purchasePlayer.Inventory);
        Assert.Equal(42U, purchaseItem.ItemSeed);
        Assert.True(purchaseItem.State.Identified);
        Assert.Equal(1, purchaseItem.State.ItemType);
        Assert.Equal(SnapshotStateHasher.Compute(purchaseSnapshot.Snapshot), purchaseSnapshot.Snapshot.StateSha256);
    }

    [Fact]
    public async Task ConnectedSessionsReceiveDistinctEntityIds()
    {
        var executor = new StoreSimulationExecutor(new StoreCatalog(), startingGold: 100);
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10, snapshotProvider: executor);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var firstClient = new TcpClient();
        using var secondClient = new TcpClient();
        await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var firstStream = firstClient.GetStream();
        await using var secondStream = secondClient.GetStream();

        await SendHelloAsync(firstStream, cancellationToken);
        await SendHelloAsync(secondStream, cancellationToken);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.PayloadCase);
        Assert.Equal(Envelope.PayloadOneofCase.ServerHello, (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.PayloadCase);
        var firstSnapshot = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
        var secondSnapshot = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);

        var firstEntityId = Assert.Single(firstSnapshot!.Snapshot.Players).EntityId;
        var secondEntityId = Assert.Single(secondSnapshot!.Snapshot.Players).EntityId;
        Assert.NotEqual(firstEntityId, secondEntityId);
    }

    [Fact]
    public async Task ResumeTokenPreservesSessionLedgerAndEntityIdAcrossReconnect()
    {
        var executor = new RecordingExecutor();
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10, snapshotProvider: executor);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string resumeToken;
        uint firstEntityId;

        using (var firstClient = new TcpClient()) {
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            var firstStream = firstClient.GetStream();
            await SendHelloAsync(firstStream, cancellationToken);
            var firstHello = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.ServerHello;
            resumeToken = firstHello.SessionToken;
            Assert.NotEmpty(resumeToken);
            var firstSnapshot = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
            firstEntityId = Assert.Single(firstSnapshot!.Snapshot.Players).EntityId;

            await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                CommandBatch = new CommandBatch {
                    Commands = { new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        AttackRequested = new AttackRequested { TargetEntityId = 1 },
                    } },
                },
            }, cancellationToken);
            var acknowledgement = await EnvelopeCodec.ReadAsync(firstStream, cancellationToken);
            Assert.Equal(CommandStatus.Accepted, acknowledgement!.CommandAck.Results.Single().Status);
            firstClient.Dispose();
        }

        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var secondStream = secondClient.GetStream();
        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                ResumeToken = resumeToken,
            },
        }, cancellationToken);
        var secondHello = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.ServerHello;
        Assert.Equal(resumeToken, secondHello.SessionToken);
        var secondSnapshot = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);
        Assert.Equal(firstEntityId, Assert.Single(secondSnapshot!.Snapshot.Players).EntityId);

        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = { new Command {
                    ClientSequence = 1,
                    RequestedTick = 10,
                    AttackRequested = new AttackRequested { TargetEntityId = 1 },
                } },
            },
        }, cancellationToken);
        var duplicate = await EnvelopeCodec.ReadAsync(secondStream, cancellationToken);

        Assert.Equal(CommandStatus.Duplicate, duplicate!.CommandAck.Results.Single().Status);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task ResumeTokenRestoresPurchasedStoreStateAndDuplicatePurchaseDoesNotMutateIt()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, new[] {
            new StoreItem(0, 42, 75, AuthoritativeItemState.Empty with { Identified = true, ItemType = 1 }),
            new StoreItem(1, 43, 25, RemainingStockState()),
        });
        var executor = new StoreSimulationExecutor(catalog, startingGold: 100, startingInventoryGrid: [-1, -1]);
        var commandServer = new AuthoritativeCommandServer(executor);
        var handshake = new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
        await using var server = new AuthoritativeTcpServer(commandServer, handshake, () => 10, snapshotProvider: executor);
        server.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        string resumeToken;
        Snapshot purchasedSnapshot;

        using (var firstClient = new TcpClient()) {
            await firstClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
            await using var firstStream = firstClient.GetStream();
            await SendHelloAsync(firstStream, cancellationToken);
            resumeToken = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.ServerHello.SessionToken;
            Assert.NotEmpty(resumeToken);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.PayloadCase);

            await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                CommandBatch = new CommandBatch {
                    Commands = { new Command {
                        ClientSequence = 1,
                        RequestedTick = 10,
                        OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
                    } },
                },
            }, cancellationToken);
            Assert.Equal(CommandStatus.Accepted, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.CommandAck.Results.Single().Status);
            Assert.Equal(Envelope.PayloadOneofCase.Snapshot, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.PayloadCase);

            await EnvelopeCodec.WriteAsync(firstStream, new Envelope {
                CommandBatch = new CommandBatch {
                    Commands = { new Command {
                        ClientSequence = 2,
                        RequestedTick = 10,
                        PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
                    } },
                },
            }, cancellationToken);
            Assert.Equal(CommandStatus.Accepted, (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.CommandAck.Results.Single().Status);
            purchasedSnapshot = (await EnvelopeCodec.ReadAsync(firstStream, cancellationToken))!.Snapshot;
        }

        var purchasedPlayer = Assert.Single(purchasedSnapshot.Players);
        Assert.Equal(25U, purchasedPlayer.Gold);
        Assert.Equal(1U, purchasedPlayer.ActiveStoreId);
        Assert.Single(purchasedPlayer.Inventory);
        Assert.Equal(SnapshotStateHasher.Compute(purchasedSnapshot), purchasedSnapshot.StateSha256);
        AssertRemainingStoreStock(purchasedSnapshot);

        using var secondClient = new TcpClient();
        await secondClient.ConnectAsync(IPAddress.Loopback, server.Port, cancellationToken);
        await using var secondStream = secondClient.GetStream();
        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
                ResumeToken = resumeToken,
            },
        }, cancellationToken);
        Assert.Equal(resumeToken, (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.ServerHello.SessionToken);
        var resumedSnapshot = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.Snapshot;
        AssertMatchingStoreSnapshot(purchasedSnapshot, resumedSnapshot);

        await EnvelopeCodec.WriteAsync(secondStream, new Envelope {
            CommandBatch = new CommandBatch {
                Commands = { new Command {
                    ClientSequence = 2,
                    RequestedTick = 10,
                    PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
                } },
            },
        }, cancellationToken);
        Assert.Equal(CommandStatus.Duplicate, (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.CommandAck.Results.Single().Status);
        var duplicateSnapshot = (await EnvelopeCodec.ReadAsync(secondStream, cancellationToken))!.Snapshot;
        AssertMatchingStoreSnapshot(purchasedSnapshot, duplicateSnapshot);
    }

    private static void AssertMatchingStoreSnapshot(Snapshot expected, Snapshot actual)
    {
        Assert.Equal(expected.StateSha256, actual.StateSha256);
        Assert.Equal(SnapshotStateHasher.Compute(actual), actual.StateSha256);

        var expectedPlayer = Assert.Single(expected.Players);
        var actualPlayer = Assert.Single(actual.Players);
        Assert.Equal(expectedPlayer.EntityId, actualPlayer.EntityId);
        Assert.Equal(expectedPlayer.Gold, actualPlayer.Gold);
        Assert.Equal(expectedPlayer.ActiveStoreId, actualPlayer.ActiveStoreId);
        Assert.Equal(expectedPlayer.Inventory.Select(item => item.ItemSeed), actualPlayer.Inventory.Select(item => item.ItemSeed));
        AssertRemainingStoreStock(expected);
        AssertRemainingStoreStock(actual);
    }

    private static void AssertRemainingStoreStock(Snapshot snapshot)
    {
        var store = Assert.IsType<StoreSnapshot>(snapshot.ActiveStore);
        Assert.Equal(1U, store.StoreId);
        var item = Assert.Single(store.Items);
        Assert.Equal(1U, item.StoreSlot);
        Assert.Equal(43U, item.ItemSeed);
        Assert.Equal(25U, item.Price);
        Assert.Equal(new ItemStateSnapshot {
            CreateInfo = 43,
            ItemType = 2,
            PositionX = 3,
            PositionY = -4,
            Identified = true,
            Value = 50,
            MaxDamage = 7,
            Flags = 9,
            ItemIndex = 8,
            Durability = 2,
            MaxDurability = 10,
            PlusDamage = 11,
            PrefixPower = 12,
            SuffixPower = 13,
            Buff = 14,
        }, item.State);
    }

    private static AuthoritativeItemState RemainingStockState()
    {
        return AuthoritativeItemState.Empty with {
            CreateInfo = 43,
            ItemType = 2,
            PositionX = 3,
            PositionY = -4,
            Identified = true,
            Value = 50,
            MaxDamage = 7,
            Flags = 9,
            ItemIndex = 8,
            Durability = 2,
            MaxDurability = 10,
            PlusDamage = 11,
            PrefixPower = 12,
            SuffixPower = 13,
            Buff = 14,
        };
    }

    private static ValueTask SendHelloAsync(Stream stream, CancellationToken cancellationToken)
    {
        return EnvelopeCodec.WriteAsync(stream, new Envelope {
            ClientHello = new ClientHello {
                ClientBuildId = "client-build",
                ProtocolSchemaVersion = "0.1.0",
                ContentManifestHash = "content-hash",
            },
        }, cancellationToken);
    }

    private sealed class RecordingExecutor : IAuthoritativeCommandExecutor, IAuthoritativeSnapshotProvider
    {
        public int CallCount { get; private set; }

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            CallCount++;
            return CommandExecutionResult.Accepted;
        }

        public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
        {
            return new Snapshot {
                Tick = tick,
                Players = { new PlayerSnapshot { EntityId = entityId } },
            };
        }
    }
}
