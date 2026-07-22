#pragma once

/**
 * @file game/replay/replay_fixture.hpp
 *
 * Parser for the small, versioned replay fixture envelope used by tests.
 */

#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

#include "game/replay/replay.hpp"

namespace devilution {

struct ReplayFixtureCommand {
	uint64_t clientSequence = 0;
	ReplayCommandOrder order;
	std::string kind;
};

struct ReplayFixtureInitialState {
	std::string player;
	uint32_t gold = 0;
	uint32_t experience = 0;
	int32_t life = 0;
	int32_t mana = 0;
	int32_t characterClass = 0;
	uint8_t characterLevel = 1;
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
	ReplayFixtureInitialState initialState;
	ReplayFixtureStoreState storeState;
	std::vector<ReplayFixtureCommand> commands;
	std::string initialStateSha256;
};

/**
 * Parses the replay fixture fields needed by the baseline tests.
 *
 * The parser intentionally supports only the fixture envelope. Unknown fields
 * are skipped recursively so fixtures can carry metadata without adding a
 * general-purpose JSON dependency to the engine.
 */
[[nodiscard]] bool ParseReplayFixture(std::string_view json, ReplayFixture &fixture, std::string &error);

} // namespace devilution
