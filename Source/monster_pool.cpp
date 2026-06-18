/**
 * MonsterPool implementation: owns Monster storage and legacy pool aliases.
 *
 * gMonsterPool owns the Monster array. Monsters is a pointer alias into pool memory.
 * ActiveMonsters and ActiveMonsterCount are separate owned globals because the pool's
 * internal index storage uses int, which cannot be aliased to unsigned/size_t.
 */
#include "monster_pool.h"

#include "monster.h"

namespace devilution {

MonsterPool gMonsterPool;

// Legacy pointer alias: Monsters[i] == gMonsterPool[i] (same memory)
Monster *Monsters = gMonsterPool.data();

// Active index bookkeeping (separate from pool internals due to type mismatch)
unsigned ActiveMonsters[MaxMonsters];
size_t ActiveMonsterCount;

void InitializeMonsterPool()
{
	gMonsterPool.clear();
	ActiveMonsterCount = 0;
}

} // namespace devilution
