#include <gtest/gtest.h>

#include "engine/assets.hpp"
#include "spells.h"
#include "tables/spelldat.h"

namespace devilution {
namespace {

TEST(SpellsTest, IsValidSpell)
{
	LoadSpellData();

	EXPECT_FALSE(IsValidSpell(SpellID::Null));
	EXPECT_TRUE(IsValidSpell(SpellID::Firebolt));
	EXPECT_TRUE(IsValidSpell(SpellID::Healing));
	EXPECT_TRUE(IsValidSpell(SpellID::Fireball));
	EXPECT_FALSE(IsValidSpell(SpellID::Invalid));
	EXPECT_FALSE(IsValidSpell(static_cast<SpellID>(-2)));
}

TEST(SpellsTest, IsWallSpell)
{
	EXPECT_TRUE(IsWallSpell(SpellID::FireWall));
	EXPECT_TRUE(IsWallSpell(SpellID::LightningWall));
	EXPECT_FALSE(IsWallSpell(SpellID::Firebolt));
	EXPECT_FALSE(IsWallSpell(SpellID::Healing));
}

TEST(SpellsTest, TargetsMonster)
{
	EXPECT_TRUE(TargetsMonster(SpellID::Fireball));
	EXPECT_TRUE(TargetsMonster(SpellID::FireWall));
	EXPECT_TRUE(TargetsMonster(SpellID::Lightning));
	EXPECT_FALSE(TargetsMonster(SpellID::Firebolt));
	EXPECT_FALSE(TargetsMonster(SpellID::Healing));
	EXPECT_FALSE(TargetsMonster(SpellID::TownPortal));
}

TEST(SpellsTest, GetSpellBitmask)
{
	EXPECT_EQ(GetSpellBitmask(SpellID::Firebolt), 1ULL << 0);
	EXPECT_EQ(GetSpellBitmask(SpellID::Healing), 1ULL << 1);
}

TEST(SpellsTest, GetSpellBookLevel)
{
	LoadSpellData();

	EXPECT_GE(GetSpellBookLevel(SpellID::Firebolt), 1);
	EXPECT_GE(GetSpellBookLevel(SpellID::Healing), 1);
}

TEST(SpellsTest, GetSpellStaffLevel)
{
	LoadSpellData();

	EXPECT_GE(GetSpellStaffLevel(SpellID::Firebolt), 1);
	EXPECT_GE(GetSpellStaffLevel(SpellID::Healing), 1);
}

} // namespace
} // namespace devilution
