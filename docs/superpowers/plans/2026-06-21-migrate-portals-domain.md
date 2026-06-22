# Migrate Portals Domain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish the hybrid source-organization pattern by moving the complete portals gameplay domain under `Source/game/portals` while preserving legacy include paths and runtime behavior.

**Architecture:** `Source/game/portals` becomes the canonical owner of portal state, operations, and validation. The legacy `Source/portal.h` and `Source/portals/validation.hpp` paths remain forwarding compatibility headers, while in-tree consumers and CMake use canonical paths. A focused GTest characterizes the state operations before the mechanical move.

**Tech Stack:** C++20, GTest/GMock, CMake, MSVC/Ninja, PowerShell, Git

---

## Scope

This is the first executable slice of the approved source-organization design.
It proves the domain-directory and compatibility-header conventions without
combining the move with API redesign.

This plan does not:

- Rename `Portal`, `Portals`, `MAXPORTAL`, or any portal function.
- Change portal state layout, initialization order, level transitions, missiles,
  lighting, network commands, save data, or validation behavior.
- Create a dedicated CMake object library for portals.
- Move network serialization from `msg.cpp` or persistence code from
  `loadsave.cpp`.
- Remove either legacy include path.

## Final File Map

```text
Source/
  game/
    portals/
      portal.cpp
      portal.hpp
      validation.cpp
      validation.hpp
  portal.h                         # Compatibility include
  portals/
    validation.hpp                # Compatibility include
  msg.h                            # Uses canonical portal header
  msg.cpp                          # Uses canonical validation header
  CMakeLists.txt                   # Lists canonical implementation paths
test/
  portal_test.cpp                  # Portal state characterization tests
CMake/
  Tests.cmake                      # Registers portal_test
docs/
  devilution/
    CHANGELOG.md                   # Records organization migration
```

### Task 1: Add a Canonical-Header Contract Test

**Files:**
- Create: `test/portal_test.cpp`
- Modify: `CMake/Tests.cmake`

- [ ] **Step 1: Create the focused test using the future canonical header**

Create `test/portal_test.cpp` with:

```cpp
#include "game/portals/portal.hpp"
#include "game/portals/validation.hpp"

#include <gtest/gtest.h>

#include "portal.h"
#include "portals/validation.hpp"

namespace devilution {
namespace {

class PortalTest : public testing::Test {
protected:
	void SetUp() override
	{
		InitPortals();
	}
};

TEST_F(PortalTest, InitClosesEveryPortal)
{
	for (Portal &portal : Portals)
		portal.open = true;

	InitPortals();

	for (const Portal &portal : Portals)
		EXPECT_FALSE(portal.open);
}

TEST_F(PortalTest, SetPortalStatsUpdatesSelectedPortal)
{
	SetPortalStats(2, true, { 20, 30 }, 5, DTYPE_CATACOMBS, false);

	const Portal &portal = Portals[2];
	EXPECT_TRUE(portal.open);
	EXPECT_EQ(portal.position, Point(20, 30));
	EXPECT_EQ(portal.level, 5);
	EXPECT_EQ(portal.ltype, DTYPE_CATACOMBS);
	EXPECT_FALSE(portal.setlvl);
}

TEST_F(PortalTest, PositionCheckAcceptsPortalAndArrivalTile)
{
	SetPortalStats(0, true, { 20, 30 }, 5, DTYPE_CATACOMBS, false);

	EXPECT_TRUE(PosOkPortal(5, { 20, 30 }));
	EXPECT_TRUE(PosOkPortal(5, { 21, 31 }));
	EXPECT_FALSE(PosOkPortal(4, { 20, 30 }));
	EXPECT_FALSE(PosOkPortal(5, { 21, 30 }));
}

} // namespace
} // namespace devilution
```

- [ ] **Step 2: Register the test**

Add `portal_test` to the `tests` list in `CMake/Tests.cmake`, immediately after
`player_test`:

```cmake
  player_test
  portal_test
  quests_test
```

- [ ] **Step 3: Build the test and verify the canonical-header contract fails**

Run:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target portal_test'
```

Expected: compilation fails because `game/portals/portal.hpp` does not exist.
If CMake reports that `portal_test` is unknown, configure first:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S . -B build\x64-Debug -G Ninja -DCMAKE_BUILD_TYPE=Debug -DDISCORD_INTEGRATION=True -DBUILD_TESTING=ON'
```

Then repeat the build and expect the missing-header failure.

### Task 2: Introduce Canonical and Compatibility Headers

**Files:**
- Create: `Source/game/portals/portal.hpp`
- Create: `Source/game/portals/validation.hpp`
- Modify: `Source/portal.h`
- Modify: `Source/portals/validation.hpp`

- [ ] **Step 1: Add the canonical portal interface**

Create `Source/game/portals/portal.hpp` by moving the declarations from
`Source/portal.h` without renaming or reordering them:

```cpp
/**
 * @file game/portals/portal.hpp
 *
 * Interface of functionality for handling town portals.
 */
#pragma once

#include <cstddef>

#include "engine/point.hpp"
#include "levels/gendung.h"

namespace devilution {

// Defined in player.h, forward declared here to allow for functions which operate in the context of a player.
struct Player;

#define MAXPORTAL 4

struct Portal {
	bool open;
	Point position;
	int level;
	dungeon_type ltype;
	bool setlvl;
};

extern Portal Portals[MAXPORTAL];

void InitPortals();
void SetPortalStats(int i, bool o, Point position, int lvl, dungeon_type lvltype, bool isSetLevel);
void AddPortalMissile(const Player &player, Point position, bool sync);
void SyncPortals();
void AddPortalInTown(const Player &player);
void ActivatePortal(const Player &player, Point position, int lvl, dungeon_type lvltype, bool sp);
void DeactivatePortal(const Player &player);
bool PortalOnLevel(const Player &player);
void RemovePortalMissile(const Player &player);
void SetCurrentPortal(size_t p);
void GetPortalLevel();
void GetPortalLvlPos();
bool PosOkPortal(int lvl, Point position);

} // namespace devilution
```

- [ ] **Step 2: Convert the legacy portal header into a forwarding header**

Replace `Source/portal.h` with:

```cpp
/**
 * @file portal.h
 *
 * Compatibility include for the canonical portal gameplay interface.
 */
#pragma once

#include "game/portals/portal.hpp"
```

- [ ] **Step 3: Add the canonical validation interface**

Create `Source/game/portals/validation.hpp` with:

```cpp
/**
 * @file game/portals/validation.hpp
 *
 * Interface of functions for validation of portal data.
 */
#pragma once

#include <cstdint>

#include "engine/world_tile.hpp"

namespace devilution {

bool IsPortalDeltaValid(WorldTilePosition location, uint8_t level, uint8_t levelType, bool isOnSetLevel);

} // namespace devilution
```

- [ ] **Step 4: Convert the legacy validation header into a forwarding header**

Replace `Source/portals/validation.hpp` with:

```cpp
/**
 * @file portals/validation.hpp
 *
 * Compatibility include for the canonical portal validation interface.
 */
#pragma once

#include "game/portals/validation.hpp"
```

- [ ] **Step 5: Build and run the portal test**

Run:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target portal_test && build\x64-Debug\portal_test.exe'
```

Expected: build succeeds and all three `PortalTest` tests pass while the
implementation still resides at its old path.

- [ ] **Step 6: Commit the interface boundary**

```bat
git add test/portal_test.cpp CMake/Tests.cmake Source/game/portals/portal.hpp Source/game/portals/validation.hpp Source/portal.h Source/portals/validation.hpp
git commit -m "Add canonical portal domain interfaces"
```

### Task 3: Move Portal Implementations to the Game Domain

**Files:**
- Move: `Source/portal.cpp` to `Source/game/portals/portal.cpp`
- Move: `Source/portals/validation.cpp` to `Source/game/portals/validation.cpp`
- Modify: `Source/game/portals/portal.cpp`
- Modify: `Source/game/portals/validation.cpp`
- Modify: `Source/CMakeLists.txt`
- Modify: `Source/msg.h`
- Modify: `Source/msg.cpp`

- [ ] **Step 1: Move the implementation files without changing their bodies**

Run:

```bat
git mv Source\portal.cpp Source\game\portals\portal.cpp
git mv Source\portals\validation.cpp Source\game\portals\validation.cpp
```

- [ ] **Step 2: Point each implementation at its canonical header**

In `Source/game/portals/portal.cpp`, replace:

```cpp
#include "portal.h"
```

with:

```cpp
#include "game/portals/portal.hpp"
```

In `Source/game/portals/validation.cpp`, replace:

```cpp
#include "portals/validation.hpp"
```

with:

```cpp
#include "game/portals/validation.hpp"
```

Also update each file's `@file` line:

```cpp
 * @file game/portals/portal.cpp
```

and:

```cpp
 * @file game/portals/validation.cpp
```

Do not change any function body, declaration, global, constant, include other
than the canonical self-include, or comment unrelated to the file path.

- [ ] **Step 3: Update CMake source registration**

In `Source/CMakeLists.txt`, replace:

```cmake
  portal.cpp
```

with:

```cmake
  game/portals/portal.cpp
```

Replace:

```cmake
  portals/validation.cpp
```

with:

```cmake
  game/portals/validation.cpp
```

Keep both entries in their existing list positions so this commit changes only
paths, not target composition or source ordering.

- [ ] **Step 4: Migrate in-tree consumers to canonical headers**

In `Source/msg.h`, replace:

```cpp
#include "portal.h"
```

with:

```cpp
#include "game/portals/portal.hpp"
```

In `Source/msg.cpp`, replace:

```cpp
#include "portals/validation.hpp"
```

with:

```cpp
#include "game/portals/validation.hpp"
```

- [ ] **Step 5: Prove canonical ownership and compatibility paths**

Run:

```powershell
rg -n '"portal.h"|"portals/validation.hpp"|portal\.cpp|portals/validation\.cpp' Source test
```

Expected:

- No implementation or production consumer includes a legacy header.
- `test/portal_test.cpp` includes both legacy headers to keep their forwarding
  contracts under compilation.
- `Source/portal.h` and `Source/portals/validation.hpp` remain present only as
  compatibility headers.
- `Source/CMakeLists.txt` contains only
  `game/portals/portal.cpp` and `game/portals/validation.cpp`.

- [ ] **Step 6: Build and run focused verification**

Run:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target portal_test devilutionx && build\x64-Debug\portal_test.exe'
```

Expected: `portal_test` and `devilutionx` build successfully and all portal
tests pass.

- [ ] **Step 7: Commit the mechanical move**

```bat
git add Source/game/portals/portal.cpp Source/game/portals/validation.cpp Source/portal.cpp Source/portals/validation.cpp Source/CMakeLists.txt Source/msg.h Source/msg.cpp
git commit -m "Move portals into game domain"
```

`git add` accepts the old paths to record deletions from the moves.

### Task 4: Document and Verify the Pilot Migration

**Files:**
- Modify: `docs/devilution/CHANGELOG.md`
- Verify: all files changed by Tasks 1-3

- [ ] **Step 1: Add the changelog entry**

Under `Unreleased` -> `Features` -> `Stability / Performance / System`, add:

```markdown
- Move portal gameplay code into the domain-oriented source layout while
  preserving legacy include paths.
```

- [ ] **Step 2: Verify repository references**

Run:

```powershell
rg -n 'Source/portal\.cpp|Source\\portal\.cpp|Source/portals/validation\.cpp|Source\\portals\\validation\.cpp' . -g '!build/**' -g '!.git/**'
```

Expected: no stale references to the old implementation paths.

Run:

```powershell
rg -n '#include "portal\.h"|#include "portals/validation\.hpp"' Source test
```

Expected: the only matches are the two deliberate compatibility includes in
`test/portal_test.cpp`; no production source includes a legacy path.

- [ ] **Step 3: Build all registered GTest executables and the game**

Generate the target list from `CMake/Tests.cmake` by using the test and
standalone-test names exactly as registered, then run:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target animationinfo_test appfat_test automap_test cursor_test debug_console_history_test debug_overlay_state_test dead_test diablo_test drlg_common_test drlg_l1_test drlg_l2_test drlg_l3_test drlg_l4_test effects_test inv_test items_test math_test missiles_test multi_logging_test objects_test pack_test level_micros_test phase4a_lighting_test phase4b_entity_test player_test portal_test quests_test scrollrt_test stores_test tile_properties_test timedemo_test tile_migration_sync_test townerdat_test writehero_test vendor_test panel_state_test store_transaction_test visual_store_test stash_test inventory_ui_test spell_ui_test char_panel_test game_menu_test codec_test crawl_test data_file_test file_util_test format_int_test ini_test palette_blending_test parse_int_test path_test vision_test random_test rectangle_test static_vector_test str_cat_test utf8_test text_render_integration_test devilutionx'
```

Expected: every listed target builds successfully. If the configured build uses
SDL1 and therefore does not register `text_render_integration_test`, omit only
that target.

- [ ] **Step 4: Run the complete registered test suite**

Run:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\ctest.exe" --test-dir build\x64-Debug --output-on-failure'
```

Expected: all discovered tests pass. Asset-dependent skips are acceptable only
when they match the existing test-suite behavior and no test fails.

- [ ] **Step 5: Verify whitespace and CRLF**

Run:

```bat
git -c core.whitespace=cr-at-eol diff --check
```

Expected: no output.

Run:

```powershell
$files = @(
  'CMake/Tests.cmake',
  'Source/CMakeLists.txt',
  'Source/game/portals/portal.cpp',
  'Source/game/portals/portal.hpp',
  'Source/game/portals/validation.cpp',
  'Source/game/portals/validation.hpp',
  'Source/msg.cpp',
  'Source/msg.h',
  'Source/portal.h',
  'Source/portals/validation.hpp',
  'docs/devilution/CHANGELOG.md',
  'test/portal_test.cpp'
)
foreach ($file in $files) {
  $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $file))
  for ($i = 0; $i -lt $bytes.Length; $i++) {
    if ($bytes[$i] -eq 10 -and ($i -eq 0 -or $bytes[$i - 1] -ne 13)) {
      throw "$file contains a non-CRLF line ending"
    }
  }
}
```

Expected: no output and exit code zero.

- [ ] **Step 6: Commit documentation and final verification state**

```bat
git add docs/devilution/CHANGELOG.md
git commit -m "Document portal source migration"
```

- [ ] **Step 7: Confirm the worktree is clean**

Run:

```bat
git status --short
```

Expected: no output.

## Follow-Up Plans

After this pilot is merged, write separate plans for:

1. Moving the quest domain and its validation module.
2. Moving small gameplay domains such as towners and stores.
3. Moving level ownership under `Source/game/levels`.
4. Decomposing `items.cpp`, `monster.cpp`, `objects.cpp`, `player.cpp`, and
   `missiles.cpp`, one responsibility and one plan at a time.
5. Separating network, persistence, UI, input, and scripting infrastructure
   after domain interfaces have stable canonical homes.

Each follow-up should reuse the compatibility-header, canonical-include, CMake,
focused-test, and verification pattern proven by this plan.
