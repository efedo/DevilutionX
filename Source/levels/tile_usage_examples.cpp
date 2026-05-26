/**
 * @file levels/tile_usage_examples.cpp
 *
 * Examples showing how to use the Tile class and migration patterns
 * from the old array-based approach.
 */

#include "levels/tile.hpp"
#include "levels/level.hpp"
#include "engine/point.hpp"

// This file is NOT compiled - it's just documentation via code examples

namespace devilution {
namespace examples {

// =============================================================================
// EXAMPLE 1: Basic Tile Usage
// =============================================================================

void basicTileUsage() {
	Tile tile;

	// Set basic properties
	tile.setPiece(42);
	tile.setLight(128);
	tile.setPlayer(1);

	// Query properties
	if (tile.hasPlayer()) {
		int playerId = tile.player();
		// do something with playerId
	}

	// Work with flags
	tile.addFlags(DungeonFlag::Visible | DungeonFlag::Explored);
	if (tile.isVisible() && tile.isExplored()) {
		// tile is known to player
	}

	// Clear tile
	tile.clear();
}

// =============================================================================
// EXAMPLE 2: Migration Pattern - Reading Tile Data
// =============================================================================

void readTileData(Point position) {
	// OLD WAY (before Tile class):
	// uint16_t piece = dPiece[position.x][position.y];
	// int8_t player = dPlayer[position.x][position.y];
	// int16_t monster = dMonster[position.x][position.y];
	// DungeonFlag flags = dFlags[position.x][position.y];

	// NEW WAY (with Tile class):
	const Tile& tile = currentLevel().tiles_[position.x][position.y];
	uint16_t piece = tile.piece();
	int8_t player = tile.player();
	int16_t monster = tile.monster();
	DungeonFlag flags = tile.flags();

	// Even better - use convenience methods:
	if (tile.hasMonster() && tile.isVisible()) {
		// monster is visible to player
	}
}

// =============================================================================
// EXAMPLE 3: Migration Pattern - Writing Tile Data
// =============================================================================

void writeTileData(Point position, int playerId) {
	// OLD WAY:
	// dPiece[position.x][position.y] = 42;
	// dPlayer[position.x][position.y] = playerId;
	// dFlags[position.x][position.y] |= DungeonFlag::Visible;

	// NEW WAY:
	Tile& tile = currentLevel().tiles_[position.x][position.y];
	tile.setPiece(42);
	tile.setPlayer(playerId);
	tile.addFlags(DungeonFlag::Visible);
}

// =============================================================================
// EXAMPLE 4: Migration Pattern - Multiple Array Access
// =============================================================================

bool canPlaceMonster(Point position) {
	// OLD WAY - requires checking multiple arrays:
	// if (dPlayer[position.x][position.y] != 0) return false;
	// if (dMonster[position.x][position.y] != 0) return false;
	// if (dObject[position.x][position.y] > 0) return false;  // positive = solid object
	// if (HasAnyOf(dFlags[position.x][position.y], DungeonFlag::Populated)) return false;
	// return true;

	// NEW WAY - single tile access:
	const Tile& tile = currentLevel().tiles_[position.x][position.y];
	if (tile.hasPlayer()) return false;
	if (tile.hasMonster()) return false;
	if (tile.hasObject() && !tile.isObjectExtension()) return false;
	if (tile.isPopulated()) return false;
	return true;

	// EVEN BETTER - use helper method:
	return tile.isPassable() && !tile.isPopulated();
}

// =============================================================================
// EXAMPLE 5: Migration Pattern - Clearing Tile Data
// =============================================================================

void clearTile(Point position) {
	// OLD WAY - must clear each array individually:
	// dPiece[position.x][position.y] = 0;
	// dPlayer[position.x][position.y] = 0;
	// dMonster[position.x][position.y] = 0;
	// dCorpse[position.x][position.y] = 0;
	// dObject[position.x][position.y] = 0;
	// dItem[position.x][position.y] = 0;
	// dSpecial[position.x][position.y] = 0;
	// dFlags[position.x][position.y] = DungeonFlag::None;
	// dTransVal[position.x][position.y] = 0;
	// dLight[position.x][position.y] = 0;
	// dPreLight[position.x][position.y] = 0;

	// NEW WAY - single call:
	currentLevel().tiles_[position.x][position.y].clear();
}

// =============================================================================
// EXAMPLE 6: Iterating Over Tile Area
// =============================================================================

void processTileArea(int startX, int startY, int width, int height) {
	// OLD WAY:
	// for (int y = startY; y < startY + height; y++) {
	//     for (int x = startX; x < startX + width; x++) {
	//         if (dMonster[x][y] != 0) {
	//             int monsterId = dMonster[x][y];
	//             // process monster
	//         }
	//     }
	// }

	// NEW WAY - better cache locality:
	for (int y = startY; y < startY + height; y++) {
		for (int x = startX; x < startX + width; x++) {
			const Tile& tile = currentLevel().tiles_[x][y];
			if (tile.hasMonster()) {
				int monsterId = tile.monster();
				// process monster
			}
		}
	}
}

// =============================================================================
// EXAMPLE 7: Corpse Handling (Encoded Data)
// =============================================================================

void handleCorpse(Point position) {
	// OLD WAY - manual bit manipulation:
	// int8_t corpseData = dCorpse[position.x][position.y];
	// if (corpseData != 0) {
	//     int8_t corpseIndex = corpseData & 0x1F;
	//     int8_t corpseDirection = corpseData >> 5;
	//     // use corpseIndex and corpseDirection
	// }

	// NEW WAY - helper methods:
	const Tile& tile = currentLevel().tiles_[position.x][position.y];
	if (tile.hasCorpse()) {
		int8_t corpseIndex = tile.corpseIndex();
		int8_t corpseDirection = tile.corpseDirection();
		// use corpseIndex and corpseDirection
	}
}

// =============================================================================
// EXAMPLE 8: Flag Operations
// =============================================================================

void manageTileFlags(Point position) {
	Tile& tile = currentLevel().tiles_[position.x][position.y];

	// OLD WAY:
	// dFlags[position.x][position.y] |= DungeonFlag::Visible;
	// dFlags[position.x][position.y] &= ~DungeonFlag::Missile;
	// bool hasFlag = HasAnyOf(dFlags[position.x][position.y], DungeonFlag::Explored);

	// NEW WAY:
	tile.addFlags(DungeonFlag::Visible);
	tile.removeFlags(DungeonFlag::Missile);
	bool hasFlag = tile.hasAnyFlag(DungeonFlag::Explored);

	// Check multiple flags:
	if (tile.hasAllFlags(DungeonFlag::Visible | DungeonFlag::Explored)) {
		// both flags are set
	}
}

// =============================================================================
// EXAMPLE 9: Performance-Critical Code (Rendering Loop)
// =============================================================================

void renderTiles(int viewX, int viewY, int width, int height) {
	// OLD WAY - each property access loads a different cache line:
	// for (int y = 0; y < height; y++) {
	//     for (int x = 0; x < width; x++) {
	//         int worldX = viewX + x;
	//         int worldY = viewY + y;
	//         
	//         uint16_t piece = dPiece[worldX][worldY];
	//         uint8_t light = dLight[worldX][worldY];
	//         DungeonFlag flags = dFlags[worldX][worldY];
	//         
	//         // render using piece, light, flags
	//     }
	// }

	// NEW WAY - all properties in same cache line:
	for (int y = 0; y < height; y++) {
		for (int x = 0; x < width; x++) {
			int worldX = viewX + x;
			int worldY = viewY + y;

			const Tile& tile = currentLevel().tiles_[worldX][worldY];
			uint16_t piece = tile.piece();
			uint8_t light = tile.light();
			DungeonFlag flags = tile.flags();

			// render using piece, light, flags
			// All data loaded in ONE cache line access!
		}
	}
}

// =============================================================================
// EXAMPLE 10: Pathfinding / AI (Common Pattern)
// =============================================================================

bool isWalkable(Point position) {
	// OLD WAY - multiple array accesses:
	// if (dPlayer[position.x][position.y] != 0) return false;
	// if (dMonster[position.x][position.y] != 0) return false;
	// if (dObject[position.x][position.y] > 0) return false;
	// // Plus need to check SOLData, dFlags, etc.

	// NEW WAY - single access with semantic method:
	const Tile& tile = currentLevel().tiles_[position.x][position.y];
	return tile.isPassable();
}

// =============================================================================
// EXAMPLE 11: Entity Placement
// =============================================================================

void placeMonster(Point position, int monsterId) {
	Tile& tile = currentLevel().tiles_[position.x][position.y];

	// Check if position is valid
	if (!tile.isPassable()) {
		// Cannot place monster here
		return;
	}

	// Place the monster
	tile.setMonster(monsterId);

	// Mark tile as populated
	tile.addFlags(DungeonFlag::Populated);
}

// =============================================================================
// EXAMPLE 12: Bulk Tile Operations
// =============================================================================

void clearRegion(int startX, int startY, int width, int height) {
	// NEW WAY - much cleaner and harder to forget a field:
	for (int y = startY; y < startY + height; y++) {
		for (int x = startX; x < startX + width; x++) {
			currentLevel().tiles_[x][y].clear();
		}
	}

	// Compare to OLD WAY which required 11+ separate loops or
	// a giant nested loop with 11 assignments per iteration
}

} // namespace examples
} // namespace devilution
