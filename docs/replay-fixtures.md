# Deterministic Replay Fixture Format

Replay fixtures are the Phase 0 behavioral baseline for comparing the current
C++ implementation with the C# authoritative server. A fixture records
the content, seed, commands, tick ordering, and expected state hashes needed to
reproduce a run.

## Goals

- Reproduce the same run without relying on wall-clock time or network timing.
- Identify the first divergent tick rather than only comparing final state.
- Keep command order and server-assigned receipt order explicit.
- Reject evaluation against a different content or protocol version.
- Allow the same fixture to be consumed by C++ and C# test runners.

## Fixture envelope

The interchange representation is UTF-8 JSON. It is a test artifact, not the
runtime wire format; runtime messages remain length-delimited Protobuf.

```json
{
  "format_version": 1,
  "fixture_id": "stores/basic-buy",
  "protocol_schema_version": "0.1.0",
  "engine_build": "302023840",
  "content_manifest": {
    "id": "base-plus-hellfire",
    "version": "1",
    "sha256": "..."
  },
  "tick_rate_hz": 20,
  "rng_seed": 305419896,
  "initial_state": {
    "player_ids": [0],
    "level_id": { "theme": "town", "index": 0 }
  },
  "legacy_store_state": {
    "active_store": 1,
    "premium_item_count": 0,
    "premium_item_level": 3,
    "premium_item_seeds": [42]
  },
  "commands": [
    {
      "client_sequence": 1,
      "target_tick": 0,
      "server_receipt_sequence": 1,
      "kind": "OpenStore",
      "payload": { "towner": "griswold" }
    },
    {
      "client_sequence": 2,
      "target_tick": 1,
      "server_receipt_sequence": 2,
      "kind": "BuyItem",
      "payload": { "store_slot": 0 }
    }
  ],
  "checkpoints": [
    { "tick": 0, "state_sha256": "..." },
    { "tick": 1, "state_sha256": "..." }
  ],
  "final_state_sha256": "..."
}
```

## Canonical rules

- `format_version`, `protocol_schema_version`, `content_manifest`, and
  `tick_rate_hz` are mandatory.
- Commands are processed by `(target_tick, server_receipt_sequence)`.
- Client sequence numbers are retained to test retry and deduplication.
- A fixture must include a checkpoint at every tick for the first baseline
  scenarios. Later fixtures may checkpoint at selected intervals once the
  state-hash implementation is established.
- State hashing uses a canonical field order and excludes presentation-only
  values, pointers, memory addresses, wall-clock timestamps, and localized
  strings.
- Missing, malformed, or mismatched content/protocol metadata invalidates the
  fixture rather than producing a misleading comparison.

## Initial fixture set

The first fixtures should cover one narrow behavior each:

1. Store opening, stock generation, and deterministic pricing.
2. Successful purchase and insufficient-gold rejection.
3. Repair, recharge, sale, and identification transactions.
4. Item generation for known seeds and quality parameters.
5. Player experience, level thresholds, life, and mana changes.
6. Portal transitions, world-item pickup, object activation, and quest progress.
7. Monster and player damage with event ordering.
8. Quest selection and quest-pool state transitions.
9. Mod reload ordering and Hellfire activation.

`test/fixtures/replay/stores/transaction-parity.json` is the first shared
transaction fixture. It exercises open-store, purchase, sale, and mana-refill
commands in both language loaders. Its five transaction checkpoints are real
C# snapshot SHA-256 values. The C# executor and the native replay executor
validate each checkpoint after the corresponding command.

`test/fixtures/replay/stores/base-content-purchase.json` repeats the purchase
and sale transition against the checked-in base manifest and the real base
store/item catalog. Its three checkpoints are consumed by both replay runners;
the C# runner loads `server/content/base` rather than an inline synthetic
catalog.

The C++ replay primitives now provide canonical field encoding, SHA-256
digests, command ordering, a protocol-shaped player/store state projection, and
native transition execution for the shared store fixture. The strict envelope
parser, `stores/basic-buy`, and `stores/transaction-parity` fixtures exercise
that projection with an explicitly value-initialized C++ baseline. The C# and
C++ hash projections include baseline resources, primary attributes, equipment,
belt contents, inventory layout, multi-cell item footprints, store stock, and
the complete protocol item state in the same field order.

## CI parity gate

`.github/workflows/authoritative-parity.yml` builds the native `replay_test`
runner and executes it from the CMake build directory, then builds and runs the
C# authoritative-server test runner. Both runners consume the same fixtures
from `test/fixtures/replay`; either implementation failing a checkpoint fails
the gate. Checkpoint failures identify the first command tick whose canonical
state hash diverges.

The gate intentionally runs the native and C# runners as separate processes.
This keeps the comparison independent of shared implementation details while
using the fixture hashes as the cross-language contract.

The authoritative server now also exposes bounded movement, blocked-cell
validation, level-aware portal transitions, life/mana maxima, character level,
healing, haste/status expiry, adjacent combat, monster snapshots, catalog-driven
drops, world-item pickup, damage spells, and line-of-sight validation. Native
snapshot projection carries level, status, monster, and world-item fields;
native event projection applies authoritative damage and experience batches;
and both hashers include the new canonical fields. The shared gameplay fixtures
now cover movement/combat, healing, portal transitions, world-item pickup, and
status expiry in both language runners. Object activation now supports an
external quest link and advances the linked quest in the same authoritative
transition. Content reload and broader world occupancy remain the next replay
additions.

Native legacy item projections now preserve the Diablo fixed-point and modifier
conventions used by the C++ item system, including elemental damage/arrow flags,
resistance clamping, and indestructible durability. World interaction lookup is
cell-based for authoritative monsters, world items, and objects, so projection
ordering cannot change the selected server entity.

`test/fixtures/replay/gameplay-multi-level-occupancy.json` covers a portal
transition while a world item and object remain on the source level. Both
runners preserve those entities with their original level IDs, and the C# save
tests verify that the same multi-level entity set survives persistence.

## Executable baseline fixture

`test/fixtures/replay/stores/basic-buy.json` is copied into the test fixture
directory and consumed by the C++ and C# replay tests. It verifies the versioned
envelope, preserves client sequence numbers, sorts commands by authoritative
order, and compares the canonical player/store checkpoint hash. The parser
skips unknown metadata and command payload fields, so the fixture format can
grow without adding a JSON dependency to the engine.

## Existing C++ characterization coverage

| Behavior | Current tests | Coverage assessment |
| --- | --- | --- |
| Item generation and unique-item availability | `items_test`, `vendor_test`, `replay_test` | Existing deterministic cases; convert selected cases to replay fixtures |
| Store inventory and pricing | `vendor_test`, `stores_test`, `store_transaction_test`, `replay_test` | Shared purchase/sale/refill transitions and normalized checkpoints |
| Purchases, sales, repairs, recharge, identification, and gold | `store_transaction_test` | Broad success/failure coverage; add command-level fixture inputs |
| Player stats/resources and experience | `player_test`, `replay_test` | Projection exists; add explicit experience/life/mana transition fixtures |
| Damage calculations and event order | `monster_test`, `player_test`, `game_event_bus_test` | Partial; add damage-state and ordering checkpoints |
| Quest selection and transitions | `quests_test` | Initial pool coverage; add seeded transition fixtures |
| Mod reload and Hellfire activation | None | Missing characterization fixture |
| Canonical state hashing | `replay_test`, `SnapshotStateHasherTests` | Shared protocol projection, item dimensions, belt, and resource coverage |
