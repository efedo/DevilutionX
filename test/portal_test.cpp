#include "game/portals/portal.hpp"
#include "portals/validation.hpp"

#include "game/portals/portal.hpp"
#include "game/portals/validation.hpp"

#include <gtest/gtest.h>

namespace devilution {
namespace {

class PortalTest : public testing::Test {
protected:
	void SetUp() override
	{
		InitPortals();
	}
};

TEST_F(PortalTest, InitClosesEveryPortal)
{
	for (Portal &portal : Portals)
		portal.open = true;

	InitPortals();

	for (const Portal &portal : Portals)
		EXPECT_FALSE(portal.open);
}

TEST_F(PortalTest, SetPortalStatsUpdatesSelectedPortal)
{
	SetPortalStats(2, true, { 20, 30 }, 5, DTYPE_CATACOMBS, false);

	const Portal &portal = Portals[2];
	EXPECT_TRUE(portal.open);
	EXPECT_EQ(portal.position, Point(20, 30));
	EXPECT_EQ(portal.level, 5);
	EXPECT_EQ(portal.ltype, DTYPE_CATACOMBS);
	EXPECT_FALSE(portal.setlvl);
}

TEST_F(PortalTest, PositionCheckAcceptsPortalAndArrivalTile)
{
	SetPortalStats(0, true, { 20, 30 }, 5, DTYPE_CATACOMBS, false);

	EXPECT_TRUE(PosOkPortal(5, { 20, 30 }));
	EXPECT_TRUE(PosOkPortal(5, { 21, 31 }));
	EXPECT_FALSE(PosOkPortal(4, { 20, 30 }));
	EXPECT_FALSE(PosOkPortal(5, { 21, 30 }));
}

} // namespace
} // namespace devilution
