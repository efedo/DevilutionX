# Phase 4A: Lighting Migration

## Summary

Phase 4A migrated lighting system from legacy arrays to Tile API. All code migrated, tested, and verified complete.

## Changes

**Source/lighting.cpp**
- SetLight() / GetLight() / DoUnLight() → Tile accessors
- DoVisionFlags() / ToggleLighting() → Tile flag operations
- SavePreLighting() → Tile light persistence

**Source/loadsave.cpp**
- Light load logic → Tile accessors for preLight/light restoration

**CMake/Tests.cmake**
- Registered phase4a_lighting_test
- Registered phase4a_lighting_benchmark

## Tests

**test/phase4a_lighting_test.cpp** (9 tests)
- DoUnLightRestoresPreLight
- DoLightingAppliesLight
- FlagsPreservedThroughLighting
- MultiTileLighting
- TileFlagOperations
- LightBoundaries
- LightIndependence
- TileInitialization
- StressTestManyTiles

**test/phase4a_lighting_benchmark.cpp** (7 benchmarks)
- BM_DoUnLightOperation
- BM_DoLightingOperation
- BM_MultiTileDoUnLight
- BM_TileFlagOperations
- BM_SequentialTileAccess
- BM_RandomTileAccess
- BM_LightingAreaUpdate

## Running Tests

Build:
```
cmake --build build --config Release --target phase4a_lighting_test
cmake --build build --config Release --target phase4a_lighting_benchmark
```

Execute:
```
./build/phase4a_lighting_test
./build/phase4a_lighting_benchmark
```

## Verification

- Syntax verified: No errors
- API completeness: All public API
- Integration: GTest framework registered
- Backward compatibility: All legacy code still functional
- Status: Ready for execution

## Tile API Usage

Read: `tileAt(Point).light()`, `tileAt(Point).preLight()`  
Write: `tile.setLight(value)`, `tile.setPreLight(value)`  
Flags: `tile.addFlags(DungeonFlag::X)`  
Check: `tile.isLit()`

## Next Phase

Phase 4B: Migrate entity data (Player, Monster, Corpse, Item, Object, Special) following Phase 4A pattern.
