#include "network/authoritative/server_backed_session.hpp"

#include "network/authoritative/store_command.hpp"

namespace devilution::authoritative {

tl::expected<std::unique_ptr<ServerBackedSession>, std::string> ServerBackedSession::Connect(Configuration configuration)
{
	configuration.client.expectInitialSnapshot = true;
	auto client = ServerBackedClient::Connect(std::move(configuration.client));
	if (!client.has_value())
		return tl::make_unexpected(client.error());

	auto session = std::unique_ptr<ServerBackedSession>(new ServerBackedSession());
	session->client_ = std::move(*client);
	auto initialSnapshot = session->client_->ReadSnapshot();
	if (!initialSnapshot.has_value())
		return tl::make_unexpected(initialSnapshot.error());
	if (auto result = session->ApplySnapshot(*initialSnapshot); !result.has_value())
		return tl::make_unexpected(result.error());
	return session;
}

tl::expected<void, std::string> ServerBackedSession::OpenVendor(uint32_t storeId, uint64_t requestedTick, uint64_t nowMs)
{
	if (vendorState_.OpenStore(storeId) != VendorIntentRoute::Pending)
		return tl::make_unexpected("The server-backed vendor is not ready to open.");
	auto command = MakeOpenStoreCommand(storeId, requestedTick);
	if (!command.has_value())
		return tl::make_unexpected(command.error());
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::OpenVendor, .storeId = storeId });
	return Flush(nowMs);
}

tl::expected<void, std::string> ServerBackedSession::Purchase(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs)
{
	if (vendorState_.Purchase(storeId, storeSlot) != VendorIntentRoute::Pending)
		return tl::make_unexpected("The server-backed vendor cannot accept this purchase.");
	auto command = MakePurchaseCommand(storeId, storeSlot, requestedTick);
	if (!command.has_value())
		return tl::make_unexpected(command.error());
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::Purchase, .storeId = storeId, .storeSlot = storeSlot });
	return Flush(nowMs);
}

tl::expected<void, std::string> ServerBackedSession::SellItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeSellItemCommand(inventoryIndex, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RepairItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeRepairItemCommand(inventoryIndex, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RechargeItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeRechargeItemCommand(inventoryIndex, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::IdentifyItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeIdentifyItemCommand(inventoryIndex, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::MoveInventoryItem(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeMoveInventoryItemCommand(inventoryIndex, targetCell, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::SubmitInventoryCommand(tl::expected<protocol::Command, std::string> command, uint64_t nowMs)
{
	if (!command.has_value())
		return tl::make_unexpected(command.error());
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::Inventory });
	return Flush(nowMs);
}

tl::expected<void, std::string> ServerBackedSession::Reconnect(uint64_t nowMs)
{
	if (auto result = client_->Reconnect(nowMs); !result.has_value())
		return tl::make_unexpected(result.error());
	vendorState_.SetConnected(true);
	auto snapshot = client_->ReadSnapshot();
	if (!snapshot.has_value())
		return tl::make_unexpected(snapshot.error());
	if (auto result = ApplySnapshot(*snapshot); !result.has_value())
		return tl::make_unexpected(result.error());
	if (client_->PendingTrackedCommandCount() == 0)
		return {};
	return Flush(nowMs);
}

void ServerBackedSession::Close() noexcept
{
	if (client_)
		client_->Close();
	vendorState_.SetConnected(false);
}

tl::expected<void, std::string> ServerBackedSession::ApplySnapshot(const protocol::Snapshot &snapshot)
{
	if (entityId_ == 0) {
		if (snapshot.players_size() == 0)
			return tl::make_unexpected("Server-backed snapshot contains no player entity.");
		entityId_ = snapshot.players(0).entity_id();
	}
	auto player = ProjectPlayerSnapshot(snapshot, entityId_);
	if (!player.has_value())
		return tl::make_unexpected(player.error());
	if (!playerState_.ApplySnapshot(std::move(*player)))
		return tl::make_unexpected("Server-backed player snapshot could not be applied.");

	vendorState_.SetEnabled(true);
	vendorState_.SetConnected(true);
	if (snapshot.has_active_store()) {
		auto vendor = ProjectVendorSnapshot(snapshot);
		if (!vendor.has_value())
			return tl::make_unexpected(vendor.error());
		if (!vendorState_.ApplySnapshot(std::move(*vendor)))
			return tl::make_unexpected("Server-backed vendor snapshot could not be applied.");
	} else {
		vendorState_.ClearSnapshot();
	}
	return {};
}

tl::expected<void, std::string> ServerBackedSession::Flush(uint64_t nowMs)
{
	if (pendingIntents_.empty())
		return {};
	if (auto result = client_->SendQueuedCommands(nowMs); !result.has_value())
		return result;
	auto acknowledgement = client_->ReceiveCommandAcknowledgement(nowMs);
	if (!acknowledgement.has_value())
		return tl::make_unexpected(acknowledgement.error());
	ApplyAcknowledgements(*acknowledgement);
	auto snapshot = client_->ReadSnapshot();
	if (!snapshot.has_value())
		return tl::make_unexpected(snapshot.error());
	return ApplySnapshot(*snapshot);
}

void ServerBackedSession::ApplyAcknowledgements(const protocol::CommandAck &acknowledgement)
{
	for (const auto &result : acknowledgement.results()) {
		const auto pending = pendingIntents_.find(result.client_sequence());
		if (pending == pendingIntents_.end())
			continue;
		if (pending->second.kind == PendingIntent::Kind::Purchase) {
			if (result.status() == protocol::COMMAND_STATUS_REJECTED)
				(void)vendorState_.ResolvePurchase(pending->second.storeId, pending->second.storeSlot, PurchaseResolution::Rejected);
			else if (result.status() == protocol::COMMAND_STATUS_ACCEPTED || result.status() == protocol::COMMAND_STATUS_DUPLICATE)
				(void)vendorState_.ResolvePurchase(pending->second.storeId, pending->second.storeSlot, PurchaseResolution::Accepted);
		}
		if (result.status() != protocol::COMMAND_STATUS_RESCHEDULED)
			pendingIntents_.erase(pending);
	}
}

} // namespace devilution::authoritative
