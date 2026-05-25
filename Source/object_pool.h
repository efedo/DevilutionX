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

#include <cstring>
#include <iterator>
#include <span>

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

namespace ObjectPoolAdapter {

class ActiveObjectIterator {
public:
	using iterator_category = std::forward_iterator_tag;
	using value_type = Object;
	using difference_type = int;
	using pointer = Object *;
	using reference = Object &;

	explicit ActiveObjectIterator(int activeIndex)
		: activeIndex_(activeIndex)
	{
	}

	reference operator*() const
	{
		return Objects[ActiveObjects[activeIndex_]];
	}

	pointer operator->() const
	{
		return &Objects[ActiveObjects[activeIndex_]];
	}

	ActiveObjectIterator &operator++()
	{
		++activeIndex_;
		return *this;
	}

	bool operator==(const ActiveObjectIterator &other) const
	{
		return activeIndex_ == other.activeIndex_;
	}

	bool operator!=(const ActiveObjectIterator &other) const
	{
		return !(*this == other);
	}

private:
	int activeIndex_;
};

class ActiveObjectRange {
public:
	ActiveObjectIterator begin() const
	{
		return ActiveObjectIterator(0);
	}

	ActiveObjectIterator end() const
	{
		return ActiveObjectIterator(ActiveObjectCount);
	}
};

[[nodiscard]] inline ActiveObjectRange ActiveObjectsRange()
{
	return ActiveObjectRange {};
}

[[nodiscard]] inline bool HasFreeObjectSlot()
{
	return ActiveObjectCount < MAXOBJECTS;
}

[[nodiscard]] inline int ActiveObjectCountValue()
{
	return ActiveObjectCount;
}

inline void SetActiveObjectCountValue(int count)
{
	ActiveObjectCount = count;
}

[[nodiscard]] inline int PeekNextAvailableObjectIndex()
{
	return AvailableObjects[0];
}

[[nodiscard]] inline int ReserveObjectSlot()
{
	if (!HasFreeObjectSlot()) {
		return -1;
	}

	const int objectIndex = AvailableObjects[0];
	AvailableObjects[0] = AvailableObjects[MAXOBJECTS - 1 - ActiveObjectCount];
	ActiveObjects[ActiveObjectCount] = objectIndex;
	return objectIndex;
}

inline void CommitReservedObjectSlot()
{
	++ActiveObjectCount;
}

[[nodiscard]] inline int ActiveObjectIndexAt(int activeIndex)
{
	return ActiveObjects[activeIndex];
}

[[nodiscard]] inline std::span<int, MAXOBJECTS> ActiveObjectIds()
{
	return std::span<int, MAXOBJECTS>(ActiveObjects);
}

[[nodiscard]] inline std::span<int, MAXOBJECTS> AvailableObjectIds()
{
	return std::span<int, MAXOBJECTS>(AvailableObjects);
}

[[nodiscard]] inline Object &ActiveObjectAt(int activeIndex)
{
	return Objects[ActiveObjectIndexAt(activeIndex)];
}

inline void ResetLegacyObjectPools()
{
	for (int i = 0; i < MAXOBJECTS; i++) {
		AvailableObjects[i] = i;
	}
	ActiveObjectCount = 0;
	memset(ActiveObjects, 0, sizeof(ActiveObjects));
}

inline void ReleaseObjectSlot(int objectIndex, int activeListIndex)
{
	AvailableObjects[MAXOBJECTS - ActiveObjectCount] = objectIndex;
	--ActiveObjectCount;
	if (ActiveObjectCount > 0 && activeListIndex != ActiveObjectCount) {
		ActiveObjects[activeListIndex] = ActiveObjects[ActiveObjectCount];
	}
}

} // namespace ObjectPoolAdapter

} // namespace devilution
