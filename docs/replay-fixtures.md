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
6. Monster and player damage with event ordering.
7. Quest selection and quest-pool state transitions.
8. Mod reload ordering and Hellfire activation.

The C++ replay primitives now provide canonical field encoding, SHA-256
digests, command ordering, and an initial player/store state projection. The
strict envelope parser and `stores/basic-buy` fixture exercise that projection
with an explicitly value-initialized C++ baseline, and the C# legacy projection
matches its initial checkpoint. Complete game-state serialization, command
execution, and transition checkpoints remain future work. The C# server uses
the same primitive encoding for its current protocol snapshot projection,
including baseline resources, primary attributes, equipment slots, inventory
layout, and core item state. Full legacy player/store transition parity will be
added as the domain model grows.

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
| Store inventory and pricing | `vendor_test`, `stores_test`, `store_transaction_test`, `replay_test` | Projection exists; add normalized state checkpoints |
| Purchases, sales, repairs, recharge, identification, and gold | `store_transaction_test` | Broad success/failure coverage; add command-level fixture inputs |
| Player stats/resources and experience | `player_test`, `replay_test` | Projection exists; add explicit experience/life/mana transition fixtures |
| Damage calculations and event order | `monster_test`, `player_test`, `game_event_bus_test` | Partial; add damage-state and ordering checkpoints |
| Quest selection and transitions | `quests_test` | Initial pool coverage; add seeded transition fixtures |
| Mod reload and Hellfire activation | None | Missing characterization fixture |
| Canonical state hashing | `replay_test` | Primitive encoding and SHA-256 tested; complete game-state projection remains |
