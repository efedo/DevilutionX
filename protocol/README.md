# Protocol package

`devilution.proto` is the canonical, transport-independent Protobuf schema
for the C++ client, C# authoritative server, and future Godot client. The
initial wire transport is TCP with a four-byte little-endian length-prefixed
`Envelope` message. The server rejects empty or oversized envelopes; the
current maximum payload is 1 MiB.

`server/src/Devilution.Server` generates C# bindings from this schema at build
time. The legacy C++ build does not generate or link Protobuf by default. An
opt-in configure with `-DDEVILUTIONX_ENABLE_AUTHORITATIVE_CLIENT=ON` generates
C++ bindings into the build tree and builds the standalone authoritative client
against the optional `authoritative-client` vcpkg feature; the legacy packet
transport remains the default.

## Compatibility rules

- `devilution.protocol.v1` is the initial package namespace. Breaking changes
  require a new package version and a new `protocol_schema_version` value in
  the handshake messages.
- Existing field numbers and meanings are immutable. Removed fields must be
  reserved rather than reused.
- New fields and new `oneof` alternatives must be safely ignorable by older
  peers. Required behavior is negotiated through the handshake, not inferred
  from a missing field.
- `client_sequence` is scoped to a session and is reused for retries. The
  server returns the original result for duplicate commands.
- Store transactions use explicit intent messages for opening, purchasing,
  selling, repairing, recharging, identifying, and moving inventory items;
  clients never send the resulting gold, durability, charges, or placement.
- The server assigns `server_receipt_sequence` and reports the authoritative
  `applied_tick`. Late-command acceptance, rejection, or rescheduling is
  explicit in `CommandResult.status`.
- Content manifest identity is part of the handshake. A mismatch is a
  protocol error, not a best-effort compatibility mode.
- A successful handshake returns a server-issued `session_token`. A reconnect
  may present that token to resume the command ledger and authoritative entity
  identity; an unknown token creates a new session.
- `Snapshot` carries the authoritative tick and per-player state. The initial
  store projection includes gold, experience, life, mana, primary attributes,
  active store, equipment slots, purchased item fields, and inventory-grid
  cells.
  `state_sha256` hashes those authoritative fields using fixed-width
  little-endian integers, length-prefixed UTF-8 strings, and SHA-256; future
  state fields must extend the canonical order explicitly.
- `ItemSnapshot.state` carries the current core item state used by the store
  vertical slice. Localized names and other presentation-only values are not
  part of the projection.

C++ descriptor compatibility checks will be added as the client surface grows.

`test-vectors/command-delivery-retry.json` is the first language-neutral
behavior vector. It covers a lost acknowledgement, a retry resolved as a
duplicate, a gameplay-critical late rejection, and a lenient reschedule. The
future generated-Protobuf client harness should consume this vector rather than
re-encoding those expectations independently; the C# server test suite now
loads and validates it directly.

## Current C++ client slice

`network/protocol/CommandDeliveryTracker` implements the client-side delivery
policy without depending on generated Protobuf types. It allocates
session-scoped sequence numbers, estimates RTT with a smoothed variance,
resubmits unacknowledged commands, and resolves accepted, rejected,
rescheduled, and duplicate outcomes. The opt-in
`network/authoritative/AuthoritativeClient` now proves the initial C++ wire
path: it performs the handshake, submits a command batch, receives an
acknowledgement, reads a snapshot, and wires the tracker into initial sends,
timeout-driven resubmissions, and acknowledgement resolution. The
authoritative client is not yet selected by the legacy gameplay network
provider. When the server is configured with a snapshot
provider, callers set `expectInitialSnapshot` and consume the initial snapshot
before submitting commands; this matches the current server session ordering.
