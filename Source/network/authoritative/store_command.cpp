/**
 * @file network/authoritative/store_command.cpp
 *
 * Authoritative store intent construction at the Protobuf boundary.
 */

#include "network/authoritative/store_command.hpp"

#include "devilution.pb.h"

namespace devilution::authoritative {

tl::expected<protocol::v1::Command, std::string> MakeOpenStoreCommand(uint32_t storeId, uint64_t requestedTick)
{
	if (storeId == 0)
		return tl::make_unexpected("Cannot open an invalid authoritative store.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_open_store_requested()->set_store_id(storeId);
	return command;
}

tl::expected<protocol::v1::Command, std::string> MakePurchaseCommand(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick)
{
	if (storeId == 0)
		return tl::make_unexpected("Cannot purchase from an invalid authoritative store.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_purchase_requested();
	request->set_store_id(storeId);
	request->set_store_slot(storeSlot);
	return command;
}

} // namespace devilution::authoritative
