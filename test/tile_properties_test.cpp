#include "game/levels/tile_properties.hpp"

#include <gmock/gmock.h>
#include <gtest/gtest.h>

#include "game/levels/dun_tile.hpp"
#include "game/levels/dungeon_common.h"
#include "game/objects/objects.hpp"
#include "tables/objdat.h"

namespace devilution {
namespace {

TEST(TilePropertiesTest, Solid)
{
	tileAt(Point { 5, 5 }).setPiece(0);
	tileProperties()[0] = TileProperties::Solid;
	EXPECT_TRUE(IsTileSolid({ 5, 5 })) << "Solid in-bounds tiles are solid";
	EXPECT_FALSE(IsTileNotSolid({ 5, 5 })) << "IsTileNotSolid returns the inverse of IsTileSolid for in-bounds tiles";

	tileAt(Point { 6, 6 }).setPiece(1);
	tileProperties()[1] = TileProperties::None;
	EXPECT_FALSE(IsTileSolid({ 6, 6 })) << "Non-solid in-bounds tiles are not solid";
	EXPECT_TRUE(IsTileNotSolid({ 6, 6 })) << "IsTileNotSolid returns the inverse of IsTileSolid for in-bounds tiles";

	EXPECT_FALSE(IsTileSolid({ -1, 1 })) << "Out of bounds tiles are not solid";
	EXPECT_FALSE(IsTileNotSolid({ -1, 1 })) << "Out of bounds tiles are also not not solid";
}

TEST(TilePropertiesTest, Walkable)
{
	tileAt(Point { 5, 5 }).setPiece(0);
	tileProperties()[0] = TileProperties::Solid; // Doing this manually to save running through the code in dungeon_common.cpp
	EXPECT_FALSE(IsTileWalkable({ 5, 5 })) << "Tile which is marked as solid should be considered blocked";
	EXPECT_FALSE(IsTileWalkable({ 5, 5 }, true)) << "Solid non-door tiles remain unwalkable when ignoring doors";

	tileProperties()[0] = TileProperties::None;
	EXPECT_TRUE(IsTileWalkable({ 5, 5 })) << "Non-solid tiles are walkable";
	EXPECT_TRUE(IsTileWalkable({ 5, 5 }, true)) << "Non-solid tiles remain walkable when ignoring doors";

	tileAt(Point { 5, 5 }).setObject(1);
	Objects[0]._oSolidFlag = true;
	EXPECT_FALSE(IsTileWalkable({ 5, 5 })) << "Tile occupied by a solid object is unwalkable";
	EXPECT_FALSE(IsTileWalkable({ 5, 5 }, true)) << "Tile occupied by a solid non-door object are unwalkable when ignoring doors";

	Objects[0]._otype = _object_id::OBJ_L1LDOOR;
	EXPECT_FALSE(IsTileWalkable({ 5, 5 })) << "Tile occupied by a door which is marked as solid should be considered blocked";
	EXPECT_TRUE(IsTileWalkable({ 5, 5 }, true)) << "Tile occupied by a door is considered walkable when ignoring doors";

	Objects[0]._oSolidFlag = false;
	EXPECT_TRUE(IsTileWalkable({ 5, 5 })) << "Tile occupied by an open door is walkable";
	EXPECT_TRUE(IsTileWalkable({ 5, 5 }, true)) << "Tile occupied by a door is considered walkable when ignoring doors";

	tileProperties()[0] = TileProperties::Solid;
	EXPECT_FALSE(IsTileWalkable({ 5, 5 })) << "Solid tiles occupied by an open door remain unwalkable";
	EXPECT_TRUE(IsTileWalkable({ 5, 5 }, true)) << "Solid tiles occupied by an open door become walkable when ignoring doors";
}

TEST(TilePropertiesTest, CanStepTest)
{
	tileAt(Point { 0, 0 }).setPiece(0);
	tileAt(Point { 0, 1 }).setPiece(0);
	tileAt(Point { 1, 0 }).setPiece(0);
	tileAt(Point { 1, 1 }).setPiece(0);
	tileProperties()[0] = TileProperties::None;
	EXPECT_TRUE(CanStep({ 0, 0 }, { 1, 1 })) << "A step in open space is free of solid pieces";
	EXPECT_TRUE(CanStep({ 1, 1 }, { 0, 0 })) << "A step in open space is free of solid pieces";
	EXPECT_TRUE(CanStep({ 1, 0 }, { 0, 1 })) << "A step in open space is free of solid pieces";
	EXPECT_TRUE(CanStep({ 0, 1 }, { 1, 0 })) << "A step in open space is free of solid pieces";

	tileProperties()[1] = TileProperties::Solid;
	tileAt(Point { 1, 0 }).setPiece(1);
	EXPECT_TRUE(CanStep({ 0, 1 }, { 1, 0 })) << "Can path to a destination which is solid";
	EXPECT_TRUE(CanStep({ 1, 0 }, { 0, 1 })) << "Can path from a starting position which is solid";
	EXPECT_TRUE(CanStep({ 0, 1 }, { 1, 1 })) << "Stepping in a cardinal direction ignores solid pieces";
	EXPECT_TRUE(CanStep({ 1, 0 }, { 1, 1 })) << "Stepping in a cardinal direction ignores solid pieces";
	EXPECT_TRUE(CanStep({ 0, 0 }, { 1, 0 })) << "Stepping in a cardinal direction ignores solid pieces";
	EXPECT_TRUE(CanStep({ 1, 1 }, { 1, 0 })) << "Stepping in a cardinal direction ignores solid pieces";

	EXPECT_FALSE(CanStep({ 0, 0 }, { 1, 1 })) << "Can't cut a solid corner";
	EXPECT_FALSE(CanStep({ 1, 1 }, { 0, 0 })) << "Can't cut a solid corner";
	tileAt(Point { 0, 1 }).setPiece(1);
	EXPECT_FALSE(CanStep({ 0, 0 }, { 1, 1 })) << "Can't walk through the boundary between two corners";
	EXPECT_FALSE(CanStep({ 1, 1 }, { 0, 0 })) << "Can't walk through the boundary between two corners";
	tileAt(Point { 1, 0 }).setPiece(0);
	EXPECT_FALSE(CanStep({ 0, 0 }, { 1, 1 })) << "Can't cut a solid corner";
	EXPECT_FALSE(CanStep({ 1, 1 }, { 0, 0 })) << "Can't cut a solid corner";
	tileAt(Point { 0, 1 }).setPiece(0);

	tileAt(Point { 0, 0 }).setPiece(1);
	EXPECT_FALSE(CanStep({ 1, 0 }, { 0, 1 })) << "Can't cut a solid corner";
	EXPECT_FALSE(CanStep({ 0, 1 }, { 1, 0 })) << "Can't cut a solid corner";
	tileAt(Point { 1, 1 }).setPiece(1);
	EXPECT_FALSE(CanStep({ 1, 0 }, { 0, 1 })) << "Can't walk through the boundary between two corners";
	EXPECT_FALSE(CanStep({ 0, 1 }, { 1, 0 })) << "Can't walk through the boundary between two corners";
	tileAt(Point { 0, 0 }).setPiece(0);
	EXPECT_FALSE(CanStep({ 1, 0 }, { 0, 1 })) << "Can't cut a solid corner";
	EXPECT_FALSE(CanStep({ 0, 1 }, { 1, 0 })) << "Can't cut a solid corner";
	tileAt(Point { 1, 1 }).setPiece(0);
}

} // namespace
} // namespace devilution
