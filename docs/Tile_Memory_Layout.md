# Tile Memory Layout Analysis

## Tile Structure Layout

```cpp
class Tile {
private:
    uint16_t piece_;        // 2 bytes [offset 0-1]
    int8_t transVal_;       // 1 byte  [offset 2]
    uint8_t light_;         // 1 byte  [offset 3]
    uint8_t preLight_;      // 1 byte  [offset 4]
    DungeonFlag flags_;     // 1 byte  [offset 5]
    int8_t player_;         // 1 byte  [offset 6]
    int16_t monster_;       // 2 bytes [offset 7-8]  ← may need alignment padding
    int8_t corpse_;         // 1 byte  [offset 9]
    int8_t object_;         // 1 byte  [offset 10]
    int8_t special_;        // 1 byte  [offset 11]
    int8_t item_;           // 1 byte  [offset 12]
};
```

### Actual Memory Layout (with alignment)

Compiler will likely align `int16_t monster_` to 2-byte boundary:

```
Byte    0   1   2   3   4   5   6   7   8   9   10  11  12  13  14  15
      ┌───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┐
      │ piece │trn│lgt│pre│flg│ply│PAD│ monster │crp│obj│spc│itm│PAD│PAD│
      └───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┘

piece       = piece_ (uint16_t)
trn         = transVal_ (int8_t)
lgt         = light_ (uint8_t)
pre         = preLight_ (uint8_t)
flg         = flags_ (DungeonFlag/uint8_t)
ply         = player_ (int8_t)
PAD         = padding byte (for monster_ alignment)
monster     = monster_ (int16_t)
crp         = corpse_ (int8_t)
obj         = object_ (int8_t)
spc         = special_ (int8_t)
itm         = item_ (int8_t)
PAD PAD     = padding bytes (to round up to 16 bytes)
```

**Total: 16 bytes** (13 bytes data + 3 bytes padding)

## Comparison with Separate Arrays

### Before: Separate Arrays in Level

```
Level {
    uint16_t dPiece_[112][112];      // 25,088 bytes  ┐
    int8_t dTransVal_[112][112];     // 12,544 bytes  │
    uint8_t dLight_[112][112];       // 12,544 bytes  │
    uint8_t dPreLight_[112][112];    // 12,544 bytes  │
    DungeonFlag dFlags_[112][112];   // 12,544 bytes  ├─ ~163 KB
    int8_t dPlayer_[112][112];       // 12,544 bytes  │
    int16_t dMonster_[112][112];     // 25,088 bytes  │
    int8_t dCorpse_[112][112];       // 12,544 bytes  │
    int8_t dObject_[112][112];       // 12,544 bytes  │
    int8_t dSpecial_[112][112];      // 12,544 bytes  │
    int8_t dItem_[112][112];         // 12,544 bytes  ┘
}
```

**Memory Layout**:
```
Address:    dPiece_    dTransVal_  dLight_     dPreLight_  ...
           ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐
    [0][0] │ piece  │ │  trans │ │  light │ │prelight│ ... (13-26KB apart!)
           └────────┘ └────────┘ └────────┘ └────────┘
              ↑          ↑           ↑           ↑
           cache line  cache line  cache line  cache line
              #1          #2          #3          #4
```

### After: Tile Array

```
Level {
    Tile tiles_[112][112];  // 200,704 bytes (~200 KB)
}
```

**Memory Layout**:
```
Address:    tiles_[0][0]                        tiles_[0][1]
           ┌──────────────────────────────────┐ ┌───────────
    Bytes  │piece│trn│lgt│...│itm│PAD│PAD│PAD│ │piece│trn│...
           └──────────────────────────────────┘ └───────────
                         ↑
                     ONE cache line
              (contains ALL tile properties)
```

## Cache Line Analysis

Modern CPUs use 64-byte cache lines.

### Before: Accessing tile[x][y] properties

To read `piece`, `player`, `monster`, `flags` for tile[10][20]:

```
Step 1: Read dPiece_[10][20]
   → Load cache line from offset (10*112 + 20)*2 = 2440 bytes into dPiece_

Step 2: Read dPlayer_[10][20]  
   → Load cache line from offset (10*112 + 20) = 1220 bytes into dPlayer_
   → dPlayer_ starts 25KB+ after dPiece_
   → CACHE MISS (different cache line)

Step 3: Read dMonster_[10][20]
   → Load cache line from offset (10*112 + 20)*2 = 2440 bytes into dMonster_
   → dMonster_ starts 75KB+ after dPiece_
   → CACHE MISS (different cache line)

Step 4: Read dFlags_[10][20]
   → Load cache line from offset (10*112 + 20) = 1220 bytes into dFlags_
   → dFlags_ starts 50KB+ after dPiece_
   → CACHE MISS (different cache line)
```

**Result: 4 cache line loads** for 4 properties

### After: Accessing tiles_[x][y] properties

To read `piece`, `player`, `monster`, `flags` for tiles_[10][20]:

```
Step 1: Read tiles_[10][20]
   → Load cache line from offset (10*112 + 20)*16 = 19,520 bytes into tiles_
   → Cache line contains 64 bytes = 4 complete Tile objects (16 bytes each)

Step 2: Access tile.piece()      → Already in cache (same 16-byte struct)
Step 3: Access tile.player()     → Already in cache (same 16-byte struct)
Step 4: Access tile.monster()    → Already in cache (same 16-byte struct)
Step 5: Access tile.flags()      → Already in cache (same 16-byte struct)
```

**Result: 1 cache line load** for all properties

**Speedup: 4×** (in ideal case)

## Spatial Locality Benefits

When processing adjacent tiles (common in rendering/pathfinding):

### Before: Separate Arrays
```
Process tiles [10][20], [10][21], [10][22], [10][23]:
- dPiece_[10][20..23] = 8 bytes → likely 1 cache line ✓
- dPlayer_[10][20..23] = 4 bytes → need to load from different array ✗
- dMonster_[10][20..23] = 8 bytes → need to load from different array ✗
- dFlags_[10][20..23] = 4 bytes → need to load from different array ✗

Total: 4 cache lines (minimum)
```

### After: Tile Array
```
Process tiles_[10][20], [10][21], [10][22], [10][23]:
- 4 tiles × 16 bytes = 64 bytes → exactly 1 cache line ✓

Total: 1 cache line (all data!)
```

**Speedup for adjacent tiles: 4-8×**

## Memory Footprint Breakdown

### Current (Separate Arrays)

```
Array               Type        Elements    Size        % of Total
─────────────────────────────────────────────────────────────────
dPiece_             uint16_t    12,544      25,088 B    15.4%
dTransVal_          int8_t      12,544      12,544 B     7.7%
dLight_             uint8_t     12,544      12,544 B     7.7%
dPreLight_          uint8_t     12,544      12,544 B     7.7%
dFlags_             uint8_t     12,544      12,544 B     7.7%
dPlayer_            int8_t      12,544      12,544 B     7.7%
dMonster_           int16_t     12,544      25,088 B    15.4%
dCorpse_            int8_t      12,544      12,544 B     7.7%
dObject_            int8_t      12,544      12,544 B     7.7%
dSpecial_           int8_t      12,544      12,544 B     7.7%
dItem_              int8_t      12,544      12,544 B     7.7%
─────────────────────────────────────────────────────────────────
TOTAL                           12,544      163,072 B   100%
                                            (~163 KB)
```

### Proposed (Tile Array)

```
Array               Type        Elements    Size        % of Total
─────────────────────────────────────────────────────────────────
tiles_              Tile        12,544      200,704 B   100%
                                            (~200 KB)
─────────────────────────────────────────────────────────────────
OVERHEAD                                    +37,632 B   +23%
```

**Trade-off**: +23% memory for 2-4× cache performance improvement

### Per-Tile Overhead

```
Data:    13 bytes (sum of all fields)
Padding:  3 bytes (for alignment)
Total:   16 bytes per tile

Overhead: 3/16 = 18.75% per tile
```

### Why Padding is Acceptable

1. **Cache Alignment**: 16 bytes keeps tiles aligned optimally
2. **Cache Lines**: 64-byte cache line ÷ 16 = 4 tiles per line (clean division)
3. **SIMD Potential**: 16-byte alignment good for future SIMD operations
4. **Small Absolute Cost**: 3 bytes × 12,544 tiles = ~37 KB total padding

## Cache Miss Estimation

### Typical Rendering Loop (60×60 viewport)

**Before (separate arrays)**:
```
For each tile (3,600 tiles):
    Access dPiece_, dLight_, dFlags_
    → 3 properties × 3,600 tiles = 10,800 memory accesses
    → Assuming 50% cache miss rate = 5,400 cache misses
```

**After (Tile array)**:
```
For each tile (3,600 tiles):
    Access tiles_[x][y]
    → 1 object × 3,600 tiles = 3,600 memory accesses
    → Assuming spatial locality, ~30% cache miss rate = 1,080 cache misses
```

**Cache Miss Reduction: 5,400 → 1,080 (5× fewer misses)**

### Typical Pathfinding Query (A* search)

**Before**:
```
Check 100 tiles for walkability:
    Each tile: dPlayer_, dMonster_, dObject_, dFlags_
    → 4 arrays × 100 tiles = 400 memory accesses
    → Random access pattern ≈ 80% miss rate = 320 cache misses
```

**After**:
```
Check 100 tiles for walkability:
    Each tile: tiles_[x][y].isPassable()
    → 1 access × 100 tiles = 100 memory accesses
    → Random access ≈ 70% miss rate = 70 cache misses
```

**Cache Miss Reduction: 320 → 70 (4.5× fewer misses)**

## Real-World Performance Impact

### Conservative Estimates

Assuming:
- 60 FPS target
- 100 cache misses per frame (from tile access)
- 100 CPU cycles per cache miss
- 3 GHz CPU

**Before**:
```
100 misses × 100 cycles = 10,000 cycles per frame
10,000 / 3,000,000,000 Hz = 3.3 microseconds per frame
```

**After**:
```
25 misses × 100 cycles = 2,500 cycles per frame
2,500 / 3,000,000,000 Hz = 0.83 microseconds per frame
```

**Savings**: 2.5 microseconds per frame (minor but measurable)

### Aggressive Estimates (heavy tile processing)

Assuming:
- Complex AI frame with heavy pathfinding
- 1,000 cache misses per frame (from tile access)

**Before**: 33 microseconds per frame  
**After**: 8 microseconds per frame  
**Savings**: 25 microseconds per frame (0.15% of 16ms frame budget)

**Conclusion**: Cache improvement is real but modest in absolute terms. Main benefit is **code quality and maintainability**.

## Compiler Optimizations

### Possible Layouts (compiler-dependent)

**MSVC typically chooses**:
```
struct Tile {  // 16 bytes (adds 1 padding after player_, 2 at end)
    uint16_t piece_;     // [0-1]
    int8_t transVal_;    // [2]
    uint8_t light_;      // [3]
    uint8_t preLight_;   // [4]
    DungeonFlag flags_;  // [5]
    int8_t player_;      // [6]
    // padding byte [7]
    int16_t monster_;    // [8-9]
    int8_t corpse_;      // [10]
    int8_t object_;      // [11]
    int8_t special_;     // [12]
    int8_t item_;        // [13]
    // padding bytes [14-15]
};
```

**GCC/Clang might choose**:
```
struct Tile {  // 14 bytes (adds 1 padding after player_)
    uint16_t piece_;     // [0-1]
    int8_t transVal_;    // [2]
    uint8_t light_;      // [3]
    uint8_t preLight_;   // [4]
    DungeonFlag flags_;  // [5]
    int8_t player_;      // [6]
    // padding byte [7]
    int16_t monster_;    // [8-9]
    int8_t corpse_;      // [10]
    int8_t object_;      // [11]
    int8_t special_;     // [12]
    int8_t item_;        // [13]
};
```

We accept up to 16 bytes total via `static_assert(sizeof(Tile) <= 16)`.

## Summary

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| **Memory per level** | 163 KB | 200 KB | +23% |
| **Cache lines per tile access** | 3-4 | 1 | -75% |
| **Memory accesses per tile** | 3-11 | 1 | -70% to -90% |
| **Cache miss rate** | 50-80% | 30-70% | -20% to -10% |
| **Bytes per tile** | 13 | 16 | +23% |
| **Useful data per tile** | 13 | 13 | 0% |
| **Padding per tile** | 0 | 3 | +3 bytes |

**Trade-off Summary**: Modest memory increase (+37 KB) for significant cache improvements (2-4× fewer cache misses).

**Recommendation**: **Accept the trade-off**. Memory is cheap, cache misses are expensive.
