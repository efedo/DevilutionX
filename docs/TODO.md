### Comments

EF tasks:
- Fix Critical reversion: cannot enter name during character creation (keyboard input blocked)
-
- Make each tile aware of its own coordinates so that you can use range based loops everywhere?
- Convert tile loops to range-based loops across the code-base, wherever feasible
-
- Rename this project (my fork of DevilutionX) Devilved (tag: "Devolution devolved")
- Add a new change log for Devilved (similar format to DevilutionX change log -- see devilution subfolder) and insert list of changes completed to date
- Add Copilot/Codex instructions, including where to put AI-generated working docs
- - Clean up redundancy between networked game and local game function
- Level map should have a single 2D grid of tiles, and each tile should have a list of entities on it (instead of having separate 2D arrays for each entity type)
- Move all implementation details from items.h to items.cpp
- Move all implementation details from player.h to player.cpp



- Finish moving any remaining dungeon grids to level objects (e.g. extern int8_t dItem[MAXDUNX][MAXDUNY])
- Consider refactoring the level grids into a single grid with multiple members? They are dense arrays anyway, so I don't
   think this will have many adverse impacts; but please think it over and comment.

- Add debug console/overlay via imGUI
  - Overlay should allow inspection of square contents
- Allow dynamic changes to level, dynamic addition of monsters/items via console
- Evantually: allow zooming in/out, multiple resolutions for sprites
- Eventually: separate all game-specific data from engine
  - Game-specfic data to be loaded via tsv files or lua scripts
  - Migrate all game-specific logic into LUA scripts
- Eventually: support other 2D RPGs in same engine
  - Support simultaneous loading of Diablo 1/2 levels, eventually everything
- Very distant future: support other 2D tile-based games, like Warcraft II
- Eventually: allow loading of save files from Diablo 1/2, Warcraft I/II, and allow playing them in the same engine
  - Binary-compatible saving may or may not be realistic, but at least support loading of save files and converting them to a common format that can be saved/loaded by the engine


- `BUGFIX` known bugs in original (vanilla) code
- `/* */` block comments are things to be fixed/checked
- `FIX_ME` bad data

Code issues (incorrect code that still works)

- Critical sections should be constructors using `CCritSect`
- Some functions/structures have incorrect signing (signed/unsigned BYTE)
