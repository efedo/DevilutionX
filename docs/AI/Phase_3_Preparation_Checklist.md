# Phase 3 Preparation Checklist

## Overview

Phase 2 is complete. This checklist guides you through preparing for and executing Phase 3: migrating call sites to the Tile API.

## Before Starting Phase 3

### 1. Review Phase 2 Changes
- [ ] Read `docs/Phase_2_Completion_Summary.md`
- [ ] Read `docs/Phase_2_Migration_Guide.md`
- [ ] Review `Source/levels/tile.hpp` API
- [ ] Review `Source/levels/tile_migration_example.cpp` examples

### 2. Establish Baselines
- [ ] Run full test suite → document pass/fail count
- [ ] Benchmark rendering performance → document frame times
- [ ] Profile cache behavior (if tools available) → document miss rates
- [ ] Test save/load functionality → verify files loadable
- [ ] Test multiplayer → verify sync working

### 3. Set Up Tracking
- [ ] Create task list for modules to migrate
- [ ] Assign priority (High/Medium/Low) to each module
- [ ] Estimate time for each migration
- [ ] Schedule milestones

## Migration Order (Recommended)

### Week 1-2: Hot Path #1 - Rendering
- [ ] **Module:** `Source/engine/render/scrollrt.cpp`
- [ ] **Why:** Highest cache benefit, most impactful
- [ ] **Tasks:**
  - [ ] Audit all `dPiece`, `dLight`, `dFlags`, `dSpecial` access
  - [ ] Convert tile access loops to use `Tile&`
  - [ ] Use `tile.piece()`, `tile.light()`, etc.
  - [ ] Remove redundant bounds checks (use `tileAt()`)
- [ ] **Testing:**
  - [ ] Run rendering benchmarks → expect 5-15% improvement
  - [ ] Visual inspection → no artifacts
  - [ ] Profile cache → verify miss reduction
- [ ] **Sign-off:** _______________

### Week 2-3: Hot Path #2 - Pathfinding
- [ ] **Module:** `Source/path.cpp` + AI functions
- [ ] **Why:** Heavy tile queries, benefits from semantic methods
- [ ] **Tasks:**
  - [ ] Convert `IsWalkable()` checks to `tile.isPassable()`
  - [ ] Convert entity checks to `tile.hasMonster()`, etc.
  - [ ] Simplify multi-property checks
- [ ] **Testing:**
  - [ ] Run pathfinding benchmarks → expect 10-20% improvement
  - [ ] Test monster AI → verify behavior unchanged
  - [ ] Test player movement → verify no pathfinding issues
- [ ] **Sign-off:** _______________

### Week 3-4: Hot Path #3 - Lighting
- [ ] **Module:** `Source/lighting.cpp`
- [ ] **Why:** Accesses multiple tile properties together
- [ ] **Tasks:**
  - [ ] Convert lighting update loops to use `Tile&`
  - [ ] Use `tile.setLight()`, `tile.addFlags(DungeonFlag::Lit)`, etc.
  - [ ] Convert light propagation to Tile API
- [ ] **Testing:**
  - [ ] Test lighting effects → verify visual correctness
  - [ ] Test light sources → verify propagation correct
  - [ ] Benchmark lighting updates → document any improvements
- [ ] **Sign-off:** _______________

### Week 4-5: Entity Management
- [ ] **Module:** Monster placement functions
  - [ ] `PlaceMonster()` variants
  - [ ] `CanPlaceMonster()` checks
  - [ ] Monster movement code
- [ ] **Module:** Object placement functions
  - [ ] `AddObject()` variants
  - [ ] Object collision checks
- [ ] **Module:** Item placement functions
  - [ ] `AddItemToInventory()` variants
  - [ ] Ground item handling
- [ ] **Testing:**
  - [ ] Test entity spawning → verify placement correct
  - [ ] Test collision detection → verify entities don't overlap
  - [ ] Test item drops → verify items appear correctly
- [ ] **Sign-off:** _______________

### Week 5-6: Level Generation
- [ ] **Module:** `Source/levels/gendung.cpp`
- [ ] **Module:** Dungeon generators (`dun/` directory)
- [ ] **Tasks:**
  - [ ] Convert bulk tile clear to `tile.clear()`
  - [ ] Convert tile initialization to Tile API
  - [ ] Simplify region operations
- [ ] **Testing:**
  - [ ] Generate multiple levels → verify correctness
  - [ ] Test different level types → verify variety
  - [ ] Visual inspection → verify no generation artifacts
- [ ] **Sign-off:** _______________

### Week 7: Remaining Code
- [ ] **Module:** `Source/loadsave.cpp`
  - [ ] Review serialization code
  - [ ] Keep using legacy arrays for now (binary format compatibility)
  - [ ] Add comments about future Tile-based serialization
- [ ] **Module:** `Source/debug.cpp`
  - [ ] Update debug overlays to optionally show Tile data
  - [ ] Keep legacy array display for comparison
- [ ] **Module:** UI code
  - [ ] Update cursor position checks to use Tile API
  - [ ] Update hover info to query Tile
- [ ] **Testing:**
  - [ ] Test save/load → verify backward compatibility
  - [ ] Test debug overlays → verify data display correct
- [ ] **Sign-off:** _______________

## After Each Module Migration

### Immediate Checks
- [ ] Code compiles without errors
- [ ] Code compiles without warnings (new or increased)
- [ ] Unit tests pass for migrated module
- [ ] Integration tests pass

### Regression Testing
- [ ] Run full game test suite
- [ ] Manual play testing of affected systems
- [ ] Check for visual artifacts
- [ ] Verify multiplayer still syncs

### Performance Validation
- [ ] Run relevant benchmarks
- [ ] Compare to baseline measurements
- [ ] Document improvements (or lack thereof)
- [ ] Profile if results unexpected

### Code Review
- [ ] Self-review changed code
- [ ] Check for mixed old/new API usage
- [ ] Verify no sync utilities in hot paths
- [ ] Confirm consistent style

## Phase 3 Completion Criteria

Before declaring Phase 3 complete, verify:

### Code Migration
- [ ] All production code using Tile API
- [ ] No uses of `dPiece[x][y]` except in:
  - [ ] Legacy macro definitions (gendung.h)
  - [ ] Serialization code (loadsave.cpp)
  - [ ] Test/benchmark setup code
- [ ] No uses of sync utilities in production code
- [ ] All module sign-offs completed

### Testing
- [ ] All tests passing (≥ baseline)
- [ ] Performance benchmarks meet expectations:
  - [ ] Rendering: 5-15% improvement
  - [ ] Pathfinding: 10-20% improvement
  - [ ] Overall frame time: measurable improvement
- [ ] Cache profiling shows expected improvements (if measured)
- [ ] Save/load backward compatible

### Documentation
- [ ] Migration notes added to each migrated file
- [ ] Performance improvements documented
- [ ] Any deviations from plan documented
- [ ] Phase 3 completion report written

### Quality
- [ ] No new bugs introduced
- [ ] No visual artifacts
- [ ] No multiplayer desyncs
- [ ] No performance regressions

## Phase 4 Readiness

Once Phase 3 is complete, you're ready for Phase 4 if:
- [ ] All above criteria met
- [ ] Team agrees migration is successful
- [ ] Stakeholders approve removing legacy code
- [ ] No known issues with Tile implementation

## Common Issues and Solutions

### Issue: Mixed API Usage
**Symptom:** Code uses both `tiles[x][y]` and `dPiece[x][y]`  
**Solution:** Complete the migration for that entire function  
**Prevention:** Migrate entire functions, not individual lines

### Issue: Macro Shadowing
**Symptom:** Compiler error about `tiles` type mismatch  
**Solution:** Rename local variable  
**Prevention:** Avoid local variables named `tiles` or `tileAt`

### Issue: Performance Doesn't Improve
**Symptom:** Benchmarks show no improvement after migration  
**Solution:** 
1. Verify entire hot path is migrated (not just parts)
2. Check that compiler is actually optimizing (`-O2` or higher)
3. Profile to confirm cache behavior changed
4. May need to migrate surrounding code for benefit

### Issue: Hard to Find All Uses
**Symptom:** Keep finding old API usage after "complete" migration  
**Solution:**
1. Use grep/search for `dPiece[`, `dLight[`, etc.
2. Exclude files: `gendung.h`, `loadsave.cpp`, tests
3. Review search results before claiming done

## Tools and Scripts

### Search for Legacy API Usage
```powershell
# PowerShell command to find legacy array access
Select-String -Path "Source\*.cpp","Source\*.h" -Pattern "dPiece\[|dLight\[|dFlags\[|dPlayer\[|dMonster\[|dCorpse\[|dObject\[|dSpecial\[|dTransVal\[|dPreLight\[" -Exclude "gendung.h","loadsave.cpp"
```

### Count Legacy References
```powershell
# Count how many times legacy arrays are used
(Select-String -Path "Source\*.cpp" -Pattern "dPiece\[|dLight\[|dFlags\[|dPlayer\[|dMonster\[|dCorpse\[|dObject\[|dSpecial\[|dItem\[|dTransVal\[|dPreLight\[").Count
```

### Find Specific Pattern
```powershell
# Find specific module's tile access
Select-String -Path "Source\path.cpp" -Pattern "dPiece\[|dLight\[|dFlags\[|dPlayer\[|dMonster\[|dCorpse\[|dObject\[|dSpecial\[|dItem\[|dTransVal\[|dPreLight\[" -Context 2,2
```

## Resources

- **Design:** `docs/Tile_Class_Design.md`
- **API Reference:** `docs/Tile_Quick_Reference.md`
- **Migration Guide:** `docs/Phase_2_Migration_Guide.md`
- **Examples:** `Source/levels/tile_migration_example.cpp`
- **Memory Analysis:** `docs/Tile_Memory_Layout.md`

## Questions?

If you encounter issues during Phase 3:
1. Check `docs/Phase_2_Migration_Guide.md` "Common Pitfalls"
2. Review `Source/levels/tile_migration_example.cpp` for patterns
3. Refer to `docs/Tile_Quick_Reference.md` for API usage
4. Profile and measure when performance doesn't match expectations

---

**Ready to begin Phase 3?** Start with Week 1: Rendering Hot Path.

**Good luck! 🚀**
