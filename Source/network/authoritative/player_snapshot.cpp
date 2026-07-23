#include "network/authoritative/player_snapshot.hpp"

#include <utility>

#include "devilution.pb.h"
#include "network/authoritative/item_snapshot.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;

tl::expected<ProjectedPlayerSnapshot, std::string> ProjectPlayerSnapshot(const protocol::Snapshot &snapshot, uint32_t entityId)
{
	const protocol::PlayerSnapshot *player = nullptr;
	for (const auto &candidate : snapshot.players()) {
		if (candidate.entity_id() != entityId)
			continue;
		if (player != nullptr)
			return tl::make_unexpected("Server-backed snapshot contains duplicate player entity IDs.");
		player = &candidate;
	}
	if (player == nullptr)
		return tl::make_unexpected("Server-backed snapshot does not contain the requested player entity.");

	ProjectedPlayerSnapshot projected {
		.entityId = player->entity_id(),
		.positionX = player->position_x(),
		.positionY = player->position_y(),
		.life = player->life(),
		.mana = player->mana(),
		.gold = player->gold(),
		.experience = player->experience(),
		.strength = { player->attributes().strength().base(), player->attributes().strength().current() },
		.magic = { player->attributes().magic().base(), player->attributes().magic().current() },
		.dexterity = { player->attributes().dexterity().base(), player->attributes().dexterity().current() },
		.vitality = { player->attributes().vitality().base(), player->attributes().vitality().current() },
		.activeStoreId = player->active_store_id() == 0 ? std::nullopt : std::optional<uint32_t>(player->active_store_id()),
	};
	projected.inventory.reserve(player->inventory_size());
	for (const auto &source : player->inventory()) {
		auto item = ProjectNativeItem(source.state(), source.item_seed(), source.price());
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.inventory.push_back({
			.storeId = source.store_id(),
			.storeSlot = source.store_slot(),
			.itemSeed = source.item_seed(),
			.price = source.price(),
			.purchasedAtTick = source.purchased_at_tick(),
			.item = std::move(*item),
		});
	}
	projected.equipment.reserve(player->equipment_size());
	for (const auto &source : player->equipment()) {
		auto item = ProjectNativeItem(source.state(), source.item_seed(), 0);
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.equipment.push_back({ .slot = source.slot(), .itemSeed = source.item_seed(), .item = std::move(*item) });
	}
	projected.inventoryGrid.assign(player->inventory_grid().begin(), player->inventory_grid().end());
	return projected;
}

bool ServerBackedPlayerState::ApplySnapshot(ProjectedPlayerSnapshot snapshot)
{
	if (snapshot.entityId == 0)
		return false;
	snapshot_ = std::move(snapshot);
	return true;
}

void ServerBackedPlayerState::Clear() noexcept
{
	snapshot_.reset();
}

const ProjectedInventoryItem *ServerBackedPlayerState::FindInventoryItem(std::size_t index) const noexcept
{
	if (!snapshot_ || index >= snapshot_->inventory.size())
		return nullptr;
	return &snapshot_->inventory[index];
}

} // namespace devilution::authoritative
