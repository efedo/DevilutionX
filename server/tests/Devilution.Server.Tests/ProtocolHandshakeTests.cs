using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ProtocolHandshakeTests
{
    [Fact]
    public void MatchingIdentityIsAccepted()
    {
        var handshake = CreateHandshake();

        var result = handshake.Validate(new ClientHello {
            ClientBuildId = "client-build",
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "content-hash",
        });

        Assert.True(result.Accepted);
        Assert.Equal("server-build", result.ServerHello!.ServerBuildId);
        Assert.Equal(20U, result.ServerHello.TickRateHz);
    }

    [Fact]
    public void SchemaMismatchIsRejected()
    {
        var result = CreateHandshake().Validate(new ClientHello {
            ProtocolSchemaVersion = "0.2.0",
            ContentManifestHash = "content-hash",
        });

        Assert.False(result.Accepted);
        Assert.Equal(ProtocolErrorCode.UnsupportedSchema, result.Error!.Code);
    }

    [Fact]
    public void ContentMismatchIsRejected()
    {
        var result = CreateHandshake().Validate(new ClientHello {
            ProtocolSchemaVersion = "0.1.0",
            ContentManifestHash = "different-content",
        });

        Assert.False(result.Accepted);
        Assert.Equal(ProtocolErrorCode.ContentMismatch, result.Error!.Code);
    }

    [Fact]
    public void MissingIdentityFieldsAreRejected()
    {
        var result = CreateHandshake().Validate(new ClientHello());

        Assert.False(result.Accepted);
        Assert.Equal(ProtocolErrorCode.InvalidMessage, result.Error!.Code);
    }

    private static ProtocolHandshake CreateHandshake()
    {
        return new ProtocolHandshake(new ProtocolServerIdentity("server-build", "0.1.0", "content-hash", 20));
    }
}
