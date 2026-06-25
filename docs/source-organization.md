# Source Directory Layout

All source lives under `Source/`, organized by architectural domain.
No `.cpp`, `.h`, or `.hpp` files exist at the `Source/` root — every source file belongs to a subdirectory.

## Core Domains

| Directory | Purpose |
|-----------|---------|
| `application/` | Entry point, main loop, initialization, fatal errors, game mode flags, debug commands |
| `engine/` | Low-level subsystems (see below) |
| `engine/audio/` | Sound effects, music, audio playback |
| `engine/gfx/` | Surface, palette, DirectX abstraction, CLX sprites |
| `engine/load/` | CEL, CL2, CLX, PCX asset loaders |
| `engine/math/` | Points, rectangles, directions, world tiles |
| `engine/render/` | Scroll, automap, CLX/dun/light/primitive/text rendering |
| `game/` | Game logic, subdivided by feature domain |
| `game/items/` | Item data, attributes, generation, pool |
| `game/levels/` | Level generation, dungeon tiles, triggers, quest levels |
| `game/missiles/` | Projectile and missile logic |
| `game/monsters/` | Monster data, AI, corpses, pool |
| `game/objects/` | Dungeon objects (shrines, traps, etc.), pool |
| `game/players/` | Player state, inventory, leveling |
| `game/portals/` | Town portal management |
| `game/quests/` | Quest logic and dialog |
| `game/spells/` | Spell data and effects |
| `game/stores/` | Vendor shops |
| `game/towners/` | Town NPC behavior |
| `network/` | Multiplayer protocol, sync, chat |
| `network/protocol/` | Message serialization, threading, sync |
| `network/storm/` | Storm library compatibility layer (net + video) |
| `network/transport/` | Low-level transport (TCP, ZeroTier, loopback) |
| `persistence/` | Save/load, pack/unpack, options, pfile |
| `support/` | General-purpose utilities: SHA1, encoding, compression |
| `ui/` | In-game UI overlay (see below) |
| `ui/debug_overlay/` | ImGui-based debug inspector overlay |
| `ui/menu/` | Menu and dialog screens (migrated from former `DiabloUI/`) |
| `ui/panel/` | In-game control panel, HUD, character/spell/party panels |

## Infrastructure & Cross-cutting

| Directory | Purpose |
|-----------|---------|
| `controls/` | Input handling (keyboard, mouse, gamepad, touch) |
| `controls/devices/` | Per-device abstractions (game controller, joystick, keyboard) |
| `controls/touch/` | Touch event handling and rendering |
| `data/` | TXT data file reading and record parsing |
| `data/mpq/` | MPQ archive reading and writing |
| `items/` | Item domain validation |
| `lua/` | Lua scripting support |
| `lua/modules/` | Lua API module bindings |
| `lua/modules/dev/` | Developer-oriented Lua bindings (level, player) |
| `monsters/` | Monster domain validation |
| `players/` | Player domain validation |
| `platform/` | Platform-specific code |
| `platform/android/` | Android integration |
| `platform/ctr/` | Nintendo 3DS integration |
| `platform/ios/` | iOS filesystem paths |
| `platform/macos_sdl1/` | macOS SDL1 filesystem |
| `platform/switch/` | Nintendo Switch integration |
| `platform/vita/` | PS Vita integration |
| `qol/` | Quality-of-life features (autopickup, stash, xp bar, etc.) |
| `tables/` | Static game data tables (items, monsters, spells, etc.) |
| `utils/` | Shared utility code (see below) |
| `utils/algorithm/` | Generic container helpers |
| `utils/container/` | Bitset2D, entity pool, static vector |
| `utils/endian/` | Endian-aware read/write/swap |
| `utils/file/` | File I/O utilities |
| `utils/image/` | Image format conversion |
| `utils/sdl/` | SDL compatibility and helpers |
| `utils/stdcompat/` | C++17/20 backports |
| `utils/string/` | String formatting, case, split, UTF-8 |

## Conventions

- Files in `game/` use `.hpp` header extension; all other domains use `.h`.
- Includes use the full relative path from `Source/`, e.g. `#include "game/items/items.hpp"`.
- Validation modules for game domains reside in top-level directories (`items/`, `monsters/`, `players/`) rather than in `game/`.
