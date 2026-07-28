#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/store_command.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

TEST(ServerBackedVendorCommand, BuildsOpenStoreIntent)
{
	auto command = MakeOpenStoreCommand(4, 19);
	ASSERT_TRUE(command.has_value()) << command.error();
	EXPECT_EQ(command->requested_tick(), 19U);
	ASSERT_TRUE(command->has_open_store_requested());
	EXPECT_EQ(command->open_store_requested().store_id(), 4U);
	EXPECT_EQ(command->client_sequence(), 0U);
}

TEST(ServerBackedVendorCommand, BuildsPurchaseIntentWithStableSlot)
{
	auto command = MakePurchaseCommand(4, 12, 21);
	ASSERT_TRUE(command.has_value()) << command.error();
	EXPECT_EQ(command->requested_tick(), 21U);
	ASSERT_TRUE(command->has_purchase_requested());
	EXPECT_EQ(command->purchase_requested().store_id(), 4U);
	EXPECT_EQ(command->purchase_requested().store_slot(), 12U);
}

TEST(ServerBackedVendorCommand, RejectsInvalidStoreIdentifier)
{
	EXPECT_FALSE(MakeOpenStoreCommand(0, 1).has_value());
	EXPECT_FALSE(MakePurchaseCommand(0, 0, 1).has_value());
}

TEST(ServerBackedVendorCommand, BuildsInventoryIntents)
{
	const auto sell = MakeSellItemCommand(2, 9);
	const auto repair = MakeRepairItemCommand(2, 9);
	const auto recharge = MakeRechargeItemCommand(2, 9);
	const auto identify = MakeIdentifyItemCommand(2, 9);
	const auto move = MakeMoveInventoryItemCommand(2, 4, 9);
	ASSERT_TRUE(sell.has_value());
	ASSERT_TRUE(repair.has_value());
	ASSERT_TRUE(recharge.has_value());
	ASSERT_TRUE(identify.has_value());
	ASSERT_TRUE(move.has_value());
	EXPECT_EQ(sell->intent_case(), protocol::Command::kSellItemRequested);
	EXPECT_EQ(repair->repair_item_requested().inventory_index(), 2U);
	EXPECT_EQ(recharge->recharge_item_requested().inventory_index(), 2U);
	EXPECT_EQ(identify->identify_item_requested().inventory_index(), 2U);
	EXPECT_EQ(move->move_inventory_item_requested().target_cell(), 4U);
}

TEST(ServerBackedVendorCommand, RejectsSentinelInventoryValues)
{
	EXPECT_FALSE(MakeSellItemCommand(UINT32_MAX, 1).has_value());
	EXPECT_FALSE(MakeMoveInventoryItemCommand(1, UINT32_MAX, 1).has_value());
}

TEST(ServerBackedVendorCommand, BuildsStableEquipmentBeltAndManaIntents)
{
	const ServerBackedItemReference belt { .location = ServerBackedItemLocation::Belt, .slot = 6 };
	const ServerBackedItemReference equipment { .location = ServerBackedItemLocation::Equipment, .slot = 3 };
	const auto sell = MakeSellItemCommand(belt, 10);
	const auto repair = MakeRepairItemCommand(equipment, 11);
	const auto refill = MakeRefillManaCommand(12);
	ASSERT_TRUE(sell.has_value());
	ASSERT_TRUE(repair.has_value());
	ASSERT_TRUE(refill.has_value());
	EXPECT_EQ(sell->sell_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_BELT);
	EXPECT_EQ(sell->sell_item_requested().item().slot(), 6U);
	EXPECT_EQ(repair->repair_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_EQUIPMENT);
	EXPECT_EQ(repair->repair_item_requested().item().slot(), 3U);
	EXPECT_EQ(refill->intent_case(), protocol::Command::kRefillManaRequested);
}

TEST(ServerBackedVendorCommand, BuildsExplicitInventoryToBeltTransfer)
{
	const ServerBackedItemReference inventory { .location = ServerBackedItemLocation::Inventory, .slot = 2 };
	const ServerBackedItemReference belt { .location = ServerBackedItemLocation::Belt, .slot = 5 };
	const auto command = MakeMoveItemCommand(inventory, belt, 17);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_move_item_requested());
	EXPECT_EQ(command->move_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(command->move_item_requested().item().slot(), 2U);
	EXPECT_EQ(command->move_item_requested().destination().location(), protocol::PLAYER_ITEM_LOCATION_BELT);
	EXPECT_EQ(command->move_item_requested().destination().slot(), 5U);
}

} // namespace
} // namespace devilution::authoritative
