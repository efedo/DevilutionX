# C# Authoritative Server and Godot Migration Plan

## Purpose

This document defines an incremental path from the current C++ engine with embedded Lua scripting to:

- A C# authoritative game server.
- A Godot client that does not own gameplay state.
- External content packs for game-specific definitions and tuning.
- Versioned C# gameplay modules for authoritative behavior that is not
  reasonably declarative.
- C# server extensions for trusted gameplay customization.
- C# Godot components for client-only presentation customization.
- No Lua or sol2 dependency in the final system.

The migration must keep the current game playable while systems move one at a time. It must not require embedding .NET in the C++ engine or translating every Lua API directly to C#.

## Target Architecture

The final architecture has six primary components:

| Component | Responsibility |
|---|---|
| C# domain kernel | Engine-neutral deterministic primitives, simulation state machinery, commands, events, validation, and stable gameplay-module APIs |
| C# game-data runtime | External data loading, overlays, validation, symbolic IDs, and content manifests |
| C# gameplay modules | Game-specific authoritative rules that consume validated data and operate through domain APIs |
| C# server host | Sessions, networking, persistence, ticking, gameplay-module loading, and operational concerns |
| Godot client | Input, rendering, audio, UI, prediction/interpolation, and presentation effects |
| Protocol package | Versioned commands, snapshots, events, identifiers, errors, and content manifests |

The C# domain kernel and game-data runtime must not reference Godot or a
specific game module. Gameplay modules may reference their stable APIs, while
the server host composes and runs the selected modules. The server host and
Godot client may both reference the protocol package, but only the server may
mutate authoritative state.

"C# scripts" means versioned gameplay modules authored in C# and built as
server-loaded assemblies. The server must not compile or execute source code
provided by clients. The shipped game and administrator-installed extensions
use the same identity, loading, and compatibility model, although public
servers may apply stricter signing or allowlist policy to third-party modules.

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

## Phase 1 Status

The first behavior-preserving extraction has been implemented:

- `data/ModManager` now owns active mod archive unload/load operations.
- `data/GameDataManager` now owns the ordered reload of mod-overridable tables.
- `game/events/GameEventBus` now carries typed engine-neutral events.
- Lua subscribes through a temporary event adapter; gameplay and table code no
  longer calls Lua event functions directly.
- Damage and experience events cross the adapter boundary with stable runtime
  IDs, while the adapter temporarily resolves those IDs to legacy Lua userdata.

The remaining Phase 1 work is to extract sound reload, replace the Hellfire
Lua bootstrap with declarative content metadata, move debug commands behind an
engine-neutral registry, and add a content manifest/hash.

The Phase 0 replay baseline now also includes strict C++ and C# JSON fixture
loaders and an executable player/store checkpoint fixture. The C# store server
executes the fixture's supported commands and validates receipt ordering. Its
snapshot hash does not yet match the broader legacy C++ state projection; that
gap is explicit in the C# test suite and is the next parity target.

## Migration Principles

1. Do not embed C# into the current C++ engine. Communicate with the C# server through the same protocol intended for Godot.
2. Do not port Lua files one-for-one. Move behavior to the component that should own it in the final architecture.
3. Keep TSV data canonical during the first migration stages. Replacing both code and data formats simultaneously would make parity harder to measure.
4. Preserve legacy numeric behavior where it affects gameplay, including fixed-point units, integer truncation, RNG sequencing, and ordering.
5. Introduce stable entity and content identifiers before network messages depend on them. Pointer-derived IDs are not valid protocol IDs.
6. Make each migration step reversible behind a temporary feature flag until parity tests pass.
7. Avoid running two authoritative simulations in production. Dual execution is for test and shadow comparison only.
8. Freeze the public Lua API once migration starts. New gameplay capabilities should be added to the C# domain or protocol instead.
9. Prefer external declarative data for definitions and tuning. Put behavior in
   a C# gameplay module only when it cannot be represented clearly as validated
   data.
10. Keep game-specific rules out of the generic domain kernel and server host.
    Each migrated slice must explicitly classify its definitions, generic
    mechanics, and game-specific behavior.

## Workstreams

The work is divided into six parallel but ordered workstreams:

| Workstream | Scope |
|---|---|
| C++ decoupling | Remove mod, data, event, and tooling responsibilities from the Lua lifecycle |
| Protocol | Define stable IDs, commands, events, snapshots, errors, versioning, and transport-independent framing |
| C# domain and server | Build deterministic engine-neutral mechanics and host them in the separate server project |
| Godot client | Consume authoritative state and replace C++ presentation systems |
| External content | Move game-specific definitions and tuning into versioned data packs |
| C# gameplay modules and mods | Move non-declarative game rules into versioned server modules and split them from client presentation extensions |

## Progress Snapshot (2026-07-22)

| Phase | State | Implemented | Remaining gate |
|---|---|---|---|
| Phase 0: decisions and baseline | Mostly complete | Decisions, Lua inventory, C++ replay hashing, shared fixture format, command-delivery vector, deterministic initial replay checkpoint | Explicit mod-reload/Hellfire fixtures and full transition checkpoint parity |
| Phase 1: C++ decoupling | Partial | `ModManager`, `GameDataManager`, typed events, Lua adapter, stable event IDs | Sound reload, declarative Hellfire metadata, debug registry, content manifest/hash |
| Phase 2: C# domain and protocol | Partial | Protobuf schema and C#/opt-in C++ generation, bounded framing, C# TCP sessions, initial C++ handshake/command/acknowledgement/snapshot client, command admission/deduplication, snapshots, state hashing, replay/vector loaders, TSV/content hashing, gameplay-module contract, fixed-point/RNG/ID primitives, reconnect ledger/entity resumption | Wire C++ retries to transport, full snapshot resync, stable ID catalogs, complete transition parity, shared C++/C# content identity |
| Phase 3: inventory and stores | Server side started | External TSV store definitions, module-owned purchase/sale/repair/recharge/identification/movement rules, shared stock, wallet/inventory snapshots, item-state projection | Legacy pricing/generation parity, full inventory/equipment semantics, C++ remote adapter, golden transaction parity |
| Phase 4: remaining authoritative systems | Not started | Protocol placeholders for movement, combat, spells, and events | Domain implementations and remote adapters |
| Phase 5: Godot client | Not started | Target boundary documented | Godot project, connection, rendering, input, UI, and correction paths |
| Phase 6: content/modules and Lua removal | Not started | Target data/domain/module layering, capability destinations, and removal gates documented | Implement replacement paths, externalize shipped content/rules, and remove Lua/sol2 |

### Current Critical Path

1. Extend the now-matching `stores/basic-buy` projection through transition
   checkpoints, item generation, and full store state rather than only the
   deterministic initial baseline.
2. Wire the generated C++ command-delivery tracker into the new feature-flagged
   TCP client, then add reconnect and full snapshot-resynchronization tests.
3. Make the C++ and C# content manifest hashes match, then complete the store
   vertical slice with definitions in external data and game-specific behavior
   in the shipped C# gameplay module: item instances, inventory placement,
   store generation/pricing, sale, repair, recharge, identification, and the
   Adria mana-refill rule.
5. Switch stores to `C# remote` only after the C++ adapter passes command-level
   replay fixtures and state-hash comparisons.

## Phase 0: Decisions and Behavioral Baseline

### Decisions Recorded

#### Protocol schema

Use Protobuf `.proto` files as the canonical protocol schema. The initial
contract is `protocol/devilution.proto`; generate C++ and C# bindings from the
same definition, and keep the message contracts independent of the underlying
transport.

This provides one versioned contract for the current C++ client, the future C#
server, and the Godot client. Protobuf field numbers and explicit versioning
rules will be treated as compatibility-sensitive API. The wire format will
carry authoritative commands, snapshots, events, errors, and content identity
metadata; it will not expose engine pointers or Godot types.

#### Initial transport

Use TCP with length-delimited Protobuf envelopes for the first implementation.
The protocol package will remain transport-independent, and unreliable or
unordered delivery will not be introduced until profiling demonstrates a need
for it.

#### Simulation rate

Use a fixed 20 Hz authoritative simulation tick for the initial server. This
matches the current engine default and keeps replay behavior stable. Client
rendering and interpolation may run at a different frame rate, but gameplay
state changes occur only on server ticks.

#### Command delivery and ordering

Commands carry a client sequence number and requested simulation tick. The
server assigns a monotonically increasing receipt sequence and processes
accepted commands by `(target_tick, server_receipt_sequence)`.

The protocol also includes application-level acknowledgements. The client
tracks the measured connection latency and resubmits a command when its
acknowledgement does not arrive within an adaptive timeout. The server
deduplicates client sequence numbers, so resubmission cannot execute a command
twice. Replay fixtures record the server-assigned receipt sequence and the
resulting target tick.

Late-command handling is command-specific. Gameplay-critical commands use a
strict lateness window and are rejected or explicitly rescheduled when they
cannot be applied against a valid state. Non-critical commands may be accepted
at the current tick when doing so is safe and does not reinterpret stale
gameplay intent. The protocol will expose whether a command was accepted,
rejected, or rescheduled.

#### Single-player hosting

Single-player will launch the same authoritative server as a local process and
connect through the normal protocol. The C++ and Godot clients will not use a
separate client-authoritative rules path. An in-process server may be added
later only as an optimization if it preserves protocol and behavior parity.

#### C# gameplay-module trust and deployment

C# gameplay modules are full-trust server code. Shipped modules are part of the
server distribution; only the server administrator may install or enable
third-party modules. Clients cannot upload, choose, or execute server
assemblies. Public servers may require signed or allowlisted assemblies. The
server advertises the ordered module IDs, versions, and hashes as part of its
combined ruleset identity.

#### External content and C# gameplay modules

Game-specific definitions, tuning values, and relationships belong in external
content packs whenever they can be expressed declaratively. The migration keeps
the existing TSV files canonical until parity is established; format changes
may occur later as independently tested content migrations.

Game-specific behavior that cannot be represented cleanly as data belongs in a
versioned C# gameplay module, not in the generic domain kernel or server host.
The shipped Diablo rules module is loaded through the same stable module
contract used by trusted administrator-installed extensions. A server identity,
save, or replay fixture records the ordered content packs, gameplay modules,
versions, and hashes as one authoritative ruleset identity.

#### Save ownership and migration

The authoritative server exclusively owns save creation, validation, migration,
and persistence. Clients may request save-related operations but never write
authoritative character or world state. Local single-player saves are stored by
the local server process using the same versioned format as multiplayer saves.
Save migrations are explicit, validated, and tied to content and schema
versions; incompatible saves are rejected with an actionable reason.

### Baseline Status

- The Lua capability inventory and freeze rules are recorded in
  `docs/lua-capability-inventory.md`.
- The cross-language replay fixture envelope and initial scenario list are
  recorded in `docs/replay-fixtures.md`.
- Existing characterization coverage includes item generation, stores,
  inventory, player behavior, quests, vendors, and the typed event bus.
- C++ replay primitives cover canonical field encoding, SHA-256 state digests,
  deterministic command ordering, and an initial player/store projection. The
  C# server now loads the same fixture, executes its store commands, validates
  receipt ordering, and reports the still-unresolved checkpoint hash mismatch.
  Complete game-state projection and exact cross-language parity remain.
- Missing baseline coverage is explicit mod-reload/Hellfire behavior and
  fixture execution against the running C++ game state.
- The initial transport-independent Protobuf contract is recorded in
  `protocol/devilution.proto`; the C# server generates bindings from it while
  C++ code generation is intentionally deferred until the new client transport
  is introduced.
- The C++ client now has a transport-independent command-delivery tracker for
  adaptive acknowledgement retries and terminal server outcomes; transport
  wiring remains.
- `server/src/Devilution.Server` now provides the initial C# authoritative
  command boundary, including generated Protobuf bindings, session-scoped
  deduplication, globally ordered receipts, and the agreed late-command policy.
  Its TCP host validates the handshake and routes command batches; the executor
  now includes the first deterministic store/purchase vertical slice and
  projects baseline player resources, primary attributes, equipment slots,
  inventory layout, wallet, active-store, and purchased-item state into TCP
  snapshots. Snapshot hashes now cover those projected authoritative fields
  using the shared canonical encoding. Complete legacy-state hashing and
  parity with legacy store pricing remain; the core legacy item fields are now
  projected, while full replay fixture parity and any fields added later to
  the legacy item model are still pending.

### Tasks

1. Record architecture decisions for:
   - The canonical protocol schema format. A schema-first IDL with C++ and C# generation is preferred.
   - Reliable transport for the first implementation. The first version should favor correctness and observability over custom low-latency transport work.
   - Server tick rate and command ordering.
   - Local-server hosting for single-player.
   - C# gameplay-module trust and deployment policy.
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
- The protocol, tick, local-server, gameplay-module, and persistence decisions are written down.
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

### Current Status

Implemented:

- One versioned Protobuf schema with generated C# bindings.
- Opt-in C++ Protobuf generation, bounded envelope framing, and an initial
  C++ authoritative client for handshake, command acknowledgement, and snapshots.
- Length-prefixed TCP envelopes, handshake validation, bounded payloads, and
  loopback integration tests.
- Session-scoped command deduplication, global receipt ordering, explicit late
  outcomes, adaptive C++ retry policy, and a shared behavior vector consumed by
  C# tests.
- An injectable authoritative clock, per-connection entity allocation,
  authoritative snapshots, and canonical SHA-256 hashing.
- Strict C# replay fixture loading and store-command execution.
- C# TSV parsing, ordered content-pack hashing, gameplay-module loading and
  dependency validation, combined ruleset identity, legacy Q6 fixed-point
  arithmetic, Borland-compatible LCG behavior, and stable entity allocation.
- Exact initial `stores/basic-buy` parity with the deterministic C++ projection.

Remaining before Phase 2 exit:

- Wire `CommandDeliveryTracker` to the generated C++ client and preserve
  command retries across reconnects.
- Define stable session/player/level/item IDs and reconnect/resynchronization.
- Add fixed-point/RNG golden vectors from the C++ implementation and port
  remaining tick-scheduling semantics.
- Match the C++ TSV/content manifest identity and add stable symbolic ID
  catalogs for every protocol-visible content type.
- Add reconnect/resynchronization and complete transition fixtures against both
  implementations.

### Recommended C# Project Boundaries

The separate server repository should contain projects equivalent to:

```text
Game.Domain          Generic deterministic primitives, state machinery, and module APIs
Game.Data            TSV parsing, validation, overlays, and content manifests
Game.Rules.Diablo    Shipped game-specific C# behavior and rule composition
Game.Protocol        Generated contracts and protocol versioning
Game.Server          Sessions, transport, tick loop, persistence, and module loading
Game.ReplayTests     Golden replays and C++/C# parity fixtures
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

### Gameplay Module Foundation

Define a small deterministic module contract that provides:

- A stable module ID, semantic version, compatibility range, and assembly hash.
- Explicit registration for command handlers, event handlers, rule services,
  and data validators; modules must not use reflection-based discovery order as
  gameplay order.
- Deterministic dependency and load ordering with duplicate/conflict errors.
- Access only to approved domain services, RNG streams, clocks, content views,
  and state mutation APIs.
- Lifecycle hooks that do not expose transport, filesystem, wall-clock, Godot,
  or mutable server-host internals to deterministic gameplay code.

The first-party Diablo module and third-party trusted modules use this contract.
Third-party modules remain full-trust process code operationally, so API
restriction supports architecture and determinism but is not a security
sandbox.

### Exit Criteria

- C# loads the same base and Hellfire data as C++.
- Known data sets produce matching content identities.
- The server loads the shipped Diablo gameplay module and produces a stable
  combined content/module ruleset identity.
- RNG and fixed-point golden tests match.
- A client can handshake, submit a no-op command, and receive a versioned snapshot.
- Replay fixtures can execute against both C++ and C# implementations.

## Phase 3: First Vertical Slice - Inventory and Stores

Inventory and stores should be the first authoritative slice because they are discrete, security-sensitive, already well tested, and touch current Lua behavior.

### Current Status

The C# server currently owns a test-only store simulation with external TSV
definitions, shared stock, per-session wallets, open-store/purchase validation,
retry-safe execution, module-owned sale/repair/recharge/identification/movement
rules, and snapshots for baseline player resources, attributes, equipment
slots, inventory layout, and legacy item fields. It rejects invalid,
disallowed, or unaffordable transactions and sends updated snapshots over TCP.

This subsystem remains `Dual test`, not `C# remote`: legacy store generation,
pricing parity, complete inventory placement/equipment semantics, Adria's
mana-refill rule, and the C++ remote-authority adapter are not implemented.

### Server Work

1. Load item, affix, store, pricing, repair, recharge, and identification
   definitions from external content through `Game.Data`.
2. Keep generic item instances, inventory layout, equipment, belt, gold, and
   deterministic collection mechanics in `Game.Domain`.
3. Implement store generation, pricing policy, transactions, eligibility, and
   Adria's mana-refill behavior in `Game.Rules.Diablo`.
4. Make the server own all item seeds and store RNG.
5. Implement commands such as:
   - `OpenStore`
   - `BuyItem`
   - `SellItem`
   - `RepairItem`
   - `RechargeItem`
   - `IdentifyItem`
   - `MoveInventoryItem`
6. Emit state changes and presentation events after accepted commands.

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
- Item/store definitions are external data, and Diablo-specific transaction
  policy is owned by the shipped gameplay module rather than the server host.
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
2. Inventory hard-coded definitions, tuning values, Lua behavior, and C++
   behavior, then classify each as external data, generic domain mechanics, or
   game-specific C# module behavior.
3. Move definitions and tuning into validated external content packs while
   preserving stable IDs and overlay semantics.
4. Add generic mechanics and state APIs to `Game.Domain` only where they are
   reusable and game-neutral.
5. Implement game-specific behavior in `Game.Rules.Diablo` through the stable
   gameplay-module API.
6. Define commands, events, and snapshot fields.
7. Run data-manifest, module-identity, replay, and state-hash comparisons.
8. Add a C++ remote-authority adapter.
9. Switch the feature default only after parity is demonstrated.
10. Remove the old local-authority and Lua paths once rollback is no longer
    required.

### Exit Criteria

- The C++ client can complete a full single-player and multiplayer session against the C# server.
- The server is the only writer of gameplay state.
- Save/load and reconnect are server-owned.
- Every migrated subsystem has no unclassified game-specific definitions or
  behavior left in the generic domain or server host.
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

## Phase 6: Complete Content/Rule Externalization and Remove Lua

### Target Content and Rule Model

Every shipped or modded gameplay capability has exactly one primary home:

| Capability | Primary home |
|---|---|
| Definitions, tuning, localization keys, and relationships | External content pack |
| Generic deterministic state and mechanics | `Game.Domain` |
| Shipped Diablo-specific behavior | `Game.Rules.Diablo` gameplay module |
| Trusted server customization | Administrator-installed C# gameplay module |
| Client-only presentation behavior | Godot C# component or presentation data |

Content packs and gameplay modules form one ordered ruleset. The server
validates dependencies and compatibility before starting a session, computes a
combined identity, and records it in handshakes, saves, diagnostics, and replay
fixtures. Reload is transactional: either the complete data/module composition
validates and becomes active, or the previous ruleset remains active.

Built-in modules are deployed as compiled assemblies. Supporting editable C#
source during development is a build-tooling concern, not a production runtime
compiler or a client-upload mechanism.

### Map Existing Bundled Scripts

| Existing Lua behavior | Destination |
|---|---|
| Hellfire bootstrap | Declarative content-pack manifest and server configuration |
| Adria refills mana | Shipped `Game.Rules.Diablo` rule triggered by an authoritative store interaction |
| Damage floating numbers | Godot C# presentation component consuming damage events |
| XP floating numbers | Godot C# presentation component consuming experience events |
| Clock | Godot C# presentation component |
| Data extension functions | Manifest-driven data overlays validated by `Game.Data` |
| Lua developer modules and REPL | Authenticated server admin commands plus Godot/C++ debug frontends |

### C# Gameplay Module Policy

Server C# gameplay modules are full-trust code unless isolated. Therefore:

- Shipped modules come from the server distribution; only server administrators
  may install third-party assemblies.
- Clients may not upload or select executable server modules.
- The server advertises ordered module IDs, versions, and hashes as part of the
  combined ruleset identity.
- Public servers should use allowlists or signed packages.
- Untrusted scripting, if required later, must use a real isolation boundary such as a separate process or sandboxed bytecode runtime.

Client extensions must not receive APIs that mutate authoritative state. They may consume snapshots and presentation events and submit the same validated commands available to normal UI code.

### Lua Removal Checklist

1. No non-Lua source includes a Lua or sol2 header.
2. No active content pack contains required `.lua` files.
3. Every migrated subsystem has documented ownership across external data,
   generic domain mechanics, and C# gameplay-module behavior.
4. Debug/admin workflows have non-Lua replacements.
5. Content and gameplay-module reload works without a Lua state.
6. Remove Lua initialization, shutdown, REPL, autocomplete, bindings, and assets.
7. Remove Lua and sol2 from CMake, vcpkg, source distributions, and documentation.
8. Remove the temporary Lua event adapter.
9. Build and test with no Lua headers or libraries available.

## Initial Delivery Sequence Status

| Step | State | Notes |
|---|---|---|
| Lua capability inventory and freeze notice | Complete | Capability destinations and freeze rules are documented |
| Minimal replay fixture and state hashing | Complete for initial baseline | Both languages load the fixture and match the deterministic initial checkpoint; transition parity remains |
| Extract `ModManager` | Complete | Active archive ownership moved out of Lua |
| Extract `GameDataManager` | Partial | Reload ordering moved; content identity and validation remain |
| Add typed engine-neutral events | Complete for initial events | Damage and experience paths use stable IDs |
| Add `LuaEventAdapter` | Complete for initial events | Lua remains the temporary consumer |
| Replace pointer-derived IDs | Partial | Event IDs are stable; protocol-wide ID catalogs remain |
| Replace Hellfire Lua bootstrap | Not started | Requires declarative content metadata |
| Add protocol schema, handshake, IDs, and bindings | Partial | Schema, handshake, C# bindings, and opt-in initial C++ bindings/client exist; complete IDs remain |
| Add C# deterministic primitives, TSV loader, and parity tests | Partial | Fixed-point, LCG, TSV, hashing, and initial parity tests exist; C++ golden vectors and complete parity remain |
| Define gameplay-module contract and ruleset identity | Started | Explicit module registry, Diablo store rules, and combined identity exist; full module API remains |
| Extract first store/item data and Diablo rules module | Partial | External store data and transactions are module-owned; legacy pricing/generation remains |
| Add minimal C++ server connection | Complete for initial slice | Opt-in client proves handshake, command acknowledgement, and snapshot exchange; retry wiring and remote gameplay remain |
| Implement remote inventory/store slice | Started server-side | C++ feature flag and legacy transaction parity remain |

Each delivery should include tests and update this document if it changes a
decision, dependency, milestone, or ownership state.

## Test Strategy

Use several complementary test layers:

- C++ characterization tests preserve current behavior during extraction.
- C# unit tests validate domain mechanics and gameplay-module rules without
  networking or Godot.
- Data validation tests cover schemas, overlays, symbolic IDs, and actionable
  source-pack errors.
- Gameplay-module conformance tests cover identity, dependency order,
  registration order, compatibility failures, and forbidden nondeterministic
  service access.
- Contract tests ensure C++, C#, and Godot agree on message compatibility.
- Replay tests run identical seeds and commands through C++ and C#.
- State hashes detect the first divergent tick, not merely the final mismatch.
- Integration tests run a real server with a headless client.
- Soak tests exercise reconnects, delayed commands, malformed input, and long sessions.

Golden fixtures must record the content manifest, ordered gameplay-module
identity, and protocol version. A fixture generated from one ruleset must never
be silently evaluated against another.

## Major Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Big-bang rewrite stalls before becoming playable | Maintain a working C++ client and migrate vertical slices |
| Hidden behavior changes during porting | Characterization tests, deterministic replays, and per-tick state hashes |
| C++ and C# assign different IDs | Schema-generated IDs or shared validated ID catalogs |
| Pointer-backed Lua objects leak into protocol design | Stable IDs, immutable events, and command handlers |
| Client remains accidentally authoritative | Server validates every command and owns every RNG decision |
| Data reload semantics change | Extract and test one ordered `GameDataManager` first |
| Game-specific definitions remain hard-coded during the server port | Require an external-data classification and manifest comparison for every migrated slice |
| Game rules accumulate in the generic domain or server host | Require first-party behavior to use the same explicit gameplay-module boundary as extensions |
| Gameplay modules introduce nondeterminism | Expose deterministic services through a narrow API and test load/registration order |
| Third-party C# modules become a remote-code-execution path | Administrator-installed trusted modules only, or process isolation |
| Godot types contaminate server rules | Keep `Game.Domain` free of Godot dependencies |
| Single-player develops a separate rules path | Always use the protocol and a local authoritative server |
| Migration creates permanent duplicate implementations | Add explicit removal gates to every migrated system |

## Progress Tracking

### Current Ownership

| Subsystem | Ownership state | Next gate |
|---|---|---|
| C++ mod/data lifecycle | `C++ local` | Finish content manifest and remove remaining Lua orchestration |
| External content packs | `Dual test` | Match C++ TSV overlay behavior and content hashes |
| C# gameplay-module platform | `Dual test` | Complete API surface, compatibility checks, and deterministic service restrictions |
| Shipped Diablo gameplay module | `Dual test` | Match legacy store pricing/generation and move the remaining store rules |
| Typed gameplay events | `C++ local` | Expand event coverage and replace the temporary Lua adapter |
| Command delivery policy | `Dual test` | Wire generated C++ messages and retry policy to the TCP server |
| Protocol transport/server sessions | `Dual test` | Add reconnect and full snapshot resync around the initial C++ client |
| Replay fixture infrastructure | `Dual test` | Add transition checkpoints and full C++/C# state projection parity |
| Inventory/store authority | `Dual test` | Match legacy pricing/generation, complete placement semantics, and add C++ remote mode |
| Remaining gameplay systems | `C++ local` | Migrate in the Phase 4 dependency order |
| Godot presentation | Not started | Begin after the Phase 2 protocol boundary stabilizes |
| Lua/sol2 | `C++ local` compatibility layer | Remove only after all capability destinations are active |

Track progress by authoritative ownership rather than by percentage of translated files. For every subsystem, record one of:

- `C++ local`: existing engine still owns the rules.
- `Dual test`: C++ and C# run only for parity comparison.
- `C# remote`: server owns the rules; C++ client is an adapter.
- `External data`: game-specific definitions are owned by versioned content
  packs rather than hard-coded source.
- `C# module`: game-specific behavior runs through the gameplay-module contract
  rather than the generic domain or server host.
- `Godot client`: Godot consumes the server implementation.
- `Legacy removed`: old C++/Lua implementation and dependencies are deleted.

The migration is complete when all gameplay subsystems are `C# remote`, their
definitions and behavior have reached the appropriate `External data` and `C#
module` ownership states, the required client features are `Godot client`, and
Lua satisfies the removal checklist.
