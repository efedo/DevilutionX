/**
 * @file network/authoritative/store_command.cpp
 *
 * Server-backed vendor intent construction at the Protobuf boundary.
 */

#include "network/authoritative/store_command.hpp"

#include "devilution.pb.h"

namespace devilution::authoritative {

tl::expected<::devilution::protocol::v1::Command, std::string> MakeOpenStoreCommand(uint32_t storeId, uint64_t requestedTick)
{
	if (storeId == 0)
		return tl::make_unexpected("Cannot open an invalid server-backed vendor.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_open_store_requested()->set_store_id(storeId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakePurchaseCommand(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick)
{
	if (storeId == 0)
		return tl::make_unexpected("Cannot purchase from an invalid server-backed vendor.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_purchase_requested();
	request->set_store_id(storeId);
	request->set_store_slot(storeSlot);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeSellItemCommand(uint32_t inventoryIndex, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX)
		return tl::make_unexpected("Cannot sell an invalid inventory index.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_sell_item_requested()->set_inventory_index(inventoryIndex);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRepairItemCommand(uint32_t inventoryIndex, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX)
		return tl::make_unexpected("Cannot repair an invalid inventory index.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_repair_item_requested()->set_inventory_index(inventoryIndex);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRechargeItemCommand(uint32_t inventoryIndex, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX)
		return tl::make_unexpected("Cannot recharge an invalid inventory index.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_recharge_item_requested()->set_inventory_index(inventoryIndex);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeIdentifyItemCommand(uint32_t inventoryIndex, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX)
		return tl::make_unexpected("Cannot identify an invalid inventory index.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_identify_item_requested()->set_inventory_index(inventoryIndex);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveInventoryItemCommand(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX || targetCell == UINT32_MAX)
		return tl::make_unexpected("Cannot move an invalid inventory item or cell.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_move_inventory_item_requested();
	request->set_inventory_index(inventoryIndex);
	request->set_target_cell(targetCell);
	return command;
}

} // namespace devilution::authoritative
