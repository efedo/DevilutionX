namespace Devilution.Client.Protocol;

/** Connection identity and retry settings shared by Godot presentation code. */
public sealed record ClientConnectionOptions(
    string Host,
    int Port,
    string ClientBuildId,
    string ProtocolSchemaVersion,
    string ContentManifestHash,
    string RulesetIdentityHash = "")
{
    public string ResumeToken { get; init; } = string.Empty;

    public TimeSpan MinimumRetryTimeout { get; init; } = TimeSpan.FromMilliseconds(150);

    public TimeSpan MaximumRetryTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan SnapshotPollInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    public static ClientConnectionOptions FromEnvironment()
    {
        return new ClientConnectionOptions(
            Environment.GetEnvironmentVariable("DEVILUTION_SERVER_HOST") ?? "127.0.0.1",
            ParsePort(Environment.GetEnvironmentVariable("DEVILUTION_SERVER_PORT")),
            Environment.GetEnvironmentVariable("DEVILUTION_CLIENT_BUILD") ?? "godot-client-dev",
            Environment.GetEnvironmentVariable("DEVILUTION_PROTOCOL_VERSION") ?? "0.1.0",
            Environment.GetEnvironmentVariable("DEVILUTION_CONTENT_HASH") ?? string.Empty,
            Environment.GetEnvironmentVariable("DEVILUTION_RULESET_HASH") ?? string.Empty) {
            ResumeToken = Environment.GetEnvironmentVariable("DEVILUTION_RESUME_TOKEN") ?? string.Empty,
        };
    }

    private static int ParsePort(string? value)
    {
        return int.TryParse(value, out var port) && port is > 0 and <= 65535 ? port : 6113;
    }
}
