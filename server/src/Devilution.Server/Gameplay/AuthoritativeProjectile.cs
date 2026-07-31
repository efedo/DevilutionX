namespace Devilution.Server.Gameplay;

/** Server-owned in-flight spell state. Projectiles resolve in stable entity order. */
public sealed class AuthoritativeProjectile
{
    public AuthoritativeProjectile(
        uint entityId,
        uint sourceEntityId,
        uint targetEntityId,
        uint spellId,
        uint levelId,
        int positionX,
        int positionY,
        int targetX,
        int targetY,
        int damage,
        AuthoritativeDamageType damageType,
        int areaRadius,
        uint remainingTicks)
    {
        if (entityId == 0 || sourceEntityId == 0 || spellId == 0 || remainingTicks == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (damage < 0 || areaRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));

        EntityId = entityId;
        SourceEntityId = sourceEntityId;
        TargetEntityId = targetEntityId;
        SpellId = spellId;
        LevelId = levelId;
        PositionX = positionX;
        PositionY = positionY;
        TargetX = targetX;
        TargetY = targetY;
        Damage = damage;
        DamageType = damageType;
        AreaRadius = areaRadius;
        RemainingTicks = remainingTicks;
    }

    public uint EntityId { get; }
    public uint SourceEntityId { get; }
    public uint TargetEntityId { get; }
    public uint SpellId { get; }
    public uint LevelId { get; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int TargetX { get; }
    public int TargetY { get; }
    public int Damage { get; }
    public AuthoritativeDamageType DamageType { get; }
    public int AreaRadius { get; }
    public uint RemainingTicks { get; set; }
}
