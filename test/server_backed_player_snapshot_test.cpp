#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/player_snapshot.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

TEST(ServerBackedPlayerSnapshot, ProjectsResourcesInventoryAndEquipment)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->set_position_x(11);
	player->set_position_y(-4);
	player->set_life(640);
	player->set_mana(32);
	player->set_gold(125);
	player->set_experience(99);
	player->mutable_attributes()->mutable_strength()->set_current(12);
	player->mutable_attributes()->mutable_strength()->set_base(10);
	player->mutable_inventory_grid()->Add(0);
	player->mutable_inventory_grid()->Add(-1);
	auto *inventory = player->add_inventory();
	inventory->set_store_id(1);
	inventory->set_store_slot(2);
	inventory->set_item_seed(42);
	inventory->set_price(75);
	inventory->set_purchased_at_tick(20);
	inventory->mutable_state()->set_item_type(4);
	auto *equipment = player->add_equipment();
	equipment->set_slot(0);
	equipment->set_item_seed(77);
	equipment->mutable_state()->set_item_type(5);

	auto projected = ProjectPlayerSnapshot(snapshot, 7);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->entityId, 7U);
	EXPECT_EQ(projected->positionX, 11);
	EXPECT_EQ(projected->life, 640);
	EXPECT_EQ(projected->gold, 125U);
	EXPECT_EQ(projected->strength.base, 10);
	EXPECT_EQ(projected->strength.current, 12);
	ASSERT_EQ(projected->inventory.size(), 1U);
	EXPECT_EQ(projected->inventory[0].itemSeed, 42U);
	EXPECT_EQ(projected->inventory[0].item._itype, static_cast<ItemType>(4));
	ASSERT_EQ(projected->equipment.size(), 1U);
	EXPECT_EQ(projected->equipment[0].itemSeed, 77U);
	EXPECT_EQ(projected->inventoryGrid, std::vector<int32_t>({ 0, -1 }));
}

TEST(ServerBackedPlayerSnapshot, RejectsMissingAndDuplicateEntities)
{
	protocol::Snapshot missing;
	EXPECT_FALSE(ProjectPlayerSnapshot(missing, 7).has_value());

	protocol::Snapshot duplicate;
	duplicate.add_players()->set_entity_id(7);
	duplicate.add_players()->set_entity_id(7);
	EXPECT_FALSE(ProjectPlayerSnapshot(duplicate, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, StateRetainsLastValidSnapshotUntilReplacement)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->add_inventory()->set_item_seed(1);
	auto projected = ProjectPlayerSnapshot(snapshot, 7);
	ASSERT_TRUE(projected.has_value()) << projected.error();

	ServerBackedPlayerState state;
	ASSERT_TRUE(state.ApplySnapshot(std::move(*projected)));
	ASSERT_TRUE(state.HasSnapshot());
	ASSERT_NE(state.FindInventoryItem(0), nullptr);
	state.Clear();
	EXPECT_FALSE(state.HasSnapshot());
	EXPECT_EQ(state.FindInventoryItem(0), nullptr);
}

} // namespace
} // namespace devilution::authoritative
