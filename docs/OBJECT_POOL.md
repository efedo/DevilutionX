# Object Pool

## Overview

`Objects[]`, `ActiveObjects[]`, `AvailableObjects[]`, and `ActiveObjectCount` are legacy globals
backed by `gObjectPool` (`DenseEntityPool<Object, MAXOBJECTS, SparseAllocationPolicy>`).
The pool owns the storage; the globals are pointer/reference aliases into it.

**Sparse allocation is required** because objects are referenced by slot index in the `dObject`
tile map and in the binary save format. Indices must remain stable for the lifetime of an object.

## Files

| File | Role |
|---|---|
| `Source/utils/entity_pool.hpp` | Generic `DenseEntityPool<T>` template |
| `Source/object_pool.h` | `ObjectPool` typedef, `ObjectPoolAdapter` namespace |
| `Source/object_pool.cpp` | `gObjectPool` definition; legacy alias bindings |

## Extending to Other Entity Types

The pattern is identical. Add a `using FooPool = DenseEntityPool<Foo, MAXFOOS, ...Policy>` and
bind the legacy globals in the corresponding `foo_pool.cpp`. Use `SparseAllocationPolicy` when
slot indices must be stable (Objects, any entity referenced by index in maps or save data).
Use `DenseAllocationPolicy` when indices may be unstable (swap-with-last on removal).

## Common Operations

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

## Save/Load Compatibility

The binary save format is preserved because `Objects` points directly to `gObjectPool.data()`
and `ActiveObjects`/`AvailableObjects`/`ActiveObjectCount` alias into the same pool storage.
Serialization code that reads or writes these arrays produces an identical byte stream.
