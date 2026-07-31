# Godot C# Client

This directory contains the first Godot presentation client for the
authoritative C# server. The client does not simulate gameplay locally. It
submits commands, receives complete snapshots, applies transient event
batches, and renders the latest authoritative projection.

## Project layout

- `Devilution.Client.Protocol` contains generated Protobuf types, TCP framing,
  handshake, command retry, and the authoritative client model.
- `Devilution.Client` is the Godot 4 C# project and presentation shell.

The server defaults to `127.0.0.1:6113`. Set `DEVILUTION_CONTENT_HASH` to the
ruleset identity hash printed by the server before connecting. The current
frozen wire contract carries this combined identity in the
`content_manifest_hash` field for compatibility with the native client. An
optional `DEVILUTION_RULESET_HASH` can provide the same identity validation.

To run the first vertical slice:

1. Start the server with `dotnet run --project server/src/Devilution.Server/Devilution.Server.csproj -- --port 6113`.
2. Copy the server's content manifest hash into `DEVILUTION_CONTENT_HASH`.
3. Open `godot/Devilution.Client/project.godot` with Godot 4.7.x Mono and run
   the project.

The protocol model and transport can be verified without Godot with:

`dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj`

The complete local harness starts the server, reads its negotiated identity,
sets the client environment, and launches Godot:

`powershell -ExecutionPolicy Bypass -File godot/run-local-client.ps1`

The harness discovers versioned Godot Mono installations from `PATH`; pass
`-GodotExecutable` only when Godot is installed outside `PATH`. See
`docs/AI/godot-build.md` for the WinGet installation workaround.

Use `-Headless` for an automated launch. In the client, arrows/WASD move,
left-click casts spell 4, `O` opens Smith, `P` purchases the selected store
item, `M` opens the Adria service, and `R` requests a mana refill. The HUD
also exposes selectable store stock, a footprint-aware inventory grid,
authoritative buy/sell/repair/recharge/identify/move actions, object and quest
interaction buttons, command feedback, and event-log management. The world grid is loaded from
`assets/levels/level_layouts.json`; it is presentation data and must remain
consistent with the server's external level definitions.

When the Godot Mono editor is installed, open
`godot/Devilution.Client/project.godot` and run the project. The current
presentation uses procedural shapes intentionally; sprites, tilemaps, and UI
art can be added without changing the authoritative model or transport.
