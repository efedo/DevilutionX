#pragma once

/**
 * @file network/authoritative/server_backed_runtime.hpp
 *
 * Opt-in lifecycle bridge between the game loop and the server-backed session.
 */

#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <string>

#include <expected.hpp>

#include "game/stores/stores.hpp"
#include "game/players/players.hpp"
#include "network/authoritative/server_backed_configuration.hpp"
#include "network/authoritative/server_backed_session.hpp"
#include "network/authoritative/server_backed_vendor_ui.hpp"

namespace devilution::authoritative {

class ServerBackedRuntime {
public:
	tl::expected<void, std::string> Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager);
	tl::expected<void, std::string> Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager, Player &player);
	void Stop() noexcept;

	[[nodiscard]] bool IsConnected() const noexcept { return session_ != nullptr; }
	[[nodiscard]] ServerBackedSession *Session() noexcept { return session_.get(); }
	[[nodiscard]] const ServerBackedSession *Session() const noexcept { return session_.get(); }

	/** Opens the experimental Smith store and applies its authoritative stock to the legacy UI buffers. */
	tl::expected<void, std::string> OpenVendor(uint32_t storeId, ServerBackedVendorDestination destination, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> PurchaseVendor(uint32_t storeId, uint32_t storeSlot, ServerBackedVendorDestination destination, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> OpenSmithStore(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> OpenAdriaStore(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> PurchaseSmith(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> SellItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RepairItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RechargeItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> IdentifyItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> SellItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RepairItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RechargeItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> IdentifyItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RefillMana(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> MoveInventoryItem(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> MoveItem(ServerBackedItemReference item, ServerBackedItemReference destination, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> Poll(uint64_t nowMs);

	[[nodiscard]] std::optional<uint32_t> SmithStoreSlotAt(std::size_t index) const noexcept;
	/** Finds the authoritative location of an item, including while it is held by the legacy cursor. */
	[[nodiscard]] std::optional<ServerBackedItemReference> PlayerItemReferenceForSeed(uint32_t itemSeed) const noexcept;

private:
	tl::expected<void, std::string> StartImpl(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager, Player *player);
	tl::expected<void, std::string> ApplyCurrentPlayerSnapshot();
	tl::expected<void, std::string> EnsureCommandAccepted() const;

	std::unique_ptr<ServerBackedSession> session_;
	std::unique_ptr<ServerBackedVendorUiAdapter> vendorUiAdapter_;
	Player *player_ = nullptr;
};

/** Returns the process-wide opt-in runtime bridge. */
ServerBackedRuntime &GetServerBackedRuntime();

} // namespace devilution::authoritative
