/**
 * ItemPool implementation: owns Item storage and legacy pool aliases.
 *
 * gItemPool owns the Item array. Items is a pointer alias into pool memory.
 * ActiveItems and ActiveItemCount are separate uint8_t globals (not backed by
 * pool internals) because the pool's internal index storage uses int, which
 * cannot be aliased to uint8_t.
 */
#include "item_pool.h"

#include "items.h"

namespace devilution {

// Global pool instance — owns all Item storage
ItemPool gItemPool;

// Legacy pointer alias: Items[i] == gItemPool[i] (same memory)
Item *Items = gItemPool.data();

// Active/available index bookkeeping (uint8_t, separate from pool internals)
uint8_t ActiveItems[MAXITEMS];
uint8_t ActiveItemCount;

void InitializeItemPool()
{
	gItemPool.clear();
	ItemPoolAdapter::ResetLegacyItemPools();
}

} // namespace devilution
