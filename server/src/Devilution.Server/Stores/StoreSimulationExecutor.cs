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

public sealed record BeltStoreItem(uint Slot, uint ItemSeed, AuthoritativeItemState State)
{
    public BeltStoreItem(uint slot, uint itemSeed)
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
    int ManaMaximum,
    PlayerAttributesState Attributes,
    IReadOnlyList<EquippedStoreItem> Equipment,
    IReadOnlyList<int> InventoryGrid,
    IReadOnlyList<BeltStoreItem> Belt)
{
    public StorePlayerSnapshot(uint gold, uint? activeStoreId, IReadOnlyList<OwnedStoreItem> inventory)
        : this(gold, activeStoreId, inventory, 0, 0, 0, 0, PlayerAttributesState.Zero, [], [], [])
    {
    }

    public StorePlayerSnapshot(
        uint gold,
        uint? activeStoreId,
        IReadOnlyList<OwnedStoreItem> inventory,
        uint experience,
        int life,
        int mana)
        : this(gold, activeStoreId, inventory, experience, life, mana, mana, PlayerAttributesState.Zero, [], [], [])
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
    /** Reserved service-only store used by Adria's mana refill action. */
    public const uint AdriaStoreId = 10;
    private readonly object synchronization = new();
    private readonly StoreCatalog catalog;
    private readonly uint startingGold;
    private readonly uint startingExperience;
    private readonly int startingLife;
    private readonly int startingMana;
    private readonly int startingManaMaximum;
    private readonly PlayerAttributesState startingAttributes;
    private readonly IReadOnlyList<EquippedStoreItem> startingEquipment;
    private readonly IReadOnlyList<int> startingInventoryGrid;
    private readonly IReadOnlyList<BeltStoreItem> startingBelt;
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
        IStoreGameplayRules? gameplayRules = null,
        IReadOnlyList<BeltStoreItem>? startingBelt = null,
        int? startingManaMaximum = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.startingGold = startingGold;
        this.startingExperience = startingExperience;
        this.startingLife = startingLife;
        this.startingMana = startingMana;
        this.startingManaMaximum = Math.Max(startingMana, startingManaMaximum ?? startingMana);
        this.startingAttributes = startingAttributes ?? PlayerAttributesState.Zero;
        this.startingEquipment = startingEquipment?.ToArray() ?? [];
        this.startingInventoryGrid = startingInventoryGrid?.ToArray() ?? [];
        this.startingBelt = startingBelt?.ToArray() ?? [];
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
                Command.IntentOneofCase.SellItemRequested => Sell(player, command.SellItemRequested.Item, command.SellItemRequested.InventoryIndex),
                Command.IntentOneofCase.RepairItemRequested => Repair(player, command.RepairItemRequested.Item, command.RepairItemRequested.InventoryIndex),
                Command.IntentOneofCase.RechargeItemRequested => Recharge(player, command.RechargeItemRequested.Item, command.RechargeItemRequested.InventoryIndex),
                Command.IntentOneofCase.IdentifyItemRequested => Identify(player, command.IdentifyItemRequested.Item, command.IdentifyItemRequested.InventoryIndex),
                Command.IntentOneofCase.MoveInventoryItemRequested => MoveInventoryItem(
                    player,
                    command.MoveInventoryItemRequested.InventoryIndex,
                    command.MoveInventoryItemRequested.TargetCell),
                Command.IntentOneofCase.MoveItemRequested => MoveItem(player, command.MoveItemRequested),
                Command.IntentOneofCase.RefillManaRequested => RefillMana(player),
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
                player.ManaMaximum,
                player.Attributes,
                player.Equipment.ToArray(),
                player.InventoryGrid.ToArray(),
                player.Belt.ToArray());
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
                ManaMaximum = state.ManaMaximum,
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

            foreach (var item in state.Belt) {
                player.Belt.Add(new BeltItemSnapshot {
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
        if (storeId != AdriaStoreId && !catalog.ContainsStore(storeId))
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

    private CommandExecutionResult Sell(PlayerStoreState player, PlayerItemReference? reference, uint inventoryIndex)
    {
        if (player.ActiveStoreId is null)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (!TryGetItem(player, reference, inventoryIndex, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        var rejection = gameplayRules.ValidateSale(item);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        player.Gold = checked(player.Gold + gameplayRules.GetSalePrice(item));
        RemoveItem(player, reference, inventoryIndex);
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Repair(PlayerStoreState player, PlayerItemReference? reference, uint inventoryIndex)
    {
        if (player.ActiveStoreId is null)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (!TryGetItem(player, reference, inventoryIndex, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        var rejection = gameplayRules.ValidateRepair(item, player.Gold);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        player.Gold -= gameplayRules.GetRepairPrice(item);
        ReplaceItem(player, reference, inventoryIndex, item with { State = item.State with { Durability = item.State.MaxDurability } });
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Recharge(PlayerStoreState player, PlayerItemReference? reference, uint inventoryIndex)
    {
        if (player.ActiveStoreId is null)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (!TryGetItem(player, reference, inventoryIndex, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        var rejection = gameplayRules.ValidateRecharge(item, player.Gold);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        player.Gold -= gameplayRules.GetRechargePrice(item);
        ReplaceItem(player, reference, inventoryIndex, item with { State = item.State with { Charges = item.State.MaxCharges } });
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Identify(PlayerStoreState player, PlayerItemReference? reference, uint inventoryIndex)
    {
        if (player.ActiveStoreId is null)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (!TryGetItem(player, reference, inventoryIndex, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        var rejection = gameplayRules.ValidateIdentification(item, player.Gold);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        player.Gold -= gameplayRules.GetIdentificationPrice(item);
        ReplaceItem(player, reference, inventoryIndex, item with { State = item.State with { Identified = true } });
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult RefillMana(PlayerStoreState player)
    {
        if (player.ActiveStoreId != AdriaStoreId)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        var rejection = gameplayRules.ValidateManaRefill(player.Mana, player.ManaMaximum, player.Gold);
        if (rejection.HasValue)
            return CommandExecutionResult.Rejected(rejection.Value);

        player.Gold -= gameplayRules.GetManaRefillPrice(player.Mana, player.ManaMaximum);
        player.Mana = player.ManaMaximum;
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

    private static CommandExecutionResult MoveItem(PlayerStoreState player, MoveItemRequested request)
    {
        if (!TryGetTransfer(player, request.Item, out var source))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (!IsValidDestination(player, request.Destination))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (SameLocation(request.Item, request.Destination))
            return CommandExecutionResult.Accepted;
        if (DestinationOccupied(player, request.Destination))
            return SwapTransfers(player, request.Item, request.Destination, source);

        RemoveTransfer(player, request.Item);
        AddTransfer(player, request.Destination, source);
        return CommandExecutionResult.Accepted;
    }

    private static CommandExecutionResult SwapTransfers(
        PlayerStoreState player,
        PlayerItemReference sourceReference,
        PlayerItemReference destinationReference,
        TransferItem source)
    {
        if (!TryGetDestinationInventoryReference(player, destinationReference, out var actualDestinationReference)
            || !TryGetTransfer(player, actualDestinationReference, out var destination))
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);

        if (sourceReference.Location == PlayerItemLocation.Inventory && actualDestinationReference.Location == PlayerItemLocation.Inventory) {
            var sourceIndex = (int)sourceReference.Slot;
            var destinationIndex = (int)actualDestinationReference.Slot;
            player.Inventory[sourceIndex] = new OwnedStoreItem(destination.StoreId, destination.StoreSlot, destination.ItemSeed, destination.Price, destination.PurchasedAtTick, destination.State);
            player.Inventory[destinationIndex] = new OwnedStoreItem(source.StoreId, source.StoreSlot, source.ItemSeed, source.Price, source.PurchasedAtTick, source.State);
            for (var cell = 0; cell < player.InventoryGrid.Count; cell++) {
                if (player.InventoryGrid[cell] == sourceIndex)
                    player.InventoryGrid[cell] = destinationIndex;
                else if (player.InventoryGrid[cell] == destinationIndex)
                    player.InventoryGrid[cell] = sourceIndex;
            }
            return CommandExecutionResult.Accepted;
        }

        ReplaceTransfer(player, sourceReference, destination);
        ReplaceTransfer(player, actualDestinationReference, source);
        return CommandExecutionResult.Accepted;
    }

    private static bool TryGetDestinationInventoryReference(PlayerStoreState player, PlayerItemReference destination, out PlayerItemReference actual)
    {
        if (destination.Location != PlayerItemLocation.Inventory) {
            actual = destination;
            return true;
        }

        if (destination.Slot >= player.InventoryGrid.Count || player.InventoryGrid[(int)destination.Slot] < 0) {
            actual = destination;
            return false;
        }

        actual = new PlayerItemReference {
            Location = PlayerItemLocation.Inventory,
            Slot = (uint)player.InventoryGrid[(int)destination.Slot],
        };
        return true;
    }

    private static void ReplaceTransfer(PlayerStoreState player, PlayerItemReference reference, TransferItem item)
    {
        if (reference.Location == PlayerItemLocation.Inventory) {
            player.Inventory[(int)reference.Slot] = new OwnedStoreItem(item.StoreId, item.StoreSlot, item.ItemSeed, item.Price, item.PurchasedAtTick, item.State);
        } else if (reference.Location == PlayerItemLocation.Belt) {
            var index = player.Belt.FindIndex(candidate => candidate.Slot == reference.Slot);
            player.Belt[index] = new BeltStoreItem(reference.Slot, item.ItemSeed, item.State);
        } else {
            var index = player.Equipment.FindIndex(candidate => candidate.Slot == reference.Slot);
            player.Equipment[index] = new EquippedStoreItem(reference.Slot, item.ItemSeed, item.State);
        }
    }

    private static bool TryGetTransfer(PlayerStoreState player, PlayerItemReference reference, out TransferItem item)
    {
        switch (reference.Location) {
        case PlayerItemLocation.Inventory when reference.Slot < player.Inventory.Count:
            var owned = player.Inventory[(int)reference.Slot];
            item = new TransferItem(owned.ItemSeed, owned.State, owned.StoreId, owned.StoreSlot, owned.Price, owned.PurchasedAtTick);
            return true;
        case PlayerItemLocation.Belt:
            var belt = player.Belt.FirstOrDefault(candidate => candidate.Slot == reference.Slot);
            if (belt is not null) {
                item = new TransferItem(belt.ItemSeed, belt.State, 0, belt.Slot, GetReferencePrice(belt.State), 0);
                return true;
            }
            break;
        case PlayerItemLocation.Equipment:
            var equipment = player.Equipment.FirstOrDefault(candidate => candidate.Slot == reference.Slot);
            if (equipment is not null) {
                item = new TransferItem(equipment.ItemSeed, equipment.State, 0, equipment.Slot, GetReferencePrice(equipment.State), 0);
                return true;
            }
            break;
        }

        item = default;
        return false;
    }

    private static bool IsValidDestination(PlayerStoreState player, PlayerItemReference reference)
    {
        return reference.Location switch {
            PlayerItemLocation.Inventory => reference.Slot < player.InventoryGrid.Count,
            PlayerItemLocation.Belt => reference.Slot < 8,
            PlayerItemLocation.Equipment => reference.Slot < 8,
            _ => false,
        };
    }

    private static bool DestinationOccupied(PlayerStoreState player, PlayerItemReference reference)
    {
        return reference.Location switch {
            PlayerItemLocation.Inventory => player.InventoryGrid[(int)reference.Slot] >= 0,
            PlayerItemLocation.Belt => player.Belt.Any(item => item.Slot == reference.Slot),
            PlayerItemLocation.Equipment => player.Equipment.Any(item => item.Slot == reference.Slot),
            _ => true,
        };
    }

    private static bool SameLocation(PlayerItemReference source, PlayerItemReference destination)
    {
        return source.Location == destination.Location && source.Slot == destination.Slot;
    }

    private static void RemoveTransfer(PlayerStoreState player, PlayerItemReference reference)
    {
        if (reference.Location == PlayerItemLocation.Inventory) {
            player.Inventory.RemoveAt((int)reference.Slot);
            for (var cell = 0; cell < player.InventoryGrid.Count; cell++) {
                if (player.InventoryGrid[cell] == reference.Slot)
                    player.InventoryGrid[cell] = -1;
                else if (player.InventoryGrid[cell] > reference.Slot)
                    player.InventoryGrid[cell]--;
            }
        } else if (reference.Location == PlayerItemLocation.Belt) {
            player.Belt.RemoveAll(item => item.Slot == reference.Slot);
        } else {
            player.Equipment.RemoveAll(item => item.Slot == reference.Slot);
        }
    }

    private static void AddTransfer(PlayerStoreState player, PlayerItemReference destination, TransferItem item)
    {
        if (destination.Location == PlayerItemLocation.Inventory) {
            var index = player.Inventory.Count;
            player.Inventory.Add(new OwnedStoreItem(item.StoreId, item.StoreSlot, item.ItemSeed, item.Price, item.PurchasedAtTick, item.State));
            player.InventoryGrid[(int)destination.Slot] = index;
        } else if (destination.Location == PlayerItemLocation.Belt) {
            player.Belt.Add(new BeltStoreItem(destination.Slot, item.ItemSeed, item.State));
        } else {
            player.Equipment.Add(new EquippedStoreItem(destination.Slot, item.ItemSeed, item.State));
        }
    }

    private readonly record struct TransferItem(uint ItemSeed, AuthoritativeItemState State, uint StoreId, uint StoreSlot, uint Price, ulong PurchasedAtTick);

    private static bool TryGetInventoryItem(PlayerStoreState player, uint inventoryIndex, out OwnedStoreItem item)
    {
        if (inventoryIndex < player.Inventory.Count) {
            item = player.Inventory[(int)inventoryIndex];
            return true;
        }

        item = null!;
        return false;
    }

    private static bool TryGetItem(PlayerStoreState player, PlayerItemReference? reference, uint inventoryIndex, out OwnedStoreItem item)
    {
        if (reference is null)
            return TryGetInventoryItem(player, inventoryIndex, out item);

        switch (reference.Location) {
        case PlayerItemLocation.Inventory:
            return TryGetInventoryItem(player, reference.Slot, out item);
        case PlayerItemLocation.Belt:
            var beltItem = player.Belt.FirstOrDefault(candidate => candidate.Slot == reference.Slot);
            if (beltItem is not null) {
                    item = new OwnedStoreItem(0, beltItem.Slot, beltItem.ItemSeed, GetReferencePrice(beltItem.State), 0, beltItem.State);
                return true;
            }
            break;
        case PlayerItemLocation.Equipment:
            var equippedItem = player.Equipment.FirstOrDefault(candidate => candidate.Slot == reference.Slot);
            if (equippedItem is not null) {
                item = new OwnedStoreItem(0, equippedItem.Slot, equippedItem.ItemSeed, GetReferencePrice(equippedItem.State), 0, equippedItem.State);
                return true;
            }
            break;
        default:
            break;
        }

        item = null!;
        return false;
    }

    private static void ReplaceItem(PlayerStoreState player, PlayerItemReference? reference, uint inventoryIndex, OwnedStoreItem item)
    {
        if (reference is null || reference.Location == PlayerItemLocation.Inventory) {
            var index = reference is null ? inventoryIndex : reference.Slot;
            player.Inventory[(int)index] = item;
            return;
        }

        if (reference.Location == PlayerItemLocation.Belt) {
            var index = player.Belt.FindIndex(candidate => candidate.Slot == reference.Slot);
            if (index >= 0)
                player.Belt[index] = new BeltStoreItem(reference.Slot, item.ItemSeed, item.State);
            return;
        }

        var equipmentIndex = player.Equipment.FindIndex(candidate => candidate.Slot == reference.Slot);
        if (equipmentIndex >= 0)
            player.Equipment[equipmentIndex] = new EquippedStoreItem(reference.Slot, item.ItemSeed, item.State);
    }

    private static void RemoveItem(PlayerStoreState player, PlayerItemReference? reference, uint inventoryIndex)
    {
        if (reference is null || reference.Location == PlayerItemLocation.Inventory) {
            var index = (int)(reference is null ? inventoryIndex : reference.Slot);
            player.Inventory.RemoveAt(index);
            for (var cell = 0; cell < player.InventoryGrid.Count; cell++) {
                if (player.InventoryGrid[cell] == index)
                    player.InventoryGrid[cell] = -1;
                else if (player.InventoryGrid[cell] > index)
                    player.InventoryGrid[cell]--;
            }
            return;
        }

        if (reference.Location == PlayerItemLocation.Belt) {
            player.Belt.RemoveAll(candidate => candidate.Slot == reference.Slot);
            return;
        }

        player.Equipment.RemoveAll(candidate => candidate.Slot == reference.Slot);
    }

    private static uint GetReferencePrice(AuthoritativeItemState state)
    {
        var value = state.Identified && state.IdentifiedValue > 0 ? state.IdentifiedValue : state.Value;
        return (uint)Math.Max(1, value);
    }

    private PlayerStoreState GetOrCreatePlayer(string sessionId)
    {
        if (!players.TryGetValue(sessionId, out var player)) {
            player = new PlayerStoreState(
                startingGold,
                startingExperience,
                startingLife,
                startingMana,
                startingManaMaximum,
                startingAttributes,
                startingEquipment,
                startingInventoryGrid,
                startingBelt);
            players.Add(sessionId, player);
        }

        return player;
    }

    private sealed class PlayerStoreState(
        uint gold,
        uint experience,
        int life,
        int mana,
        int manaMaximum,
        PlayerAttributesState attributes,
        IReadOnlyList<EquippedStoreItem> equipment,
        IReadOnlyList<int> inventoryGrid,
        IReadOnlyList<BeltStoreItem> belt)
    {
        public uint Gold { get; set; } = gold;

        public uint Experience { get; } = experience;

        public int Life { get; } = life;

        public int Mana { get; set; } = mana;

        public int ManaMaximum { get; } = Math.Max(manaMaximum, 0);

        public PlayerAttributesState Attributes { get; } = attributes;

        public List<EquippedStoreItem> Equipment { get; } = equipment.ToList();

        public List<BeltStoreItem> Belt { get; } = belt.ToList();

        public List<int> InventoryGrid { get; } = inventoryGrid.ToList();

        public uint? ActiveStoreId { get; set; }

        public List<OwnedStoreItem> Inventory { get; } = [];
    }
}
