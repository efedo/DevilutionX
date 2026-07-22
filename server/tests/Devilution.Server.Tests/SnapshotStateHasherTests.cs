using Devilution.Protocol.V1;
using Devilution.Server.Snapshots;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class SnapshotStateHasherTests
{
    [Fact]
    public void EmptySnapshotHashesItsZeroPlayerCount()
    {
        var snapshot = new Snapshot();

        Assert.Equal("af5570f5a1810b7af78caf4bc70a660f0df51e42baf91d4de5b2328de0e83dfc", SnapshotStateHasher.Compute(snapshot));
    }

    [Fact]
    public void CanonicalPrimitivesMatchLittleEndianLengthPrefixedRules()
    {
        var hasher = new CanonicalStateHasher();
        hasher.AppendBool(true);
        hasher.AppendUInt8(7);
        hasher.AppendInt32(-42);
        hasher.AppendUInt32(9001);
        hasher.AppendUInt64(123456789);
        hasher.AppendString("griswold");

        Assert.Equal("b23c2ac66840117b646028471a19b9e5928967f875de54c461e9a4a95edf7a1b", hasher.HexDigest());
    }

    [Fact]
    public void PlayerAndInventoryOrderingDoesNotChangeHash()
    {
        var first = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 2,
                    Gold = 100,
                    Experience = 200,
                    Inventory = {
                        new ItemSnapshot { StoreId = 1, StoreSlot = 2, ItemSeed = 30 },
                        new ItemSnapshot { StoreId = 1, StoreSlot = 1, ItemSeed = 20 },
                    },
                },
                new PlayerSnapshot { EntityId = 1, Gold = 50 },
            },
        };
        var second = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 1, Gold = 50 },
                new PlayerSnapshot {
                    EntityId = 2,
                    Gold = 100,
                    Experience = 200,
                    Inventory = {
                        new ItemSnapshot { StoreId = 1, StoreSlot = 1, ItemSeed = 20 },
                        new ItemSnapshot { StoreId = 1, StoreSlot = 2, ItemSeed = 30 },
                    },
                },
            },
        };

        Assert.Equal(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }

    [Fact]
    public void ItemAffixAndBuffFieldsChangeTheStateHash()
    {
        var first = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    Inventory = {
                        new ItemSnapshot { State = new ItemStateSnapshot { PrefixPower = 1, Buff = 2 } },
                    },
                },
            },
        };
        var second = first.Clone();
        second.Players[0].Inventory[0].State.Buff = 3;

        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }
}
