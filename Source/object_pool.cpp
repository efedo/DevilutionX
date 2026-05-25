/**
 * ObjectPool implementation: Modern container management.
 *
 * Provides a parallel pool structure that can be synchronized with legacy globals.
 * This allows gradual migration without requiring immediate changes to all code paths.
 * 
 * The pool uses sparse allocation to maintain stable indices, which is essential for:
 * - Binary save/load compatibility (object indices don't change)
 * - Network sync messages (indices are the entity identifiers)
 * - Persistent map references (dObject array uses object indices)
 */
#include "object_pool.h"

#include "objects.h"

namespace devilution {

// Global pool instance
ObjectPool gObjectPool;

void InitializeObjectPool()
{
	// Clear and reinitialize the pool
	// This sets up the free-list for sparse allocation
	gObjectPool.clear();
}

} // namespace devilution
