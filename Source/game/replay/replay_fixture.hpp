#pragma once

/**
 * @file game/replay/replay_fixture.hpp
 *
 * Parser for the small, versioned replay fixture envelope used by tests.
 */

#include <cstdint>
#include <array>
#include <string>
#include <string_view>
#include <vector>

#include "game/replay/replay.hpp"

namespace devilution {

struct ReplayFixtureCommand {
	uint64_t clientSequence = 0;
	ReplayCommandOrder order;
	std::string kind;
	uint32_t storeId = 1;
	uint32_t storeSlot = 0;
	int32_t directionX = 0;
	int32_t directionY = 0;
	uint32_t targetEntityId = 0;
};

struct ReplayFixtureContentManifest {
	std::string id;
	std::string version;
	std::string sha256;
	std::vector<std::string> packs;
};

struct ReplayFixtureCheckpoint {
	uint64_t tick = 0;
	std::string stateSha256;
};

struct ReplayFixtureInitialState {
	std::string player;
	uint32_t gold = 0;
	uint32_t experience = 0;
	int32_t life = 0;
	int32_t mana = 0;
	int32_t manaMaximum = 0;
	int32_t characterClass = 0;
	uint8_t characterLevel = 1;
	int32_t positionX = 0;
	int32_t positionY = 0;
};

struct ReplayFixtureStoreState {
	int32_t activeStore = 1;
	int32_t premiumItemCount = 0;
	int32_t premiumItemLevel = 3;
	std::vector<uint32_t> premiumItemSeeds { 42 };
};

struct ReplayFixture {
	uint32_t formatVersion = 0;
	std::string fixtureId;
	std::string protocolSchemaVersion;
	uint32_t tickRateHz = 0;
	uint64_t rngSeed = 0;
	ReplayFixtureContentManifest contentManifest;
	ReplayFixtureInitialState initialState;
	ReplayFixtureStoreState storeState;
	std::vector<ReplayFixtureCommand> commands;
	std::vector<ReplayFixtureCheckpoint> checkpoints;
	std::string initialStateSha256;
};

/** Snapshot item fields used by the cross-language authoritative hash. */
struct ReplayFixtureItemState {
	uint32_t createInfo = 0;
	int32_t itemType = -1;
	int32_t positionX = 0;
	int32_t positionY = 0;
	bool deleted = false;
	bool identified = false;
	int32_t magical = 0;
	int32_t equipLocation = 0;
	int32_t itemClass = 0;
	int32_t value = 0;
	int32_t identifiedValue = 0;
	int32_t minDamage = 0;
	int32_t maxDamage = 0;
	int32_t armorClass = 0;
	uint32_t flags = 0;
	int32_t miscId = 0;
	int32_t spellId = 0;
	int32_t itemIndex = -1;
	int32_t charges = 0;
	int32_t maxCharges = 0;
	int32_t durability = 0;
	int32_t maxDurability = 0;
	int32_t plusDamage = 0;
	int32_t plusToHit = 0;
	int32_t plusArmorClass = 0;
	int32_t plusStrength = 0;
	int32_t plusMagic = 0;
	int32_t plusDexterity = 0;
	int32_t plusVitality = 0;
	int32_t plusFireResistance = 0;
	int32_t plusLightningResistance = 0;
	int32_t plusMagicResistance = 0;
	int32_t plusMana = 0;
	int32_t plusHitPoints = 0;
	int32_t plusDamageModifier = 0;
	int32_t plusGetHit = 0;
	int32_t plusLight = 0;
	int32_t spellLevelAdd = 0;
	int32_t uniqueId = 0;
	int32_t fireMinDamage = 0;
	int32_t fireMaxDamage = 0;
	int32_t lightningMinDamage = 0;
	int32_t lightningMaxDamage = 0;
	int32_t plusEnemyArmorClass = 0;
	int32_t prefixPower = -1;
	int32_t suffixPower = -1;
	int32_t valueAdd1 = 0;
	int32_t valueMultiply1 = 0;
	int32_t valueAdd2 = 0;
	int32_t valueMultiply2 = 0;
	int32_t minimumStrength = 0;
	int32_t minimumMagic = 0;
	int32_t minimumDexterity = 0;
	bool statFlag = false;
	int32_t hellfireDamageArmorFlags = 0;
	uint32_t buff = 0;
	uint32_t inventoryWidth = 1;
	uint32_t inventoryHeight = 1;
};

struct ReplayFixtureInventoryItem {
	uint32_t storeId = 0;
	uint32_t storeSlot = 0;
	uint32_t itemSeed = 0;
	uint32_t price = 0;
	uint64_t purchasedAtTick = 0;
	ReplayFixtureItemState state;
};

struct ReplayFixtureSlottedItem {
	uint32_t slot = 0;
	uint32_t itemSeed = 0;
	ReplayFixtureItemState state;
};

struct ReplayFixtureStoreItem {
	uint32_t storeSlot = 0;
	uint32_t itemSeed = 0;
	uint32_t price = 0;
	ReplayFixtureItemState state;
};

/** Minimal protocol-shaped state used by native replay transition tests. */
struct ReplayFixtureExecutionState {
	uint32_t entityId = 1;
	int32_t positionX = 0;
	int32_t positionY = 0;
	int32_t life = 0;
	int32_t mana = 0;
	int32_t manaMaximum = 0;
	int32_t lifeMaximum = 0;
	uint32_t characterLevel = 1;
	uint32_t levelId = 0;
	struct StatusEffect {
		uint32_t effectId = 0;
		uint32_t remainingTicks = 0;
		int32_t magnitude = 0;
	};
	std::vector<StatusEffect> statusEffects;
	uint32_t gold = 0;
	uint32_t experience = 0;
	uint32_t activeStoreId = 0;
	std::array<int32_t, 8> attributes {};
	std::vector<ReplayFixtureInventoryItem> inventory;
	std::vector<ReplayFixtureSlottedItem> equipment;
	std::vector<ReplayFixtureSlottedItem> belt;
	std::vector<int32_t> inventoryGrid;
	std::vector<ReplayFixtureStoreItem> activeStoreItems;
	uint32_t combatTargetEntityId = 0;
	int32_t combatTargetPositionX = 0;
	int32_t combatTargetPositionY = 0;
	int32_t combatTargetHitPoints = 0;
};

struct ReplayFixtureTransitionResult {
	uint64_t tick = 0;
	std::string kind;
	bool accepted = false;
	std::string stateSha256;
};

struct ReplayFixtureExecutionResult {
	std::vector<ReplayFixtureTransitionResult> transitions;
};

/** Hashes the protocol-shaped state using the C# SnapshotStateHasher order. */
[[nodiscard]] std::string ComputeReplayFixtureStateHash(const ReplayFixtureExecutionState &state);

/** Applies supported fixture commands and validates every command checkpoint. */
[[nodiscard]] bool ExecuteReplayFixture(
	const ReplayFixture &fixture,
	ReplayFixtureExecutionState &state,
	ReplayFixtureExecutionResult &result,
	std::string &error);

/**
 * Parses the replay fixture fields needed by the baseline tests.
 *
 * The parser intentionally supports only the fixture envelope. Unknown fields
 * are skipped recursively so fixtures can carry metadata without adding a
 * general-purpose JSON dependency to the engine.
 */
[[nodiscard]] bool ParseReplayFixture(std::string_view json, ReplayFixture &fixture, std::string &error);

} // namespace devilution
