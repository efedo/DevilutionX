#include "debug_overlay/overlay_state.hpp"

#include <algorithm>
#include <iterator>

namespace devilution {

DebugOverlayDisplaySize ResolveDebugOverlayDisplaySize(
    int windowWidth, int windowHeight, int logicalWidth, int logicalHeight)
{
	if (logicalWidth > 0 && logicalHeight > 0) {
		return { static_cast<float>(logicalWidth), static_cast<float>(logicalHeight) };
	}
	return { static_cast<float>(windowWidth), static_cast<float>(windowHeight) };
}

bool DebugOverlayState::IsActive() const
{
	return active_;
}

void DebugOverlayState::ToggleActive()
{
	if (active_) {
		Close();
		return;
	}

	active_ = true;
}

void DebugOverlayState::Close()
{
	active_ = false;
	std::fill(std::begin(visibleWindows_), std::end(visibleWindows_), false);
}

bool DebugOverlayState::IsWindowVisible(DebugOverlayWindow window) const
{
	return visibleWindows_[static_cast<std::size_t>(window)];
}

void DebugOverlayState::ToggleWindow(DebugOverlayWindow window)
{
	const std::size_t index = static_cast<std::size_t>(window);
	visibleWindows_[index] = !visibleWindows_[index];
}

void DebugOverlayState::SetWindowVisible(DebugOverlayWindow window, bool visible)
{
	visibleWindows_[static_cast<std::size_t>(window)] = visible;
}

bool DebugOverlayState::TryOpenEditor(bool multiplayer, int pauseMode)
{
	if (multiplayer)
		return false;

	editorPreviousPauseMode_ = pauseMode;
	SetWindowVisible(DebugOverlayWindow::Editor, true);
	return true;
}

int DebugOverlayState::CloseEditor()
{
	const int previousPauseMode = editorPreviousPauseMode_.value_or(0);
	editorPreviousPauseMode_.reset();
	selectedEditorTile_.reset();
	SetWindowVisible(DebugOverlayWindow::Editor, false);
	return previousPauseMode;
}

bool DebugOverlayState::EditorOwnsPause() const
{
	return editorPreviousPauseMode_.has_value();
}

bool DebugOverlayState::ShouldSelectEditorTile(bool mouseCaptured, bool leftButton) const
{
	return IsWindowVisible(DebugOverlayWindow::Editor) && !mouseCaptured && leftButton;
}

void DebugOverlayState::SelectEditorTile(Point tile)
{
	selectedEditorTile_ = tile;
}

const std::optional<Point> &DebugOverlayState::GetSelectedEditorTile() const
{
	return selectedEditorTile_;
}

} // namespace devilution
