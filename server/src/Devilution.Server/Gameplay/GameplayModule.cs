using System.Security.Cryptography;
using System.Text;
using Devilution.Protocol.V1;
using Devilution.Server.Content;
using Devilution.Server.Simulation;
using Devilution.Server.Stores;

namespace Devilution.Server.Gameplay;

/** Stable identity of one server-loaded C# gameplay module. */
public sealed record GameplayModuleIdentity
{
    public GameplayModuleIdentity(string id, string version, string assemblySha256)
        : this(id, version, assemblySha256, [])
    {
    }

    public GameplayModuleIdentity(string id, string version, string assemblySha256, IReadOnlyList<string>? dependencies)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(assemblySha256))
            throw new ArgumentException("Gameplay module IDs, versions, and assembly hashes are required.");
        Id = id;
        Version = version;
        AssemblySha256 = assemblySha256;
        Dependencies = dependencies?.ToArray() ?? [];
        if (Dependencies.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Gameplay module dependencies must be named.", nameof(dependencies));
    }

    public string Id { get; }

    public string Version { get; }

    public string AssemblySha256 { get; }

    public IReadOnlyList<string> Dependencies { get; }

    public static string BuiltInAssemblyHash(string moduleId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"builtin:{moduleId}:{version}"))).ToLowerInvariant();
    }
}

public sealed record GameplayModuleContext(ContentManifest Content, IAuthoritativeClock Clock);

public interface IGameplayModule
{
    GameplayModuleIdentity Identity { get; }

    void Register(GameplayModuleRegistry registry, GameplayModuleContext context);
}

public interface IStoreGameplayRules
{
    CommandRejectReason? ValidatePurchase(StoreItem item, uint playerGold);

    CommandRejectReason? ValidateSale(OwnedStoreItem item);

    CommandRejectReason? ValidateRepair(OwnedStoreItem item, uint playerGold);

    CommandRejectReason? ValidateRecharge(OwnedStoreItem item, uint playerGold);

    CommandRejectReason? ValidateIdentification(OwnedStoreItem item, uint playerGold);

    uint GetSalePrice(OwnedStoreItem item);

    uint GetRepairPrice(OwnedStoreItem item);

    uint GetRechargePrice(OwnedStoreItem item);

    uint GetIdentificationPrice(OwnedStoreItem item);
}

/** Registry for explicit, deterministic module registrations. */
public sealed class GameplayModuleRegistry
{
    private readonly Dictionary<string, IStoreGameplayRules> storeRules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<Command>> commandHandlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<TsvTable>> dataValidators = new(StringComparer.Ordinal);
    private readonly List<IGameplayModule> modules = [];

    public IReadOnlyList<IGameplayModule> Modules => modules;

    public IStoreGameplayRules? StoreRules { get; private set; }

    public IReadOnlyDictionary<string, Action<Command>> CommandHandlers => commandHandlers;

    public IReadOnlyDictionary<string, Action<TsvTable>> DataValidators => dataValidators;

    public void Load(IEnumerable<IGameplayModule> candidates, GameplayModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);
        if (modules.Count != 0)
            throw new InvalidOperationException("Gameplay modules have already been loaded.");

        var pending = candidates.ToArray();
        if (pending.Any(module => module is null))
            throw new ArgumentException("Gameplay module instances cannot be null.", nameof(candidates));
        if (pending.Select(module => module.Identity.Id).Distinct(StringComparer.Ordinal).Count() != pending.Length)
            throw new InvalidOperationException("Gameplay module IDs must be unique.");

        while (modules.Count != pending.Length) {
            var next = pending.FirstOrDefault(module => !modules.Any(loaded => loaded.Identity.Id == module.Identity.Id)
                && module.Identity.Dependencies.All(dependency => modules.Any(loaded => loaded.Identity.Id == dependency)));
            if (next is null) {
                var unresolved = pending
                    .Where(module => !modules.Contains(module))
                    .Select(module => module.Identity.Id)
                    .ToArray();
                throw new InvalidOperationException($"Gameplay module dependencies cannot be resolved: {string.Join(", ", unresolved)}.");
            }

            next.Register(this, context);
            modules.Add(next);
        }
    }

    public void RegisterStoreRules(string moduleId, IStoreGameplayRules rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(rules);
        if (!storeRules.TryAdd(moduleId, rules))
            throw new InvalidOperationException($"Gameplay module '{moduleId}' registered store rules more than once.");
        if (StoreRules is not null)
            throw new InvalidOperationException("Only one gameplay module may own store rules.");
        StoreRules = rules;
    }

    public void RegisterCommandHandler(string commandKind, Action<Command> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKind);
        ArgumentNullException.ThrowIfNull(handler);
        if (!commandHandlers.TryAdd(commandKind, handler))
            throw new InvalidOperationException($"A gameplay command handler for '{commandKind}' is already registered.");
    }

    public void RegisterDataValidator(string tableName, Action<TsvTable> validator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(validator);
        if (!dataValidators.TryAdd(tableName, validator))
            throw new InvalidOperationException($"A gameplay data validator for '{tableName}' is already registered.");
    }
}

/** Combined identity advertised by the protocol and recorded by replays. */
public sealed class GameplayRulesetIdentity
{
    public GameplayRulesetIdentity(ContentManifest content, IEnumerable<GameplayModuleIdentity> modules)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ArgumentNullException.ThrowIfNull(modules);
        Modules = modules.ToArray();
        if (Modules.Count == 0)
            throw new ArgumentException("A gameplay ruleset must contain a module.", nameof(modules));
        CombinedSha256 = ComputeHash();
    }

    public ContentManifest Content { get; }

    public IReadOnlyList<GameplayModuleIdentity> Modules { get; }

    public string CombinedSha256 { get; }

    private string ComputeHash()
    {
        var hasher = new Devilution.Server.Snapshots.CanonicalStateHasher();
        hasher.AppendString(Content.Sha256);
        hasher.AppendUInt64((ulong)Modules.Count);
        foreach (var module in Modules) {
            hasher.AppendString(module.Id);
            hasher.AppendString(module.Version);
            hasher.AppendString(module.AssemblySha256);
            hasher.AppendUInt64((ulong)module.Dependencies.Count);
            foreach (var dependency in module.Dependencies)
                hasher.AppendString(dependency);
        }
        return hasher.HexDigest();
    }
}

/** First-party Diablo store rules; more authoritative behaviors will move here. */
public sealed class DiabloGameplayModule : IGameplayModule, IStoreGameplayRules
{
    public static DiabloGameplayModule Instance { get; } = new();

    public GameplayModuleIdentity Identity { get; } = new(
        "devilution.diablo",
        "0.1.0",
        GameplayModuleIdentity.BuiltInAssemblyHash("devilution.diablo", "0.1.0"));

    public void Register(GameplayModuleRegistry registry, GameplayModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);
        registry.RegisterStoreRules(Identity.Id, this);
    }

    public CommandRejectReason? ValidatePurchase(StoreItem item, uint playerGold)
    {
        if (item.State.Deleted)
            return CommandRejectReason.InvalidTarget;
        if (playerGold < item.Price)
            return CommandRejectReason.InsufficientResources;
        return null;
    }

    public CommandRejectReason? ValidateSale(OwnedStoreItem item)
    {
        return item.State.Deleted ? CommandRejectReason.InvalidTarget : null;
    }

    public CommandRejectReason? ValidateRepair(OwnedStoreItem item, uint playerGold)
    {
        if (item.State.Deleted)
            return CommandRejectReason.InvalidTarget;
        if (item.State.MaxDurability <= item.State.Durability)
            return CommandRejectReason.NotAllowed;
        return playerGold < GetRepairPrice(item) ? CommandRejectReason.InsufficientResources : null;
    }

    public CommandRejectReason? ValidateRecharge(OwnedStoreItem item, uint playerGold)
    {
        if (item.State.Deleted)
            return CommandRejectReason.InvalidTarget;
        if (item.State.MaxCharges <= item.State.Charges)
            return CommandRejectReason.NotAllowed;
        return playerGold < GetRechargePrice(item) ? CommandRejectReason.InsufficientResources : null;
    }

    public CommandRejectReason? ValidateIdentification(OwnedStoreItem item, uint playerGold)
    {
        if (item.State.Deleted)
            return CommandRejectReason.InvalidTarget;
        if (item.State.Identified)
            return CommandRejectReason.NotAllowed;
        return playerGold < GetIdentificationPrice(item) ? CommandRejectReason.InsufficientResources : null;
    }

    public uint GetSalePrice(OwnedStoreItem item) => Math.Max(1U, item.Price / 2);

    public uint GetRepairPrice(OwnedStoreItem item)
    {
        var unitPrice = Math.Max(1U, item.Price / 10);
        return checked(unitPrice * (uint)(item.State.MaxDurability - item.State.Durability));
    }

    public uint GetRechargePrice(OwnedStoreItem item)
    {
        var unitPrice = Math.Max(1U, item.Price / (uint)Math.Max(1, item.State.MaxCharges));
        return checked(unitPrice * (uint)(item.State.MaxCharges - item.State.Charges));
    }

    public uint GetIdentificationPrice(OwnedStoreItem item) => Math.Max(1U, item.Price / 10);
}
