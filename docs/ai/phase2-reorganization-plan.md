# Phase 2 Source File Reorganization

## Completed

All top-level `Source/` files migrated to domain subdirectories:

| Domain | Directory | Files moved |
|--------|-----------|-------------|
| Game (portals) | `game/portals/` | `portal.cpp` + validation |
| Game (spells) | `game/spells/` | `spells.cpp` |
| Game (missiles) | `game/missiles/` | `missiles.cpp` |
| Game (objects) | `game/objects/` | `objects.cpp`, `object_pool.cpp`, `object_manager.cpp` |
| Game (items) | `game/items/` | `items.cpp`, `item_pool.cpp` |
| Game (monsters) | `game/monsters/` | `monsters.cpp`, `monster_pool.cpp`, `dead.cpp` |
| Game (players) | `game/players/` | `players.cpp`, `inv.cpp` + `inv_iterators.hpp` |
| Game (levels) | `game/levels/` | entire `levels/` directory |
| Game (quests) | `game/quests/` | `quests.cpp`, `validation.cpp`, `doom.cpp` |
| Game (stores) | `game/stores/` | `stores.cpp` |
| Game (towners) | `game/towners/` | `towners.cpp` |
| Engine | `engine/` | `lighting`, `vision`, `crawl`, `cursor`, `hwcursor`, `track`, `effects`, `effects_stubs` |
| Support | `support/` | `codec`, `sha`, `encrypt`, `translation_dummy` |
| Network | `network/` | `msg`, `multi`, `nthread`, `sync`, `tmsg` |
| Persistence | `persistence/` | `loadsave`, `pfile`, `pack`, `options` |
| Application | `application/` | `main`, `diablo`, `game_mode`, `headless_mode`, `init`, `appfat`, `capture`, `restrict`, `debug` |
| UI | `ui/` | `automap`, `diablo_msg`, `plrmsg`, `quick_messages`, `help`, `interfac`, `minitext`, `movie`, `gamemenu`, `gmenu`, `menu` |

## Migration pattern used

1. `git mv` `.cpp` files to preserve history
2. `git mv` `.h`/`.hpp` files to destination (except where game domain created new `.hpp`)
3. Create forwarder at old location: `#include "domain/file"`
4. Update `.cpp` self-include and `@file` doc line
5. Update `Source/CMakeLists.txt` and root `CMakeLists.txt` paths
6. Build `libdevilutionx` to verify

## Notable details

- All game domains use canonical `.hpp` headers at `game/<domain>/<domain>.hpp`
- Other domains moved `.h` files directly (engine, support, network, persistence, application, ui all use `.h`)
- Compatibility forwarders remain at old `Source/` locations for all moved headers
- `libdevilutionx_multiplayer` spans two domains: `network/multi.cpp` and `persistence/pack.cpp`
- `main.cpp` is referenced from root `CMakeLists.txt`, not `Source/CMakeLists.txt`
