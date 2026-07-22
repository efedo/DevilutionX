namespace Devilution.Server.Simulation;

/** Signed 26.6 fixed-point value used by legacy hit-point arithmetic. */
public readonly record struct FixedPointQ6(int Raw)
{
    public static FixedPointQ6 FromInt(int value) => new(checked(value << 6));

    public int ToIntTruncated() => Raw / 64;

    public static FixedPointQ6 operator +(FixedPointQ6 left, FixedPointQ6 right) => new(checked(left.Raw + right.Raw));

    public static FixedPointQ6 operator -(FixedPointQ6 left, FixedPointQ6 right) => new(checked(left.Raw - right.Raw));

    public static FixedPointQ6 operator *(FixedPointQ6 left, int right) => new(checked(left.Raw * right));

    public static FixedPointQ6 operator /(FixedPointQ6 left, int right) => new(checked(left.Raw / right));
}
