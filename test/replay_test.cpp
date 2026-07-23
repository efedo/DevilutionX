#include <cstdint>
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

TEST(ReplayFixture, RejectsMalformedJson)
{
	ReplayFixture fixture;
	std::string error;

	EXPECT_FALSE(ParseReplayFixture(R"({ "format_version": 1, "commands": [ })", fixture, error));
	EXPECT_FALSE(error.empty());
}

} // namespace
} // namespace devilution
