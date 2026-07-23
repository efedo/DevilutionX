#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/store_command.hpp"

namespace devilution::authoritative {
namespace {

TEST(AuthoritativeStoreCommand, BuildsOpenStoreIntent)
{
	auto command = MakeOpenStoreCommand(4, 19);
	ASSERT_TRUE(command.has_value()) << command.error();
	EXPECT_EQ(command->requested_tick(), 19U);
	ASSERT_TRUE(command->has_open_store_requested());
	EXPECT_EQ(command->open_store_requested().store_id(), 4U);
	EXPECT_EQ(command->client_sequence(), 0U);
}

TEST(AuthoritativeStoreCommand, BuildsPurchaseIntentWithStableSlot)
{
	auto command = MakePurchaseCommand(4, 12, 21);
	ASSERT_TRUE(command.has_value()) << command.error();
	EXPECT_EQ(command->requested_tick(), 21U);
	ASSERT_TRUE(command->has_purchase_requested());
	EXPECT_EQ(command->purchase_requested().store_id(), 4U);
	EXPECT_EQ(command->purchase_requested().store_slot(), 12U);
}

TEST(AuthoritativeStoreCommand, RejectsInvalidStoreIdentifier)
{
	EXPECT_FALSE(MakeOpenStoreCommand(0, 1).has_value());
	EXPECT_FALSE(MakePurchaseCommand(0, 0, 1).has_value());
}

} // namespace
} // namespace devilution::authoritative
