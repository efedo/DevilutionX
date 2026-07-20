#pragma once

#include <cstdint>
#include <vector>

namespace devilution {

struct LevelGenerationRow {
	int minLevel;
	int maxLevel;
	int monsterDensityDivisor;
	int mpMonsterMultiplierPercent;
	int trapPercent;
};

extern std::vector<LevelGenerationRow> LevelGenerationData;

void LoadLevelGenerationData();
int GetMonsterDensityDivisor(int level);
int GetMpMonsterMultiplierPercent(int level);
int GetTrapPercent(int level);

} // namespace devilution
