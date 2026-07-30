namespace Devilution.Server.Gameplay;

/** Minimal server-owned combat target used by the initial combat boundary. */
public sealed class AuthoritativeCombatTarget
{
    public AuthoritativeCombatTarget(uint entityId, int positionX, int positionY, int hitPoints, int armorClass = 0)
    {
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (hitPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(hitPoints));

        EntityId = entityId;
        PositionX = positionX;
        PositionY = positionY;
        HitPoints = hitPoints;
        ArmorClass = armorClass;
    }

    public uint EntityId { get; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int HitPoints { get; set; }
    public int ArmorClass { get; }
}
