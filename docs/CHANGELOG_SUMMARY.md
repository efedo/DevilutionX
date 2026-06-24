# Changelog Summary

All notable changes to this fork from upstream are summarized below.

## 2026-06-24

### Source Reorganization (Phase 3)
- Discord integration removed entirely (`Source/discord/`, cmake option, all calls)
- `menus/` → `ui/menu/`, `panel/` → `ui/panel/`, `debug_overlay/` → `ui/debug_overlay/`
- `network/*` → `network/protocol/`, `net_transport/` → `network/transport/`
- `mpq/` → `data/mpq/`, `storm/` → `network/storm/`
- Validation files restored from centralized `validation/` back to subject folders
- All includes updated to canonical paths; no forwarders remain

### Housekeeping
- `Testing/` removed from git tracking, added to `.gitignore`
- 291 source files received `@file` doxygen summaries
- `docs/CHANGELOG.md` and `docs/CHANGELOG_SUMMARY.md` created

## 2026-06-23

### Source Reorganization (Phase 2)
- `control/` and `panels/` merged into `panel/`
- `utils/` subdivided: `string/`, `sdl/`, `endian/`, `image/`, `container/`, `file/`
- `engine/` subdivided: `audio/`, `gfx/`, `load/`, `math/`
- `DiabloUI/` renamed to `menus/`
- `dvlnet/` renamed to `net_transport/`
- Remaining Source/ files migrated to domain subdirectories
- Game domains moved under `Source/game/`
- Portals moved into game domain with canonical interfaces
- All compatibility forwarders removed

## 2026-06-22

### Macro Modernization
- `gendung` macro shims replaced with typed accessors

## 2026-06-21

### Tile System (Phase 3)
- Positioned tile grid iteration added
- Dungeon megatile storage unified
- Legacy `dTransVal` removal documented

## 2026-06-20

### Tile System (Phase 3)
- Legacy `dPiece` storage removed
- Debug overlay: Inspector panel, toolbar, piece selector, Lua help system

## 2026-06-19

### Tile System (Phase 3)
- Tile API completed: flags, special data, fallback, `tileAt(Point)` access
- Legacy dungeon entity arrays removed
- Object subsystem unit tests added
- ImGui debug overlay console added

## 2026-06-18

### Tile System (Phase 4B)
- Entity accessor migration (cursor, missiles, items, objects, dead, players, monsters, debug, towners, sync, inv)
- Final macro migrations complete
- Documentation updated

## 2026-06-17

### Tile System (Phase 4A)
- Lighting migration to Tile API
- Rendering hot-paths converted
- Tests and benchmarks added

## 2026-06-16

### Tile System (Phase 2)
- `Tile` class and `Level` integration
- `dItem` refactored to be level-specific
- Monster and item pool adapters introduced
- `MonsterPoolAdapter`, `ItemPoolAdapter`

## 2026-06-15

### Player & Object Modernization
- Player attributes: damage, life/mana, validation grouped
- Object storage modernized with `DenseEntityPool` and `ObjectPoolAdapter`

## 2026-06-14

### Object & Player Architecture
- Type-safe object pool, `ObjectManager` facade
- Shared attribute value types
- Player attribute model design planned

## 2026-06-13

### Entity Architecture (Phase 1)
- `Actor` base struct introduced for `Player` and `Monster`
- `CombatActor` base type
- `Bestiary` class for monster behavior
- `World` and `Level` state containers
- Player logic refactored into `Player` class methods
