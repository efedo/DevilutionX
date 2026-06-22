# Source File Organization Design

## Purpose

This design defines a long-term source organization for Devilved that combines
gameplay-domain ownership with centralized infrastructure. It also defines a
backward-compatible migration path from the current mostly flat source tree.

The reorganization should make code easier to locate, reduce oversized source
files, clarify dependency direction, and support incremental modernization
without changing runtime behavior or compatibility-sensitive formats.

## Goals

- Organize gameplay logic around recognizable game domains.
- Centralize genuinely cross-cutting engine and infrastructure code.
- Split oversized files by stable responsibility.
- Make dependency direction understandable from directory placement.
- Preserve existing C++ APIs during migration through compatibility facades.
- Preserve savegame, network, Lua, data-file, and runtime behavior.
- Keep each migration change small enough to review and verify independently.

## Non-Goals

- Rewriting gameplay systems during file moves.
- Replacing CMake or the existing object-library build structure.
- Immediately removing legacy APIs or global state.
- Changing savegame layouts, network protocols, Lua APIs, or asset formats.
- Enforcing a rigid maximum source-file length.

## Target Source Layout

```text
Source/
  application/
  game/
  engine/
  ui/
  input/
  network/
  persistence/
  scripting/
  platform/
  support/
```

### `application`

Owns executable-level orchestration:

- Startup and shutdown.
- Command-line processing.
- Application initialization.
- Session lifecycle.
- Main-menu and game-loop transitions.
- High-level event dispatch.

This area coordinates other modules but should contain little game-rule logic.
The responsibilities currently concentrated in `diablo.cpp` should migrate
here over time.

### `game`

Owns game rules and domain state:

```text
game/
  items/
  monsters/
  objects/
  players/
  levels/
  quests/
  spells/
  missiles/
  stores/
  towners/
  portals/
```

Each domain should expose a narrow interface and own operations that primarily
act on its state. Domain code may use engine and support facilities but should
not depend on concrete UI, network transport, persistence, or platform
implementations.

The current `World`, `Level`, tile, and entity-pool modernization belongs in
this layer. Existing locations can remain temporarily while compatibility
headers provide stable include paths.

### `engine`

Owns reusable runtime facilities that do not encode Diablo-specific rules:

- Graphics and rendering primitives.
- Audio playback and mixing interfaces.
- Asset loading and image conversion.
- Timing and event facilities.
- Geometry, surfaces, palettes, and pathfinding primitives.
- Filesystem-facing abstractions used by multiple higher layers.

Existing reusable code under `Source/engine` should remain here, with
subdirectories introduced only where they improve navigation.

### `ui`

Owns user-facing presentation and interaction:

```text
ui/
  menus/
  dialogs/
  hud/
  inventory/
  character/
  spells/
  debug/
```

This consolidates the current `DiabloUI`, `panels`, `control`, debug-overlay,
and presentation-oriented quality-of-life code. UI code may query and invoke
game-domain interfaces but should not own game rules.

### `input`

Owns keyboard, controller, joystick, touch, key-mapping, and input-mode
translation. It converts device events into neutral input commands consumed by
application orchestration or gameplay adapters. Those command types belong to
the input boundary so input does not depend back on application.

This replaces the ambiguous distinction between the current `control` and
`controls` directories: presentation controls move to `ui`, while device and
command mapping move to `input`.

### `network`

Owns multiplayer infrastructure:

- Transport implementations.
- Packet framing and encryption.
- Protocol encoding and decoding.
- Command synchronization and dispatch.
- Compatibility wrappers for legacy Storm APIs.

Game domains define the state and operations being synchronized. Network code
defines their wire representation and delivery. Wire-compatible structures
must remain unchanged unless handled by a separate compatibility design.

### `persistence`

Owns durable representation and archive concerns:

- Savegame and hero-file serialization.
- Version migration.
- MPQ and unpacked-save integration.
- Persistent configuration where it is not application orchestration.

Domain objects should not directly own binary format details. Persistence code
may access explicitly supported domain state through stable interfaces or
compatibility adapters.

### `scripting`

Owns the Lua runtime, module registration, events, autocomplete, REPL, and
bindings. Bindings adapt game and engine interfaces without becoming the
canonical implementation of game behavior.

### `platform`

Owns platform-specific implementations and entry-point adaptations. Platform
code should implement interfaces defined by engine, application, or support
modules rather than introduce platform checks throughout gameplay code.

### `support`

Owns generic low-level helpers without game-domain knowledge:

- Containers and spans.
- String and UTF-8 helpers.
- Parsing utilities.
- Logging.
- Endian and serialization primitives.
- Compile-time and compatibility helpers.

Code should move here from `utils` only when it has no engine or gameplay
responsibility. `utils` should be retired gradually rather than renamed in one
large change.

## Dependency Direction

The intended dependency direction is:

```text
application -> game, ui, input, network, persistence, scripting
ui          -> game, engine, support
input       -> engine, support
network     -> game interfaces, support
persistence -> game interfaces, support
scripting   -> game interfaces, engine, support
game        -> engine, support
engine      -> support
platform    -> application/engine/support interfaces
support     -> third-party and standard-library facilities only
```

These are directional constraints rather than a demand for immediate physical
purity. Existing cycles should be removed incrementally. New dependencies
should follow the target direction unless a documented compatibility
constraint requires otherwise.

Gameplay modules must not acquire dependencies on concrete UI, transport,
serialization, or platform implementations. Infrastructure code may adapt
domain interfaces but must not become the owner of domain behavior.

## Large-File Decomposition

Files should be divided by responsibility rather than by line count. A split
should create units that can be named, understood, tested, and changed
independently.

### Items

The responsibilities currently concentrated in `items.cpp` should move toward:

```text
game/items/
  item.hpp
  item_pool.cpp
  generation.cpp
  affixes.cpp
  equipment.cpp
  durability.cpp
  placement.cpp
  interaction.cpp
  validation.cpp
```

Serialization belongs in `persistence`, network representations belong in
`network`, and inventory presentation belongs in `ui`.

### Monsters

`monster.cpp` should be decomposed into:

- Lifecycle and allocation.
- Spawning and placement.
- AI decisions.
- Movement.
- Combat and damage.
- Animation and presentation state.
- Validation.

The exact file names should follow the stable concepts discovered during the
mechanical extraction rather than imposing speculative abstractions.

### Objects

`objects.cpp` should be decomposed into:

- Lifecycle and pool management.
- Dungeon placement.
- Interaction dispatch.
- Doors.
- Traps.
- Shrines and fountains.
- Quest-specific object behavior.

The existing object-manager facade should remain the preferred public entry
point while implementation files are separated.

### Players

`player.cpp` should be decomposed into:

- Lifecycle and initialization.
- Movement.
- Combat.
- Progression and attributes.
- Animation.
- Equipment-derived state.
- Validation.

### Missiles

`missiles.cpp` should be decomposed into:

- Definitions and lookup.
- Creation and spawning.
- Movement.
- Collision.
- Damage and effects.
- Specialized missile behaviors.

### Application and Networking

`diablo.cpp` should gradually yield startup, session, game-loop, and event
dispatch files under `application`.

`msg.cpp` should separate command encoding, decoding, dispatch, and state
synchronization under `network`. Protocol-compatible definitions should remain
stable and explicitly documented.

## Public Headers and Compatibility

During migration, old include paths remain valid. An old header may:

- Include the new canonical header.
- Re-export an existing declaration.
- Provide an inline compatibility wrapper.
- Retain a legacy facade whose implementation delegates to the new module.

Compatibility headers must not create duplicate ownership or parallel
implementations. Every type and operation has one canonical definition.

Removing an old path or API requires a separate migration after all in-tree
call sites have moved and compatibility requirements have been reviewed.

## CMake Organization

The existing object-library strategy should continue. Physical source modules
should correspond to focused CMake targets where doing so improves dependency
visibility or standalone testing.

Recommended direction:

- Add a `CMakeLists.txt` to substantial top-level modules.
- Keep small domain files grouped into one object library until a narrower
  target provides measurable clarity.
- Avoid one target per source file.
- Express dependencies through target links rather than global includes.
- Preserve the final `libdevilutionx` aggregation target and executable shape.

Initial file moves may continue to be listed in `Source/CMakeLists.txt`.
Subdirectory build files should be introduced incrementally, not as a
repository-wide prerequisite.

## Migration Phases

### Phase 1: Establish Boundaries

- Document the target layout and dependency rules.
- Create directories only when the first real module moves into them.
- Add compatibility-header conventions.
- Prevent new unrelated files from being added to the flat `Source` root.

### Phase 2: Move Cohesive Existing Modules

- Start with small modules whose responsibilities already match the target.
- Move files without redesigning behavior.
- Preserve old include paths.
- Update CMake and tests in the same change.

Likely early candidates include portals, quests, validation modules, game-mode
state, and narrowly scoped quality-of-life or UI components.

### Phase 3: Mechanically Split Large Files

- Extract one responsibility at a time.
- Preserve declarations, function signatures, globals, and initialization
  order.
- Avoid semantic cleanup in extraction commits.
- Run focused tests after every extraction.

### Phase 4: Separate Cross-Cutting Responsibilities

- Move save-format code to `persistence`.
- Move protocol and synchronization code to `network`.
- Move presentation code to `ui`.
- Move device handling and command mapping to `input`.
- Move Lua adaptation to `scripting`.

Domain interfaces should be narrowed as these dependencies are separated.

### Phase 5: Reduce Compatibility Facades

- Migrate remaining in-tree callers to canonical headers.
- Remove obsolete forwarding headers only when backward-compatibility policy
  permits.
- Consolidate or remove transitional globals and aliases through separate,
  behavior-preserving designs.

## Change Discipline

Each migration change should:

- Move or split one coherent responsibility.
- Preserve behavior and compatibility.
- Avoid combining file movement with algorithmic modernization.
- Update CMake, succinct code documentation, and the changelog.
- Add tests for behavior changes; rely on existing coverage for purely
  mechanical moves only when that coverage exercises the affected paths.
- Keep commits independently buildable and reviewable.

Where a responsibility has weak test coverage, characterization tests should
be added before extraction.

## Verification

For each migration:

1. Build the narrowest affected target.
2. Run focused GTests for the affected domain.
3. Run formatting and compile checks appropriate to the touched files.

At phase boundaries:

1. Configure and build the game with the documented MSVC workflow.
2. Build all registered GTest executables.
3. Run the complete registered GTest suite with CTest.
4. Verify representative save loading, multiplayer compatibility, Lua startup,
   and asset loading when those boundaries were touched.

## Success Criteria

The reorganization is successful when:

- New contributors can locate code by domain or infrastructure responsibility.
- The flat `Source` root no longer contains major gameplay implementations.
- Oversized source files have been split into stable, named responsibilities.
- Directory placement communicates dependency direction.
- Game-domain code no longer directly owns UI, transport, persistence, or
  platform implementations.
- Old APIs continue to work for as long as backward compatibility requires.
- Full builds and tests remain green throughout the migration.
