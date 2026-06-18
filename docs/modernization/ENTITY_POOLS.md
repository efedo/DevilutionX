# Entity Pools

## Overview

Legacy globals for Objects and Items are now backed by pool storage owned in the
corresponding `*_pool.cpp` files.

### Object Pool

`Objects[]`, `ActiveObjects[]`, `AvailableObjects[]`, and `ActiveObjectCount` are
pointer/reference aliases into `gObjectPool` (`DenseEntityPool<Object, MAXOBJECTS, SparseAllocationPolicy>`).

**Sparse allocation is required** because objects are referenced by slot index in the `dObject`
tile map and in the binary save format. Indices must remain stable for the lifetime of an object.

### Item Pool

`Items[]` is a pointer alias into `gItemPool` (`DenseEntityPool<Item, MAXITEMS+1, SparseAllocationPolicy>`).
`ActiveItems[]` and `ActiveItemCount` are separate `uint8_t` globals in `item_pool.cpp` (not pool-internal)
because the pool's index storage uses `int`, which cannot be aliased to `uint8_t`.

The extra slot at `Items[MAXITEMS]` is a scratch buffer for item generation routines; the pool
capacity is `MAXITEMS+1` to preserve it at that exact index.

## Files

| File | Role |
|---|---|
| `Source/utils/entity_pool.hpp` | Generic `DenseEntityPool<T>` template |
| `Source/object_pool.h` | `ObjectPool` typedef, `ObjectPoolAdapter` namespace |
| `Source/object_pool.cpp` | `gObjectPool` definition; Object alias bindings |
| `Source/item_pool.h` | `ItemPool` typedef, `ItemPoolAdapter` namespace |
| `Source/item_pool.cpp` | `gItemPool` definition; `Items` alias + `ActiveItems`/`ActiveItemCount` storage |
| `Source/monster_pool.h` | `MonsterPool` typedef, `MonsterPoolAdapter` namespace |
| `Source/monster_pool.cpp` | `gMonsterPool` definition; `Monsters` alias + `ActiveMonsters`/`ActiveMonsterCount` storage |

### Monster Pool

`Monsters[]` is a pointer alias into `gMonsterPool` (`DenseEntityPool<Monster, MaxMonsters, SparseAllocationPolicy>`).
`ActiveMonsters[MaxMonsters]` and `ActiveMonsterCount` are separate owned globals in `monster_pool.cpp`
(the pool's index storage uses `int`; `unsigned`/`size_t` cannot be aliased from it).

Sparse allocation is required because monsters are referenced by slot index in the `dMonster` tile map
and in network sync messages.

The full `ActiveMonsters[MaxMonsters]` array (all slots) is serialized to the save file, not just the
active count. `MonsterPoolAdapter::ActiveMonsterIds()` returns a `std::span<unsigned, MaxMonsters>`
covering the entire array for use in serialization loops.

## Extending to Other Entity Types

Add a `using FooPool = DenseEntityPool<Foo, MAXFOOS, ...Policy>` and bind the legacy globals
in `foo_pool.cpp`. Use `SparseAllocationPolicy` when slot indices must be stable (referenced
by index in tile maps or save data). Use `DenseAllocationPolicy` when indices may be unstable.

## Common Operations — Objects

```cpp
// Iterate active objects
for (Object &obj : ObjectPoolAdapter::ActiveObjectsRange()) { ... }

// Allocate a slot
int idx = ObjectPoolAdapter::ReserveObjectSlot();   // -1 if full
// ... initialize Objects[idx] ...
ObjectPoolAdapter::CommitReservedObjectSlot();

// Release a slot (activeListIndex = position in ActiveObjects[])
ObjectPoolAdapter::ReleaseObjectSlot(objectIndex, activeListIndex);

// Reset all objects (new level)
ObjectPoolAdapter::ResetLegacyObjectPools();
```

## Common Operations — Items

```cpp
// Iterate active items
for (Item &item : ItemPoolAdapter::ActiveItemsRange()) { ... }

// Allocate: existing AllocateItem() / PlaceItemInWorld() handle this directly
// Delete:   existing DeleteItem(i) handles this directly

// Reset all items (new level) — InitItems() calls this implicitly via the aliases
ItemPoolAdapter::ResetLegacyItemPools();
```

## Save/Load Compatibility

The binary save format is preserved because the storage pointers alias directly into the
respective pool's `data()`. Serialization code that reads or writes these arrays produces
an identical byte stream to the original owning-array layout.
