# Tile Class Design Document

## Overview

The `Tile` class consolidates all per-tile data that was previously scattered across multiple `[MAXDUNX][MAXDUNY]` arrays in the `Level` class. This provides better data locality, improved cache performance, type safety, and clearer code organization.

## Motivation

### Current State (Before Tile Class)

In `level.hpp`, each type of per-tile information is stored in a separate 2D array:

```cpp
uint16_t dPiece_[MAXDUNX][MAXDUNY] = {};       // Piece IDs
int8_t dTransVal_[MAXDUNX][MAXDUNY] = {};      // Transparency
uint8_t dLight_[MAXDUNX][MAXDUNY] = {};        // Current lighting
uint8_t dPreLight_[MAXDUNX][MAXDUNY] = {};     // Static lighting
DungeonFlag dFlags_[MAXDUNX][MAXDUNY] = {};    // Flags
int8_t dPlayer_[MAXDUNX][MAXDUNY] = {};        // Player indices
int16_t dMonster_[MAXDUNX][MAXDUNY] = {};      // Monster indices
int8_t dCorpse_[MAXDUNX][MAXDUNY] = {};        // Corpse data
int8_t dObject_[MAXDUNX][MAXDUNY] = {};        // Object indices
int8_t dSpecial_[MAXDUNX][MAXDUNY] = {};       // Special tiles
int8_t dItem_[MAXDUNX][MAXDUNY] = {};          // Item indices
```

### Problems with Current Approach

1. **Poor Cache Locality**: Accessing related tile data (e.g., checking if a tile has a monster AND is visible) requires loading data from multiple cache lines spread across memory.

2. **Scattered Logic**: Code that works with tiles must know about 11 different arrays and keep them in sync manually.

3. **No Type Safety**: Raw arrays provide no encapsulation or validation. It's easy to forget to update related fields.

4. **Debugging Difficulty**: Inspecting a tile's complete state requires viewing 11 separate array entries in the debugger.

5. **Memory Waste**: Arrays must always allocate space for all `MAXDUNX × MAXDUNY` tiles even if only a small region is used.

### Benefits of Tile Class

1. **Cache Efficiency**: Related tile data is stored contiguously. Accessing one tile's data loads all its properties into cache at once.

2. **Encapsulation**: Tile provides a clean interface with methods like `hasMonster()`, `isPassable()`, etc.

3. **Type Safety**: Methods can validate state and enforce invariants.

4. **Single Source of Truth**: All tile data in one place with a clear structure.

5. **Better Debugging**: Inspecting a `Tile` object shows all its state at once.

6. **Maintainability**: Adding new per-tile data only requires modifying the `Tile` class, not scattered code throughout the codebase.

## Design

### Memory Layout

The `Tile` class stores all per-tile data as private members:

```cpp
class Tile {
private:
    uint16_t piece_ = 0;           // 2 bytes - piece ID
    int8_t transVal_ = 0;          // 1 byte  - transparency
    uint8_t light_ = 0;            // 1 byte  - current light
    uint8_t preLight_ = 0;         // 1 byte  - static light
    DungeonFlag flags_ = {};       // 1 byte  - flags (enum)
    int8_t player_ = 0;            // 1 byte  - player index
    int16_t monster_ = 0;          // 2 bytes - monster index
    int8_t corpse_ = 0;            // 1 byte  - corpse data
    int8_t object_ = 0;            // 1 byte  - object index
    int8_t special_ = 0;           // 1 byte  - special tile
    int8_t item_ = 0;              // 1 byte  - item index
};
// Total: 13 bytes (likely padded to 14 or 16 by compiler)
```

**Size Target**: ≤ 16 bytes for optimal cache line utilization.

### Public Interface

#### Accessors (one per field)
```cpp
uint16_t piece() const;
void setPiece(uint16_t value);

int8_t transVal() const;
void setTransVal(int8_t value);
// ... etc for all fields
```

#### Flag Operations
```cpp
DungeonFlag flags() const;
void setFlags(DungeonFlag value);
bool hasAnyFlag(DungeonFlag mask) const;
bool hasAllFlags(DungeonFlag mask) const;
void addFlags(DungeonFlag mask);
void removeFlags(DungeonFlag mask);
```

#### Convenience Query Methods
```cpp
// Entity checks
bool hasPlayer() const;
bool hasMonster() const;
bool hasMovingMonster() const;
bool hasCorpse() const;
bool hasObject() const;
bool isObjectExtension() const;
bool hasItem() const;
bool hasSpecial() const;

// Corpse helpers (decode packed byte)
int8_t corpseIndex() const;      // corpse_ & 0x1F
int8_t corpseDirection() const;  // corpse_ >> 5

// Flag-based queries
bool hasMissile() const;         // DungeonFlag::Missile
bool isVisible() const;          // DungeonFlag::Visible
bool isExplored() const;         // DungeonFlag::Explored
bool isLit() const;              // DungeonFlag::Lit
bool hasDeadPlayer() const;      // DungeonFlag::DeadPlayer
bool isPopulated() const;        // DungeonFlag::Populated

// Composite state
bool isEmpty() const;            // no entities at all
bool isOccupied() const;         // has any entity
bool isPassable() const;         // can move through
```

#### Utility
```cpp
void clear();  // Reset all fields to default
```

## Migration Strategy

### Phase 1: Add Tile Class (Non-Breaking) ✅ COMPLETE

- Create `Source/levels/tile.hpp` with complete implementation
- Add validation test (`tile_test.cpp`)
- **No changes to existing code** - purely additive

### Phase 2: Update Level to Use Tile Array (Breaking Change)

**Goal**: Replace 11 separate `[MAXDUNX][MAXDUNY]` arrays with a single `Tile tiles_[MAXDUNX][MAXDUNY]` array.

#### Changes to `level.hpp`:

**Before:**
```cpp
uint16_t dPiece_[MAXDUNX][MAXDUNY] = {};
int8_t dTransVal_[MAXDUNX][MAXDUNY] = {};
// ... 9 more arrays
```

**After:**
```cpp
Tile tiles_[MAXDUNX][MAXDUNY] = {};

// Accessor for compatibility during migration
Tile& tileAt(int x, int y) { return tiles_[x][y]; }
const Tile& tileAt(int x, int y) const { return tiles_[x][y]; }
```

#### Update Macros in `gendung.h`:

**Option A: Keep macro compatibility (recommended for gradual migration)**
```cpp
// Macro shims that delegate to Tile methods
#define dPiece        (currentLevel().tiles_)  // Returns Tile[MAXDUNX][MAXDUNY]
#define dPieceAt(x,y) (currentLevel().tiles_[x][y].piece())
```

**Option B: Force breaking change (faster but requires updating all code)**
```cpp
// Remove all dPiece, dLight, etc. macros
// Force all code to use currentLevel().tiles_[x][y].piece() directly
```

**Recommendation**: Start with Option A for incremental migration, then move to Option B once most code is updated.

### Phase 3: Migrate Call Sites

#### Pattern 1: Direct Array Access
**Before:**
```cpp
dPiece[x][y] = 42;
dPlayer[x][y] = playerId;
```

**After (using tileAt accessor):**
```cpp
currentLevel().tileAt(x, y).setPiece(42);
currentLevel().tileAt(x, y).setPlayer(playerId);
```

**After (direct access):**
```cpp
auto& tile = currentLevel().tiles_[x][y];
tile.setPiece(42);
tile.setPlayer(playerId);
```

#### Pattern 2: Multiple Array Accesses
**Before:**
```cpp
if (dMonster[x][y] != 0 && HasAnyOf(dFlags[x][y], DungeonFlag::Visible)) {
    // do something
}
```

**After:**
```cpp
const auto& tile = currentLevel().tiles_[x][y];
if (tile.hasMonster() && tile.isVisible()) {
    // do something
}
```

#### Pattern 3: Clearing Tile Data
**Before:**
```cpp
dPiece[x][y] = 0;
dPlayer[x][y] = 0;
dMonster[x][y] = 0;
dCorpse[x][y] = 0;
dObject[x][y] = 0;
dItem[x][y] = 0;
dSpecial[x][y] = 0;
```

**After:**
```cpp
currentLevel().tiles_[x][y].clear();
```

### Phase 4: Remove Old Arrays

Once all code is migrated:
1. Remove the 11 separate array members from `level.hpp`
2. Remove the corresponding macros from `gendung.h`
3. Update save/load code if it directly serializes these arrays

## Performance Considerations

### Cache Line Analysis

Modern CPUs typically have 64-byte cache lines.

**Before (separate arrays):**
- Accessing `dPiece[10][20]`, `dPlayer[10][20]`, and `dMonster[10][20]` loads **3 cache lines**
- Arrays are ~13KB apart in memory (MAXDUNX × MAXDUNY ≈ 112 × 112)

**After (Tile array):**
- Accessing `tiles_[10][20]` loads **1 cache line** containing all 13 bytes of tile data
- Multiple adjacent tiles might fit in the same cache line

**Expected Improvement**: 2-3× reduction in cache misses for typical tile access patterns.

### Memory Footprint

**Before:**
```
uint16_t[112][112] = 2 bytes × 12,544 = 25,088 bytes
int8_t[112][112]   = 1 byte  × 12,544 = 12,544 bytes (×9 arrays = 112,896 bytes)
int16_t[112][112]  = 2 bytes × 12,544 = 25,088 bytes
------------------------------------------------------
Total: ~163 KB per level
```

**After:**
```
Tile[112][112] = 16 bytes × 12,544 = ~200 KB per level
```

**Trade-off**: ~23% more memory (163KB → 200KB) but dramatically better cache performance.

### Alignment & Padding

The `Tile` class is 13 bytes of data but will likely be padded to 14 or 16 bytes by the compiler for alignment. We explicitly verify `sizeof(Tile) <= 16` via `static_assert`.

**Best case**: 14 bytes × 12,544 = 175.6 KB  
**Worst case**: 16 bytes × 12,544 = 200.7 KB

Both are acceptable for the cache benefits gained.

## Testing Strategy

### Unit Tests
- [x] Size validation (`sizeof(Tile) <= 16`)
- [x] Default initialization (all zeros)
- [x] Getter/setter round-trip for all fields
- [x] Flag operations (add, remove, query)
- [x] Corpse encoding/decoding
- [x] Convenience methods (`isEmpty()`, `isOccupied()`, etc.)

### Integration Tests
- [ ] Create test level with Tile array
- [ ] Verify macro compatibility with existing code
- [ ] Benchmark tile access patterns (before vs after)
- [ ] Validate save/load compatibility

### Regression Tests
- [ ] Run full game test suite after migration
- [ ] Verify no visual artifacts
- [ ] Check multiplayer sync
- [ ] Confirm save file compatibility

## Future Enhancements

### Possible Extensions

1. **Spatial Queries**:
   ```cpp
   std::span<Tile> Level::tilesInRect(WorldTileRectangle rect);
   ```

2. **Iterators**:
   ```cpp
   for (auto& tile : currentLevel().allTiles()) {
       if (tile.hasMonster()) { ... }
   }
   ```

3. **Serialization**:
   ```cpp
   void Tile::serialize(Archive& ar);
   ```

4. **Undo/Redo**:
   ```cpp
   TileSnapshot Tile::snapshot() const;
   void Tile::restore(const TileSnapshot& snap);
   ```

5. **Compact Storage**:
   - Store only active tiles in a sparse map for large levels
   - Use bitfields to compress flag storage

## Compatibility Notes

### Save/Load

The old save format directly serializes the separate arrays. After migration:

**Option 1: Maintain Binary Compatibility**
- Keep old save format
- Convert between `Tile[]` and separate arrays during load/save

**Option 2: New Save Format**
- Serialize `Tile[]` directly
- Bump save version number
- Add converter for old saves

**Recommendation**: Option 1 for initial migration, Option 2 after stabilization.

### Macro Compatibility

The existing macro layer (`dPiece`, `dPlayer`, etc.) can be preserved during migration:

```cpp
// Temporary compatibility macros
#define dPiece        currentLevel().tiles_
#define dPieceValue(x, y)  currentLevel().tiles_[x][y].piece()
```

This allows gradual migration without breaking all existing code at once.

## Implementation Checklist

- [x] Create `tile.hpp` with complete `Tile` class
- [x] Add size validation test
- [x] Add functional validation test
- [ ] Update `level.hpp` to add `Tile tiles_[MAXDUNX][MAXDUNY]`
- [ ] Add compatibility accessors to `Level`
- [ ] Update `gendung.h` macros for Tile support
- [ ] Migrate high-impact hot paths first (rendering, pathfinding)
- [ ] Migrate entity management code
- [ ] Migrate level generation code
- [ ] Update save/load serialization
- [ ] Remove old arrays once migration complete
- [ ] Remove compatibility macros
- [ ] Update documentation

## Conclusion

The `Tile` class provides a cleaner, more maintainable, and higher-performance way to manage per-tile dungeon data. While the migration requires careful planning, the benefits in code quality and performance make it worthwhile.

**Status**: Phase 1 complete ✅  
**Next Step**: Phase 2 - Update `Level` class to use `Tile` array
