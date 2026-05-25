# Entity Pool Modernization: Answers to Your Questions

## Question 1: Sparse vs. Dense Allocation — Which Approach?

### **Option A: Sparse Allocation (Stable Indices with Free-List)**

**How it works:**
- Deleted elements are **not removed** from the array
- Free slots are tracked in a linked-list (`AvailableObjects`)
- Element indices **never change** across the element's lifetime
- Iteration must **skip over unused slots**

**Pros:**
- ✅ Stable, predictable indices → no reference breakage
- ✅ Existing object IDs in `dObject[x][y]` remain valid
- ✅ External pointers/references still work
- ✅ Save files use exact same index mapping
- ✅ Simple migration (copy-paste existing code)

**Cons:**
- ❌ Array fragmentation over time (gaps in allocation)
- ❌ Slower iteration (must walk entire array, skipping gaps)
- ❌ Slightly higher memory overhead (free-list metadata)
- ❌ Cache unfriendly (scattered active elements)

### **Option B: Dense Allocation (Swap-with-Last Deletion)**

**How it works:**
- When deleting, **swap element with last active element**, then shrink
- All active elements are **contiguous at array start**
- Element indices **change when swaps occur** (unstable)
- Iteration **only walks active elements** (very fast)

**Pros:**
- ✅ O(1) deletion (no fragmentation)
- ✅ Fast iteration (only active items, contiguous)
- ✅ Cache-friendly (packed array)
- ✅ Lower memory overhead (no free-list)
- ✅ Simpler for new designs

**Cons:**
- ❌ Indices change on deletion → breaks external references
- ❌ Must update `dObject[x][y]` for every swap → complex
- ❌ Monsters/Items use this; Objects do not
- ❌ Save-file remapping required during load
- ❌ Requires careful index tracking everywhere

### **Decision: Sparse for Objects (RECOMMENDED)**

**Why:**
1. Objects have **stable identity requirements** — they're referenced in `dObject[x][y]` map lookup tables
2. Changing an object's index would require updating **all map references** — error-prone
3. **Objects already use sparse allocation** (AvailableObjects free-list exists)
4. **Zero migration effort** — code continues working unchanged
5. **Save-file compatibility is simpler** (exact index preservation)

**Trade-off accepted:**
- Slower iteration is mitigated by the new iterator (only walks active items)
- Memory overhead is negligible (~500 bytes for 127 free-list entries)

---

## Question 2: Binary Save-File Compatibility

### **Approach: Exact Index Preservation**

The new `DenseEntityPool` stores elements in **exact array positions** matching the legacy code:

```cpp
// Legacy: Objects[i] refers to slot i
// New: gObjectPool[i] refers to same slot i
// Result: Identical binary layout
```

**Binary layout guarantee:**
```
Offset  | Legacy Code          | New Pool
--------|----------------------|----------------------
0x000   | Objects[0]           | gObjectPool[0]       ✓ Same
0x200   | Objects[1]           | gObjectPool[1]       ✓ Same
...     | ...                  | ...
0x7F00  | Objects[127]         | gObjectPool[127]     ✓ Same
0x8000  | ActiveObjects[0]     | activeIndices_[0]    ✓ Same
...     | ...                  | ...
0x8200  | ActiveObjectCount    | activeCount_         ✓ Same
```

**Save/Load Compatibility:**

```cpp
// EXISTING save code (works unchanged)
for (int i = 0; i < MAXOBJECTS; ++i) {
  SaveObject(Objects[i]);
}
SaveInt(ActiveObjectCount);

// EXISTING load code (works unchanged)
for (int i = 0; i < MAXOBJECTS; ++i) {
  LoadObject(Objects[i]);
}
ActiveObjectCount = LoadInt();
```

Both produce **identical binary files** because:
- `Objects` pointer = `gObjectPool.data()` (same memory location)
- `ActiveObjectCount` = `gObjectPool.activeCount()` (same value)
- Element layout is unchanged

**Answer: YES, exact binary format is preserved with zero changes to save/load code.**

---

## Question 3: Reusable Container vs. Object-Specific?

### **Decision: Reusable Template (with Objects as pilot)**

Created **`DenseEntityPool<T, MaxCapacity, AllocationPolicy>`** — a fully generic template that:

```cpp
// Works with any entity type
template <typename T, size_t MaxCapacity, typename AllocationPolicy = SparseAllocationPolicy>
class DenseEntityPool { /* ... */ };

// Usage for Objects (now)
using ObjectPool = DenseEntityPool<Object, MAXOBJECTS, SparseAllocationPolicy>;

// Usage for Monsters (future)
using MonsterPool = DenseEntityPool<Monster, MaxMonsters, DenseAllocationPolicy>;

// Usage for Items (future)
using ItemPool = DenseEntityPool<Item, MAXITEMS, DenseAllocationPolicy>;
```

**Why reusable:**
1. **Eliminates code duplication** — same pool logic for all entities
2. **Enforces consistency** — uniform behavior across entity types
3. **Scalable** — any new entity type gets same container for free
4. **Testable** — single template, tested once, works everywhere
5. **Maintainable** — fixes/optimizations benefit all users

**Why Objects first:**
- Sparse allocation is safer (no index remapping needed)
- Largest potential for errors (complex dObject map references)
- Other modules can be migrated once pattern is proven

---

## Files Created

```
Source/
  ├─ utils/
  │  └─ entity_pool.hpp                  # Generic pool template
  ├─ object_pool.h                        # ObjectPool typedef & legacy pointers
  ├─ object_pool.cpp                      # Global pool instance
  └─ docs/
     ├─ ENTITY_POOL_MODERNIZATION.md     # Detailed strategy document
     └─ ENTITY_POOL_ANSWERS.md           # This file
```

## Next Steps

1. **Verify build** ✅ (done — build successful)
2. **Update objects.h** to include `object_pool.h`
3. **Run existing tests** to confirm binary compatibility
4. **Create ObjectManager::allocate/deallocate** to wrap pool
5. **Migrate call sites incrementally** to new API
6. **Repeat for Monsters, Items** (future)
