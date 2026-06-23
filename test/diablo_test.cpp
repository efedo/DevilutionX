#include <gtest/gtest.h>

#include "application/diablo.h"
#include "network/multi.h"

using namespace devilution;

TEST(Diablo, diablo_pause_game_unpause)
{
	gbIsMultiplayer = false;
	PauseMode = 1;
	diablo_pause_game();
	EXPECT_EQ(PauseMode, 0);
}
