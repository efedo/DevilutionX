/**
 * ObjectPool: Modern container for efficient object management.
 *
 * Provides a type-safe, STL-compatible fixed-capacity pool using sparse allocation
 * to maintain stable object indices (required for save compatibility and network sync).
 *
 * This is provided as an opt-in modernization facility. Legacy code continues to use
 * Objects[]/AvailableObjects[]/ActiveObjects[]/ActiveObjectCount globals.
 * 
 * DESIGN NOTES:
 * - Sparse allocation: Object indices remain stable throughout their lifetime.
 *   This is essential for save file format, network sync, and map references.
 * - The pool can coexist with legacy arrays indefinitely.
 * - New hot-path code can use the pool directly; legacy code sees no difference.
 * - Both paradigms remain valid; they can even be used simultaneously in different parts of the code.
 * 
 * FUTURE MIGRATION PATH:
 * 1. Identify hot paths that iterate over objects frequently
 * 2. Add pool methods to those functions to parallelize reads
 * 3. Gradually replace array-based iteration with pool iteration
 * 4. Eventually, move internal implementation details to use pool storage directly
 */
#pragma once

#include "objects.h"  // For MAXOBJECTS definition
#include "utils/entity_pool.hpp"

namespace devilution {

struct Object;

// Modern pool type: Sparse allocation (stable indices across save/network)
using ObjectPool = DenseEntityPool<Object, MAXOBJECTS, SparseAllocationPolicy>;

// Global pool instance
extern ObjectPool gObjectPool;

// Initialization hook (called by objects.cpp during InitObjects)
void InitializeObjectPool();

} // namespace devilution
