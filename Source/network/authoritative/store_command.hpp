#pragma once

/**
 * @file network/authoritative/store_command.hpp
 *
 * Server-backed vendor intent construction at the Protobuf boundary.
 */

#include <cstdint>
#include <string>

#include <expected.hpp>

#include "devilution.pb.h"

namespace devilution::authoritative {

[[nodiscard]] tl::expected<protocol::v1::Command, std::string> MakeOpenStoreCommand(uint32_t storeId, uint64_t requestedTick);
[[nodiscard]] tl::expected<protocol::v1::Command, std::string> MakePurchaseCommand(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick);

} // namespace devilution::authoritative
