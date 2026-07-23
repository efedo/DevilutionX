using Devilution.Server.Stores;

namespace Devilution.Server.Replay;

public sealed record ReplayInitialState(string Player, uint Gold, uint Experience, int Life, int Mana)
{
    public int CharacterClass { get; init; }

    public byte CharacterLevel { get; init; } = 1;
}

public sealed record ReplayLegacyStoreState(
    int ActiveStore,
    int PremiumItemCount,
    int PremiumItemLevel,
    IReadOnlyList<uint> PremiumItemSeeds)
{
    public IReadOnlyList<StoreItem> ToStoreItems()
    {
        return PremiumItemSeeds.Select(seed => new StoreItem(0, seed, 0)).ToArray();
    }
}

public sealed record ReplayFixtureCommand(
    ulong ClientSequence,
    ulong TargetTick,
    ulong ServerReceiptSequence,
    string Kind,
    uint StoreId,
    uint StoreSlot);

public sealed record ReplayContentManifest(string Id, string Version, string Sha256);

public sealed record ReplayCheckpoint(ulong Tick, string StateSha256);

public sealed record ReplayFixture(
    int FormatVersion,
    string FixtureId,
    string ProtocolSchemaVersion,
    uint TickRateHz,
    ulong RngSeed,
    ReplayInitialState InitialState,
    IReadOnlyList<ReplayFixtureCommand> Commands,
    IReadOnlyList<ReplayCheckpoint> Checkpoints)
{
    public ReplayLegacyStoreState LegacyStoreState { get; init; } = new(1, 0, 3, [42]);

    /** Null when loading the legacy array-shaped content_manifest metadata. */
    public ReplayContentManifest? ContentManifest { get; init; }

    /** Optional authoritative final snapshot hash for transition fixtures. */
    public string? FinalStateSha256 { get; init; }

    public IReadOnlyList<ReplayFixtureCommand> OrderedCommands => Commands
        .OrderBy(command => command.TargetTick)
        .ThenBy(command => command.ServerReceiptSequence)
        .ToArray();
}
