# Tile Grid Position Range Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add mutable and const coordinate-yielding tile-grid ranges in both supported traversal orders, then identify existing loops that can use them.

**Architecture:** Extend `TileGrid` with an index-based position iterator that shares the existing Y-major and X-major coordinate formulas. Return a small aggregate containing a `Point` and a tile reference so structured bindings preserve mutability and constness without storing coordinates in each tile.

**Tech Stack:** C++20, GTest, CMake, MSVC/Ninja

---

### Task 1: Specify Position-Aware Iteration

**Files:**
- Modify: `test/tile_migration_sync_test.cpp`

- [ ] **Step 1: Add failing traversal tests**

Add tests that iterate mutable `grid.withPositions()` and const
`grid.withPositionsColumnMajor()` ranges with structured bindings. Verify each
reported `Point`, traversal order, complete coverage, mutation through the tile
reference, and const tile access.

- [ ] **Step 2: Build and confirm the expected failure**

Run:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target tile_migration_sync_test'
```

Expected: compilation fails because `TileGrid` has no `withPositions()` or
`withPositionsColumnMajor()` member.

### Task 2: Implement Position-Aware Iteration

**Files:**
- Modify: `Source/levels/tile.hpp`
- Test: `test/tile_migration_sync_test.cpp`

- [ ] **Step 1: Add the position entry and iterator**

Add a nested aggregate that contains `Point position` and a conditional tile
reference. Add an index-based iterator whose dereference computes:

```cpp
const size_t x = ColumnMajor ? index_ / MAXDUNY : index_ % MAXDUNX;
const size_t y = ColumnMajor ? index_ % MAXDUNY : index_ / MAXDUNX;
return { { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) }, (*grid_)[x][y] };
```

Provide forward-iterator increment, equality, and mutable-to-const conversion
matching the existing tile iterator.

- [ ] **Step 2: Add the range methods**

Expose:

```cpp
[[nodiscard]] auto withPositions();
[[nodiscard]] auto withPositions() const;
[[nodiscard]] auto withPositionsColumnMajor();
[[nodiscard]] auto withPositionsColumnMajor() const;
```

Each range spans exactly `MAXDUNX * MAXDUNY` entries.

- [ ] **Step 3: Build and run the focused test**

Run:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target tile_migration_sync_test && build\x64-Debug\tile_migration_sync_test.exe'
```

Expected: build succeeds and every `TileMigrationSyncTest` passes.

### Task 3: Document the API

**Files:**
- Modify: `docs/devilution/CHANGELOG.md`

- [ ] **Step 1: Add a succinct changelog entry**

Document that tile-grid iteration can now yield coordinates together with tile
references in both traversal orders.

### Task 4: Audit Additional Conversion Candidates

**Files:**
- No source changes

- [ ] **Step 1: Locate indexed full-grid traversals**

Search `Source` and `test` for nested loops bounded by `MAXDUNX` and
`MAXDUNY`, including loops using `WorldTileCoord`.

- [ ] **Step 2: Classify each traversal**

Mark loops as:

- `withPositions()` candidates when Y is outer and X is inner.
- `withPositionsColumnMajor()` candidates when X is outer and Y is inner.
- Non-candidates when they use partial bounds, non-unit strides, mutate loop
  indices, or traverse another array whose ordering cannot be represented by
  the tile range.

- [ ] **Step 3: Report file, line, order, and constraints**

Produce a concise candidate list and a separate list of loops that remain
indexed.

### Task 5: Verify

**Files:**
- Verify all modified files

- [ ] **Step 1: Build the game and focused test**

Run:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target tile_migration_sync_test devilutionx'
```

- [ ] **Step 2: Run the focused test**

Run:

```bat
build\x64-Debug\tile_migration_sync_test.exe
```

- [ ] **Step 3: Check formatting and line endings**

Run the staged or unstaged diff whitespace check with
`core.whitespace=cr-at-eol` and verify every modified file uses CRLF exclusively.
