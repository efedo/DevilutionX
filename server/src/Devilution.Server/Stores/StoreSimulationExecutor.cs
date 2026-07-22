using Devilution.Protocol.V1;
using Devilution.Server.Commands;

namespace Devilution.Server.Stores;

public sealed record OwnedStoreItem(uint StoreId, uint StoreSlot, uint ItemSeed, uint Price, ulong PurchasedAtTick);

/** Read-only player state projection for tests and the future snapshot layer. */
public sealed record StorePlayerSnapshot(uint Gold, uint? ActiveStoreId, IReadOnlyList<OwnedStoreItem> Inventory);

/**
 * Authoritative store command executor for the first gameplay vertical slice.
 *
 * Stock and wallet mutations occur only after every validation succeeds. The
 * outer command server provides command-level deduplication, so this executor
 * is called once even when a purchase is retried.
 */
public sealed class StoreSimulationExecutor : IAuthoritativeCommandExecutor
{
    private readonly object synchronization = new();
    private readonly StoreCatalog catalog;
    private readonly uint startingGold;
    private readonly Dictionary<string, PlayerStoreState> players = new(StringComparer.Ordinal);

    public StoreSimulationExecutor(StoreCatalog catalog, uint startingGold)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.startingGold = startingGold;
    }

    public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(sessionId))
            return CommandExecutionResult.Rejected(CommandRejectReason.Malformed);

        lock (synchronization) {
            var player = GetOrCreatePlayer(sessionId);
            return command.IntentCase switch {
                Command.IntentOneofCase.OpenStoreRequested => OpenStore(player, command.OpenStoreRequested.StoreId),
                Command.IntentOneofCase.PurchaseRequested => Purchase(player, command.PurchaseRequested, appliedTick),
                _ => CommandExecutionResult.Rejected(CommandRejectReason.Malformed),
            };
        }
    }

    public StorePlayerSnapshot GetPlayerState(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));

        lock (synchronization) {
            var player = GetOrCreatePlayer(sessionId);
            return new StorePlayerSnapshot(player.Gold, player.ActiveStoreId, player.Inventory.ToArray());
        }
    }

    private CommandExecutionResult OpenStore(PlayerStoreState player, uint storeId)
    {
        if (!catalog.ContainsStore(storeId))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        player.ActiveStoreId = storeId;
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Purchase(PlayerStoreState player, PurchaseRequested request, ulong appliedTick)
    {
        if (player.ActiveStoreId != request.StoreId)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (!catalog.TryGetItem(request.StoreId, request.StoreSlot, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (player.Gold < item.Price)
            return CommandExecutionResult.Rejected(CommandRejectReason.InsufficientResources);

        if (!catalog.RemoveItem(request.StoreId, request.StoreSlot))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        player.Gold -= item.Price;
        player.Inventory.Add(new OwnedStoreItem(request.StoreId, item.StoreSlot, item.ItemSeed, item.Price, appliedTick));
        return CommandExecutionResult.Accepted;
    }

    private PlayerStoreState GetOrCreatePlayer(string sessionId)
    {
        if (!players.TryGetValue(sessionId, out var player)) {
            player = new PlayerStoreState(startingGold);
            players.Add(sessionId, player);
        }

        return player;
    }

    private sealed class PlayerStoreState(uint gold)
    {
        public uint Gold { get; set; } = gold;

        public uint? ActiveStoreId { get; set; }

        public List<OwnedStoreItem> Inventory { get; } = [];
    }
}
