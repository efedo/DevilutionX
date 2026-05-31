#include <gtest/gtest.h>

#include "levels/gendung.h"
#include "levels/level.hpp"

namespace devilution {

TEST(TileMigrationSyncTest, TileHasAnyFallsBackToLegacyPiece)
{
	SwitchCurrentLevel({ .levelNum = 0, .type = DTYPE_TOWN, .isSetLevel = false, .setLevelId = SL_NONE });
	InitLevels();

	constexpr Point Pos { 16, 16 };
	tileAt(Pos).setPiece(0);
	dPiece[Pos.x][Pos.y] = 230;
	SOLData[230] = TileProperties::Solid;

	EXPECT_TRUE(TileHasAny(Pos, TileProperties::Solid));
}

TEST(TileMigrationSyncTest, SyncTilesFromLegacyMapsCopiesPieceAndTransVal)
{
	SwitchCurrentLevel({ .levelNum = 0, .type = DTYPE_TOWN, .isSetLevel = false, .setLevelId = SL_NONE });
	InitLevels();

	constexpr Point Pos { 20, 20 };
	tileAt(Pos).setPiece(0);
	tileAt(Pos).setTransVal(0);
	dPiece[Pos.x][Pos.y] = 321;
	dTransVal[Pos.x][Pos.y] = 7;

	SyncTilesFromLegacyMapsForTesting();

	EXPECT_EQ(tileAt(Pos).piece(), 321);
	EXPECT_EQ(tileAt(Pos).transVal(), 7);
}

TEST(TileMigrationSyncTest, TownCreateDungeonKeepsTileAndLegacyPiecesInSync)
{
	SwitchCurrentLevel({ .levelNum = 0, .type = DTYPE_TOWN, .isSetLevel = false, .setLevelId = SL_NONE });
	InitLevels();
	CreateDungeon(/*rseed=*/0x12345678, ENTRY_MAIN);

	int legacyPieceCount = 0;
	int desyncCount = 0;
	for (int x = 0; x < MAXDUNX; ++x) {
		for (int y = 0; y < MAXDUNY; ++y) {
			if (dPiece[x][y] != 0) {
				++legacyPieceCount;
				if (tileAt(x, y).piece() == 0)
					++desyncCount;
			}
		}
	}

	EXPECT_GT(legacyPieceCount, 0);
	EXPECT_EQ(desyncCount, 0);
}

TEST(TileMigrationSyncTest, TownCreateDungeonProducesSolidCollisionTiles)
{
	SwitchCurrentLevel({ .levelNum = 0, .type = DTYPE_TOWN, .isSetLevel = false, .setLevelId = SL_NONE });
	InitLevels();
	CreateDungeon(/*rseed=*/0x87654321, ENTRY_MAIN);

	int solidCount = 0;
	for (int x = 0; x < MAXDUNX; ++x) {
		for (int y = 0; y < MAXDUNY; ++y) {
			if (TileHasAny({ x, y }, TileProperties::Solid))
				++solidCount;
		}
	}

	EXPECT_GT(solidCount, 0);
}

TEST(TileMigrationSyncTest, TownEntryLoadKeepsTileAndLegacyPiecesInSync)
{
	SwitchCurrentLevel({ .levelNum = 0, .type = DTYPE_TOWN, .isSetLevel = false, .setLevelId = SL_NONE });
	InitLevels();

	// Seed a load-style legacy-only state.
	constexpr Point SeedPosA { 16, 16 };
	constexpr Point SeedPosB { 18, 20 };
	dPiece[SeedPosA.x][SeedPosA.y] = 230;
	dPiece[SeedPosB.x][SeedPosB.y] = 231;
	dTransVal[SeedPosA.x][SeedPosA.y] = 3;
	tileAt(SeedPosA).setPiece(0);
	tileAt(SeedPosB).setPiece(0);
	tileAt(SeedPosA).setTransVal(0);

	CreateDungeon(/*rseed=*/0x13572468, ENTRY_LOAD);

	int legacyPieceCount = 0;
	int desyncCount = 0;
	for (int x = 0; x < MAXDUNX; ++x) {
		for (int y = 0; y < MAXDUNY; ++y) {
			if (dPiece[x][y] != 0) {
				++legacyPieceCount;
				if (tileAt(x, y).piece() == 0)
					++desyncCount;
			}
		}
	}

	EXPECT_GT(legacyPieceCount, 0);
	EXPECT_EQ(desyncCount, 0);

	// Focused regression guard: property lookups must still work on a tile
	// that started as legacy-only state before ENTRY_LOAD sync.
	const uint16_t loadedPiece = dPiece[SeedPosA.x][SeedPosA.y];
	ASSERT_NE(loadedPiece, 0);
	SOLData[loadedPiece] = TileProperties::Solid;
	EXPECT_TRUE(TileHasAny(SeedPosA, TileProperties::Solid));

	const uint16_t loadedTransparentPiece = dPiece[SeedPosB.x][SeedPosB.y];
	ASSERT_NE(loadedTransparentPiece, 0);
	SOLData[loadedTransparentPiece] = TileProperties::Transparent;
	EXPECT_TRUE(TileHasAny(SeedPosB, TileProperties::Transparent));
}

} // namespace devilution
