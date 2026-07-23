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

[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeOpenStoreCommand(uint32_t storeId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakePurchaseCommand(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeSellItemCommand(uint32_t inventoryIndex, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRepairItemCommand(uint32_t inventoryIndex, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRechargeItemCommand(uint32_t inventoryIndex, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeIdentifyItemCommand(uint32_t inventoryIndex, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveInventoryItemCommand(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick);

} // namespace devilution::authoritative
