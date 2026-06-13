#include "debug_overlay/imgui_overlay.hpp"

#if defined(_DEBUG) && !defined(USE_SDL1)

#include <cfloat>
#include <string>
#include <string_view>
#include <vector>

#include <imgui.h>

#ifdef USE_SDL3
#include <backends/imgui_impl_sdl3.h>
#include <backends/imgui_impl_sdlrenderer3.h>
#else
#include <backends/imgui_impl_sdl2.h>
#include <backends/imgui_impl_sdlrenderer2.h>
#endif

#include "cursor.h"
#include "debug_overlay/console_history.hpp"
#include "debug_overlay/overlay_state.hpp"
#include "diablo.h"
#include "engine/backbuffer_state.hpp"
#include "engine/dx.h"
#include "game_mode.hpp"
#include "items.h"
#include "levels/gendung.h"
#include "levels/tile_properties.hpp"
#include "monster.h"
#include "multi.h"
#include "objects.h"
#include "panels/console.hpp"
#include "utils/display.h"
#include "utils/sdl_compat.h"
#include "utils/str_cat.hpp"

namespace devilution {
namespace {

bool Initialized;
SDL_Renderer *InitializedRenderer;
char InputBuffer[4096];
DebugConsoleHistory History;
bool ScrollToBottom;
bool RefocusInput;
DebugOverlayState OverlayState;

struct PiecePaletteEntry {
	uint16_t piece;
	std::string label;
};

Point PiecePaletteTarget;
std::vector<PiecePaletteEntry> PiecePaletteEntries;

std::string_view BoolToString(bool value)
{
	return value ? "true" : "false";
}

std::string_view TileTypeToString(TileType tileType)
{
	switch (tileType) {
	case TileType::Square:
		return "Square";
	case TileType::TransparentSquare:
		return "TransparentSquare";
	case TileType::LeftTriangle:
		return "LeftTriangle";
	case TileType::RightTriangle:
		return "RightTriangle";
	case TileType::LeftTrapezoid:
		return "LeftTrapezoid";
	case TileType::RightTrapezoid:
		return "RightTrapezoid";
	}
	return "???";
}

std::string BuildPieceLabel(uint16_t piece)
{
	if (piece == 0) {
		return "0 [Empty]";
	}

	const MICROS &micros = levelMicros()[piece];
	for (const LevelCelBlock block : micros.mt) {
		if (!block.hasValue())
			continue;
		return StrCat(piece, " [", TileTypeToString(block.type()), " #", block.frame(), "]");
	}

	return StrCat(piece, " [Empty]");
}

void RefreshPiecePalette()
{
	PiecePaletteEntries.clear();
	PiecePaletteEntries.reserve(MAXTILES);
	PiecePaletteEntries.push_back(PiecePaletteEntry { .piece = 0, .label = BuildPieceLabel(0) });
	for (uint16_t piece = 1; piece < MAXTILES; ++piece) {
		const MICROS &micros = levelMicros()[piece];
		bool hasValue = false;
		for (const LevelCelBlock block : micros.mt) {
			if (block.hasValue()) {
				hasValue = true;
				break;
			}
		}
		if (!hasValue)
			continue;
		PiecePaletteEntries.push_back(PiecePaletteEntry { .piece = piece, .label = BuildPieceLabel(piece) });
	}
}

void ApplyPieceToTile(Point position, uint16_t piece)
{
	dPiece[position.x][position.y] = piece;
	tileAt(position).setPiece(piece);
}

void DrawPieceSelectorPopup()
{
	if (!ImGui::BeginPopupModal("Piece Selector", nullptr, ImGuiWindowFlags_AlwaysAutoResize)) {
		return;
	}

	const Point target = PiecePaletteTarget;
	ImGui::Text("Tile: %d, %d", target.x, target.y);
	ImGui::Text("Current piece: %u", static_cast<unsigned>(tileAt(target).piece()));
	ImGui::Separator();

	if (PiecePaletteEntries.empty()) {
		RefreshPiecePalette();
	}

	if (ImGui::BeginChild("PiecePaletteScroll", ImVec2(540.0F, 360.0F), true, ImGuiWindowFlags_HorizontalScrollbar)) {
		ImGuiListClipper clipper;
		clipper.Begin(static_cast<int>(PiecePaletteEntries.size()));
		while (clipper.Step()) {
			for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; ++i) {
				const PiecePaletteEntry &entry = PiecePaletteEntries[static_cast<size_t>(i)];
				const bool selected = entry.piece == tileAt(target).piece();
				if (ImGui::Selectable(entry.label.c_str(), selected)) {
					ApplyPieceToTile(target, entry.piece);
					ImGui::CloseCurrentPopup();
				}
			}
		}
	}
	ImGui::EndChild();

	if (ImGui::Button("Close")) {
		ImGui::CloseCurrentPopup();
	}
	ImGui::EndPopup();
}

void DrawValue(const char *label, std::string_view value)
{
	ImGui::Text("%s: %.*s", label, static_cast<int>(value.size()), value.data());
}

void DrawValue(const char *label, int value)
{
	ImGui::Text("%s: %d", label, value);
}

void DrawSectionHeader(const char *label)
{
	ImGui::Spacing();
	ImGui::TextUnformatted(label);
	ImGui::Separator();
}

void DrawCompactSummaryLabel(const char *label, std::string_view value)
{
	ImGui::Text("%s: %.*s", label, static_cast<int>(value.size()), value.data());
}

void DrawCompactSummaryLabel(const char *label, int value)
{
	ImGui::Text("%s: %d", label, value);
}

void ProcessBackendEvent(const SDL_Event &event)
{
#ifdef USE_SDL3
	ImGui_ImplSDL3_ProcessEvent(&event);
#else
	ImGui_ImplSDL2_ProcessEvent(&event);
#endif
}

bool EnsureInitialized()
{
	if (renderer == nullptr || ghMainWnd == nullptr)
		return false;

	if (Initialized && InitializedRenderer == renderer)
		return true;

	DebugOverlayShutdown();

	IMGUI_CHECKVERSION();
	ImGui::CreateContext();
	ImGuiIO &io = ImGui::GetIO();
	io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
	io.IniFilename = nullptr;
	ImGui::StyleColorsDark();

#ifdef USE_SDL3
	ImGui_ImplSDL3_InitForSDLRenderer(ghMainWnd, renderer);
	ImGui_ImplSDLRenderer3_Init(renderer);
#else
	ImGui_ImplSDL2_InitForSDLRenderer(ghMainWnd, renderer);
	ImGui_ImplSDLRenderer2_Init(renderer);
#endif

	Initialized = true;
	InitializedRenderer = renderer;
	return true;
}

void ConfigureDisplaySize()
{
	int windowWidth;
	int windowHeight;
	SDL_GetWindowSize(ghMainWnd, &windowWidth, &windowHeight);

	int logicalWidth = 0;
	int logicalHeight = 0;
#ifdef USE_SDL3
	SDL_GetRenderLogicalPresentation(renderer, &logicalWidth, &logicalHeight, nullptr);
#else
	SDL_RenderGetLogicalSize(renderer, &logicalWidth, &logicalHeight);
#endif

	const DebugOverlayDisplaySize size = ResolveDebugOverlayDisplaySize(
	    windowWidth, windowHeight, logicalWidth, logicalHeight);
	ImGuiIO &io = ImGui::GetIO();
	io.DisplaySize = ImVec2(size.width, size.height);
	io.DisplayFramebufferScale = ImVec2(1.0F, 1.0F);
}

ImVec4 ColorForLine(ConsoleLineType type)
{
	switch (type) {
	case ConsoleLineType::Input:
		return ImVec4(0.76F, 0.86F, 1.0F, 1.0F);
	case ConsoleLineType::Warning:
		return ImVec4(1.0F, 0.82F, 0.33F, 1.0F);
	case ConsoleLineType::Error:
		return ImVec4(1.0F, 0.35F, 0.35F, 1.0F);
	case ConsoleLineType::Help:
		return ImVec4(0.70F, 0.70F, 0.70F, 1.0F);
	case ConsoleLineType::Output:
		return ImVec4(0.92F, 0.92F, 0.92F, 1.0F);
	}
	return ImVec4(0.92F, 0.92F, 0.92F, 1.0F);
}

int ConsoleInputCallback(ImGuiInputTextCallbackData *data)
{
	if (data->EventFlag != ImGuiInputTextFlags_CallbackHistory)
		return 0;

	std::string_view replacement;
	if (data->EventKey == ImGuiKey_UpArrow) {
		replacement = History.Previous(data->Buf);
	} else if (data->EventKey == ImGuiKey_DownArrow) {
		replacement = History.Next();
	} else {
		return 0;
	}

	data->DeleteChars(0, data->BufTextLen);
	data->InsertChars(0, replacement.data(), replacement.data() + replacement.size());
	return 0;
}

void DrawConsoleContent()
{
	if (ImGui::Button("Clear Input")) {
		InputBuffer[0] = '\0';
	}
	ImGui::SameLine();
	ImGui::TextUnformatted("Lua REPL");
	ImGui::Separator();

	const float footerHeight = ImGui::GetStyle().ItemSpacing.y + ImGui::GetFrameHeightWithSpacing();
	if (ImGui::BeginChild("ConsoleScrollback", ImVec2(0.0F, -footerHeight), false, ImGuiWindowFlags_HorizontalScrollbar)) {
		for (const ConsoleLine &line : GetConsoleLines()) {
			ImGui::PushStyleColor(ImGuiCol_Text, ColorForLine(line.type));
			ImGui::TextUnformatted(line.text.c_str());
			ImGui::PopStyleColor();
		}
		if (ScrollToBottom || ImGui::GetScrollY() >= ImGui::GetScrollMaxY())
			ImGui::SetScrollHereY(1.0F);
		ScrollToBottom = false;
	}
	ImGui::EndChild();

	ImGuiInputTextFlags flags = ImGuiInputTextFlags_EnterReturnsTrue | ImGuiInputTextFlags_EscapeClearsAll | ImGuiInputTextFlags_CallbackHistory;
	if (RefocusInput) {
		ImGui::SetKeyboardFocusHere();
		RefocusInput = false;
	}
	ImGui::SetNextItemWidth(-FLT_MIN);
	if (ImGui::InputText("##DebugConsoleInput", InputBuffer, sizeof(InputBuffer), flags, ConsoleInputCallback)) {
		std::string command = InputBuffer;
		if (!command.empty()) {
			History.Push(command);
			RunInConsole(command);
			InputBuffer[0] = '\0';
			ScrollToBottom = true;
			RefocusInput = true;
		}
	}
}

void DrawInspectorContent()
{
	const Point tile = OverlayState.GetSelectedEditorTile().value_or(cursPosition);
	if (!InDungeonBounds(tile)) {
		ImGui::TextUnformatted("Cursor tile is out of bounds.");
		return;
	}

	const Tile &dungeonTile = tileAt(tile);
	ImGui::Text("Tile: %d, %d", tile.x, tile.y);
	ImGui::Text("Solid: %s   Walkable: %s   Occupied: %s", BoolToString(IsTileSolid(tile)).data(), BoolToString(IsTileWalkable(tile)).data(), BoolToString(IsTileOccupied(tile)).data());
	DrawCompactSummaryLabel("Piece", dungeonTile.piece());
	DrawValue("TransVal", dungeonTile.transVal());
	DrawValue("Light", dungeonTile.light());
	DrawValue("PreLight", dungeonTile.preLight());
	DrawValue("Flags", static_cast<int>(dungeonTile.flags()));
	DrawValue("Corpse", dungeonTile.corpse());
	DrawValue("Special", dungeonTile.special());
	DrawValue("Player", dungeonTile.player());

	if (ObjectUnderCursor != nullptr || dungeonTile.object() != 0) {
		DrawSectionHeader("Object");
		if (ObjectUnderCursor != nullptr) {
			const Object &object = *ObjectUnderCursor;
			ImGui::Text("Object: %s (%d)", object.name().str().data(), static_cast<int>(object._otype));
			DrawValue("Solid", BoolToString(object._oSolidFlag));
			DrawValue("Missile", BoolToString(object._oMissFlag));
			DrawValue("Trap", BoolToString(object._oTrapFlag));
			DrawValue("Door", BoolToString(object._oDoorFlag));
			DrawValue("Interactable", BoolToString(object.canInteractWith()));
		} else {
			ImGui::TextWrapped("Object index set but no object pointer available.");
		}
	}

	if (pcursmonst != -1 || dungeonTile.monster() != 0) {
		DrawSectionHeader("Monster");
		if (pcursmonst != -1) {
			const Monster &monster = Monsters[pcursmonst];
			ImGui::Text("Monster: %s (%d)", monster.name().data(), static_cast<int>(monster.mode));
			DrawValue("Level Type", static_cast<int>(monster.levelType));
			DrawValue("HP", monster.hitPoints);
			DrawValue("Max HP", monster.maxHitPoints);
			DrawValue("Unique", BoolToString(monster.isUnique()));
			DrawValue("Active Ticks", monster.activeForTicks);
		} else {
			ImGui::TextWrapped("Monster index set but no monster selection available.");
		}
	}

	if (pcursitem != -1 || dungeonTile.item() != 0) {
		DrawSectionHeader("Item");
		if (pcursitem != -1) {
			const Item &item = Items[pcursitem];
			ImGui::Text("Item: %s (%d)", item.getName().str().data(), static_cast<int>(item._itype));
			DrawValue("Quality", static_cast<int>(item._iMagical));
			DrawValue("CreateInfo", static_cast<int>(item._iCreateInfo));
			DrawValue("Identified", BoolToString(item._iIdentified));
			DrawValue("Cursor", static_cast<int>(item._iCurs));
			DrawValue("Class", static_cast<int>(item._iClass));
		} else {
			ImGui::TextWrapped("Item index set but no item selection available.");
		}
	}
}

void DrawEditorContent()
{
	const std::optional<Point> &selectedTile = OverlayState.GetSelectedEditorTile();
	if (!selectedTile.has_value()) {
		ImGui::TextUnformatted("Click a tile in the game view to select it.");
		return;
	}

	const Point tile = *selectedTile;
	const Tile &dungeonTile = tileAt(tile);
	ImGui::Text("Tile: %d, %d", tile.x, tile.y);
	ImGui::Text("Piece: %u", static_cast<unsigned>(dungeonTile.piece()));
	ImGui::SameLine();
	if (ImGui::SmallButton("Edit##Piece")) {
		PiecePaletteTarget = tile;
		RefreshPiecePalette();
		ImGui::OpenPopup("Piece Selector");
	}
	DrawPieceSelectorPopup();
}

void DrawOverlayToolbar()
{
	if (!OverlayState.IsActive())
		return;

	const ImGuiIO &io = ImGui::GetIO();
	ImGui::SetNextWindowPos(ImVec2(io.DisplaySize.x * 0.5f, 0.0f), ImGuiCond_Always, ImVec2(0.5f, 0.0f));
	ImGui::SetNextWindowBgAlpha(0.85f);
	constexpr ImGuiWindowFlags flags = ImGuiWindowFlags_NoDecoration
		| ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoSavedSettings
		| ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoFocusOnAppearing;

	if (ImGui::Begin("##OverlayToolbar", nullptr, flags)) {
		// Returns true when the button was just toggled ON.
		auto ToggleButton = [](const char *label, DebugOverlayWindow window) -> bool {
			const bool visible = OverlayState.IsWindowVisible(window);
			if (visible)
				ImGui::PushStyleColor(ImGuiCol_Button, ImGui::GetStyleColorVec4(ImGuiCol_ButtonActive));
			const bool clicked = ImGui::Button(label);
			if (visible)
				ImGui::PopStyleColor();
			if (clicked)
				OverlayState.ToggleWindow(window);
			return clicked && OverlayState.IsWindowVisible(window);
		};
		if (ToggleButton("Console", DebugOverlayWindow::Console))
			RefocusInput = true;
		ImGui::SameLine();
		ToggleButton("Inspector", DebugOverlayWindow::Inspector);
		ImGui::SameLine();
		ImGui::BeginDisabled(gbIsMultiplayer);
		const bool editorVisible = OverlayState.IsWindowVisible(DebugOverlayWindow::Editor);
		if (editorVisible)
			ImGui::PushStyleColor(ImGuiCol_Button, ImGui::GetStyleColorVec4(ImGuiCol_ButtonActive));
		const bool editorClicked = ImGui::Button("Editor");
		if (editorVisible)
			ImGui::PopStyleColor();
		ImGui::EndDisabled();
		if (gbIsMultiplayer && ImGui::IsItemHovered(ImGuiHoveredFlags_AllowWhenDisabled))
			ImGui::SetTooltip("Editor is disabled in multiplayer.");
		if (editorClicked) {
			if (editorVisible) {
				PauseMode = OverlayState.CloseEditor();
				RedrawEverything();
			} else if (OverlayState.TryOpenEditor(gbIsMultiplayer, PauseMode)) {
				PauseMode = 2;
				LastPlayerAction = PlayerActionType::None;
				RedrawEverything();
			}
		}
	}
	ImGui::End();
}

void DrawConsoleWindow()
{
	if (!OverlayState.IsWindowVisible(DebugOverlayWindow::Console))
		return;

	bool open = true;
	ImGui::SetNextWindowSize(ImVec2(760.0F, 360.0F), ImGuiCond_FirstUseEver);
	if (!ImGui::Begin("Console", &open, ImGuiWindowFlags_NoCollapse)) {
		ImGui::End();
		if (!open)
			OverlayState.SetWindowVisible(DebugOverlayWindow::Console, false);
		return;
	}
	if (!open)
		OverlayState.SetWindowVisible(DebugOverlayWindow::Console, false);

	InitConsole();
	SDLC_StartTextInput(ghMainWnd);
	DrawConsoleContent();
	ImGui::End();
}

void DrawInspectorWindow()
{
	if (!OverlayState.IsWindowVisible(DebugOverlayWindow::Inspector))
		return;

	bool open = true;
	ImGui::SetNextWindowSize(ImVec2(400.0F, 500.0F), ImGuiCond_FirstUseEver);
	if (!ImGui::Begin("Inspector", &open, ImGuiWindowFlags_NoCollapse)) {
		ImGui::End();
		if (!open)
			OverlayState.SetWindowVisible(DebugOverlayWindow::Inspector, false);
		return;
	}
	if (!open)
		OverlayState.SetWindowVisible(DebugOverlayWindow::Inspector, false);

	DrawInspectorContent();
	ImGui::End();
}

void DrawEditorWindow()
{
	if (!OverlayState.IsWindowVisible(DebugOverlayWindow::Editor))
		return;

	bool open = true;
	ImGui::SetNextWindowSize(ImVec2(400.0F, 300.0F), ImGuiCond_FirstUseEver);
	if (!ImGui::Begin("Editor", &open, ImGuiWindowFlags_NoCollapse)) {
		ImGui::End();
		if (!open) {
			PauseMode = OverlayState.CloseEditor();
			RedrawEverything();
		}
		return;
	}

	if (open)
		DrawEditorContent();
	ImGui::End();

	if (!open) {
		PauseMode = OverlayState.CloseEditor();
		RedrawEverything();
	}
}

bool IsOverlayInputEvent(const SDL_Event &event)
{
	switch (event.type) {
	case SDL_EVENT_KEY_DOWN:
	case SDL_EVENT_KEY_UP:
	case SDL_EVENT_TEXT_INPUT:
#ifdef USE_SDL3
	case SDL_EVENT_TEXT_EDITING:
#else
	case SDL_TEXTEDITING:
#endif
	case SDL_EVENT_MOUSE_MOTION:
	case SDL_EVENT_MOUSE_BUTTON_DOWN:
	case SDL_EVENT_MOUSE_BUTTON_UP:
	case SDL_EVENT_MOUSE_WHEEL:
		return true;
	default:
		return false;
	}
}

} // namespace

bool DebugOverlayHandleEvent(const SDL_Event &event)
{
	// Toggle before initialization so the toolbar can appear on the next render.
	if (event.type == SDL_EVENT_KEY_DOWN && SDLC_EventScancode(event) == SDL_SCANCODE_GRAVE) {
		if (OverlayState.IsActive()) {
			if (OverlayState.EditorOwnsPause())
				PauseMode = OverlayState.CloseEditor();
			OverlayState.Close();
			RedrawEverything();
		} else {
			OverlayState.ToggleActive();
		}
		if (!OverlayState.IsActive() && ghMainWnd != nullptr)
			SDLC_StopTextInput(ghMainWnd);
		return true;
	}

	if (!OverlayState.IsActive())
		return false;

	if (!EnsureInitialized()) {
		return false;
	}

	ProcessBackendEvent(event);

	if (event.type == SDL_EVENT_KEY_DOWN && SDLC_EventKey(event) == SDLK_ESCAPE) {
		if (OverlayState.EditorOwnsPause())
			PauseMode = OverlayState.CloseEditor();
		OverlayState.Close();
		SDLC_StopTextInput(ghMainWnd);
		RedrawEverything();
		return true;
	}

	// Let ImGui decide if it wants the event based on whether it's capturing keyboard/mouse input
	const ImGuiIO &io = ImGui::GetIO();
	if (OverlayState.IsWindowVisible(DebugOverlayWindow::Editor)) {
		if (event.type == SDL_EVENT_MOUSE_MOTION) {
			MousePosition = { SDLC_EventMotionIntX(event), SDLC_EventMotionIntY(event) };
			if (!io.WantCaptureMouse)
				CheckCursMove();
		} else if (event.type == SDL_EVENT_MOUSE_BUTTON_DOWN
		    && OverlayState.ShouldSelectEditorTile(io.WantCaptureMouse, event.button.button == SDL_BUTTON_LEFT)) {
			MousePosition = { SDLC_EventButtonIntX(event), SDLC_EventButtonIntY(event) };
			CheckCursMove();
			if (InDungeonBounds(cursPosition)) {
				OverlayState.SelectEditorTile(cursPosition);
				RedrawViewport();
			}
			return true;
		}
	}
	if (IsOverlayInputEvent(event)) {
		switch (event.type) {
		case SDL_EVENT_KEY_DOWN:
		case SDL_EVENT_KEY_UP:
		case SDL_EVENT_TEXT_INPUT:
#ifdef USE_SDL3
		case SDL_EVENT_TEXT_EDITING:
#else
		case SDL_TEXTEDITING:
#endif
			return io.WantCaptureKeyboard || io.WantTextInput;
		case SDL_EVENT_MOUSE_MOTION:
		case SDL_EVENT_MOUSE_BUTTON_DOWN:
		case SDL_EVENT_MOUSE_BUTTON_UP:
		case SDL_EVENT_MOUSE_WHEEL:
			return io.WantCaptureMouse;
		default:
			return false;
		}
	}

	return false;
}

bool DebugOverlayIsAvailable()
{
	return renderer != nullptr;
}

bool DebugOverlayEditorOwnsPause()
{
	return OverlayState.EditorOwnsPause();
}

const std::optional<Point> &DebugOverlaySelectedTile()
{
	return OverlayState.GetSelectedEditorTile();
}

void DebugOverlayRender()
{
	if (!EnsureInitialized())
		return;

#ifdef USE_SDL3
	ImGui_ImplSDLRenderer3_NewFrame();
	ImGui_ImplSDL3_NewFrame();
#else
	ImGui_ImplSDLRenderer2_NewFrame();
	ImGui_ImplSDL2_NewFrame();
#endif
	ConfigureDisplaySize();
	ImGui::NewFrame();

	DrawOverlayToolbar();
	if (OverlayState.IsWindowVisible(DebugOverlayWindow::Console)) {
		DrawConsoleWindow();
	} else {
		SDLC_StopTextInput(ghMainWnd);
	}
	DrawInspectorWindow();
	DrawEditorWindow();

	ImGui::Render();
#ifdef USE_SDL3
	ImGui_ImplSDLRenderer3_RenderDrawData(ImGui::GetDrawData(), renderer);
#else
	ImGui_ImplSDLRenderer2_RenderDrawData(ImGui::GetDrawData(), renderer);
#endif
}

void DebugOverlayShutdown()
{
	if (!Initialized)
		return;

#ifdef USE_SDL3
	ImGui_ImplSDLRenderer3_Shutdown();
	ImGui_ImplSDL3_Shutdown();
#else
	ImGui_ImplSDLRenderer2_Shutdown();
	ImGui_ImplSDL2_Shutdown();
#endif
	ImGui::DestroyContext();
	Initialized = false;
	InitializedRenderer = nullptr;
}

} // namespace devilution

#else

namespace devilution {

bool DebugOverlayHandleEvent(const SDL_Event & /*event*/)
{
	return false;
}

bool DebugOverlayIsAvailable()
{
	return false;
}

bool DebugOverlayEditorOwnsPause()
{
	return false;
}

const std::optional<Point> &DebugOverlaySelectedTile()
{
	static const std::optional<Point> NoSelectedTile;
	return NoSelectedTile;
}

void DebugOverlayRender()
{
}

void DebugOverlayShutdown()
{
}

} // namespace devilution

#endif
