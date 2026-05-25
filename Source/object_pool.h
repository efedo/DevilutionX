/**
 * ObjectPool: Modernized container for object lifecycle management.
 *
 * Replaces the legacy Objects[]/AvailableObjects[]/ActiveObjects[] pattern
 * with a type-safe, STL-compatible container while maintaining binary-compatible
 * serialization and identical runtime behavior.
 *
 * MIGRATION STRATEGY:
 * 1. Maintain legacy global pointers (Objects, AvailableObjects, ActiveObjects)
 *    that point into the pool's internal storage.
 * 2. Maintain a reference to the pool's activeCount for ActiveObjectCount.
 * 3. Gradually migrate call sites from direct array access to pool methods.
 * 4. When save/load code changes, index mapping ensures backward compatibility.
 */
#pragma once

#include <cstdint>

#include "utils/entity_pool.hpp"

namespace devilution {

struct Object;

// Legacy capacity constant
static constexpr int MAXOBJECTS = 127;

// Pool type: Sparse allocation maintains stable indices across lifetime
using ObjectPool = DenseEntityPool<Object, MAXOBJECTS, SparseAllocationPolicy>;

// Global pool instance (hidden implementation detail)
extern ObjectPool gObjectPool;

// Legacy pointers: maintained for binary compatibility and gradual migration
// Points directly into the pool's storage.
extern Object *Objects;           // gObjectPool.data()
extern int *AvailableObjects;     // Tracks free slots (sparse only)
extern int *ActiveObjects;        // gObjectPool.activeIndices()

// Reference to active count (for convenient int usage)
extern int &ActiveObjectCount;    // Reference to gObjectPool.activeCount()

} // namespace devilution
