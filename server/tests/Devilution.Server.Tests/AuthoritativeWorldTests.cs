using Devilution.Server.Gameplay;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeWorldTests
{
    [Fact]
    public void LevelsValidateBoundsAndBlockedCellsIndependently()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 3, 3, [4]));
        world.AddLevel(new AuthoritativeLevel(2, 2, 2, [0]));

        Assert.True(world.IsWalkable(1, 0, 0));
        Assert.False(world.IsWalkable(1, 1, 1));
        Assert.False(world.IsWalkable(2, 0, 0));
        Assert.False(world.IsWalkable(2, 2, 0));
    }

    [Fact]
    public void RejectsDuplicateLevelsAndInvalidBlockedCells()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 2, 2));
        Assert.Throws<InvalidDataException>(() => world.AddLevel(new AuthoritativeLevel(1, 2, 2)));
        Assert.Throws<InvalidDataException>(() => new AuthoritativeLevel(2, 2, 2, [4]));
    }

    [Fact]
    public void LoadsLevelGeometryFromExternalTsv()
    {
        var world = AuthoritativeWorld.LoadTsv("levels.tsv", "level_id\twidth\theight\tblocked_cells\n1\t3\t3\t4\n");

        Assert.True(world.ContainsLevel(1));
        Assert.False(world.IsWalkable(1, 1, 1));
    }

    [Fact]
    public void LineOfSightRejectsBlockedIntermediateCells()
    {
        var level = new AuthoritativeLevel(1, 5, 3, [1 * 5 + 2]);

        Assert.False(level.HasLineOfSight(0, 1, 4, 1));
        Assert.True(level.HasLineOfSight(0, 0, 4, 0));
    }
}
