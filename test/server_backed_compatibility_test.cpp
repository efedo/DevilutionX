#include <type_traits>

#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/authoritative_client.hpp"
#include "network/authoritative/runtime_configuration.hpp"
#include "network/authoritative/store_snapshot.hpp"
#include "network/authoritative/store_state.hpp"
#include "network/authoritative/server_backed_client.hpp"
#include "network/authoritative/server_backed_configuration.hpp"
#include "network/authoritative/vendor_snapshot.hpp"
#include "network/authoritative/server_backed_vendor_state.hpp"

namespace devilution::authoritative {
namespace {

TEST(ServerBackedCompatibility, PreservesRenamedSourceAliases)
{
	static_assert(std::is_same_v<AuthoritativeClient, ServerBackedClient>);
	static_assert(std::is_same_v<RuntimeConfiguration, ServerBackedRuntimeConfiguration>);
	static_assert(std::is_same_v<ProjectedStoreItem, ProjectedVendorItem>);
	static_assert(std::is_same_v<ProjectedStoreSnapshot, ProjectedVendorSnapshot>);
	static_assert(std::is_same_v<StoreIntentRoute, VendorIntentRoute>);
	static_assert(std::is_same_v<AuthoritativeStorePhase, ServerBackedVendorPhase>);
	static_assert(std::is_same_v<AuthoritativeStoreState, ServerBackedVendorState>);

	const auto configuration = ParseAuthoritativeEndpoint("localhost:6113");
	ASSERT_TRUE(configuration.has_value()) << configuration.error();
	EXPECT_TRUE(configuration->enabled);

	protocol::Snapshot snapshot;
	snapshot.mutable_active_store()->set_store_id(1);
	const auto projected = ProjectStoreSnapshot(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->storeId, 1U);
}

} // namespace
} // namespace devilution::authoritative
