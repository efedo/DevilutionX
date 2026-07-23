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

        Assert.Equal("3e7077fd2f66d689e0cee6a7cf5b37bf2dca7c979af356d0a31cbc5c85605c7d", SnapshotStateHasher.Compute(snapshot));
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

    [Fact]
    public void ActiveStoreItemStateChangesTheStateHash()
    {
        var first = new Snapshot {
            ActiveStore = new StoreSnapshot {
                StoreId = 1,
                Items = {
                    new StoreItemSnapshot {
                        StoreSlot = 0,
                        ItemSeed = 42,
                        Price = 75,
                        State = new ItemStateSnapshot { PrefixPower = 1, Buff = 2 },
                    },
                },
            },
        };
        var second = first.Clone();
        second.ActiveStore.Items[0].State.Buff = 3;

        Assert.NotEqual(SnapshotStateHasher.Compute(first), SnapshotStateHasher.Compute(second));
    }
}
