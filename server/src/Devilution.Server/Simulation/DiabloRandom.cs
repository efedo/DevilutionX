namespace Devilution.Server.Simulation;

/** Borland-compatible 32-bit LCG and the legacy GenerateRnd behavior. */
public sealed class DiabloRandom
{
    private uint state;

    public DiabloRandom(uint seed)
    {
        state = seed;
    }

    public uint State => state;

    public uint NextUInt32()
    {
        state = unchecked(0x015A4E35U * state + 1U);
        return state;
    }

    public int AdvanceRndSeed()
    {
        var seed = unchecked((int)NextUInt32());
        return seed == int.MinValue ? int.MinValue : Math.Abs(seed);
    }

    public int GenerateRnd(int limit)
    {
        if (limit <= 0)
            return 0;
        var seed = AdvanceRndSeed();
        return (limit <= 0x7FFF ? seed >> 16 : seed) % limit;
    }

    public int RandomIntLessThan(int limit) => Math.Max(GenerateRnd(limit), 0);

    public bool FlipCoin(uint frequency) => GenerateRnd(unchecked((int)frequency)) == 0;
}
