# Remove Legacy dPiece Design

## Goal

Make `Tile::piece()` the sole source of truth for dungeon piece IDs and remove
the legacy `Level::dPiece_` array and `dPiece` macro.

The historical term "dPiece coordinates" remains valid where it describes the
world-tile coordinate scale rather than the removed storage array.

## Scope

- Replace production reads of `dPiece[x][y]` with `tileAt(x, y).piece()`.
- Replace production writes with `tileAt(x, y).setPiece(value)`.
- Remove piece fallback logic from rendering and tile property queries.
- Remove piece synchronization from `SyncTilesFromLegacyMaps`.
- Remove `Level::dPiece_` and the `dPiece` macro.
- Update debug tools and tests to use the tile API.
- Remove obsolete migration tests and replace them with tests that verify town
  generation and object micro-tile updates populate `Tile::piece()`.

## Compatibility

This migration changes only in-memory representation. It must preserve dungeon
generation, rendering, collision, trigger detection, object updates, savegame
behavior, and network behavior.

The legacy transparency array remains out of scope. Its synchronization and
fallback behavior will continue unchanged.

## Implementation Strategy

1. Add or adapt focused tests to express tile-only behavior.
2. Convert dungeon and town generation writes to `Tile::setPiece`.
3. Convert trigger, quest, object, debug, and level-specific reads.
4. Remove renderer and `TileHasAny` fallbacks.
5. Remove legacy piece synchronization, macro, and storage.
6. Confirm no production or test references to the legacy symbol remain.

## Verification

- Build and run the focused tile, generation, object, and rendering tests.
- Build the main game target to catch all source-level references.
- Run `git diff --check`.
- Search `Source` and `test` for `dPiece_` and storage-style `dPiece[...]`
  references.
