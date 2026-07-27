#pragma once

/**
 * @file network/authoritative/server_backed_runtime.hpp
 *
 * Opt-in lifecycle bridge between the game loop and the server-backed session.
 */

#include <cstdint>
#include <memory>
#include <string>

#include <expected.hpp>

#include "game/stores/stores.hpp"
#include "network/authoritative/server_backed_configuration.hpp"
#include "network/authoritative/server_backed_session.hpp"
#include "network/authoritative/server_backed_vendor_ui.hpp"

namespace devilution::authoritative {

class ServerBackedRuntime {
public:
	tl::expected<void, std::string> Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager);
	void Stop() noexcept;

	[[nodiscard]] bool IsConnected() const noexcept { return session_ != nullptr; }
	[[nodiscard]] ServerBackedSession *Session() noexcept { return session_.get(); }
	[[nodiscard]] const ServerBackedSession *Session() const noexcept { return session_.get(); }

	/** Opens the experimental Smith store and applies its authoritative stock to the legacy UI buffers. */
	tl::expected<void, std::string> OpenSmithStore(uint64_t requestedTick, uint64_t nowMs);

private:
	std::unique_ptr<ServerBackedSession> session_;
	std::unique_ptr<ServerBackedVendorUiAdapter> vendorUiAdapter_;
};

/** Returns the process-wide opt-in runtime bridge. */
ServerBackedRuntime &GetServerBackedRuntime();

} // namespace devilution::authoritative
