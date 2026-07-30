using Devilution.Server.Stores;

namespace Devilution.Server.Gameplay;

/** Server-owned item lying in the world until a player picks it up. */
public sealed class AuthoritativeWorldItem
{
    public AuthoritativeWorldItem(
        uint entityId,
        uint levelId,
        int positionX,
        int positionY,
        uint itemSeed,
        uint price,
        AuthoritativeItemState state)
    {
        if (entityId == 0 || itemSeed == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId), "World items require non-zero entity and item IDs.");
        if (positionX < 0 || positionY < 0)
            throw new ArgumentOutOfRangeException(nameof(positionX));
        EntityId = entityId;
        LevelId = levelId;
        PositionX = positionX;
        PositionY = positionY;
        ItemSeed = itemSeed;
        Price = price;
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public uint EntityId { get; }
    public uint LevelId { get; }
    public int PositionX { get; }
    public int PositionY { get; }
    public uint ItemSeed { get; }
    public uint Price { get; }
    public AuthoritativeItemState State { get; }
}
