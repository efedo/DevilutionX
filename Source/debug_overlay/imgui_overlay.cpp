#include "debug_overlay/imgui_overlay.hpp"

#if defined(_DEBUG) && !defined(USE_SDL1)

#include <cfloat>
#include <string>
#include <string_view>

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
#include "engine/dx.h"
#include "items.h"
#include "levels/gendung.h"
#include "levels/tile_properties.hpp"
#include "monster.h"
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

bool ConsoleWindowVisible = false;
bool InspectorWindowVisible = false;
bool InspectorWindowAlwaysActive = false;

std::string_view BoolToString(bool value)
{
	return value ? "true" : "false";
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
	const Point tile = cursPosition;
	if (!InDungeonBounds(tile)) {
		ImGui::TextUnformatted("Cursor tile is out of bounds.");
		return;
	}

	const Tile &dungeonTile = tileAt(tile);
	ImGui::Text("Hovered tile: %d, %d", tile.x, tile.y);
	ImGui::Text("Solid: %s   Walkable: %s   Occupied: %s", BoolToString(IsTileSolid(tile)).data(), BoolToString(IsTileWalkable(tile)).data(), BoolToString(IsTileOccupied(tile)).data());
	DrawValue("Position", StrCat(tile.x, ", ", tile.y));
	DrawValue("Piece", dungeonTile.piece());
	DrawValue("TransVal", dungeonTile.transVal());
	DrawValue("Light", dungeonTile.light());
	DrawValue("PreLight", dungeonTile.preLight());
	DrawValue("Flags", static_cast<int>(dungeonTile.flags()));
	DrawValue("Monster Index", dungeonTile.monster());
	DrawValue("Object Index", dungeonTile.object());
	DrawValue("Item Index", dungeonTile.item());
	DrawValue("Corpse", dungeonTile.corpse());
	DrawValue("Special", dungeonTile.special());
	DrawValue("Player", dungeonTile.player());

	if (ObjectUnderCursor != nullptr || dungeonTile.object() != 0) {
		DrawSectionHeader("Object");
		if (ObjectUnderCursor != nullptr) {
			const Object &object = *ObjectUnderCursor;
			ImGui::Text("Object: %s (%d)", object.name().str().data(), static_cast<int>(object._otype));
			DrawValue("Name", object.name().str());
			DrawValue("Type", static_cast<int>(object._otype));
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
			DrawValue("Name", monster.name());
			DrawValue("Mode", static_cast<int>(monster.mode));
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
			DrawValue("Name", item.getName().str());
			DrawValue("Type", static_cast<int>(item._itype));
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

void DrawDebugToolbar()
{
	ImGui::SetNextWindowPos(ImVec2(10.0F, 10.0F), ImGuiCond_Always);
	ImGui::SetNextWindowBgAlpha(0.85F);
	constexpr ImGuiWindowFlags flags = ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoFocusOnAppearing | ImGuiWindowFlags_NoNav;
	if (!ImGui::Begin("Debug Toolbar", nullptr, flags)) {
		ImGui::End();
		return;
	}

	const auto drawToggleButton = [](const char *label, bool active) {
		if (active) {
			ImGui::PushStyleColor(ImGuiCol_Button, ImGui::GetStyleColorVec4(ImGuiCol_ButtonActive));
			ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImGui::GetStyleColorVec4(ImGuiCol_ButtonActive));
		}
		const bool pressed = ImGui::Button(label);
		if (active) {
			ImGui::PopStyleColor(2);
		}
		return pressed;
	};

	if (drawToggleButton("Console", ConsoleWindowVisible)) {
		ConsoleWindowVisible = !ConsoleWindowVisible;
		if (ConsoleWindowVisible)
			RefocusInput = true;
	}
	ImGui::SameLine();
	if (drawToggleButton("Inspector", InspectorWindowVisible)) {
		InspectorWindowVisible = !InspectorWindowVisible;
		InspectorWindowAlwaysActive = InspectorWindowVisible;
	}
	ImGui::SameLine();
	if (ImGui::Button("Hide All")) {
		ConsoleWindowVisible = false;
		InspectorWindowVisible = false;
		InspectorWindowAlwaysActive = false;
		CloseConsole();
	}

	ImGui::End();
}

void DrawConsoleWindow()
{
	if (!ConsoleWindowVisible)
		return;

	InitConsole();
	SDLC_StartTextInput(ghMainWnd);

	bool open = true;
	ImGui::SetNextWindowSize(ImVec2(760.0F, 360.0F), ImGuiCond_FirstUseEver);
	if (!ImGui::Begin("Debug Console", &open, ImGuiWindowFlags_NoCollapse)) {
		ImGui::End();
		if (!open)
			ConsoleWindowVisible = false;
		return;
	}
	if (!open)
		ConsoleWindowVisible = false;

	DrawConsoleContent();
	ImGui::End();
}

void DrawInspectorWindow()
{
	if (!InspectorWindowVisible)
		return;

	bool open = true;
	ImGui::SetNextWindowSize(ImVec2(180.0F, 0.0F), ImGuiCond_FirstUseEver);
	if (!ImGui::Begin("Inspector", &open, ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_AlwaysAutoResize)) {
		ImGui::End();
		if (!open)
			InspectorWindowVisible = false;
		return;
	}
	if (!open)
		InspectorWindowVisible = false;

	DrawInspectorContent();
	ImGui::End();
}

void DrawDebugOverlayWindow()
{
	if (!IsConsoleOpen())
		return;

	DrawDebugToolbar();
	if (ConsoleWindowVisible) {
		DrawConsoleWindow();
	} else {
		SDLC_StopTextInput(ghMainWnd);
	}
	if (InspectorWindowAlwaysActive) {
		DrawInspectorWindow();
	} else if (InspectorWindowVisible) {
		DrawInspectorWindow();
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
	if (!EnsureInitialized()) {
		return false;
	}

	ProcessBackendEvent(event);

	if (event.type == SDL_EVENT_KEY_DOWN && SDLC_EventScancode(event) == SDL_SCANCODE_GRAVE) {
		OpenConsole();
		ConsoleWindowVisible = false;
		InspectorWindowVisible = false;
		InspectorWindowAlwaysActive = false;
		return true;
	}

	if (!IsConsoleOpen())
		return false;

	if (event.type == SDL_EVENT_KEY_DOWN && SDLC_EventKey(event) == SDLK_ESCAPE) {
		CloseConsole();
		return true;
	}

	// Let ImGui decide if it wants the event based on whether it's capturing keyboard/mouse input
	const ImGuiIO &io = ImGui::GetIO();
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
	ImGui::NewFrame();

	DrawDebugOverlayWindow();

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

void DebugOverlayRender()
{
}

void DebugOverlayShutdown()
{
}

} // namespace devilution

#endif
