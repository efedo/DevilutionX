/**
 * @file levels/tile_migration_example.cpp
 *
 * Concrete example showing Phase 2 migration in a real function.
 * This demonstrates both old and new approaches side-by-side.
 */

#include "levels/gendung.h"
#include "levels/level.hpp"
#include "levels/tile.hpp"
#include "engine/point.hpp"

namespace devilution {

// =============================================================================
// EXAMPLE: Monster Placement Function
// =============================================================================

// -----------------------------------------------------------------------------
// OLD VERSION (Phase 1 - uses separate arrays)
// -----------------------------------------------------------------------------
bool CanPlaceMonster_Old(Point position) {
	// Check if position is valid
	if (position.x < 0 || position.x >= MAXDUNX || position.y < 0 || position.y >= MAXDUNY) {
		return false;
	}

	// Check if tile is already occupied
	if (dPlayer[position.x][position.y] != 0) return false;
	if (dMonster[position.x][position.y] != 0) return false;
	if (dObject[position.x][position.y] > 0) return false;  // Positive = solid object

	// Check flags
	if (HasAnyOf(dFlags[position.x][position.y], DungeonFlag::Populated)) return false;

	return true;
}

void PlaceMonster_Old(Point position, int monsterId) {
	if (!CanPlaceMonster_Old(position)) {
		return;  // Can't place here
	}

	// Place the monster
	dMonster[position.x][position.y] = monsterId;
	dFlags[position.x][position.y] |= DungeonFlag::Populated;
}

// -----------------------------------------------------------------------------
// NEW VERSION (Phase 2 - uses Tile API)
// -----------------------------------------------------------------------------
bool CanPlaceMonster_New(Point position) {
	// Check if position is valid
	if (position.x < 0 || position.x >= MAXDUNX || position.y < 0 || position.y >= MAXDUNY) {
		return false;
	}

	// Get the tile and check in one place
	const Tile& tile = tileAt(position);

	// Use semantic methods instead of raw checks
	return tile.isPassable() && !tile.isPopulated();
}

void PlaceMonster_New(Point position, int monsterId) {
	if (!CanPlaceMonster_New(position)) {
		return;  // Can't place here
	}

	// Get tile and set properties
	Tile& tile = tileAt(position);
	tile.setMonster(monsterId);
	tile.addFlags(DungeonFlag::Populated);
}

// =============================================================================
// EXAMPLE: Lighting Update Function
// =============================================================================

// -----------------------------------------------------------------------------
// OLD VERSION
// -----------------------------------------------------------------------------
void UpdateLighting_Old(Point position, uint8_t newLight) {
	// Read current state
	uint8_t preLight = dPreLight[position.x][position.y];
	DungeonFlag flags = dFlags[position.x][position.y];

	// Update light
	dLight[position.x][position.y] = newLight;

	// Mark as lit if bright enough
	if (newLight > 5) {
		dFlags[position.x][position.y] |= DungeonFlag::Lit;
	} else {
		dFlags[position.x][position.y] &= ~DungeonFlag::Lit;
	}
}

void UpdateLightingArea_Old(Point center, int radius, uint8_t brightness) {
	for (int dy = -radius; dy <= radius; dy++) {
		for (int dx = -radius; dx <= radius; dx++) {
			Point pos = center + Displacement { dx, dy };
			if (pos.x >= 0 && pos.x < MAXDUNX && pos.y >= 0 && pos.y < MAXDUNY) {
				UpdateLighting_Old(pos, brightness);
			}
		}
	}
}

// -----------------------------------------------------------------------------
// NEW VERSION
// -----------------------------------------------------------------------------
void UpdateLighting_New(Point position, uint8_t newLight) {
	Tile& tile = tileAt(position);

	// Update light
	tile.setLight(newLight);

	// Mark as lit if bright enough
	if (newLight > 5) {
		tile.addFlags(DungeonFlag::Lit);
	} else {
		tile.removeFlags(DungeonFlag::Lit);
	}
}

void UpdateLightingArea_New(Point center, int radius, uint8_t brightness) {
	for (int dy = -radius; dy <= radius; dy++) {
		for (int dx = -radius; dx <= radius; dx++) {
			Point pos = center + Displacement { dx, dy };
			if (pos.x >= 0 && pos.x < MAXDUNX && pos.y >= 0 && pos.y < MAXDUNY) {
				UpdateLighting_New(pos, brightness);
			}
		}
	}
}

// =============================================================================
// EXAMPLE: Pathfinding Query
// =============================================================================

// -----------------------------------------------------------------------------
// OLD VERSION
// -----------------------------------------------------------------------------
bool IsWalkable_Old(Point position) {
	// Bounds check
	if (position.x < 0 || position.x >= MAXDUNX || position.y < 0 || position.y >= MAXDUNY) {
		return false;
	}

	// Check entity occupation
	if (dPlayer[position.x][position.y] != 0) return false;
	if (dMonster[position.x][position.y] != 0) return false;
	if (dObject[position.x][position.y] > 0) return false;

	// Would also need to check SOLData for tile solidity (not shown)

	return true;
}

int CountWalkableTiles_Old(Point start, int width, int height) {
	int count = 0;
	for (int y = 0; y < height; y++) {
		for (int x = 0; x < width; x++) {
			Point pos = start + Displacement { x, y };
			if (IsWalkable_Old(pos)) {
				count++;
			}
		}
	}
	return count;
}

// -----------------------------------------------------------------------------
// NEW VERSION
// -----------------------------------------------------------------------------
bool IsWalkable_New(Point position) {
	// Bounds check
	if (position.x < 0 || position.x >= MAXDUNX || position.y < 0 || position.y >= MAXDUNY) {
		return false;
	}

	const Tile& tile = tileAt(position);

	// One method call instead of 3+ checks
	return tile.isPassable();

	// Would still need SOLData check, but that's separate concern
}

int CountWalkableTiles_New(Point start, int width, int height) {
	int count = 0;
	for (int y = 0; y < height; y++) {
		for (int x = 0; x < width; x++) {
			Point pos = start + Displacement { x, y };
			if (IsWalkable_New(pos)) {
				count++;
			}
		}
	}
	return count;
}

// =============================================================================
// EXAMPLE: Clear Region (Bulk Operation)
// =============================================================================

// -----------------------------------------------------------------------------
// OLD VERSION
// -----------------------------------------------------------------------------
void ClearRegion_Old(Point start, int width, int height) {
	for (int y = 0; y < height; y++) {
		for (int x = 0; x < width; x++) {
			Point pos = start + Displacement { x, y };
			if (pos.x >= 0 && pos.x < MAXDUNX && pos.y >= 0 && pos.y < MAXDUNY) {
				// Clear all tile data (11 assignments!)
				dPiece[pos.x][pos.y] = 0;
				dTransVal[pos.x][pos.y] = 0;
				dLight[pos.x][pos.y] = 0;
				dPreLight[pos.x][pos.y] = 0;
				dFlags[pos.x][pos.y] = DungeonFlag::None;
				dPlayer[pos.x][pos.y] = 0;
				dMonster[pos.x][pos.y] = 0;
				dCorpse[pos.x][pos.y] = 0;
				dObject[pos.x][pos.y] = 0;
				dSpecial[pos.x][pos.y] = 0;
				dItem[pos.x][pos.y] = 0;
			}
		}
	}
}

// -----------------------------------------------------------------------------
// NEW VERSION
// -----------------------------------------------------------------------------
void ClearRegion_New(Point start, int width, int height) {
	for (int y = 0; y < height; y++) {
		for (int x = 0; x < width; x++) {
			Point pos = start + Displacement { x, y };
			if (pos.x >= 0 && pos.x < MAXDUNX && pos.y >= 0 && pos.y < MAXDUNY) {
				// One call clears everything
				tileAt(pos).clear();
			}
		}
	}
}

// =============================================================================
// COMPARISON: Line-of-Code Reduction
// =============================================================================

// OLD: 11 lines to set up a tile
void SetupTile_Old(Point pos, uint16_t piece, uint8_t light) {
	dPiece[pos.x][pos.y] = piece;
	dLight[pos.x][pos.y] = light;
	dPreLight[pos.x][pos.y] = light;
	dFlags[pos.x][pos.y] = DungeonFlag::Visible | DungeonFlag::Explored | DungeonFlag::Lit;
	dPlayer[pos.x][pos.y] = 0;
	dMonster[pos.x][pos.y] = 0;
	dCorpse[pos.x][pos.y] = 0;
	dObject[pos.x][pos.y] = 0;
	dSpecial[pos.x][pos.y] = 0;
	dItem[pos.x][pos.y] = 0;
	dTransVal[pos.x][pos.y] = 0;
}

// NEW: 5 lines to set up a tile
void SetupTile_New(Point pos, uint16_t piece, uint8_t light) {
	Tile& tile = tileAt(pos);
	tile.clear();  // Start fresh
	tile.setPiece(piece);
	tile.setLight(light);
	tile.setPreLight(light);
	tile.setFlags(DungeonFlag::Visible | DungeonFlag::Explored | DungeonFlag::Lit);
}

// =============================================================================
// CACHE BENEFIT EXAMPLE: Rendering Hot Path
// =============================================================================

// OLD: Multiple cache lines loaded per tile
void RenderTile_Old(Point pos) {
	uint16_t piece = dPiece[pos.x][pos.y];        // Cache line 1
	uint8_t light = dLight[pos.x][pos.y];         // Cache line 2
	DungeonFlag flags = dFlags[pos.x][pos.y];     // Cache line 3
	int8_t special = dSpecial[pos.x][pos.y];      // Cache line 4

	// Render using piece, light, flags, special
	// (rendering code not shown)
}

// NEW: Single cache line for all tile data
void RenderTile_New(Point pos) {
	const Tile& tile = tileAt(pos);      // Cache line 1 (contains ALL data)
	uint16_t piece = tile.piece();       // Already loaded
	uint8_t light = tile.light();        // Already loaded
	DungeonFlag flags = tile.flags();    // Already loaded
	int8_t special = tile.special();     // Already loaded

	// Render using piece, light, flags, special
	// (rendering code not shown)
}

// For a 60x60 viewport (3600 tiles):
// OLD: 3600 tiles × 4 properties = 14,400 potential cache misses
// NEW: 3600 tiles × 1 load = 3,600 potential cache misses
// IMPROVEMENT: ~75% reduction in cache traffic

} // namespace devilution
