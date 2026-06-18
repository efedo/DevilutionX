# Remove Legacy dTransVal Design

## Goal

Make `Tile::transVal()` the sole in-memory source of transparency-region IDs
and remove the legacy `Level::dTransVal_` array and `dTransVal` macro.

## Scope

- Replace production reads of `dTransVal[x][y]` with
  `tileAt(x, y).transVal()`.
- Replace production writes with `tileAt(x, y).setTransVal(value)`.
- Convert transparency flood filling, copying, initialization, and DUN loading
  to the Tile API.
- Remove the renderer fallback to legacy transparency storage.
- Remove legacy transparency synchronization and its testing hook.
- Remove `Level::dTransVal_` and the `dTransVal` macro.
- Update themes, tests, examples, and comments to use tile-owned transparency.
- Advertise `transVal` in the Lua debug command while accepting `dTransVal` as
  a backward-compatible alias.

## Compatibility

This migration changes only the in-memory representation. It must preserve
dungeon generation, theme placement, transparency rendering, player vision,
lighting, savegame behavior, network behavior, and DUN export behavior.

The `TransVal` region allocator and `TransList` activation table remain
unchanged. Existing serialized formats remain unchanged because transparency
values are already consumed through level-generation and tile APIs rather than
serialized as the removed array's native memory layout.

## Architecture

`Tile::transVal()` and `Tile::setTransVal()` become the only access path for
per-coordinate transparency-region IDs. Functions that copy transparency
values read the source tile once and write destination tiles through
`setTransVal()`. Bulk initialization iterates over `tiles` and clears only the
transparency field so unrelated tile state remains intact.

Generation and DUN loading write transparency IDs directly into tiles. Theme
logic and rendering read directly from tiles. Once all producers and consumers
use the Tile API, the compatibility synchronization pass, renderer fallback,
macro, and legacy storage are removed together.

## Testing

- Add a compile-time regression guard that `Level` has no `dTransVal_` member.
- Test transparency initialization, rectangular assignment, copying, and DUN
  layer loading through `Tile::transVal()`.
- Update dungeon fixture assertions to compare expected transparency-region IDs
  with tile values.
- Preserve Lua debug-command compatibility coverage if an existing suitable
  test seam is available; otherwise retain the alias in the command parser and
  verify it through compilation.
- Build and run focused tile, dungeon-generation, rendering, lighting, player,
  and theme-dependent tests.
- Build the main game target.
- Confirm no `dTransVal_`, `dTransVal` macro, or storage-style
  `dTransVal[...]` references remain in `Source` or `test`.
- Run the repository diff check with CRLF-aware whitespace handling.

## Documentation

Update the changelog with a concise note that transparency-region IDs now use
unified tile storage. Remove stale migration examples and comments without
creating additional completion-summary documents.
