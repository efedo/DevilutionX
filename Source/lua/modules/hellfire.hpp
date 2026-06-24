#pragma once

/**
 * @file lua/modules/hellfire.hpp
 *
 * Interface for hellfire.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaHellfireModule(sol::state_view &lua);

} // namespace devilution
