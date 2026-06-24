/**
 * @file floatingnumbers.h
 *
 * Adds floating numbers QoL feature
 */
#pragma once

#include <string>

#include "ui/menu/ui_flags.hpp"
#include "engine/math/displacement.hpp"
#include "engine/math/point.hpp"
#include "engine/gfx/surface.hpp"

namespace devilution {

void AddFloatingNumber(Point pos, Displacement offset, std::string text, UiFlags style, int id = 0, bool reverseDirection = false);
void DrawFloatingNumbers(const Surface &out, Point viewPosition, Displacement offset);
void ClearFloatingNumbers();

} // namespace devilution
