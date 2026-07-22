using Devilution.Server.Simulation;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class DeterministicPrimitiveTests
{
    [Fact]
    public void FixedPointQ6PreservesLegacyRawValuesAndTruncation()
    {
        var value = FixedPointQ6.FromInt(10) + new FixedPointQ6(32);

        Assert.Equal(672, value.Raw);
        Assert.Equal(10, value.ToIntTruncated());
        Assert.Equal(new FixedPointQ6(1344), value * 2);
    }

    [Fact]
    public void DiabloRandomUsesTheLegacyLcgAndHighBitPath()
    {
        var random = new DiabloRandom(0x12345678);

        Assert.Equal(0x955E76D9U, random.NextUInt32());
        Assert.Equal(0x560EB8EEU, random.NextUInt32());

        random = new DiabloRandom(0x12345678);
        Assert.Equal(0, random.GenerateRnd(1));
        Assert.Equal(0x955E76D9U, random.State);
    }

    [Fact]
    public void EntityIdsAreNonZeroAndDoNotWrapToZero()
    {
        var allocator = new StableEntityIdAllocator(uint.MaxValue);

        Assert.Equal(uint.MaxValue, allocator.Allocate());
        Assert.Throws<InvalidOperationException>(() => allocator.Allocate());
    }
}
