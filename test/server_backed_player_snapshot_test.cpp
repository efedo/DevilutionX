#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/player_snapshot.hpp"
#include "network/authoritative/server_backed_player_ui.hpp"
#include "network/authoritative/server_backed_world_projection.hpp"

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
	auto *healing = events.add_events()->mutable_healing();
	healing->set_target_entity_id(7);
	healing->set_amount(10);

	ApplyServerBackedEventBatch(player, events, 7);

	EXPECT_EQ(player.life.current, 65);
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

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeMonsters)
{
	protocol::Snapshot snapshot;
	auto *second = snapshot.add_monsters();
	second->set_entity_id(9);
	second->set_monster_id(12);
	second->set_level_id(2);
	second->set_position_x(4);
	second->set_position_y(5);
	second->set_hit_points(7);
	second->set_max_hit_points(20);
	second->set_armor_class(3);
	second->set_alive(true);
	second->set_attack_damage(11);
	second->set_aggro_range(6);
	second->set_fire_resistance(25);
	second->set_lightning_resistance(-10);
	second->set_magic_resistance(40);
	const auto first = snapshot.add_monsters();
	first->set_entity_id(3);
	first->set_hit_points(0);
	first->set_max_hit_points(10);
	first->set_alive(false);

	auto projected = ProjectMonsterSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ((*projected)[0].entityId, 3U);
	EXPECT_FALSE((*projected)[0].alive);
	EXPECT_EQ((*projected)[1].entityId, 9U);
	EXPECT_EQ((*projected)[1].monsterId, 12U);
	EXPECT_EQ((*projected)[1].hitPoints, 7);
	EXPECT_EQ((*projected)[1].attackDamage, 11);
	EXPECT_EQ((*projected)[1].aggroRange, 6);
	EXPECT_EQ((*projected)[1].fireResistance, 25);
	EXPECT_EQ((*projected)[1].lightningResistance, -10);
	EXPECT_EQ((*projected)[1].magicResistance, 40);
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeWorldItems)
{
	devilution::protocol::v1::Snapshot snapshot;
	auto *second = snapshot.add_world_items();
	second->set_entity_id(20);
	second->set_item_seed(42);
	second->set_price(75);
	second->mutable_state()->set_item_type(1);
	auto *first = snapshot.add_world_items();
	first->set_entity_id(10);
	first->set_item_seed(43);
	first->set_price(80);
	first->mutable_state()->set_item_type(1);

	auto projected = ProjectWorldItemSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value());
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ((*projected)[0].entityId, 10U);
	EXPECT_EQ((*projected)[1].entityId, 20U);
	EXPECT_EQ((*projected)[0].itemSeed, 43U);
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeObjects)
{
	protocol::Snapshot snapshot;
	auto *second = snapshot.add_objects();
	second->set_entity_id(20);
	second->set_object_id(2);
	second->set_level_id(1);
	second->set_position_x(8);
	second->set_position_y(9);
	second->set_activated(true);
	second->set_quest_id(30);
	auto *first = snapshot.add_objects();
	first->set_entity_id(10);
	first->set_object_id(1);
	first->set_level_id(1);
	first->set_position_x(4);
	first->set_position_y(4);

	auto projected = ProjectObjectSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value());
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ((*projected)[0].entityId, 10U);
	EXPECT_EQ((*projected)[0].objectId, 1U);
	EXPECT_FALSE((*projected)[0].activated);
	EXPECT_EQ((*projected)[1].entityId, 20U);
	EXPECT_TRUE((*projected)[1].activated);
	EXPECT_EQ((*projected)[1].questId, 30U);
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeProjectiles)
{
	protocol::Snapshot snapshot;
	auto *second = snapshot.add_projectiles();
	second->set_entity_id(12);
	second->set_source_entity_id(1);
	second->set_spell_id(4);
	second->set_remaining_ticks(2);
	second->set_damage(6);
	auto *first = snapshot.add_projectiles();
	first->set_entity_id(11);
	first->set_source_entity_id(1);
	first->set_spell_id(4);
	first->set_remaining_ticks(1);
	first->set_damage(6);

	auto projected = ProjectProjectileSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value());
	ASSERT_EQ(projected->size(), 2u);
	EXPECT_EQ((*projected)[0].entityId, 11u);
	EXPECT_EQ((*projected)[1].entityId, 12u);
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionRetainsEntitiesFromAnotherLevel)
{
	ServerBackedWorldProjection projection;
	ProjectedMonsterSnapshot monster;
	monster.entityId = 7;
	monster.levelId = 2;

	ASSERT_TRUE(projection.Apply({ monster }, {}, {}, 1).has_value());
	ASSERT_EQ(projection.Monsters().size(), 1U);
	EXPECT_EQ(projection.Monsters()[0].levelId, 2U);
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionProvidesStableInteractionLookups)
{
	ServerBackedWorldProjection projection;
	ProjectedMonsterSnapshot monster;
	monster.entityId = 40;
	monster.levelId = 1;
	monster.positionX = 4;
	monster.positionY = 5;
	monster.alive = true;
	ProjectedWorldItemSnapshot item;
	item.entityId = 20;
	item.levelId = 2;
	item.positionX = 4;
	item.positionY = 5;
	ProjectedObjectSnapshot object;
	object.entityId = 30;
	object.levelId = 1;
	object.positionX = 4;
	object.positionY = 5;

	ASSERT_TRUE(projection.Apply({ monster }, { item }, { object }, 1).has_value());
	EXPECT_EQ(projection.MonsterAt(4, 5), std::optional<uint32_t> { 40 });
	EXPECT_FALSE(projection.MonsterAt(5, 5).has_value());
	EXPECT_FALSE(projection.WorldItemAt(4, 5).has_value());
	EXPECT_EQ(projection.ObjectAt(4, 5), std::optional<uint32_t>(30));
	object.activated = true;
	ASSERT_TRUE(projection.Apply({}, { item }, { object }, 1).has_value());
	EXPECT_FALSE(projection.ObjectAt(4, 5).has_value());
	ASSERT_TRUE(projection.Apply({}, { item }, {}, 2).has_value());
	EXPECT_EQ(projection.WorldItemAt(4, 5), std::optional<uint32_t>(20));
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedAuthoritativeObjects)
{
	protocol::Snapshot missingId;
	missingId.add_objects()->set_object_id(1);
	EXPECT_FALSE(ProjectObjectSnapshots(missingId).has_value());

	protocol::Snapshot duplicate;
	duplicate.add_objects()->set_entity_id(7);
	duplicate.mutable_objects(0)->set_object_id(1);
	duplicate.add_objects()->set_entity_id(7);
	duplicate.mutable_objects(1)->set_object_id(2);
	EXPECT_FALSE(ProjectObjectSnapshots(duplicate).has_value());
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
