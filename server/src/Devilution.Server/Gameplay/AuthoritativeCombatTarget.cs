using Devilution.Server.Stores;

namespace Devilution.Server.Gameplay;

/** Minimal server-owned combat target used by the initial combat boundary. */
public sealed class AuthoritativeCombatTarget
{
    public AuthoritativeCombatTarget(
        uint entityId,
        int positionX,
        int positionY,
        int hitPoints,
        int armorClass = 0,
        int? maxHitPoints = null,
        uint levelId = 0,
        uint monsterId = 0,
        uint dropItemEntityId = 0,
        uint dropItemSeed = 0,
        uint dropItemPrice = 0,
        AuthoritativeItemState? dropItemState = null,
        int attackDamage = 0,
        int aggroRange = 0,
        int fireResistance = 0,
        int lightningResistance = 0,
        int magicResistance = 0)
    {
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (hitPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(hitPoints));
        if (attackDamage < 0 || aggroRange < 0)
            throw new ArgumentOutOfRangeException(nameof(attackDamage));
        if (fireResistance is < -100 or > 100 || lightningResistance is < -100 or > 100 || magicResistance is < -100 or > 100)
            throw new ArgumentOutOfRangeException(nameof(fireResistance));

        EntityId = entityId;
        PositionX = positionX;
        PositionY = positionY;
        HitPoints = hitPoints;
        ArmorClass = armorClass;
        MaxHitPoints = Math.Max(hitPoints, maxHitPoints ?? hitPoints);
        LevelId = levelId;
        MonsterId = monsterId;
        AttackDamage = attackDamage;
        AggroRange = aggroRange;
        FireResistance = fireResistance;
        LightningResistance = lightningResistance;
        MagicResistance = magicResistance;
        if (dropItemEntityId != 0 || dropItemSeed != 0) {
            if (dropItemEntityId == 0 || dropItemSeed == 0)
                throw new ArgumentException("Monster drops require both entity and item IDs.");
            Drop = new AuthoritativeWorldItem(
                dropItemEntityId,
                levelId,
                positionX,
                positionY,
                dropItemSeed,
                dropItemPrice,
                dropItemState ?? AuthoritativeItemState.Empty with { ItemType = 1, Value = (int)dropItemPrice, IdentifiedValue = (int)dropItemPrice });
        }
    }

    public uint EntityId { get; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int HitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int MaxHitPoints { get; set; }
    public uint LevelId { get; set; }
    public uint MonsterId { get; set; }
    public int AttackDamage { get; set; }
    public int AggroRange { get; set; }
    public int FireResistance { get; set; }
    public int LightningResistance { get; set; }
    public int MagicResistance { get; set; }
    public AuthoritativeWorldItem? Drop { get; }
}
