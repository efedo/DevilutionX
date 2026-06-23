# Source Directory Layout

All source lives under `Source/`, organized by architectural domain:

## Domains

| Directory   | Purpose |
|-------------|---------|
| `application/` | Entry point, main loop, initialization, fatal errors, game mode flags, debug commands |
| `engine/` | Low-level subsystems: rendering, lighting, vision, sound, input, palette, random, pathfinding |
| `game/` | Game logic, subdivided by feature domain |
| `game/portals/` | Town portal management |
| `game/spells/` | Spell data and effects |
| `game/missiles/` | Projectile and missile logic |
| `game/objects/` | Dungeon objects (shrines, traps, etc.) |
| `game/items/` | Item data, attributes, generation |
| `game/monsters/` | Monster data, AI, corpses |
| `game/players/` | Player state, inventory, leveling |
| `game/levels/` | Level generation, dungeon tiles, triggers |
| `game/quests/` | Quest logic and dialog |
| `game/stores/` | Vendor shops |
| `game/towners/` | Town NPC behavior |
| `network/` | Multiplayer protocol, sync, chat |
| `persistence/` | Save/load, pack/unpack, options, player files |
| `support/` | General-purpose utilities: SHA1, encoding, compression |
| `ui/` | In-game UI overlay: automap, help, menus, messages, movies |

## Cross-cutting directories (stable)

| Directory   | Purpose |
|-------------|---------|
| `control/` | In-game control panel and HUD |
| `controls/` | Input handling (keyboard, mouse, gamepad, touch) |
| `data/` | TXT data file parsing |
| `DiabloUI/` | Menu and dialog screens |
| `discord/` | Discord Rich Presence integration |
| `dvlnet/` | Low-level network transport (TCP, ZeroTier) |
| `lua/` | Lua scripting support |
| `mpq/` | MPQ archive reading and writing |
| `panels/` | In-game UI panels (character, spell book, party) |
| `platform/` | Platform-specific code (Android, iOS, Vita) |
| `qol/` | Quality-of-life features (autopickup, stash, etc.) |
| `storm/` | Storm library compatibility layer |
| `tables/` | Static game data tables |
| `utils/` | Shared utility code |

## Conventions

- Files in `game/` use `.hpp` header extension; all other domains use `.h`.
- No files at `Source/` root level — every source file belongs to a subdirectory.
- Includes use the full relative path from `Source/`, e.g. `#include "game/items/items.hpp"`.
