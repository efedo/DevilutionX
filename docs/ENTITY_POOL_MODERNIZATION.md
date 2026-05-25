# EntityPool Modernization Strategy

## Overview

This document describes the strategy for modernizing fixed-size array pools (`Objects[]`, `ActiveObjects[]`, `AvailableObjects[]`, `ActiveObjectCount`) into type-safe, STL-compatible containers while preserving binary save-file compatibility and enabling gradual migration.

## Problem Statement

The codebase uses a repeated pattern across three entity types:

### Objects (objects.h/objects.cpp)
```cpp
extern Object Objects[MAXOBJECTS];
extern int AvailableObjects[MAXOBJECTS];   // Free-list indices
extern int ActiveObjects[MAXOBJECTS];      // Active indices
extern int ActiveObjectCount;              // Count of active items
```

### Monsters (monster.h/monster.cpp)
```cpp
extern Monster Monsters[MaxMonsters];
extern unsigned ActiveMonsters[MaxMonsters];
extern size_t ActiveMonsterCount;
```

### Items (items.h/items.cpp)
```cpp
extern Item Items[MAXITEMS + 1];
extern uint8_t ActiveItems[MAXITEMS];
extern uint8_t ActiveItemCount;
```

**Issues with this pattern:**
- No type safety; easy to mix up which array is which
- No encapsulation; internals exposed globally
- Manual lifecycle management (allocation/deallocation)
- Inconsistent between entity types (sparse vs. dense)
- Difficult to refactor without breaking backward compatibility

## Solution: DenseEntityPool<T>

A generic, type-safe container that:
1. **Encapsulates** pool management internally
2. **Provides STL-compatible iteration** over active elements
3. **Supports two allocation policies**:
   - `SparseAllocationPolicy`: Stable indices (free-list) — suitable for Objects
   - `DenseAllocationPolicy`: Unstable indices (swap-with-last) — suitable for Monsters/Items
4. **Maintains exact binary format** for save/load compatibility
5. **Exposes legacy pointers** for gradual migration

### Key Design Decisions

#### Question 1: Sparse vs. Dense Allocation

**Chosen: Sparse (for Objects)** because:
- Objects are referenced via `dObject[x][y]` map lookup tables
- Deleting an object must not change indices of other objects (would break map references)
- Swapping would require updating all `dObject` entries — error-prone
- Current `AvailableObjects` free-list is already in place
- Exact save-file format can be preserved

**Trade-off accepted:**
- Slightly slower iteration (must skip gaps) — mitigated by STL iterator that only walks active items
- More memory overhead for free-list — negligible (127 ints ≈ 500 bytes)

#### Question 2: Binary Compatibility

**Approach: Exact Index Preservation**

The pool stores elements in **exact array positions** matching the original:
- `Objects[123]` in legacy code = `gObjectPool[123]` in new code
- `ActiveObjects[i]` = `gObjectPool.activeIndices()[i]`
- `ActiveObjectCount` = `gObjectPool.activeCount()`

Save/load code **does not need to change** if it serializes by index.
If load code reads `Objects[i]._otype`, it works exactly as before.

#### Question 3: Reusability

**Created `DenseEntityPool<T, MaxCapacity, AllocationPolicy>`** template that:
- Is fully generic (works with any `T`)
- Requires only allocation policy selection
- Can be applied to Objects now, Monsters/Items later

### File Structure

```
Source/
  utils/
    entity_pool.hpp          # Generic pool template
  object_pool.h              # ObjectPool typedef and legacy pointers
  object_pool.cpp            # Global pool instance, initialization
  objects.h                  # Unchanged externally; includes object_pool.h
  objects.cpp                # Unchanged; uses global pointers
```

## Migration Path

### Phase 1: Introduce Pool (This PR)
- Add `entity_pool.hpp` (generic template)
- Add `object_pool.h/cpp` (ObjectPool wrapper)
- Update `objects.h` to include `object_pool.h`
- **No changes to objects.cpp or call sites yet**
- Verify binary format unchanged

### Phase 2: Gradual Adoption (Future)
- Create `ObjectManager::allocate()` that calls `gObjectPool.allocate()`
- Create `ObjectManager::deallocate()` that calls `gObjectPool.deallocate()`
- Migrate call sites incrementally to `ObjectManager` API
- Keep legacy code working during transition

### Phase 3: Monsters & Items (Future)
- Wrap Monsters in `DenseEntityPool<Monster, MaxMonsters, DenseAllocationPolicy>`
- Wrap Items in `DenseEntityPool<Item, MAXITEMS, DenseAllocationPolicy>`
- Update save/load code if needed (with index remapping)

## API Comparison

### Legacy Code
```cpp
Object *obj = &Objects[i];
if (ActiveObjectCount > 0) {
  for (int j = 0; j < ActiveObjectCount; ++j) {
    Object &active = Objects[ActiveObjects[j]];
    // ...
  }
}
```

### New Code (Direct Pool Usage)
```cpp
Object *obj = &gObjectPool[i];
if (!gObjectPool.empty()) {
  for (Object &active : gObjectPool) {
    // ...
  }
}
```

### New Code (via ObjectManager Facade)
```cpp
Object *obj = ObjectManager::Find(i);
for (Object &active : ObjectManager::GetAll()) {
  // ...
}
```

## Serialization Guarantee

The pool's internal layout is:
```cpp
T elements_[MaxCapacity];           // Raw element array (binary identical)
size_type activeIndices_[MaxCapacity];  // Active index list (binary identical)
size_type activeCount_;             // Active count (binary identical)
```

When serializing:
```cpp
// Legacy way (still works)
for (int i = 0; i < MAXOBJECTS; ++i) {
  SaveObject(Objects[i]);
}
SaveInt(ActiveObjectCount);

// New way (identical result)
for (size_t i = 0; i < gObjectPool.capacity(); ++i) {
  SaveObject(gObjectPool[i]);
}
SaveInt(gObjectPool.activeCount());
```

Both produce identical binary output → save files remain compatible.

## Testing Strategy

1. **Unit tests**: Verify `DenseEntityPool` behavior (allocate, deallocate, iterate)
2. **Compatibility tests**: Load legacy save files with new pool code
3. **Behavioral tests**: Existing object tests should pass unchanged
4. **Migration tests**: Add tests for `ObjectManager` facade as it's introduced

## Conclusion

This approach provides:
- **Type safety** without breaking changes
- **STL compatibility** for modern C++ idioms
- **Zero-cost abstraction** (compile-time optimized)
- **Gradual migration** (existing code works as-is)
- **Reusability** (template applies to all entity types)
- **Binary compatibility** (save files unchanged)
