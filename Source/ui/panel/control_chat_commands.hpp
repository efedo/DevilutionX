#pragma once

/**
 * @file ui/panel/control_chat_commands.hpp
 *
 * Interface for chat commands.
 */


#include <string_view>

namespace devilution {

bool CheckChatCommand(std::string_view text);

} // namespace devilution
