namespace Devilution.Server.Gameplay;

using Devilution.Server.Content;

/** Immutable level geometry used by authoritative movement and transitions. */
public sealed class AuthoritativeWorld
{
    private readonly Dictionary<uint, AuthoritativeLevel> levels = new();

    public void AddLevel(AuthoritativeLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!levels.TryAdd(level.LevelId, level))
            throw new InvalidDataException($"Level {level.LevelId} is registered more than once.");
    }

    public bool IsWalkable(uint levelId, int positionX, int positionY)
    {
        return levels.TryGetValue(levelId, out var level) && level.IsWalkable(positionX, positionY);
    }

    public bool ContainsLevel(uint levelId) => levels.ContainsKey(levelId);

    public IReadOnlyList<AuthoritativeLevel> Levels => levels.Values.OrderBy(level => level.LevelId).ToArray();

    public static AuthoritativeWorld LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var world = new AuthoritativeWorld();
        foreach (var row in table.Rows) {
            var blocked = row.TryGet("blocked_cells", out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(cell => {
                    if (!int.TryParse(cell, out var index))
                        throw new InvalidDataException($"Level {row.Required("level_id")} contains an invalid blocked cell '{cell}'.");
                    return index;
                })
                : [];
            world.AddLevel(new AuthoritativeLevel(
                row.RequiredUInt32("level_id"),
                row.RequiredInt32("width"),
                row.RequiredInt32("height"),
                blocked));
        }
        return world;
    }
}

public sealed class AuthoritativeLevel
{
    private readonly HashSet<int> blockedCells;

    public AuthoritativeLevel(uint levelId, int width, int height, IEnumerable<int>? blockedCells = null)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Level dimensions must be positive.");
        LevelId = levelId;
        Width = width;
        Height = height;
        this.blockedCells = (blockedCells ?? []).ToHashSet();
        if (this.blockedCells.Any(cell => cell < 0 || cell >= width * height))
            throw new InvalidDataException($"Level {levelId} contains an out-of-range blocked cell.");
    }

    public uint LevelId { get; }

    public int Width { get; }

    public int Height { get; }

    public bool IsWalkable(int positionX, int positionY)
    {
        return positionX >= 0 && positionX < Width
            && positionY >= 0 && positionY < Height
            && !blockedCells.Contains(positionY * Width + positionX);
    }

    public bool HasLineOfSight(int startX, int startY, int endX, int endY)
    {
        var x = startX;
        var y = startY;
        var deltaX = Math.Abs(endX - startX);
        var deltaY = Math.Abs(endY - startY);
        var stepX = startX < endX ? 1 : -1;
        var stepY = startY < endY ? 1 : -1;
        var error = deltaX - deltaY;

        while (true) {
            if ((x != startX || y != startY) && (x != endX || y != endY) && !IsWalkable(x, y))
                return false;
            if (x == endX && y == endY)
                return true;

            var doubledError = 2 * error;
            if (doubledError > -deltaY) {
                error -= deltaY;
                x += stepX;
            }
            if (doubledError < deltaX) {
                error += deltaX;
                y += stepY;
            }
        }
    }
}
