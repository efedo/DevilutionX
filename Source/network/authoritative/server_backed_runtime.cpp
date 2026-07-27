#include "network/authoritative/server_backed_runtime.hpp"

#include <utility>

namespace devilution::authoritative {
namespace {

constexpr uint32_t SmithStoreId = 1;

} // namespace

tl::expected<void, std::string> ServerBackedRuntime::Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager)
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
	return {};
}

void ServerBackedRuntime::Stop() noexcept
{
	if (session_)
		session_->Close();
	vendorUiAdapter_.reset();
	session_.reset();
}

tl::expected<void, std::string> ServerBackedRuntime::OpenSmithStore(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_ || !vendorUiAdapter_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->OpenVendor(SmithStoreId, requestedTick, nowMs); !result.has_value())
		return result;
	const ProjectedVendorSnapshot *snapshot = session_->VendorState().Snapshot();
	if (snapshot == nullptr)
		return tl::make_unexpected("The server-backed Smith store returned no stock snapshot.");
	return vendorUiAdapter_->Apply(*snapshot, ServerBackedVendorDestination::Smith, SmithStoreId);
}

ServerBackedRuntime &GetServerBackedRuntime()
{
	static ServerBackedRuntime runtime;
	return runtime;
}

} // namespace devilution::authoritative
