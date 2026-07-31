using Devilution.Protocol.V1;
using Google.Protobuf;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Snapshots;
using System.Text.Json;

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
    public uint LevelId { get; init; }
    public uint EntityId { get; init; }
    public IReadOnlyList<AuthoritativeStatusEffect> StatusEffects { get; init; } = [];

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
public sealed class StoreSimulationExecutor : IAuthoritativeCommandExecutor, IAuthoritativeTickExecutor, IAuthoritativeSnapshotProvider, IAuthoritativeEventProvider, IAuthoritativeSaveProvider
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
    private readonly int startingArmorClass;
    private readonly int worldWidth;
    private readonly int worldHeight;
    private readonly uint startingLevelId;
    private readonly HashSet<int> blockedCells;
    private readonly PlayerAttributesState startingAttributes;
    private readonly IReadOnlyList<EquippedStoreItem> startingEquipment;
    private readonly IReadOnlyList<int> startingInventoryGrid;
    private readonly IReadOnlyList<BeltStoreItem> startingBelt;
    private readonly IStoreGameplayRules gameplayRules;
    private readonly Dictionary<uint, AuthoritativeCombatTarget> combatTargets = new();
    private readonly Dictionary<uint, AuthoritativePortal> portals = new();
    private readonly Dictionary<uint, AuthoritativeWorldItem> worldItems = new();
    private readonly Dictionary<uint, AuthoritativeWorldObject> objects = new();
    private readonly Dictionary<uint, AuthoritativeQuestState> quests = new();
    private readonly Dictionary<uint, AuthoritativeProjectile> projectiles = new();
    private readonly AuthoritativeSpellCatalog spells;
    private readonly AuthoritativeCombatRules combatRules;
    private readonly AuthoritativeWorld? world;
    private readonly Dictionary<string, List<PendingGameplayEvent>> pendingEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayerStoreState> players = new(StringComparer.Ordinal);
    private uint nextProjectileEntityId = 0x80000000;
    private ulong lastAdvancedTick;

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
        int worldHeight = 40,
        uint startingLevelId = 0,
        IReadOnlyList<int>? startingBlockedCells = null,
        IReadOnlyList<AuthoritativePortal>? startingPortals = null,
        AuthoritativeWorld? startingWorld = null,
        AuthoritativeSpellCatalog? startingSpells = null,
        AuthoritativeCombatRules? startingCombatRules = null,
        IReadOnlyList<AuthoritativeWorldItem>? startingWorldItems = null,
        IReadOnlyList<AuthoritativeWorldObject>? startingObjects = null,
        IReadOnlyList<AuthoritativeQuestState>? startingQuests = null,
        int startingArmorClass = 0)
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
        this.startingArmorClass = Math.Max(0, startingArmorClass);
        this.worldWidth = worldWidth > 0 ? worldWidth : throw new ArgumentOutOfRangeException(nameof(worldWidth));
        this.worldHeight = worldHeight > 0 ? worldHeight : throw new ArgumentOutOfRangeException(nameof(worldHeight));
        this.startingLevelId = startingLevelId;
        this.blockedCells = (startingBlockedCells ?? []).ToHashSet();
        if (this.blockedCells.Any(cell => cell < 0 || cell >= checked(worldWidth * worldHeight)))
            throw new InvalidDataException("Starting blocked cells must be inside the configured world bounds.");
        world = startingWorld;
        spells = startingSpells ?? new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 5, 20, 0, 0, 0),
            new AuthoritativeSpellDefinition(2, 3, 0, 1, 10, 1),
        ]);
        combatRules = startingCombatRules ?? new AuthoritativeCombatRules(10, 1, 100);
        this.startingAttributes = startingAttributes ?? PlayerAttributesState.Zero;
        this.startingEquipment = startingEquipment?.ToArray() ?? [];
        this.startingInventoryGrid = startingInventoryGrid?.ToArray() ?? [];
        this.startingBelt = startingBelt?.ToArray() ?? [];
        this.gameplayRules = gameplayRules ?? DiabloGameplayModule.Instance;
        foreach (var target in startingCombatTargets ?? [])
            combatTargets.Add(target.EntityId, target);
        foreach (var portal in startingPortals ?? [])
            portals.Add(portal.PortalId, portal);
        foreach (var item in startingWorldItems ?? [])
            worldItems.Add(item.EntityId, item);
        foreach (var @object in startingObjects ?? []) {
            if (!objects.TryAdd(@object.EntityId, @object))
                throw new InvalidDataException($"Object entity {@object.EntityId} is registered more than once.");
        }
        foreach (var quest in startingQuests ?? []) {
            if (!quests.TryAdd(quest.QuestId, quest))
                throw new InvalidDataException($"Quest {quest.QuestId} is registered more than once.");
        }

        if (!IsWalkable(startingLevelId, startingPositionX, startingPositionY))
            throw new InvalidDataException("The starting player position is not walkable in the authoritative world.");

        var entityIds = new HashSet<uint>();
        foreach (var target in combatTargets.Values) {
            ValidateWorldPosition(target.LevelId, target.PositionX, target.PositionY, "monster");
            if (!entityIds.Add(target.EntityId))
                throw new InvalidDataException($"Entity {target.EntityId} is registered more than once.");
            if (target.Drop is not null && !entityIds.Add(target.Drop.EntityId))
                throw new InvalidDataException($"Entity {target.Drop.EntityId} is registered more than once.");
        }
        foreach (var item in worldItems.Values) {
            ValidateWorldPosition(item.LevelId, item.PositionX, item.PositionY, "world item");
            if (!entityIds.Add(item.EntityId))
                throw new InvalidDataException($"Entity {item.EntityId} is registered more than once.");
        }
        foreach (var @object in objects.Values) {
            ValidateWorldPosition(@object.LevelId, @object.PositionX, @object.PositionY, "object");
            if (!entityIds.Add(@object.EntityId))
                throw new InvalidDataException($"Entity {@object.EntityId} is registered more than once.");
        }
        foreach (var portal in portals.Values) {
            ValidateWorldPosition(portal.SourceLevelId, portal.SourcePositionX, portal.SourcePositionY, "portal source");
            ValidateWorldPosition(portal.DestinationLevelId, portal.DestinationPositionX, portal.DestinationPositionY, "portal destination");
        }
        ValidateWorldOccupancy(
            startingLevelId,
            startingPositionX,
            startingPositionY,
            combatTargets.Values.Where(target => target.HitPoints > 0).Select(target => (target.LevelId, target.PositionX, target.PositionY)),
            worldItems.Values.Select(item => (item.LevelId, item.PositionX, item.PositionY)),
            objects.Values.Where(@object => !@object.Activated).Select(@object => (@object.LevelId, @object.PositionX, @object.PositionY)));
    }

    public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(sessionId))
            return CommandExecutionResult.Rejected(CommandRejectReason.Malformed);

        lock (synchronization) {
            var player = GetOrCreatePlayer(sessionId);
            AdvanceStatuses(player, appliedTick);
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
                Command.IntentOneofCase.CastRequested => Cast(sessionId, player, command.CastRequested),
                Command.IntentOneofCase.AttackRequested => Attack(sessionId, player, command.AttackRequested, appliedTick),
                Command.IntentOneofCase.UsePortalRequested => UsePortal(player, command.UsePortalRequested.PortalId),
                Command.IntentOneofCase.PickupWorldItemRequested => PickupWorldItem(player, command.PickupWorldItemRequested.ItemEntityId, appliedTick),
                Command.IntentOneofCase.OperateObjectRequested => OperateObject(sessionId, player, command.OperateObjectRequested.ObjectEntityId),
                Command.IntentOneofCase.AdvanceQuestRequested => AdvanceQuest(player, command.AdvanceQuestRequested.QuestId),
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
                LevelId = player.LevelId,
                EntityId = player.EntityId,
                StatusEffects = player.StatusEffects.ToArray(),
            };
        }
    }

    public Snapshot CreateSnapshot(string sessionId, uint entityId, ulong tick)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("A session ID is required.", nameof(sessionId));

        AdvanceTo(tick);
        lock (synchronization) {
            var state = GetOrCreatePlayer(sessionId);
            AdvanceStatuses(state, tick);
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
                LevelId = state.LevelId,
                Experience = state.Experience,
                Attributes = new PlayerAttributesSnapshot {
                    Strength = ToSnapshot(state.Attributes.Strength),
                    Magic = ToSnapshot(state.Attributes.Magic),
                    Dexterity = ToSnapshot(state.Attributes.Dexterity),
                    Vitality = ToSnapshot(state.Attributes.Vitality),
                },
            };

            foreach (var effect in state.StatusEffects)
                player.StatusEffects.Add(new StatusEffectSnapshot {
                    EffectId = effect.EffectId,
                    RemainingTicks = effect.RemainingTicks,
                    Magnitude = effect.Magnitude,
                });

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
            foreach (var target in combatTargets.Values.OrderBy(target => target.EntityId)) {
                snapshot.Monsters.Add(new MonsterSnapshot {
                    EntityId = target.EntityId,
                    MonsterId = target.MonsterId,
                    LevelId = target.LevelId,
                    PositionX = target.PositionX,
                    PositionY = target.PositionY,
                    HitPoints = target.HitPoints,
                    MaxHitPoints = target.MaxHitPoints,
                    ArmorClass = target.ArmorClass,
                    Alive = target.HitPoints > 0,
                    AttackDamage = target.AttackDamage,
                    AggroRange = target.AggroRange,
                    FireResistance = target.FireResistance,
                    LightningResistance = target.LightningResistance,
                    MagicResistance = target.MagicResistance,
                });
            }
            foreach (var item in worldItems.Values.OrderBy(item => item.EntityId)) {
                snapshot.WorldItems.Add(new WorldItemSnapshot {
                    EntityId = item.EntityId,
                    LevelId = item.LevelId,
                    PositionX = item.PositionX,
                    PositionY = item.PositionY,
                    ItemSeed = item.ItemSeed,
                    Price = item.Price,
                    State = ToSnapshot(item.State),
                });
            }
            foreach (var @object in objects.Values.OrderBy(@object => @object.EntityId)) {
                snapshot.Objects.Add(new ObjectSnapshot {
                    EntityId = @object.EntityId,
                    ObjectId = @object.ObjectId,
                    LevelId = @object.LevelId,
                    PositionX = @object.PositionX,
                    PositionY = @object.PositionY,
                    Activated = @object.Activated,
                    QuestId = @object.QuestId,
                    EffectKind = (int)@object.EffectKind,
                    EffectAmount = @object.EffectAmount,
                });
            }
            foreach (var quest in quests.Values.OrderBy(quest => quest.QuestId)) {
                snapshot.Quests.Add(new QuestSnapshot {
                    QuestId = quest.QuestId,
                    LevelId = quest.LevelId,
                    Progress = quest.Progress,
                    RequiredProgress = quest.RequiredProgress,
                    Completed = quest.Completed,
                });
            }
            foreach (var projectile in projectiles.Values.OrderBy(projectile => projectile.EntityId)) {
                snapshot.Projectiles.Add(new ProjectileSnapshot {
                    EntityId = projectile.EntityId,
                    SourceEntityId = projectile.SourceEntityId,
                    TargetEntityId = projectile.TargetEntityId,
                    SpellId = projectile.SpellId,
                    LevelId = projectile.LevelId,
                    PositionX = projectile.PositionX,
                    PositionY = projectile.PositionY,
                    TargetX = projectile.TargetX,
                    TargetY = projectile.TargetY,
                    Damage = projectile.Damage,
                    DamageType = (int)projectile.DamageType,
                    AreaRadius = projectile.AreaRadius,
                    RemainingTicks = projectile.RemainingTicks,
                });
            }
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

    /** Serializes authoritative player state for server-owned persistence. */
    public string ExportPlayerSave(string sessionId)
    {
        var state = GetPlayerState(sessionId);
        var snapshot = CreateSnapshot(sessionId, state.EntityId, 0);
        return JsonSerializer.Serialize(new AuthoritativeSaveDocument(1, Convert.ToBase64String(snapshot.ToByteArray())));
    }

    /** Restores a validated save into an existing session without changing world configuration. */
    public void ImportPlayerSave(string sessionId, string serializedSave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedSave);
        AuthoritativeSaveDocument? document;
        try {
            document = JsonSerializer.Deserialize<AuthoritativeSaveDocument>(serializedSave);
        } catch (JsonException exception) {
            throw new InvalidDataException("The authoritative save is not valid JSON.", exception);
        }
        if (document is null || document.FormatVersion != 1 || string.IsNullOrWhiteSpace(document.SnapshotBase64))
            throw new InvalidDataException("The authoritative save format is unsupported or incomplete.");

        Snapshot snapshot;
        try {
            snapshot = Snapshot.Parser.ParseFrom(Convert.FromBase64String(document.SnapshotBase64));
        } catch (Exception exception) when (exception is FormatException or InvalidProtocolBufferException) {
            throw new InvalidDataException("The authoritative save snapshot is not valid.", exception);
        }
        var sourcePlayer = snapshot.Players.Count == 1 ? snapshot.Players[0] : null;
        if (sourcePlayer is null)
            throw new InvalidDataException("The authoritative save must contain exactly one player snapshot.");

        lock (synchronization) {
            var player = GetOrCreatePlayer(sessionId);
            var source = ToStorePlayerSnapshot(sourcePlayer);
            ValidateSavedPlayer(source, player);
            player.Gold = source.Gold;
            player.Experience = source.Experience;
            player.Life = source.Life;
            player.Mana = source.Mana;
            player.Attributes = source.Attributes;
            player.CharacterLevel = source.CharacterLevel;
            player.PositionX = source.PositionX;
            player.PositionY = source.PositionY;
            player.LevelId = source.LevelId;
            player.StatusEffects = source.StatusEffects.ToList();
            player.ActiveStoreId = source.ActiveStoreId;
            player.Inventory.Clear();
            player.Inventory.AddRange(source.Inventory);
            player.Equipment.Clear();
            player.Equipment.AddRange(source.Equipment);
            player.Belt.Clear();
            player.Belt.AddRange(source.Belt);
            player.InventoryGrid.Clear();
            player.InventoryGrid.AddRange(source.InventoryGrid);
            player.EntityId = source.EntityId;
            RestoreWorldState(snapshot, player.EntityId);
        }
    }

    /** Advances statuses and autonomous monster behavior at a server tick boundary. */
    public void AdvanceTo(ulong tick)
    {
        lock (synchronization) {
            if (tick <= lastAdvancedTick)
                return;
            foreach (var player in players.Values)
                AdvanceStatuses(player, tick);
            for (var simulationTick = lastAdvancedTick + 1; simulationTick <= tick; simulationTick++) {
                AdvanceMonsters(simulationTick);
                AdvanceProjectiles(simulationTick);
            }
            lastAdvancedTick = tick;
        }
    }

    /** Restores the shared authoritative entities captured in a server save. */
    private void RestoreWorldState(Snapshot snapshot, uint playerEntityId)
    {
        var sourcePlayer = snapshot.Players.Count == 1 ? snapshot.Players[0] : null;
        if (sourcePlayer is null)
            throw new InvalidDataException("The authoritative save must contain exactly one player snapshot.");
        var monsterEntityIds = snapshot.Monsters.Select(monster => monster.EntityId).ToArray();
        var worldItemEntityIds = snapshot.WorldItems.Select(item => item.EntityId).ToArray();
        var objectEntityIds = snapshot.Objects.Select(@object => @object.EntityId).ToArray();
        var projectileEntityIds = snapshot.Projectiles.Select(projectile => projectile.EntityId).ToArray();
        var allWorldEntityIds = monsterEntityIds.Concat(worldItemEntityIds).Concat(objectEntityIds).Concat(projectileEntityIds).ToArray();
        if (monsterEntityIds.Any(entityId => entityId == 0)
            || worldItemEntityIds.Any(entityId => entityId == 0)
            || objectEntityIds.Any(entityId => entityId == 0)
            || projectileEntityIds.Any(entityId => entityId == 0)
            || monsterEntityIds.Distinct().Count() != monsterEntityIds.Length
            || worldItemEntityIds.Distinct().Count() != worldItemEntityIds.Length
            || objectEntityIds.Distinct().Count() != objectEntityIds.Length
            || projectileEntityIds.Distinct().Count() != projectileEntityIds.Length
            || allWorldEntityIds.Distinct().Count() != allWorldEntityIds.Length
            || allWorldEntityIds.Contains(playerEntityId)
            || snapshot.Monsters.Count > 4096
            || snapshot.WorldItems.Count > 4096
            || snapshot.Objects.Count > 4096
            || snapshot.Projectiles.Count > 4096)
            throw new InvalidDataException("The authoritative save contains invalid world entity identity.");

        var restoredWorldItems = snapshot.WorldItems.Select(item => {
            if (item.ItemSeed == 0 || item.State is null || !IsWalkable(item.LevelId, item.PositionX, item.PositionY))
                throw new InvalidDataException("The authoritative save contains an invalid world item.");
            return new AuthoritativeWorldItem(
                item.EntityId,
                item.LevelId,
                item.PositionX,
                item.PositionY,
                item.ItemSeed,
                item.Price,
                FromSnapshot(item.State));
        }).ToArray();

        var restoredMonsters = snapshot.Monsters.Select(monster => {
            if (!IsWalkable(monster.LevelId, monster.PositionX, monster.PositionY)
                || monster.HitPoints < 0
                || monster.MaxHitPoints < monster.HitPoints
                || monster.MaxHitPoints < 0
                || monster.AttackDamage < 0
                || monster.AggroRange < 0
                || monster.FireResistance is < -100 or > 100
                || monster.LightningResistance is < -100 or > 100
                || monster.MagicResistance is < -100 or > 100)
                throw new InvalidDataException("The authoritative save contains an invalid monster.");
            return monster;
        }).ToArray();
        var restoredObjects = snapshot.Objects.Select(@object => {
            if (!IsWalkable(@object.LevelId, @object.PositionX, @object.PositionY)
                || @object.EffectAmount < 0
                || !Enum.IsDefined(typeof(AuthoritativeObjectEffectKind), @object.EffectKind))
                throw new InvalidDataException("The authoritative save contains an invalid object position.");
            return @object;
        }).ToArray();
        var restoredProjectiles = snapshot.Projectiles.Select(projectile => {
            if (projectile.SourceEntityId == 0 || projectile.SpellId == 0 || projectile.RemainingTicks == 0
                || projectile.Damage < 0 || projectile.AreaRadius < 0
                || !IsWalkable(projectile.LevelId, projectile.PositionX, projectile.PositionY)
                || !IsWalkable(projectile.LevelId, projectile.TargetX, projectile.TargetY))
                throw new InvalidDataException("The authoritative save contains an invalid projectile.");
            return new AuthoritativeProjectile(
                projectile.EntityId,
                projectile.SourceEntityId,
                projectile.TargetEntityId,
                projectile.SpellId,
                projectile.LevelId,
                projectile.PositionX,
                projectile.PositionY,
                projectile.TargetX,
                projectile.TargetY,
                projectile.Damage,
                Enum.IsDefined(typeof(AuthoritativeDamageType), projectile.DamageType)
                    ? (AuthoritativeDamageType)projectile.DamageType
                    : throw new InvalidDataException("The authoritative save contains an invalid projectile damage type."),
                projectile.AreaRadius,
                projectile.RemainingTicks);
        }).ToArray();
        var restoredQuests = snapshot.Quests.Select(quest => {
            if (quest.QuestId == 0 || quest.RequiredProgress == 0 || quest.Progress > quest.RequiredProgress)
                throw new InvalidDataException("The authoritative save contains an invalid quest state.");
            return quest;
        }).ToArray();
        ValidateWorldOccupancy(
            sourcePlayer.LevelId,
            sourcePlayer.PositionX,
            sourcePlayer.PositionY,
            restoredMonsters.Where(monster => monster.HitPoints > 0).Select(monster => (monster.LevelId, monster.PositionX, monster.PositionY)),
            restoredWorldItems.Select(item => (item.LevelId, item.PositionX, item.PositionY)),
            restoredObjects.Where(@object => !@object.Activated).Select(@object => (@object.LevelId, @object.PositionX, @object.PositionY)));

        var savedMonsterIds = restoredMonsters.Select(monster => monster.EntityId).ToHashSet();
        foreach (var entityId in combatTargets.Keys.Where(entityId => !savedMonsterIds.Contains(entityId)).ToArray())
            combatTargets.Remove(entityId);
        foreach (var monster in restoredMonsters) {
            if (combatTargets.TryGetValue(monster.EntityId, out var target)) {
                target.PositionX = monster.PositionX;
                target.PositionY = monster.PositionY;
                target.HitPoints = monster.HitPoints;
                target.ArmorClass = monster.ArmorClass;
                target.MaxHitPoints = monster.MaxHitPoints;
                target.LevelId = monster.LevelId;
                target.MonsterId = monster.MonsterId;
                target.AttackDamage = monster.AttackDamage;
                target.AggroRange = monster.AggroRange;
                target.FireResistance = monster.FireResistance;
                target.LightningResistance = monster.LightningResistance;
                target.MagicResistance = monster.MagicResistance;
            } else {
                combatTargets.Add(monster.EntityId, new AuthoritativeCombatTarget(
                    monster.EntityId,
                    monster.PositionX,
                    monster.PositionY,
                    monster.HitPoints,
                    monster.ArmorClass,
                    monster.MaxHitPoints,
                    monster.LevelId,
                    monster.MonsterId,
                    attackDamage: monster.AttackDamage,
                    aggroRange: monster.AggroRange,
                    fireResistance: monster.FireResistance,
                    lightningResistance: monster.LightningResistance,
                    magicResistance: monster.MagicResistance));
            }
        }

        worldItems.Clear();
        foreach (var item in restoredWorldItems)
            worldItems.Add(item.EntityId, item);

        var savedObjectIds = restoredObjects.Select(@object => @object.EntityId).ToHashSet();
        foreach (var entityId in objects.Keys.Where(entityId => !savedObjectIds.Contains(entityId)).ToArray())
            objects.Remove(entityId);
        foreach (var @object in restoredObjects) {
            if (objects.TryGetValue(@object.EntityId, out var target)) {
                target.ObjectId = @object.ObjectId;
                target.LevelId = @object.LevelId;
                target.PositionX = @object.PositionX;
                target.PositionY = @object.PositionY;
                target.Activated = @object.Activated;
                target.QuestId = @object.QuestId;
                target.EffectKind = Enum.IsDefined(typeof(AuthoritativeObjectEffectKind), @object.EffectKind)
                    ? (AuthoritativeObjectEffectKind)@object.EffectKind
                    : throw new InvalidDataException("The authoritative save contains an invalid object effect kind.");
                target.EffectAmount = @object.EffectAmount;
            } else {
                objects.Add(@object.EntityId, new AuthoritativeWorldObject(
                    @object.EntityId,
                    @object.ObjectId,
                    @object.LevelId,
                    @object.PositionX,
                    @object.PositionY,
                    @object.Activated,
                    @object.QuestId,
                    Enum.IsDefined(typeof(AuthoritativeObjectEffectKind), @object.EffectKind)
                        ? (AuthoritativeObjectEffectKind)@object.EffectKind
                        : throw new InvalidDataException("The authoritative save contains an invalid object effect kind."),
                    @object.EffectAmount));
            }
        }

        projectiles.Clear();
        foreach (var projectile in restoredProjectiles)
            projectiles.Add(projectile.EntityId, projectile);

        var savedQuestIds = restoredQuests.Select(quest => quest.QuestId).ToHashSet();
        foreach (var questId in quests.Keys.Where(questId => !savedQuestIds.Contains(questId)).ToArray())
            quests.Remove(questId);
        foreach (var quest in restoredQuests) {
            if (quests.TryGetValue(quest.QuestId, out var target)) {
                target.LevelId = quest.LevelId;
                target.Progress = quest.Progress;
                target.RequiredProgress = quest.RequiredProgress;
                target.Completed = quest.Completed;
            } else {
                quests.Add(quest.QuestId, new AuthoritativeQuestState(
                    quest.QuestId,
                    quest.LevelId,
                    quest.RequiredProgress,
                    quest.Progress,
                    quest.Completed));
            }
        }
    }

    private static StorePlayerSnapshot ToStorePlayerSnapshot(PlayerSnapshot source)
    {
        var attributes = source.Attributes ?? new PlayerAttributesSnapshot();
        return new StorePlayerSnapshot(
            source.Gold,
            source.ActiveStoreId == 0 ? null : source.ActiveStoreId,
            source.Inventory.Select(item => new OwnedStoreItem(
                item.StoreId,
                item.StoreSlot,
                item.ItemSeed,
                item.Price,
                item.PurchasedAtTick,
                FromSnapshot(item.State))).ToArray(),
            source.Experience,
            source.Life,
            source.Mana,
            source.ManaMaximum,
            new PlayerAttributesState(
                FromSnapshot(attributes.Strength),
                FromSnapshot(attributes.Magic),
                FromSnapshot(attributes.Dexterity),
                FromSnapshot(attributes.Vitality)),
            source.Equipment.Select(item => new EquippedStoreItem(item.Slot, item.ItemSeed, FromSnapshot(item.State))).ToArray(),
            source.InventoryGrid.ToArray(),
            source.Belt.Select(item => new BeltStoreItem(item.Slot, item.ItemSeed, FromSnapshot(item.State))).ToArray()) {
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            LifeMaximum = source.LifeMaximum,
            CharacterLevel = source.CharacterLevel,
            LevelId = source.LevelId,
            EntityId = source.EntityId,
            StatusEffects = source.StatusEffects.Select(effect => new AuthoritativeStatusEffect(effect.EffectId, effect.RemainingTicks, effect.Magnitude)).ToArray(),
        };
    }

    private static PlayerAttributeState FromSnapshot(AttributeSnapshot? attribute)
    {
        return new PlayerAttributeState(attribute?.Base ?? 0, attribute?.Current ?? 0);
    }

    private static AuthoritativeItemState FromSnapshot(ItemStateSnapshot? source)
    {
        source ??= new ItemStateSnapshot { ItemType = -1, ItemIndex = -1 };
        return AuthoritativeItemState.Empty with {
            CreateInfo = source.CreateInfo,
            ItemType = source.ItemType,
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            Deleted = source.Deleted,
            Identified = source.Identified,
            Magical = source.Magical,
            EquipLocation = source.EquipLocation,
            ItemClass = source.ItemClass,
            Value = source.Value,
            IdentifiedValue = source.IdentifiedValue,
            MinDamage = source.MinDamage,
            MaxDamage = source.MaxDamage,
            ArmorClass = source.ArmorClass,
            Flags = source.Flags,
            MiscId = source.MiscId,
            SpellId = source.SpellId,
            ItemIndex = source.ItemIndex,
            Charges = source.Charges,
            MaxCharges = source.MaxCharges,
            Durability = source.Durability,
            MaxDurability = source.MaxDurability,
            PlusDamage = source.PlusDamage,
            PlusToHit = source.PlusToHit,
            PlusArmorClass = source.PlusArmorClass,
            PlusStrength = source.PlusStrength,
            PlusMagic = source.PlusMagic,
            PlusDexterity = source.PlusDexterity,
            PlusVitality = source.PlusVitality,
            PlusFireResistance = source.PlusFireResistance,
            PlusLightningResistance = source.PlusLightningResistance,
            PlusMagicResistance = source.PlusMagicResistance,
            PlusMana = source.PlusMana,
            PlusHitPoints = source.PlusHitPoints,
            PlusDamageModifier = source.PlusDamageModifier,
            PlusGetHit = source.PlusGetHit,
            PlusLight = source.PlusLight,
            SpellLevelAdd = source.SpellLevelAdd,
            UniqueId = source.UniqueId,
            FireMinDamage = source.FireMinDamage,
            FireMaxDamage = source.FireMaxDamage,
            LightningMinDamage = source.LightningMinDamage,
            LightningMaxDamage = source.LightningMaxDamage,
            PlusEnemyArmorClass = source.PlusEnemyArmorClass,
            PrefixPower = source.PrefixPower,
            SuffixPower = source.SuffixPower,
            ValueAdd1 = source.ValueAdd1,
            ValueMultiply1 = source.ValueMultiply1,
            ValueAdd2 = source.ValueAdd2,
            ValueMultiply2 = source.ValueMultiply2,
            MinimumStrength = source.MinimumStrength,
            MinimumMagic = source.MinimumMagic,
            MinimumDexterity = source.MinimumDexterity,
            StatFlag = source.StatFlag,
            HellfireDamageArmorFlags = source.HellfireDamageArmorFlags,
            Buff = source.Buff,
            InventoryWidth = source.InventoryWidth == 0 ? 1 : (int)source.InventoryWidth,
            InventoryHeight = source.InventoryHeight == 0 ? 1 : (int)source.InventoryHeight,
        };
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
                            SourceEntityId = gameplayEvent.SourceEntityId,
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
                case PendingGameplayEventKind.Healing:
                    batch.Events.Add(new GameEvent {
                        Healing = new HealingEvent {
                            TargetEntityId = gameplayEvent.TargetEntityId,
                            Amount = gameplayEvent.Amount,
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
        if (!IsWalkable(player.LevelId, targetX, targetY) || IsOccupied(player.LevelId, targetX, targetY, player.EntityId))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        player.PositionX = targetX;
        player.PositionY = targetY;
        return CommandExecutionResult.Accepted;
    }

    private bool IsWalkable(int positionX, int positionY)
    {
        return IsWalkable(0, positionX, positionY);
    }

    private bool IsWalkable(uint levelId, int positionX, int positionY)
    {
        if (world is not null)
            return world.IsWalkable(levelId, positionX, positionY);
        return positionX >= 0 && positionX < worldWidth
            && positionY >= 0 && positionY < worldHeight
            && !blockedCells.Contains(positionY * worldWidth + positionX);
    }

    private bool HasLineOfSight(uint levelId, int startX, int startY, int endX, int endY)
    {
        if (world is not null) {
            var level = world.Levels.SingleOrDefault(candidate => candidate.LevelId == levelId);
            return level?.HasLineOfSight(startX, startY, endX, endY) ?? false;
        }

        var x = startX;
        var y = startY;
        var deltaX = Math.Abs(endX - startX);
        var deltaY = Math.Abs(endY - startY);
        var stepX = startX < endX ? 1 : -1;
        var stepY = startY < endY ? 1 : -1;
        var error = deltaX - deltaY;
        while (true) {
            if ((x != startX || y != startY) && (x != endX || y != endY)
                && !IsWalkable(levelId, x, y))
                return false;
            if (x == endX && y == endY)
                return true;
            var doubledError = 2 * error;
            if (doubledError > -deltaY) {
                error -= deltaY;
                x += stepX;
            }
            if (doubledError < deltaX) {
                error += deltaX;
                y += stepY;
            }
        }
    }

    private void ValidateWorldPosition(uint levelId, int positionX, int positionY, string entityKind)
    {
        if (!IsWalkable(levelId, positionX, positionY))
            throw new InvalidDataException($"The starting {entityKind} position is not walkable in the authoritative world.");
    }

    private void ValidateWorldOccupancy(
        uint sourceLevelId,
        int sourcePositionX,
        int sourcePositionY,
        IEnumerable<(uint LevelId, int PositionX, int PositionY)> monsters,
        IEnumerable<(uint LevelId, int PositionX, int PositionY)> items,
        IEnumerable<(uint LevelId, int PositionX, int PositionY)> worldObjects)
    {
        var occupied = new HashSet<(uint LevelId, int PositionX, int PositionY)> {
            (sourceLevelId, sourcePositionX, sourcePositionY),
        };
        foreach (var monster in monsters) {
            if (!occupied.Add((monster.LevelId, monster.PositionX, monster.PositionY)))
                throw new InvalidDataException("The authoritative world contains overlapping live entities.");
        }
        foreach (var item in items) {
            if (!occupied.Add((item.LevelId, item.PositionX, item.PositionY)))
                throw new InvalidDataException("The authoritative world contains overlapping live entities.");
        }
        foreach (var @object in worldObjects) {
            if (!occupied.Add((@object.LevelId, @object.PositionX, @object.PositionY)))
                throw new InvalidDataException("The authoritative world contains overlapping live entities.");
        }
    }

    private bool IsOccupied(uint levelId, int positionX, int positionY, uint excludedEntityId = 0)
    {
        return combatTargets.Values.Any(target => target.HitPoints > 0
            && target.EntityId != excludedEntityId
            && (target.LevelId == 0 || target.LevelId == levelId)
            && target.PositionX == positionX
            && target.PositionY == positionY)
            || players.Values.Any(player => player.EntityId != 0
                && player.EntityId != excludedEntityId
                && player.LevelId == levelId
                && player.PositionX == positionX
                && player.PositionY == positionY)
            || objects.Values.Any(@object => !@object.Activated
                && @object.EntityId != excludedEntityId
                && @object.LevelId == levelId
                && @object.PositionX == positionX
                && @object.PositionY == positionY);
    }

    private CommandExecutionResult UsePortal(PlayerStoreState player, uint portalId)
    {
        if (!portals.TryGetValue(portalId, out var portal)
            || player.LevelId != portal.SourceLevelId
            || player.PositionX != portal.SourcePositionX
            || player.PositionY != portal.SourcePositionY)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (!IsWalkable(portal.DestinationLevelId, portal.DestinationPositionX, portal.DestinationPositionY))
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        if (IsOccupied(portal.DestinationLevelId, portal.DestinationPositionX, portal.DestinationPositionY, player.EntityId))
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);

        player.LevelId = portal.DestinationLevelId;
        player.PositionX = portal.DestinationPositionX;
        player.PositionY = portal.DestinationPositionY;
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult PickupWorldItem(PlayerStoreState player, uint itemEntityId, ulong appliedTick)
    {
        if (!worldItems.TryGetValue(itemEntityId, out var item)
            || item.LevelId != player.LevelId
            || Math.Max(Math.Abs(item.PositionX - player.PositionX), Math.Abs(item.PositionY - player.PositionY)) > 1)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        var targetCell = FindFirstInventoryPlacement(player, item.State);
        if (targetCell < 0)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);

        var inventoryIndex = player.Inventory.Count;
        player.Inventory.Add(new OwnedStoreItem(0, 0, item.ItemSeed, item.Price, appliedTick, item.State));
        FillInventoryItem(player, targetCell, inventoryIndex, item.State);
        worldItems.Remove(itemEntityId);
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult OperateObject(string sessionId, PlayerStoreState player, uint objectEntityId)
    {
        if (!objects.TryGetValue(objectEntityId, out var @object)
            || @object.Activated
            || @object.LevelId != player.LevelId
            || Math.Max(Math.Abs(@object.PositionX - player.PositionX), Math.Abs(@object.PositionY - player.PositionY)) > 1)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        @object.Activated = true;
        if (@object.QuestId != 0 && quests.TryGetValue(@object.QuestId, out var quest)
            && !quest.Completed && quest.LevelId == player.LevelId) {
            quest.Progress++;
            if (quest.Progress >= quest.RequiredProgress)
                quest.Completed = true;
        }
        if (@object.EffectAmount > 0) {
            switch (@object.EffectKind) {
            case AuthoritativeObjectEffectKind.Heal:
                var healing = Math.Min(@object.EffectAmount, Math.Max(0, player.LifeMaximum - player.Life));
                player.Life = checked(player.Life + healing);
                if (healing > 0)
                    GetPendingEvents(sessionId).Add(new PendingGameplayEvent(PendingGameplayEventKind.Healing, @object.EntityId, player.EntityId, healing));
                break;
            case AuthoritativeObjectEffectKind.Damage:
                var damage = Math.Min(player.Life, @object.EffectAmount);
                player.Life -= damage;
                if (damage > 0)
                    GetPendingEvents(sessionId).Add(new PendingGameplayEvent(PendingGameplayEventKind.Damage, @object.EntityId, player.EntityId, damage));
                break;
            case AuthoritativeObjectEffectKind.Experience:
                GrantExperience(player, checked((uint)@object.EffectAmount));
                GetPendingEvents(sessionId).Add(new PendingGameplayEvent(PendingGameplayEventKind.Experience, player.EntityId, player.EntityId, @object.EffectAmount));
                break;
            }
        }
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult AdvanceQuest(PlayerStoreState player, uint questId)
    {
        if (!quests.TryGetValue(questId, out var quest)
            || quest.Completed
            || quest.LevelId != player.LevelId)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        quest.Progress++;
        if (quest.Progress >= quest.RequiredProgress)
            quest.Completed = true;
        return CommandExecutionResult.Accepted;
    }

    private CommandExecutionResult Cast(string sessionId, PlayerStoreState player, CastRequested request)
    {
        if (!spells.TryGet(request.SpellId, out var spell))
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (spell.DamageAmount == 0 && request.TargetEntityId != 0 && request.TargetEntityId != player.EntityId)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        AuthoritativeCombatTarget? target = null;
        IReadOnlyList<AuthoritativeCombatTarget> damageTargets = [];
        var centerX = request.TargetX;
        var centerY = request.TargetY;
        if (spell.DamageAmount > 0) {
            if (request.TargetEntityId != 0)
                combatTargets.TryGetValue(request.TargetEntityId, out target);
            else {
                target = combatTargets.Values
                    .Where(candidate => candidate.HitPoints > 0
                        && (candidate.LevelId == 0 || candidate.LevelId == player.LevelId)
                        && candidate.PositionX == request.TargetX
                        && candidate.PositionY == request.TargetY)
                    .OrderBy(candidate => candidate.EntityId)
                    .FirstOrDefault();
            }
            centerX = target?.PositionX ?? request.TargetX;
            centerY = target?.PositionY ?? request.TargetY;
            if ((request.TargetEntityId != 0 && target is null)
                || (target is not null && (target.HitPoints <= 0 || (target.LevelId != 0 && target.LevelId != player.LevelId)))
                || Math.Max(Math.Abs(centerX - player.PositionX), Math.Abs(centerY - player.PositionY)) > spell.Range
                || (spell.AreaRadius == 0 && target is null))
                return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
            if (!HasLineOfSight(player.LevelId, player.PositionX, player.PositionY, centerX, centerY))
                return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

            damageTargets = combatTargets.Values
                .Where(candidate => candidate.HitPoints > 0
                    && (candidate.LevelId == 0 || candidate.LevelId == player.LevelId)
                    && Math.Max(Math.Abs(candidate.PositionX - centerX), Math.Abs(candidate.PositionY - centerY)) <= spell.AreaRadius)
                .OrderBy(candidate => candidate.EntityId)
                .ToArray();
            if (spell.AreaRadius == 0)
                damageTargets = [target!];
        }
        if (spell.HealingAmount > 0 && player.Life >= player.LifeMaximum)
            return CommandExecutionResult.Rejected(CommandRejectReason.NotAllowed);
        var projectileTravelTicks = 0;
        if (spell.ProjectileSpeed > 0) {
            var distance = Math.Max(Math.Abs(centerX - player.PositionX), Math.Abs(centerY - player.PositionY));
            projectileTravelTicks = Math.Max(1, (distance + spell.ProjectileSpeed - 1) / spell.ProjectileSpeed);
            if (projectileTravelTicks > spell.ProjectileLifetime)
                return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        }
        if (player.Mana < spell.ManaCost)
            return CommandExecutionResult.Rejected(CommandRejectReason.InsufficientResources);

        player.Mana -= spell.ManaCost;
        if (spell.HealingAmount > 0)
            player.Life = Math.Min(player.LifeMaximum, player.Life + spell.HealingAmount);
        if (spell.StatusEffectId != 0) {
            player.StatusEffects.RemoveAll(effect => effect.EffectId == spell.StatusEffectId);
            player.StatusEffects.Add(new AuthoritativeStatusEffect(spell.StatusEffectId, spell.StatusDuration, spell.StatusMagnitude));
        }
        if (spell.ProjectileSpeed > 0) {
            var projectileEntityId = AllocateProjectileEntityId();
            projectiles.Add(projectileEntityId, new AuthoritativeProjectile(
                projectileEntityId,
                player.EntityId == 0 ? 1U : player.EntityId,
                target?.EntityId ?? 0,
                spell.SpellId,
                player.LevelId,
                player.PositionX,
                player.PositionY,
                centerX,
                centerY,
                spell.DamageAmount,
                spell.DamageType,
                spell.AreaRadius,
                (uint)projectileTravelTicks));
        } else {
            foreach (var damageTarget in damageTargets)
                ApplyDamage(sessionId, player, damageTarget, spell.DamageAmount, spell.DamageType);
        }
        return CommandExecutionResult.Accepted;
    }

    private void ApplyDamage(
        string sessionId,
        PlayerStoreState player,
        AuthoritativeCombatTarget target,
        int baseDamage,
        AuthoritativeDamageType damageType)
    {
        var damage = combatRules.ResolveDamage(baseDamage, target.ArmorClass, damageType, target);
        ApplyResolvedDamage(sessionId, player, target, damage);
    }

    private void ApplyResolvedDamage(
        string sessionId,
        PlayerStoreState player,
        AuthoritativeCombatTarget target,
        int damage)
    {
        target.HitPoints = Math.Max(0, target.HitPoints - damage);
        if (damage > 0)
            GetPendingEvents(sessionId).Add(new PendingGameplayEvent(PendingGameplayEventKind.Damage, player.EntityId, target.EntityId, damage));
        if (target.HitPoints == 0) {
            GrantExperience(player, combatRules.DefeatExperience);
            GetPendingEvents(sessionId).Add(new PendingGameplayEvent(PendingGameplayEventKind.Experience, player.EntityId, player.EntityId, checked((int)combatRules.DefeatExperience)));
            SpawnDrop(target);
        }
    }

    private static void AdvanceStatuses(PlayerStoreState player, ulong tick)
    {
        if (player.LastAppliedTick is not ulong previousTick || tick <= previousTick) {
            player.LastAppliedTick = Math.Max(player.LastAppliedTick ?? 0, tick);
            return;
        }

        var elapsed = tick - previousTick;
        player.StatusEffects = player.StatusEffects
            .Select(effect => effect with { RemainingTicks = effect.RemainingTicks > elapsed ? effect.RemainingTicks - (uint)elapsed : 0 })
            .Where(effect => effect.RemainingTicks > 0)
            .ToList();
        player.LastAppliedTick = tick;
    }

    private void AdvanceMonsters(ulong tick)
    {
        foreach (var target in combatTargets.Values.OrderBy(target => target.EntityId)) {
            if (target.HitPoints <= 0 || target.AggroRange <= 0)
                continue;

            var victim = players
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => (SessionId: entry.Key, Player: entry.Value))
                .Where(entry => entry.Player.Life > 0
                    && entry.Player.LevelId == target.LevelId
                    && Math.Max(Math.Abs(entry.Player.PositionX - target.PositionX), Math.Abs(entry.Player.PositionY - target.PositionY)) <= target.AggroRange)
                .OrderBy(entry => Math.Max(Math.Abs(entry.Player.PositionX - target.PositionX), Math.Abs(entry.Player.PositionY - target.PositionY)))
                .ThenBy(entry => entry.Player.EntityId)
                .FirstOrDefault();
            if (victim.Player is null)
                continue;

            var distance = Math.Max(Math.Abs(victim.Player.PositionX - target.PositionX), Math.Abs(victim.Player.PositionY - target.PositionY));
            if (distance <= 1) {
                var damage = combatRules.ResolveAttackDamage(
                    target.AttackDamage,
                    victim.Player.ArmorClass,
                    unchecked((uint)tick ^ target.EntityId ^ victim.Player.EntityId));
                damage = Math.Min(victim.Player.Life, damage);
                victim.Player.Life -= damage;
                if (damage > 0)
                    GetPendingEvents(victim.SessionId).Add(new PendingGameplayEvent(PendingGameplayEventKind.Damage, target.EntityId, victim.Player.EntityId, damage));
                continue;
            }

            var deltaX = Math.Sign(victim.Player.PositionX - target.PositionX);
            var deltaY = Math.Sign(victim.Player.PositionY - target.PositionY);
            var candidates = new[] {
                (X: target.PositionX + deltaX, Y: target.PositionY + deltaY),
                (X: target.PositionX + deltaX, Y: target.PositionY),
                (X: target.PositionX, Y: target.PositionY + deltaY),
            };
            foreach (var candidate in candidates.Where(candidate => candidate.X != target.PositionX || candidate.Y != target.PositionY)) {
                if (!IsWalkable(target.LevelId, candidate.X, candidate.Y)
                    || IsOccupied(target.LevelId, candidate.X, candidate.Y, target.EntityId))
                    continue;
                target.PositionX = candidate.X;
                target.PositionY = candidate.Y;
                break;
            }
        }
    }

    private void AdvanceProjectiles(ulong tick)
    {
        foreach (var projectile in projectiles.Values.OrderBy(projectile => projectile.EntityId).ToArray()) {
            if (projectile.RemainingTicks > 1) {
                var nextX = projectile.PositionX + Math.Sign(projectile.TargetX - projectile.PositionX);
                var nextY = projectile.PositionY + Math.Sign(projectile.TargetY - projectile.PositionY);
                if (!IsWalkable(projectile.LevelId, nextX, nextY)
                    || !HasLineOfSight(projectile.LevelId, projectile.PositionX, projectile.PositionY, nextX, nextY)) {
                    projectiles.Remove(projectile.EntityId);
                    continue;
                }
                projectile.PositionX = nextX;
                projectile.PositionY = nextY;
                projectile.RemainingTicks--;
                continue;
            }

            if (projectile.TargetEntityId != 0) {
                if (combatTargets.TryGetValue(projectile.TargetEntityId, out var target)
                    && target.HitPoints > 0
                    && (target.LevelId == 0 || target.LevelId == projectile.LevelId))
                    ResolveProjectileDamage(projectile, target);
            } else {
                foreach (var target in combatTargets.Values
                    .Where(target => target.HitPoints > 0
                        && (target.LevelId == 0 || target.LevelId == projectile.LevelId)
                        && Math.Max(Math.Abs(target.PositionX - projectile.TargetX), Math.Abs(target.PositionY - projectile.TargetY)) <= projectile.AreaRadius)
                    .OrderBy(target => target.EntityId))
                    ResolveProjectileDamage(projectile, target);
            }
            projectiles.Remove(projectile.EntityId);
        }
    }

    private void ResolveProjectileDamage(AuthoritativeProjectile projectile, AuthoritativeCombatTarget target)
    {
        var owner = players
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .FirstOrDefault(entry => entry.Value.EntityId == projectile.SourceEntityId
                || (projectile.SourceEntityId == 1 && entry.Value.EntityId == 0));
        if (owner.Value is null)
            return;
        ApplyDamage(owner.Key, owner.Value, target, projectile.Damage, projectile.DamageType);
    }

    private uint AllocateProjectileEntityId()
    {
        while (nextProjectileEntityId == 0
            || combatTargets.ContainsKey(nextProjectileEntityId)
            || worldItems.ContainsKey(nextProjectileEntityId)
            || objects.ContainsKey(nextProjectileEntityId)
            || projectiles.ContainsKey(nextProjectileEntityId))
            nextProjectileEntityId = checked(nextProjectileEntityId + 1);
        return nextProjectileEntityId++;
    }

    private CommandExecutionResult Attack(string sessionId, PlayerStoreState player, AttackRequested request, ulong appliedTick)
    {
        if (!combatTargets.TryGetValue(request.TargetEntityId, out var target) || target.HitPoints <= 0)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (target.LevelId != 0 && target.LevelId != player.LevelId)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);
        if (Math.Max(Math.Abs(target.PositionX - player.PositionX), Math.Abs(target.PositionY - player.PositionY)) > 1)
            return CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget);

        var damage = combatRules.ResolveAttackDamage(
            combatRules.BaseAttackDamage,
            target,
            unchecked((uint)appliedTick ^ target.EntityId ^ player.EntityId));
        ApplyResolvedDamage(sessionId, player, target, damage);
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

    private void SpawnDrop(AuthoritativeCombatTarget target)
    {
        if (target.Drop is not null)
            worldItems[target.Drop.EntityId] = target.Drop;
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

    private static int FindFirstInventoryPlacement(PlayerStoreState player, AuthoritativeItemState state)
    {
        for (uint cell = 0; cell < player.InventoryGrid.Count; cell++) {
            if (CanPlaceInventoryItem(player, cell, state))
                return (int)cell;
        }
        return -1;
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
        Healing,
    }

    private readonly record struct PendingGameplayEvent(PendingGameplayEventKind Kind, uint SourceEntityId, uint TargetEntityId, int Amount);

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
                startingCharacterLevel,
                startingLevelId,
                startingArmorClass);
            players.Add(sessionId, player);
        }

        return player;
    }

    private void ValidateSavedPlayer(StorePlayerSnapshot source, PlayerStoreState destination)
    {
        if (source.Life < 0 || source.Life > destination.LifeMaximum
            || source.Mana < 0 || source.Mana > destination.ManaMaximum
            || source.CharacterLevel is < 1 or > 50
            || !IsWalkable(source.LevelId, source.PositionX, source.PositionY)
            || source.Inventory.Count > 40
            || source.Equipment.Count > 7
            || source.Belt.Count > 8
            || source.InventoryGrid.Count > 400
            || source.StatusEffects.Count > 128
            || source.LifeMaximum != destination.LifeMaximum
            || source.ManaMaximum != destination.ManaMaximum)
            throw new InvalidDataException("The authoritative save contains out-of-range player state.");

        ValidateInventoryTopology(source);
    }

    private static void ValidateInventoryTopology(StorePlayerSnapshot source)
    {
        var itemSeeds = new HashSet<uint>();
        foreach (var item in source.Inventory) {
            if (item.ItemSeed == 0 || !HasValidDimensions(item.State))
                throw new InvalidDataException("The authoritative save contains an invalid inventory item.");
            if (!itemSeeds.Add(item.ItemSeed))
                throw new InvalidDataException("The authoritative save contains a duplicate item seed.");
        }
        foreach (var item in source.Equipment) {
            if (item.ItemSeed == 0 || item.Slot >= 7 || !HasValidDimensions(item.State) || !itemSeeds.Add(item.ItemSeed))
                throw new InvalidDataException("The authoritative save contains invalid or duplicate equipment.");
        }
        if (source.Equipment.Select(item => item.Slot).Distinct().Count() != source.Equipment.Count)
            throw new InvalidDataException("The authoritative save contains duplicate equipment slots.");
        foreach (var item in source.Belt) {
            if (item.ItemSeed == 0 || item.Slot >= 8 || !HasValidDimensions(item.State) || !itemSeeds.Add(item.ItemSeed))
                throw new InvalidDataException("The authoritative save contains invalid or duplicate belt items.");
        }
        if (source.Belt.Select(item => item.Slot).Distinct().Count() != source.Belt.Count)
            throw new InvalidDataException("The authoritative save contains duplicate belt slots.");
        if (source.InventoryGrid.Any(cell => cell < -1 || cell >= source.Inventory.Count))
            throw new InvalidDataException("The authoritative save contains an invalid inventory-grid reference.");

        for (var inventoryIndex = 0; inventoryIndex < source.Inventory.Count; inventoryIndex++) {
            var state = source.Inventory[inventoryIndex].State;
            var anchor = -1;
            for (var cell = 0; cell < source.InventoryGrid.Count; cell++) {
                if (source.InventoryGrid[cell] == inventoryIndex) {
                    anchor = cell;
                    break;
                }
            }
            if (anchor < 0)
                continue;
            if (!CanPlaceInventoryItem(source.InventoryGrid, anchor, inventoryIndex, state))
                throw new InvalidDataException("The authoritative save contains an invalid inventory footprint.");
        }
    }

    private static bool HasValidDimensions(AuthoritativeItemState state)
    {
        return state.InventoryWidth is >= 1 and <= 10
            && state.InventoryHeight is >= 1 and <= 40;
    }

    private static bool CanPlaceInventoryItem(IReadOnlyList<int> grid, int targetCell, int inventoryIndex, AuthoritativeItemState state)
    {
        var width = Math.Max(1, state.InventoryWidth);
        var height = Math.Max(1, state.InventoryHeight);
        var column = targetCell % 10;
        var row = targetCell / 10;
        if (column + width > 10 || row + height > (grid.Count + 9) / 10)
            return false;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var cell = (row + y) * 10 + column + x;
                if (cell >= grid.Count || grid[cell] != inventoryIndex)
                    return false;
            }
        }
        return true;
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
        uint characterLevel,
        uint levelId,
        int armorClass)
    {
        public uint Gold { get; set; } = gold;

        public uint Experience { get; set; } = experience;

        public int Life { get; set; } = Math.Clamp(life, 0, Math.Max(lifeMaximum, 0));

        public int LifeMaximum { get; } = Math.Max(lifeMaximum, 0);

        public uint CharacterLevel { get; set; } = characterLevel;

        public int PositionX { get; set; } = positionX;

        public int PositionY { get; set; } = positionY;

        public uint LevelId { get; set; } = levelId;

        public int ArmorClass { get; } = Math.Max(0, armorClass);

        public List<AuthoritativeStatusEffect> StatusEffects { get; set; } = [];

        public ulong? LastAppliedTick { get; set; }

        public uint EntityId { get; set; }

        public int Mana { get; set; } = mana;

        public int ManaMaximum { get; } = Math.Max(manaMaximum, 0);

        public PlayerAttributesState Attributes { get; set; } = attributes;

        public List<EquippedStoreItem> Equipment { get; } = equipment.ToList();

        public List<BeltStoreItem> Belt { get; } = belt.ToList();

        public List<int> InventoryGrid { get; } = inventoryGrid.ToList();

        public uint? ActiveStoreId { get; set; }

        public List<OwnedStoreItem> Inventory { get; } = [];
    }
}
