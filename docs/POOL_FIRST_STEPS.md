# First Steps: Starting the ObjectPool Migration

This guide outlines the safest, most practical first steps to start using the modern ObjectPool in production code.

## Phase 1: Read-Only Iteration (Low Risk)

### Objective
Replace manual active-object iteration loops with pool iteration.

### Why Start Here?
- ✅ No allocation/deallocation changes
- ✅ No state modifications
- ✅ Easy to verify correctness (output should be identical)
- ✅ Clear performance wins (fewer indirections)
- ✅ Can be reverted easily if needed

### Candidates in objects.cpp

#### 1. ClrAllObjects() - Zero all objects

**Current**:
```cpp
void ClrAllObjects()
{
  for (int i = 0; i < MAXOBJECTS; i++) {
    Objects[i]._otype = OBJ_NULL;
  }
  ActiveObjectCount = 0;
  // ...
}
```

**Proposal**: Convert to use pool (if pool is synchronized)

### 2. ProcessObjects() - Main game loop

**Current**:
```cpp
void ProcessObjects()
{
  for (int i = 0; i < ActiveObjectCount; ++i) {
    Object &object = Objects[ActiveObjects[i]];
    UpdateTrapState(object);
    // ... more processing
  }
}
```

**Improved**:
```cpp
void ProcessObjects()
{
  for (Object &object : gObjectPool) {
    UpdateTrapState(object);
    // ... more processing
  }
}
```

**Risk Level**: 🟢 Very Low
- No allocation/deallocation
- Output should be bitwise identical
- Easy to test: compare frame updates

#### 3. Rendering Loop - Render all objects

**Current**: Renders via ObjectAtPosition() which uses legacy arrays

**Improved**: Could use pool iterator if we separate rendering concerns

**Risk Level**: 🟡 Medium
- Rendering order matters
- Needs visual verification

### 4. FindObjectAtPosition() - Single object lookup

**Current**: Linear search through ActiveObjects

**Could use**: Simple O(1) lookup if we maintain a spatial hash (future optimization)

**Risk Level**: 🟡 Medium
- Needs performance benchmarking

## Phase 2: Optional Pool Synchronization

### What Needs to Happen
When `AddObject()` or object deletion occurs, the pool should be updated.

```cpp
// In AddObject():
Object *pObject = ObjectPoolAdapter::Allocate();  // Future helper
if (pObject == nullptr) return nullptr;

// In object deletion:
ObjectPoolAdapter::Deallocate(objectIndex);
```

This would require:
- Modifying `AddObject()` to also call pool allocation
- Modifying object deletion to also deallocate from pool
- Updating `ActiveObjectCount` from pool

### Impact
- ✅ Pool stays in sync with legacy arrays
- ❌ Slightly more code in allocation paths
- ❌ Deallocation performance may degrade (sparse policy)

## Concrete First Step Recommendation

### Start with: ProcessObjects() Loop Conversion

**Why This One First?**
1. Already a hot path (runs every game tick)
2. Simple mechanical change
3. Safe to revert
4. Performance benefits are measurable
5. Doesn't require pool synchronization changes

**Detailed Steps**:

1. **Open**: `Source/objects.cpp`, find `ProcessObjects()` (around line 4050)

2. **Add include** (if not already present):
   ```cpp
   #include "object_pool.h"
   ```

3. **Find the loop**:
   ```cpp
   for (int i = 0; i < ActiveObjectCount; ++i) {
       Object &object = Objects[ActiveObjects[i]];
   ```

4. **Replace with pool iteration**:
   ```cpp
   for (Object &object : gObjectPool) {
   ```

5. **Compile and test**:
   ```bash
   cmake --build build --config Debug
   ```

6. **Functional test**:
   - Load a level with objects (chests, barrels, doors, etc.)
   - Interact with them
   - Verify they behave identically
   - Save and load the game

7. **Verify output**:
   ```cpp
   // Debug output to verify we're processing the same objects
   int legacyCount = 0;
   for (int i = 0; i < ActiveObjectCount; ++i) {
       legacyCount++;
   }
   int poolCount = 0;
   for (Object &obj : gObjectPool) {
       poolCount++;
   }
   assert(legacyCount == poolCount);  // Should always match
   ```

## Measuring Success

After converting ProcessObjects():

### Code Quality Metrics
- ✅ Lines of code reduced (fewer indirections)
- ✅ Cyclomatic complexity reduced (simpler loop)
- ✅ No behavior changes (identical output)

### Performance Metrics
- ✅ Fewer memory indirections per object
- ✅ Better cache locality (active objects are contiguous in pool)
- ✅ Compiler can generate better code

### Safety Metrics
- ✅ No segfaults (indices stay stable)
- ✅ No data corruption (read-only operation)
- ✅ Easy to revert if needed

## Next Steps After ProcessObjects()

Once comfortable with the pattern:

1. **FindObjectAtPosition()** - Replace linear search
2. **Rendering loops** - Replace ObjectAtPosition() calls
3. **Monster door interaction** - MonstCheckDoors()
4. **Other utility functions** - Any function that iterates objects

Then, when many functions use the pool:

5. **Synchronize allocations** - Update pool when objects are created
6. **Track deletions** - Update pool when objects are destroyed
7. **Verify save/load** - Ensure compatibility with legacy files

## Rollback Plan

If issues arise:

1. **Revert the code change**: Git revert the commit
2. **Rebuild**: Should compile successfully
3. **Re-test**: Verify behavior returns to baseline
4. **Post-mortem**: Understand what went wrong

The beauty of this approach: you can always revert individual functions without affecting others.

## Expected Outcomes

### Immediate (First Iteration)
- ✅ ProcessObjects() uses pool iterator
- ✅ Build succeeds
- ✅ Game runs identically
- ✅ No save-file issues

### Short Term (Next Month)
- ✅ 3-5 functions converted to pool iteration
- ✅ Code becomes simpler and more readable
- ✅ Small performance improvements accumulate

### Medium Term (Next Quarter)
- ✅ Most object iteration uses pool
- ✅ Consider synchronizing allocations
- ✅ Begin planning Phase 2 (full integration)

### Long Term (Next Year)
- ✅ Pool is the primary container
- ✅ Legacy arrays become optional compatibility layer
- ✅ Codebase is fully modernized

## Questions & Answers

**Q: Will this break multiplayer?**
A: No. The pool iteration order should be identical to legacy order (sparse allocation maintains indices).

**Q: What about save compatibility?**
A: No issues. We're only changing iteration, not serialization. Save files work as-is.

**Q: Can I mix pool and legacy code?**
A: Yes! Both work simultaneously. Use whichever fits your code better.

**Q: What if pool and legacy arrays get out of sync?**
A: During Phase 1, they won't. Phase 2 will ensure sync. Phase 1 is purely read-only.

## Resources

- **Pool API**: `Source/utils/entity_pool.hpp`
- **Migration Guide**: `docs/POOL_MIGRATION_GUIDE.md`
- **Design Rationale**: `docs/ENTITY_POOL_MODERNIZATION.md`
- **Q&A**: `docs/ENTITY_POOL_ANSWERS.md`

