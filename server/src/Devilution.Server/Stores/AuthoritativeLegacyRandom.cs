namespace Devilution.Server.Stores;

/** Compatibility implementation of the native Diablo LCG used by item generation. */
public sealed class AuthoritativeLegacyRandom
{
    private uint state;

    public AuthoritativeLegacyRandom(uint seed)
    {
        state = seed;
    }

    public int Next(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
            return 0;

        var value = AdvanceSeed();
        return value <= 0x7FFF
            ? (value >> 16) % exclusiveMaximum
            : value % exclusiveMaximum;
    }

    public void Discard(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        for (var index = 0; index < count; index++)
            AdvanceSeed();
    }

    private int AdvanceSeed()
    {
        state = unchecked(state * 0x015A4E35U + 1U);
        var signed = unchecked((int)state);
        return signed == int.MinValue ? int.MinValue : Math.Abs(signed);
    }
}
