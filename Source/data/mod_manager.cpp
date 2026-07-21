/**
 * @file data/mod_manager.cpp
 *
 * Active mod archive management.
 */


#include "data/mod_manager.hpp"

#include "engine/assets.hpp"
#include "persistence/options.h"

namespace devilution {

ModManager CurrentModManager;

std::vector<std::string_view> ModManager::ReloadActiveMods()
{
	UnloadModArchives();
	std::vector<std::string_view> activeMods = GetOptions().Mods.GetActiveModList();
	LoadModArchives(activeMods);
	return activeMods;
}

} // namespace devilution
