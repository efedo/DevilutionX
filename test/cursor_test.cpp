#include <gtest/gtest.h>

#include "engine/cursor.h"

using namespace devilution;

TEST(Cursor, NewCursor)
{
	NewCursor(CURSOR_HOURGLASS);
	EXPECT_EQ(pcurs, CURSOR_HOURGLASS);
}
