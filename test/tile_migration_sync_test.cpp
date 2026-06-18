#include <gtest/gtest.h>

#include "levels/gendung.h"
#include "levels/level.hpp"
#include "levels/town.h"

namespace devilution {
namespace {

void SelectTownLevel()
{
	currentLevel().setId(Level::create(LevelId { .levelNum = 0, .type = DTYPE_TOWN }).id());
}

TEST(TileMigrationSyncTest, TileHasAnyUsesTilePiece)
{
	SelectTownLevel();
	InitLevels();

	constexpr Point Pos { 16, 16 };
	tileAt(Pos).setPiece(230);
	SOLData[230] = TileProperties::Solid;

	EXPECT_TRUE(TileHasAny(Pos, TileProperties::Solid));
}

TEST(TileMigrationSyncTest, TransparencySyncPreservesLoadedTilePiece)
{
	SelectTownLevel();
	InitLevels();

	constexpr Point Pos { 20, 20 };
	tileAt(Pos).setPiece(321);
	tileAt(Pos).setTransVal(0);
	dTransVal[Pos.x][Pos.y] = 7;

	SyncTileTransparencyFromLegacyMapForTesting();

	EXPECT_EQ(tileAt(Pos).piece(), 321);
	EXPECT_EQ(tileAt(Pos).transVal(), 7);
}

TEST(TileMigrationSyncTest, TownOpenHivePopulatesTilePieces)
{
	SelectTownLevel();
	InitLevels();

	TownOpenHive();

	EXPECT_EQ(tileAt(78, 60).piece(), 0x489);
	EXPECT_EQ(tileAt(79, 61).piece(), 0x50d);
	EXPECT_EQ(tileAt(86, 61).piece(), 17);
}

TEST(TileMigrationSyncTest, TownOpenGravePopulatesTilePieces)
{
	SelectTownLevel();
	InitLevels();

	TownOpenGrave();

	EXPECT_EQ(tileAt(36, 21).piece(), 0x532);
	EXPECT_EQ(tileAt(37, 24).piece(), 0x539);
	EXPECT_EQ(tileAt(34, 21).piece(), 0x53b);
}

} // namespace
} // namespace devilution
