/**
 * @file game/replay/replay_fixture.cpp
 *
 * Parser for the small, versioned replay fixture envelope used by tests.
 */

#include "game/replay/replay_fixture.hpp"

#include <charconv>
#include <cctype>
#include <limits>
#include <utility>

namespace devilution {
namespace {

class JsonReader {
public:
	JsonReader(std::string_view input, ReplayFixture &fixture, std::string &error)
	    : input_(input)
	    , fixture_(fixture)
	    , error_(error)
	{
	}

	[[nodiscard]] bool Parse()
	{
		SkipWhitespace();
		if (!ParseObject([this](std::string_view key) { return ParseFixtureField(key); }))
			return false;
		SkipWhitespace();
		return position_ == input_.size() || Fail("unexpected trailing data");
	}

private:
	template <typename FieldParser>
	bool ParseObject(FieldParser &&parseField)
	{
		if (!Consume('{'))
			return Fail("expected object");
		SkipWhitespace();
		if (Consume('}'))
			return true;
		while (true) {
			std::string key;
			if (!ParseString(key))
				return false;
			SkipWhitespace();
			if (!Consume(':'))
				return Fail("expected colon");
			SkipWhitespace();
			if (!parseField(key))
				return false;
			SkipWhitespace();
			if (Consume('}'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing brace");
			SkipWhitespace();
		}
	}

	bool ParseFixtureField(std::string_view key)
	{
		if (key == "format_version")
			return ParseUnsigned(fixture_.formatVersion);
		if (key == "fixture_id")
			return ParseString(fixture_.fixtureId);
		if (key == "protocol_schema_version")
			return ParseString(fixture_.protocolSchemaVersion);
		if (key == "tick_rate_hz")
			return ParseUnsigned(fixture_.tickRateHz);
		if (key == "rng_seed")
			return ParseUnsigned(fixture_.rngSeed);
		if (key == "initial_state")
			return ParseInitialState();
		if (key == "legacy_store_state")
			return ParseStoreState();
		if (key == "commands")
			return ParseCommands();
		if (key == "checkpoints")
			return ParseCheckpoints();
		return SkipValue();
	}

	bool ParseInitialState()
	{
		return ParseObject([this](std::string_view key) {
			if (key == "player")
				return ParseString(fixture_.initialState.player);
			if (key == "gold")
				return ParseUnsigned(fixture_.initialState.gold);
			if (key == "experience")
				return ParseUnsigned(fixture_.initialState.experience);
			if (key == "life")
				return ParseSigned(fixture_.initialState.life);
			if (key == "mana")
				return ParseSigned(fixture_.initialState.mana);
			if (key == "character_class")
				return ParseSigned(fixture_.initialState.characterClass);
			if (key == "character_level")
				return ParseUnsigned(fixture_.initialState.characterLevel);
			return SkipValue();
		});
	}

	bool ParseStoreState()
	{
		return ParseObject([this](std::string_view key) {
			if (key == "active_store")
				return ParseSigned(fixture_.storeState.activeStore);
			if (key == "premium_item_count")
				return ParseSigned(fixture_.storeState.premiumItemCount);
			if (key == "premium_item_level")
				return ParseSigned(fixture_.storeState.premiumItemLevel);
			if (key == "premium_item_seeds")
				return ParseSeedArray();
			return SkipValue();
		});
	}

	bool ParseSeedArray()
	{
		if (!Consume('['))
			return Fail("expected premium item seed array");
		fixture_.storeState.premiumItemSeeds.clear();
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			uint32_t seed = 0;
			if (!ParseUnsigned(seed))
				return false;
			fixture_.storeState.premiumItemSeeds.push_back(seed);
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseCommands()
	{
		if (!Consume('['))
			return Fail("expected commands array");
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			ReplayFixtureCommand command;
			if (!ParseObject([this, &command](std::string_view key) { return ParseCommandField(key, command); }))
				return false;
			fixture_.commands.push_back(std::move(command));
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseCommandField(std::string_view key, ReplayFixtureCommand &command)
	{
		if (key == "client_sequence")
			return ParseUnsigned(command.clientSequence);
		if (key == "target_tick")
			return ParseUnsigned(command.order.targetTick);
		if (key == "server_receipt_sequence")
			return ParseUnsigned(command.order.serverReceiptSequence);
		if (key == "kind")
			return ParseString(command.kind);
		return SkipValue();
	}

	bool ParseCheckpoints()
	{
		if (!Consume('['))
			return Fail("expected checkpoints array");
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			uint64_t tick = 0;
			std::string stateHash;
			if (!ParseObject([this, &tick, &stateHash](std::string_view key) {
				if (key == "tick")
					return ParseUnsigned(tick);
				if (key == "state_sha256")
					return ParseString(stateHash);
				return SkipValue();
			}))
				return false;
			if (tick == 0)
				fixture_.initialStateSha256 = std::move(stateHash);
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	template <typename Integer>
	bool ParseUnsigned(Integer &value)
	{
		uint64_t parsed = 0;
		bool negative = false;
		if (!ParseUnsigned(parsed, negative))
			return false;
		if (negative || parsed > std::numeric_limits<Integer>::max())
			return Fail("unsigned integer out of range");
		value = static_cast<Integer>(parsed);
		return true;
	}

	template <typename Integer>
	bool ParseSigned(Integer &value)
	{
		const size_t start = position_;
		bool negative = false;
		uint64_t magnitude = 0;
		if (!ParseUnsigned(magnitude, negative))
			return false;
		if (negative) {
			if (magnitude > static_cast<uint64_t>(std::numeric_limits<Integer>::max()) + 1)
				return Fail("signed integer out of range");
			value = static_cast<Integer>(-static_cast<int64_t>(magnitude));
		} else {
			if (magnitude > static_cast<uint64_t>(std::numeric_limits<Integer>::max()))
				return Fail("signed integer out of range");
			value = static_cast<Integer>(magnitude);
		}
		return position_ != start || Fail("expected signed integer");
	}

	bool ParseUnsigned(uint64_t &value, bool &negative)
	{
		const size_t start = position_;
		if (position_ < input_.size() && input_[position_] == '-') {
			negative = true;
			++position_;
		} else {
			negative = false;
		}
		const size_t digits = position_;
		while (position_ < input_.size() && std::isdigit(static_cast<unsigned char>(input_[position_])))
			++position_;
		if (digits == position_) {
			position_ = start;
			return Fail("expected unsigned integer");
		}
		const auto result = std::from_chars(input_.data() + digits, input_.data() + position_, value);
		if (result.ec != std::errc())
			return Fail("invalid unsigned integer");
		return true;
	}

	bool ParseString(std::string &value)
	{
		if (!Consume('"'))
			return Fail("expected string");
		value.clear();
		while (position_ < input_.size()) {
			const char character = input_[position_++];
			if (character == '"')
				return true;
			if (character == '\\') {
				if (position_ >= input_.size())
					return Fail("unterminated escape");
				const char escaped = input_[position_++];
				switch (escaped) {
				case '"': value.push_back('"'); break;
				case '\\': value.push_back('\\'); break;
				case '/': value.push_back('/'); break;
				case 'b': value.push_back('\b'); break;
				case 'f': value.push_back('\f'); break;
				case 'n': value.push_back('\n'); break;
				case 'r': value.push_back('\r'); break;
				case 't': value.push_back('\t'); break;
				default: return Fail("unsupported string escape");
				}
			} else {
				if (static_cast<unsigned char>(character) < 0x20)
					return Fail("control character in string");
				value.push_back(character);
			}
		}
		return Fail("unterminated string");
	}

	bool SkipValue()
	{
		if (position_ >= input_.size())
			return Fail("expected value");
		if (input_[position_] == '"') {
			std::string ignored;
			return ParseString(ignored);
		}
		if (input_[position_] == '{')
			return ParseObject([this](std::string_view) { return SkipValue(); });
		if (input_[position_] == '[')
			return ParseArray();
		const size_t start = position_;
		while (position_ < input_.size() && input_[position_] != ',' && input_[position_] != '}' && input_[position_] != ']')
			++position_;
		while (position_ > start && std::isspace(static_cast<unsigned char>(input_[position_ - 1])))
			--position_;
		return position_ > start || Fail("expected value");
	}

	bool ParseArray()
	{
		if (!Consume('['))
			return Fail("expected array");
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			if (!SkipValue())
				return false;
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	void SkipWhitespace()
	{
		while (position_ < input_.size() && std::isspace(static_cast<unsigned char>(input_[position_])))
			++position_;
	}

	bool Consume(char expected)
	{
		if (position_ < input_.size() && input_[position_] == expected) {
			++position_;
			return true;
		}
		return false;
	}

	bool Fail(std::string_view message)
	{
		if (error_.empty())
			error_ = std::string(message) + " at offset " + std::to_string(position_);
		return false;
	}

	std::string_view input_;
	ReplayFixture &fixture_;
	std::string &error_;
	size_t position_ = 0;
};

} // namespace

bool ParseReplayFixture(std::string_view json, ReplayFixture &fixture, std::string &error)
{
	fixture = {};
	error.clear();
	return JsonReader(json, fixture, error).Parse();
}

} // namespace devilution
