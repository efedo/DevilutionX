#include <gtest/gtest.h>

#include "debug_overlay/overlay_state.hpp"

using namespace devilution;

TEST(DebugOverlayState, TogglingOverlayStartsWithAllWindowsClosed)
{
	DebugOverlayState state;

	state.ToggleActive();

	EXPECT_TRUE(state.IsActive());
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Console));
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Inspector));
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Editor));
}

TEST(DebugOverlayState, WindowsToggleIndependently)
{
	DebugOverlayState state;
	state.ToggleActive();

	state.ToggleWindow(DebugOverlayWindow::Console);
	state.ToggleWindow(DebugOverlayWindow::Inspector);

	EXPECT_TRUE(state.IsWindowVisible(DebugOverlayWindow::Console));
	EXPECT_TRUE(state.IsWindowVisible(DebugOverlayWindow::Inspector));
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Editor));

	state.ToggleWindow(DebugOverlayWindow::Console);

	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Console));
	EXPECT_TRUE(state.IsWindowVisible(DebugOverlayWindow::Inspector));
}

TEST(DebugOverlayState, ClosingOverlayClosesEveryWindow)
{
	DebugOverlayState state;
	state.ToggleActive();
	state.ToggleWindow(DebugOverlayWindow::Console);
	state.ToggleWindow(DebugOverlayWindow::Inspector);
	state.ToggleWindow(DebugOverlayWindow::Editor);

	state.Close();

	EXPECT_FALSE(state.IsActive());
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Console));
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Inspector));
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Editor));
}

TEST(DebugOverlayState, TogglingActiveOverlayClosesIt)
{
	DebugOverlayState state;
	state.ToggleActive();
	state.ToggleWindow(DebugOverlayWindow::Console);

	state.ToggleActive();

	EXPECT_FALSE(state.IsActive());
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Console));
}

TEST(DebugOverlayTextInputState, DoesNotStopTextInputOwnedByAnotherScreen)
{
	DebugOverlayTextInputState state;

	EXPECT_EQ(state.Update(/*consoleVisible=*/false), DebugOverlayTextInputAction::None);
}

TEST(DebugOverlayTextInputState, StartsAndStopsOnlyOverlayOwnedTextInput)
{
	DebugOverlayTextInputState state;

	EXPECT_EQ(state.Update(/*consoleVisible=*/true), DebugOverlayTextInputAction::Start);
	EXPECT_EQ(state.Update(/*consoleVisible=*/true), DebugOverlayTextInputAction::None);
	EXPECT_EQ(state.Update(/*consoleVisible=*/false), DebugOverlayTextInputAction::Stop);
	EXPECT_EQ(state.Update(/*consoleVisible=*/false), DebugOverlayTextInputAction::None);
}

TEST(DebugOverlayDisplaySize, PrefersRendererLogicalSize)
{
	const DebugOverlayDisplaySize size = ResolveDebugOverlayDisplaySize(
	    /*windowWidth=*/1920,
	    /*windowHeight=*/1080,
	    /*logicalWidth=*/640,
	    /*logicalHeight=*/480);

	EXPECT_FLOAT_EQ(size.width, 640.0F);
	EXPECT_FLOAT_EQ(size.height, 480.0F);
}

TEST(DebugOverlayDisplaySize, FallsBackToWindowSizeWithoutLogicalPresentation)
{
	const DebugOverlayDisplaySize size = ResolveDebugOverlayDisplaySize(
	    /*windowWidth=*/1920,
	    /*windowHeight=*/1080,
	    /*logicalWidth=*/0,
	    /*logicalHeight=*/0);

	EXPECT_FLOAT_EQ(size.width, 1920.0F);
	EXPECT_FLOAT_EQ(size.height, 1080.0F);
}

TEST(DebugOverlayEditor, OwnsAndRestoresPauseState)
{
	DebugOverlayState state;
	state.ToggleActive();

	ASSERT_TRUE(state.TryOpenEditor(/*multiplayer=*/false, /*pauseMode=*/1));
	EXPECT_TRUE(state.IsWindowVisible(DebugOverlayWindow::Editor));
	EXPECT_TRUE(state.EditorOwnsPause());
	EXPECT_EQ(state.CloseEditor(), 1);
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Editor));
	EXPECT_FALSE(state.EditorOwnsPause());
}

TEST(DebugOverlayEditor, IsDisabledInMultiplayer)
{
	DebugOverlayState state;
	state.ToggleActive();

	EXPECT_FALSE(state.TryOpenEditor(/*multiplayer=*/true, /*pauseMode=*/0));
	EXPECT_FALSE(state.IsWindowVisible(DebugOverlayWindow::Editor));
	EXPECT_FALSE(state.EditorOwnsPause());
}

TEST(DebugOverlayEditor, KeepsSelectionUntilClosed)
{
	DebugOverlayState state;
	state.ToggleActive();
	ASSERT_TRUE(state.TryOpenEditor(/*multiplayer=*/false, /*pauseMode=*/0));

	state.SelectEditorTile({ 42, 57 });

	ASSERT_TRUE(state.GetSelectedEditorTile().has_value());
	EXPECT_EQ(*state.GetSelectedEditorTile(), Point(42, 57));

	state.CloseEditor();

	EXPECT_FALSE(state.GetSelectedEditorTile().has_value());
}

TEST(DebugOverlayEditor, SelectsOnlyUncapturedWorldClicks)
{
	DebugOverlayState state;
	state.ToggleActive();
	ASSERT_TRUE(state.TryOpenEditor(/*multiplayer=*/false, /*pauseMode=*/0));

	EXPECT_TRUE(state.ShouldSelectEditorTile(/*mouseCaptured=*/false, /*leftButton=*/true));
	EXPECT_FALSE(state.ShouldSelectEditorTile(/*mouseCaptured=*/true, /*leftButton=*/true));
	EXPECT_FALSE(state.ShouldSelectEditorTile(/*mouseCaptured=*/false, /*leftButton=*/false));

	state.CloseEditor();
	EXPECT_FALSE(state.ShouldSelectEditorTile(/*mouseCaptured=*/false, /*leftButton=*/true));
}

TEST(DebugPieceSelector, OpensWithCurrentPieceSelected)
{
	DebugPieceSelectorState state;

	state.Open(/*currentPiece=*/42);

	EXPECT_TRUE(state.IsOpen());
	EXPECT_EQ(state.GetSelectedPiece(), 42);
}

TEST(DebugPieceSelector, BrowsingDoesNotApplyUntilConfirmed)
{
	DebugPieceSelectorState state;
	state.Open(/*currentPiece=*/42);

	state.Select(/*piece=*/57);

	EXPECT_EQ(state.GetSelectedPiece(), 57);
	EXPECT_FALSE(state.TakeAppliedPiece().has_value());
	EXPECT_TRUE(state.IsOpen());
}

TEST(DebugPieceSelector, ApplyReturnsSelectionAndCloses)
{
	DebugPieceSelectorState state;
	state.Open(/*currentPiece=*/42);
	state.Select(/*piece=*/57);

	state.Apply();

	EXPECT_EQ(state.TakeAppliedPiece(), 57);
	EXPECT_FALSE(state.IsOpen());
	EXPECT_FALSE(state.TakeAppliedPiece().has_value());
}

TEST(DebugPieceSelector, CancelClosesWithoutApplying)
{
	DebugPieceSelectorState state;
	state.Open(/*currentPiece=*/42);
	state.Select(/*piece=*/57);

	state.Cancel();

	EXPECT_FALSE(state.IsOpen());
	EXPECT_FALSE(state.TakeAppliedPiece().has_value());
}

TEST(DebugPiecePreviewSize, PreservesAspectRatioWhenHeightLimited)
{
	const DebugOverlayDisplaySize size = ResolveDebugPiecePreviewSize(
	    /*sourceWidth=*/64,
	    /*sourceHeight=*/256,
	    /*maximumWidth=*/128.0F,
	    /*maximumHeight=*/320.0F);

	EXPECT_FLOAT_EQ(size.width, 80.0F);
	EXPECT_FLOAT_EQ(size.height, 320.0F);
}

TEST(DebugPiecePreviewSize, PreservesAspectRatioWhenWidthLimited)
{
	const DebugOverlayDisplaySize size = ResolveDebugPiecePreviewSize(
	    /*sourceWidth=*/64,
	    /*sourceHeight=*/96,
	    /*maximumWidth=*/128.0F,
	    /*maximumHeight=*/320.0F);

	EXPECT_FLOAT_EQ(size.width, 128.0F);
	EXPECT_FLOAT_EQ(size.height, 192.0F);
}

TEST(DebugPiecePreviewFoliage, UsesFoliageDecoderForFirstFloorBlocks)
{
	EXPECT_TRUE(IsDebugPiecePreviewFoliage(
	    /*isFloorPiece=*/true,
	    /*blockIndex=*/0,
	    TileType::TransparentSquare));
	EXPECT_TRUE(IsDebugPiecePreviewFoliage(
	    /*isFloorPiece=*/true,
	    /*blockIndex=*/1,
	    TileType::TransparentSquare));
}

TEST(DebugPiecePreviewFoliage, UsesNormalDecoderForOtherBlocks)
{
	EXPECT_FALSE(IsDebugPiecePreviewFoliage(
	    /*isFloorPiece=*/false,
	    /*blockIndex=*/0,
	    TileType::TransparentSquare));
	EXPECT_FALSE(IsDebugPiecePreviewFoliage(
	    /*isFloorPiece=*/true,
	    /*blockIndex=*/2,
	    TileType::TransparentSquare));
	EXPECT_FALSE(IsDebugPiecePreviewFoliage(
	    /*isFloorPiece=*/true,
	    /*blockIndex=*/0,
	    TileType::Square));
}
