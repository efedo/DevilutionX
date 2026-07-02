# DevilutionY Changelog

All notable changes to this fork are documented below.

## 2026-07-01

- Refactor engine/lighting into `LightManager` class with singleton `CurrentLightManager`; update ~22 caller files
- Move 12 theme/trigger/crypt scalar globals (`numthemes`, `armorFlag`, `weaponFlag`, `zharlib`, `trigflag`, `numtrigs`, `TWarpFrom`, `UberRow`, `UberCol`, `IsUberRoomOpened`, `IsUberLeverActivated`, `UberDiabloMonsterIndex`) into `Level` class with inline accessors
- Replace `Missile::var1`-`var7` with typed anonymous union providing named struct accessors per missile family (projectile, guardian, portal, rune, wall, chain, inferno, nova, firewall, lightning, golem); migrate ProcessGenericProjectile and ProcessGuardian/AddGuardian

## 2026-06-24

- Add file-level summaries to all source files missing them
- Remove Testing/ from tracking, add to .gitignore
- Move mpq/ to data/mpq/ and storm/ to network/storm/
- Restructure network/ directory (protocol/ and transport/ subfolders)
- Move validation/ files back to respective subject folders
- Move menus/, panel/, debug_overlay/ under ui/
- Remove Discord integration

## 2026-06-23

- Rename dvlnet/ to net_transport/
- Subdivide utils/ into string/, sdl/, endian/, image/, container/, file/
- Subdivide engine/ into audio/, gfx/, load/, math/ subdirectories
- Rename DiabloUI/ to menus/
- Collect validation files into Source/validation/
- Merge control/ and panels/ into panel/ directory
- Remove compatibility forwarders and update all includes to canonical paths
- Migrate remaining Source/ files to domain subdirectories
- Migrate game domains to Source/game/
- Update docs
- Fix portal migration verification
- Document portal source migration
- Move portals into game domain
- Add canonical portal domain interfaces
- Plan portal domain source migration
- Document source organization architecture

## 2026-06-22

- Replace gendung macro shims with accessors
- Plan gendung macro shim removal
- Require comments on level accessors
- Document gendung macro shim removal

## 2026-06-21

- Add positioned tile grid iteration
- Document tile grid iteration API
- Plan tile position range implementation
- Document tile position range design
- Modernize dungeon level tile storage
- Document unified dungeon megatile storage
- Document legacy dTransVal removal

## 2026-06-20

- Remove legacy dPiece storage
- Document legacy dPiece removal
- Move AI docs under docs/AI
- Move player helpers into Player
- line endings
- Add graphical debug piece selector
- Fix debug overlay windows and editor selection
- Expand debug overlay: add Inspector panel & toolbar
- Add built-in help system to debug Lua console

## 2026-06-19

- Add tile fallback and sync with legacy map data
- Refactor: add levelMicros() span for tile micro access
- Migrate dungeon flags and special data to Tile API
- Remove legacy dungeon entity arrays, use Tile API only
- Refactor tile access to use tileAt(Point) API
- Add object subsystem unit tests and test visibility
- Complete Tile state migration
- Update CMake, codecov, and level init in tests/benchmarks
- Add ImGui debug overlay console and refactor console history

## 2026-06-18

- Phase 4B COMPLETE: Update documentation
- Phase 4B-12,13,14: Final entity macro migrations
- Phase 4B-9,10,11: Migrate debug, towners, sync, inv to Tile API
- Phase 4B-8: Migrate cursor.cpp to Tile API entity accessors
- Phase 4B-7: Migrate missiles.cpp to Tile API entity accessors
- Phase 4B-6: Migrate loadsave.cpp to Tile API entity accessors
- Phase 4B-3,4,5: Migrate dead.cpp, items.cpp, objects.cpp to Tile API
- Phase 4B-1 & 4B-2: Migrate player.cpp and monster.cpp to Tile API entity accessors

## 2026-06-17

- Phase 4A lighting migration: Tile API conversion, tests, benchmarks
- Better agent instructions
- Migrate rendering hot-paths to Tile API, update docs

## 2026-06-16

- Add Tile class and Level integration (Phase 2)
- Migrate dItem to be level-specific, not global
- Refactor dItem to be level-specific, not global
- Minor items.h cleanup
- Refactor active entity bookkeeping to pool headers
- Refactor to use MonsterPoolAdapter and ItemPoolAdapter
- Introduce monster pool for modern entity management

## 2026-06-15

- Clean up player attribute migration
- Clarify packed player validation fields
- Group player damage bonuses
- Group player life and mana resources
- Normalize player header line endings
- Modernize object storage with DenseEntityPool
- Refactor object pool access via ObjectPoolAdapter

## 2026-06-14

- Modernize object pool with type-safe container
- Introduce ObjectManager facade for object operations
- Minor comment clean-up to objects.h
- Initial objects.h cleanup
- Add shared attribute value types
- Plan player attribute model migration
- Document player attribute model design

## 2026-06-13

- Refactor player logic into Player class methods
- Introduce World and Level state containers
- Introduce Bestiary and migrate Monster behavior
- Add CombatActor base type
- Refactor players and monsters onto Actor
- Introduce Bestiary class and related engine updates
- Introduce Actor base struct for Player, Monster
