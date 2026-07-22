# Protocol package

`devilution.proto` is the canonical, transport-independent Protobuf schema
for the C++ client, C# authoritative server, and future Godot client. The
initial wire transport is TCP with a length-delimited `Envelope` message.

The schema is not generated or linked into the C++ build yet. Keeping the
schema artifact independent at this stage lets the server project consume the
same contract without introducing a generator or runtime dependency into the
legacy client prematurely.

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

Code generation and descriptor compatibility checks will be added when the
separate C# server project is brought into the workspace.
