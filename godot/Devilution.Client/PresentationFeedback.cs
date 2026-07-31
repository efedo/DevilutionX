using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Godot;

namespace Devilution.Client;

/** Optional audio feedback. Missing audio assets intentionally degrade to silence. */
public partial class PresentationFeedback : Node
{
    private readonly AudioStreamPlayer damage = new();
    private readonly AudioStreamPlayer healing = new();
    private readonly AudioStreamPlayer commandRejected = new();
    private int appliedEventCount;
    private string? appliedCommandResult;

    public override void _Ready()
    {
        AddChild(damage);
        AddChild(healing);
        AddChild(commandRejected);
        damage.Stream = LoadOptional("res://assets/audio/damage.ogg");
        healing.Stream = LoadOptional("res://assets/audio/healing.ogg");
        commandRejected.Stream = LoadOptional("res://assets/audio/command_rejected.ogg");
    }

    public void Apply(AuthoritativeClientModel model)
    {
        if (model.RecentEvents.Count < appliedEventCount)
            appliedEventCount = 0;
        for (var index = appliedEventCount; index < model.RecentEvents.Count; index++) {
            switch (model.RecentEvents[index].EventCase) {
                case GameEvent.EventOneofCase.Damage:
                    damage.Play();
                    break;
                case GameEvent.EventOneofCase.Healing:
                    healing.Play();
                    break;
            }
        }
        appliedEventCount = model.RecentEvents.Count;

        var result = model.LastCommandResult;
        if (result is null)
            return;
        var resultKey = $"{result.ClientSequence}:{result.Status}:{result.AppliedTick}";
        if (resultKey == appliedCommandResult)
            return;
        appliedCommandResult = resultKey;
        if (result.Status != CommandStatus.Accepted)
            commandRejected.Play();
    }

    private static AudioStream? LoadOptional(string path)
    {
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;
    }
}
