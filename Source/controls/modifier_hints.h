#pragma once

#include "engine/gfx/surface.hpp"

namespace devilution {

void DrawControllerModifierHints(const Surface &out);
void InitModifierHints();
void FreeModifierHints();

} // namespace devilution
