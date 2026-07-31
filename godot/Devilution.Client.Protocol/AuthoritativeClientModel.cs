using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Main-thread authoritative projection consumed by Godot presentation nodes. */
public sealed class AuthoritativeClientModel
{
    private readonly List<GameEvent> recentEvents = [];
    private readonly Dictionary<ulong, PredictedMove> predictedMoves = [];

    public Snapshot? Snapshot { get; private set; }

    public string? LastError { get; private set; }

    public IReadOnlyList<GameEvent> RecentEvents => recentEvents;

    public ulong CurrentTick => Snapshot?.Tick ?? 0;

    public uint PlayerEntityId => Snapshot?.Players.Count == 1 ? Snapshot.Players[0].EntityId : 0;

    public ulong NextCommandTick => checked(CurrentTick + 1);

    public ClientPosition? PredictedPlayerPosition { get; private set; }

    public bool IsCorrectingPosition { get; private set; }

    public void Apply(AuthoritativeClientMessage message)
    {
        if (message.Snapshot is not null) {
            if (message.Snapshot.Players.Count != 1 || message.Snapshot.Players[0].EntityId == 0)
                throw new InvalidDataException("The authoritative snapshot must contain exactly one valid player.");
            Snapshot = message.Snapshot;
            LastError = null;
            recentEvents.Clear();
            RebuildPredictedPosition();
        }

        if (message.Events is not null)
            recentEvents.AddRange(message.Events.Events);

        if (message.Error is not null)
            LastError = message.Error.Detail;

        if (message.Acknowledgement is not null) {
            foreach (var result in message.Acknowledgement.Results)
                predictedMoves.Remove(result.ClientSequence);
            RebuildPredictedPosition();
        }
    }

    public void ClearRecentEvents()
    {
        recentEvents.Clear();
    }

    public void TrackPredictedMove(ulong sequence, int directionX, int directionY)
    {
        if (sequence == 0 || (directionX == 0 && directionY == 0))
            return;
        predictedMoves[sequence] = new PredictedMove(directionX, directionY);
        RebuildPredictedPosition();
    }

    private void RebuildPredictedPosition()
    {
        if (Snapshot?.Players.SingleOrDefault() is not { } player) {
            PredictedPlayerPosition = null;
            IsCorrectingPosition = false;
            return;
        }

        var x = player.PositionX;
        var y = player.PositionY;
        foreach (var move in predictedMoves.Values)
            (x, y) = (x + move.DirectionX, y + move.DirectionY);

        IsCorrectingPosition = predictedMoves.Count > 0
            && (x != player.PositionX || y != player.PositionY);
        PredictedPlayerPosition = IsCorrectingPosition ? new ClientPosition(x, y) : null;
    }

    private readonly record struct PredictedMove(int DirectionX, int DirectionY);
}

public readonly record struct ClientPosition(int X, int Y);
