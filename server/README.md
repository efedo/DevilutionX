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
wallet/inventory state. The executor projects baseline player resources,
primary attributes, equipment slots, inventory layout, wallet, active-store,
and purchased-item state into snapshots sent after handshake and command
batches. External TSV store definitions feed module-owned purchase, sale,
repair, recharge, identification, and inventory-movement rules. Purchased
items now retain core identity, classification, combat, value, durability,
affix, and Hellfire effect fields. The server also has a strict C# replay-fixture
loader/executor, an injectable authoritative clock, explicit empty-batch
rejection, and command-delivery vector coverage.
Snapshots now carry a deterministic hash of those authoritative fields;
complete legacy-state parity and pricing parity remain future slices.

Run the standalone server with the repository's first external store-content
pack:

```powershell
dotnet run --project server/src/Devilution.Server/Devilution.Server.csproj
```

The default listener is `127.0.0.1:6113`. Override it with `--bind`, `--port`,
or `--content-root`; the server prints the active content and ruleset hashes
that clients must use during the handshake.

Run the server unit tests with xUnit's native runner:

```powershell
dotnet run --project server/tests/Devilution.Server.Tests/Devilution.Server.Tests.csproj
```
