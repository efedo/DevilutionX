# Player Attribute Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace raw `Player` stat/resource/damage fields with small grouped value types while preserving gameplay, save compatibility, and network packet layout.

**Architecture:** Add shared plain value structs, then migrate `Player` in three compile-preserving slices: primary attributes, life/mana resources, and damage bonuses. Keep save and packet structs binary-compatible by mapping old serialized fields explicitly to the new runtime fields.

**Tech Stack:** C++17, CMake, Visual Studio generator, GoogleTest/CTest, existing DevilutionX save/load and pack helpers.

---

## File Structure

- Create: `Source/engine/attributes.hpp`
  - Shared value structs: `ModifiableAttribute`, `VitalResource`, `DamageRange`, `DamageBonuses`, `PrimaryAttributes`.
- Modify: `Source/player.h`
  - Include the shared header and replace raw stat/resource/damage member clusters.
  - Keep existing public methods during migration.
- Modify: `Source/player.cpp`
  - Move all direct `Player` field logic to the grouped members.
- Modify: `Source/items.cpp`, `Source/objects.cpp`, `Source/spells.cpp`, `Source/monster.cpp`, `Source/stores.cpp`, `Source/multi.cpp`, `Source/loadsave.cpp`, `Source/pack.cpp`, `Source/panels/charpanel.cpp`, `Source/engine/render/scrollrt.cpp`, `Source/panels/partypanel.cpp`, `Source/lua/modules/player.cpp`, `Source/lua/modules/dev/player/stats.cpp`
  - Update direct field access to grouped members or existing `Player` methods.
- Modify: `Source/pack.h`
  - Keep binary layout unchanged. Rename fields only if each rename has an explicit compatibility comment and all pack/unpack code is updated in the same task.
- Modify: `test/player_test.cpp`, `test/pack_test.cpp`, `test/writehero_test.cpp`, `test/char_panel_test.cpp`, `test/store_transaction_test.cpp`
  - Update assertions to use grouped fields or public methods.

## Task 1: Add Shared Attribute Value Types

**Files:**
- Create: `Source/engine/attributes.hpp`
- Modify: `Source/player.h`
- Test: `test/player_test.cpp`

- [ ] **Step 1: Add the shared value structs**

Create `Source/engine/attributes.hpp`:

```cpp
/**
 * @file engine/attributes.hpp
 *
 * Small value types for combat attributes and resources.
 */
#pragma once

namespace devilution {

struct ModifiableAttribute {
	int base = 0;
	int current = 0;
};

struct PrimaryAttributes {
	ModifiableAttribute strength;
	ModifiableAttribute magic;
	ModifiableAttribute dexterity;
	ModifiableAttribute vitality;
};

struct VitalResource {
	int base = 0;
	int current = 0;
	int maximum = 0;
	int maximumBase = 0;
	int percentage = 0;
};

struct DamageRange {
	int minimum = 0;
	int maximum = 0;
};

struct DamageBonuses {
	DamageRange physical;
	DamageRange fire;
	DamageRange lightning;
	int percent = 0;
	int flat = 0;
	int armorPiercing = 0;
};

} // namespace devilution
```

- [ ] **Step 2: Include the new header**

In `Source/player.h`, add the include beside the other engine includes:

```cpp
#include "engine/attributes.hpp"
```

- [ ] **Step 3: Build the narrow target**

Run:

```powershell
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat'
$cmake = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
cmd /c "`"$vs`" -arch=x64 && `"$cmake`" --build build/x64-Debug --target player_test --config Debug -j 4"
```

Expected: `player_test.exe` links successfully.

- [ ] **Step 4: Commit**

Run:

```powershell
git add Source/engine/attributes.hpp Source/player.h
git commit -m "Add shared attribute value types"
```

## Task 2: Convert Primary Attributes

**Files:**
- Modify: `Source/player.h`
- Modify: `Source/player.cpp`
- Modify: direct call sites found by `rg "_pBaseStr|_pStrength|_pBaseMag|_pMagic|_pBaseDex|_pDexterity|_pBaseVit|_pVitality" Source test`
- Test: `test/player_test.cpp`, `test/char_panel_test.cpp`, `test/pack_test.cpp`, `test/writehero_test.cpp`

- [ ] **Step 1: Replace raw primary stat fields**

In `Source/player.h`, replace:

```cpp
int _pStrength;
int _pBaseStr;
int _pMagic;
int _pBaseMag;
int _pDexterity;
int _pBaseDex;
int _pVitality;
int _pBaseVit;
```

with:

```cpp
PrimaryAttributes attributes;
```

- [ ] **Step 2: Update `Player` attribute accessors**

In `Source/player.cpp`, update `GetBaseAttributeValue`:

```cpp
int Player::GetBaseAttributeValue(CharacterAttribute attribute) const
{
	switch (attribute) {
	case CharacterAttribute::Dexterity:
		return attributes.dexterity.base;
	case CharacterAttribute::Magic:
		return attributes.magic.base;
	case CharacterAttribute::Strength:
		return attributes.strength.base;
	case CharacterAttribute::Vitality:
		return attributes.vitality.base;
	}
	app_fatal("Invalid attribute");
}
```

Update `GetCurrentAttributeValue`:

```cpp
int Player::GetCurrentAttributeValue(CharacterAttribute attribute) const
{
	switch (attribute) {
	case CharacterAttribute::Dexterity:
		return attributes.dexterity.current;
	case CharacterAttribute::Magic:
		return attributes.magic.current;
	case CharacterAttribute::Strength:
		return attributes.strength.current;
	case CharacterAttribute::Vitality:
		return attributes.vitality.current;
	}
	app_fatal("Invalid attribute");
}
```

- [ ] **Step 3: Update creation and stat mutation code**

Replace assignments in `Player::create`:

```cpp
player.attributes.strength.base = attr.baseStr;
player.attributes.strength.current = player.attributes.strength.base;
player.attributes.magic.base = attr.baseMag;
player.attributes.magic.current = player.attributes.magic.base;
player.attributes.dexterity.base = attr.baseDex;
player.attributes.dexterity.current = player.attributes.dexterity.base;
player.attributes.vitality.base = attr.baseVit;
player.attributes.vitality.current = player.attributes.vitality.base;
```

Update the four mutators to operate on `attributes.<name>.base/current`. For example, `modifyStrength` should use:

```cpp
l = std::clamp(l, 0 - player.attributes.strength.base, player.GetMaximumAttributeValue(CharacterAttribute::Strength) - player.attributes.strength.base);
player.attributes.strength.current += l;
player.attributes.strength.base += l;
```

Apply the same pattern for magic, dexterity, and vitality.

- [ ] **Step 4: Update formula call sites**

Use `GetCurrentAttributeValue` or direct grouped fields where local code is already inside `Player`.

Examples:

```cpp
return attributes.strength.current >= item._iMinStr
    && attributes.magic.current >= item._iMinMag
    && attributes.dexterity.current >= item._iMinDex;
```

```cpp
return _pIBonusAC + _pIAC + attributes.dexterity.current / 5;
```

```cpp
return attr.adjLife + (attr.lvlLife * getCharacterLevel()) + (attr.chrLife * attributes.vitality.base);
```

- [ ] **Step 5: Update pack/save and UI call sites**

Keep `PlayerPack` and `PlayerNetPack` field names unchanged in this task. Update mapping code only:

```cpp
packed.pBaseStr = player.attributes.strength.base;
packed.pBaseMag = player.attributes.magic.base;
packed.pBaseDex = player.attributes.dexterity.base;
packed.pBaseVit = player.attributes.vitality.base;
```

```cpp
player.attributes.strength.base = packed.pBaseStr;
player.attributes.strength.current = player.attributes.strength.base;
```

Apply equivalent updates for magic, dexterity, and vitality.

- [ ] **Step 6: Update tests**

Replace direct test assertions such as:

```cpp
ASSERT_EQ(player._pBaseStr, 20);
ASSERT_EQ(player._pStrength, 20);
```

with:

```cpp
ASSERT_EQ(player.attributes.strength.base, 20);
ASSERT_EQ(player.attributes.strength.current, 20);
```

- [ ] **Step 7: Verify primary attributes**

Run:

```powershell
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat'
$cmake = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
cmd /c "`"$vs`" -arch=x64 && `"$cmake`" --build build/x64-Debug --target player_test pack_test char_panel_test --config Debug -j 4"
```

Expected: all three test executables link successfully.

- [ ] **Step 8: Run focused tests**

Run:

```powershell
ctest --test-dir build/x64-Debug -C Debug -R "player_test|pack_test|char_panel_test" --output-on-failure
```

Expected: all selected tests pass.

- [ ] **Step 9: Commit**

Run:

```powershell
git add Source test
git commit -m "Group player primary attributes"
```

## Task 3: Convert Life And Mana Resources

**Files:**
- Modify: `Source/player.h`
- Modify: `Source/player.cpp`
- Modify: direct call sites found by `rg "hitPoints|maxHitPoints|_pHPBase|_pMaxHPBase|_pHPPer|_pMana|_pMaxMana|_pManaBase|_pMaxManaBase|_pManaPer" Source test`
- Test: `test/player_test.cpp`, `test/pack_test.cpp`, `test/writehero_test.cpp`, `test/store_transaction_test.cpp`

- [ ] **Step 1: Move player life out of inherited `Actor` storage**

Because `Monster` still uses `Actor::hitPoints` and `Actor::maxHitPoints`, do not remove those inherited fields yet. In `Source/player.h`, add player-specific resources:

```cpp
VitalResource life;
VitalResource mana;
```

Then update every player call site from `player.hitPoints` and `player.maxHitPoints` to `player.life.current` and `player.life.maximum`. Leave monster call sites unchanged.

- [ ] **Step 2: Update life/mana creation**

In `Player::create`, replace life/mana assignments with:

```cpp
player.life.current = player.calculateBaseLife();
player.life.maximum = player.life.current;
player.life.base = player.life.current;
player.life.maximumBase = player.life.current;

player.mana.current = player.calculateBaseMana();
player.mana.maximum = player.mana.current;
player.mana.base = player.mana.current;
player.mana.maximumBase = player.mana.current;
```

- [ ] **Step 3: Update life and mana methods**

`RestoreFullLife`:

```cpp
void Player::RestoreFullLife()
{
	life.current = life.maximum;
	life.base = life.maximumBase;
}
```

`RestoreFullMana`:

```cpp
void Player::RestoreFullMana()
{
	if (HasNoneOf(_pIFlags, ItemSpecialEffect::NoMana)) {
		mana.current = mana.maximum;
		mana.base = mana.maximumBase;
	}
}
```

`UpdateHitPointPercentage`:

```cpp
int Player::UpdateHitPointPercentage()
{
	if (life.maximum <= 0) {
		life.percentage = 0;
	} else {
		life.percentage = std::clamp(life.current * 81 / life.maximum, 0, 81);
	}

	return life.percentage;
}
```

`UpdateManaPercentage`:

```cpp
int Player::UpdateManaPercentage()
{
	if (mana.maximum <= 0) {
		mana.percentage = 0;
	} else {
		mana.percentage = std::clamp(mana.current * 81 / mana.maximum, 0, 81);
	}

	return mana.percentage;
}
```

- [ ] **Step 4: Update save/load mappings**

In `Source/loadsave.cpp`, keep file order identical. Replace reads:

```cpp
player.life.base = file.NextLE<int32_t>();
player.life.maximumBase = file.NextLE<int32_t>();
player.life.current = file.NextLE<int32_t>();
player.life.maximum = file.NextLE<int32_t>();
file.Skip<int32_t>(); // Skip life.percentage - always derived from current and maximum.
```

Replace mana reads:

```cpp
player.mana.base = file.NextLE<int32_t>();
player.mana.maximumBase = file.NextLE<int32_t>();
player.mana.current = file.NextLE<int32_t>();
player.mana.maximum = file.NextLE<int32_t>();
file.Skip<int32_t>(); // Skip mana.percentage - always derived from current and maximum.
```

Write the fields in the same order.

- [ ] **Step 5: Update pack mappings**

Keep `PlayerPack` and `PlayerNetPack` binary field order unchanged. Update assignments:

```cpp
packed.pHPBase = Swap32LE(player.life.base);
packed.pMaxHPBase = Swap32LE(player.life.maximumBase);
packed.pManaBase = Swap32LE(player.mana.base);
packed.pMaxManaBase = Swap32LE(player.mana.maximumBase);
packed.pHitPoints = Swap32LE(player.life.current);
packed.pMaxHP = Swap32LE(player.life.maximum);
packed.pMana = Swap32LE(player.mana.current);
packed.pMaxMana = Swap32LE(player.mana.maximum);
```

- [ ] **Step 6: Update UI and Lua call sites**

Examples:

```cpp
DrawFlaskValues(out, position, MyPlayer->life.current >> 6, MyPlayer->life.maximum >> 6);
```

```cpp
[](Player &player) { return player.mana.current >> 6; }
```

- [ ] **Step 7: Update tests**

Replace direct assertions:

```cpp
ASSERT_EQ(player.hitPoints, 2880);
ASSERT_EQ(player.maxHitPoints, 2880);
ASSERT_EQ(player._pMana, 1440);
ASSERT_EQ(player._pMaxMana, 1440);
```

with:

```cpp
ASSERT_EQ(player.life.current, 2880);
ASSERT_EQ(player.life.maximum, 2880);
ASSERT_EQ(player.mana.current, 1440);
ASSERT_EQ(player.mana.maximum, 1440);
```

- [ ] **Step 8: Verify life and mana**

Run:

```powershell
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat'
$cmake = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
cmd /c "`"$vs`" -arch=x64 && `"$cmake`" --build build/x64-Debug --target player_test pack_test writehero_test store_transaction_test --config Debug -j 4"
ctest --test-dir build/x64-Debug -C Debug -R "player_test|pack_test|writehero_test|store_transaction_test" --output-on-failure
```

Expected: selected tests pass.

- [ ] **Step 9: Commit**

Run:

```powershell
git add Source test
git commit -m "Group player life and mana resources"
```

## Task 4: Convert Damage Ranges And Bonuses

**Files:**
- Modify: `Source/player.h`
- Modify: `Source/player.cpp`
- Modify: `Source/items.cpp`
- Modify: `Source/panels/charpanel.cpp`
- Modify: `Source/loadsave.cpp`
- Modify: `Source/pack.cpp`
- Test: `test/player_test.cpp`, `test/pack_test.cpp`, `test/writehero_test.cpp`

- [ ] **Step 1: Replace raw damage fields**

In `Source/player.h`, replace:

```cpp
int _pIMinDam;
int _pIMaxDam;
int _pIBonusDam;
int _pIBonusDamMod;
int _pIEnAc;
int _pIFMinDam;
int _pIFMaxDam;
int _pILMinDam;
int _pILMaxDam;
```

with:

```cpp
DamageBonuses damageBonuses;
```

Leave `_pIAC`, `_pIBonusToHit`, `_pIBonusAC`, and `_pIGetHit` unchanged in this task because they are defense/to-hit fields, not damage ranges.

- [ ] **Step 2: Update damage calculations**

Replace physical damage reads:

```cpp
const int mind = player.damageBonuses.physical.minimum;
const int maxd = player.damageBonuses.physical.maximum;
dam += dam * player.damageBonuses.percent / 100;
dam += player.damageBonuses.flat;
```

Replace armor piercing reads:

```cpp
if (damageBonuses.armorPiercing > 0) {
	int armorPiercing = damageBonuses.armorPiercing - 1;
```

- [ ] **Step 3: Update item recalculation**

In `Source/items.cpp`, replace assignments:

```cpp
player.damageBonuses.physical.minimum = minDamage;
player.damageBonuses.physical.maximum = maxDamage;
player.damageBonuses.percent = dam;
player.damageBonuses.flat = damMod;
player.damageBonuses.armorPiercing = targetAc;
player.damageBonuses.fire.minimum = minFireDam;
player.damageBonuses.fire.maximum = maxFireDam;
player.damageBonuses.lightning.minimum = minLightDam;
player.damageBonuses.lightning.maximum = maxLightDam;
```

- [ ] **Step 4: Update save/load and packet mappings**

Keep serialized order unchanged. In `Source/loadsave.cpp`, read:

```cpp
player.damageBonuses.physical.minimum = file.NextLE<int32_t>();
player.damageBonuses.physical.maximum = file.NextLE<int32_t>();
```

Map packet fields:

```cpp
packed.pIMinDam = Swap32LE(player.damageBonuses.physical.minimum);
packed.pIMaxDam = Swap32LE(player.damageBonuses.physical.maximum);
packed.pIBonusDam = Swap32LE(player.damageBonuses.percent);
packed.pIBonusDamMod = Swap32LE(player.damageBonuses.flat);
packed.pIEnAc = Swap32LE(player.damageBonuses.armorPiercing);
packed.pIFMinDam = Swap32LE(player.damageBonuses.fire.minimum);
packed.pIFMaxDam = Swap32LE(player.damageBonuses.fire.maximum);
packed.pILMinDam = Swap32LE(player.damageBonuses.lightning.minimum);
packed.pILMaxDam = Swap32LE(player.damageBonuses.lightning.maximum);
```

- [ ] **Step 5: Update tests**

Replace:

```cpp
ASSERT_EQ(player._pIMinDam, 1);
ASSERT_EQ(player._pIMaxDam, 4);
ASSERT_EQ(player._pIBonusDam, 0);
ASSERT_EQ(player._pIBonusDamMod, 0);
ASSERT_EQ(player._pIEnAc, 0);
```

with:

```cpp
ASSERT_EQ(player.damageBonuses.physical.minimum, 1);
ASSERT_EQ(player.damageBonuses.physical.maximum, 4);
ASSERT_EQ(player.damageBonuses.percent, 0);
ASSERT_EQ(player.damageBonuses.flat, 0);
ASSERT_EQ(player.damageBonuses.armorPiercing, 0);
```

- [ ] **Step 6: Verify damage migration**

Run:

```powershell
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat'
$cmake = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
cmd /c "`"$vs`" -arch=x64 && `"$cmake`" --build build/x64-Debug --target player_test pack_test writehero_test --config Debug -j 4"
ctest --test-dir build/x64-Debug -C Debug -R "player_test|pack_test|writehero_test" --output-on-failure
```

Expected: selected tests pass.

- [ ] **Step 7: Commit**

Run:

```powershell
git add Source test
git commit -m "Group player damage bonuses"
```

## Task 5: Rename Packed Concepts With Compatibility Comments

**Files:**
- Modify: `Source/pack.h`
- Modify: `Source/pack.cpp`
- Modify: `test/pack_test.cpp`
- Modify: `test/writehero_test.cpp`

- [ ] **Step 1: Decide which packed names are worth renaming**

Rename only validation-only fields in `PlayerNetPack`, because they are not part of disk save files. Keep `PlayerPack` names unchanged to avoid obscuring the save file memory map.

Use these renames:

```cpp
int32_t currentStrength;
int32_t currentMagic;
int32_t currentDexterity;
int32_t currentVitality;
int32_t currentHitPoints;
int32_t maximumHitPoints;
int32_t currentMana;
int32_t maximumMana;
int32_t physicalDamageMinimum;
int32_t physicalDamageMaximum;
int32_t damagePercentBonus;
int32_t damageFlatBonus;
int32_t armorPiercing;
int32_t fireDamageMinimum;
int32_t fireDamageMaximum;
int32_t lightningDamageMinimum;
int32_t lightningDamageMaximum;
```

- [ ] **Step 2: Add the compatibility comment**

Above the validation fields in `Source/pack.h`, write:

```cpp
// Validation-only mirrors of runtime fields. These names are clearer than the
// legacy `_p*` names, but the field order must remain stable for network compatibility.
```

- [ ] **Step 3: Update pack/unpack code and tests**

For each renamed `PlayerNetPack` field, update the mapping in `PackNetPlayer`, validation in `UnPackNetPlayer`, and invalid-field tests in `test/pack_test.cpp`.

Example:

```cpp
packed.currentHitPoints = Swap32LE(player.life.current);
ValidateFields(player.life.current, SwapSigned32LE(packed.currentHitPoints), player.life.current == SwapSigned32LE(packed.currentHitPoints));
```

- [ ] **Step 4: Verify packet compatibility**

Run:

```powershell
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat'
$cmake = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
cmd /c "`"$vs`" -arch=x64 && `"$cmake`" --build build/x64-Debug --target pack_test writehero_test --config Debug -j 4"
ctest --test-dir build/x64-Debug -C Debug -R "pack_test|writehero_test" --output-on-failure
```

Expected: selected tests pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add Source/pack.h Source/pack.cpp test/pack_test.cpp test/writehero_test.cpp
git commit -m "Clarify packed player validation fields"
```

## Task 6: Final Cleanup And Full Verification

**Files:**
- Modify: any remaining files reported by searches.

- [ ] **Step 1: Search for old player field names**

Run:

```powershell
rg "_pBaseStr|_pStrength|_pBaseMag|_pMagic|_pBaseDex|_pDexterity|_pBaseVit|_pVitality|_pHPBase|_pMaxHPBase|_pHPPer|_pMana\\b|_pMaxMana\\b|_pManaBase|_pMaxManaBase|_pManaPer|_pIMinDam|_pIMaxDam|_pIBonusDam\\b|_pIBonusDamMod|_pIEnAc|_pIFMinDam|_pIFMaxDam|_pILMinDam|_pILMaxDam" Source test
```

Expected: no runtime `Player` field references remain. Remaining `PlayerPack` disk-save names are acceptable only in `Source/pack.h`, `Source/pack.cpp`, `Source/loadsave.cpp`, `test/fixtures/memory_map/hero.txt`, and save-format tests.

- [ ] **Step 2: Check formatting-sensitive lines**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors.

- [ ] **Step 3: Build all tests**

Run:

```powershell
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat'
$cmake = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
cmd /c "`"$vs`" -arch=x64 && `"$cmake`" --build build/x64-Debug --target tests --config Debug -j 4"
```

Expected: `tests` target builds successfully.

- [ ] **Step 4: Run all tests**

Run:

```powershell
ctest --test-dir build/x64-Debug -C Debug --output-on-failure
```

Expected: all tests pass.

- [ ] **Step 5: Commit cleanup if needed**

If Step 1 or Step 2 required cleanup, commit it:

```powershell
git add Source test
git commit -m "Clean up player attribute migration"
```
