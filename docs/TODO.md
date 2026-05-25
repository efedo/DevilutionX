### Comments

EF tasks:
1. Finish moving any remaining dungeon grids to level objects (e.g. extern int8_t dItem[MAXDUNX][MAXDUNY])
2. Consider refactoring the level grids into a single grid with multiple members? They are dense arrays anyway, so I don't
   think this will have many adverse impacts; but please think it over and comment.

- `BUGFIX` known bugs in original (vanilla) code
- `/* */` block comments are things to be fixed/checked
- `FIX_ME` bad data

Code issues (incorrect code that still works)

- Critical sections should be constructors using `CCritSect`
- Some functions/structures have incorrect signing (signed/unsigned BYTE)
