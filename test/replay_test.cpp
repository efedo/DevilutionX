#include <cstdint>
#include <algorithm>
#include <fstream>
#include <iterator>
#include <string>
#include <vector>

#include <gtest/gtest.h>

#include "game/players/players.hpp"
#include "game/replay/replay.hpp"
#include "game/replay/replay_fixture.hpp"
#include "game/stores/stores.hpp"

namespace devilution {
namespace {

TEST(ReplayStateHasher, EmptyStateUsesSha256)
{
	ReplayStateHasher hasher;

	EXPECT_EQ(hasher.HexDigest(), "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
}

TEST(ReplayStateHasher, CanonicalFieldsAreStable)
{
	ReplayStateHasher first;
	first.AppendBool(true);
	first.AppendUint8(7);
	first.AppendInt32(-42);
	first.AppendUint32(9001);
	first.AppendUint64(123456789);
	first.AppendString("griswold");

	ReplayStateHasher second;
	second.AppendBool(true);
	second.AppendUint8(7);
	second.AppendInt32(-42);
	second.AppendUint32(9001);
	second.AppendUint64(123456789);
	second.AppendString("griswold");

	EXPECT_EQ(first.Digest(), second.Digest());

	second.AppendString("different");
	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayStateHasher, StringsAreLengthPrefixed)
{
	ReplayStateHasher first;
	first.AppendString("ab");

	ReplayStateHasher second;
	second.AppendString("a");
	second.AppendString("b");

	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayCommands, SortsByTargetTickThenServerReceiptSequence)
{
	const std::vector<ReplayCommand> sorted = SortReplayCommands({
	    { .clientSequence = 3, .order = { .targetTick = 2, .serverReceiptSequence = 1 } },
	    { .clientSequence = 1, .order = { .targetTick = 1, .serverReceiptSequence = 5 } },
	    { .clientSequence = 2, .order = { .targetTick = 1, .serverReceiptSequence = 2 } },
	});

	ASSERT_EQ(sorted.size(), 3U);
	EXPECT_EQ(sorted[0].clientSequence, 2U);
	EXPECT_EQ(sorted[1].clientSequence, 1U);
	EXPECT_EQ(sorted[2].clientSequence, 3U);
}

TEST(ReplayCommands, PreservesInputOrderForDuplicateReceiptSequences)
{
	const std::vector<ReplayCommand> sorted = SortReplayCommands({
	    { .clientSequence = 1, .order = { .targetTick = 1, .serverReceiptSequence = 4 } },
	    { .clientSequence = 2, .order = { .targetTick = 1, .serverReceiptSequence = 4 } },
	});

	ASSERT_EQ(sorted.size(), 2U);
	EXPECT_EQ(sorted[0].clientSequence, 1U);
	EXPECT_EQ(sorted[1].clientSequence, 2U);
}

TEST(ReplayStateProjection, ChangesWhenAuthoritativePlayerStateChanges)
{
	Player player{};
	player._pName[0] = 'A';
	player._pName[1] = '\0';
	player._pGold = 100;
	player._pExperience = 200;
	player.life.current = 640;
	player.life.maximum = 640;

	ReplayStateHasher first;
	AppendReplayPlayerState(first, 0, player);

	player._pGold = 101;
	ReplayStateHasher second;
	AppendReplayPlayerState(second, 0, player);

	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayStateProjection, ExcludesLocalizedItemNames)
{
	Item item;
	item._iSeed = 42;
	item._itype = ItemType::Sword;
	item._iIvalue = 100;

	ReplayStateHasher first;
	AppendReplayItemState(first, item);

	item._iName[0] = 'S';
	item._iName[1] = 'w';
	item._iName[2] = 'o';
	item._iName[3] = 'r';
	item._iName[4] = 'd';
	item._iName[5] = '\0';
	item._iIName[0] = 'E';
	item._iIName[1] = '\0';
	ReplayStateHasher second;
	AppendReplayItemState(second, item);

	EXPECT_EQ(first.Digest(), second.Digest());
}

TEST(ReplayStateProjection, IncludesStoreInventoryAndSelection)
{
	StoreManager store;
	store.activeStore() = TalkID::Smith;
	store.premiumItemLevel() = 3;
	store.premiumItems().push_back();
	store.premiumItems()[0]._iSeed = 42;

	ReplayStateHasher first;
	AppendReplayStoreState(first, store);

	store.premiumItems()[0]._iSeed = 43;
	ReplayStateHasher second;
	AppendReplayStoreState(second, store);

	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayFixture, ParsesAndHashesInitialStoreState)
{
	std::ifstream file("test/fixtures/replay/stores/basic-buy.json");
	ASSERT_TRUE(file.is_open());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());

	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.formatVersion, 1U);
	EXPECT_EQ(fixture.fixtureId, "stores/basic-buy");
	EXPECT_EQ(fixture.protocolSchemaVersion, "0.1.0");
	EXPECT_EQ(fixture.tickRateHz, 20U);
	EXPECT_EQ(fixture.rngSeed, 305419896U);
	ASSERT_EQ(fixture.contentManifest.packs.size(), 1U);
	EXPECT_EQ(fixture.contentManifest.packs[0], "baseline-store-content");
	ASSERT_EQ(fixture.commands.size(), 2U);
	EXPECT_EQ(fixture.commands[0].clientSequence, 2U);
	EXPECT_EQ(fixture.commands[0].kind, "BuyItem");
	EXPECT_EQ(fixture.commands[0].storeId, 1U);
	EXPECT_EQ(fixture.commands[0].storeSlot, 0U);
	EXPECT_EQ(fixture.commands[1].storeId, 1U);
	EXPECT_EQ(fixture.commands[1].storeSlot, 0U);
	ASSERT_EQ(fixture.checkpoints.size(), 1U);
	EXPECT_EQ(fixture.checkpoints[0].tick, 0U);
	EXPECT_EQ(fixture.checkpoints[0].stateSha256, "67c0e197eb04c359e6501c0df7419799d878903d6760c471d0eae93dd12c45be");

	const std::vector<ReplayCommand> sorted = SortReplayCommands({
	    { .clientSequence = fixture.commands[0].clientSequence, .order = fixture.commands[0].order },
	    { .clientSequence = fixture.commands[1].clientSequence, .order = fixture.commands[1].order },
	});
	ASSERT_EQ(sorted.size(), 2U);
	EXPECT_EQ(sorted[0].clientSequence, 1U);
	EXPECT_EQ(sorted[1].clientSequence, 2U);

	Player player{};
	player._pName[0] = fixture.initialState.player[0];
	player._pName[1] = '\0';
	player._pClass = static_cast<HeroClass>(fixture.initialState.characterClass);
	ASSERT_EQ(fixture.initialState.characterLevel, player.getCharacterLevel());
	player._pGold = fixture.initialState.gold;
	player._pExperience = fixture.initialState.experience;
	player.life.current = fixture.initialState.life;
	player.life.maximum = fixture.initialState.life;
	player.mana.current = fixture.initialState.mana;
	player.mana.maximum = fixture.initialState.mana;
	StoreManager store;
	store.activeStore() = static_cast<TalkID>(fixture.storeState.activeStore);
	store.premiumItemCount() = fixture.storeState.premiumItemCount;
	store.premiumItemLevel() = fixture.storeState.premiumItemLevel;
	for (const uint32_t seed : fixture.storeState.premiumItemSeeds) {
		store.premiumItems().push_back();
		store.premiumItems().back()._iSeed = seed;
	}

	ReplayStateHasher state;
	AppendReplayPlayerState(state, 0, player);
	AppendReplayStoreState(state, store);
	EXPECT_EQ(fixture.initialStateSha256, state.HexDigest());
}

TEST(ReplayFixture, ParsesStructuredManifestCheckpointAndStorePayloads)
{
	constexpr std::string_view Fixture = R"({
  "content_manifest": {
    "id": "base-plus-hellfire",
    "version": "1",
    "sha256": "content-hash"
  },
  "commands": [
    {
      "client_sequence": 1,
      "target_tick": 4,
      "server_receipt_sequence": 1,
      "kind": "OpenStore",
      "payload": { "store_id": 7 }
    },
    {
      "client_sequence": 2,
      "target_tick": 5,
      "server_receipt_sequence": 2,
      "payload": { "store_id": 7, "item_index": 3 },
      "kind": "BuyItem"
    }
  ],
  "checkpoints": [
    { "tick": 0, "state_sha256": "initial-hash" },
    { "tick": 5, "state_sha256": "purchase-hash" }
  ]
})";

	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(Fixture, fixture, error)) << error;
	EXPECT_EQ(fixture.contentManifest.id, "base-plus-hellfire");
	EXPECT_EQ(fixture.contentManifest.version, "1");
	EXPECT_EQ(fixture.contentManifest.sha256, "content-hash");
	EXPECT_TRUE(fixture.contentManifest.packs.empty());
	ASSERT_EQ(fixture.commands.size(), 2U);
	EXPECT_EQ(fixture.commands[0].storeId, 7U);
	EXPECT_EQ(fixture.commands[0].storeSlot, 0U);
	EXPECT_EQ(fixture.commands[1].storeId, 7U);
	EXPECT_EQ(fixture.commands[1].storeSlot, 3U);
	ASSERT_EQ(fixture.checkpoints.size(), 2U);
	EXPECT_EQ(fixture.checkpoints[0].tick, 0U);
	EXPECT_EQ(fixture.checkpoints[0].stateSha256, "initial-hash");
	EXPECT_EQ(fixture.checkpoints[1].tick, 5U);
	EXPECT_EQ(fixture.checkpoints[1].stateSha256, "purchase-hash");
	EXPECT_EQ(fixture.initialStateSha256, "initial-hash");
}

TEST(ReplayFixture, ParsesTransactionParityCommands)
{
	std::ifstream file("test/fixtures/replay/stores/transaction-parity.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;
	EXPECT_EQ(fixture.initialState.manaMaximum, 640);
	ASSERT_EQ(fixture.commands.size(), 5U);
	ASSERT_EQ(fixture.checkpoints.size(), 5U);
	EXPECT_EQ(fixture.checkpoints[0].tick, 10U);
	EXPECT_EQ(fixture.checkpoints[1].tick, 12U);
	EXPECT_EQ(fixture.checkpoints[2].tick, 14U);
	EXPECT_EQ(fixture.checkpoints[3].tick, 15U);
	EXPECT_EQ(fixture.checkpoints[4].tick, 16U);
	EXPECT_EQ(fixture.checkpoints[0].stateSha256, "be0daa30bd593e5db72615b4c4ada959146c0ab140735a8d82376f2de170ae64");
	EXPECT_EQ(fixture.checkpoints[4].stateSha256, "8e703c723fb1217eb74b1a3bb4a3134b86fd441bf5f40abbd6152fe9c5d68987");
	EXPECT_EQ(fixture.commands[2].kind, "SellItem");
	EXPECT_EQ(fixture.commands[2].storeSlot, 0U);
	EXPECT_EQ(fixture.commands[3].kind, "OpenStore");
	EXPECT_EQ(fixture.commands[3].storeId, 10U);
	EXPECT_EQ(fixture.commands[4].kind, "RefillMana");
}

TEST(ReplayFixture, ExecutesTransactionTransitionsAndMatchesCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/stores/transaction-parity.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.lifeMaximum = fixture.initialState.life;
	state.characterLevel = fixture.initialState.characterLevel;
	state.gold = fixture.initialState.gold;
	state.experience = fixture.initialState.experience;
	ReplayFixtureItemState itemState;
	itemState.itemType = 1;
	itemState.value = 75;
	itemState.identifiedValue = 75;
	itemState.durability = 1;
	itemState.maxDurability = 1;
	state.activeStoreItems.push_back({ 0, 42, 75, itemState });

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 5U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.gold, 33U);
	EXPECT_EQ(state.mana, 640);
	EXPECT_TRUE(state.inventory.empty());
	EXPECT_EQ(result.transitions.back().stateSha256, fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesBaseContentPurchaseAndSaleAgainstCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/stores/base-content-purchase.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.lifeMaximum = fixture.initialState.life;
	state.characterLevel = fixture.initialState.characterLevel;
	state.gold = fixture.initialState.gold;
	state.experience = fixture.initialState.experience;
	ReplayFixtureItemState itemState;
	itemState.createInfo = 42;
	itemState.itemType = 1;
	itemState.value = 75;
	itemState.identifiedValue = 75;
	itemState.identified = true;
	itemState.itemIndex = 1;
	itemState.minDamage = 4;
	itemState.maxDamage = 8;
	itemState.durability = 20;
	itemState.maxDurability = 20;
	state.activeStoreItems.push_back({ 0, 42, 75, itemState });
	ReplayFixtureItemState armorState;
	armorState.createInfo = 43;
	armorState.itemType = 1;
	armorState.value = 25;
	armorState.identifiedValue = 25;
	armorState.identified = true;
	armorState.itemIndex = 2;
	armorState.armorClass = 5;
	armorState.durability = 20;
	armorState.maxDurability = 20;
	state.activeStoreItems.push_back({ 1, 43, 25, armorState });

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 3U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.gold, 43U);
	for (size_t index = 0; index < result.transitions.size(); ++index)
		EXPECT_EQ(result.transitions[index].stateSha256, fixture.checkpoints[index].stateSha256);
}

TEST(ReplayFixture, ExecutesGameplayTransitionsAndMatchesCSharpCheckpoints)
{
	std::ifstream file("test/fixtures/replay/gameplay-movement-combat.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.lifeMaximum = fixture.initialState.life;
	state.characterLevel = fixture.initialState.characterLevel;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.combatTargetEntityId = 9;
	state.combatTargetPositionX = 2;
	state.combatTargetPositionY = 0;
	state.combatTargetHitPoints = 11;
	state.combatTargetMaxHitPoints = 11;
	state.combatTargetArmorClass = 2;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 3U);
	EXPECT_TRUE(std::all_of(result.transitions.begin(), result.transitions.end(), [](const auto &transition) {
		return transition.accepted;
	}));
	EXPECT_EQ(state.positionX, 1);
	EXPECT_EQ(state.experience, 100U);
	EXPECT_EQ(result.transitions.back().stateSha256, fixture.checkpoints.back().stateSha256);
}

TEST(ReplayFixture, ExecutesDataDrivenSpellTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-spell-cast.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = 40;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.life, 40);
	EXPECT_EQ(state.mana, 5);
}

TEST(ReplayFixture, ExecutesPortalLevelTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-portal-transition.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.portalId = 5;
	state.portalDestinationLevelId = 2;
	state.portalDestinationX = 3;
	state.portalDestinationY = 4;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.levelId, 2U);
	EXPECT_EQ(state.positionX, 3);
	EXPECT_EQ(state.positionY, 4);
}

TEST(ReplayFixture, RetainsEntitiesOnTheirOriginalLevelAcrossPortalTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-multi-level-occupancy.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = 2;
	state.worldItemPositionY = 0;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;
	state.objectEntityId = fixture.initialState.objectEntityId;
	state.objectId = fixture.initialState.objectId;
	state.objectLevelId = fixture.initialState.levelId;
	state.objectPositionX = fixture.initialState.objectPositionX;
	state.objectPositionY = fixture.initialState.objectPositionY;
	state.portalId = 5;
	state.portalDestinationLevelId = 2;
	state.portalDestinationX = 3;
	state.portalDestinationY = 4;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_EQ(state.levelId, 2U);
	EXPECT_EQ(state.worldItemEntityId, 20U);
	EXPECT_EQ(state.worldItemLevelId, 1U);
	EXPECT_EQ(state.objectEntityId, 30U);
	EXPECT_EQ(state.objectLevelId, 1U);
}

TEST(ReplayFixture, ExecutesWorldItemPickupTransition)
{
	std::ifstream file("test/fixtures/replay/gameplay-world-item-pickup.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.inventoryGrid.assign(40, -1);
	state.worldItemEntityId = fixture.initialState.worldItemEntityId;
	state.worldItemLevelId = fixture.initialState.levelId;
	state.worldItemPositionX = fixture.initialState.positionX + 1;
	state.worldItemPositionY = fixture.initialState.positionY;
	state.worldItemSeed = fixture.initialState.worldItemSeed;
	state.worldItemPrice = fixture.initialState.worldItemPrice;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 1U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(state.worldItemEntityId == 0);
	ASSERT_EQ(state.inventory.size(), 1U);
	EXPECT_EQ(state.inventory[0].itemSeed, fixture.initialState.worldItemSeed);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
}

TEST(ReplayFixture, ExecutesObjectQuestTransitions)
{
	std::ifstream file("test/fixtures/replay/gameplay-object-quest.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.levelId = fixture.initialState.levelId;
	state.positionX = fixture.initialState.positionX;
	state.positionY = fixture.initialState.positionY;
	state.objectEntityId = fixture.initialState.objectEntityId;
	state.objectId = fixture.initialState.objectId;
	state.objectLevelId = fixture.initialState.levelId;
	state.objectPositionX = fixture.initialState.objectPositionX;
	state.objectPositionY = fixture.initialState.objectPositionY;
	state.questId = fixture.initialState.questId;
	state.questLevelId = fixture.initialState.levelId;
	state.questRequiredProgress = fixture.initialState.questRequiredProgress;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_TRUE(state.objectActivated);
	EXPECT_EQ(state.questProgress, 1U);
	EXPECT_TRUE(state.questCompleted);
	EXPECT_EQ(result.transitions[0].stateSha256, fixture.checkpoints[0].stateSha256);
	EXPECT_EQ(result.transitions[1].stateSha256, fixture.checkpoints[1].stateSha256);
}

TEST(ReplayFixture, ExecutesStatusExpiryByAuthoritativeTick)
{
	std::ifstream file("test/fixtures/replay/gameplay-status-expiry.json");
	ASSERT_TRUE(file.good());
	const std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
	ReplayFixture fixture;
	std::string error;
	ASSERT_TRUE(ParseReplayFixture(json, fixture, error)) << error;

	ReplayFixtureExecutionState state;
	state.life = fixture.initialState.life;
	state.lifeMaximum = fixture.initialState.life;
	state.mana = fixture.initialState.mana;
	state.manaMaximum = fixture.initialState.manaMaximum;
	state.spellStatusEffectId = fixture.initialState.statusEffectId;
	state.spellStatusDuration = fixture.initialState.statusDuration;
	state.spellStatusMagnitude = fixture.initialState.statusMagnitude;

	ReplayFixtureExecutionResult result;
	ASSERT_TRUE(ExecuteReplayFixture(fixture, state, result, error)) << error;
	ASSERT_EQ(result.transitions.size(), 2U);
	EXPECT_TRUE(result.transitions[0].accepted);
	EXPECT_TRUE(result.transitions[1].accepted);
	EXPECT_EQ(state.positionX, 1);
	EXPECT_TRUE(state.statusEffects.empty());
}

TEST(ReplayFixture, RejectsMalformedJson)
{
	ReplayFixture fixture;
	std::string error;

	EXPECT_FALSE(ParseReplayFixture(R"({ "format_version": 1, "commands": [ })", fixture, error));
	EXPECT_FALSE(error.empty());
}

} // namespace
} // namespace devilution
