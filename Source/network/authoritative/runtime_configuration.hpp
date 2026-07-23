#pragma once

/**
 * @file network/authoritative/runtime_configuration.hpp
 *
 * Opt-in runtime configuration for the authoritative server connection.
 */

#include <cstdint>
#include <string>
#include <string_view>

#include <expected.hpp>

namespace devilution::authoritative {

struct RuntimeConfiguration {
	bool enabled = false;
	std::string host = "127.0.0.1";
	uint16_t port = 6113;
};

/** Returns the process-wide opt-in authoritative connection settings. */
RuntimeConfiguration &GetRuntimeConfiguration();

/** Parses a command-line endpoint and enables the authoritative connection. */
tl::expected<RuntimeConfiguration, std::string> ParseAuthoritativeEndpoint(std::string_view endpoint);

} // namespace devilution::authoritative
