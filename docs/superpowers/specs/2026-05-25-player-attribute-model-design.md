# Player Attribute Model Design

## Goal

Group the raw `Player` combat/stat fields into small value types that make the model easier to read, safer to update, and reusable by `Monster` later. The first pass should improve structure without changing gameplay behavior, save compatibility, or network packet layout.

## Scope

This design covers primary attributes, life/mana resources, and damage ranges/bonuses in `Player`. It does not move the same model into `Monster` yet, but the new types should live in a shared location and avoid player-only assumptions.

The implementation should be split into commits that compile independently:

1. Add shared attribute value types.
2. Convert Strength, Magic, Dexterity, and Vitality.
3. Convert life and mana.
4. Convert damage ranges and damage-related bonuses.
5. Rename serialization and packet concepts where useful, while preserving binary compatibility.

## Attribute Types

Use plain value structs. Do not use virtual functions or a deep inheritance hierarchy; these are small data containers that will be copied, serialized, and touched in hot code.

Proposed shared types:

```cpp
struct ModifiableAttribute {
	int base = 0;
	int current = 0;
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
```

`VitalResource` intentionally differs from `ModifiableAttribute`. Life and mana currently carry both effective maximum and base maximum values, and folding them into the same shape as Strength/Magic would hide important invariants.

## Player Layout

`Player` should eventually replace the raw stat clusters with grouped fields:

```cpp
struct PrimaryAttributes {
	ModifiableAttribute strength;
	ModifiableAttribute magic;
	ModifiableAttribute dexterity;
	ModifiableAttribute vitality;
};

PrimaryAttributes attributes;
VitalResource life;
VitalResource mana;
DamageBonuses damageBonuses;
```

Existing methods such as `GetBaseAttributeValue`, `GetCurrentAttributeValue`, `modifyStrength`, `RestoreFullLife`, and `CalculateArmorPierce` should remain as the public interface during the migration. Call sites should move toward clearer accessors over time instead of reaching directly into nested fields everywhere at once.

## Serialization And Packets

Serialized and packed field names may be renamed when doing so makes the code clearer, but the actual saved data and packet layout must remain compatible. Renames should be accompanied by small adapter logic or comments when the new name no longer matches an old file-format field name.

Rules:

- Do not reorder binary fields unless the format explicitly allows it.
- Keep old save files loadable.
- Keep network messages compatible with the current protocol.
- Prefer explicit mapping code over clever casts between packed structs and runtime structs.
- Add or update tests for save/load and packet round trips around each migrated cluster.

## Migration Details

Primary attributes should move first because their model is straightforward:

- `_pBaseStr` and `_pStrength` become `attributes.strength.base` and `attributes.strength.current`.
- `_pBaseMag` and `_pMagic` become `attributes.magic.base` and `attributes.magic.current`.
- `_pBaseDex` and `_pDexterity` become `attributes.dexterity.base` and `attributes.dexterity.current`.
- `_pBaseVit` and `_pVitality` become `attributes.vitality.base` and `attributes.vitality.current`.

Life and mana should move second:

- `hitPoints`, `maxHitPoints`, `_pHPBase`, `_pMaxHPBase`, and `_pHPPer` become `life.current`, `life.maximum`, `life.base`, `life.maximumBase`, and `life.percentage`.
- `_pMana`, `_pMaxMana`, `_pManaBase`, `_pMaxManaBase`, and `_pManaPer` become the equivalent fields in `mana`.

Damage should move third:

- `_pIMinDam` and `_pIMaxDam` become `damageBonuses.physical`.
- `_pIFMinDam` and `_pIFMaxDam` become `damageBonuses.fire`.
- `_pILMinDam` and `_pILMaxDam` become `damageBonuses.lightning`.
- `_pIBonusDam`, `_pIBonusDamMod`, and `_pIEnAc` become named bonus fields.

## Testing

Each implementation commit should compile on its own. The narrowest useful checks are:

- `player_test` after primary attribute and life/mana changes.
- `pack_test` after packed player fields change.
- Save/load tests or an equivalent focused test around old save compatibility after life/mana and serialization changes.
- Combat or item tests after damage fields change.

Run the full test suite after the final commit in the series.

## Open Risks

The main risk is confusing fixed-point gameplay values with displayed whole-number values. Life and mana are stored in fixed-point units in many paths, so helpers should keep that distinction visible.

The second risk is partial migration producing a mix of old and new direct field access. That is acceptable inside a single commit only if the result compiles and tests pass, but each commit should end in a coherent state.
