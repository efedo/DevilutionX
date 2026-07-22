using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Devilution.Server.Simulation;

namespace Devilution.Server.Host;

/**
 * TCP session host for length-delimited Protobuf envelopes.
 *
 * Each connection is a session for the current slice. A future reconnect
 * token can preserve a session's command ledger without changing the wire
 * command contract.
 */
public sealed class AuthoritativeTcpServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly AuthoritativeCommandServer commandServer;
    private readonly ProtocolHandshake handshake;
    private readonly IAuthoritativeClock clock;
    private readonly IAuthoritativeSnapshotProvider? snapshotProvider;
    private readonly StableEntityIdAllocator entityIds = new();
    private readonly ConcurrentDictionary<TcpClient, byte> clients = new();
    private readonly ConcurrentDictionary<string, SessionState> sessionsByToken = new(StringComparer.Ordinal);
    private CancellationTokenSource? cancellation;
    private Task? acceptLoop;

    public AuthoritativeTcpServer(
        AuthoritativeCommandServer commandServer,
        ProtocolHandshake handshake,
        Func<ulong> currentTickProvider,
        int port = 0,
        IPAddress? address = null,
        IAuthoritativeSnapshotProvider? snapshotProvider = null)
        : this(commandServer, handshake, new DelegateAuthoritativeClock(currentTickProvider), port, address, snapshotProvider)
    {
    }

    public AuthoritativeTcpServer(
        AuthoritativeCommandServer commandServer,
        ProtocolHandshake handshake,
        IAuthoritativeClock clock,
        int port = 0,
        IPAddress? address = null,
        IAuthoritativeSnapshotProvider? snapshotProvider = null)
    {
        this.commandServer = commandServer ?? throw new ArgumentNullException(nameof(commandServer));
        this.handshake = handshake ?? throw new ArgumentNullException(nameof(handshake));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.snapshotProvider = snapshotProvider;
        listener = new TcpListener(address ?? IPAddress.Loopback, port);
    }

    public int Port => ((IPEndPoint?)listener.LocalEndpoint)?.Port ?? 0;

    public void Start()
    {
        if (cancellation is not null)
            throw new InvalidOperationException("The server has already started.");

        cancellation = new CancellationTokenSource();
        listener.Start();
        acceptLoop = AcceptLoopAsync(cancellation.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (cancellation is null)
            return;

        cancellation.Cancel();
        listener.Stop();
        foreach (var client in clients.Keys)
            client.Dispose();

        if (acceptLoop is not null) {
            try {
                await acceptLoop;
            } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            }
        }

        cancellation.Dispose();
        cancellation = null;
        acceptLoop = null;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested) {
            TcpClient client;
            try {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                break;
            } catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
                break;
            }

            clients.TryAdd(client, 0);
            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
            try {
                await using var stream = client.GetStream();
            var helloEnvelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
            if (helloEnvelope is null || helloEnvelope.PayloadCase != Envelope.PayloadOneofCase.ClientHello) {
                await SendErrorAsync(stream, ProtocolErrorCode.InvalidMessage, "The first envelope must be a client hello.", cancellationToken);
                return;
            }

            var handshakeResult = handshake.Validate(helloEnvelope.ClientHello);
            if (!handshakeResult.Accepted) {
                await EnvelopeCodec.WriteAsync(stream, new Envelope { Error = handshakeResult.Error! }, cancellationToken);
                return;
            }

            var session = GetOrCreateSession(helloEnvelope.ClientHello.ResumeToken);
            var serverHello = handshakeResult.ServerHello!.Clone();
            serverHello.SessionToken = session.Token;
            await EnvelopeCodec.WriteAsync(stream, new Envelope { ServerHello = serverHello }, cancellationToken);
            var sessionId = session.Id;
            var entityId = session.EntityId;
            var currentTick = clock.CurrentTick;
            await SendSnapshotIfAvailableAsync(stream, sessionId, entityId, currentTick, cancellationToken);
            while (!cancellationToken.IsCancellationRequested) {
                var envelope = await EnvelopeCodec.ReadAsync(stream, cancellationToken);
                if (envelope is null)
                    return;

                if (envelope.PayloadCase == Envelope.PayloadOneofCase.CommandBatch) {
                    currentTick = clock.CurrentTick;
                    var acknowledgement = commandServer.ProcessBatch(sessionId, envelope.CommandBatch, currentTick);
                    await EnvelopeCodec.WriteAsync(stream, new Envelope { CommandAck = acknowledgement }, cancellationToken);
                    await SendSnapshotIfAvailableAsync(stream, sessionId, entityId, currentTick, cancellationToken);
                } else {
                    await SendErrorAsync(stream, ProtocolErrorCode.InvalidMessage, "The session only accepts command batches after the handshake.", cancellationToken);
                    return;
                }
            }
        } catch (EndOfStreamException) {
        } catch (InvalidDataException exception) {
            try {
                await using var stream = client.GetStream();
                await SendErrorAsync(stream, ProtocolErrorCode.InvalidMessage, exception.Message, cancellationToken);
            } catch (Exception) {
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        } finally {
            clients.TryRemove(client, out _);
            client.Dispose();
        }
    }

    private SessionState GetOrCreateSession(string resumeToken)
    {
        if (!string.IsNullOrWhiteSpace(resumeToken) && sessionsByToken.TryGetValue(resumeToken, out var resumed))
            return resumed;

        var session = new SessionState(Guid.NewGuid().ToString("N"), Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant(), entityIds.Allocate());
        sessionsByToken.TryAdd(session.Token, session);
        return session;
    }

    private static ValueTask SendErrorAsync(Stream stream, ProtocolErrorCode code, string detail, CancellationToken cancellationToken)
    {
        return EnvelopeCodec.WriteAsync(stream, new Envelope {
            Error = new ProtocolError { Code = code, Detail = detail },
        }, cancellationToken);
    }

    private sealed record SessionState(string Id, string Token, uint EntityId);

    private async ValueTask SendSnapshotIfAvailableAsync(
        Stream stream,
        string sessionId,
        uint entityId,
        ulong tick,
        CancellationToken cancellationToken)
    {
        if (snapshotProvider is null)
            return;

        var snapshot = snapshotProvider.CreateSnapshot(sessionId, entityId, tick);
        await EnvelopeCodec.WriteAsync(stream, new Envelope { Snapshot = snapshot }, cancellationToken);
    }
}
