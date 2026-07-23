using System.Net;
using Devilution.Server.Host;
using Devilution.Server.Simulation;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ServerHostTests
{
    [Fact]
    public void DefaultsAreLoopbackAndTheExpectedDevelopmentPort()
    {
        Assert.Equal(IPAddress.Loopback, ServerHostOptions.Defaults.BindAddress);
        Assert.Equal(6113, ServerHostOptions.Defaults.Port);
        Assert.Equal(20U, ServerHostOptions.Defaults.TickRateHz);
    }

    [Fact]
    public void ParsesHostAndContentOverrides()
    {
        var parsed = ServerHostOptions.TryParse([
            "--bind", "0.0.0.0",
            "--port", "6120",
            "--content-root", "content/custom",
            "--content-id", "modded",
            "--content-version", "2.0.0",
            "--build-id", "test-server",
            "--protocol-version", "0.2.0",
            "--tick-rate", "30",
            "--starting-gold", "900"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.Equal(IPAddress.Any, options.BindAddress);
        Assert.Equal(6120, options.Port);
        Assert.Equal("content/custom", options.ContentRoot);
        Assert.Equal("modded", options.ContentId);
        Assert.Equal("2.0.0", options.ContentVersion);
        Assert.Equal(30U, options.TickRateHz);
        Assert.Equal(900U, options.StartingGold);
    }

    [Theory]
    [InlineData("--port", "-1")]
    [InlineData("--port", "65536")]
    [InlineData("--tick-rate", "0")]
    [InlineData("--bind", "not-an-address")]
    [InlineData("--unknown", "value")]
    public void RejectsInvalidOptions(string name, string value)
    {
        Assert.False(ServerHostOptions.TryParse([name, value], out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task RealtimeClockIsMonotonic()
    {
        var clock = new RealtimeAuthoritativeClock(1000);
        var first = clock.CurrentTick;
        await Task.Delay(5, TestContext.Current.CancellationToken);
        Assert.True(clock.CurrentTick >= first);
    }
}
