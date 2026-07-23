using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Snapshots;

namespace Devilution.Server.Stores;

public sealed record OwnedStoreItem(
    uint StoreId,
    uint StoreSlot,
    uint ItemSeed,
    uint Price,
    ulong PurchasedAtTick,
    AuthoritativeItemState State)
{
    public OwnedStoreItem(uint storeId, uint storeSlot, uint itemSeed, uint price, ulong purchasedAtTick)
        : this(storeId, storeSlot, itemSeed, price, purchasedAtTick, AuthoritativeItemState.Empty)
    {
    }
}

public sealed record PlayerAttributeState(int Base, int Current);

public sealed record PlayerAttributesState(
    PlayerAttributeState Strength,
    PlayerAttributeState Magic,
    PlayerAttributeState Dexterity,
    PlayerAttributeState Vitality)
{
    public static PlayerAttributesState Zero { get; } = new(
        new PlayerAttributeState(0, 0),
        new PlayerAttributeState(0, 0),
        new PlayerAttributeState(0, 0),
        new PlayerAttributeState(0, 0));
}

public sealed record EquippedStoreItem(uint Slot, uint ItemSeed, AuthoritativeItemState State)
{
    public EquippedStoreItem(uint slot, uint itemSeed)
        : this(slot, itemSeed, AuthoritativeItemState.Empty)
    {
    }
}

/** Read-only player state projection for tests and snapshot generation. */
public sealed record StorePlayerSnapshot(
    uint Gold,
    uint? ActiveStoreId,
    IReadOnlyList<OwnedStoreItem> Inventory,
    uint Experience,
    int Life,
    int Mana,
    PlayerAttributesState Attributes,
    IReadOnlyList<EquippedStoreItem> Equipment,
    IReadOnlyList<int> InventoryGrid)
{
    public StorePlayerSnapshot(uint gold, uint? activeStoreId, IReadOnlyList<OwnedStoreItem> inventory)
        : this(gold, activeStoreId, inventory, 0, 0, 0, PlayerAttributesState.Zero, [], [])
    {
    }

    public StorePlayerSnapshot(
        uint gold,
        uint? activeStoreId,
        IReadOnlyList<OwnedStoreItem> inventory,
        uint experience,
        int life,
        int mana)
        : this(gold, activeStoreId, inventory, experience, life, mana, PlayerAttributesState.Zero, [], [])
    {
    }
}

/**
 * Authoritative store command executor for the first gameplay vertical slice.
 *
 * Stock and wallet mutations occur only after every validation succeeds. The
 * outer command server provides command-level deduplication, so this executor
 * is called once even when a purchase is retried.
 */
public sealed class StoreSimulationExecutor : IAuthoritativeCommandExecutor, IAuthoritativeSnapshotProvider
{
    private readonly object synchronization = new();
    private readonly StoreCatalog catalog;
    private readonly uint startingGold;
    private readonly uint startingExperience;
    private readonly int startingLife;
    private readonly int startingMana;
    private readonly PlayerAttributesState startingAttributes;
    private readonly IReadOnlyList<EquippedStoreItem> startingEquipment;
    private readonly IReadOnlyList<int> startingInventoryGrid;
    private readonly IStoreGameplayRules gameplayRules;
    private readonly Dictionary<string, PlayerStoreState> players = new(StringComparer.Ordinal);

    public StoreSimulationExecutor(
        StoreCatalog catalog,
        uint startingGold,
        uint startingExperience = 0,
        int startingLife = 0,
        int startingMana = 0,
        PlayerAttributesState? startingAttributes = null,
        IReadOnlyList<EquippedStoreItem>? startingEquipment = null,
        IReadOnlyList<int>? startingInventoryGrid = null,
        IStoreGameplayRules? gameplayRules = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.startingGold = startingGold;
        this.startingExperience = startingExperience;
        this.startingLife = startingLife;
        this.startingMana = startingMana;
        this.startingAttributes = startingAttributes ?? PlayerAttributesState.Zero;
        this.startingEquipment = startingEquipment?.ToArray() ?? [];
        this.startingInventoryGrid = startingInventoryGrid?.ToArray() ?? [];
        this.gameplayRules = gameplayRules ?? DiabloGameplayModule.Instance;
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
                Command.IntentOneofCase.SellItemRequested => Sell(player, command.SellItemRequested.InventoryIndex),
                Command.IntentOneofCase.RepairItemRequested => Repair(player, command.RepairItemRequested.InventoryIndex),
                Command.IntentOneofCase.RechargeItemRequested => Recharge(player, command.RechargeItemRequested.InventoryIndex),
                Command.IntentOneofCase.IdentifyItemRequested => Identify(player, command.IdentifyItemRequested.InventoryIndex),
                Command.IntentOneofCase.MoveInventoryItemRequested => MoveInventoryItem(
                    player,
                    command.MoveInventoryItemRequested.InventoryIndex,
                    command.MoveInventoryItemRequested.TargetCell),
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
            return new StorePlayerSnapshot(
                player.Gold,
                player.ActiveStoreId,
                player.Inventory.ToArray(),
                player.Experience,
                player.Life,
                player.Mana,
                player.Attributes,
                player.Equipment.ToArray(),
                player.InventoryGrid.ToArray());
        }
    }

    public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));

        lock (synchronization) {
            var state = GetOrCreatePlayer(sessionId);
            var player = new PlayerSnapshot {
                EntityId = entityId,
                Gold = state.Gold,
                ActiveStoreId = state.ActiveStoreId ?? 0,
                Life = state.Life,
                Mana = state.Mana,
                Experience = state.Experience,
                Attributes = new PlayerAttributesSnapshot {
                    Strength = ToSnapshot(state.Attributes.Strength),
                    Magic = ToSnapshot(state.Attributes.Magic),
                    Dexterity = ToSnapshot(state.Attributes.Dexterity),
                    Vitality = ToSnapshot(state.Attributes.Vitality),
                },
            };

            foreach (var item in state.Inventory) {
                player.Inventory.Add(new ItemSnapshot {
                    StoreId = item.StoreId,
                    StoreSlot = item.StoreSlot,
                    ItemSeed = item.ItemSeed,
                    Price = item.Price,
                    PurchasedAtTick = item.PurchasedAtTick,
                    State = ToSnapshot(item.State),
                });
            }

            foreach (var item in state.Equipment) {
                player.Equipment.Add(new EquippedItemSnapshot {
                    Slot = item.Slot,
                    ItemSeed = item.ItemSeed,
                    State = ToSnapshot(item.State),
                });
            }

            player.InventoryGrid.Add(state.InventoryGrid);

            var snapshot = new Snapshot {
                Tick = tick,
                Players = { player },
            };
            if (state.ActiveStoreId is uint storeId) {
                var store = new StoreSnapshot { StoreId = storeId };
                foreach (var item in catalog.GetItems(storeId)) {
                    store.Items.Add(new StoreItemSnapshot {
                        StoreSlot = item.StoreSlot,
                        ItemSeed = item.ItemSeed,
                        Price = item.Price,
                        State = ToSnapshot(item.State),
                    });
                }
                snapshot.ActiveStore = store;
            }

            snapshot.StateSha256 = SnapshotStateHasher.Compute(snapshot);
            return snapshot;
        }
    }

    private static AttributeSnapshot ToSnapshot(PlayerAttributeState attribute)
    {
        return new AttributeSnapshot { Base = attribute.Base, Current = attribute.Current };
    }

    private static ItemStateSnapshot ToSnapshot(AuthoritativeItemState state)
    {
        return new ItemStateSnapshot {
            CreateInfo = state.CreateInfo,
            ItemType = state.ItemType,
            PositionX = state.PositionX,
            PositionY = state.PositionY,
            Deleted = state.Deleted,
            Identified = state.Identified,
            Magical = state.Magical,
            EquipLocation = state.EquipLocation,
            ItemClass = state.ItemClass,
            Value = state.Value,
            IdentifiedValue = state.IdentifiedValue,
            MinDamage = state.MinDamage,
            MaxDamage = state.MaxDamage,
            ArmorClass = state.ArmorClass,
            Flags = state.Flags,
            MiscId = state.MiscId,
            SpellId = state.SpellId,
            ItemIndex = state.ItemIndex,
            Charges = state.Charges,
            MaxCharges = state.MaxCharges,
            Durability = state.Durability,
            MaxDurability = state.MaxDurability,
            PlusDamage = state.PlusDamage,
            PlusToHit = state.PlusToHit,
            PlusArmorClass = state.PlusArmorClass,
            PlusStrength = state.PlusStrength,
            PlusMagic = state.PlusMagic,
            PlusDexterity = state.PlusDexterity,
            PlusVitality = state.PlusVitality,
            PlusFireResistance = state.PlusFireResistance,
            PlusLightningResistance = state.PlusLightningResistance,
            PlusMagicResistance = state.PlusMagicResistance,
            PlusMana = state.PlusMana,
            PlusHitPoints = state.PlusHitPoints,
            PlusDamageModifier = state.PlusDamageModifier,
            PlusGetHit = state.PlusGetHit,
            PlusLight = state.PlusLight,
            SpellLevelAdd = state.SpellLevelAdd,
            UniqueId = state.UniqueId,
            FireMinDamage = state.FireMinDamage,
            FireMaxDamage = state.FireMaxDamage,
            LightningMinDamage = state.LightningMinDamage,
            LightningMaxDamage = state.LightningMaxDamage,
            PlusEnemyArmorClass = state.PlusEnemyArmorClass,
            PrefixPower = state.PrefixPower,
            SuffixPower = state.SuffixPower,
            ValueAdd1 = state.ValueAdd1,
            ValueMultiply1 = state.ValueMultiply1,
            ValueAdd2 = state.ValueAdd2,
            ValueMultiply2 = state.ValueMultiply2,
            MinimumStrength = state.MinimumStrength,
            MinimumMagic = state.MinimumMagic,
            MinimumDexterity = state.MinimumDexterity,
            StatFlag = state.StatFlag,
            HellfireDamageArmorFlags = state.HellfireDamageArmorFlags,
            Buff = state.Buff,
        };
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
        var rejection = gameplayRules.ValidatePurchase(item, player.Gold);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        if (!catalog.RemoveItem(request.StoreId, request.StoreSlot))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        player.Gold -= item.Price;
        player.Inventory.Add(new OwnedStoreItem(request.StoreId, item.StoreSlot, item.ItemSeed, item.Price, appliedTick, item.State));
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Sell(PlayerStoreState player, uint inventoryIndex)
    {
        if (player.ActiveStoreId is null)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (!TryGetInventoryItem(player, inventoryIndex, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        var rejection = gameplayRules.ValidateSale(item);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        player.Gold = checked(player.Gold + gameplayRules.GetSalePrice(item));
        player.Inventory.RemoveAt((int)inventoryIndex);
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Repair(PlayerStoreState player, uint inventoryIndex)
    {
        if (player.ActiveStoreId is null)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (!TryGetInventoryItem(player, inventoryIndex, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        var rejection = gameplayRules.ValidateRepair(item, player.Gold);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        player.Gold -= gameplayRules.GetRepairPrice(item);
        player.Inventory[(int)inventoryIndex] = item with {
            State = item.State with { Durability = item.State.MaxDurability },
        };
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Recharge(PlayerStoreState player, uint inventoryIndex)
    {
        if (player.ActiveStoreId is null)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (!TryGetInventoryItem(player, inventoryIndex, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        var rejection = gameplayRules.ValidateRecharge(item, player.Gold);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        player.Gold -= gameplayRules.GetRechargePrice(item);
        player.Inventory[(int)inventoryIndex] = item with {
            State = item.State with { Charges = item.State.MaxCharges },
        };
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Identify(PlayerStoreState player, uint inventoryIndex)
    {
        if (player.ActiveStoreId is null)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (!TryGetInventoryItem(player, inventoryIndex, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        var rejection = gameplayRules.ValidateIdentification(item, player.Gold);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        player.Gold -= gameplayRules.GetIdentificationPrice(item);
        player.Inventory[(int)inventoryIndex] = item with {
            State = item.State with { Identified = true },
        };
        return CommandExecutionResult.Accepted;
    }

    private static CommandExecutionResult MoveInventoryItem(PlayerStoreState player, uint inventoryIndex, uint targetCell)
    {
        if (!TryGetInventoryItem(player, inventoryIndex, out _))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (targetCell >= player.InventoryGrid.Count)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (player.InventoryGrid[(int)targetCell] >= 0 && player.InventoryGrid[(int)targetCell] != (int)inventoryIndex)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);

        for (var cell = 0; cell < player.InventoryGrid.Count; cell++) {
            if (player.InventoryGrid[cell] == (int)inventoryIndex)
                player.InventoryGrid[cell] = -1;
        }
        player.InventoryGrid[(int)targetCell] = (int)inventoryIndex;
        return CommandExecutionResult.Accepted;
    }

    private static bool TryGetInventoryItem(PlayerStoreState player, uint inventoryIndex, out OwnedStoreItem item)
    {
        if (inventoryIndex < player.Inventory.Count) {
            item = player.Inventory[(int)inventoryIndex];
            return true;
        }

        item = null!;
        return false;
    }

    private PlayerStoreState GetOrCreatePlayer(string sessionId)
    {
        if (!players.TryGetValue(sessionId, out var player)) {
            player = new PlayerStoreState(
                startingGold,
                startingExperience,
                startingLife,
                startingMana,
                startingAttributes,
                startingEquipment,
                startingInventoryGrid);
            players.Add(sessionId, player);
        }

        return player;
    }

    private sealed class PlayerStoreState(
        uint gold,
        uint experience,
        int life,
        int mana,
        PlayerAttributesState attributes,
        IReadOnlyList<EquippedStoreItem> equipment,
        IReadOnlyList<int> inventoryGrid)
    {
        public uint Gold { get; set; } = gold;

        public uint Experience { get; } = experience;

        public int Life { get; } = life;

        public int Mana { get; } = mana;

        public PlayerAttributesState Attributes { get; } = attributes;

        public List<EquippedStoreItem> Equipment { get; } = equipment.ToList();

        public List<int> InventoryGrid { get; } = inventoryGrid.ToList();

        public uint? ActiveStoreId { get; set; }

        public List<OwnedStoreItem> Inventory { get; } = [];
    }
}
