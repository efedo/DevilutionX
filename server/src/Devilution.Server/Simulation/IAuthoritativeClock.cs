namespace Devilution.Server.Simulation;

/** Supplies the simulation tick at an authoritative boundary. */
public interface IAuthoritativeClock
{
    ulong CurrentTick { get; }
}

public sealed class DelegateAuthoritativeClock : IAuthoritativeClock
{
    private readonly Func<ulong> provider;

    public DelegateAuthoritativeClock(Func<ulong> provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public ulong CurrentTick => provider();
}
