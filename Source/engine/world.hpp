/**
 * @file engine/world.hpp
 *
 * The World class is the top-level owner of all game-session state.
 *
 * It holds:
 *   - Game-wide configuration (difficulty, flags, seeds).
 *   - A collection of Level objects, keyed by LevelIndex.
 *   - The concept of the "current level" that the local player occupies.
 *
 * A single global instance, `CurrentWorld`, is provided.  The free
 * function `currentLevel()` returns a reference to whichever Level the
 * local player is currently on.
 *
 * Relationship to existing globals
 * ----------------------------------
 * The globals in gendung.h (currlevel, leveltype, setlevel, …) and
 * diablo.h (DungeonSeeds, LevelSeeds, sgGameInitInfo, …) represent state
 * that logically belongs here.  They will be migrated incrementally;
 * in the meantime World::syncFromGlobals() / syncToGlobals() bridge the
 * gap.
 */
#pragma once

#include <cstdint>
#include <optional>
#include <unordered_map>

#include "levels/level.hpp"
#include "levels/gendung_defs.hpp"
#include "utils/attributes.h"

namespace devilution {

/** Number of regular dungeon levels (including town). Mirrors NUMLEVELS in diablo.h. */
constexpr int WorldNumLevels = 25;

/**
 * @brief Top-level container for all game-session state.
 *
 * Owns every Level that has been loaded during the current game session and
 * tracks which one the local player is currently occupying.
 */
class World {
public:
    World();
    ~World() = default;

    /** Non-copyable: owns all Level objects for the session. */
    World(const World &) = delete;
    World &operator=(const World &) = delete;

    // -------------------------------------------------------------------------
    // Level management
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // Current level
    // -------------------------------------------------------------------------

    /**
     * @brief Returns the Level the local player currently occupies.
     *
     * Always returns a valid Level; defaults to an empty town level.
     */
    [[nodiscard]] Level &currentLevel();
    [[nodiscard]] const Level &currentLevel() const;

    /**
     * @brief Creates (if needed) and activates the level at @p index.
     *
     * Updates currentLevel_ with the index and initializes it as a placeholder
     * if needed. Callers immediately fill in identity fields via gendung macros.
     */
    void switchLevel(LevelIndex index);

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /**
     * @brief Resets the world ready for a new game session.
     *
     * Clears cached levels and resets current level to empty town.
     */
    void reset();

private:
    /** The current level the local player is on. */
    std::unique_ptr<Level> currentLevel_;

    /**
     * Cache for other levels (not yet actively used, but available for future
     * caching of level state). Currently unused; kept for architecture compatibility.
     */
    std::unordered_map<LevelIndex, Level> cachedLevels_;
};

// ---------------------------------------------------------------------------
// Global instance and convenience accessor
// ---------------------------------------------------------------------------

/** @brief The single global World instance for the current game session. */
extern DVL_API_FOR_TEST World CurrentWorld;

/**
 * @brief Returns the Level the local player is currently on.
 *
 * Convenience wrapper around CurrentWorld.currentLevel().
 */
inline Level &currentLevel()
{
    return CurrentWorld.currentLevel();
}

/**
 * @brief Creates (if needed) and activates the level at @p index.
 *
 * Must be called before any gendung macro (currlevel, leveltype, …) is
 * first written on a level transition so that currentLevel() resolves
 * to a valid Level object.
 */
inline void SwitchCurrentLevel(LevelIndex index)
{
    CurrentWorld.switchLevel(index);
}

} // namespace devilution
