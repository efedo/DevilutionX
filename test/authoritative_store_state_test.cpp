#include <gtest/gtest.h>

#include <initializer_list>

#include "network/authoritative/store_state.hpp"

namespace devilution::authoritative {
namespace {

ProjectedStoreItem StoreItem(uint32_t slot, uint32_t seed = 0, uint32_t price = 0)
{
	return {
		.storeSlot = slot,
		.itemSeed = seed,
		.price = price,
	};
}

ProjectedStoreSnapshot StoreSnapshot(uint32_t storeId, std::initializer_list<ProjectedStoreItem> items)
{
	return { .storeId = storeId, .items = items };
}

TEST(AuthoritativeStoreState, DefaultsToUnchangedLocalRouting)
{
	AuthoritativeStoreState state;

	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::Disabled);
	EXPECT_EQ(state.OpenStore(1), StoreIntentRoute::Local);
	EXPECT_EQ(state.Purchase(1, 2), StoreIntentRoute::Local);
	EXPECT_EQ(state.PendingPurchaseCount(), 0U);
}

TEST(AuthoritativeStoreState, EnabledDisconnectedStateBlocksRemoteIntents)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);

	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::Disconnected);
	EXPECT_EQ(state.OpenStore(1), StoreIntentRoute::Blocked);
	EXPECT_EQ(state.Purchase(1, 2), StoreIntentRoute::Blocked);
	EXPECT_FALSE(state.ApplySnapshot(StoreSnapshot(1, { StoreItem(2) })));
}

TEST(AuthoritativeStoreState, ConnectedOpenRemainsPendingUntilMatchingSnapshot)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);
	state.SetConnected(true);

	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::AwaitingSnapshot);
	EXPECT_EQ(state.OpenStore(7), StoreIntentRoute::Pending);
	EXPECT_EQ(state.PendingOpenStoreId(), 7U);
	EXPECT_FALSE(state.ApplySnapshot(StoreSnapshot(8, { StoreItem(1) })));
	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::AwaitingSnapshot);
	EXPECT_EQ(state.PendingOpenStoreId(), 7U);

	EXPECT_TRUE(state.ApplySnapshot(StoreSnapshot(7, { StoreItem(3, 42, 75) })));
	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::Ready);
	EXPECT_FALSE(state.PendingOpenStoreId().has_value());
	ASSERT_NE(state.FindItem(7, 3), nullptr);
	EXPECT_EQ(state.FindItem(7, 3)->itemSeed, 42U);
}

TEST(AuthoritativeStoreState, StoreSlotsRemainStableWhenSnapshotOrderChanges)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);
	state.SetConnected(true);
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(4, {
	                                                StoreItem(10, 100, 20),
	                                                StoreItem(20, 200, 30),
	                                            })));

	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(4, {
	                                                StoreItem(20, 201, 31),
	                                                StoreItem(10, 101, 21),
	                                            })));
	ASSERT_NE(state.FindItem(4, 10), nullptr);
	ASSERT_NE(state.FindItem(4, 20), nullptr);
	EXPECT_EQ(state.FindItem(4, 10)->itemSeed, 101U);
	EXPECT_EQ(state.FindItem(4, 20)->itemSeed, 201U);
	EXPECT_EQ(state.FindItem(4, 10)->price, 21U);
}

TEST(AuthoritativeStoreState, PurchaseRequiresReadyMatchingStoreAndExistingSlot)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);
	state.SetConnected(true);
	EXPECT_EQ(state.Purchase(5, 9), StoreIntentRoute::Blocked);
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(9) })));

	EXPECT_EQ(state.Purchase(6, 9), StoreIntentRoute::Blocked);
	EXPECT_EQ(state.Purchase(5, 8), StoreIntentRoute::Blocked);
	EXPECT_EQ(state.Purchase(5, 9), StoreIntentRoute::Pending);
	EXPECT_EQ(state.Purchase(5, 9), StoreIntentRoute::Blocked);
	EXPECT_TRUE(state.IsPurchasePending(5, 9));
	EXPECT_EQ(state.PendingPurchaseCount(), 1U);
}

TEST(AuthoritativeStoreState, RejectedPurchaseImmediatelyClearsPendingState)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);
	state.SetConnected(true);
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(9) })));
	ASSERT_EQ(state.Purchase(5, 9), StoreIntentRoute::Pending);

	EXPECT_TRUE(state.ResolvePurchase(5, 9, PurchaseResolution::Rejected));
	EXPECT_FALSE(state.IsPurchasePending(5, 9));
	EXPECT_EQ(state.Purchase(5, 9), StoreIntentRoute::Pending);
	EXPECT_FALSE(state.ResolvePurchase(5, 8, PurchaseResolution::Rejected));
}

TEST(AuthoritativeStoreState, AcceptedPurchaseWaitsForSlotRemovalSnapshot)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);
	state.SetConnected(true);
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(9), StoreItem(10) })));
	ASSERT_EQ(state.Purchase(5, 9), StoreIntentRoute::Pending);

	EXPECT_TRUE(state.ResolvePurchase(5, 9, PurchaseResolution::Accepted));
	EXPECT_TRUE(state.IsPurchasePending(5, 9));
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(10), StoreItem(9) })));
	EXPECT_TRUE(state.IsPurchasePending(5, 9));
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(10) })));
	EXPECT_FALSE(state.IsPurchasePending(5, 9));
}

TEST(AuthoritativeStoreState, UnacknowledgedPurchaseIsNotClearedBySnapshot)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);
	state.SetConnected(true);
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(9) })));
	ASSERT_EQ(state.Purchase(5, 9), StoreIntentRoute::Pending);

	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, {})));
	EXPECT_TRUE(state.IsPurchasePending(5, 9));
	EXPECT_TRUE(state.ResolvePurchase(5, 9, PurchaseResolution::Accepted));
	EXPECT_TRUE(state.IsPurchasePending(5, 9));
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, {})));
	EXPECT_FALSE(state.IsPurchasePending(5, 9));
}

TEST(AuthoritativeStoreState, DisconnectPreservesPendingStateForReconnect)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);
	state.SetConnected(true);
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(9) })));
	ASSERT_EQ(state.Purchase(5, 9), StoreIntentRoute::Pending);
	ASSERT_TRUE(state.ResolvePurchase(5, 9, PurchaseResolution::Accepted));

	state.SetConnected(false);
	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::Disconnected);
	EXPECT_TRUE(state.IsPurchasePending(5, 9));
	EXPECT_EQ(state.Purchase(5, 9), StoreIntentRoute::Blocked);

	state.SetConnected(true);
	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::AwaitingSnapshot);
	EXPECT_TRUE(state.IsPurchasePending(5, 9));
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, {})));
	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::Ready);
	EXPECT_FALSE(state.IsPurchasePending(5, 9));
}

TEST(AuthoritativeStoreState, InvalidSnapshotDoesNotReplaceReadyState)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);
	state.SetConnected(true);
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(9, 90) })));

	EXPECT_FALSE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(3), StoreItem(3) })));
	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::Ready);
	ASSERT_NE(state.FindItem(5, 9), nullptr);
	EXPECT_EQ(state.FindItem(5, 9)->itemSeed, 90U);
	EXPECT_FALSE(state.ApplySnapshot(StoreSnapshot(0, {})));
}

TEST(AuthoritativeStoreState, DisablingReturnsToLocalRoutingAndClearsRemoteState)
{
	AuthoritativeStoreState state;
	state.SetEnabled(true);
	state.SetConnected(true);
	ASSERT_TRUE(state.ApplySnapshot(StoreSnapshot(5, { StoreItem(9) })));
	ASSERT_EQ(state.Purchase(5, 9), StoreIntentRoute::Pending);

	state.SetEnabled(false);
	EXPECT_EQ(state.Phase(), AuthoritativeStorePhase::Disabled);
	EXPECT_EQ(state.Purchase(5, 9), StoreIntentRoute::Local);
	EXPECT_EQ(state.OpenStore(5), StoreIntentRoute::Local);
	EXPECT_EQ(state.PendingPurchaseCount(), 0U);
	EXPECT_EQ(state.FindItem(5, 9), nullptr);
}

} // namespace
} // namespace devilution::authoritative
