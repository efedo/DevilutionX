using Devilution.Server.Content;

namespace Devilution.Server.Gameplay;

/** Mutable server-owned object state used by interaction commands. */
public sealed class AuthoritativeWorldObject
{
    public AuthoritativeWorldObject(
        uint entityId,
        uint objectId,
        uint levelId,
        int positionX,
        int positionY,
        bool activated = false,
        uint questId = 0)
    {
        if (entityId == 0 || objectId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        EntityId = entityId;
        ObjectId = objectId;
        LevelId = levelId;
        PositionX = positionX;
        PositionY = positionY;
        Activated = activated;
        QuestId = questId;
    }

    public uint EntityId { get; }
    public uint ObjectId { get; set; }
    public uint LevelId { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public bool Activated { get; set; }
    public uint QuestId { get; set; }
}

/** External object placement and identity definitions. */
public static class AuthoritativeWorldObjectCatalog
{
    public static IReadOnlyList<AuthoritativeWorldObject> LoadTsv(string sourcePath, string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var objects = table.Rows.Select(row => new AuthoritativeWorldObject(
            row.RequiredUInt32("entity_id"),
            row.RequiredUInt32("object_id"),
            row.RequiredUInt32("level_id"),
            row.RequiredInt32("position_x"),
            row.RequiredInt32("position_y"),
            row.OptionalInt32("activated") != 0,
            row.OptionalUInt32("quest_id"))).ToArray();
        if (objects.Select(@object => @object.EntityId).Distinct().Count() != objects.Length)
            throw new InvalidDataException($"Object table '{sourcePath}' contains duplicate entity IDs.");
        return objects;
    }
}
