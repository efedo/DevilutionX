#include <gtest/gtest.h>

#include "ui/automap.h"

using namespace devilution;

TEST(Automap, InitAutomap)
{
	CurrentAutomapManager.InitAutomapOnce();
	EXPECT_EQ(CurrentAutomapManager.GetAutomapActive(), false);
	EXPECT_EQ(CurrentAutomapManager.GetAutoMapScale(), 50);
	EXPECT_EQ(AmLine(AmLineLength::DoubleTile), static_cast<int>(AmLineLength::FullTile));
	EXPECT_EQ(AmLine(AmLineLength::FullAndHalfTile), 6);
	EXPECT_EQ(AmLine(AmLineLength::FullTile), static_cast<int>(AmLineLength::HalfTile));
	EXPECT_EQ(AmLine(AmLineLength::HalfTile), static_cast<int>(AmLineLength::QuarterTile));
	EXPECT_EQ(AmLine(AmLineLength::QuarterTile), 1);
}

TEST(Automap, StartAutomap)
{
	CurrentAutomapManager.StartAutomap();
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaX, 0);
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaY, 0);
	EXPECT_EQ(CurrentAutomapManager.GetAutomapActive(), true);
}

TEST(Automap, AutomapUp)
{
	CurrentAutomapManager.SetAutomapOffset({ 1, 1 });
	CurrentAutomapManager.AutomapUp();
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaX, 0);
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaY, 0);
}

TEST(Automap, AutomapDown)
{
	CurrentAutomapManager.SetAutomapOffset({ 1, 1 });
	CurrentAutomapManager.AutomapDown();
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaX, 2);
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaY, 2);
}

TEST(Automap, AutomapLeft)
{
	CurrentAutomapManager.SetAutomapOffset({ 1, 1 });
	CurrentAutomapManager.AutomapLeft();
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaX, 0);
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaY, 2);
}

TEST(Automap, AutomapRight)
{
	CurrentAutomapManager.SetAutomapOffset({ 1, 1 });
	CurrentAutomapManager.AutomapRight();
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaX, 2);
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaY, 0);
}

TEST(Automap, AutomapZoomIn)
{
	CurrentAutomapManager.SetAutoMapScale(50);
	CurrentAutomapManager.AutomapZoomIn();
	EXPECT_EQ(CurrentAutomapManager.GetAutoMapScale(), 75);
	EXPECT_EQ(AmLine(AmLineLength::DoubleTile), static_cast<int>(AmLineLength::FullAndHalfTile));
	EXPECT_EQ(AmLine(AmLineLength::FullTile), 6);
	EXPECT_EQ(AmLine(AmLineLength::HalfTile), 3);
	EXPECT_EQ(AmLine(AmLineLength::QuarterTile), 1);
}

TEST(Automap, AutomapZoomIn_Max)
{
	CurrentAutomapManager.SetAutoMapScale(175);
	CurrentAutomapManager.SetAutoMapScale(175);
	CurrentAutomapManager.AutomapZoomIn();
	CurrentAutomapManager.AutomapZoomIn();
	EXPECT_EQ(CurrentAutomapManager.GetAutoMapScale(), 200);
	EXPECT_EQ(AmLine(AmLineLength::DoubleTile), 32);
	EXPECT_EQ(AmLine(AmLineLength::FullTile), static_cast<int>(AmLineLength::DoubleTile));
	EXPECT_EQ(AmLine(AmLineLength::HalfTile), static_cast<int>(AmLineLength::FullTile));
	EXPECT_EQ(AmLine(AmLineLength::QuarterTile), static_cast<int>(AmLineLength::HalfTile));
}

TEST(Automap, AutomapZoomOut)
{
	CurrentAutomapManager.SetAutoMapScale(200);
	CurrentAutomapManager.AutomapZoomOut();
	EXPECT_EQ(CurrentAutomapManager.GetAutoMapScale(), 175);
	EXPECT_EQ(AmLine(AmLineLength::DoubleTile), 28);
	EXPECT_EQ(AmLine(AmLineLength::FullTile), 14);
	EXPECT_EQ(AmLine(AmLineLength::HalfTile), 7);
	EXPECT_EQ(AmLine(AmLineLength::QuarterTile), 3);
}

TEST(Automap, AutomapZoomOut_Min)
{
	CurrentAutomapManager.SetAutoMapScale(50);
	CurrentAutomapManager.AutomapZoomOut();
	CurrentAutomapManager.AutomapZoomOut();
	EXPECT_EQ(CurrentAutomapManager.GetAutoMapScale(), 25);
	EXPECT_EQ(AmLine(AmLineLength::DoubleTile), static_cast<int>(AmLineLength::HalfTile));
	EXPECT_EQ(AmLine(AmLineLength::FullTile), static_cast<int>(AmLineLength::QuarterTile));
	EXPECT_EQ(AmLine(AmLineLength::HalfTile), 1);
	EXPECT_EQ(AmLine(AmLineLength::QuarterTile), 0);
}

TEST(Automap, AutomapZoomReset)
{
	CurrentAutomapManager.SetAutoMapScale(50);
	CurrentAutomapManager.SetAutomapOffset({ 1, 1 });
	CurrentAutomapManager.AutomapZoomReset();
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaX, 0);
	EXPECT_EQ(CurrentAutomapManager.GetAutomapOffset().deltaY, 0);
	EXPECT_EQ(CurrentAutomapManager.GetAutoMapScale(), 50);
	EXPECT_EQ(AmLine(AmLineLength::DoubleTile), static_cast<int>(AmLineLength::FullTile));
	EXPECT_EQ(AmLine(AmLineLength::FullTile), static_cast<int>(AmLineLength::HalfTile));
	EXPECT_EQ(AmLine(AmLineLength::HalfTile), static_cast<int>(AmLineLength::QuarterTile));
	EXPECT_EQ(AmLine(AmLineLength::QuarterTile), 1);
}
