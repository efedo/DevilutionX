#pragma once

/**
 * @file network/authoritative/server_backed_session.hpp
 *
 * Runtime lifecycle for the opt-in server-backed inventory/store slice.
 */

#include <cstdint>
#include <map>
#include <memory>
#include <string>

#include <expected.hpp>

#include "network/authoritative/server_backed_client.hpp"
#include "network/authoritative/player_snapshot.hpp"
#include "network/authoritative/server_backed_vendor_state.hpp"

namespace devilution::authoritative {

class ServerBackedSession {
public:
	struct Configuration {
		ServerBackedClient::Configuration client;
	};

	static tl::expected<std::unique_ptr<ServerBackedSession>, std::string> Connect(Configuration configuration);

	[[nodiscard]] const ServerBackedClient &Client() const noexcept { return *client_; }
	[[nodiscard]] uint32_t EntityId() const noexcept { return entityId_; }
	[[nodiscard]] const ServerBackedPlayerState &PlayerState() const noexcept { return playerState_; }
	[[nodiscard]] const ServerBackedVendorState &VendorState() const noexcept { return vendorState_; }

	/** Opens a vendor and waits for its authoritative snapshot. */
	tl::expected<void, std::string> OpenVendor(uint32_t storeId, uint64_t requestedTick, uint64_t nowMs);

	/** Purchases one stable vendor slot and waits for the updated snapshot. */
	tl::expected<void, std::string> Purchase(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> SellItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RepairItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RechargeItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> IdentifyItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> MoveInventoryItem(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs);

	/** Reconnects, applies the resynchronization snapshot, and resolves retries. */
	tl::expected<void, std::string> Reconnect(uint64_t nowMs);

	void Close() noexcept;

private:
	struct PendingIntent {
		enum class Kind {
			OpenVendor,
			Purchase,
			Inventory,
		};
		Kind kind;
		uint32_t storeId = 0;
		uint32_t storeSlot = 0;
	};

	ServerBackedSession() = default;

	tl::expected<void, std::string> ApplySnapshot(const protocol::Snapshot &snapshot);
	tl::expected<void, std::string> Flush(uint64_t nowMs);
	tl::expected<void, std::string> SubmitInventoryCommand(tl::expected<protocol::Command, std::string> command, uint64_t nowMs);
	void ApplyAcknowledgements(const protocol::CommandAck &acknowledgement);

	std::unique_ptr<ServerBackedClient> client_;
	uint32_t entityId_ = 0;
	ServerBackedPlayerState playerState_;
	ServerBackedVendorState vendorState_;
	std::map<uint64_t, PendingIntent> pendingIntents_;
};

} // namespace devilution::authoritative
