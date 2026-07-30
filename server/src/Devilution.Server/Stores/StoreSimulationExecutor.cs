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
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int LifeMaximum { get; init; }
    public uint CharacterLevel { get; init; } = 1;

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
public sealed class StoreSimulationExecutor : IAuthoritativeCommandExecutor, IAuthoritativeSnapshotProvider, IAuthoritativeEventProvider
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
    private readonly int startingPositionX;
    private readonly int startingPositionY;
    private readonly int startingLifeMaximum;
    private readonly uint startingCharacterLevel;
    private readonly int worldWidth;
    private readonly int worldHeight;
    private readonly PlayerAttributesState startingAttributes;
    private readonly IReadOnlyList<EquippedStoreItem> startingEquipment;
    private readonly IReadOnlyList<int> startingInventoryGrid;
    private readonly IReadOnlyList<BeltStoreItem> startingBelt;
    private readonly IStoreGameplayRules gameplayRules;
    private readonly Dictionary<uint, AuthoritativeCombatTarget> combatTargets = new();
    private readonly Dictionary<string, List<PendingGameplayEvent>> pendingEvents = new(StringComparer.Ordinal);
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
        int? startingManaMaximum = null,
        int startingPositionX = 0,
        int startingPositionY = 0,
        int? startingLifeMaximum = null,
        uint startingCharacterLevel = 1,
        IReadOnlyList<AuthoritativeCombatTarget>? startingCombatTargets = null,
        int worldWidth = 40,
        int worldHeight = 40)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.startingGold = startingGold;
        this.startingExperience = startingExperience;
        this.startingLife = startingLife;
        this.startingMana = startingMana;
        this.startingManaMaximum = Math.Max(startingMana, startingManaMaximum ?? startingMana);
        this.startingPositionX = startingPositionX;
        this.startingPositionY = startingPositionY;
        this.startingLifeMaximum = Math.Max(startingLife, startingLifeMaximum ?? startingLife);
        this.startingCharacterLevel = Math.Clamp(startingCharacterLevel, 1U, 50U);
        this.worldWidth = worldWidth > 0 ? worldWidth : throw new ArgumentOutOfRangeException(nameof(worldWidth));
        this.worldHeight = worldHeight > 0 ? worldHeight : throw new ArgumentOutOfRangeException(nameof(worldHeight));
        this.startingAttributes = startingAttributes ?? PlayerAttributesState.Zero;
        this.startingEquipment = startingEquipment?.ToArray() ?? [];
        this.startingInventoryGrid = startingInventoryGrid?.ToArray() ?? [];
        this.startingBelt = startingBelt?.ToArray() ?? [];
        this.gameplayRules = gameplayRules ?? DiabloGameplayModule.Instance;
        foreach (var target in startingCombatTargets ?? [])
            combatTargets.Add(target.EntityId, target);
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
                Command.IntentOneofCase.MoveRequested => Move(player, command.MoveRequested),
                Command.IntentOneofCase.CastRequested => Cast(player, command.CastRequested),
                Command.IntentOneofCase.AttackRequested => Attack(sessionId, player, command.AttackRequested, appliedTick),
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
                player.Belt.ToArray()) {
                PositionX = player.PositionX,
                PositionY = player.PositionY,
                LifeMaximum = player.LifeMaximum,
                CharacterLevel = player.CharacterLevel,
            };
        }
    }

    public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));

        lock (synchronization) {
            var state = GetOrCreatePlayer(sessionId);
            state.EntityId = entityId;
            var player = new PlayerSnapshot {
                EntityId = entityId,
                PositionX = state.PositionX,
                PositionY = state.PositionY,
                Gold = state.Gold,
                ActiveStoreId = state.ActiveStoreId ?? 0,
                Life = state.Life,
                Mana = state.Mana,
                ManaMaximum = state.ManaMaximum,
                LifeMaximum = state.LifeMaximum,
                CharacterLevel = state.CharacterLevel,
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

    public EventBatch? DrainEvents(string sessionId, uint entityId, ulong tick)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));

        lock (synchronization) {
            if (!pendingEvents.Remove(sessionId, out var events) || events.Count == 0)
                return null;

            var batch = new EventBatch { Tick = tick };
            foreach (var gameplayEvent in events) {
                switch (gameplayEvent.Kind) {
                case PendingGameplayEventKind.Damage:
                    batch.Events.Add(new GameEvent {
                        Damage = new DamageEvent {
                            SourceEntityId = entityId,
                            TargetEntityId = gameplayEvent.TargetEntityId,
                            Amount = gameplayEvent.Amount,
                        },
                    });
                    break;
                case PendingGameplayEventKind.Experience:
                    batch.Events.Add(new GameEvent {
                        Experience = new ExperienceEvent {
                            PlayerEntityId = entityId,
                            Amount = (uint)gameplayEvent.Amount,
                        },
                    });
                    break;
                }
            }
            return batch;
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
            InventoryWidth = state.InventoryWidth > 1 ? (uint)state.InventoryWidth : 0,
            InventoryHeight = state.InventoryHeight > 1 ? (uint)state.InventoryHeight : 0,
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

    private CommandExecutionResult Move(PlayerStoreState player, MoveRequested request)
    {
        if (request.DirectionX is < -1 or > 1 || request.DirectionY is < -1 or > 1
            || (request.DirectionX == 0 && request.DirectionY == 0))
            return CommandExecutionResult.Rejected(CommandRejectReason.Malformed);

        var targetX = player.PositionX + request.DirectionX;
        var targetY = player.PositionY + request.DirectionY;
        if (targetX < 0 || targetX >= worldWidth || targetY < 0 || targetY >= worldHeight)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        player.PositionX = targetX;
        player.PositionY = targetY;
        return CommandExecutionResult.Accepted;
    }

    private static CommandExecutionResult Cast(PlayerStoreState player, CastRequested request)
    {
        const uint HealingSpellId = 1;
        const int ManaCost = 5;
        const int HealingAmount = 20;
        if (request.SpellId != HealingSpellId)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (request.TargetEntityId != 0 && request.TargetEntityId != player.EntityId)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (player.Life >= player.LifeMaximum)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (player.Mana < ManaCost)
            return CommandExecutionResult.Rejected(CommandRejectReason.InsufficientResources);

        player.Mana -= ManaCost;
        player.Life = Math.Min(player.LifeMaximum, player.Life + HealingAmount);
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Attack(string sessionId, PlayerStoreState player, AttackRequested request, ulong appliedTick)
    {
        if (!combatTargets.TryGetValue(request.TargetEntityId, out var target) || target.HitPoints <= 0)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (Math.Max(Math.Abs(target.PositionX - player.PositionX), Math.Abs(target.PositionY - player.PositionY)) > 1)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        var damage = Math.Max(1, 10 - target.ArmorClass);
        target.HitPoints = Math.Max(0, target.HitPoints - damage);
        GetPendingEvents(sessionId).Add(new PendingGameplayEvent(PendingGameplayEventKind.Damage, target.EntityId, damage));
        if (target.HitPoints == 0)
        {
            GrantExperience(player, 100);
            GetPendingEvents(sessionId).Add(new PendingGameplayEvent(PendingGameplayEventKind.Experience, player.EntityId, 100));
        }
        return CommandExecutionResult.Accepted;
    }

    private List<PendingGameplayEvent> GetPendingEvents(string sessionId)
    {
        if (!pendingEvents.TryGetValue(sessionId, out var events)) {
            events = [];
            pendingEvents.Add(sessionId, events);
        }
        return events;
    }

    private static void GrantExperience(PlayerStoreState player, uint amount)
    {
        player.Experience = checked(player.Experience + amount);
        while (player.CharacterLevel < 50 && player.Experience >= player.CharacterLevel * 1000U)
            player.CharacterLevel++;
    }

    private static CommandExecutionResult MoveInventoryItem(PlayerStoreState player, uint inventoryIndex, uint targetCell)
    {
        if (!TryGetInventoryItem(player, inventoryIndex, out var item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (!CanPlaceInventoryItem(player, targetCell, item.State, (int)inventoryIndex))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        ClearInventoryItem(player, (int)inventoryIndex);
        FillInventoryItem(player, (int)targetCell, (int)inventoryIndex, item.State);
        return CommandExecutionResult.Accepted;
    }

    private static CommandExecutionResult MoveItem(PlayerStoreState player, MoveItemRequested request)
    {
        if (!TryGetTransfer(player, request.Item, out var source))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (!IsValidDestination(player, request.Destination, source.State, request.Item))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (SameLocation(request.Item, request.Destination))
            return CommandExecutionResult.Accepted;

        var destinationIndex = request.Destination.Location == PlayerItemLocation.Inventory
            ? GetInventoryItemIndexAt(player, request.Destination.Slot)
            : -1;
        if (destinationIndex >= 0) {
            var destinationReference = request.Destination.Clone();
            destinationReference.Slot = (uint)destinationIndex;
            destinationReference.Location = PlayerItemLocation.Inventory;
            if (!TryGetTransfer(player, destinationReference, out var destination))
                return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
            return SwapTransfers(player, request.Item, request.Destination, source, destinationReference, destination);
        }

        if (request.Destination.Location != PlayerItemLocation.Inventory && DestinationSlotOccupied(player, request.Destination)) {
            if (!TryGetTransfer(player, request.Destination, out var destination))
                return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
            return SwapTransfers(player, request.Item, request.Destination, source, request.Destination, destination);
        }

        if (request.Destination.Location == PlayerItemLocation.Inventory && destinationIndex < 0) {
            var ignoredIndex = request.Item.Location == PlayerItemLocation.Inventory ? (int)request.Item.Slot : -1;
            if (!CanPlaceInventoryItem(player, request.Destination.Slot, source.State, ignoredIndex))
                return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
            if (request.Item.Location == PlayerItemLocation.Inventory) {
                ClearInventoryItem(player, (int)request.Item.Slot);
                FillInventoryItem(player, (int)request.Destination.Slot, (int)request.Item.Slot, source.State);
            } else {
                AddTransfer(player, request.Destination, source);
                RemoveTransfer(player, request.Item);
            }
            return CommandExecutionResult.Accepted;
        }

        RemoveTransfer(player, request.Item);
        AddTransfer(player, request.Destination, source);
        return CommandExecutionResult.Accepted;
    }

    private static CommandExecutionResult SwapTransfers(
        PlayerStoreState player,
        PlayerItemReference sourceReference,
        PlayerItemReference destinationReference,
        TransferItem source,
        PlayerItemReference actualDestinationReference,
        TransferItem destination)
    {
        if (sourceReference.Location == PlayerItemLocation.Inventory
            && actualDestinationReference.Location == PlayerItemLocation.Inventory) {
            var sourceIndex = (int)sourceReference.Slot;
            var destinationIndex = (int)actualDestinationReference.Slot;
            var sourceAnchor = FindInventoryAnchor(player, sourceIndex);
            if (sourceAnchor < 0
                || !CanPlaceInventoryItem(player, (uint)sourceAnchor, destination.State, sourceIndex, destinationIndex)
                || !CanPlaceInventoryItem(player, destinationReference.Slot, source.State, sourceIndex, destinationIndex))
                return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);

            ClearInventoryItem(player, sourceIndex);
            ClearInventoryItem(player, destinationIndex);
            player.Inventory[sourceIndex] = ToOwnedItem(destination);
            player.Inventory[destinationIndex] = ToOwnedItem(source);
            FillInventoryItem(player, sourceAnchor, sourceIndex, destination.State);
            FillInventoryItem(player, (int)destinationReference.Slot, destinationIndex, source.State);
            return CommandExecutionResult.Accepted;
        }

        if (actualDestinationReference.Location == PlayerItemLocation.Inventory) {
            var destinationIndex = (int)actualDestinationReference.Slot;
            if (!CanPlaceInventoryItem(player, destinationReference.Slot, source.State, destinationIndex))
                return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
            var destinationAnchor = (int)destinationReference.Slot;
            ClearInventoryItem(player, destinationIndex);
            player.Inventory[destinationIndex] = ToOwnedItem(source);
            FillInventoryItem(player, destinationAnchor, destinationIndex, source.State);
            ReplaceTransfer(player, sourceReference, destination);
            return CommandExecutionResult.Accepted;
        }

        if (sourceReference.Location == PlayerItemLocation.Inventory) {
            var sourceIndex = (int)sourceReference.Slot;
            var sourceAnchor = FindInventoryAnchor(player, sourceIndex);
            if (sourceAnchor < 0 || !CanPlaceInventoryItem(player, (uint)sourceAnchor, destination.State, sourceIndex))
                return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
            ClearInventoryItem(player, sourceIndex);
            player.Inventory[sourceIndex] = ToOwnedItem(destination);
            FillInventoryItem(player, sourceAnchor, sourceIndex, destination.State);
            ReplaceTransfer(player, destinationReference, source);
            return CommandExecutionResult.Accepted;
        }

        ReplaceTransfer(player, sourceReference, destination);
        ReplaceTransfer(player, actualDestinationReference, source);
        return CommandExecutionResult.Accepted;
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

    private static bool IsValidDestination(PlayerStoreState player, PlayerItemReference reference, AuthoritativeItemState state, PlayerItemReference source)
    {
        if (reference.Location == PlayerItemLocation.Inventory)
            return IsInventoryBoundsValid(player, reference.Slot, state);
        if (reference.Slot >= 8)
            return false;
        return state.InventoryWidth <= 1 && state.InventoryHeight <= 1;
    }

    private static bool IsInventoryBoundsValid(PlayerStoreState player, uint targetCell, AuthoritativeItemState state)
    {
        if (targetCell >= player.InventoryGrid.Count)
            return false;
        var width = Math.Max(1, state.InventoryWidth);
        var height = Math.Max(1, state.InventoryHeight);
        var column = (int)targetCell % 10;
        var row = (int)targetCell / 10;
        return column + width <= 10 && row + height <= (player.InventoryGrid.Count + 9) / 10;
    }

    private static bool CanPlaceInventoryItem(PlayerStoreState player, uint targetCell, AuthoritativeItemState state, params int[] ignoredIndices)
    {
        if (targetCell >= player.InventoryGrid.Count)
            return false;
        var width = Math.Max(1, state.InventoryWidth);
        var height = Math.Max(1, state.InventoryHeight);
        var column = (int)targetCell % 10;
        var row = (int)targetCell / 10;
        if (column + width > 10 || row + height > (player.InventoryGrid.Count + 9) / 10)
            return false;
        var ignored = ignoredIndices.ToHashSet();
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var cell = (row + y) * 10 + column + x;
                if (cell >= player.InventoryGrid.Count)
                    return false;
                var occupant = player.InventoryGrid[cell];
                if (occupant >= 0 && !ignored.Contains(occupant))
                    return false;
            }
        }
        return true;
    }

    private static int GetInventoryItemIndexAt(PlayerStoreState player, uint cell)
    {
        return cell < player.InventoryGrid.Count ? player.InventoryGrid[(int)cell] : -1;
    }

    private static bool DestinationSlotOccupied(PlayerStoreState player, PlayerItemReference reference)
    {
        return reference.Location == PlayerItemLocation.Belt
            ? player.Belt.Any(item => item.Slot == reference.Slot)
            : player.Equipment.Any(item => item.Slot == reference.Slot);
    }

    private static int FindInventoryAnchor(PlayerStoreState player, int inventoryIndex)
    {
        return player.InventoryGrid.FindIndex(cell => cell == inventoryIndex);
    }

    private static void ClearInventoryItem(PlayerStoreState player, int inventoryIndex)
    {
        for (var cell = 0; cell < player.InventoryGrid.Count; cell++) {
            if (player.InventoryGrid[cell] == inventoryIndex)
                player.InventoryGrid[cell] = -1;
        }
    }

    private static void FillInventoryItem(PlayerStoreState player, int targetCell, int inventoryIndex, AuthoritativeItemState state)
    {
        var width = Math.Max(1, state.InventoryWidth);
        var height = Math.Max(1, state.InventoryHeight);
        var column = targetCell % 10;
        var row = targetCell / 10;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                player.InventoryGrid[(row + y) * 10 + column + x] = inventoryIndex;
    }

    private static OwnedStoreItem ToOwnedItem(TransferItem item)
    {
        return new OwnedStoreItem(item.StoreId, item.StoreSlot, item.ItemSeed, item.Price, item.PurchasedAtTick, item.State);
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
            FillInventoryItem(player, (int)destination.Slot, index, item.State);
        } else if (destination.Location == PlayerItemLocation.Belt) {
            player.Belt.Add(new BeltStoreItem(destination.Slot, item.ItemSeed, item.State));
        } else {
            player.Equipment.Add(new EquippedStoreItem(destination.Slot, item.ItemSeed, item.State));
        }
    }

    private readonly record struct TransferItem(uint ItemSeed, AuthoritativeItemState State, uint StoreId, uint StoreSlot, uint Price, ulong PurchasedAtTick);

    private enum PendingGameplayEventKind
    {
        Damage,
        Experience,
    }

    private readonly record struct PendingGameplayEvent(PendingGameplayEventKind Kind, uint TargetEntityId, int Amount);

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
                startingBelt,
                startingPositionX,
                startingPositionY,
                startingLifeMaximum,
                startingCharacterLevel);
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
        IReadOnlyList<BeltStoreItem> belt,
        int positionX,
        int positionY,
        int lifeMaximum,
        uint characterLevel)
    {
        public uint Gold { get; set; } = gold;

        public uint Experience { get; set; } = experience;

        public int Life { get; set; } = Math.Clamp(life, 0, Math.Max(lifeMaximum, 0));

        public int LifeMaximum { get; } = Math.Max(lifeMaximum, 0);

        public uint CharacterLevel { get; set; } = characterLevel;

        public int PositionX { get; set; } = positionX;

        public int PositionY { get; set; } = positionY;

        public uint EntityId { get; set; }

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
