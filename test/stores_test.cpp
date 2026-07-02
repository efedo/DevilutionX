#include <gtest/gtest.h>

#include "game/stores/stores.hpp"

using namespace devilution;

namespace {

/** Helper to find the options vector for a towner by short name, or nullptr. */
std::vector<TownerDialogOption> *FindOptions(std::string_view name)
{
	for (auto &[key, opts] : CurrentStoreManager.extraTownerOptions()) {
		if (key == name)
			return &opts;
	}
	return nullptr;
}

TEST(Stores, AddStoreHoldRepair_magic)
{
	devilution::Item *item;

	item = &CurrentStoreManager.playerItems()[0];

	item->_iMaxDur = 60;
	item->_iDurability = item->_iMaxDur;
	item->_iMagical = ITEM_QUALITY_MAGIC;
	item->_iIdentified = true;
	item->_ivalue = 2000;
	item->_iIvalue = 19000;

	for (int i = 1; i < item->_iMaxDur; i++) {
		item->_ivalue = 2000;
		item->_iIvalue = 19000;
		item->_iDurability = i;
		CurrentStoreManager.currentItemIndex() = 0;
		AddStoreHoldRepair(item, 0);
		EXPECT_EQ(1, CurrentStoreManager.currentItemIndex());
		EXPECT_EQ(95 * (item->_iMaxDur - i) / 2, item->_ivalue);
	}

	item->_iDurability = 59;
	CurrentStoreManager.currentItemIndex() = 0;
	item->_ivalue = 500;
	item->_iIvalue = 30; // To cheap to repair
	AddStoreHoldRepair(item, 0);
	EXPECT_EQ(0, CurrentStoreManager.currentItemIndex());
	EXPECT_EQ(30, item->_iIvalue);
	EXPECT_EQ(500, item->_ivalue);
}

TEST(Stores, AddStoreHoldRepair_normal)
{
	devilution::Item *item;

	item = &CurrentStoreManager.playerItems()[0];

	item->_iMaxDur = 20;
	item->_iDurability = item->_iMaxDur;
	item->_iMagical = ITEM_QUALITY_NORMAL;
	item->_iIdentified = true;
	item->_ivalue = 2000;
	item->_iIvalue = item->_ivalue;

	for (int i = 1; i < item->_iMaxDur; i++) {
		item->_ivalue = 2000;
		item->_iIvalue = item->_ivalue;
		item->_iDurability = i;
		CurrentStoreManager.currentItemIndex() = 0;
		AddStoreHoldRepair(item, 0);
		EXPECT_EQ(1, CurrentStoreManager.currentItemIndex());
		EXPECT_EQ(50 * (item->_iMaxDur - i), item->_ivalue);
	}

	item->_iDurability = 19;
	CurrentStoreManager.currentItemIndex() = 0;
	item->_ivalue = 10; // less than 1 per dur
	item->_iIvalue = item->_ivalue;
	AddStoreHoldRepair(item, 0);
	EXPECT_EQ(1, CurrentStoreManager.currentItemIndex());
	EXPECT_EQ(1, item->_ivalue);
	EXPECT_EQ(1, item->_iIvalue);
}

TEST(Stores, TownerNameForTalkID_knownTowners)
{
	EXPECT_STREQ(TownerNameForTalkID(TalkID::Smith), "griswold");
	EXPECT_STREQ(TownerNameForTalkID(TalkID::Witch), "adria");
	EXPECT_STREQ(TownerNameForTalkID(TalkID::Boy), "wirt");
	EXPECT_STREQ(TownerNameForTalkID(TalkID::Healer), "pepin");
	EXPECT_STREQ(TownerNameForTalkID(TalkID::Storyteller), "cain");
	EXPECT_STREQ(TownerNameForTalkID(TalkID::Tavern), "ogden");
	EXPECT_STREQ(TownerNameForTalkID(TalkID::Drunk), "farnham");
	EXPECT_STREQ(TownerNameForTalkID(TalkID::Barmaid), "gillian");
}

TEST(Stores, TownerNameForTalkID_subPagesReturnNull)
{
	// Sub-pages (buy/sell screens) should not fire StoreOpened
	EXPECT_EQ(TownerNameForTalkID(TalkID::None), nullptr);
	EXPECT_EQ(TownerNameForTalkID(TalkID::SmithBuy), nullptr);
	EXPECT_EQ(TownerNameForTalkID(TalkID::SmithSell), nullptr);
	EXPECT_EQ(TownerNameForTalkID(TalkID::SmithRepair), nullptr);
	EXPECT_EQ(TownerNameForTalkID(TalkID::WitchBuy), nullptr);
	EXPECT_EQ(TownerNameForTalkID(TalkID::Gossip), nullptr);
	EXPECT_EQ(TownerNameForTalkID(TalkID::StorytellerIdentify), nullptr);
	EXPECT_EQ(TownerNameForTalkID(TalkID::StorytellerIdentifyShow), nullptr);
}

TEST(Stores, ClearTownerDialogOptions_removesRegistrations)
{
	CurrentStoreManager.RegisterTownerDialogOption("farnham", []() { return std::string("A"); }, []() {});
	ASSERT_FALSE(CurrentStoreManager.extraTownerOptions().empty());

	CurrentStoreManager.ClearTownerDialogOptions();

	EXPECT_TRUE(CurrentStoreManager.extraTownerOptions().empty());
}

TEST(Stores, RegisterTownerDialogOption_afterClearDoesNotAccumulate)
{
	CurrentStoreManager.RegisterTownerDialogOption("farnham", []() { return std::string("First"); }, []() {});
	CurrentStoreManager.ClearTownerDialogOptions();
	CurrentStoreManager.RegisterTownerDialogOption("farnham", []() { return std::string("Second"); }, []() {});

	auto *opts = FindOptions("farnham");
	ASSERT_NE(opts, nullptr);
	ASSERT_EQ(opts->size(), 1u);
	EXPECT_EQ((*opts)[0].getLabel(), "Second");

	CurrentStoreManager.ClearTownerDialogOptions();
}

TEST(Stores, RegisterTownerDialogOption_storesOption)
{
	CurrentStoreManager.ClearTownerDialogOptions();

	CurrentStoreManager.RegisterTownerDialogOption("farnham", []() { return std::string("Go to Tiny Town"); }, []() {});

	auto *opts = FindOptions("farnham");
	ASSERT_NE(opts, nullptr);
	ASSERT_EQ(opts->size(), 1u);
	EXPECT_EQ((*opts)[0].getLabel(), "Go to Tiny Town");

	CurrentStoreManager.ClearTownerDialogOptions();
}

TEST(Stores, RegisterTownerDialogOption_callsOnSelect)
{
	CurrentStoreManager.ClearTownerDialogOptions();

	bool called = false;
	CurrentStoreManager.RegisterTownerDialogOption("farnham", []() { return std::string("Travel"); }, [&called]() { called = true; });

	auto *opts = FindOptions("farnham");
	ASSERT_NE(opts, nullptr);
	ASSERT_EQ(opts->size(), 1u);
	(*opts)[0].onSelect();
	EXPECT_TRUE(called);

	CurrentStoreManager.ClearTownerDialogOptions();
}

TEST(Stores, RegisterTownerDialogOption_emptyLabelHidesOption)
{
	CurrentStoreManager.ClearTownerDialogOptions();

	CurrentStoreManager.RegisterTownerDialogOption("farnham", []() { return std::string(""); }, []() {});

	auto *opts = FindOptions("farnham");
	ASSERT_NE(opts, nullptr);
	ASSERT_EQ(opts->size(), 1u);
	EXPECT_TRUE((*opts)[0].getLabel().empty());

	CurrentStoreManager.ClearTownerDialogOptions();
}

TEST(Stores, RegisterTownerDialogOption_multipleTowners)
{
	CurrentStoreManager.ClearTownerDialogOptions();

	CurrentStoreManager.RegisterTownerDialogOption("farnham", []() { return std::string("A"); }, []() {});
	CurrentStoreManager.RegisterTownerDialogOption("griswold", []() { return std::string("B"); }, []() {});

	auto *farnhamOpts = FindOptions("farnham");
	auto *griswoldOpts = FindOptions("griswold");
	ASSERT_NE(farnhamOpts, nullptr);
	ASSERT_NE(griswoldOpts, nullptr);
	EXPECT_EQ(farnhamOpts->size(), 1u);
	EXPECT_EQ(griswoldOpts->size(), 1u);
	EXPECT_EQ((*farnhamOpts)[0].getLabel(), "A");
	EXPECT_EQ((*griswoldOpts)[0].getLabel(), "B");

	CurrentStoreManager.ClearTownerDialogOptions();
}

} // namespace
