### Comments

EF tasks:
1. Finish moving any remaining dungeon grids to level objects (e.g. extern int8_t dItem[MAXDUNX][MAXDUNY])
2. Consider refactoring the level grids into a single grid with multiple members? They are dense arrays anyway, so I don't
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


- `BUGFIX` known bugs in original (vanilla) code
- `/* */` block comments are things to be fixed/checked
- `FIX_ME` bad data

Code issues (incorrect code that still works)

- Critical sections should be constructors using `CCritSect`
- Some functions/structures have incorrect signing (signed/unsigned BYTE)
