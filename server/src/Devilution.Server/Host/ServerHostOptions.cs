using System.Globalization;
using System.Net;

namespace Devilution.Server.Host;

/** Command-line configuration for the standalone authoritative server host. */
public sealed record ServerHostOptions(
    IPAddress BindAddress,
    int Port,
    string ContentRoot,
    string ContentId,
    string ContentVersion,
    string BuildId,
    string ProtocolSchemaVersion,
    uint TickRateHz,
    uint StartingGold)
{
    public static ServerHostOptions Defaults { get; } = new(
        IPAddress.Loopback,
        6113,
        Path.Combine("server", "content", "base"),
        "devilution.base",
        "0.1.0",
        "devilution-server",
        "0.1.0",
        20,
        5000);

    public static bool TryParse(IReadOnlyList<string> args, out ServerHostOptions options, out string error)
    {
        ArgumentNullException.ThrowIfNull(args);
        var current = Defaults;
        for (var index = 0; index < args.Count; index++) {
            var argument = args[index];
            if (argument is "--help" or "-h") {
                options = current;
                error = string.Empty;
                return false;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count) {
                options = current;
                error = $"Unknown or incomplete option '{argument}'.";
                return false;
            }

            var value = args[++index];
            switch (argument) {
            case "--bind":
                if (!IPAddress.TryParse(value, out var bindAddress)) {
                    options = current;
                    error = $"'{value}' is not a valid bind address.";
                    return false;
                }
                current = current with { BindAddress = bindAddress };
                break;
            case "--port":
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 0 or > 65535) {
                    options = current;
                    error = $"'{value}' is not a valid TCP port.";
                    return false;
                }
                current = current with { Port = port };
                break;
            case "--content-root":
                current = current with { ContentRoot = value };
                break;
            case "--content-id":
                current = current with { ContentId = value };
                break;
            case "--content-version":
                current = current with { ContentVersion = value };
                break;
            case "--build-id":
                current = current with { BuildId = value };
                break;
            case "--protocol-version":
                current = current with { ProtocolSchemaVersion = value };
                break;
            case "--tick-rate":
                if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var tickRate) || tickRate == 0) {
                    options = current;
                    error = $"'{value}' is not a valid tick rate.";
                    return false;
                }
                current = current with { TickRateHz = tickRate };
                break;
            case "--starting-gold":
                if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var startingGold)) {
                    options = current;
                    error = $"'{value}' is not a valid starting gold amount.";
                    return false;
                }
                current = current with { StartingGold = startingGold };
                break;
            default:
                options = current;
                error = $"Unknown option '{argument}'.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(current.ContentRoot)
            || string.IsNullOrWhiteSpace(current.ContentId)
            || string.IsNullOrWhiteSpace(current.ContentVersion)
            || string.IsNullOrWhiteSpace(current.BuildId)
            || string.IsNullOrWhiteSpace(current.ProtocolSchemaVersion)) {
            options = current;
            error = "Content, build, and protocol identity values cannot be empty.";
            return false;
        }

        options = current;
        error = string.Empty;
        return true;
    }

    public static string Usage => """
        Usage: Devilution.Server [options]

          --bind <address>             Bind address (default: 127.0.0.1)
          --port <port>                TCP port, 0 chooses an available port
          --content-root <directory>   External TSV content root
          --content-id <id>            Content pack identity
          --content-version <version>  Content pack version
          --build-id <id>              Server build identity
          --protocol-version <version> Protocol schema version
          --tick-rate <hz>             Authoritative simulation rate
          --starting-gold <amount>     Initial wallet for new sessions
        """;
}
