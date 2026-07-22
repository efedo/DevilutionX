using Devilution.Protocol.V1;

namespace Devilution.Server.Protocol;

public sealed record ProtocolServerIdentity(
    string BuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    uint TickRateHz);

public readonly record struct HandshakeResult(bool Accepted, ServerHello? ServerHello, ProtocolError? Error)
{
    public static HandshakeResult Success(ServerHello serverHello)
    {
        return new(true, serverHello, null);
    }

    public static HandshakeResult Failure(ProtocolError error)
    {
        return new(false, null, error);
    }
}

/** Validates the identity contract before a session can submit commands. */
public sealed class ProtocolHandshake
{
    private readonly ProtocolServerIdentity identity;

    public ProtocolHandshake(ProtocolServerIdentity identity)
    {
        this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
        if (string.IsNullOrWhiteSpace(identity.BuildId)
            || string.IsNullOrWhiteSpace(identity.ProtocolSchemaVersion)
            || string.IsNullOrWhiteSpace(identity.ContentManifestHash)
            || identity.TickRateHz == 0)
            throw new ArgumentException("The server identity must contain all handshake fields.", nameof(identity));
    }

    public HandshakeResult Validate(ClientHello? clientHello)
    {
        if (clientHello is null
            || string.IsNullOrWhiteSpace(clientHello.ProtocolSchemaVersion)
            || string.IsNullOrWhiteSpace(clientHello.ContentManifestHash))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.InvalidMessage, "Client hello is missing required identity fields."));

        if (!string.Equals(clientHello.ProtocolSchemaVersion, identity.ProtocolSchemaVersion, StringComparison.Ordinal))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.UnsupportedSchema, "The client protocol schema version is not supported."));

        if (!string.Equals(clientHello.ContentManifestHash, identity.ContentManifestHash, StringComparison.Ordinal))
            return HandshakeResult.Failure(Error(ProtocolErrorCode.ContentMismatch, "The client content manifest does not match the server."));

        return HandshakeResult.Success(new ServerHello {
            ServerBuildId = identity.BuildId,
            ProtocolSchemaVersion = identity.ProtocolSchemaVersion,
            ContentManifestHash = identity.ContentManifestHash,
            TickRateHz = identity.TickRateHz,
        });
    }

    private static ProtocolError Error(ProtocolErrorCode code, string detail)
    {
        return new ProtocolError { Code = code, Detail = detail };
    }
}
