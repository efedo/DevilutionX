# Remove `gendung.h` Macro Shims Design

## Goal

Remove every macro shim from `Source/levels/gendung.h` and migrate all
production, test, and benchmark call sites to typed accessors.

## Scope

The following macros will be removed:

```text
DungeonMask
Protected
SetPieceRoom
SetPiece
pSpecialCels
pMegaTiles
pDungeonCels
SOLData
dminPosition
dmaxPosition
leveltype
currlevel
setlevel
setlvlnum
setlvltype
ViewPosition
MicroTileLen
TransVal
TransList
DPieceMicros
themeCount
themeLoc
tiles
tileAt
```

The user explicitly requested removal of this compatibility layer, so source
compatibility for these macro names is intentionally not retained. Runtime
behavior, data layout, save compatibility, and traversal order must remain
unchanged.

## Accessor API

Add named inline free accessors that forward to `currentLevel()` and preserve
the underlying reference type:

```cpp
decltype(auto) dungeonMask();                // Megatiles used by the generated map.
decltype(auto) protectedTiles();             // Megatiles protected from generator changes.
decltype(auto) setPieceRoom();               // Set-piece room membership map.
decltype(auto) setPiece();                   // Active quest set-piece area.
decltype(auto) specialCels();                // Special CEL sprite data.
decltype(auto) megaTiles();                   // Megatile definitions for the active dungeon.
decltype(auto) dungeonCels();                 // Dungeon CEL sprite data.
decltype(auto) tileProperties();              // Properties indexed by dungeon piece.
decltype(auto) minimumDungeonPosition();      // Minimum rendered dungeon position.
decltype(auto) maximumDungeonPosition();      // Maximum rendered dungeon position.
decltype(auto) levelType();                   // Active dungeon type.
decltype(auto) currentLevelNumber();          // Active dungeon level number.
decltype(auto) isSetLevel();                  // Whether the active level is a quest level.
decltype(auto) setLevelNumber();              // Active quest-level identifier.
decltype(auto) setLevelType();                // Dungeon type of the active quest level.
decltype(auto) viewPosition();                // Player viewpoint position.
decltype(auto) microTileLength();             // Microtiles used by each dungeon piece.
decltype(auto) nextTransparencyValue();       // Next transparency-region identifier.
decltype(auto) visibleTransparencyRegions();  // Transparency regions visible to the player.
decltype(auto) levelMicros();                 // Microtile lookup indexed by dungeon piece.
decltype(auto) themeCount();                  // Number of generated theme rooms.
decltype(auto) themeLocations();              // Generated theme-room areas.
decltype(auto) tiles();                       // Runtime state for each world tile.
```

Const overloads are unnecessary because these accessors target the mutable
process-wide current level. Existing functions that accept a `const Level`
continue to use that object directly.

`tileAt(Point)` and `tileAt(x, y)` become ordinary overloaded inline free
functions. Existing `megaTileAt` remains an ordinary free function.

Accessor names use lower camel case and avoid legacy names that could mask
missed migrations. `levelMicros()` retains its established name and span return
type. Every new accessor declaration has a brief trailing `//` comment
describing the referenced state. Comments remain succinct and wrap only when
necessary.

## Migration

Migrate all unqualified macro uses throughout `Source`, `test`, and benchmark
sources:

- Reads become accessor calls, such as `levelType()`.
- Assignments mutate through reference-returning calls, such as
  `currentLevelNumber() = level`.
- Indexing becomes `tileProperties()[piece]`, `visibleTransparencyRegions()[i]`,
  or `tiles()[x][y]`.
- Existing calls such as `tileAt(position)` continue to compile against the
  new free functions.
- Local declarations that conflict with accessor names are renamed only where
  required.

The migration will be split into logical batches so compiler errors can expose
missed or ambiguous uses without producing one unreviewable mechanical edit.

## Existing Working-Tree Changes

Preserve the current uncommitted comment cleanup in `gendung.h`, the comment
cleanup in `tile_properties.hpp`, and deletion of the two obsolete tile example
files. Do not revert or silently fold those unrelated changes into the macro
migration commit unless the user later requests committing all changes.

## Testing

Before implementation, add a compile-time test that includes `gendung.h` and
fails if any removed macro remains defined using `#ifdef` checks.

Verification includes:

- The macro-removal test.
- Existing level, dungeon-generation, tile, lighting, object, quest, load/save,
  and gameplay tests affected by compilation.
- A complete `devilutionx` build.
- A repository search proving none of the removed macro definitions remain.
- Whitespace checks and exclusive CRLF line endings for modified files.

## Documentation

Add a concise changelog entry noting replacement of transitional level-state
macros with typed accessors. No additional migration guide is needed because
all in-tree consumers are migrated in the same change.
