#pragma once

/**
 * @file network/authoritative/player_snapshot.hpp
 *
 * Protocol-free native projection of authoritative player state.
 */

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include <expected.hpp>

#include "game/items/items.hpp"

namespace devilution::protocol::v1 {
class Snapshot;
}

namespace devilution::authoritative {

struct ProjectedPlayerAttribute {
	int32_t base = 0;
	int32_t current = 0;
};

struct ProjectedInventoryItem {
	uint32_t storeId = 0;
	uint32_t storeSlot = 0;
	uint32_t itemSeed = 0;
	uint32_t price = 0;
	uint64_t purchasedAtTick = 0;
	Item item;
};

struct ProjectedEquippedItem {
	uint32_t slot = 0;
	uint32_t itemSeed = 0;
	Item item;
};

struct ProjectedPlayerSnapshot {
	uint32_t entityId = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
	int32_t life = 0;
	int32_t mana = 0;
	uint32_t gold = 0;
	uint32_t experience = 0;
	ProjectedPlayerAttribute strength;
	ProjectedPlayerAttribute magic;
	ProjectedPlayerAttribute dexterity;
	ProjectedPlayerAttribute vitality;
	std::optional<uint32_t> activeStoreId;
	std::vector<ProjectedInventoryItem> inventory;
	std::vector<ProjectedEquippedItem> equipment;
	std::vector<int32_t> inventoryGrid;
};

/** Projects exactly one entity's player state from a server snapshot. */
[[nodiscard]] tl::expected<ProjectedPlayerSnapshot, std::string> ProjectPlayerSnapshot(
	const ::devilution::protocol::v1::Snapshot &snapshot,
	uint32_t entityId);

/** Reconnect-safe owner of the latest validated authoritative player state. */
class ServerBackedPlayerState {
public:
	[[nodiscard]] bool ApplySnapshot(ProjectedPlayerSnapshot snapshot);
	void Clear() noexcept;

	[[nodiscard]] bool HasSnapshot() const noexcept { return snapshot_.has_value(); }
	[[nodiscard]] const ProjectedPlayerSnapshot *Snapshot() const noexcept { return snapshot_ ? &*snapshot_ : nullptr; }
	[[nodiscard]] const ProjectedInventoryItem *FindInventoryItem(std::size_t index) const noexcept;

private:
	std::optional<ProjectedPlayerSnapshot> snapshot_;
};

} // namespace devilution::authoritative
