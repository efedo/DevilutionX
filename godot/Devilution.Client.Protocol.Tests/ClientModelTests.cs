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
}
