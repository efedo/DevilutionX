# Authoritative Protocol Contract

This document freezes the server/client boundary used by the current native
client and the future Godot client. The canonical schema is
`protocol/devilution.proto`; C++ and C# bindings must be generated from that
file and must not introduce engine-specific types into the wire contract.

## Compatibility identity

Every connection begins with `ClientHello` and must receive `ServerHello`
before sending commands. A client must match:

- `protocol_schema_version` (`0.1.0` for the current contract);
- `content_manifest_hash`; and
- `ruleset_identity_hash` when supplied by the client.

The server returns `ProtocolError` and closes the connection on a required
identity mismatch. Schema evolution is additive: existing field numbers are
never reused, removed fields remain reserved by convention, and a client must
ignore unknown fields. A breaking change requires a new schema version.

## Transport

The initial transport is TCP. Each envelope is encoded as:

1. a four-byte unsigned little-endian payload length;
2. one serialized `Envelope` Protobuf payload.

Payloads must be non-empty and no larger than one MiB. The `Envelope` oneof
contains handshake messages, command batches, acknowledgements, snapshots,
event batches, snapshot requests, and protocol errors. Transport framing is
not part of the gameplay model and can be replaced behind the same envelope
contract later.

## Command delivery

Commands contain a client sequence and requested simulation tick. The server
assigns receipt ordering and deduplicates by session and client sequence.
Clients retain unresolved commands and resubmit them when the acknowledgement
deadline expires. The deadline is derived from measured client/server latency;
resubmission is safe because a duplicate produces `DUPLICATE` rather than a
second state mutation.

Gameplay-critical commands use the strict late-command tolerance. Non-critical
commands may be rescheduled to the current tick within their configured
tolerance. The acknowledgement is authoritative for accepted, rescheduled,
rejected, and duplicate outcomes.

## Authoritative state

`Snapshot` is the complete state projection for the receiving session. It may
contain:

- the player, resources, attributes, status effects, inventory, equipment, and
  belt;
- vendor stock;
- monsters and their combat state;
- world items and interactable objects;
- quest progress; and
- in-flight projectiles.

The server owns movement, collision/occupancy validation, combat, spell
resolution, projectile travel, object effects, quests, portals, inventory
placement, and persistence. The client may interpolate or predict presentation
only; it must reconcile to the next snapshot.

Projectile snapshots are transient authoritative entities. Their stable entity
ID, source, target, position, destination, damage, effect type, area radius,
and remaining lifetime are all server-owned. A projectile that reaches blocked
geometry or expires is removed without client-side resolution.

Object effect fields are declarative content values. Current effects are heal,
damage, and experience, with quest advancement remaining an explicit object
relationship. Unknown effect kinds are rejected by content validation.

## Events and hashes

`EventBatch` carries presentation-friendly authoritative deltas such as
damage, healing, and experience. Events are not a second source of truth;
clients always apply the following snapshot as the canonical result.

`Snapshot.state_sha256` is the canonical deterministic projection hash. Lists
are sorted by their stable identity before hashing. Legacy-empty optional
collections preserve the previous hash representation; non-empty projectiles
and non-default object effects participate in the hash. Any new authoritative
field must add a parity vector before it is used by a client.

## Godot entry criteria

The boundary is ready for a Godot client when the following are true:

- the Godot client can generate bindings from the shared `.proto` file;
- it implements the length-delimited TCP envelope and handshake identity
  checks;
- it retains and resubmits commands using client sequence numbers;
- it treats snapshots as complete authoritative state and events as transient
  presentation deltas; and
- it can display the current player/world projection without running gameplay
  simulation locally.

The next phase is therefore a Godot C# project and transport/model shell, not
another native gameplay adapter.
