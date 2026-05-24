/**
 * @file levels/level.hpp
 *
 * The Level class owns all data for a single dungeon level: its identity,
 * tile/dungeon-generation grids, entity occupation maps, lighting tables,
 * theme rooms and per-level monster catalogue (Bestiary).
 *
 * Data members use a trailing underscore to distinguish them from the
 * macro aliases in gendung.h.  Each macro expands to currentLevel().member_
 * so all existing call sites compile and behave correctly without any change.
 */
#pragma once

#include <array>
#include <cstdint>
#include <memory>
#include <optional>

#include "engine/bestiary.hpp"
#include "engine/clx_sprite.hpp"
#include "engine/point.hpp"
#include "engine/rectangle.hpp"
#include "engine/world_tile.hpp"
#include "levels/dun_tile.hpp"
#include "levels/gendung_defs.hpp"
#include "utils/bitset2d.hpp"

namespace devilution {

// ---------------------------------------------------------------------------
// LevelIndex / LevelId
// ---------------------------------------------------------------------------

/** Key for storing a Level inside World. Regular levels 0-24 map directly to their number. */
using LevelIndex = int8_t;

/** Sentinel: "no level / not loaded". */
constexpr LevelIndex LevelIndexNone = -1;

/** Immutable identity of a dungeon level. */
struct LevelId {
	uint8_t      levelNum   = 0;
	dungeon_type type       = DTYPE_TOWN;
	bool         isSetLevel = false;
	_setlevels   setLevelId = SL_NONE;

	bool operator==(const LevelId &) const = default;
};

// ---------------------------------------------------------------------------
// Level
// ---------------------------------------------------------------------------

/**
 * @brief All data that belongs to a single dungeon level.
 *
 * Every field previously a standalone global in gendung.cpp lives here.
 * Member names carry a trailing underscore; gendung.h provides macro aliases
 * with the original names that expand to currentLevel().member_ so no
 * existing call site needs to change.
 */
class Level {
public:
	Level(const Level &) = delete;
	Level &operator=(const Level &) = delete;

	Level(Level &&) = default;
	Level &operator=(Level &&) = default;

	// -------------------------------------------------------------------------
	// Identity
	// -------------------------------------------------------------------------
	[[nodiscard]] const LevelId &id()         const { return id_; }
	[[nodiscard]] uint8_t        levelNum()   const { return id_.levelNum; }
	[[nodiscard]] dungeon_type   type()       const { return id_.type; }
	[[nodiscard]] bool           isSetLevel() const { return id_.isSetLevel; }
	[[nodiscard]] _setlevels     setLevelId() const { return id_.setLevelId; }

	/** @brief Updates the level identity without recreating the level data. */
	void setId(const LevelId &newId) { id_ = newId; }

	// -------------------------------------------------------------------------
	// Monster catalogue
	// -------------------------------------------------------------------------
	Bestiary bestiary;

	// -------------------------------------------------------------------------
	// Dungeon generation grids  (previously in gendung.cpp)
	// -------------------------------------------------------------------------

	/** @see DungeonMask */
	Bitset2d<DMAXX, DMAXY> DungeonMask_;
	/** @see dungeon */
	uint8_t dungeon_[DMAXX][DMAXY] = {};
	/** @see pdungeon */
	uint8_t pdungeon_[DMAXX][DMAXY] = {};
	/** @see Protected */
	Bitset2d<DMAXX, DMAXY> Protected_;
	/** @see SetPieceRoom */
	WorldTileRectangle SetPieceRoom_ = {};
	/** @see SetPiece */
	WorldTileRectangle SetPiece_ = {};
	/** @see pSpecialCels */
	OptionalOwnedClxSpriteList pSpecialCels_;
	/** @see pMegaTiles */
	std::unique_ptr<MegaTile[]> pMegaTiles_;
	/** @see pDungeonCels */
	std::unique_ptr<std::byte[]> pDungeonCels_;
	/** @see SOLData */
	TileProperties SOLData_[MAXTILES] = {};
	/** @see dminPosition */
	WorldTilePosition dminPosition_ = {};
	/** @see dmaxPosition */
	WorldTilePosition dmaxPosition_ = {};

	// -------------------------------------------------------------------------
	// Level identity  (previously in gendung.cpp)
	// -------------------------------------------------------------------------
	/** @see leveltype */
	dungeon_type leveltype_ = DTYPE_TOWN;
	/** @see currlevel */
	uint8_t currlevel_ = 0;
	/** @see setlevel */
	bool setlevel_ = false;
	/** @see setlvlnum */
	_setlevels setlvlnum_ = SL_NONE;
	/** @see setlvltype */
	dungeon_type setlvltype_ = DTYPE_TOWN;

	// -------------------------------------------------------------------------
	// View / rendering
	// -------------------------------------------------------------------------
	/** @see ViewPosition */
	Point ViewPosition_ = {};
	/** @see MicroTileLen */
	uint_fast8_t MicroTileLen_ = 0;
	/** @see TransVal */
	int8_t TransVal_ = 0;
	/** @see TransList */
	std::array<bool, 256> TransList_ = {};

	// -------------------------------------------------------------------------
	// Per-tile maps  (MAXDUNX × MAXDUNY)
	// -------------------------------------------------------------------------
	/** @see dPiece */
	uint16_t dPiece_[MAXDUNX][MAXDUNY] = {};
	/** @see DPieceMicros */
	MICROS DPieceMicros_[MAXTILES] = {};
	/** @see dTransVal */
	int8_t dTransVal_[MAXDUNX][MAXDUNY] = {};
	/** @see dLight */
	uint8_t dLight_[MAXDUNX][MAXDUNY] = {};
	/** @see dPreLight */
	uint8_t dPreLight_[MAXDUNX][MAXDUNY] = {};
	/** @see dFlags */
	DungeonFlag dFlags_[MAXDUNX][MAXDUNY] = {};
	/** @see dPlayer */
	int8_t dPlayer_[MAXDUNX][MAXDUNY] = {};
	/** @see dMonster */
	int16_t dMonster_[MAXDUNX][MAXDUNY] = {};
	/** @see dCorpse */
	int8_t dCorpse_[MAXDUNX][MAXDUNY] = {};
	/** @see dObject */
	int8_t dObject_[MAXDUNX][MAXDUNY] = {};
	/** @see dSpecial */
	int8_t dSpecial_[MAXDUNX][MAXDUNY] = {};

	// -------------------------------------------------------------------------
	// Theme rooms
	// -------------------------------------------------------------------------
	/** @see themeCount */
	int themeCount_ = 0;
	/** @see themeLoc */
	THEME_LOC themeLoc_[MAXTHEMES] = {};

	// -------------------------------------------------------------------------
	// Factory
	// -------------------------------------------------------------------------
	explicit Level(LevelId id);
	static Level create(LevelId id);

private:
	LevelId id_;
};

} // namespace devilution
