# Object Pool Migration — Session Notes

## What Was Done

1. Created `Source/utils/entity_pool.hpp` — generic `DenseEntityPool<T, MaxCapacity, Policy>`.
2. Created `Source/object_pool.h` / `Source/object_pool.cpp`:
   - `object_pool.cpp` owns `gObjectPool` and binds the legacy globals to pool storage:
     ```cpp
     Object *Objects            = gObjectPool.data();
     int    *AvailableObjects   = gObjectPool.availableIndices();
     int    *ActiveObjects      = gObjectPool.activeIndices();
     int    &ActiveObjectCount  = gObjectPool.activeCountRef();
     ```
   - `object_pool.h` exposes `ObjectPoolAdapter` helpers (range iteration, slot management, count/span accessors).
3. Migrated active-object loops in `objects.cpp`, `items.cpp`, `msg.cpp`, `player.cpp`, `quests.cpp` to `ActiveObjectsRange()`.
4. Migrated `loadsave.cpp` serialization/deserialization to use adapter spans and count accessors.
5. Removed owning array definitions from `objects.cpp`; changed `objects.h` declarations to pointer/reference aliases.
6. Build confirmed successful.

## Key Pitfalls Encountered

- **Duplicate overloads differing only by return type** (`const`/non-`const` span): illegal in C++; remove the const version.
- **`std::span<int, N>` from a pointer**: must pass pointer + extent explicitly:
  `std::span<int, MAXOBJECTS>(ActiveObjects, MAXOBJECTS)`, not `std::span<int, MAXOBJECTS>(ActiveObjects)`.
- **`ActiveObjectCount` as a reference**: all code that takes `int&` or `auto` must still compile;
  passing it by value to legacy APIs is fine because it dereferences correctly.

## Save Format Notes

`loadsave.cpp` serializes the active-object count, then the full `ActiveObjects[MAXOBJECTS]` table,
then the full `AvailableObjects[MAXOBJECTS]` table in that order. The alias approach preserves this
exactly because the pointers reference the same memory the pool owns.

## Future Migration Targets

- Monster pool: `Monsters[]`, `ActiveMonsters[]`, `ActiveMonsterCount` in `monster.h/cpp`.
  - Use `SparseAllocationPolicy` (tile map references via `dMonster[]`).
- ~~Item pool~~ — **Done.** See `Source/item_pool.h` and `Source/item_pool.cpp`.
