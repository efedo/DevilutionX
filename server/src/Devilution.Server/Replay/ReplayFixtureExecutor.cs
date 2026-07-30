using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Stores;

namespace Devilution.Server.Replay;

public sealed record ReplayCheckpointResult(ulong Tick, string ExpectedStateSha256, string ActualStateSha256);

public sealed record ReplayExecutionResult(
    Snapshot InitialSnapshot,
    Snapshot FinalSnapshot,
    IReadOnlyList<CommandResult> Results,
    IReadOnlyList<ReplayCheckpointResult> Checkpoints);

/** Executes the fixture's supported authoritative commands and validates receipts. */
public static class ReplayFixtureExecutor
{
    public static ReplayExecutionResult Execute(
        ReplayFixture fixture,
        StoreSimulationExecutor executor,
        AuthoritativeCommandServer commandServer,
        uint entityId = 1)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(commandServer);

        var sessionId = fixture.InitialState.Player;
        var initialSnapshot = executor.CreateSnapshot(sessionId, entityId, 0);
        var results = new List<CommandResult>();
        var checkpoints = new List<ReplayCheckpointResult>();
        foreach (var fixtureCommand in fixture.OrderedCommands) {
            var command = fixtureCommand.Kind switch {
                "OpenStore" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    OpenStoreRequested = new OpenStoreRequested { StoreId = fixtureCommand.StoreId },
                },
                "BuyItem" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    PurchaseRequested = new PurchaseRequested {
                        StoreId = fixtureCommand.StoreId,
                        StoreSlot = fixtureCommand.StoreSlot,
                    },
                },
                "SellItem" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    SellItemRequested = new SellItemRequested { InventoryIndex = fixtureCommand.StoreSlot },
                },
                "RefillMana" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    RefillManaRequested = new RefillManaRequested(),
                },
                "Move" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    MoveRequested = new MoveRequested {
                        DirectionX = fixtureCommand.DirectionX,
                        DirectionY = fixtureCommand.DirectionY,
                    },
                },
                "Attack" => new Command {
                    ClientSequence = fixtureCommand.ClientSequence,
                    RequestedTick = fixtureCommand.TargetTick,
                    AttackRequested = new AttackRequested { TargetEntityId = fixtureCommand.TargetEntityId },
                },
                _ => throw new InvalidDataException($"Unsupported replay command '{fixtureCommand.Kind}'."),
            };
            var result = commandServer.Process(sessionId, command, fixtureCommand.TargetTick);
            if (result.ServerReceiptSequence != fixtureCommand.ServerReceiptSequence)
                throw new InvalidDataException($"Replay receipt mismatch for client sequence {fixtureCommand.ClientSequence}.");
            results.Add(result);

            if (fixture.ContentManifest is not null) {
                var checkpoint = fixture.Checkpoints.FirstOrDefault(candidate => candidate.Tick == fixtureCommand.TargetTick);
                if (checkpoint is null)
                    throw new InvalidDataException($"Replay fixture must include a checkpoint at command tick {fixtureCommand.TargetTick}.");
                var snapshot = executor.CreateSnapshot(sessionId, entityId, fixtureCommand.TargetTick);
                if (IsSha256(checkpoint.StateSha256)
                    && !string.Equals(checkpoint.StateSha256, snapshot.StateSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Replay checkpoint hash mismatch at tick {checkpoint.Tick}: expected {checkpoint.StateSha256}, actual {snapshot.StateSha256}.");
                checkpoints.Add(new ReplayCheckpointResult(checkpoint.Tick, checkpoint.StateSha256, snapshot.StateSha256));
            }
        }

        var finalSnapshot = executor.CreateSnapshot(sessionId, entityId, fixture.OrderedCommands.Last().TargetTick);
        if (fixture.FinalStateSha256 is not null
            && !string.Equals(fixture.FinalStateSha256, finalSnapshot.StateSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Replay final snapshot hash mismatch.");

        return new ReplayExecutionResult(initialSnapshot, finalSnapshot, results, checkpoints);
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(character => Uri.IsHexDigit(character));
    }
}
