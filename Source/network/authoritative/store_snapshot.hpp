#pragma once

/**
 * @file network/authoritative/store_snapshot.hpp
 *
 * Native projection of authoritative vendor-stock snapshots.
 */

#include <cstdint>
#include <string>
#include <vector>

#include <expected.hpp>

#include "game/items/items.hpp"

namespace devilution::protocol::v1 {
class Snapshot;
}

namespace devilution::authoritative {

struct ProjectedStoreItem {
	uint32_t storeSlot = 0;
	uint32_t itemSeed = 0;
	uint32_t price = 0;
	Item item;
};

struct ProjectedStoreSnapshot {
	uint32_t storeId = 0;
	std::vector<ProjectedStoreItem> items;
};

/** Converts wire vendor stock into a protocol-independent native projection. */
[[nodiscard]] tl::expected<ProjectedStoreSnapshot, std::string> ProjectStoreSnapshot(const protocol::v1::Snapshot &snapshot);

} // namespace devilution::authoritative
