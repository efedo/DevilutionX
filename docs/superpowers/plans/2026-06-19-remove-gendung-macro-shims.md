# Remove `gendung.h` Macro Shims Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace every transitional level-state macro in `gendung.h` with typed free accessors and migrate every in-tree consumer.

**Architecture:** Add reference-preserving inline accessors over `currentLevel()`, with brief trailing comments, and ordinary overloads for `tileAt`. Migrate call sites in compiler-guided batches before deleting the macro block, preserving runtime state, layout, traversal order, and save compatibility.

**Tech Stack:** C++20, GTest, CMake, MSVC/Ninja, PowerShell mechanical replacement

---

### Task 1: Add Macro-Removal Test

**Files:**
- Modify: `test/tile_migration_sync_test.cpp`

- [ ] Add preprocessor checks after including `gendung.h` that fail compilation when any legacy shim remains defined.
- [ ] Build `tile_migration_sync_test` and confirm it fails because the macros are still present.

### Task 2: Add Typed Accessors

**Files:**
- Modify: `Source/levels/gendung.h`

- [ ] Add inline reference-preserving accessors for every state macro.
- [ ] Add ordinary `tileAt(Point)` and `tileAt(x, y)` overloads.
- [ ] Keep `levelMicros()` and `megaTileAt()` as established typed accessors.
- [ ] Add a succinct trailing `//` comment after every new accessor declaration.

### Task 3: Migrate Call Sites

**Files:**
- Modify: all matching files under `Source` and `test`

- [ ] Mechanically replace read, assignment, indexing, and call uses with the approved accessor names.
- [ ] Rename local identifiers only where they collide with accessor function names.
- [ ] Build after state groups to locate ambiguous or missed uses.

### Task 4: Remove Macro Definitions

**Files:**
- Modify: `Source/levels/gendung.h`
- Modify: `docs/devilution/CHANGELOG.md`

- [ ] Delete the complete transitional macro block and stale migration comments.
- [ ] Add a concise changelog entry for typed level-state accessors.
- [ ] Search `gendung.h` and the repository to prove none of the removed macros remain defined.

### Task 5: Verify

**Files:**
- Verify all modified source and test files

- [ ] Build `tile_migration_sync_test` and `devilutionx`.
- [ ] Run `tile_migration_sync_test`.
- [ ] Build additional registered tests affected by migrated level state.
- [ ] Run whitespace checks with `core.whitespace=cr-at-eol`.
- [ ] Verify all modified files use CRLF exclusively.
- [ ] Preserve and exclude the unrelated pending comment cleanup and obsolete-example deletions from any task-specific commit.
