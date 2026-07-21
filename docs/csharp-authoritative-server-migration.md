# C# Authoritative Server and Godot Migration Plan

## Purpose

This document defines an incremental path from the current C++ engine with embedded Lua scripting to:

- A C# authoritative game server.
- A Godot client that does not own gameplay state.
- C# server extensions for trusted gameplay customization.
- C# Godot components for client-only presentation customization.
- No Lua or sol2 dependency in the final system.

The migration must keep the current game playable while systems move one at a time. It must not require embedding .NET in the C++ engine or translating every Lua API directly to C#.

## Target Architecture

The final architecture has four primary components:

| Component | Responsibility |
|---|---|
| C# domain library | Deterministic rules, simulation state, commands, events, validation, and game-data models |
| C# server host | Sessions, networking, persistence, ticking, plugin loading, and operational concerns |
| Godot client | Input, rendering, audio, UI, prediction/interpolation, and presentation effects |
| Protocol package | Versioned commands, snapshots, events, identifiers, errors, and content manifests |

The C# domain library must not reference Godot. The server host and Godot client may both reference the protocol package, but only the server may mutate authoritative state.

Single-player should use the same protocol as multiplayer. It may launch a local server process or host the server in-process, but it must not use a separate client-authoritative rules path.

## Authority Rules

The server owns:

- Simulation time and tick ordering.
- Random-number generation and seeds.
- Player and monster positions accepted by the simulation.
- Combat, damage, healing, experience, and leveling.
- Item generation, inventory, equipment, gold, and stores.
- Spells, missiles, monster AI, objects, quests, and level state.
- Persistence and save validation.
- The active gameplay mod set and content manifest.

The client owns:

- Input collection and command submission.
- Rendering, animation, audio, menus, HUD, and accessibility presentation.
- Interpolation and explicitly permitted prediction.
- Presentation-only features such as clocks and floating numbers.

The client sends intent, such as `MoveRequested`, `AttackRequested`, `CastRequested`, or `PurchaseRequested`. It must not send final positions, damage, item rolls, gold totals, or other authoritative results.

## Current Lua Responsibilities

Lua is currently more than a script runner. `LuaReloadActiveMods()` in `Source/lua/lua_global.cpp` coordinates:

- Active mod selection and archive loading.
- Hellfire archive loading and mode activation.
- Store callback cleanup.
- Script loading and event registration.
- Sound shutdown and reinitialization.
- Reloading text, player, spell, missile, monster, item, object, quest, set-level, quest-pool, and level-generation data.
- Mod-change notifications.

The Lua surface also includes:

- Gameplay mutation through player, item, monster, towner, and Hellfire modules.
- Presentation through render, audio, and floating-number modules.
- Events for game start, draw completion, data loading, stores, damage, and experience.
- Developer commands, autocomplete, and a REPL.
- Direct exposure of mutable C++ objects through sol2 userdata.

These responsibilities must be separated before Lua can be removed safely.

## Migration Principles

1. Do not embed C# into the current C++ engine. Communicate with the C# server through the same protocol intended for Godot.
2. Do not port Lua files one-for-one. Move behavior to the component that should own it in the final architecture.
3. Keep TSV data canonical during the first migration stages. Replacing both code and data formats simultaneously would make parity harder to measure.
4. Preserve legacy numeric behavior where it affects gameplay, including fixed-point units, integer truncation, RNG sequencing, and ordering.
5. Introduce stable entity and content identifiers before network messages depend on them. Pointer-derived IDs are not valid protocol IDs.
6. Make each migration step reversible behind a temporary feature flag until parity tests pass.
7. Avoid running two authoritative simulations in production. Dual execution is for test and shadow comparison only.
8. Freeze the public Lua API once migration starts. New gameplay capabilities should be added to the C# domain or protocol instead.

## Workstreams

The work is divided into five parallel but ordered workstreams:

| Workstream | Scope |
|---|---|
| C++ decoupling | Remove mod, data, event, and tooling responsibilities from the Lua lifecycle |
| Protocol | Define stable IDs, commands, events, snapshots, errors, versioning, and transport-independent framing |
| C# domain and server | Port authoritative rules and host them in the separate server project |
| Godot client | Consume authoritative state and replace C++ presentation systems |
| Content and mods | Version data packs and split trusted server plugins from client presentation extensions |

## Phase 0: Decisions and Behavioral Baseline

### Tasks

1. Record architecture decisions for:
   - The canonical protocol schema format. A schema-first IDL with C++ and C# generation is preferred.
   - Reliable transport for the first implementation. The first version should favor correctness and observability over custom low-latency transport work.
   - Server tick rate and command ordering.
   - Local-server hosting for single-player.
   - C# plugin trust and deployment policy.
   - Save ownership and migration strategy.
2. Inventory every Lua module, function, event, and bundled script.
3. Classify each Lua capability as server-authoritative, client-presentation, data-loading, or developer tooling.
4. Freeze the Lua API except for compatibility fixes.
5. Add characterization tests for behavior that will move:
   - Item generation for known seeds.
   - Store inventory, prices, purchases, repairs, and gold changes.
   - Player stat, life, mana, and experience changes.
   - Damage calculations and event ordering.
   - Quest selection and state transitions.
   - Mod reload ordering and Hellfire activation.
6. Create a replay fixture format containing initial content version, seed, commands, ticks, and expected state hashes.

### Exit Criteria

- Every current Lua entry point has an assigned target owner.
- The protocol, tick, local-server, plugin, and persistence decisions are written down.
- The first deterministic replay fixtures run against the C++ implementation.
- No new gameplay feature is added through the Lua API.

## Phase 1: Decouple Lua from C++ Infrastructure

This phase should not intentionally change gameplay.

### 1. Introduce `ModManager`

Move the following out of `LuaReloadActiveMods()`:

- Reading the active mod list.
- Unloading and loading mod archives.
- Loading Hellfire archives and setting the active game mode from a manifest.
- Producing an ordered list of active content packs.
- Notifying consumers after the active set changes.

Replace the executable Hellfire Lua bootstrap with declarative mod metadata. The current `loadData()` and `enable()` calls are configuration, not behavior that requires a scripting language.

### 2. Introduce `GameDataManager`

Create one explicit, tested data-reload pipeline. It should accept the ordered content-pack list and load data in a documented order. Sound reinitialization should be a separate subscriber rather than part of script execution.

The manager should produce:

- A content version.
- A deterministic content manifest/hash.
- Validation errors that identify the source pack and file.
- A notification after a complete successful reload.

Do not expose individual `add*DataFromTsv` functions as general mutation APIs. Content packs should declare their data overlays in a manifest.

### 3. Introduce Engine-Neutral Events

Add a small typed event dispatcher outside `Source/lua`. Initial events should cover the current Lua events but use stable values:

- `GameStarted`
- `StoreOpened`
- `MonsterDamaged`
- `PlayerDamaged`
- `ExperienceGranted`
- `GameDataReloaded`
- `FramePresented` for temporary client presentation compatibility

The temporary `LuaEventAdapter` should subscribe to these events and call the existing Lua handlers. Gameplay code must publish neutral events and stop including `lua/lua_event.hpp` directly.

Events must carry entity IDs and immutable data, not `Player *`, `Monster *`, `Item *`, or writable userdata.

### 4. Separate Developer Tooling

Define an engine-neutral debug command registry. Adapt existing debug commands to it, then make the Lua REPL one optional frontend. This prevents Lua removal from also removing essential diagnostics.

### Exit Criteria

- Mod and game-data reload works when Lua scripts are disabled.
- Gameplay and table-loading code no longer includes Lua headers.
- All Lua events flow through one adapter.
- sol2 types occur only under `Source/lua` and the temporary adapter boundary.
- Existing tests and replay fixtures still match.

## Phase 2: Establish the C# Domain and Protocol

### Recommended C# Project Boundaries

The separate server repository should contain projects equivalent to:

```text
Game.Domain        Pure game state and deterministic rules
Game.Data          TSV parsing, validation, overlays, and content manifests
Game.Protocol      Generated contracts and protocol versioning
Game.Server        Sessions, transport, tick loop, persistence, and plugins
Game.ReplayTests   Golden replays and C++/C# parity fixtures
```

### Protocol Foundation

Define:

- `SessionId`, `PlayerId`, `EntityId`, `LevelId`, `ItemInstanceId`, and stable content IDs.
- Handshake fields for protocol version, game version, content hash, and server mod hash.
- Command sequence numbers and target simulation ticks.
- Explicit rejection messages with machine-readable reason codes.
- Full initial snapshots and incremental state/event messages.
- Reconnect and resynchronization behavior.

Start with reliable ordered delivery and keep message contracts independent of transport. Add unreliable snapshots only after profiling demonstrates a need.

### Deterministic Domain Foundation

Port foundational behavior before feature systems:

- Fixed-point numeric types and legacy conversion rules.
- RNG implementation, seed ownership, and stream separation policy.
- Coordinates, directions, rectangles, and level identity.
- Stable entity allocation and lookup.
- Tick scheduler and deterministic command ordering.
- State hashing for replay comparison.

### Data Parity

Implement C# loaders for the existing TSV files. Both implementations must agree on:

- Row identity and enum mappings.
- Defaults and optional fields.
- Overlay order.
- Validation behavior.
- The final content manifest hash.

Generate or validate shared symbolic IDs so C++ and C# cannot silently assign different ordinals.

### Exit Criteria

- C# loads the same base and Hellfire data as C++.
- Known data sets produce matching content identities.
- RNG and fixed-point golden tests match.
- A client can handshake, submit a no-op command, and receive a versioned snapshot.
- Replay fixtures can execute against both C++ and C# implementations.

## Phase 3: First Vertical Slice - Inventory and Stores

Inventory and stores should be the first authoritative slice because they are discrete, security-sensitive, already well tested, and touch current Lua behavior.

### Server Work

1. Port item instances, inventory layout, equipment, belt, gold, and derived-stat recalculation.
2. Port store stock generation, pricing, purchase, sale, repair, recharge, and identification.
3. Make the server own all item seeds and store RNG.
4. Implement commands such as:
   - `OpenStore`
   - `BuyItem`
   - `SellItem`
   - `RepairItem`
   - `RechargeItem`
   - `IdentifyItem`
   - `MoveInventoryItem`
5. Emit state changes and presentation events after accepted commands.

### Existing C++ Client Work

1. Add a remote-authority adapter behind a feature flag.
2. Render inventory and store state received from the server.
3. Convert UI actions into commands rather than direct mutations.
4. Retain the local implementation for parity testing until the remote path passes all fixtures.

### Comparison Strategy

For each fixture, run the same initial data, seeds, and commands through both implementations. Compare normalized state after every command. Differences must be explained and recorded; network mode must not quietly adopt changed legacy behavior.

### Exit Criteria

- The server rejects invalid or unaffordable transactions without client-side trust.
- C++ remote mode completes inventory and store flows.
- Golden transactions match legacy behavior.
- The Adria mana-refill behavior has a C# server rule and does not require Lua in remote mode.

## Phase 4: Move Remaining Authoritative Systems

Migrate in dependency order:

1. Player stats, resources, leveling, spells, and status effects.
2. Level identity, occupancy, movement validation, and portals.
3. Combat resolution, missiles, damage, healing, and death.
4. Item drops and world-item ownership.
5. Monsters, targeting, pathfinding, and AI.
6. Objects, traps, shrines, and environmental interactions.
7. Quests, towners, dialogs, and set-level transitions.
8. Level generation and persistent level state.
9. Save data, character persistence, reconnect, and recovery.

For each system:

1. Add or extend C++ characterization fixtures.
2. Define commands, events, and snapshot fields.
3. Port the rules to `Game.Domain`.
4. Run replay and state-hash comparisons.
5. Add a C++ remote-authority adapter.
6. Switch the feature default only after parity is demonstrated.
7. Remove the old local-authority path once rollback is no longer required.

### Exit Criteria

- The C++ client can complete a full single-player and multiplayer session against the C# server.
- The server is the only writer of gameplay state.
- Save/load and reconnect are server-owned.
- Long-running replay and soak tests show no unexplained divergence.

## Phase 5: Build the Godot Client

Godot work can begin once Phase 2 stabilizes the protocol; it does not need to wait for every server system.

Recommended order:

1. Connection, handshake, logging, and protocol diagnostics.
2. Asset loading and coordinate conversion.
3. Initial level snapshot and static rendering.
4. Entity spawning, movement, interpolation, and animation.
5. Input-to-command mapping.
6. HUD, inventory, stores, dialogs, and menus.
7. Audio and presentation events.
8. Prediction for selected actions, with authoritative correction.
9. Accessibility and developer overlays.

Do not share Godot nodes, vectors, resources, or scene objects with `Game.Domain`. Adapt protocol DTOs at the Godot boundary.

### Exit Criteria

- Godot can play the same server-hosted vertical slices as the C++ client.
- Captured protocol sessions can drive repeatable client tests.
- Client disconnection, stale commands, correction, and resynchronization are handled visibly.

## Phase 6: Replace Mods and Remove Lua

### Map Existing Bundled Scripts

| Existing Lua behavior | Destination |
|---|---|
| Hellfire bootstrap | Declarative content-pack manifest and server configuration |
| Adria refills mana | Trusted C# server rule triggered by an authoritative store interaction |
| Damage floating numbers | Godot C# presentation component consuming damage events |
| XP floating numbers | Godot C# presentation component consuming experience events |
| Clock | Godot C# presentation component |
| Data extension functions | Manifest-driven data overlays validated by `Game.Data` |
| Lua developer modules and REPL | Authenticated server admin commands plus Godot/C++ debug frontends |

### C# Plugin Policy

Server C# plugins are full-trust code unless isolated. Therefore:

- Only server administrators may install server assemblies.
- Clients may not upload or select executable server plugins.
- The server advertises plugin IDs, versions, and a manifest hash.
- Public servers should use allowlists or signed packages.
- Untrusted scripting, if required later, must use a real isolation boundary such as a separate process or sandboxed bytecode runtime.

Client extensions must not receive APIs that mutate authoritative state. They may consume snapshots and presentation events and submit the same validated commands available to normal UI code.

### Lua Removal Checklist

1. No non-Lua source includes a Lua or sol2 header.
2. No active content pack contains required `.lua` files.
3. Debug/admin workflows have non-Lua replacements.
4. Mod and data reload works without a Lua state.
5. Remove Lua initialization, shutdown, REPL, autocomplete, bindings, and assets.
6. Remove Lua and sol2 from CMake, vcpkg, source distributions, and documentation.
7. Remove the temporary Lua event adapter.
8. Build and test with no Lua headers or libraries available.

## Initial Pull Request Sequence

Keep the first changes small and behavior-preserving:

1. Add the Lua capability inventory and freeze notice; add missing characterization tests.
2. Add replay fixture types and state hashing for a minimal player/store scenario.
3. Extract `ModManager` from `LuaReloadActiveMods()` without changing reload behavior.
4. Extract `GameDataManager` and document/test reload order.
5. Add typed engine-neutral events and route current call sites through them.
6. Add `LuaEventAdapter`; remove Lua includes from gameplay and table modules.
7. Replace pointer-derived script IDs with stable runtime entity IDs.
8. Replace Hellfire Lua bootstrap with a declarative content-pack manifest.
9. Add the initial protocol schema, handshake, IDs, and generated C++/C# bindings.
10. Add the C# deterministic primitives, TSV loader, and parity tests.
11. Add a minimal C++ connection to the server and snapshot diagnostic screen.
12. Implement the inventory/store vertical slice behind a remote-authority feature flag.

Each pull request should include tests and update this document if it changes a decision, dependency, or milestone.

## Test Strategy

Use several complementary test layers:

- C++ characterization tests preserve current behavior during extraction.
- C# unit tests validate domain rules without networking or Godot.
- Contract tests ensure C++, C#, and Godot agree on message compatibility.
- Replay tests run identical seeds and commands through C++ and C#.
- State hashes detect the first divergent tick, not merely the final mismatch.
- Integration tests run a real server with a headless client.
- Soak tests exercise reconnects, delayed commands, malformed input, and long sessions.

Golden fixtures must record the content manifest and protocol version. A fixture generated from one content set must never be silently evaluated against another.

## Major Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Big-bang rewrite stalls before becoming playable | Maintain a working C++ client and migrate vertical slices |
| Hidden behavior changes during porting | Characterization tests, deterministic replays, and per-tick state hashes |
| C++ and C# assign different IDs | Schema-generated IDs or shared validated ID catalogs |
| Pointer-backed Lua objects leak into protocol design | Stable IDs, immutable events, and command handlers |
| Client remains accidentally authoritative | Server validates every command and owns every RNG decision |
| Data reload semantics change | Extract and test one ordered `GameDataManager` first |
| C# plugins become a remote-code-execution path | Administrator-installed trusted plugins only, or process isolation |
| Godot types contaminate server rules | Keep `Game.Domain` free of Godot dependencies |
| Single-player develops a separate rules path | Always use the protocol and a local authoritative server |
| Migration creates permanent duplicate implementations | Add explicit removal gates to every migrated system |

## Progress Tracking

Track progress by authoritative ownership rather than by percentage of translated files. For every subsystem, record one of:

- `C++ local`: existing engine still owns the rules.
- `Dual test`: C++ and C# run only for parity comparison.
- `C# remote`: server owns the rules; C++ client is an adapter.
- `Godot client`: Godot consumes the server implementation.
- `Legacy removed`: old C++/Lua implementation and dependencies are deleted.

The migration is complete when all gameplay subsystems are `C# remote`, the required client features are `Godot client`, and Lua satisfies the removal checklist.
