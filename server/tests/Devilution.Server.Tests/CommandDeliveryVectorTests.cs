using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class CommandDeliveryVectorTests
{
    [Fact]
    public void RetryVectorPreservesDuplicateAndLateCommandExpectations()
    {
        var vector = LoadVector();
        var duplicate = vector.Steps.Single(step => step.Operation == "server_result" && step.Status == CommandStatus.Duplicate);
        var late = vector.Steps.Single(step => step.ClientSequence == 2 && step.Status == CommandStatus.Rejected);
        var rescheduled = vector.Steps.Single(step => step.ClientSequence == 3 && step.Status == CommandStatus.Rescheduled);

        Assert.Equal("command-delivery/retry-dedup", vector.VectorId);
        Assert.Equal(100U, vector.InitialRttMilliseconds);
        Assert.Equal(7UL, duplicate.ServerReceiptSequence);
        Assert.Equal(CommandRejectReason.TooLate, late.RejectReason);
        Assert.Equal(18UL, rescheduled.AppliedTick);
        Assert.Contains(vector.Assertions, assertion => assertion.Contains("executes once", StringComparison.Ordinal));
    }

    private static CommandDeliveryVector LoadVector()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Vectors", "command-delivery-retry.json");
        return CommandDeliveryVectorLoader.Load(File.ReadAllText(path));
    }
}
