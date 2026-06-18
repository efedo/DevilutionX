# Phase 4B: Entity Data Migration to Tile API

## Executive Summary

Phase 4B will migrate all entity occupation data from legacy macro shims to the Tile API. This includes:
- `dPlayer[]` → `tile.player()`
- `dMonster[]` → `tile.monster()`
- `dCorpse[]` → `tile.corpse()`
- `dObject[]` → `tile.object()`
- `dItem[]` → `tile.item()`
- `dSpecial[]` → `tile.special()`

## Audit Summary

### Usage by File (201 total occurrences)

**High Priority (>10 occurrences):**
- objects.cpp: 58
- cursor.cpp: 23
- loadsave.cpp: 20
- monster.cpp: 20
- items.cpp: 21
- missiles.cpp: 18

**Medium Priority (5-10):**
- debug.cpp: 11
- inv.cpp: 7
- towners.cpp: 5

**Low Priority (<5):**
- automap.cpp: 2
- dead.cpp: 2
- msg.cpp: 4
- player.cpp: 8
- sync.cpp: 1
- tile_properties_test.cpp: 1
- dead_test.cpp: 2

## Migration Strategy

### Phase Structure

**Phase 4B-1: Player Module (8 occurrences)**
- Migrate player.cpp entity updates
- Update occupyTile() and related methods
- Verify player_test.cpp compatibility

**Phase 4B-2: Monster Module (20 occurrences)**
- Migrate monster.cpp entity updates
- Update SaveMonster() / LoadMonster() routines

**Phase 4B-3: Corpse Module (2 occurrences)**
- Migrate dead.cpp entity updates
- Update corpse handling

**Phase 4B-4: Item Module (21 occurrences)**
- Migrate items.cpp entity updates
- Update item placement logic

**Phase 4B-5: Object Module (58 occurrences)**
- Migrate objects.cpp entity updates
- Largest migration target; may require multiple commits

**Phase 4B-6: Missile/Collision Module (18 occurrences)**
- Migrate missiles.cpp entity updates
- Update collision detection

**Phase 4B-7: Rendering/UI Modules (23+11 occurrences)**
- Migrate cursor.cpp rendering
- Migrate debug.cpp diagnostics

**Phase 4B-8: Utility Modules (11+7+5+4+1 occurrences)**
- Migrate loadsave.cpp (20)
- Migrate inv.cpp (7)
- Migrate towners.cpp (5)
- Migrate msg.cpp (4)
- Migrate sync.cpp (1)
- Migrate automap.cpp (2)

**Phase 4B-9: Testing & Validation**
- Create `test/phase4b_entity_test.cpp`
- Create `test/phase4b_entity_benchmark.cpp`
- Update existing tests (dead_test.cpp, tile_properties_test.cpp)

**Phase 4B-10: Documentation & Completion**
- Create `docs/Phase_4B.md` completion checklist
- Update `.copilot` with migration pattern reference
- Final verification and git commit

## Tile API Reference

### Entity Accessors (all constexpr)

```cpp
// Reading
int8_t player() const;
int16_t monster() const;
int8_t corpse() const;
int8_t object() const;
int8_t item() const;
int8_t special() const;

// Writing
void setPlayer(int8_t value);
void setMonster(int16_t value);
void setCorpse(int8_t value);
void setObject(int8_t value);
void setItem(int8_t value);
void setSpecial(int8_t value);

// Query helpers
bool hasPlayer() const;
bool hasMonster() const;
bool hasMovingMonster() const;
bool hasCorpse() const;
bool hasObject() const;
bool isObjectExtension() const;
bool hasItem() const;
bool hasSpecial() const;
```

### Access Pattern

**Legacy (macro-based):**
```cpp
dPlayer[x][y] = playerId;
auto id = dMonster[position.x][position.y];
```

**New (Tile API):**
```cpp
tileAt(x, y).setPlayer(playerId);
auto id = tileAt(position).monster();
```

## Migration Pattern

Each migration follows this pattern:

1. Identify all uses of entity macro in file
2. Replace array indexing with `tileAt()` accessor
3. Replace macro read/write with appropriate Tile method
4. Verify no compilation errors
5. Run existing tests to verify backward compatibility
6. Add new tests for migrated code (if applicable)

## Backward Compatibility

All migrations maintain 100% backward compatibility:
- Tile API is new; legacy arrays still exist in Level class
- During migration phase, both can coexist
- Phase 5 (future) will remove legacy arrays after full migration

## Testing Requirements

Each sub-phase includes:
- Existing test suite verification (no regressions)
- New unit tests for entity-specific logic
- Stress tests for bulk operations (loadsave, monster lists)

## Documentation Requirements

Each sub-phase includes:
- Brief comment explaining migration pattern
- Reference to Phase 4B_Plan.md if non-obvious
- No redundant documentation (avoid repeating Tile API docs)

## Success Criteria

✓ All 201 entity macro uses migrated to Tile API
✓ All existing tests pass without modification
✓ New comprehensive test suite added
✓ No performance regressions (benchmarked)
✓ 100% backward compatibility maintained
✓ Clear commit history tracking each sub-phase
