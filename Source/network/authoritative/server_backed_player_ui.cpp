#include "network/authoritative/server_backed_player_ui.hpp"

#include <climits>
#include <cstddef>
#include <cstdint>
#include <algorithm>
#include <set>

namespace devilution::authoritative {
namespace {

tl::expected<void, std::string> ValidateSnapshot(const ProjectedPlayerSnapshot &snapshot)
{
	if (snapshot.entityId == 0)
		return tl::make_unexpected("Server-backed player snapshot has an invalid entity ID.");
	if (snapshot.positionX < 0 || snapshot.positionX > UINT8_MAX || snapshot.positionY < 0 || snapshot.positionY > UINT8_MAX)
		return tl::make_unexpected("Server-backed player position is outside the native tile range.");
	if (snapshot.life < 0 || snapshot.mana < 0 || snapshot.manaMaximum < 0 || snapshot.gold > static_cast<uint32_t>(INT_MAX))
		return tl::make_unexpected("Server-backed player resources are outside the native range.");
	if (snapshot.belt.size() > MaxBeltItems)
		return tl::make_unexpected("Server-backed belt exceeds the native belt capacity.");
	for (const auto &beltItem : snapshot.belt) {
		if (beltItem.slot >= MaxBeltItems)
			return tl::make_unexpected("Server-backed belt contains an invalid slot.");
	}
	if (snapshot.inventory.size() > InventoryGridCells || snapshot.inventoryGrid.size() != InventoryGridCells)
		return tl::make_unexpected("Server-backed inventory does not fit the native inventory layout.");

	std::set<uint32_t> equipmentSlots;
	for (const auto &equipment : snapshot.equipment) {
		if (equipment.slot >= NUM_INVLOC || !equipmentSlots.insert(equipment.slot).second)
			return tl::make_unexpected("Server-backed equipment contains a duplicate or invalid slot.");
	}
	for (const int32_t cell : snapshot.inventoryGrid) {
		if (cell < -1 || (cell >= 0 && static_cast<std::size_t>(cell) >= snapshot.inventory.size()))
			return tl::make_unexpected("Server-backed inventory grid references an invalid item.");
	}
	return {};
}

void ApplyResource(VitalResource &destination, int32_t current)
{
	destination.current = current;
	destination.base = current + destination.maximumBase - destination.maximum;
	destination.current = std::clamp(destination.current, 0, destination.maximum);
	destination.base = std::clamp(destination.base, 0, destination.maximumBase);
}

} // namespace

tl::expected<void, std::string> ApplyServerBackedPlayerSnapshot(Player &player, const ProjectedPlayerSnapshot &snapshot)
{
	if (auto result = ValidateSnapshot(snapshot); !result.has_value())
		return result;

	player.position.tile = { static_cast<WorldTileCoord>(snapshot.positionX), static_cast<WorldTileCoord>(snapshot.positionY) };
	player.position.future = player.position.tile;
	player.position.last = player.position.tile;
	player.position.old = player.position.tile;
	player.position.temp = player.position.tile;
	player._pGold = static_cast<int>(snapshot.gold);
	player._pExperience = snapshot.experience;
	player.attributes.strength = { snapshot.strength.base, snapshot.strength.current };
	player.attributes.magic = { snapshot.magic.base, snapshot.magic.current };
	player.attributes.dexterity = { snapshot.dexterity.base, snapshot.dexterity.current };
	player.attributes.vitality = { snapshot.vitality.base, snapshot.vitality.current };
	if (snapshot.manaMaximum > 0) {
		player.mana.maximum = snapshot.manaMaximum;
		player.mana.maximumBase = snapshot.manaMaximum;
	}
	ApplyResource(player.life, snapshot.life);
	ApplyResource(player.mana, snapshot.mana);

	for (auto &item : player.InvBody)
		item.clear();
	for (auto &item : player.InvList)
		item.clear();
	for (auto &item : player.SpdList)
		item.clear();
	for (const auto &equipment : snapshot.equipment)
		player.InvBody[equipment.slot] = equipment.item;
	for (const auto &beltItem : snapshot.belt)
		player.SpdList[beltItem.slot] = beltItem.item;
	for (std::size_t index = 0; index < snapshot.inventory.size(); ++index)
		player.InvList[index] = snapshot.inventory[index].item;
	player._pNumInv = static_cast<int>(snapshot.inventory.size());
	for (std::size_t index = 0; index < snapshot.inventoryGrid.size(); ++index) {
		const int32_t cell = snapshot.inventoryGrid[index];
		player.InvGrid[index] = cell < 0 ? 0 : static_cast<int8_t>(cell + 1);
	}
	player.UpdateHitPointPercentage();
	player.UpdateManaPercentage();
	return {};
}

} // namespace devilution::authoritative
