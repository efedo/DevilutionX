using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
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
