# Phase 2 Source File Reorganization - Execution Plan

## Strategy
Game domains first. Compatibility forwarders for single-domain moves (proven portals pattern). Direct include-site updates for the `levels/` directory move. Characterization tests created before moving weakly-tested domains.

## Task Order

| # | Task | Domain | New files | Forwarders | Key risk |
|---|------|--------|-----------|------------|----------|
| 1 | Characterization tests | spells, monsters | 2 test files | - | Test design |
| 2 | spells ships game/spells/ | 2 files | 2 | 1 | Low |
| 3 | missiles ships game/missiles/ | 2 files | 2 | 1 | Low |
| 4 | objects ships game/objects/ | 6 files | 6 | 3 | Low |
| 5 | items ships game/items/ | 4+2 files | 6 | 2 | Low |
| 6 | monsters ships game/monsters/ | 4+2 files | 6 | 2 | Low |
| 7 | player ships game/players/ | 2+2 files | 4 | 1 | Med (64 consumers) |
| 8 | levels/ ships game/levels/ | 30 files | 0 | 0 (direct update) | Med (79 files to update) |
| 9 | engine support ships engine/ | 12 files | 12 | 12 | Low |
| 10 | support files ships support/ | 5 files | 5 | 4 | Low |
| 11 | network files ships network/ | 5 files | 5 | 5 | Low |
| 12 | persistence files ships persistence/ | 3 files | 3 | 3 | Low |
| 13 | application files ships application/ | 7 files | 7 | 7 | Med (game_mode 47 consumers) |
| 14 | UI files ships ui/ | 12 files | 12 | 12 | Low |

## Pattern for each domain move (compatibility forwarder approach)
1. Create canonical header at `game/<domain>/<domain>.hpp`
2. Convert old header to `#include "game/<domain>/<domain>.hpp"` forwarder
3. `git mv` the `.cpp` to `game/<domain>/<domain>.cpp`
4. Update the `.cpp`'s self-include and `@file` line to canonical path
5. Update `Source/CMakeLists.txt` source paths
6. Build `libdevilutionx` + `devilutionx` + domain test
7. Run domain test

## Verification
Each task: build affected target, run domain test, confirm no regressions.
Final: full build + `ctest --test-dir build --output-on-failure -j $(nproc)`.
