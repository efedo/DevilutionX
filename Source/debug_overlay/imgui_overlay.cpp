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

#include "debug_overlay/console_history.hpp"
#include "engine/dx.h"
#include "panels/console.hpp"
#include "utils/display.h"
#include "utils/sdl_compat.h"

namespace devilution {
namespace {

bool Initialized;
SDL_Renderer *InitializedRenderer;
char InputBuffer[4096];
DebugConsoleHistory History;
bool ScrollToBottom;
bool RefocusInput;

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

void DrawConsoleWindow()
{
	if (!IsConsoleOpen())
		return;

	InitConsole();

	bool open = true;
	ImGui::SetNextWindowSize(ImVec2(760.0F, 360.0F), ImGuiCond_FirstUseEver);
	if (!ImGui::Begin("Debug Console", &open, ImGuiWindowFlags_NoCollapse)) {
		ImGui::End();
		if (!open)
			CloseConsole();
		return;
	}
	if (!open)
		CloseConsole();

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

	ImGui::End();
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
		RefocusInput = true;
		return true;
	}

	if (!IsConsoleOpen())
		return false;

	if (event.type == SDL_EVENT_KEY_DOWN && SDLC_EventKey(event) == SDLK_ESCAPE) {
		CloseConsole();
		return true;
	}

	return IsOverlayInputEvent(event);
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

	DrawConsoleWindow();

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
