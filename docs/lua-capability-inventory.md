# Lua Capability Inventory

This is the Phase 0 inventory of the Lua surface currently exposed by the
engine. It is the compatibility boundary for the migration: existing APIs are
frozen, and new authoritative behavior must be added to the C++ domain or the
future server protocol rather than to Lua.

## Runtime and bootstrap

| Surface | Current responsibility | Migration destination | Status |
| --- | --- | --- | --- |
| `LuaInitialize` / `LuaShutdown` | Creates and destroys the embedded Lua state | Temporary compatibility host, then removed | Transitional |
| `LuaReloadActiveMods` | Loads mod scripts, invokes mod-change callbacks, reloads audio/data | `ModManager`, `GameDataManager`, content manifest, and typed events | Partially extracted |
| Sandbox and `require` | Loads `devilutionx.*` and `mods.*` scripts from assets | Removed from authoritative server; optional client compatibility only | Transitional |
| `devilutionx.events` | Registers Lua event callbacks | `GameEventBus` adapter, then protocol/server events | Partially extracted |
| `devilutionx.version` | Exposes the engine version | Protocol/server handshake metadata | Transitional |
| `devilutionx.message` | Sends a red player-facing message | Server result or client notification event | Transitional |

## Common Lua modules

| Module | Registered surface | Classification | Destination |
| --- | --- | --- | --- |
| `devilutionx.player` | `self`, `walk_to`; `Player` name/id/position, experience, level, inventory, life, and mana access | Authoritative gameplay | C# domain commands and snapshots |
| `devilutionx.items` | `Item` mutable fields and helpers; `addItemDataFromTsv`; `addUniqueItemDataFromTsv` | Authoritative gameplay and data loading | C# item model; manifest-driven data overlays |
| `devilutionx.monsters` | Monster position/id access; `addMonsterDataFromTsv`; `addUniqueMonsterDataFromTsv` | Authoritative gameplay and data loading | C# monster model; manifest-driven data overlays. The legacy Lua `Monster.id` property is pointer-derived and must not enter the protocol |
| `devilutionx.towners` | Towner positions and `addDialogOption` | Authoritative interaction plus client UI | Server dialog/store rules and client presentation |
| `devilutionx.hellfire` | `loadData`; `enable` | Content selection and game mode | Declarative content-pack manifest |
| `devilutionx.audio` | Sound-effect playback and music control | Client presentation | Godot audio services |
| `devilutionx.floatingnumbers` | Floating damage/experience text | Client presentation | Godot presentation events |
| `devilutionx.render` | Text rendering, screen dimensions | Client presentation | Godot UI/rendering |
| `devilutionx.i18n` | Language code, translation, pluralization, font metrics | Client presentation | Godot localization service |
| `devilutionx.system` | Millisecond clock access | Client diagnostics/presentation | Godot clock or protocol tick display |
| `devilutionx.log` | Lua-side logging at multiple severities | Diagnostics | Server/client logging adapters |

## Events

The current event names are defined in `assets/lua/devilutionx/events.lua`.
Gameplay and table code now publishes through `GameEventBus`; the Lua adapter
preserves these names during the transition.

| Lua event | Current payload | Target owner | Current migration state |
| --- | --- | --- | --- |
| `LoadModsComplete` | None | Content pipeline | Typed event exists; content manifest pending |
| `ItemDataLoaded` | None | Content pipeline | Typed event exists |
| `UniqueItemDataLoaded` | None | Content pipeline | Typed event exists |
| `MonsterDataLoaded` | None | Content pipeline | Typed event exists |
| `UniqueMonsterDataLoaded` | None | Content pipeline | Typed event exists |
| `GameStart` | None | Server lifecycle | Typed event exists |
| `GameDrawComplete` | None | Client presentation | Typed event exists as frame completion |
| `StoreOpened` | Towner short name | Server store event plus client UI | Typed event exists |
| `OnMonsterTakeDamage` | Monster userdata, damage, damage type | Server combat event | Stable monster ID crosses the bus; Lua adapter resolves legacy userdata |
| `OnPlayerTakeDamage` | Player userdata, damage, damage type | Server combat event | Stable player ID crosses the bus; Lua adapter resolves legacy userdata |
| `OnPlayerGainExperience` | Player userdata, experience | Server progression event | Stable player ID crosses the bus; Lua adapter resolves legacy userdata |
| `registerCustom` | Arbitrary Lua event name | Explicit server/client extension API | Lua-only and intentionally frozen |

## Developer tooling

The debug namespace is currently exposed only in debug builds through the Lua
REPL and autocomplete frontend.

| Namespace | Commands | Migration destination |
| --- | --- | --- |
| `dev.display` | FPS, fullbright, grid, path, scroll view, tile data, vision | C++/Godot debug registry |
| `dev.items` | Get selected item, inspect, spawn, spawn unique | Authenticated server admin commands plus client inspector |
| `dev.level` | Export/reset/seed and map/warp subcommands | Authenticated server admin commands |
| `dev.monsters` | Spawn and spawn unique | Authenticated server admin commands |
| `dev.player` | Arrow, god mode, info, invisibility, TRN, gold, spells, stats | Authenticated server admin commands |
| `dev.quests` | Activate, activate all, list, info | Authenticated server admin commands |
| `dev.search` | Clear, item, monster, object search | C++/Godot diagnostic tools |
| `dev.towners` | Talk to and visit a towner | Authenticated server admin commands |
| REPL/autocomplete | Lua code execution, completion, and documentation | Optional compatibility frontend; never the only debug path |

## Bundled scripts and mods

| Script | Current behavior | Destination |
| --- | --- | --- |
| `assets/lua/devilutionx/events.lua` | Lua event registry and callback lists | Temporary adapter; then protocol/event subscriptions |
| `assets/lua/inspect.lua` | Value inspection used by the REPL | Non-authoritative debug frontend or delete with Lua |
| `assets/lua/repl_prelude.lua` | Console helper functions | C++/Godot debug console |
| `assets/lua/mods/adria_refills_mana/init.lua` | Refills mana through a store-open event | Trusted C# server store rule |
| `assets/lua/mods/clock/init.lua` | Draws a real-time clock | Godot client presentation |
| `assets/lua/mods/Floating Numbers - Damage/init.lua` | Draws damage numbers from damage events | Godot client presentation |
| `assets/lua/mods/Floating Numbers - XP/init.lua` | Draws experience numbers from XP events | Godot client presentation |
| `mods/Hellfire/lua/mods/Hellfire/init.lua` | Calls `hellfire.loadData()` and `hellfire.enable()` | Declarative Hellfire content manifest |

## Freeze rules

1. Existing Lua names and callback payloads remain available for compatibility
   until their replacement has parity coverage.
2. No new authoritative gameplay behavior is added to Lua.
3. New data extensions must be represented as validated content-pack overlays,
   not calls to `add*DataFromTsv`.
4. New presentation behavior belongs in the future Godot client and may use
   typed server events during the transition.
5. Any compatibility change to this inventory requires an update to the
   migration plan and a characterization test or fixture.
