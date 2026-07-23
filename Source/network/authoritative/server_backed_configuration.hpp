#pragma once

/**
 * @file network/authoritative/server_backed_configuration.hpp
 *
 * Opt-in runtime configuration for the server-backed client connection.
 */

#include <cstdint>
#include <string>
#include <string_view>

#include <expected.hpp>

namespace devilution::authoritative {

struct ServerBackedRuntimeConfiguration {
	bool enabled = false;
	std::string host = "127.0.0.1";
	uint16_t port = 6113;
};

/** Returns the process-wide opt-in server-backed connection settings. */
ServerBackedRuntimeConfiguration &GetServerBackedRuntimeConfiguration();

/** Parses a command-line server endpoint and enables the server-backed client. */
tl::expected<ServerBackedRuntimeConfiguration, std::string> ParseServerEndpoint(std::string_view endpoint);

} // namespace devilution::authoritative
