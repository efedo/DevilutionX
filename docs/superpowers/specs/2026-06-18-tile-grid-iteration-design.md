# Tile Grid Iteration Design

## Goal

Provide range-based iteration over the level tile grid while preserving existing
`tiles[x][y]` indexing and storage compatibility.

## Traversal Order

The codebase predominantly traverses full tile grids with Y in the outer loop
and X in the inner loop. The default range therefore visits:

```text
(0, 0), (1, 0), ... (MAXDUNX - 1, 0), (0, 1), ...
```

Some lighting, rendering, lookup, save restoration, and test code intentionally
uses X-outer/Y-inner traversal. The grid exposes this alternate order through
`columnMajor()`.

## API

Replace the raw `Tile[MAXDUNX][MAXDUNY]` member type with a `TileGrid` wrapper.
The wrapper retains two-dimensional indexing:

```cpp
tiles[x][y]
```

Default range iteration is row-major in world coordinates:

```cpp
for (Tile &tile : tiles)
```

Explicit column-major iteration is:

```cpp
for (Tile &tile : tiles.columnMajor())
```

Both forms support mutable and const grids. Iterators expose `Tile` references
and visit every tile exactly once.

## Compatibility

`TileGrid` owns the same contiguous `Tile[MAXDUNX][MAXDUNY]` storage. It must
have no additional per-instance state and must retain the raw grid's size.
Existing indexed access and `Level::tileAt()` behavior remain unchanged.

## Migration

Existing nested range loops that do not require coordinates will use the
flattened default range. Coordinate-dependent loops remain explicit. Existing
X-outer/Y-inner loops may use `columnMajor()` where flattening improves clarity.

## Testing

GTest coverage will verify:

- default Y-outer/X-inner ordering;
- explicit X-outer/Y-inner ordering;
- complete visitation without duplicates;
- mutable and const iteration;
- existing `tiles[x][y]` indexing;
- storage-size compatibility.

