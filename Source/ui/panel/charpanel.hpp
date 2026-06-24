#pragma once

#include <string>

#include <expected.hpp>

#include "engine/gfx/clx_sprite.hpp"
#include "engine/gfx/surface.hpp"

namespace devilution {

extern OptionalOwnedClxSpriteList pChrButtons;

void DrawChr(const Surface &);
tl::expected<void, std::string> LoadCharPanel();
void FreeCharPanel();

} // namespace devilution
