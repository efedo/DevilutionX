# Tile Grid Position Range Design

## Goal

Add coordinate-yielding tile-grid ranges so full-grid loops that need both a
tile and its position can use range-based `for` loops without changing
traversal order.

## API

`TileGrid::withPositions()` yields entries usable through structured bindings:

```cpp
for (auto [position, tile] : tiles.withPositions())
```

The default range traverses Y first, matching `TileGrid::begin()`.
`TileGrid::withPositionsColumnMajor()` traverses X first, matching
`TileGrid::columnMajor()`.

Both methods support mutable and const `TileGrid` instances. The yielded tile
is a reference, allowing mutation for mutable grids and read-only access for
const grids. The yielded position is a `Point`.

## Implementation

Extend the existing index-based `TileGrid` iterator machinery in
`Source/levels/tile.hpp`. The position-aware iterator derives X and Y from the
same linear index formulas already used by the tile-only iterator and returns
a lightweight tuple-like value containing the `Point` and tile reference.

No tile stores its own coordinates, and no additional per-tile memory is
introduced.

## Testing

Extend `test/tile_migration_sync_test.cpp` to verify:

- Default position traversal is Y-major.
- Column-major position traversal is X-major.
- Mutable ranges expose mutable tile references.
- Const ranges expose const tile references.
- Every grid position is visited exactly once.

Tests are added before implementation and must fail because the new methods do
not yet exist.

## Loop Audit

After implementing the API, audit all `MAXDUNX`/`MAXDUNY` nested loops that
access `tiles` or `tileAt`. Classify each loop as:

- Directly convertible using `withPositions()`.
- Directly convertible using `withPositionsColumnMajor()`.
- Not convertible because it uses partial bounds, strides, manual index
  mutation, or depends on a separate grid with incompatible storage order.

The audit report identifies candidate files and line numbers. Call-site
conversion is outside this task.

## Compatibility

The API is additive. Existing tile-only iteration, indexed access, memory
layout, traversal order, and save-file serialization remain unchanged.
