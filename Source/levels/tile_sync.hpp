/**
 * @file levels/tile_sync.hpp
 *
 * Synchronization utilities to keep legacy arrays in sync with Tile array
 * during Phase 2 migration. These will be removed in Phase 4.
 */
#pragma once

#include "levels/gendung_defs.hpp"
#include "levels/tile.hpp"

namespace devilution {

/**
 * @brief Sync all tile data from Tile array to legacy arrays.
 * 
 * During migration, use this to ensure legacy code sees the same data
 * as code using the new Tile API.
 * 
 * @param tiles The Tile array to sync from
 * @param dPiece Legacy piece array
 * @param dTransVal Legacy transparency array
 * @param dLight Legacy light array
 * @param dPreLight Legacy pre-light array
 * @param dFlags Legacy flags array
 * @param dPlayer Legacy player array
 * @param dMonster Legacy monster array
 * @param dCorpse Legacy corpse array
 * @param dObject Legacy object array
 * @param dSpecial Legacy special array
 * @param dItem Legacy item array
 */
inline void SyncTilesToLegacy(
	const Tile tiles[MAXDUNX][MAXDUNY],
	uint16_t dPiece[MAXDUNX][MAXDUNY],
	int8_t dTransVal[MAXDUNX][MAXDUNY],
	uint8_t dLight[MAXDUNX][MAXDUNY],
	uint8_t dPreLight[MAXDUNX][MAXDUNY],
	DungeonFlag dFlags[MAXDUNX][MAXDUNY],
	int8_t dPlayer[MAXDUNX][MAXDUNY],
	int16_t dMonster[MAXDUNX][MAXDUNY],
	int8_t dCorpse[MAXDUNX][MAXDUNY],
	int8_t dObject[MAXDUNX][MAXDUNY],
	int8_t dSpecial[MAXDUNX][MAXDUNY],
	int8_t dItem[MAXDUNX][MAXDUNY])
{
	for (int y = 0; y < MAXDUNY; y++) {
		for (int x = 0; x < MAXDUNX; x++) {
			const Tile &tile = tiles[x][y];
			dPiece[x][y] = tile.piece();
			dTransVal[x][y] = tile.transVal();
			dLight[x][y] = tile.light();
			dPreLight[x][y] = tile.preLight();
			dFlags[x][y] = tile.flags();
			dPlayer[x][y] = tile.player();
			dMonster[x][y] = tile.monster();
			dCorpse[x][y] = tile.corpse();
			dObject[x][y] = tile.object();
			dSpecial[x][y] = tile.special();
			dItem[x][y] = tile.item();
		}
	}
}

/**
 * @brief Sync all tile data from legacy arrays to Tile array.
 * 
 * During migration, use this when legacy code has modified arrays
 * and you need to ensure Tile API sees the changes.
 * 
 * @param dPiece Legacy piece array
 * @param dTransVal Legacy transparency array
 * @param dLight Legacy light array
 * @param dPreLight Legacy pre-light array
 * @param dFlags Legacy flags array
 * @param dPlayer Legacy player array
 * @param dMonster Legacy monster array
 * @param dCorpse Legacy corpse array
 * @param dObject Legacy object array
 * @param dSpecial Legacy special array
 * @param dItem Legacy item array
 * @param tiles The Tile array to sync to
 */
inline void SyncLegacyToTiles(
	const uint16_t dPiece[MAXDUNX][MAXDUNY],
	const int8_t dTransVal[MAXDUNX][MAXDUNY],
	const uint8_t dLight[MAXDUNX][MAXDUNY],
	const uint8_t dPreLight[MAXDUNX][MAXDUNY],
	const DungeonFlag dFlags[MAXDUNX][MAXDUNY],
	const int8_t dPlayer[MAXDUNX][MAXDUNY],
	const int16_t dMonster[MAXDUNX][MAXDUNY],
	const int8_t dCorpse[MAXDUNX][MAXDUNY],
	const int8_t dObject[MAXDUNX][MAXDUNY],
	const int8_t dSpecial[MAXDUNX][MAXDUNY],
	const int8_t dItem[MAXDUNX][MAXDUNY],
	Tile tiles[MAXDUNX][MAXDUNY])
{
	for (int y = 0; y < MAXDUNY; y++) {
		for (int x = 0; x < MAXDUNX; x++) {
			Tile &tile = tiles[x][y];
			tile.setPiece(dPiece[x][y]);
			tile.setTransVal(dTransVal[x][y]);
			tile.setLight(dLight[x][y]);
			tile.setPreLight(dPreLight[x][y]);
			tile.setFlags(dFlags[x][y]);
			tile.setPlayer(dPlayer[x][y]);
			tile.setMonster(dMonster[x][y]);
			tile.setCorpse(dCorpse[x][y]);
			tile.setObject(dObject[x][y]);
			tile.setSpecial(dSpecial[x][y]);
			tile.setItem(dItem[x][y]);
		}
	}
}

/**
 * @brief Sync a single tile coordinate between Tile and legacy arrays.
 * 
 * @param x X coordinate
 * @param y Y coordinate
 * @param tileToLegacy If true, sync from Tile to legacy; if false, sync from legacy to Tile
 * @param tiles The Tile array
 * @param dPiece Legacy piece array
 * @param dTransVal Legacy transparency array
 * @param dLight Legacy light array
 * @param dPreLight Legacy pre-light array
 * @param dFlags Legacy flags array
 * @param dPlayer Legacy player array
 * @param dMonster Legacy monster array
 * @param dCorpse Legacy corpse array
 * @param dObject Legacy object array
 * @param dSpecial Legacy special array
 * @param dItem Legacy item array
 */
inline void SyncSingleTile(
	int x, int y, bool tileToLegacy,
	Tile tiles[MAXDUNX][MAXDUNY],
	uint16_t dPiece[MAXDUNX][MAXDUNY],
	int8_t dTransVal[MAXDUNX][MAXDUNY],
	uint8_t dLight[MAXDUNX][MAXDUNY],
	uint8_t dPreLight[MAXDUNX][MAXDUNY],
	DungeonFlag dFlags[MAXDUNX][MAXDUNY],
	int8_t dPlayer[MAXDUNX][MAXDUNY],
	int16_t dMonster[MAXDUNX][MAXDUNY],
	int8_t dCorpse[MAXDUNX][MAXDUNY],
	int8_t dObject[MAXDUNX][MAXDUNY],
	int8_t dSpecial[MAXDUNX][MAXDUNY],
	int8_t dItem[MAXDUNX][MAXDUNY])
{
	if (tileToLegacy) {
		const Tile &tile = tiles[x][y];
		dPiece[x][y] = tile.piece();
		dTransVal[x][y] = tile.transVal();
		dLight[x][y] = tile.light();
		dPreLight[x][y] = tile.preLight();
		dFlags[x][y] = tile.flags();
		dPlayer[x][y] = tile.player();
		dMonster[x][y] = tile.monster();
		dCorpse[x][y] = tile.corpse();
		dObject[x][y] = tile.object();
		dSpecial[x][y] = tile.special();
		dItem[x][y] = tile.item();
	} else {
		Tile &tile = tiles[x][y];
		tile.setPiece(dPiece[x][y]);
		tile.setTransVal(dTransVal[x][y]);
		tile.setLight(dLight[x][y]);
		tile.setPreLight(dPreLight[x][y]);
		tile.setFlags(dFlags[x][y]);
		tile.setPlayer(dPlayer[x][y]);
		tile.setMonster(dMonster[x][y]);
		tile.setCorpse(dCorpse[x][y]);
		tile.setObject(dObject[x][y]);
		tile.setSpecial(dSpecial[x][y]);
		tile.setItem(dItem[x][y]);
	}
}

} // namespace devilution
