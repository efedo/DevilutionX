# Unified Dungeon Megatile Storage Design

## Goal

Replace the parallel `Level::dungeon_` and `Level::pdungeon_` arrays with one
grid whose cells explicitly store the current and replacement megatile IDs.
Migrate callers from legacy macros and direct array access to named APIs without
changing dungeon generation, automap, quest, object, save, or multiplayer
behavior.

## Current Model

`dungeon_` and `pdungeon_` are both `DMAXX` by `DMAXY` grids of megatile IDs.
They represent related states of the same coarse map cell:

- `dungeon_` is the current layout. Generation constructs it, the automap reads
  it, and runtime map changes update it.
- `pdungeon_` begins as a snapshot of the generated layout. Quest generation
  may modify it with the layout that should be revealed later.
- `ObjChangeMap` and `ObjChangeMapResync` copy selected cells from `pdungeon_`
  into `dungeon_`, rebuild the corresponding world tiles, and resynchronize
  objects and doors.

The parallel arrays obscure this relationship and permit callers to manipulate
the states without expressing their intent.

## Data Model

Introduce one cell type:

```cpp
class DungeonMegaTile {
public:
	[[nodiscard]] uint8_t current() const;
	void setCurrent(uint8_t value);

	[[nodiscard]] uint8_t replacement() const;
	void setReplacement(uint8_t value);

	void snapshotReplacement();
	void applyReplacement();

private:
	uint8_t current_ = 0;
	uint8_t replacement_ = 0;
};
```

`Level` owns:

```cpp
DungeonMegaTile megaTiles_[DMAXX][DMAXY] = {};
```

The two bytes retain the existing memory footprint while making the states part
of one coherent model.

## Access API

Provide coordinate-based access through `megaTileAt` overloads:

```cpp
DungeonMegaTile &megaTileAt(int x, int y);
const DungeonMegaTile &megaTileAt(int x, int y);
DungeonMegaTile &megaTileAt(Point position);
const DungeonMegaTile &megaTileAt(Point position);
```

Callers use `current()`, `setCurrent()`, `replacement()`, and
`setReplacement()` rather than legacy macros. Bulk operations use named
functions:

```cpp
void FillCurrentMegaTiles(uint8_t value);
void SnapshotReplacementMegaTiles();
```

Runtime map application calls `applyReplacement()` on each affected cell before
rebuilding its expanded world tiles.

The `dungeon` and `pdungeon` macros are removed. Debug display names change to
clear current terminology; no compatibility aliases are retained.

## Lifecycle

1. Dungeon generation initializes and modifies each cell's current value.
2. At the existing snapshot points, `SnapshotReplacementMegaTiles()` copies
   every current value into its paired replacement value.
3. Quest setup may modify selected replacement values.
4. Pass 3 expands current megatiles into the world-tile grid.
5. Automap, debugging, export, and fixture validation read current values.
6. Runtime quest and object events apply selected replacement values to current
   values and rebuild the affected world-tile region.

Snapshot and application timing must remain identical to the existing
`memcpy(pdungeon, dungeon, ...)` and assignment behavior.

## Migration Scope

The migration includes:

- Dungeon generators and shared generation helpers.
- Town and predefined dungeon loaders.
- Quest generation and runtime quest resynchronization.
- Object and monster map-change paths.
- Automap reads.
- Debug grid display and Lua dungeon export.
- Dungeon-generation tests and direct test setup.
- Comments, examples, and the changelog.

The migration does not alter:

- Megatile IDs or coordinate systems.
- World-tile expansion behavior.
- Dungeon generation random-number consumption.
- Quest progression or map-change regions.
- Save-file or network formats.
- The fine-grained `Tile` grid.

## Backward Compatibility

Gameplay and generated maps remain byte-for-byte compatible. Existing fixture
tests continue comparing generated current megatile IDs with their expected
data. Replacement values are internal runtime state and are not serialized
through the removed array names.

Source compatibility for direct `dungeon` and `pdungeon` access is intentionally
not retained because the purpose is a broader API migration. User-visible Lua
debug names may change with no aliases.

## Testing

Add focused GTest coverage for:

- Default initialization of both states.
- Reading and writing current and replacement values independently.
- Full-grid snapshot behavior.
- Applying a replacement to the current value without changing the replacement.
- Runtime region application through the existing map-change path.
- Compile-time removal of `dungeon_` and `pdungeon_`.

Retain and run existing dungeon fixture, automap, object, quest, rendering,
lighting, player, and tile migration tests. Build the game target to cover debug
display and Lua export integration.

## Documentation

Document the distinction between coarse `DungeonMegaTile` cells and fine
world-space `Tile` cells. Add a concise changelog entry describing unified
megatile state storage. Remove stale documentation and debug labels referring
to `dungeon` and `pdungeon`.
