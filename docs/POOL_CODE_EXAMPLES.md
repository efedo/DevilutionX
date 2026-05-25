# ObjectPool Migration Examples

This document provides concrete, copy-paste-ready examples of converting real code to use the modern ObjectPool.

## Example 1: Simple Iteration Loop

### Before (Legacy)
```cpp
// From objects.cpp - typical pattern
void SomeObjectFunction()
{
    for (int i = 0; i < ActiveObjectCount; ++i) {
        Object &object = Objects[ActiveObjects[i]];

        // Do something with object
        if (object._otype == OBJ_NULL) {
            continue;
        }

        DoSomething(object);
    }
}
```

### After (Modern)
```cpp
void SomeObjectFunction()
{
    for (Object &object : gObjectPool) {
        // Do something with object
        if (object._otype == OBJ_NULL) {
            continue;
        }

        DoSomething(object);
    }
}
```

### Benefits
- ❌ No more `ActiveObjects[i]` indirection
- ✅ Clearer intent: "for each active object"
- ✅ Fewer variables to track
- ✅ Compiler optimization friendly

### Verification
```cpp
// Both produce identical results:
std::vector<Object*> legacyObjects, poolObjects;

// Legacy way
for (int i = 0; i < ActiveObjectCount; ++i) {
    legacyObjects.push_back(&Objects[ActiveObjects[i]]);
}

// Pool way
for (Object &obj : gObjectPool) {
    poolObjects.push_back(&obj);
}

// Should be identical
assert(legacyObjects.size() == poolObjects.size());
for (size_t i = 0; i < legacyObjects.size(); ++i) {
    assert(legacyObjects[i] == poolObjects[i]);
}
```

---

## Example 2: Processing with Early Exit

### Before (Legacy)
```cpp
bool FindSpecialObject()
{
    for (int i = 0; i < ActiveObjectCount; ++i) {
        Object &object = Objects[ActiveObjects[i]];

        if (object._otype == OBJ_SHRINE) {
            ProcessShrine(object);
            return true;  // Found it!
        }
    }
    return false;  // Not found
}
```

### After (Modern)
```cpp
bool FindSpecialObject()
{
    for (Object &object : gObjectPool) {
        if (object._otype == OBJ_SHRINE) {
            ProcessShrine(object);
            return true;  // Found it!
        }
    }
    return false;  // Not found
}
```

### Notes
- Early exit (return) works identically in both
- Pool iterator is efficient even with early exit
- No performance degradation

---

## Example 3: Nested Iteration

### Before (Legacy)
```cpp
void CheckAllObjectsAgainstAllMonsters()
{
    for (int i = 0; i < ActiveObjectCount; ++i) {
        Object &object = Objects[ActiveObjects[i]];

        // Inner loop (from monsters)
        for (int j = 0; j < ActiveMonsterCount; ++j) {
            Monster &monster = Monsters[ActiveMonsters[j]];

            if (CheckCollision(object, monster)) {
                HandleCollision(object, monster);
            }
        }
    }
}
```

### After (Modern)
```cpp
void CheckAllObjectsAgainstAllMonsters()
{
    for (Object &object : gObjectPool) {
        // Inner loop (from monsters)
        // Stays the same for now (monsters aren't converted yet)
        for (int j = 0; j < ActiveMonsterCount; ++j) {
            Monster &monster = Monsters[ActiveMonsters[j]];

            if (CheckCollision(object, monster)) {
                HandleCollision(object, monster);
            }
        }
    }
}
```

### Notes
- Outer loop uses pool
- Inner loop stays legacy (not yet converted)
- Both styles can coexist safely
- No nesting issues

---

## Example 4: Counting Active Objects

### Before (Legacy)
```cpp
int CountBarrels()
{
    int count = 0;
    for (int i = 0; i < ActiveObjectCount; ++i) {
        Object &object = Objects[ActiveObjects[i]];
        if (object.IsBarrel()) {
            count++;
        }
    }
    return count;
}
```

### After (Modern)
```cpp
int CountBarrels()
{
    int count = 0;
    for (Object &object : gObjectPool) {
        if (object.IsBarrel()) {
            count++;
        }
    }
    return count;
}
```

### Or Even Better (C++ Idiom)
```cpp
int CountBarrels()
{
    return std::count_if(gObjectPool.begin(), gObjectPool.end(),
        [](const Object &object) { return object.IsBarrel(); });
}
```

---

## Example 5: Direct Index Access

### Before (Legacy)
```cpp
void UpdateObjectAtIndex(int objectIndex)
{
    if (objectIndex < 0 || objectIndex >= MAXOBJECTS) {
        return;
    }
    Object &object = Objects[objectIndex];
    ProcessObject(object);
}
```

### After (Modern)
Both still work! Choose based on context:

**Option A**: Keep as-is (Objects[] still works)
```cpp
void UpdateObjectAtIndex(int objectIndex)
{
    if (objectIndex < 0 || objectIndex >= MAXOBJECTS) {
        return;
    }
    Object &object = Objects[objectIndex];  // Still valid!
    ProcessObject(object);
}
```

**Option B**: Use pool (when you want to be explicit)
```cpp
void UpdateObjectAtIndex(int objectIndex)
{
    if (objectIndex < 0 || objectIndex >= MAXOBJECTS) {
        return;
    }
    Object &object = gObjectPool[objectIndex];  // Pool version
    ProcessObject(object);
}
```

### Notes
- Both are equivalent
- `Objects[i]` and `gObjectPool[i]` access the same memory
- Use whichever matches your code style
- No performance difference

---

## Example 6: Counting Active Objects (Alternative)

### Before (Legacy)
```cpp
int GetActiveObjectCount()
{
    return ActiveObjectCount;
}
```

### After (Modern)
```cpp
int GetActiveObjectCount()
{
    return gObjectPool.activeCount();  // Modern way
    // OR stay as:
    return ActiveObjectCount;  // Still works!
}
```

### Notes
- Both are equivalent during Phase 1
- Recommended: use legacy for now (less refactoring)
- Switch to pool when you do Phase 2 (allocation sync)

---

## Example 7: Range-based for with Type Checking

### Before (Legacy)
```cpp
void ProcessTraps()
{
    for (int i = 0; i < ActiveObjectCount; ++i) {
        Object &object = Objects[ActiveObjects[i]];

        if (!object.IsTrap()) {
            continue;
        }

        UpdateTrapState(object);
    }
}
```

### After (Modern)
```cpp
void ProcessTraps()
{
    for (Object &object : gObjectPool) {
        if (!object.IsTrap()) {
            continue;
        }

        UpdateTrapState(object);
    }
}
```

### Or with STL Algorithm
```cpp
void ProcessTraps()
{
    for (Object &object : gObjectPool) {
        if (object.IsTrap()) {
            UpdateTrapState(object);
        }
    }
}
```

---

## Example 8: Conditional Filtering

### Before (Legacy)
```cpp
void ProcessObjectsInRange(int x1, int y1, int x2, int y2)
{
    for (int i = 0; i < ActiveObjectCount; ++i) {
        Object &object = Objects[ActiveObjects[i]];

        if (object.position.x < x1 || object.position.x >= x2 ||
            object.position.y < y1 || object.position.y >= y2) {
            continue;
        }

        ProcessObject(object);
    }
}
```

### After (Modern)
```cpp
void ProcessObjectsInRange(int x1, int y1, int x2, int y2)
{
    for (Object &object : gObjectPool) {
        if (object.position.x < x1 || object.position.x >= x2 ||
            object.position.y < y1 || object.position.y >= y2) {
            continue;
        }

        ProcessObject(object);
    }
}
```

### Notes
- Logic stays identical
- Just the outer loop changes
- Benefits from cleaner loop structure

---

## Example 9: Modifying Objects During Iteration

### Before (Legacy)
```cpp
void AnimateAllObjects()
{
    for (int i = 0; i < ActiveObjectCount; ++i) {
        Object &object = Objects[ActiveObjects[i]];

        // Modify the object
        object._oAnimCnt++;
        if (object._oAnimCnt > object._oAnimDelay) {
            object._oAnimFrame++;
            object._oAnimCnt = 0;
        }
    }
}
```

### After (Modern)
```cpp
void AnimateAllObjects()
{
    for (Object &object : gObjectPool) {
        // Modify the object
        object._oAnimCnt++;
        if (object._oAnimCnt > object._oAnimDelay) {
            object._oAnimFrame++;
            object._oAnimCnt = 0;
        }
    }
}
```

### Safety Notes
- ✅ Safe to modify object fields
- ✅ Safe to modify while iterating
- ❌ NOT safe to allocate/deallocate during iteration (Phase 2+)
- ✅ Phase 1 (read-only iteration): always safe

---

## Example 10: Converting a Complex Function

### Before (Legacy) - Real Example
```cpp
void ObjChangeMap(int x1, int y1, int x2, int y2)
{
    for (int i = 0; i < ActiveObjectCount; i++) {
        Object &object = Objects[ActiveObjects[i]];

        if (object.position.x >= x1 && 
            object.position.x < x2 &&
            object.position.y >= y1 && 
            object.position.y < y2) {

            switch (object._otype) {
                case OBJ_L1LDOOR:
                case OBJ_L1RDOOR:
                    SetDoor(i);
                    break;
                case OBJ_L2LDOOR:
                case OBJ_L2RDOOR:
                    SetDoor(i);
                    break;
                // ... more cases
            }
        }
    }
}
```

### After (Modern)
```cpp
void ObjChangeMap(int x1, int y1, int x2, int y2)
{
    for (Object &object : gObjectPool) {
        if (object.position.x >= x1 && 
            object.position.x < x2 &&
            object.position.y >= y1 && 
            object.position.y < y2) {

            switch (object._otype) {
                case OBJ_L1LDOOR:
                case OBJ_L1RDOOR:
                    SetDoor(object);  // Note: pass object, not index
                    break;
                case OBJ_L2LDOOR:
                case OBJ_L2RDOOR:
                    SetDoor(object);  // Note: pass object, not index
                    break;
                // ... more cases
            }
        }
    }
}
```

### Important Note
- In the pool version, pass `object` not `i` to helper functions
- Helper functions should accept `Object&` not `int` index
- This is clearer and type-safer

---

## Conversion Checklist

When converting a function to use the pool:

- [ ] Add `#include "object_pool.h"` if not already present
- [ ] Find the outer loop: `for (int i = 0; i < ActiveObjectCount; ++i)`
- [ ] Find the inner access: `Objects[ActiveObjects[i]]`
- [ ] Replace both with: `for (Object &obj : gObjectPool)`
- [ ] Update any variable names for clarity
- [ ] Don't change the logic, just the loop structure
- [ ] Compile and verify no warnings
- [ ] Test the function (load level, interact with objects)
- [ ] Commit and document the change

---

## Common Gotchas

### ❌ Gotcha 1: Allocating During Iteration
```cpp
// DON'T DO THIS (Phase 1)
for (Object &obj : gObjectPool) {
    if (ShouldSpawn(obj)) {
        AddObject(OBJ_BARREL, obj.position);  // Breaks iteration!
    }
}
```

**Why**: You can't modify the container while iterating.

**Solution**: Collect indices first, then allocate:
```cpp
std::vector<int> toSpawn;
for (Object &obj : gObjectPool) {
    if (ShouldSpawn(obj)) {
        toSpawn.push_back(obj.GetId());
    }
}
for (int id : toSpawn) {
    AddObject(OBJ_BARREL, Objects[id].position);
}
```

### ❌ Gotcha 2: Storing Pointers
```cpp
// DON'T DO THIS
Object *ptr = &gObjectPool[10];
// ... later ...
gObjectPool.deallocate(10);  // Invalidates ptr!
(*ptr).DoSomething();  // Crash!
```

**Why**: Pointers become invalid when pool changes.

**Solution**: Store indices, not pointers:
```cpp
int objectId = 10;
// ... later ...
gObjectPool.deallocate(objectId);
Object &obj = gObjectPool[objectId];  // Only if still alive
```

### ❌ Gotcha 3: Mixing Loop Types
```cpp
// DON'T DO THIS
for (Object &obj : gObjectPool) {
    // Now accessing via legacy array
    for (int j = 0; j < ActiveObjectCount; ++j) {
        // This could interfere with outer loop's internal state
    }
}
```

**Why**: Confusing and error-prone.

**Solution**: Make inner loop explicit:
```cpp
for (Object &obj : gObjectPool) {
    // Inner loop with clear variable names
    for (int j = 0; j < static_cast<int>(gObjectPool.activeCount()); ++j) {
        // ... safely iterate
    }
}
```

---

## Testing Your Conversion

After converting a function:

```cpp
// Add temporary debug code:
#ifdef DEBUG_OBJECT_POOL
std::vector<Object*> legacyList, poolList;

for (int i = 0; i < ActiveObjectCount; ++i) {
    legacyList.push_back(&Objects[ActiveObjects[i]]);
}
for (Object &obj : gObjectPool) {
    poolList.push_back(&obj);
}

if (legacyList.size() != poolList.size()) {
    LOG_ERROR("Object count mismatch!");
}
for (size_t i = 0; i < legacyList.size(); ++i) {
    if (legacyList[i] != poolList[i]) {
        LOG_ERROR("Object order mismatch at index {}", i);
    }
}
#endif
```

This verifies that the pool iteration produces identical results.

---

## Summary

Converting to the pool is straightforward:
1. Find `for (int i = 0; i < ActiveObjectCount; ++i)`
2. Find `Objects[ActiveObjects[i]]`
3. Replace both with `for (Object &obj : gObjectPool)`
4. Test and commit

Each conversion is a small, safe, incrementally-testable change.

