#include "network/authoritative/player_snapshot.hpp"

#include <utility>

#include <algorithm>

#include "devilution.pb.h"
#include "game/players/players.hpp"
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
		.lifeMaximum = player->life_maximum(),
		.mana = player->mana(),
		.manaMaximum = player->mana_maximum(),
		.gold = player->gold(),
		.experience = player->experience(),
		.characterLevel = player->character_level(),
		.levelId = player->level_id(),
		.strength = { player->attributes().strength().base(), player->attributes().strength().current() },
		.magic = { player->attributes().magic().base(), player->attributes().magic().current() },
		.dexterity = { player->attributes().dexterity().base(), player->attributes().dexterity().current() },
		.vitality = { player->attributes().vitality().base(), player->attributes().vitality().current() },
		.activeStoreId = player->active_store_id() == 0 ? std::nullopt : std::optional<uint32_t>(player->active_store_id()),
	};
	projected.statusEffects.reserve(player->status_effects_size());
	for (const auto &effect : player->status_effects())
		projected.statusEffects.push_back({ effect.effect_id(), effect.remaining_ticks(), effect.magnitude() });
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
	projected.belt.reserve(player->belt_size());
	for (const auto &source : player->belt()) {
		auto item = ProjectNativeItem(source.state(), source.item_seed(), 0);
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.belt.push_back({ .slot = source.slot(), .itemSeed = source.item_seed(), .item = std::move(*item) });
	}
	projected.inventoryGrid.assign(player->inventory_grid().begin(), player->inventory_grid().end());
	return projected;
}

tl::expected<std::vector<ProjectedMonsterSnapshot>, std::string> ProjectMonsterSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedMonsterSnapshot> projected;
	projected.reserve(snapshot.monsters_size());
	for (const auto &source : snapshot.monsters()) {
		if (source.entity_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains a monster with an invalid entity ID.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate monster entity IDs.");
		projected.push_back({
			.entityId = source.entity_id(),
			.monsterId = source.monster_id(),
			.levelId = source.level_id(),
			.positionX = source.position_x(),
			.positionY = source.position_y(),
			.hitPoints = source.hit_points(),
			.maxHitPoints = source.max_hit_points(),
			.armorClass = source.armor_class(),
			.alive = source.alive(),
			.attackDamage = source.attack_damage(),
			.aggroRange = source.aggro_range(),
			.fireResistance = source.fire_resistance(),
			.lightningResistance = source.lightning_resistance(),
			.magicResistance = source.magic_resistance(),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

tl::expected<std::vector<ProjectedWorldItemSnapshot>, std::string> ProjectWorldItemSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedWorldItemSnapshot> projected;
	projected.reserve(snapshot.world_items_size());
	for (const auto &source : snapshot.world_items()) {
		if (source.entity_id() == 0 || source.item_seed() == 0)
			return tl::make_unexpected("Server-backed snapshot contains a world item with an invalid entity or item ID.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate world item entity IDs.");
		auto item = ProjectNativeItem(source.state(), source.item_seed(), source.price());
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.push_back({
			.entityId = source.entity_id(),
			.levelId = source.level_id(),
			.positionX = source.position_x(),
			.positionY = source.position_y(),
			.itemSeed = source.item_seed(),
			.price = source.price(),
			.item = std::move(*item),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

tl::expected<std::vector<ProjectedObjectSnapshot>, std::string> ProjectObjectSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedObjectSnapshot> projected;
	projected.reserve(snapshot.objects_size());
	for (const auto &source : snapshot.objects()) {
		if (source.entity_id() == 0 || source.object_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains an object with an invalid entity or object ID.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate object entity IDs.");
		projected.push_back({
			.entityId = source.entity_id(),
			.objectId = source.object_id(),
			.levelId = source.level_id(),
			.positionX = source.position_x(),
			.positionY = source.position_y(),
			.activated = source.activated(),
			.questId = source.quest_id(),
			.effectKind = source.effect_kind(),
			.effectAmount = source.effect_amount(),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

tl::expected<std::vector<ProjectedProjectileSnapshot>, std::string> ProjectProjectileSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedProjectileSnapshot> projected;
	projected.reserve(snapshot.projectiles_size());
	for (const auto &source : snapshot.projectiles()) {
		if (source.entity_id() == 0 || source.source_entity_id() == 0 || source.spell_id() == 0 || source.remaining_ticks() == 0)
			return tl::make_unexpected("Server-backed snapshot contains a projectile with an invalid identity or lifetime.");
		if (source.damage() < 0 || source.area_radius() < 0)
			return tl::make_unexpected("Server-backed snapshot contains a projectile with an invalid effect.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate projectile entity IDs.");
		projected.push_back({
			.entityId = source.entity_id(),
			.sourceEntityId = source.source_entity_id(),
			.targetEntityId = source.target_entity_id(),
			.spellId = source.spell_id(),
			.levelId = source.level_id(),
			.positionX = source.position_x(),
			.positionY = source.position_y(),
			.targetX = source.target_x(),
			.targetY = source.target_y(),
			.damage = source.damage(),
			.damageType = source.damage_type(),
			.areaRadius = source.area_radius(),
			.remainingTicks = source.remaining_ticks(),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

void ApplyServerBackedEventBatch(Player &player, const protocol::EventBatch &eventBatch, uint32_t entityId)
{
	for (const auto &event : eventBatch.events()) {
		if (event.has_damage() && event.damage().target_entity_id() == entityId) {
			player.life.current = std::max(0, player.life.current - std::max(0, event.damage().amount()));
			player.UpdateHitPointPercentage();
		}
		if (event.has_healing() && event.healing().target_entity_id() == entityId) {
			player.life.current = std::min(player.life.maximum, player.life.current + std::max(0, event.healing().amount()));
			player.UpdateHitPointPercentage();
		}
		if (event.has_experience() && event.experience().player_entity_id() == entityId)
			player._pExperience += event.experience().amount();
	}
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
