# C# authoritative server

The `Devilution.Server` project is the initial deterministic server domain for
the C# migration. It generates C# bindings from `protocol/devilution.proto` and
owns command admission and exactly-once execution at the server boundary.

`AuthoritativeCommandServer` currently provides:

- Globally monotonic server receipt sequences.
- Deduplication by `(session ID, client sequence)`.
- Replay-safe duplicate responses that retain the original receipt and tick.
- Strict handling for late gameplay-changing commands.
- A two-tick leniency window for late `OpenStoreRequested` commands.

The executor interface is intentionally separate from transport and gameplay
rules. `AuthoritativeTcpServer` now provides the TCP session boundary and
routes authenticated command batches into this domain. `Stores` now provides
the first deterministic store executor with shared stock and per-session
wallet/inventory state. The executor projects wallet, active-store, and
purchased-item state into snapshots sent after handshake and command batches.
State hashing and legacy pricing parity remain future slices.

Run the server unit tests with xUnit's native runner:

```powershell
dotnet run --project server/tests/Devilution.Server.Tests/Devilution.Server.Tests.csproj
```
