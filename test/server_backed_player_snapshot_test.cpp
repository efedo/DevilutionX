#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/player_snapshot.hpp"
#include "network/authoritative/server_backed_player_ui.hpp"

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
	player->set_life_maximum(800);
	player->set_mana(32);
	player->set_mana_maximum(640);
	player->set_gold(125);
	player->set_experience(99);
	player->set_character_level(3);
	player->set_level_id(7);
	player->add_status_effects()->set_effect_id(1);
	player->mutable_status_effects(0)->set_remaining_ticks(10);
	player->mutable_status_effects(0)->set_magnitude(1);
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
	auto *belt = player->add_belt();
	belt->set_slot(2);
	belt->set_item_seed(88);
	belt->mutable_state()->set_item_type(6);

	auto projected = ProjectPlayerSnapshot(snapshot, 7);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->entityId, 7U);
	EXPECT_EQ(projected->positionX, 11);
	EXPECT_EQ(projected->life, 640);
	EXPECT_EQ(projected->lifeMaximum, 800);
	EXPECT_EQ(projected->manaMaximum, 640);
	EXPECT_EQ(projected->gold, 125U);
	EXPECT_EQ(projected->characterLevel, 3U);
	EXPECT_EQ(projected->levelId, 7U);
	ASSERT_EQ(projected->statusEffects.size(), 1U);
	EXPECT_EQ(projected->statusEffects[0].remainingTicks, 10U);
	EXPECT_EQ(projected->strength.base, 10);
	EXPECT_EQ(projected->strength.current, 12);
	ASSERT_EQ(projected->inventory.size(), 1U);
	EXPECT_EQ(projected->inventory[0].itemSeed, 42U);
	EXPECT_EQ(projected->inventory[0].item._itype, static_cast<ItemType>(4));
	ASSERT_EQ(projected->equipment.size(), 1U);
	EXPECT_EQ(projected->equipment[0].itemSeed, 77U);
	ASSERT_EQ(projected->belt.size(), 1U);
	EXPECT_EQ(projected->belt[0].slot, 2U);
	EXPECT_EQ(projected->belt[0].itemSeed, 88U);
	EXPECT_EQ(projected->inventoryGrid, std::vector<int32_t>({ 0, -1 }));
}

TEST(ServerBackedPlayerSnapshot, AppliesAuthoritativeEventBatchToNativeResources)
{
	Player player;
	player._pExperience = 0;
	player.life.maximum = 100;
	player.life.maximumBase = 100;
	player.life.current = 80;
	protocol::EventBatch events;
	auto *damage = events.add_events()->mutable_damage();
	damage->set_target_entity_id(7);
	damage->set_amount(25);
	auto *experience = events.add_events()->mutable_experience();
	experience->set_player_entity_id(7);
	experience->set_amount(40);

	ApplyServerBackedEventBatch(player, events, 7);

	EXPECT_EQ(player.life.current, 55);
	EXPECT_EQ(player._pExperience, 40U);
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

TEST(ServerBackedPlayerSnapshot, AppliesResourcesEquipmentInventoryAndGrid)
{
	Player player;
	player.life.maximum = 1000;
	player.life.maximumBase = 1000;
	player.mana.maximum = 500;
	player.mana.maximumBase = 500;
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.positionX = 12;
	snapshot.positionY = 13;
	snapshot.life = 800;
	snapshot.lifeMaximum = 1000;
	snapshot.mana = 125;
	snapshot.manaMaximum = 750;
	snapshot.gold = 450;
	snapshot.experience = 99;
	snapshot.characterLevel = 4;
	snapshot.strength = { 10, 12 };
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	Item inventoryItem;
	inventoryItem._itype = ItemType::Sword;
	snapshot.inventory.push_back({ .item = inventoryItem });
	snapshot.inventoryGrid[0] = 0;
	Item equippedItem;
	equippedItem._itype = ItemType::Shield;
	snapshot.equipment.push_back({ .slot = INVLOC_HAND_LEFT, .item = equippedItem });
	Item beltItem;
	beltItem._itype = ItemType::Misc;
	snapshot.belt.push_back({ .slot = 2, .item = beltItem });

	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player.position.tile.x, 12);
	EXPECT_EQ(player.position.tile.y, 13);
	EXPECT_EQ(player._pGold, 450);
	EXPECT_EQ(player._pExperience, 99U);
	EXPECT_EQ(player.attributes.strength.base, 10);
	EXPECT_EQ(player.attributes.strength.current, 12);
	EXPECT_EQ(player.life.current, 800);
	EXPECT_EQ(player.life.maximum, 1000);
	EXPECT_EQ(player.mana.current, 125);
	EXPECT_EQ(player.mana.maximum, 750);
	EXPECT_EQ(player.SpdList[2]._itype, ItemType::Misc);
	EXPECT_EQ(player._pNumInv, 1);
	EXPECT_EQ(player.InvList[0]._itype, ItemType::Sword);
	EXPECT_EQ(player.InvBody[INVLOC_HAND_LEFT]._itype, ItemType::Shield);
	EXPECT_EQ(player.InvGrid[0], 1);
	EXPECT_EQ(player.InvGrid[1], 0);
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedSnapshotWithoutMutation)
{
	Player player;
	player._pGold = 42;
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	snapshot.inventoryGrid[0] = 3;

	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player._pGold, 42);
}

TEST(ServerBackedPlayerSnapshot, RejectsDuplicateEquipmentSlots)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	snapshot.equipment.push_back({ .slot = INVLOC_HEAD });
	snapshot.equipment.push_back({ .slot = INVLOC_HEAD });

	Player player;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
}

} // namespace
} // namespace devilution::authoritative
