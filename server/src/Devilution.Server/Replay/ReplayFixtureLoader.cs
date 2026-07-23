using System.Text.Json;

namespace Devilution.Server.Replay;

/** Strict loader for the versioned cross-language replay fixture envelope. */
public static class ReplayFixtureLoader
{
    public static ReplayFixture Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var fixture = new ReplayFixture(
            RequiredInt(root, "format_version"),
            RequiredString(root, "fixture_id"),
            RequiredString(root, "protocol_schema_version"),
            RequiredUInt(root, "tick_rate_hz"),
            RequiredULong(root, "rng_seed"),
            ParseInitialState(root.GetProperty("initial_state")),
            ParseCommands(root.GetProperty("commands")),
            ParseCheckpoints(root.GetProperty("checkpoints"))) {
            LegacyStoreState = ParseLegacyStoreState(root),
            ContentManifest = ParseContentManifest(root),
            FinalStateSha256 = OptionalString(root, "final_state_sha256"),
        };

        if (fixture.FormatVersion != 1)
            throw new InvalidDataException($"Unsupported replay fixture format {fixture.FormatVersion}.");
        if (fixture.TickRateHz == 0)
            throw new InvalidDataException("Replay fixture tick rate must be non-zero.");
        if (fixture.Commands.Count == 0)
            throw new InvalidDataException("Replay fixture must contain commands.");
        if (fixture.Commands.Select(command => command.ClientSequence).Distinct().Count() != fixture.Commands.Count)
            throw new InvalidDataException("Replay fixture command sequences must be unique.");
        if (fixture.ContentManifest is not null) {
            foreach (var commandTick in fixture.Commands.Select(command => command.TargetTick).Distinct()) {
                if (!fixture.Checkpoints.Any(checkpoint => checkpoint.Tick == commandTick))
                    throw new InvalidDataException($"Replay fixture must include a checkpoint at command tick {commandTick}.");
            }
        }

        return fixture;
    }

    private static ReplayContentManifest? ParseContentManifest(JsonElement root)
    {
        if (!root.TryGetProperty("content_manifest", out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Array)
            return null;
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Replay fixture content_manifest must be an object or legacy array.");

        return new ReplayContentManifest(
            RequiredString(value, "id"),
            RequiredString(value, "version"),
            RequiredString(value, "sha256"));
    }

    private static ReplayInitialState ParseInitialState(JsonElement value)
    {
        return new ReplayInitialState(
            RequiredString(value, "player"),
            RequiredUInt(value, "gold"),
            RequiredUInt(value, "experience"),
            RequiredInt(value, "life"),
            value.TryGetProperty("mana", out var mana) ? mana.GetInt32() : 0) {
            CharacterClass = value.TryGetProperty("character_class", out var characterClass) ? characterClass.GetInt32() : 0,
            CharacterLevel = value.TryGetProperty("character_level", out var characterLevel) ? characterLevel.GetByte() : (byte)1,
        };
    }

    private static ReplayLegacyStoreState ParseLegacyStoreState(JsonElement root)
    {
        if (!root.TryGetProperty("legacy_store_state", out var value))
            return new ReplayLegacyStoreState(1, 0, 3, [42]);
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Replay fixture legacy_store_state must be an object.");

        var seeds = value.TryGetProperty("premium_item_seeds", out var seedValues) && seedValues.ValueKind == JsonValueKind.Array
            ? seedValues.EnumerateArray().Select(seed => seed.GetUInt32()).ToArray()
            : throw new InvalidDataException("Replay fixture premium_item_seeds must be an array.");
        return new ReplayLegacyStoreState(
            value.TryGetProperty("active_store", out var activeStore) ? activeStore.GetInt32() : 1,
            value.TryGetProperty("premium_item_count", out var itemCount) ? itemCount.GetInt32() : 0,
            value.TryGetProperty("premium_item_level", out var itemLevel) ? itemLevel.GetInt32() : 3,
            seeds);
    }

    private static IReadOnlyList<ReplayFixtureCommand> ParseCommands(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Replay fixture commands must be an array.");

        var commands = new List<ReplayFixtureCommand>();
        foreach (var command in value.EnumerateArray()) {
            var kind = RequiredString(command, "kind");
            var payload = command.TryGetProperty("payload", out var payloadValue)
                ? payloadValue
                : default;
            var storeId = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("store_id", out var store)
                ? store.GetUInt32()
                : 1U;
            var storeSlot = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("item_index", out var item)
                ? item.GetUInt32()
                : payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("store_slot", out var slot)
                    ? slot.GetUInt32()
                    : 0U;

            if (kind is not ("OpenStore" or "BuyItem"))
                throw new InvalidDataException($"Unsupported replay command kind '{kind}'.");

            commands.Add(new ReplayFixtureCommand(
                RequiredULong(command, "client_sequence"),
                RequiredULong(command, "target_tick"),
                RequiredULong(command, "server_receipt_sequence"),
                kind,
                storeId,
                storeSlot));
        }

        return commands;
    }

    private static IReadOnlyList<ReplayCheckpoint> ParseCheckpoints(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Replay fixture checkpoints must be an array.");

        return value.EnumerateArray()
            .Select(checkpoint => new ReplayCheckpoint(
                RequiredULong(checkpoint, "tick"),
                RequiredString(checkpoint, "state_sha256")))
            .ToArray();
    }

    private static string RequiredString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Replay fixture property '{property}' must be a string.");
        var text = result.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException($"Replay fixture property '{property}' must not be empty.");
        return text;
    }

    private static string? OptionalString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result))
            return null;
        if (result.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Replay fixture property '{property}' must be a string.");
        var text = result.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException($"Replay fixture property '{property}' must not be empty.");
        return text;
    }

    private static int RequiredInt(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != JsonValueKind.Number || !result.TryGetInt32(out var number))
            throw new InvalidDataException($"Replay fixture property '{property}' must be an integer.");
        return number;
    }

    private static uint RequiredUInt(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != JsonValueKind.Number || !result.TryGetUInt32(out var number))
            throw new InvalidDataException($"Replay fixture property '{property}' must be an unsigned integer.");
        return number;
    }

    private static ulong RequiredULong(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != JsonValueKind.Number || !result.TryGetUInt64(out var number))
            throw new InvalidDataException($"Replay fixture property '{property}' must be an unsigned integer.");
        return number;
    }
}
