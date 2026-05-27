# Phase 4B: Entity Data Migration

## Summary

Phase 4B migrates entity occupation data from legacy macro shims to Tile API. This includes player, monster, corpse, object, item, and special tile data.

**Status:** In Progress  
**Focus Areas:** Core migrations with comprehensive testing framework

## What Changed

### New Test Suite

**test/phase4b_entity_test.cpp** (20+ tests)
- Player entity read/write operations
- Monster entity with moving detection
- Corpse entity with index/direction encoding
- Object entity with extension detection
- Item entity operations
- Special tile operations
- Multi-entity independence
- Boundary condition testing
- Stress tests for bulk operations

### API Reference

All entity data is now accessible through Tile API:

```cpp
// Player entity
int8_t player() const;
void setPlayer(int8_t value);
bool hasPlayer() const;

// Monster entity (negative = moving)
int16_t monster() const;
void setMonster(int16_t value);
bool hasMonster() const;
bool hasMovingMonster() const;

// Corpse entity (index & 0x1F, direction >> 5)
int8_t corpse() const;
void setCorpse(int8_t value);
bool hasCorpse() const;
int8_t corpseIndex() const;
int8_t corpseDirection() const;

// Object entity (negative = extension)
int8_t object() const;
void setObject(int8_t value);
bool hasObject() const;
bool isObjectExtension() const;

// Item entity
int8_t item() const;
void setItem(int8_t value);
bool hasItem() const;

// Special tile entity
int8_t special() const;
void setSpecial(int8_t value);
bool hasSpecial() const;
```

### Migration Pattern

**Before (macro):**
```cpp
dPlayer[x][y] = playerId;
auto id = dMonster[pos.x][pos.y];
```

**After (Tile API):**
```cpp
tileAt(x, y).setPlayer(playerId);
auto id = tileAt(pos).monster();
```

## Phase Breakdown

### Phase 4B-1: Player Module (COMPLETE)
- ✓ source/player.cpp (4 locations)
- All player entity references migrated to Tile API
- No regressions in player_test.cpp

### Phase 4B-2 through 4B-8: Core Modules (Queued)
- Monster module (20 occurrences)
- Corpse/Dead module (2 occurrences)
- Item module (21 occurrences)
- Object module (58 occurrences - largest)
- Missile/Collision module (18 occurrences)
- Rendering/UI modules (cursor.cpp, debug.cpp)
- Utility modules (loadsave, inv, towners, msg, sync, automap)

### Phase 4B-9: Testing & Benchmarks
- Comprehensive test suite registered in CMake
- Benchmark suite for performance validation
- All existing tests remain functional

### Phase 4B-10: Documentation & Completion
- Phase_4B.md completion checklist
- .copilot migration pattern reference
- Final verification and commit

## Tile API Usage

Read: `tileAt(Point).player()`, `tileAt(x, y).monster()`  
Write: `tile.setPlayer(value)`, `tile.setMonster(value)`  
Check: `tile.hasPlayer()`, `tile.hasMonster()`, `tile.isObjectExtension()`  
Encode: `corpse() = (direction << 5) | index`

## Backward Compatibility

- All migrations maintain 100% backward compatibility
- Legacy arrays (dPlayer_, dMonster_, etc.) remain in Level class
- Both old and new code can coexist during migration
- Phase 5 (future) will remove legacy arrays after full migration

## Test Coverage

**phase4b_entity_test.cpp** provides:
- Unit tests for each entity type (player, monster, corpse, object, item, special)
- Edge case testing (moving monsters, object extensions, corpse encoding)
- Multi-entity independence verification
- Boundary condition testing
- Stress tests for bulk operations
- Signed value range testing

## Next Phase

Phase 4B-2: Migrate monster.cpp and related modules, then systematically work through remaining modules in order of complexity and impact.

Phase 5: Remove legacy entity arrays after all code is migrated to Tile API.
