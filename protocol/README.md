# Protocol package

`devilution.proto` is the canonical, transport-independent Protobuf schema
for the C++ client, C# authoritative server, and future Godot client. The
initial wire transport is TCP with a four-byte little-endian length-prefixed
`Envelope` message. The server rejects empty or oversized envelopes; the
current maximum payload is 1 MiB.

`server/src/Devilution.Server` generates C# bindings from this schema at build
time. The legacy C++ build intentionally does not generate or link Protobuf
yet, so the server can evolve against the same contract without prematurely
adding a generator or runtime dependency to the legacy client.

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
- The server assigns `server_receipt_sequence` and reports the authoritative
  `applied_tick`. Late-command acceptance, rejection, or rescheduling is
  explicit in `CommandResult.status`.
- Content manifest identity is part of the handshake. A mismatch is a
  protocol error, not a best-effort compatibility mode.

C++ code generation and descriptor compatibility checks will be added once the
new client transport is introduced.

`test-vectors/command-delivery-retry.json` is the first language-neutral
behavior vector. It covers a lost acknowledgement, a retry resolved as a
duplicate, a gameplay-critical late rejection, and a lenient reschedule. The
future C# server tests and a generated-Protobuf client harness should consume
this vector rather than re-encoding those expectations independently.

## Current C++ client slice

`network/protocol/CommandDeliveryTracker` implements the client-side delivery
policy without depending on generated Protobuf types. It allocates
session-scoped sequence numbers, estimates RTT with a smoothed variance,
resubmits unacknowledged commands, and resolves accepted, rejected,
rescheduled, and duplicate outcomes. Transport integration and server-side
deduplication are supplied by the C# server; TCP session integration is the
next cross-project step.
