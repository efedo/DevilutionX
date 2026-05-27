#pragma once

#ifdef USE_SDL3
#include <SDL3/SDL_events.h>
#else
#include <SDL.h>
#endif

namespace devilution {

bool DebugOverlayHandleEvent(const SDL_Event &event);
bool DebugOverlayIsAvailable();
void DebugOverlayRender();
void DebugOverlayShutdown();

} // namespace devilution
