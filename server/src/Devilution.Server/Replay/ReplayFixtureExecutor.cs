using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Stores;

namespace Devilution.Server.Replay;

public sealed record ReplayExecutionResult(Snapshot InitialSnapshot, Snapshot FinalSnapshot, IReadOnlyList<CommandResult> Results);

/** Executes the fixture's supported store commands and validates receipts. */
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
                _ => throw new InvalidDataException($"Unsupported replay command '{fixtureCommand.Kind}'."),
            };
            var result = commandServer.Process(sessionId, command, fixtureCommand.TargetTick);
            if (result.ServerReceiptSequence != fixtureCommand.ServerReceiptSequence)
                throw new InvalidDataException($"Replay receipt mismatch for client sequence {fixtureCommand.ClientSequence}.");
            results.Add(result);
        }

        return new ReplayExecutionResult(
            initialSnapshot,
            executor.CreateSnapshot(sessionId, entityId, fixture.OrderedCommands.Last().TargetTick),
            results);
    }
}
