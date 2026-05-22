/**
 * @file levels/level.cpp
 *
 * Implementation of the Level class.
 */
#include "levels/level.hpp"

namespace devilution {

Level::Level(LevelId id)
	: id_(id)
{
}

/*static*/
Level Level::create(LevelId id)
{
	return Level(id);
}

} // namespace devilution
