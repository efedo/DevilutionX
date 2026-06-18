# Modernization Opportunities

## Purpose

This document records low-risk opportunities to simplify DevilutionX code and
make it more legible with modern, readable C++. It is based on a broad scan of
the files under `Source`.

Modernization should preserve behavior and file, savegame, network, and memory
layout compatibility. Changes should be small enough to review independently
and should follow existing project helpers and conventions.

## Priorities

### 1. Replace Manual Array Sizes

Use `std::size(array)` when only the element count is needed. Use
`std::span<const T>` when a pointer and length are passed or selected together.

Representative targets:

- `Source/DiabloUI/hero/selhero.cpp`
- `Source/engine/demomode.cpp`
- `Source/items.cpp`
- `Source/levels/trigs.cpp`
- `Source/panels/partypanel.cpp`
- `Source/spells.cpp`

Example:

```cpp
for (size_t i = 0; i < std::size(statLabels); ++i) {
```

`ForceArenaTrig` in `levels/trigs.cpp` is a particularly useful target. Its
switch currently selects a pointer and separately calculates a length. Returning
a `std::span<const uint16_t>` from the selection logic would keep the data and
its size together.

### 2. Prefer `empty()` for Container Checks

Replace `container.size() == 0` and `container.size() > 0` with
`container.empty()` and `!container.empty()`.

Representative targets:

- `Source/debug.cpp`
- `Source/diablo.cpp`
- `Source/controls/devices/joystick.cpp`
- `Source/levels/gendung.cpp`

This is mechanical, improves intent, and avoids making readers distinguish
between count-dependent and emptiness-only code.

### 3. Simplify Optional and Expected Handling

Prefer direct boolean checks and dereferencing when the type supports them:

```cpp
if (auto value = magic_enum::enum_cast<MyEnum>(text); value)
	return *value;
```

Representative targets:

- `Source/effects.cpp`
- `Source/tables/itemdat.cpp`
- `Source/tables/monstdat.cpp`
- `Source/tables/playerdat.cpp`
- `Source/tables/textdat.cpp`
- `Source/tables/townerdat.cpp`

The repeated enum parsing pattern may justify a small shared helper if it
meaningfully reduces duplication without obscuring the error text. Avoid adding
an abstraction if the call sites require materially different validation.

For `tl::expected`, keep explicit error propagation where it makes control flow
clear. Do not replace readable error handling with dense expressions merely to
reduce line count.

### 4. Modernize UI String Handling

Prefer `std::string`, `std::string_view`, `fmt::format`, and existing UTF-8 copy
helpers over temporary fixed-size buffers and unchecked C string operations.

Representative targets:

- `Source/DiabloUI/hero/selhero.cpp`
- `Source/DiabloUI/multi/selgame.cpp`
- `Source/control/control_chat.cpp`

Examples include:

- Constructing the delete-character confirmation directly as a `std::string`
  instead of formatting and copying into `char dialogText[256]`.
- Replacing `strlen(buffer) > 0` with a direct first-character or
  `std::string_view` emptiness check.
- Replacing `strcpy` with `CopyUtf8` where a fixed external buffer must remain.
- Using existing ASCII case-conversion helpers rather than an indexed manual
  uppercase-to-lowercase loop.

Do not replace fixed buffers that are part of external APIs, packed structures,
or stable serialization formats without first verifying layout requirements.

### 5. Use Range-Based Grid Iteration

The project already provides `PointsInRectangle` and
`PointsInRectangleColMajor`. Use them where code simply visits every tile in a
rectangular region and does not depend on separate loop counters.

Representative targets:

- `Source/levels/crypt.cpp`
- `Source/levels/themes.cpp`
- `Source/levels/town.cpp`
- `Source/levels/trigs.cpp`

Example:

```cpp
for (const Point position :
    PointsInRectangle(Rectangle { { 0, 0 }, { MAXDUNX, MAXDUNY } })) {
	Tile &tile = tileAt(position);
	// ...
}
```

Retain indexed loops where traversal order, strides, paired arrays, or
performance-sensitive buffer access are central to the algorithm.

### 6. Replace Simple Array Clearing with Algorithms

Use `std::fill`, `std::ranges::fill`, or value initialization for ordinary typed
arrays when the intended value is expressed naturally by the element type.

Representative targets:

- `Source/items.cpp`
- `Source/player.cpp`
- `Source/qol/visual_store.cpp`

Example:

```cpp
std::fill(std::begin(player.walkpath), std::end(player.walkpath), WALK_NONE);
```

Continue using `memcpy`, `memmove`, and `memset` for encoded graphics, packet
buffers, packed save structures, cryptographic code, and byte-oriented file
formats where byte-level behavior is deliberate.

### 7. Prefer Value Assignment for Trivial Structures

Where a structure is safely assignable, replace copies such as:

```cpp
memcpy(&destination, &source, sizeof(destination));
```

with:

```cpp
destination = source;
```

Potential UI-focused targets exist in `Source/DiabloUI/hero/selhero.cpp`.
Before changing any copy in save, network, or asset code, verify that the code is
not intentionally copying a packed byte representation.

### 8. Move Single-Object Helpers onto Their Domain Types

Free functions that primarily inspect or mutate one object may be clearer as
methods, particularly when this removes repeated parameter naming and exposes a
natural object API.

Potential areas:

- Player inventory and equipment operations in `Source/inv.cpp`
- Item initialization and mutation helpers in `Source/items.cpp`
- Towner animation and initialization helpers in `Source/towners.cpp`

Apply this selectively. Functions coordinating multiple systems, global pools,
network state, rendering, or level transitions should generally remain
module-level operations.

### 9. Replace Output Parameters When Return Types Are Clearer

Some functions return success while modifying multiple output references.
Consider returning a small named structure when it makes ownership and valid
states clearer.

Representative targets:

- `GetRunGameLoop` in `Source/engine/demomode.cpp`
- `LoadPcxMeta` in `Source/utils/pcx_to_clx.cpp`
- `CalculatePreferredWindowSize` in `Source/utils/display.cpp`

This is a higher-risk category than the mechanical cleanups above because it
changes interfaces. Preserve existing APIs with wrappers when backward
compatibility requires it.

### 10. Use Named Constants and Typed Enums for Local Magic Values

Look for local loops and conditionals whose bounds encode domain concepts, such
as fixed player counts, UI row counts, or animation slots. Replace unexplained
numeric values with existing constants or narrowly scoped `constexpr` values.

Avoid introducing a constant when its name would merely repeat an obvious
literal. The goal is to explain domain meaning, not eliminate every number.

## Suggested Work Batches

### Batch 1: Mechanical Readability

- Replace manual array counts with `std::size`.
- Replace size comparisons with `empty()`.
- Replace remaining `NULL` uses in project-owned C++ code with `nullptr`.
- Simplify straightforward `optional` checks.

These changes should be grouped by subsystem or file rather than submitted as a
single project-wide mechanical patch.

### Batch 2: Safer UI Strings

- Remove unchecked `strcpy` calls in DiabloUI and control code.
- Replace temporary formatted character buffers with `std::string`.
- Use existing UTF-8 and ASCII case helpers consistently.

Add focused tests where input length, truncation, or character handling can
affect behavior.

### Batch 3: Ranges and Spans

- Convert pointer-and-count selections to `std::span`.
- Use range-based loops for simple array and container traversal.
- Use `PointsInRectangle` for uncomplicated tile-region iteration.

Review traversal order carefully before converting grid loops.

### Batch 4: Domain API Cleanup

- Identify helpers that naturally belong to `Player`, `Item`, `Towner`, or
  another existing type.
- Move only cohesive single-object behavior.
- Keep orchestration and cross-system operations at module scope.

Each migration should include focused tests and remove obsolete free functions
once all call sites have moved.

## Areas to Treat Carefully

The following areas contain code that may look old-fashioned but often requires
precise byte-level behavior:

- `Source/codec.cpp`
- `Source/encrypt.cpp`
- `Source/msg.cpp`
- `Source/multi.cpp`
- `Source/pack.cpp`
- `Source/pfile.cpp`
- `Source/loadsave.cpp`
- `Source/sha.cpp`
- `Source/engine/render`
- `Source/levels/reencode_dun_cels.cpp`

Modernize these only with format-aware tests and explicit confirmation that
binary compatibility is preserved.

## Verification Expectations

Every modernization change should:

- Preserve savegame, network, asset, and packed-structure compatibility.
- Add or update GTest coverage when behavior or interfaces are touched.
- Build the narrowest relevant target during development.
- Run the relevant registered tests before completion.
- Pass `git diff --check`.
- Include concise documentation and a clear commit summary.

Purely mechanical edits should remain behavior-neutral and avoid unrelated
formatting churn.
