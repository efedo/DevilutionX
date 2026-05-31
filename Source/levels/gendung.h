/**
 * @file gendung.h
 *
 * Interface of general dungeon generation code.
 */
#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <span>
#include <string>
#include <string_view>

#include <expected.hpp>

#include "engine/clx_sprite.hpp"
#include "engine/point.hpp"
#include "engine/rectangle.hpp"
#include "engine/render/scrollrt.h"
#include "engine/world.hpp"
#include "engine/world_tile.hpp"
#include "levels/dun_tile.hpp"
#include "levels/gendung_defs.hpp"
#include "utils/attributes.h"
#include "utils/bitset2d.hpp"
#include "utils/enum_traits.h"

namespace devilution {

// _setlevels, _difficulty, DungeonFlag, THEME_LOC, MegaTile, ShadowStruct,
// MAXTHEMES and MAXTILES are defined in gendung_defs.hpp (included above).

inline bool IsArenaLevel(_setlevels setLevel)
{
	switch (setLevel) {
	case SL_ARENA_CHURCH:
	case SL_ARENA_HELL:
	case SL_ARENA_CIRCLE_OF_LIFE:
		return true;
	default:
		return false;
	}
}

tl::expected<dungeon_type, std::string> ParseDungeonType(std::string_view value);
tl::expected<_setlevels, std::string> ParseSetLevel(std::string_view value);

// ---------------------------------------------------------------------------
// Macro shims — each name expands to the corresponding Level member so that
// all existing call sites (assignments, address-of, array indexing, …)
// continue to work without modification.
// ---------------------------------------------------------------------------
// clang-format off
/** Reprecents what tiles are being utilized in the generated map. */
#define DungeonMask   (currentLevel().DungeonMask_)
/** Contains the tile IDs of the map. */
#define dungeon       (currentLevel().dungeon_)
/** Contains a backup of the tile IDs of the map. */
#define pdungeon      (currentLevel().pdungeon_)
/** Tile that may not be overwritten by the level generator */
#define Protected     (currentLevel().Protected_)
#define SetPieceRoom  (currentLevel().SetPieceRoom_)
/** Specifies the active set quest piece in coordinate. */
#define SetPiece      (currentLevel().SetPiece_)
#define pSpecialCels  (currentLevel().pSpecialCels_)
/** Specifies the tile definitions of the active dungeon type. */
#define pMegaTiles    (currentLevel().pMegaTiles_)
#define pDungeonCels  (currentLevel().pDungeonCels_)
/** List tile properties */
#define SOLData       (currentLevel().SOLData_)
/** Specifies the minimum X,Y-coordinates of the map. */
#define dminPosition  (currentLevel().dminPosition_)
/** Specifies the maximum X,Y-coordinates of the map. */
#define dmaxPosition  (currentLevel().dmaxPosition_)


/** Specifies the active dungeon type of the current game. */
#define leveltype     (currentLevel().leveltype_)
/** Specifies the active dungeon level of the current game. */
#define currlevel     (currentLevel().currlevel_)
#define setlevel      (currentLevel().setlevel_)
/** Specifies the active quest level of the current game. */
#define setlvlnum     (currentLevel().setlvlnum_)
/** Specifies the dungeon type of the active quest level of the current game. */
#define setlvltype    (currentLevel().setlvltype_)

///** Specifies the active dungeon type of the current game. */
//extern DVL_API_FOR_TEST dungeon_type leveltype;
///** Specifies the active dungeon level of the current game. */
//extern DVL_API_FOR_TEST uint8_t currlevel;
//extern bool setlevel;
///** Specifies the active quest level of the current game. */
//extern _setlevels setlvlnum;
///** Specifies the dungeon type of the active quest level of the current game. */
//extern dungeon_type setlvltype;

/** Specifies the player viewpoint X,Y-coordinates of the map. */
#define ViewPosition  (currentLevel().ViewPosition_)
#define MicroTileLen  (currentLevel().MicroTileLen_)
#define TransVal      (currentLevel().TransVal_)
/** Specifies the active transparency indices. */
#define TransList     (currentLevel().TransList_)
/** Contains the piece IDs of each tile on the map. */
#define dPiece        (currentLevel().dPiece_)
/** Map of micros that comprises a full tile for any given dungeon piece. */
// LEGACY macro kept for transition; prefer levelMicros() for new code.
#define DPieceMicros  (currentLevel().DPieceMicros_)
/** Specifies the transparency at each coordinate of the map. */
#define dTransVal     (currentLevel().dTransVal_)
/** Holds various information about dungeon tiles, @see DungeonFlag */
#define themeCount    (currentLevel().themeCount_)
#define themeLoc      (currentLevel().themeLoc_)

// ---------------------------------------------------------------------------
// NEW: Tile-based access (Phase 2 migration)
// ---------------------------------------------------------------------------
/** Direct access to the consolidated Tile array. Use tiles[x][y].method() */
#define tiles         (currentLevel().tiles_)
/** Accessor helper for getting a tile. Use tileAt(x, y) or tileAt(Point) */
#define tileAt        (currentLevel().tileAt)

// clang-format on

/**
 * @brief Returns a std::span over the level's MICROS lookup table.
 *
 * Prefer this over the legacy DPieceMicros macro for new code.
 * The table is indexed by piece ID (0..MAXTILES-1) and maps each piece
 * to its LevelCelBlock data for rendering.
 */
[[nodiscard]] inline std::span<MICROS, MAXTILES> levelMicros()
{
	return std::span<MICROS, MAXTILES>(currentLevel().DPieceMicros_);
}

// clang-format on

#ifdef BUILD_TESTING
std::optional<WorldTileSize> GetSizeForThemeRoom();
void SyncTilesFromLegacyMapsForTesting();
#endif

dungeon_type GetLevelType(int level);
void CreateDungeon(uint32_t rseed, lvl_entry entry);

DVL_ALWAYS_INLINE constexpr bool InDungeonBounds(Point position)
{
	return position.x >= 0 && position.x < MAXDUNX && position.y >= 0 && position.y < MAXDUNY;
}

/**
 * @brief Checks if a given tile contains at least one missile
 * @param position Coordinates of the dungeon tile to check
 * @return true if a missile exists at this position
 */
DVL_ALWAYS_INLINE bool TileContainsMissile(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::Missile);
}

/**
 * @brief Checks if a given tile contains a player corpse
 * @param position Coordinates of the dungeon tile to check
 * @return true if a dead player exists at this position
 */
DVL_ALWAYS_INLINE bool TileContainsDeadPlayer(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::DeadPlayer);
}

/**
 * @brief Check if a given tile contains a decorative object (or similar non-pathable set piece)
 *
 * This appears to include stairs so that monsters do not spawn or path onto them, but players can path to them to navigate between layers
 *
 * @param position Coordinates of the dungeon tile to check
 * @return true if a set piece was spawned at this position
 */
DVL_ALWAYS_INLINE bool TileContainsSetPiece(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::Populated);
}

/**
 * @brief Checks if any player can currently see this tile
 *
 * Currently only used by monster AI routines so basic monsters out of sight can be ignored until they're likely to interact with the player
 *
 * @param position Coordinates of the dungeon tile to check
 * @return true if the tile is within at least one players vision
 */
DVL_ALWAYS_INLINE bool IsTileVisible(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::Visible);
}

/**
 * @brief Checks if a light source is illuminating this tile
 * @param position Coordinates of the dungeon tile to check
 * @return true if the tile is within the radius of at least one light source
 */
DVL_ALWAYS_INLINE bool IsTileLit(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::Lit);
}

struct Miniset {
	WorldTileSize size;
	/* these are indexed as [y][x] */
	uint8_t search[6][6];
	uint8_t replace[6][6];

	/**
	 * @param position Coordinates of the dungeon tile to check
	 * @param respectProtected Match bug from Crypt levels if false
	 */
	bool matches(WorldTilePosition position, bool respectProtected = true) const
	{
		for (WorldTileCoord yy = 0; yy < size.height; yy++) {
			for (WorldTileCoord xx = 0; xx < size.width; xx++) {
				if (search[yy][xx] != 0 && dungeon[xx + position.x][yy + position.y] != search[yy][xx])
					return false;
				if (respectProtected && Protected.test(xx + position.x, yy + position.y))
					return false;
			}
		}
		return true;
	}

	void place(WorldTilePosition position, bool protect = false) const
	{
		for (WorldTileCoord y = 0; y < size.height; y++) {
			for (WorldTileCoord x = 0; x < size.width; x++) {
				if (replace[y][x] == 0)
					continue;
				dungeon[x + position.x][y + position.y] = replace[y][x];
				if (protect)
					Protected.set(x + position.x, y + position.y);
			}
		}
	}
};

DVL_ALWAYS_INLINE bool TileHasAny(Point coords, TileProperties property)
{
	uint16_t piece = tileAt(coords).piece();
	if (piece == 0) {
		piece = dPiece[coords.x][coords.y];
	}
	return HasAnyOf(SOLData[piece], property);
}

tl::expected<void, std::string> LoadLevelSOLData();
void SetDungeonMicros(std::unique_ptr<std::byte[]> &dungeonCels, uint_fast8_t &microTileLen);
void DRLG_InitTrans();
void DRLG_MRectTrans(WorldTilePosition origin, WorldTilePosition extent);
void DRLG_MRectTrans(WorldTileRectangle area);
void DRLG_RectTrans(WorldTileRectangle area);
void DRLG_CopyTrans(int sx, int sy, int dx, int dy);
void LoadTransparency(const uint16_t *dunData);
void LoadDungeonBase(const char *path, Point spawn, int floorId, int dirtId);
void Make_SetPC(WorldTileRectangle area);
/**
 * @param miniset The miniset to place
 * @param tries Tiles to try, 1600 will scan the full map
 * @param drlg1Quirk Match buggy behaviour of Diablo's Cathedral
 */
std::optional<Point> PlaceMiniSet(const Miniset &miniset, int tries = 199, bool drlg1Quirk = false);
void PlaceDunTiles(const uint16_t *dunData, Point position, int floorId = 0);
void DRLG_PlaceThemeRooms(int minSize, int maxSize, int floor, int freq, bool rndSize);
void DRLG_HoldThemeRooms();
/**
 * @brief Returns the size in tiles of the specified ".dun" Data
 */
WorldTileSize GetDunSize(const uint16_t *dunData);
void DRLG_LPass3(int lv);

/**
 * @brief Checks if a theme room is located near the target point
 * @param position Target location in dungeon coordinates
 * @return True if a theme room is near (within 2 tiles of) this point, false if it is free.
 */
bool IsNearThemeRoom(WorldTilePosition position);
void InitLevels();
void FloodTransparencyValues(uint8_t floorID);

} // namespace devilution
