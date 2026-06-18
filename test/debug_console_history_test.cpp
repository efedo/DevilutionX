#include <gtest/gtest.h>

#include "debug_overlay/console_history.hpp"

using namespace devilution;

TEST(DebugConsoleHistory, KeepsUniqueCommandsInMostRecentOrder)
{
	DebugConsoleHistory history;

	history.Push("dev.player.god()");
	history.Push("dev.display.grid()");
	history.Push("dev.player.god()");

	EXPECT_EQ(history.Previous(""), "dev.player.god()");
	EXPECT_EQ(history.Previous(""), "dev.display.grid()");
	EXPECT_EQ(history.Previous(""), "dev.display.grid()");
	EXPECT_EQ(history.Next(), "dev.player.god()");
	EXPECT_EQ(history.Next(), "");
}

TEST(DebugConsoleHistory, RestoresDraftAfterBrowsing)
{
	DebugConsoleHistory history;

	history.Push("dev.player.god()");

	EXPECT_EQ(history.Previous("dev."), "dev.player.god()");
	EXPECT_EQ(history.Next(), "dev.");
}
