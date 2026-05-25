# ObjectPool Migration Guide

## Quick Start

The modern `ObjectPool` is now available and ready for incremental migration. This guide shows how to use it safely alongside the legacy globals.

## Accessing the Pool

### Include the Header
```cpp
#include "object_pool.h"  // Provides gObjectPool
```

### Query Functions
```cpp
// Get the global pool
ObjectPool &pool = gObjectPool;

// Check pool state
int activeCount = pool.activeCount();
bool isFull = (activeCount >= MAXOBJECTS);

// Access by index (unchecked)
Object &obj = pool[42];

// Iterate over active objects
for (Object &obj : pool) {
    // Process active object
}
```

## Migration Patterns

### Pattern 1: Replace Manual Loops

**Before (Legacy)**
```cpp
// Process all active objects
for (int i = 0; i < ActiveObjectCount; ++i) {
    Object &obj = Objects[ActiveObjects[i]];
    ProcessObject(obj);
}
```

**After (Modern)**
```cpp
// Use pool iterator
for (Object &obj : gObjectPool) {
    ProcessObject(obj);
}
```

### Pattern 2: Safe Index-Based Access

**Before**
```cpp
Object &obj = Objects[objectIndex];
```

**After**
```cpp
// Both are equivalent and still valid:
Object &obj = gObjectPool[objectIndex];     // Modern
// OR
Object &obj = Objects[objectIndex];         // Legacy (still works)
```

### Pattern 3: Allocating New Objects

The allocation system should continue using `AddObject()` from `objects.h` for now.
The pool is maintained as a parallel structure (future work).

```cpp
// Continue using legacy API for allocation
Object *newObj = AddObject(OBJ_BARREL, { 10, 20 });

// The pool will eventually be synchronized to track this
```

## Safety Guarantees

### Stable Indices
The pool uses sparse allocation, which means:
- Object indices never change during their lifetime
- Safe to store indices in save files
- Safe for network sync messages
- Safe for map references (`dObject[][]`)

### Thread Safety
- The pool is **not thread-safe** (same as legacy arrays)
- All access must be serialized

### Memory Layout
The pool's memory layout is:
```cpp
Object elements_[MAXOBJECTS];           // Raw element array
uint32_t activeIndices_[MAXOBJECTS];    // Active indices
uint32_t activeCount_;                  // Active count
```

This means:
- Binary serialization is identical to legacy format
- Save/load code needs **no changes**
- Old save files work with new code

## Recommended Hot-Path Optimizations

### High-Priority Candidates
1. **Object rendering loop** - iterates all active objects
2. **Object update/process loop** - in ProcessObjects()
3. **Object search queries** - FindObjectAtPosition()
4. **Monster door interaction** - MonstCheckDoors()

### Low-Priority Candidates
- Rarely-executed initialization code
- Single-object lookups

## Example: Optimizing ProcessObjects()

**Current Implementation (Legacy)**
```cpp
void ProcessObjects()
{
    for (int i = 0; i < ActiveObjectCount; ++i) {
        Object &obj = Objects[ActiveObjects[i]];
        UpdateTrapState(obj);
        // ... more processing
    }
}
```

**Optimized (Modern)**
```cpp
void ProcessObjects()
{
    for (Object &obj : gObjectPool) {
        UpdateTrapState(obj);
        // ... more processing
    }
}
```

**Benefits**:
- Simpler, clearer code
- Fewer indirections (direct iterator)
- Compiler can optimize better

## Common Pitfalls

### ❌ Don't Mix Allocation APIs
```cpp
// WRONG: Mixing legacy and pool allocation
Object *obj = AddObject(OBJ_BARREL, pos);
gObjectPool.allocate();  // Don't do this!
```

**Why**: The legacy `AddObject()` currently manages its own allocation. The pool is synchronized separately.

### ❌ Don't Assume Pool is Always Synchronized
```cpp
// WRONG: Treating pool as the source of truth
if (gObjectPool.activeCount() != ActiveObjectCount) {
    // This might actually happen during transition
}
```

**Why**: During the migration period, both systems coexist. Use the API that your code currently uses.

### ❌ Don't Store Raw Pointers to Pool Data
```cpp
// WRONG: Storing direct pointers
Object *ptr = &gObjectPool[10];
DoSomething(ptr);
// Later...
gObjectPool.deallocate(10);  // Invalidates ptr!
```

**Why**: Use indices, not pointers, for persistence.

## Phase 2: Full Integration (Future)

Once all hot paths have been converted to use the pool iterator:

1. **Internalize allocation**: Move `AddObject()` to use pool.allocate()
2. **Synchronize globals**: Update `ActiveObjectCount` from pool.activeCount()
3. **Redirect legacy arrays**: Make `Objects[]` return pool elements
4. **Deprecate free functions**: Mark legacy functions for removal

This phased approach means no breaking changes during migration.

## Performance Characteristics

### Memory
- **Pool**: 127 Objects + 127 indices + 1 count = same as legacy
- **No extra overhead**: Identical memory footprint

### Speed
- **Iteration**: O(n) where n = active object count (same as legacy)
- **Random access**: O(1) (same as legacy)
- **Allocation**: O(1) amortized (same as legacy)
- **Deallocation**: O(n) for sparse policy (may be slower than legacy)

*Note*: Sparse allocation trades deallocation speed for stable indices (which we need).

## Testing Your Changes

After converting a function to use the pool:

1. **Compile**: Build should succeed
2. **Unit test**: If you have tests, run them
3. **Functional test**: Load a level with objects and interact with them
4. **Save/load**: Load a legacy save file and make sure objects are still there
5. **Multiplayer** (if applicable): Test network object sync

## Questions?

See also:
- `docs/ENTITY_POOL_ANSWERS.md` - Q&A about design decisions
- `Source/utils/entity_pool.hpp` - Full pool API documentation
- `Source/object_pool.h` - Object-specific pool wrapper

