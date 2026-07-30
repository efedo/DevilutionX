#include "network/authoritative/server_backed_runtime.hpp"

#include "network/authoritative/server_backed_player_ui.hpp"
#include <algorithm>
#include <utility>

namespace devilution::authoritative {
namespace {

constexpr uint32_t SmithStoreId = 1;
constexpr uint32_t AdriaStoreId = 10;

} // namespace

tl::expected<void, std::string> ServerBackedRuntime::Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager)
{
	return StartImpl(configuration, storeManager, nullptr);
}

tl::expected<void, std::string> ServerBackedRuntime::Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager, Player &player)
{
	return StartImpl(configuration, storeManager, &player);
}

tl::expected<void, std::string> ServerBackedRuntime::StartImpl(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager, Player *player)
{
	Stop();
	if (!configuration.enabled)
		return {};
	if (configuration.clientBuildId.empty() || configuration.protocolSchemaVersion.empty() || configuration.contentManifestHash.empty())
		return tl::make_unexpected("Server-backed runtime requires client build, protocol, and content identity values.");

	ServerBackedSession::Configuration sessionConfiguration {
		.client = {
			.host = configuration.host,
			.port = configuration.port,
			.clientBuildId = configuration.clientBuildId,
			.protocolSchemaVersion = configuration.protocolSchemaVersion,
			.contentManifestHash = configuration.contentManifestHash,
			.resumeToken = configuration.resumeToken,
			.expectInitialSnapshot = true,
		},
	};
	auto session = ServerBackedSession::Connect(std::move(sessionConfiguration));
	if (!session.has_value())
		return tl::make_unexpected(session.error());

	session_ = std::move(*session);
	vendorUiAdapter_ = std::make_unique<ServerBackedVendorUiAdapter>(storeManager);
	player_ = player;
	if (player_ != nullptr) {
		if (auto result = ApplyCurrentPlayerSnapshot(); !result.has_value()) {
			Stop();
			return result;
		}
	}
	return {};
}

void ServerBackedRuntime::Stop() noexcept
{
	if (session_)
		session_->Close();
	vendorUiAdapter_.reset();
	session_.reset();
	worldProjection_.Clear();
	worldPresentation_.Clear();
	player_ = nullptr;
}

tl::expected<void, std::string> ServerBackedRuntime::OpenSmithStore(uint64_t requestedTick, uint64_t nowMs)
{
	return OpenVendor(SmithStoreId, ServerBackedVendorDestination::Smith, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::OpenVendor(uint32_t storeId, ServerBackedVendorDestination destination, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_ || !vendorUiAdapter_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->OpenVendor(storeId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	const ProjectedVendorSnapshot *snapshot = session_->VendorState().Snapshot();
	if (snapshot == nullptr)
		return tl::make_unexpected("The authoritative vendor returned no stock snapshot.");
	return vendorUiAdapter_->Apply(*snapshot, destination, storeId);
}

tl::expected<void, std::string> ServerBackedRuntime::PurchaseVendor(uint32_t storeId, uint32_t storeSlot, ServerBackedVendorDestination destination, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_ || !vendorUiAdapter_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->Purchase(storeId, storeSlot, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	if (auto result = ApplyCurrentPlayerSnapshot(); !result.has_value())
		return result;
	const ProjectedVendorSnapshot *snapshot = session_->VendorState().Snapshot();
	if (snapshot == nullptr)
		return tl::make_unexpected("The authoritative vendor purchase returned no stock snapshot.");
	return vendorUiAdapter_->Apply(*snapshot, destination, storeId);
}

tl::expected<void, std::string> ServerBackedRuntime::OpenAdriaStore(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->OpenVendor(AdriaStoreId, requestedTick, nowMs); !result.has_value())
		return result;
	return EnsureCommandAccepted();
}

tl::expected<void, std::string> ServerBackedRuntime::PurchaseSmith(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs)
{
	return PurchaseVendor(SmithStoreId, storeSlot, ServerBackedVendorDestination::Smith, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::SellItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	return SellItem({ .location = ServerBackedItemLocation::Inventory, .slot = inventoryIndex }, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::SellItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->SellItem(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RepairItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	return RepairItem({ .location = ServerBackedItemLocation::Inventory, .slot = inventoryIndex }, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::RepairItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RepairItem(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RechargeItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	return RechargeItem({ .location = ServerBackedItemLocation::Inventory, .slot = inventoryIndex }, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::RechargeItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RechargeItem(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::IdentifyItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	return IdentifyItem({ .location = ServerBackedItemLocation::Inventory, .slot = inventoryIndex }, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::IdentifyItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->IdentifyItem(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RefillMana(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RefillMana(requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::MoveInventoryItem(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->MoveInventoryItem(inventoryIndex, targetCell, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::MoveItem(ServerBackedItemReference item, ServerBackedItemReference destination, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->MoveItem(item, destination, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::Move(int32_t directionX, int32_t directionY, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->Move(directionX, directionY, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::Attack(uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->Attack(targetEntityId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::Cast(uint32_t spellId, uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return Cast(spellId, targetEntityId, 0, 0, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::Cast(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->Cast(spellId, targetEntityId, targetX, targetY, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::UsePortal(uint32_t portalId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->UsePortal(portalId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::PickupWorldItem(uint32_t itemEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->PickupWorldItem(itemEntityId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::OperateObject(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->OperateObject(objectEntityId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::AdvanceQuest(uint32_t questId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->AdvanceQuest(questId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::Poll(uint64_t nowMs)
{
	if (!session_)
		return {};
	if (auto result = session_->Poll(nowMs); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

std::optional<uint32_t> ServerBackedRuntime::SmithStoreSlotAt(std::size_t index) const noexcept
{
	if (!vendorUiAdapter_)
		return std::nullopt;
	return vendorUiAdapter_->StoreSlotAt(index);
}

std::optional<uint32_t> ServerBackedRuntime::MonsterEntityIdAt(std::size_t index) const noexcept
{
	if (index >= worldProjection_.Monsters().size())
		return std::nullopt;
	const auto entityId = worldProjection_.Monsters()[index].entityId;
	return entityId == 0 ? std::nullopt : std::optional<uint32_t> { entityId };
}

std::optional<uint32_t> ServerBackedRuntime::WorldItemEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	return worldProjection_.WorldItemAt(positionX, positionY);
}

std::optional<uint32_t> ServerBackedRuntime::MonsterEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	return worldProjection_.MonsterAt(positionX, positionY);
}

std::optional<uint32_t> ServerBackedRuntime::ObjectEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	return worldProjection_.ObjectAt(positionX, positionY);
}

std::optional<ServerBackedItemReference> ServerBackedRuntime::PlayerItemReferenceForSeed(uint32_t itemSeed) const noexcept
{
	if (!session_ || itemSeed == 0)
		return std::nullopt;
	const ProjectedPlayerSnapshot *snapshot = session_->PlayerState().Snapshot();
	if (snapshot == nullptr)
		return std::nullopt;
	for (std::size_t index = 0; index < snapshot->inventory.size(); ++index) {
		if (snapshot->inventory[index].itemSeed == itemSeed)
			return ServerBackedItemReference { .location = ServerBackedItemLocation::Inventory, .slot = static_cast<uint32_t>(index) };
	}
	for (const auto &beltItem : snapshot->belt) {
		if (beltItem.itemSeed == itemSeed)
			return ServerBackedItemReference { .location = ServerBackedItemLocation::Belt, .slot = beltItem.slot };
	}
	for (const auto &equipment : snapshot->equipment) {
		if (equipment.itemSeed == itemSeed)
			return ServerBackedItemReference { .location = ServerBackedItemLocation::Equipment, .slot = equipment.slot };
	}
	return std::nullopt;
}

tl::expected<void, std::string> ServerBackedRuntime::ApplyCurrentPlayerSnapshot()
{
	if (!session_ || player_ == nullptr)
		return {};
	const ProjectedPlayerSnapshot *snapshot = session_->PlayerState().Snapshot();
	if (snapshot == nullptr)
		return tl::make_unexpected("The server-backed session has no player snapshot.");
	if (auto result = worldProjection_.Apply(session_->MonsterState(), session_->WorldItemState(), session_->ObjectState(), snapshot->levelId); !result.has_value())
		return result;
	if (auto result = worldPresentation_.Apply(worldProjection_, snapshot->levelId); !result.has_value())
		return result;
	for (const auto &eventBatch : session_->TakePendingEventBatches())
		ApplyServerBackedEventBatch(*player_, eventBatch, session_->EntityId());
	return ApplyServerBackedPlayerSnapshot(*player_, *snapshot);
}

tl::expected<void, std::string> ServerBackedRuntime::EnsureCommandAccepted() const
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	switch (session_->LastCommandResolution()) {
	case ServerBackedSession::CommandResolution::Accepted:
	case ServerBackedSession::CommandResolution::Duplicate:
		return {};
	case ServerBackedSession::CommandResolution::Rejected:
		return tl::make_unexpected("The authoritative server rejected the store command.");
	case ServerBackedSession::CommandResolution::Rescheduled:
		return tl::make_unexpected("The authoritative server rescheduled the store command; it was not applied locally.");
	case ServerBackedSession::CommandResolution::None:
		return tl::make_unexpected("The authoritative server did not resolve the store command.");
	}
	return tl::make_unexpected("The authoritative server returned an unknown command status.");
}

ServerBackedRuntime &GetServerBackedRuntime()
{
	static ServerBackedRuntime runtime;
	return runtime;
}

} // namespace devilution::authoritative
