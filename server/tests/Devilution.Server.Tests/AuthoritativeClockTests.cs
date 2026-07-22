using Devilution.Server.Simulation;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeClockTests
{
    [Fact]
    public void DelegateClockReadsTheAuthoritativeTickAtEachAccess()
    {
        ulong tick = 10;
        var clock = new DelegateAuthoritativeClock(() => tick);

        Assert.Equal(10UL, clock.CurrentTick);
        tick = 11;
        Assert.Equal(11UL, clock.CurrentTick);
    }
}
