# Phase 2 Migration Guide

## What Changed

Phase 2 adds the Tile array infrastructure to the codebase while maintaining full backward compatibility with existing code.

### Changes Made

1. **`Source/levels/level.hpp`**
   - ✅ Added `#include "levels/tile.hpp"`
   - ✅ Added `Tile tiles_[MAXDUNX][MAXDUNY]` member
   - ✅ Added `tileAt(x, y)` and `tileAt(Point)` accessor methods
   - ✅ Added `tiles()` method for bulk access
   - ✅ Kept all legacy arrays (`dPiece_`, `dLight_`, etc.) with TODO comments

2. **`Source/levels/gendung.h`**
   - ✅ Added `tiles` macro → `currentLevel().tiles_`
   - ✅ Added `tileAt` macro → `currentLevel().tileAt`
   - ✅ Kept all legacy macros (`dPiece`, `dLight`, etc.) unchanged

3. **`Source/levels/tile_sync.hpp`** (NEW)
   - ✅ Sync utilities to keep Tile and legacy arrays in sync during migration
   - ✅ `SyncTilesToLegacy()` - copy Tile data to old arrays
   - ✅ `SyncLegacyToTiles()` - copy old arrays to Tile data
   - ✅ `SyncSingleTile()` - sync one coordinate

## Current State

### ✅ What Works Now

**All existing code continues to work unchanged:**
```cpp
// Old code still works exactly as before
dPiece[x][y] = 42;
dPlayer[x][y] = 1;
if (dMonster[x][y] != 0) { ... }
```

**New Tile-based code can be written:**
```cpp
// New code using Tile API
Tile& tile = tiles[x][y];
tile.setPiece(42);
tile.setPlayer(1);
if (tile.hasMonster()) { ... }

// Or using accessor
tileAt(x, y).setPiece(42);
tileAt(position).setPlayer(1);
```

**Both can coexist:**
```cpp
// Mix old and new (though not recommended long-term)
dPiece[x][y] = 42;           // Old way
tiles[x][y].setPlayer(1);    // New way
```

### ⚠️ Important Considerations

**Data is NOT automatically synchronized** between the two representations:
- Writing to `tiles[x][y]` does NOT update `dPiece[x][y]`, etc.
- Writing to `dPiece[x][y]` does NOT update `tiles[x][y].piece()`
- They are separate storage (temporarily, during migration)

**This means:**
- New code using Tile API should be in isolated modules
- Or use sync utilities when crossing boundaries
- Or fully migrate a module at once

## Migration Strategies

### Strategy 1: Module-by-Module (Recommended)

Migrate entire modules at once to avoid mixing:

1. Pick a module (e.g., `monster.cpp`, `objects.cpp`)
2. Convert all tile access in that module to use Tile API
3. Ensure module doesn't interact with non-migrated modules' tile data
4. Test thoroughly
5. Repeat for next module

**Example:**
```cpp
// Before (in monster.cpp)
void PlaceMonster(int x, int y, int monsterId) {
    dMonster[x][y] = monsterId;
    dFlags[x][y] |= DungeonFlag::Populated;
}

// After (in monster.cpp)
void PlaceMonster(int x, int y, int monsterId) {
    Tile& tile = tiles[x][y];
    tile.setMonster(monsterId);
    tile.addFlags(DungeonFlag::Populated);
}
```

### Strategy 2: With Sync (For Shared Modules)

Use sync utilities when modules must interact:

```cpp
// Module A (migrated) writes to Tile
void MigratedFunction() {
    tiles[10][20].setPlayer(1);
    tiles[10][20].addFlags(DungeonFlag::Visible);

    // Sync to legacy before calling non-migrated code
    SyncTilesToLegacy(currentLevel().tiles_, 
                      currentLevel().dPiece_,
                      currentLevel().dTransVal_,
                      // ... all other arrays
                      currentLevel().dItem_);
}

// Module B (not migrated) reads from legacy arrays
void LegacyFunction() {
    if (dPlayer[10][20] != 0) {  // Will see the sync'd value
        // ...
    }
}
```

### Strategy 3: Gradual Function-by-Function

For large files, migrate incrementally:

```cpp
// Function 1: Fully migrated
void NewFunction() {
    Tile& tile = tiles[x][y];
    tile.setMonster(id);
}

// Function 2: Still using legacy
void OldFunction() {
    dMonster[x][y] = id;
}

// At module boundaries, sync as needed
void BoundaryFunction() {
    NewFunction();
    SyncTilesToLegacy(...);  // Make changes visible to old code
    OldFunction();
}
```

## Migration Priorities

### High-Impact Hot Paths (Migrate First)

1. **Rendering Loop** (`scrollrt.cpp`, `render/`)
   - Accesses multiple tile properties per pixel
   - Maximum cache benefit from Tile consolidation
   - Expected: 2-4× cache miss reduction

2. **Pathfinding** (`path.cpp`, AI functions)
   - Heavy tile property queries
   - Benefits from `isPassable()` and similar methods
   - Cleaner logic with semantic methods

3. **Lighting** (`lighting.cpp`)
   - Accesses `dLight`, `dPreLight`, `dFlags` together
   - Perfect use case for Tile consolidation

### Medium-Impact (Migrate Second)

4. **Entity Placement** (monster/object/item placement functions)
   - Benefits from `isOccupied()`, `hasEntity()` methods
   - Clearer validation logic

5. **Level Generation** (`gendung.cpp`, `dun/`)
   - Bulk tile operations
   - Benefits from `clear()` method

### Low-Impact (Migrate Last)

6. **Serialization** (`loadsave.cpp`)
   - Mostly bulk reads/writes
   - Less cache-critical
   - Migrate after other code is stable

7. **Debug/UI** (`debug.cpp`, `diabloui/`)
   - Low frequency calls
   - Migrate for consistency

## Example Migrations

### Example 1: Simple Access Pattern

**Before:**
```cpp
void SetTileData(int x, int y, uint16_t piece, uint8_t light) {
    dPiece[x][y] = piece;
    dLight[x][y] = light;
    dFlags[x][y] |= DungeonFlag::Visible;
}
```

**After:**
```cpp
void SetTileData(int x, int y, uint16_t piece, uint8_t light) {
    Tile& tile = tiles[x][y];
    tile.setPiece(piece);
    tile.setLight(light);
    tile.addFlags(DungeonFlag::Visible);
}
```

### Example 2: Multi-Property Check

**Before:**
```cpp
bool CanPlaceMonster(int x, int y) {
    if (dPlayer[x][y] != 0) return false;
    if (dMonster[x][y] != 0) return false;
    if (dObject[x][y] > 0) return false;
    if (HasAnyOf(dFlags[x][y], DungeonFlag::Populated)) return false;
    return true;
}
```

**After:**
```cpp
bool CanPlaceMonster(int x, int y) {
    const Tile& tile = tiles[x][y];
    return tile.isPassable() && !tile.isPopulated();
}
```

### Example 3: Iteration with Point

**Before:**
```cpp
for (int y = startY; y < endY; y++) {
    for (int x = startX; x < endX; x++) {
        if (dMonster[x][y] != 0) {
            ProcessMonster(dMonster[x][y]);
        }
    }
}
```

**After:**
```cpp
for (int y = startY; y < endY; y++) {
    for (int x = startX; x < endX; x++) {
        const Tile& tile = tiles[x][y];
        if (tile.hasMonster()) {
            ProcessMonster(tile.monster());
        }
    }
}
```

**Even Better with Point:**
```cpp
for (Point pos : IterateArea({startX, startY}, {endX, endY})) {
    const Tile& tile = tileAt(pos);
    if (tile.hasMonster()) {
        ProcessMonster(tile.monster());
    }
}
```

### Example 4: Clear Region

**Before:**
```cpp
void ClearRegion(int startX, int startY, int width, int height) {
    for (int y = startY; y < startY + height; y++) {
        for (int x = startX; x < startX + width; x++) {
            dPiece[x][y] = 0;
            dPlayer[x][y] = 0;
            dMonster[x][y] = 0;
            dCorpse[x][y] = 0;
            dObject[x][y] = 0;
            dItem[x][y] = 0;
            dSpecial[x][y] = 0;
            dFlags[x][y] = DungeonFlag::None;
            dTransVal[x][y] = 0;
            dLight[x][y] = 0;
            dPreLight[x][y] = 0;
        }
    }
}
```

**After:**
```cpp
void ClearRegion(int startX, int startY, int width, int height) {
    for (int y = startY; y < startY + height; y++) {
        for (int x = startX; x < startX + width; x++) {
            tiles[x][y].clear();
        }
    }
}
```

## Testing Strategy

### Before Migrating a Module

1. Identify all tile array accesses in the module
2. Create tests covering those access patterns
3. Run tests with old code (baseline)

### After Migrating a Module

1. Verify tests still pass
2. Add new tests using Tile API
3. Performance benchmark (optional but recommended for hot paths)

### Integration Testing

For modules that interact:
```cpp
TEST(TileMigration, LegacyAndNewCodeInteract) {
    // Setup
    tiles[5][5].setMonster(42);
    SyncTilesToLegacy(...);  // Sync to legacy

    // Test legacy code sees the value
    EXPECT_EQ(dMonster[5][5], 42);

    // Modify via legacy
    dMonster[5][5] = 99;
    SyncLegacyToTiles(...);  // Sync to Tile

    // Test Tile sees the value
    EXPECT_EQ(tiles[5][5].monster(), 99);
}
```

## Common Pitfalls

### ❌ Pitfall 1: Forgetting to Sync

```cpp
// WRONG: Mixing without sync
tiles[x][y].setPlayer(1);
if (dPlayer[x][y] != 0) {  // ❌ Will NOT see the change!
    // This won't execute
}
```

**Fix:** Either fully migrate the module, or sync:
```cpp
// RIGHT: Sync before accessing legacy
tiles[x][y].setPlayer(1);
SyncTilesToLegacy(...);
if (dPlayer[x][y] != 0) {  // ✅ Will see the change
    // This will execute
}
```

### ❌ Pitfall 2: Partial Migration of Tight Loops

```cpp
// WRONG: Mixing in hot path
for (int i = 0; i < 10000; i++) {
    tiles[x][i].setLight(brightness);  // New API
    dFlags[x][i] |= DungeonFlag::Lit;  // Old API
}
```

**Fix:** Migrate the entire loop:
```cpp
// RIGHT: Consistent API
for (int i = 0; i < 10000; i++) {
    Tile& tile = tiles[x][i];
    tile.setLight(brightness);
    tile.addFlags(DungeonFlag::Lit);
}
```

### ❌ Pitfall 3: Assuming Automatic Sync

```cpp
// WRONG: Assuming macro magic
dPiece[x][y] = 42;
// ❌ tiles[x][y].piece() will NOT be 42 automatically
```

**Reality:** The arrays are separate. Sync is manual until Phase 4 when legacy arrays are removed.

## Performance Notes

### Cache Benefits Require Full Module Migration

The cache benefits of Tile consolidation only materialize when a module consistently uses the Tile API:

```cpp
// ❌ No cache benefit (still accessing 3 separate arrays)
uint16_t p = dPiece[x][y];    // Load from dPiece_ array
uint8_t l = dLight[x][y];     // Load from dLight_ array
int8_t m = dMonster[x][y];    // Load from dMonster_ array

// ✅ Cache benefit (single load gets all data)
const Tile& tile = tiles[x][y];
uint16_t p = tile.piece();    // All in same cache line
uint8_t l = tile.light();     // Already loaded
int8_t m = tile.monster();    // Already loaded
```

### Sync Operations Are Expensive

`SyncTilesToLegacy()` and `SyncLegacyToTiles()` copy **all** tile data (12,544 tiles × 13 bytes = ~163 KB):

- **Cost:** ~100,000 CPU cycles (rough estimate)
- **When:** Only use at module boundaries, not in loops
- **Goal:** Minimize sync calls; eventually eliminate by full migration

## Rollout Plan

### Week 1-2: Hot Path Migration
- Migrate rendering loop
- Migrate pathfinding
- Migrate lighting
- Run performance benchmarks
- Verify cache improvements

### Week 3-4: Entity Management
- Migrate monster placement
- Migrate object placement
- Migrate item placement
- Run game tests

### Week 5-6: Level Generation
- Migrate dungeon generation
- Migrate set piece placement
- Test level generation integrity

### Week 7-8: Remaining Code
- Migrate serialization
- Migrate debug/UI
- Remove sync utilities
- Begin Phase 4 prep

## Phase 3 Readiness Checklist

Before starting Phase 3 (removing legacy arrays), verify:

- [ ] All modules migrated to Tile API
- [ ] No remaining uses of `dPiece`, `dLight`, etc. (except in macros)
- [ ] All tests passing
- [ ] Performance benchmarks show expected improvements
- [ ] Save/load tested and working
- [ ] No uses of sync utilities (except tests)

## Questions?

See also:
- `docs/Tile_Class_Design.md` - Original design document
- `docs/Tile_Quick_Reference.md` - API quick reference
- `docs/Tile_Memory_Layout.md` - Cache analysis
- `Source/levels/tile_usage_examples.cpp` - Code examples
