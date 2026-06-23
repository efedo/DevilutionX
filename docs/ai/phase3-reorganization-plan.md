# Phase 3: Source Reorganization

## Items (one commit each, build after each)

### P0: Merge control/ + panels/ → panel/
- `git mv` all files from `control/` and `panels/` into `panel/`
- No filename collisions between the two dirs
- Update CMakeLists.txt
- Update all `#include "control/..."` and `#include "panels/..."` → `"panel/..."`
- Remove empty `control/` and `panels/` dirs

### P1: Validation collection
- `git mv items/validation.* → validation/`
- `git mv monsters/validation.* → validation/`
- `git mv players/validation.* → validation/`
- `git mv portals/validation.hpp → validation/`
- `git mv quests/validation.hpp → validation/`
- Update CMakeLists.txt
- Update includes

### P2: Rename DiabloUI/ → menus/
- `git mv DiabloUI/ menus/`
- Retain subdirs `menus/hero/` and `menus/multi/`
- Update CMakeLists.txt
- Update includes (`"DiabloUI/"` → `"menus/"`)

### P3: Subdivide engine/
- Create `engine/audio/`, `engine/gfx/`, `engine/load/`, `engine/math/`
- Move files and update includes
- **engine/audio/**: effects, sound, sound_defs, sound_effect_enums, sound_position, sound_stubs, effects_stubs
- **engine/gfx/**: clx_sprite, palette, surface, dx, trn, backbuffer_state
- **engine/load/**: load_cel, load_cl2, load_clx, load_pcx, load_file
- **engine/math/**: point, displacement, rectangle, size, circle, direction, world_tile, points_in_rectangle_range
- Remaining ~28 files stay at engine/ root

### P4: Subdivide utils/
- Create `utils/string/`, `utils/sdl/`, `utils/endian/`, `utils/image/`, `utils/container/`, `utils/file/`
- Move files and update includes
- **utils/string/**: str_cat, str_case, str_split, string_or_view, string_view_hash, utf8, format_int
- **utils/sdl/**: sdl2_backports, sdl2_to_1_2_backports, sdl_bilinear_scale, sdl_compat, sdl_geometry, sdl_mutex, sdl_ptrs, sdl_thread, sdl_wrap
- **utils/endian/**: endian_read, endian_stream, endian_swap, endian_write
- **utils/image/**: cel_to_clx, cl2_to_clx, pcx, png, pcx_to_clx, surface_to_clx, surface_to_pcx, surface_to_png, clx_decode, clx_encode
- **utils/container/**: bitset2d, entity_pool, intrusive_optional, static_vector
- **utils/file/**: file_util, file_name_generator, logged_fstream
- `math.h` stays at utils/ root (fundamental macros)
- Existing `utils/algorithm/` and `utils/stdcompat/` stay as-is
- Remaining ~22 files stay at utils/ root

### P5: Rename dvlnet/ → net_transport/
- `git mv dvlnet/ net_transport/`
- Update CMakeLists.txt
- Update includes (`"dvlnet/"` → `"net_transport/"`)
