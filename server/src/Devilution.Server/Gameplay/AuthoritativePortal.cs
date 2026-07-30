namespace Devilution.Server.Gameplay;

/** Deterministic level transition owned by the authoritative world. */
public sealed record AuthoritativePortal(
    uint PortalId,
    uint SourceLevelId,
    int SourcePositionX,
    int SourcePositionY,
    uint DestinationLevelId,
    int DestinationPositionX,
    int DestinationPositionY);

public sealed record AuthoritativeStatusEffect(uint EffectId, uint RemainingTicks, int Magnitude);
