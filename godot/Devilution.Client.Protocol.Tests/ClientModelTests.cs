using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClientModelTests
{
    [Fact]
    public void AppliesAuthoritativeSnapshotAndEvents()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Tick = 12,
                Players = { new PlayerSnapshot { EntityId = 7, Life = 30, LifeMaximum = 40 } },
            }));
        model.Apply(new AuthoritativeClientMessage(
            Events: new EventBatch {
                Tick = 12,
                Events = { new GameEvent { Damage = new DamageEvent { TargetEntityId = 7, Amount = 3 } } },
            }));

        Assert.Equal(12UL, model.CurrentTick);
        Assert.Equal(7U, model.PlayerEntityId);
        Assert.Single(model.RecentEvents);
        Assert.Equal(3, model.RecentEvents[0].Damage.Amount);
        Assert.Equal(13UL, model.NextCommandTick);
    }

    [Fact]
    public void RejectsSnapshotsWithoutExactlyOnePlayer()
    {
        var model = new AuthoritativeClientModel();

        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot())));
    }

    [Fact]
    public void KeepsTheLatestCommandResultForPresentationFeedback()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Acknowledgement: new CommandAck {
                Results = {
                    new CommandResult {
                        ClientSequence = 4,
                        Status = CommandStatus.Accepted,
                        AppliedTick = 22,
                    },
                },
            }));

        Assert.NotNull(model.LastCommandResult);
        Assert.Equal(4UL, model.LastCommandResult!.ClientSequence);
        Assert.Equal(22UL, model.LastCommandResult.AppliedTick);
    }

    [Fact]
    public void ProjectsMultiCellInventoryItemsAndFindsFallbackAnchors()
    {
        var items = new List<ItemSnapshot> {
            new() { State = new ItemStateSnapshot { InventoryWidth = 2, InventoryHeight = 2 } },
            new() { State = new ItemStateSnapshot { InventoryWidth = 1, InventoryHeight = 1 } },
        };
        var layout = InventoryLayout.Build(items, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 4, 3);

        Assert.Equal(0, layout.Anchors[0]);
        Assert.Equal(2, layout.Anchors[1]);
        Assert.Equal(new[] { 0, 0, 1, -1, 0, 0, -1, -1, -1, -1, -1, -1 }, layout.Occupants);
    }
}
