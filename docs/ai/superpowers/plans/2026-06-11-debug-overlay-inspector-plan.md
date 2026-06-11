# Debug Overlay Inspector Expansion Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Keep each step small, compile-preserving, and validated with build/tests.

**Goal:** Expand the debug overlay from a console-only UI into a multi-feature tool with a dedicated inspector for tiles, objects, monsters, items, and later tile-editing actions such as swapping tiles or changing tile properties.

**Architecture:** Keep the Lua console as one panel in the overlay, but add a small overlay navigation layer and a separate read-only inspector panel that follows the hovered tile/entity. Reuse existing debug helpers (`debug.h`, `debug.cpp`, `lua/modules/dev/display.cpp`) so the new GUI reflects the same data already exposed through debug rendering and console commands.

**Tech Stack:** C++20, CMake/Ninja, MSVC, Dear ImGui overlay in `_DEBUG` builds, existing DevilutionX debug helpers and tests.

---

## File Structure

- Modify: `Source/debug_overlay/imgui_overlay.cpp`
  - Add overlay navigation/state for Console / Inspector / future Editor views.
  - Add buttons/toggles for inspection mode and entity selection.
  - Render the inspector panel and wire hover-based selection.
- Modify: `Source/debug_overlay/imgui_overlay.hpp`
  - Add any new overlay-facing helpers if needed.
- Modify: `Source/debug.h`
  - Add any new inspector-facing enums or helper declarations if they belong to the shared debug API.
- Modify: `Source/debug.cpp`
  - Add helper functions for resolving tile, object, monster, and item details from a hover position.
  - Reuse existing debug state, map query helpers, and message formatting where possible.
- Modify: `Source/engine/render/scrollrt.cpp` and/or `Source/engine/render/dun_render.cpp`
  - Only if needed to expose a clean hover/selection hook for the overlay.
- Modify: `Source/lua/modules/dev/display.cpp`
  - Optionally expose new debug display toggles if the inspector should sync with existing grid/path/vision modes.
- Test: `test/*.cpp` as applicable
  - Add regression coverage for inspector data resolution and command/help behavior if new helpers are introduced.
- Docs: `docs/debug.md`
  - Document the new overlay sections and how to use the inspector.

---

## Task 1: Define Overlay Navigation and State

**Files:**
- Modify: `Source/debug_overlay/imgui_overlay.cpp`
- Modify: `Source/debug_overlay/imgui_overlay.hpp` if necessary

- [ ] **Step 1: Add a small overlay mode model**

  Introduce a minimal enum for overlay pages, for example:

  - Console
  - Inspector
  - Future Editor

  Keep the state local to the overlay implementation unless it must be shared.

- [ ] **Step 2: Add a top-level UI switcher**

  Render a tab bar or row of buttons at the top of the overlay so the user can switch between Console and Inspector without losing the console.

- [ ] **Step 3: Preserve existing console behavior**

  Ensure the current Lua console, history, autocomplete, and help handling continue to work unchanged when the Console page is selected.

- [ ] **Step 4: Validate**

  Build the debug target and confirm the overlay still opens, focuses, and accepts input.

---

## Task 2: Add Read-Only Inspector Data Model

**Files:**
- Modify: `Source/debug.cpp`
- Modify: `Source/debug.h`
- Modify: `Source/debug_overlay/imgui_overlay.cpp`

- [ ] **Step 1: Add a hover target model**

  Introduce a small data structure that can represent the current hovered tile and optional contents:

  - tile coordinates
  - tile piece / transparency / solidity
  - monster reference if present
  - object reference if present
  - item/corpse/special data if present

- [ ] **Step 2: Add resolver helpers**

  Add helper functions that take a tile coordinate and return the relevant debug summary in a consistent format.

  Prefer existing tile helpers and properties over adding duplicate logic.

- [ ] **Step 3: Reuse legacy fallback behavior**

  If the code still has partial Tile API migration fallback behavior, make sure the inspector reports the same resolved values that the game uses for rendering and collision.

- [ ] **Step 4: Validate**

  Confirm the inspector renders the same tile information you would expect from current debug grid output and existing Lua/debug functions.

---

## Task 3: Add Hover-Based Inspector View

**Files:**
- Modify: `Source/debug_overlay/imgui_overlay.cpp`
- Modify: `Source/engine/render/scrollrt.cpp` if hover info needs to be exposed centrally

- [ ] **Step 1: Capture hovered world/tile position**

  Add a way for the overlay to identify which tile the cursor is over while the game is visible.

  If a shared hover position already exists, reuse it. Otherwise, derive it from mouse position and viewport geometry.

- [ ] **Step 2: Add an `Inspect Tile` toggle button**

  Add a button that enables/disables live inspection mode.

  When enabled, hovering the game world should update the inspector panel.

- [ ] **Step 3: Render inspector sections**

  Show sections such as:

  - Tile
  - Object
  - Monster
  - Item / corpse / special
  - Relevant flags and properties

- [ ] **Step 4: Make the inspector stable**

  Handle empty tiles and out-of-bounds positions gracefully.

- [ ] **Step 5: Validate**

  Move the cursor around town and dungeon scenes and verify the displayed information updates correctly and does not affect gameplay.

---

## Task 4: Add Existing Debug Feature Shortcuts to the GUI

**Files:**
- Modify: `Source/debug_overlay/imgui_overlay.cpp`
- Modify: `Source/lua/modules/dev/display.cpp`
- Modify: `Source/lua/modules/dev/level.cpp` if useful for display-only shortcuts

- [ ] **Step 1: Surface common toggles as buttons**

  Add buttons or checkboxes for already-existing debug features such as:

  - grid
  - vision
  - path
  - fullbright
  - tile data selection

- [ ] **Step 2: Link GUI controls to existing state**

  Toggle the existing debug flags rather than creating separate duplicate state.

- [ ] **Step 3: Keep the Lua console and GUI in sync**

  If a command changes a debug state, the GUI should reflect it on the next render.

- [ ] **Step 4: Validate**

  Confirm the GUI buttons and Lua commands stay in agreement.

---

## Task 5: Prepare Editing Hooks for Future Tile Swapping

**Files:**
- Modify: `Source/debug.cpp`
- Modify: `Source/debug.h`
- Modify: `Source/debug_overlay/imgui_overlay.cpp`

- [ ] **Step 1: Define action slots without enabling edits yet**

  Add placeholder action identifiers for future operations such as:

  - swap tile piece
  - change transparency
  - toggle solidity
  - spawn/remove local entities

- [ ] **Step 2: Keep actions behind explicit buttons**

  Do not perform edits automatically on hover.

  Require deliberate user interaction so the inspector remains safe and predictable.

- [ ] **Step 3: Leave mutation paths disabled or stubbed**

  If the UI is not ready for safe editing yet, wire the buttons to clear “not implemented yet” feedback instead of partial behavior.

- [ ] **Step 4: Validate**

  Confirm the inspector can be extended without restructuring the entire overlay again.

---

## Task 6: Add Regression Coverage and Documentation

**Files:**
- Test: `test/*.cpp`
- Modify: `docs/debug.md`

- [ ] **Step 1: Add tests for inspector data resolution**

  Cover the read-only helper logic for tile/entity summaries, especially the fallback behavior that was previously fragile during Tile migration.

- [ ] **Step 2: Add tests for overlay command/help behavior if changed**

  Ensure the console help text still works after the overlay grows beyond the console.

- [ ] **Step 3: Update documentation**

  Add a short section in `docs/debug.md` explaining:

  - how to open the overlay
  - what each tab does
  - how to use `Inspect Tile`
  - how console help and GUI features complement each other

- [ ] **Step 4: Validate**

  Run the relevant tests and a full debug build.

---

## Implementation Order

1. Add overlay navigation and preserve console behavior.
2. Build the read-only inspector data model.
3. Add hover-driven inspector rendering.
4. Surface existing debug toggles in the GUI.
5. Add future editing hooks as stubs.
6. Add tests and docs.

---

## Success Criteria

- The debug overlay contains both the Lua console and at least one other feature panel.
- A user can toggle an inspector mode and view live tile/entity details.
- Existing debug console behavior remains intact.
- The inspector reflects the same tile state used by the game.
- Future tile-editing work can be added without redesigning the overlay.
