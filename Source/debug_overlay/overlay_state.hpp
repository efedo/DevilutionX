#pragma once

#include <cstddef>
#include <optional>

#include "engine/point.hpp"

namespace devilution {

enum class DebugOverlayWindow : std::size_t {
	Console,
	Inspector,
	Editor,
	Count,
};

struct DebugOverlayDisplaySize {
	float width;
	float height;
};

DebugOverlayDisplaySize ResolveDebugOverlayDisplaySize(
    int windowWidth, int windowHeight, int logicalWidth, int logicalHeight);

class DebugOverlayState {
public:
	bool IsActive() const;
	void ToggleActive();
	void Close();

	bool IsWindowVisible(DebugOverlayWindow window) const;
	void ToggleWindow(DebugOverlayWindow window);
	void SetWindowVisible(DebugOverlayWindow window, bool visible);

	bool TryOpenEditor(bool multiplayer, int pauseMode);
	int CloseEditor();
	bool EditorOwnsPause() const;
	bool ShouldSelectEditorTile(bool mouseCaptured, bool leftButton) const;
	void SelectEditorTile(Point tile);
	const std::optional<Point> &GetSelectedEditorTile() const;

private:
	bool active_ = false;
	bool visibleWindows_[static_cast<std::size_t>(DebugOverlayWindow::Count)] {};
	std::optional<int> editorPreviousPauseMode_;
	std::optional<Point> selectedEditorTile_;
};

} // namespace devilution
