/**
 * ObjectPool implementation: Setup and initialization.
 */
#include "object_pool.h"

#include <utility>

namespace devilution {

// Global pool instance
ObjectPool gObjectPool;

// Legacy pointers: point to pool storage
Object *Objects = gObjectPool.data();
int *AvailableObjects = nullptr;  // Will be initialized below
int *ActiveObjects = reinterpret_cast<int *>(gObjectPool.activeIndices());

// Reference to active count
int &ActiveObjectCount = const_cast<int &>(reinterpret_cast<const int &>(gObjectPool.activeCount()));

// Initialize AvailableObjects free-list
namespace {
	int gAvailableObjects[MAXOBJECTS];

	void InitializeAvailableObjects()
	{
		for (int i = 0; i < MAXOBJECTS; ++i) {
			gAvailableObjects[i] = i + 1;
		}
		AvailableObjects = gAvailableObjects;
	}

	struct Initializer {
		Initializer() { InitializeAvailableObjects(); }
	};

	static Initializer gInit;
}

} // namespace devilution
