# DevilutionY Changelog

All notable changes to this fork are documented below.

## 2026-07-01

- Refactor engine/lighting into `LightManager` class with singleton `CurrentLightManager`; update ~22 caller files
- Move 12 theme/trigger/crypt scalar globals (`numthemes`, `armorFlag`, `weaponFlag`, `zharlib`, `trigflag`, `numtrigs`, `TWarpFrom`, `UberRow`, `UberCol`, `IsUberRoomOpened`, `IsUberLeverActivated`, `UberDiabloMonsterIndex`) into `Level` class with inline accessors
- Replace `Missile::var1`-`var7` with typed anonymous union providing named struct accessors per missile family (projectile, guardian, portal, rune, wall, chain, inferno, nova, firewall, lightning, golem); migrate ProcessGenericProjectile and ProcessGuardian/AddGuardian
- Refactor `game/stores/` into `StoreManager` class with singleton `CurrentStoreManager`; update ~30 caller files

## 2026-07-02

- Refactor `ui/automap` into `AutomapManager` class with singleton `CurrentAutomapManager`; move 8 globals + 13 functions; update ~18 caller files

## 2026-07-20

- Remove all Packaging/ and Android-specific build artifacts
- Prune Android/UWP conditionals and references from CMake build system
- Consolidate Level identity fields into `LevelId`, remove 5 duplicate fields from Level
- Make `World::switchLevel` accept a `LevelId` instead of bare `LevelIndex`, centralizing identity setup
- Simplify all level-transition callers to construct `LevelId` in one call
- Externalize monster AI dispatch: replace `AiProc[]` array with `std::unordered_map` registry populated at startup
- Externalize AI-to-missile-type mapping: replace `GetMissileType()` switch with data lookup from `monster_missiles.tsv`
- Add `behavior` column to `objdat.tsv`, populate for all 109 object types
- Replace `OperateObject()` hardcoded `switch` with registry lookup via `ObjectBehaviorRegistry`
- Externalize item generation parameters: quality curve, affix allocation, drop tables into `item_generation.tsv`
- Create `ItemGenerationConfig` struct loaded from `item_generation.tsv` replacing hardcoded constants in `GetItemBLevel()`, `GetItemPowerPrefixAndSuffix()`, `RndItemForMonsterLevel()`, `RndAllItems()`, and `SpawnItem()`
- Externalize level generation parameters: monster density divisor, MP multiplier, and trap frequency into `level_generation.tsv`
- Create `LevelGenerationData` struct with level-range lookups for monster density, MP multiplier, and trap percent
- Use new data-driven lookups in `AddObjTraps()` and `InitMonsters()`
- Externalize quest pools and set level names into `quest_pools.tsv` and `set_level_names.tsv`
- Add `Object::data()` accessor returning `AllObjects[_otype]`
- Replace `QuestLevelNames[]` array with data-driven `SetLevelNames` lookup
- Add `onExpiryMissile` column to `misdat.tsv` replacing the switch in `ProcessGenericProjectile`
- Add `shortName` column to `towners.tsv`, replacing hardcoded `TownerShortNames` map
- Add TSV entries for TOWN_FARMER, TOWN_GIRL, TOWN_COWFARM (Hellfire NPCs)

## 2026-07-21

- Fix quest-pool initialization, item-generation reload state and legacy drop probabilities
- Restore test includes and test access to the refactored store and automap managers
- Extract active-mod and game-data reload ownership from Lua behind C++ managers and a typed event bus
- Add deterministic replay primitives for canonical state hashing and command ordering
- Add initial canonical player and store state projections for replay comparison

## 2026-07-22

- Add authoritative wallet, active-store, and inventory snapshots after
  handshake and command batches
- Add deterministic SHA-256 hashes for the authoritative snapshot projection
- Add baseline experience, life, and mana fields to the store player projection
- Add primary attributes, equipment slots, and inventory-grid snapshot fields
- Add core authoritative item-state fields to store inventory snapshots
- Add C# replay-fixture execution, authoritative clock abstraction, session
  entity-ID coverage, empty-batch validation, and command-delivery vector tests
- Reconcile the C# migration plan with implemented phase status, ownership,
  remaining exit gates, and the current critical path
- Add external content and versioned C# gameplay modules as first-class
  migration workstreams with per-slice ownership and verification gates
- Add external TSV store loading, module-owned store transactions, deterministic
  replay checkpoint parity, fixed-point/RNG/ID primitives, and gameplay ruleset
  identity coverage
- Add an executable JSON replay fixture baseline for player/store projection

- Add the initial transport-independent Protobuf protocol contract and compatibility rules
- Add adaptive client command acknowledgement retries with duplicate and late-command outcomes
- Add a shared retry/deduplication protocol test vector consumed by the C# server tests
- Add the initial C# authoritative command server with Protobuf bindings and deduplication tests
- Add bounded TCP Protobuf sessions with handshake validation and command routing
- Add the first C# authoritative store executor with retry-safe purchases and shared stock
- Add opt-in C++ Protobuf generation, bounded envelope framing, and a minimal
  authoritative client covering handshake, command acknowledgement, and snapshots
- Wire the C++ command-delivery tracker into queued authoritative sends,
  adaptive resubmissions, and acknowledgement resolution
- Fix native authoritative builds by generating Protobuf sources in the consuming
  CMake directory and exporting generated message data to C++ DLL tests
- Fix C++ authoritative connection includes and invalid-frame boundary coverage
- Add authoritative active-store stock snapshots and include vendor items in
  deterministic snapshot hashes and reconnect-resynchronization tests
- Add matching C++ and C# canonical content-manifest hashes with shared golden
  vectors and malformed-row validation
- Extend C++ and C# replay fixtures with structured content identity, retained
  checkpoints, store command payloads, and optional final snapshot validation
- Fix the native replay fixture test to avoid character-level validation before
  player experience tables are initialized
- Preserve native queued and in-flight authoritative commands, adaptive retry
  state, and the server resume token across reconnects
- Add opt-in authoritative endpoint parsing, stable store-command construction,
  validated native vendor-stock projection, and reconnect-safe store UI state

## 2026-06-24

- Add file-level summaries to all source files missing them
- Remove Testing/ from tracking, add to .gitignore
- Move mpq/ to data/mpq/ and storm/ to network/storm/
- Restructure network/ directory (protocol/ and transport/ subfolders)
- Move validation/ files back to respective subject folders
- Move menus/, panel/, debug_overlay/ under ui/
- Remove Discord integration

## 2026-06-23

- Rename dvlnet/ to net_transport/
- Subdivide utils/ into string/, sdl/, endian/, image/, container/, file/
- Subdivide engine/ into audio/, gfx/, load/, math/ subdirectories
- Rename DiabloUI/ to menus/
- Collect validation files into Source/validation/
- Merge control/ and panels/ into panel/ directory
- Remove compatibility forwarders and update all includes to canonical paths
- Migrate remaining Source/ files to domain subdirectories
- Migrate game domains to Source/game/
- Update docs
- Fix portal migration verification
- Document portal source migration
- Move portals into game domain
- Add canonical portal domain interfaces
- Plan portal domain source migration
- Document source organization architecture

## 2026-06-22

- Replace gendung macro shims with accessors
- Plan gendung macro shim removal
- Require comments on level accessors
- Document gendung macro shim removal

## 2026-06-21

- Add positioned tile grid iteration
- Document tile grid iteration API
- Plan tile position range implementation
- Document tile position range design
- Modernize dungeon level tile storage
- Document unified dungeon megatile storage
- Document legacy dTransVal removal

## 2026-06-20

- Remove legacy dPiece storage
- Document legacy dPiece removal
- Move AI docs under docs/AI
- Move player helpers into Player
- line endings
- Add graphical debug piece selector
- Fix debug overlay windows and editor selection
- Expand debug overlay: add Inspector panel & toolbar
- Add built-in help system to debug Lua console

## 2026-06-19

- Add tile fallback and sync with legacy map data
- Refactor: add levelMicros() span for tile micro access
- Migrate dungeon flags and special data to Tile API
- Remove legacy dungeon entity arrays, use Tile API only
- Refactor tile access to use tileAt(Point) API
- Add object subsystem unit tests and test visibility
- Complete Tile state migration
- Update CMake, codecov, and level init in tests/benchmarks
- Add ImGui debug overlay console and refactor console history

## 2026-06-18

- Phase 4B COMPLETE: Update documentation
- Phase 4B-12,13,14: Final entity macro migrations
- Phase 4B-9,10,11: Migrate debug, towners, sync, inv to Tile API
- Phase 4B-8: Migrate cursor.cpp to Tile API entity accessors
- Phase 4B-7: Migrate missiles.cpp to Tile API entity accessors
- Phase 4B-6: Migrate loadsave.cpp to Tile API entity accessors
- Phase 4B-3,4,5: Migrate dead.cpp, items.cpp, objects.cpp to Tile API
- Phase 4B-1 & 4B-2: Migrate player.cpp and monster.cpp to Tile API entity accessors

## 2026-06-17

- Phase 4A lighting migration: Tile API conversion, tests, benchmarks
- Better agent instructions
- Migrate rendering hot-paths to Tile API, update docs

## 2026-06-16

- Add Tile class and Level integration (Phase 2)
- Migrate dItem to be level-specific, not global
- Refactor dItem to be level-specific, not global
- Minor items.h cleanup
- Refactor active entity bookkeeping to pool headers
- Refactor to use MonsterPoolAdapter and ItemPoolAdapter
- Introduce monster pool for modern entity management

## 2026-06-15

- Clean up player attribute migration
- Clarify packed player validation fields
- Group player damage bonuses
- Group player life and mana resources
- Normalize player header line endings
- Modernize object storage with DenseEntityPool
- Refactor object pool access via ObjectPoolAdapter

## 2026-06-14

- Modernize object pool with type-safe container
- Introduce ObjectManager facade for object operations
- Minor comment clean-up to objects.h
- Initial objects.h cleanup
- Add shared attribute value types
- Plan player attribute model migration
- Document player attribute model design

## 2026-06-13

- Refactor player logic into Player class methods
- Introduce World and Level state containers
- Introduce Bestiary and migrate Monster behavior
- Add CombatActor base type
- Refactor players and monsters onto Actor
- Introduce Bestiary class and related engine updates
- Introduce Actor base struct for Player, Monster
