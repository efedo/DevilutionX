# Tile Class Quick Reference

## Quick Start

```cpp
#include "levels/tile.hpp"

// Get a tile
Tile& tile = currentLevel().tiles_[x][y];

// Set properties
tile.setPiece(42);
tile.setPlayer(1);
tile.addFlags(DungeonFlag::Visible);

// Query properties
if (tile.hasMonster() && tile.isVisible()) {
    int monsterId = tile.monster();
    // ...
}

// Clear tile
tile.clear();
```

## Common Patterns

### Check if Tile is Walkable
```cpp
const Tile& tile = currentLevel().tiles_[x][y];
bool canWalk = tile.isPassable() && !tile.isPopulated();
```

### Place Entity
```cpp
Tile& tile = currentLevel().tiles_[x][y];
if (tile.isPassable()) {
    tile.setMonster(monsterId);
    tile.addFlags(DungeonFlag::Populated);
}
```

### Check Multiple Properties
```cpp
const Tile& tile = currentLevel().tiles_[x][y];
if (tile.isVisible() && tile.isExplored() && tile.hasItem()) {
    int itemId = tile.item();
    // show item to player
}
```

### Clear Region
```cpp
for (int y = startY; y < endY; y++) {
    for (int x = startX; x < endX; x++) {
        currentLevel().tiles_[x][y].clear();
    }
}
```

## API Reference

### Getters/Setters
| Property | Type | Getter | Setter |
|----------|------|--------|--------|
| Piece ID | `uint16_t` | `piece()` | `setPiece(value)` |
| Transparency | `int8_t` | `transVal()` | `setTransVal(value)` |
| Light | `uint8_t` | `light()` | `setLight(value)` |
| Pre-Light | `uint8_t` | `preLight()` | `setPreLight(value)` |
| Flags | `DungeonFlag` | `flags()` | `setFlags(value)` |
| Player | `int8_t` | `player()` | `setPlayer(value)` |
| Monster | `int16_t` | `monster()` | `setMonster(value)` |
| Corpse | `int8_t` | `corpse()` | `setCorpse(value)` |
| Object | `int8_t` | `object()` | `setObject(value)` |
| Special | `int8_t` | `special()` | `setSpecial(value)` |
| Item | `int8_t` | `item()` | `setItem(value)` |

### Entity Checks
| Method | Returns | Description |
|--------|---------|-------------|
| `hasPlayer()` | `bool` | True if player at tile |
| `hasMonster()` | `bool` | True if monster at tile |
| `hasMovingMonster()` | `bool` | True if moving monster |
| `hasCorpse()` | `bool` | True if corpse at tile |
| `hasObject()` | `bool` | True if object at tile |
| `isObjectExtension()` | `bool` | True if extended object area |
| `hasItem()` | `bool` | True if item at tile |
| `hasSpecial()` | `bool` | True if special tile |

### State Checks
| Method | Returns | Description |
|--------|---------|-------------|
| `isEmpty()` | `bool` | No entities at all |
| `isOccupied()` | `bool` | Has any entity |
| `isPassable()` | `bool` | Can be walked through |
| `isVisible()` | `bool` | Visible to players |
| `isExplored()` | `bool` | Has been explored |
| `isLit()` | `bool` | Tile is lit |
| `hasDeadPlayer()` | `bool` | Dead player corpse |
| `isPopulated()` | `bool` | Contains set piece |
| `hasMissile()` | `bool` | Contains missile |

### Flag Operations
| Method | Description |
|--------|-------------|
| `addFlags(mask)` | Add flags |
| `removeFlags(mask)` | Remove flags |
| `hasAnyFlag(mask)` | Check if any flag set |
| `hasAllFlags(mask)` | Check if all flags set |

### Corpse Helpers
| Method | Returns | Description |
|--------|---------|-------------|
| `corpseIndex()` | `int8_t` | Corpse array index (& 0x1F) |
| `corpseDirection()` | `int8_t` | Corpse direction (>> 5) |

### Utility
| Method | Description |
|--------|-------------|
| `clear()` | Reset all fields to default |

## Flag Constants

```cpp
DungeonFlag::None
DungeonFlag::Missile
DungeonFlag::Visible
DungeonFlag::DeadPlayer
DungeonFlag::Populated
DungeonFlag::MissileFireWall
DungeonFlag::MissileLightningWall
DungeonFlag::Lit
DungeonFlag::Explored
```

## Migration Cheat Sheet

### Before (Separate Arrays)
```cpp
// Read
uint16_t piece = dPiece[x][y];
int8_t player = dPlayer[x][y];

// Write
dPiece[x][y] = 42;
dPlayer[x][y] = 1;

// Check
if (dMonster[x][y] != 0 && 
    HasAnyOf(dFlags[x][y], DungeonFlag::Visible)) {
    // ...
}

// Clear
dPiece[x][y] = 0;
dPlayer[x][y] = 0;
dMonster[x][y] = 0;
// ... 8 more lines
```

### After (Tile Class)
```cpp
// Read
Tile& tile = currentLevel().tiles_[x][y];
uint16_t piece = tile.piece();
int8_t player = tile.player();

// Write
tile.setPiece(42);
tile.setPlayer(1);

// Check
if (tile.hasMonster() && tile.isVisible()) {
    // ...
}

// Clear
tile.clear();
```

## Performance Notes

- **Cache-Friendly**: All tile data in 16 bytes (fits in one cache line)
- **Array of Structs**: `Tile[112][112]` better than 11 separate arrays
- **Access Pattern**: Get tile once, query many properties = 1 cache load
- **Memory**: ~200KB per level (acceptable trade-off for cache benefits)

## Files

- **Header**: `Source/levels/tile.hpp`
- **Test**: `Source/levels/tile_test.cpp`
- **Examples**: `Source/levels/tile_usage_examples.cpp`
- **Design**: `docs/Tile_Class_Design.md`
- **Summary**: `docs/Tile_Implementation_Summary.md`

## Status

✅ **Phase 1 Complete**: Tile class implemented and validated  
🔄 **Phase 2 Pending**: Integration into Level class  
🔄 **Phase 3 Pending**: Migration of call sites  
🔄 **Phase 4 Pending**: Remove old arrays

## Questions?

See full documentation:
- `docs/Tile_Class_Design.md` - Complete design rationale
- `docs/Tile_Implementation_Summary.md` - Implementation details
- `Source/levels/tile_usage_examples.cpp` - 12 detailed examples
