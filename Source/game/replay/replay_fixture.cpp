/**
 * @file game/replay/replay_fixture.cpp
 *
 * Parser for the small, versioned replay fixture envelope used by tests.
 */

#include "game/replay/replay_fixture.hpp"

#include <algorithm>
#include <charconv>
#include <cctype>
#include <cstdlib>
#include <limits>
#include <utility>

namespace devilution {
namespace {

void AppendItemState(ReplayStateHasher &hasher, const ReplayFixtureItemState &state)
{
	hasher.AppendUint32(state.createInfo);
	hasher.AppendInt32(state.itemType);
	hasher.AppendInt32(state.positionX);
	hasher.AppendInt32(state.positionY);
	hasher.AppendBool(state.deleted);
	hasher.AppendBool(state.identified);
	hasher.AppendInt32(state.magical);
	hasher.AppendInt32(state.equipLocation);
	hasher.AppendInt32(state.itemClass);
	hasher.AppendInt32(state.value);
	hasher.AppendInt32(state.identifiedValue);
	hasher.AppendInt32(state.minDamage);
	hasher.AppendInt32(state.maxDamage);
	hasher.AppendInt32(state.armorClass);
	hasher.AppendUint32(state.flags);
	hasher.AppendInt32(state.miscId);
	hasher.AppendInt32(state.spellId);
	hasher.AppendInt32(state.itemIndex);
	hasher.AppendInt32(state.charges);
	hasher.AppendInt32(state.maxCharges);
	hasher.AppendInt32(state.durability);
	hasher.AppendInt32(state.maxDurability);
	hasher.AppendInt32(state.plusDamage);
	hasher.AppendInt32(state.plusToHit);
	hasher.AppendInt32(state.plusArmorClass);
	hasher.AppendInt32(state.plusStrength);
	hasher.AppendInt32(state.plusMagic);
	hasher.AppendInt32(state.plusDexterity);
	hasher.AppendInt32(state.plusVitality);
	hasher.AppendInt32(state.plusFireResistance);
	hasher.AppendInt32(state.plusLightningResistance);
	hasher.AppendInt32(state.plusMagicResistance);
	hasher.AppendInt32(state.plusMana);
	hasher.AppendInt32(state.plusHitPoints);
	hasher.AppendInt32(state.plusDamageModifier);
	hasher.AppendInt32(state.plusGetHit);
	hasher.AppendInt32(state.plusLight);
	hasher.AppendInt32(state.spellLevelAdd);
	hasher.AppendInt32(state.uniqueId);
	hasher.AppendInt32(state.fireMinDamage);
	hasher.AppendInt32(state.fireMaxDamage);
	hasher.AppendInt32(state.lightningMinDamage);
	hasher.AppendInt32(state.lightningMaxDamage);
	hasher.AppendInt32(state.plusEnemyArmorClass);
	hasher.AppendInt32(state.prefixPower);
	hasher.AppendInt32(state.suffixPower);
	hasher.AppendInt32(state.valueAdd1);
	hasher.AppendInt32(state.valueMultiply1);
	hasher.AppendInt32(state.valueAdd2);
	hasher.AppendInt32(state.valueMultiply2);
	hasher.AppendInt32(state.minimumStrength);
	hasher.AppendInt32(state.minimumMagic);
	hasher.AppendInt32(state.minimumDexterity);
	hasher.AppendBool(state.statFlag);
	hasher.AppendInt32(state.hellfireDamageArmorFlags);
	hasher.AppendUint32(state.buff);
	hasher.AppendUint32(state.inventoryWidth == 0 ? 1 : state.inventoryWidth);
	hasher.AppendUint32(state.inventoryHeight == 0 ? 1 : state.inventoryHeight);
}

template <typename Item>
void AppendSlottedItems(ReplayStateHasher &hasher, std::vector<Item> items)
{
	std::sort(items.begin(), items.end(), [](const Item &left, const Item &right) {
		if (left.slot != right.slot)
			return left.slot < right.slot;
		return left.itemSeed < right.itemSeed;
	});
	hasher.AppendUint64(items.size());
	for (const auto &item : items) {
		hasher.AppendUint32(item.slot);
		hasher.AppendUint32(item.itemSeed);
		AppendItemState(hasher, item.state);
	}
}

void AppendCanonicalState(ReplayStateHasher &hasher, const ReplayFixtureExecutionState &state)
{
	hasher.AppendUint64(1);
	hasher.AppendUint32(state.entityId);
	hasher.AppendInt32(state.positionX);
	hasher.AppendInt32(state.positionY);
	hasher.AppendInt32(state.life);
	hasher.AppendInt32(state.mana);
	hasher.AppendInt32(state.manaMaximum);
	hasher.AppendInt32(state.lifeMaximum);
	hasher.AppendUint32(state.characterLevel);
	hasher.AppendUint32(state.levelId);
	std::vector<ReplayFixtureExecutionState::StatusEffect> statusEffects = state.statusEffects;
	std::sort(statusEffects.begin(), statusEffects.end(), [](const auto &left, const auto &right) {
		if (left.effectId != right.effectId)
			return left.effectId < right.effectId;
		if (left.remainingTicks != right.remainingTicks)
			return left.remainingTicks < right.remainingTicks;
		return left.magnitude < right.magnitude;
	});
	hasher.AppendUint64(statusEffects.size());
	for (const auto &effect : statusEffects) {
		hasher.AppendUint32(effect.effectId);
		hasher.AppendUint32(effect.remainingTicks);
		hasher.AppendInt32(effect.magnitude);
	}
	hasher.AppendUint32(state.gold);
	hasher.AppendUint32(state.experience);
	hasher.AppendUint32(state.activeStoreId);
	for (const int32_t attribute : state.attributes)
		hasher.AppendInt32(attribute);

	AppendSlottedItems(hasher, state.equipment);
	AppendSlottedItems(hasher, state.belt);

	std::vector<ReplayFixtureInventoryItem> inventory = state.inventory;
	std::sort(inventory.begin(), inventory.end(), [](const auto &left, const auto &right) {
		if (left.storeId != right.storeId)
			return left.storeId < right.storeId;
			if (left.storeSlot != right.storeSlot)
				return left.storeSlot < right.storeSlot;
		if (left.itemSeed != right.itemSeed)
			return left.itemSeed < right.itemSeed;
		if (left.price != right.price)
			return left.price < right.price;
		return left.purchasedAtTick < right.purchasedAtTick;
	});
	hasher.AppendUint64(inventory.size());
	for (const auto &item : inventory) {
		hasher.AppendUint32(item.storeId);
		hasher.AppendUint32(item.storeSlot);
		hasher.AppendUint32(item.itemSeed);
		hasher.AppendUint32(item.price);
		hasher.AppendUint64(item.purchasedAtTick);
		AppendItemState(hasher, item.state);
	}

	hasher.AppendUint64(state.inventoryGrid.size());
	for (const int32_t cell : state.inventoryGrid)
		hasher.AppendInt32(cell);

	hasher.AppendBool(state.activeStoreId != 0);
	if (state.activeStoreId != 0) {
		hasher.AppendUint32(state.activeStoreId);
		std::vector<ReplayFixtureStoreItem> items = state.activeStoreItems;
		std::sort(items.begin(), items.end(), [](const auto &left, const auto &right) {
			if (left.storeSlot != right.storeSlot)
				return left.storeSlot < right.storeSlot;
			if (left.itemSeed != right.itemSeed)
				return left.itemSeed < right.itemSeed;
			return left.price < right.price;
		});
		hasher.AppendUint64(items.size());
		for (const auto &item : items) {
			hasher.AppendUint32(item.storeSlot);
			hasher.AppendUint32(item.itemSeed);
			hasher.AppendUint32(item.price);
			AppendItemState(hasher, item.state);
		}
	}
}

bool IsSha256(std::string_view value)
{
	if (value.size() != 64)
		return false;
	return std::all_of(value.begin(), value.end(), [](char character) {
		return std::isdigit(static_cast<unsigned char>(character)) || (character >= 'a' && character <= 'f') || (character >= 'A' && character <= 'F');
	});
}

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
		if (key == "content_manifest")
			return ParseContentManifest();
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

	bool ParseContentManifest()
	{
		if (position_ >= input_.size())
			return Fail("expected content manifest");
		if (input_[position_] == '[')
			return ParseContentManifestPacks();
		if (input_[position_] != '{')
			return Fail("expected content manifest object or pack array");
		return ParseObject([this](std::string_view key) {
			if (key == "id")
				return ParseString(fixture_.contentManifest.id);
			if (key == "version")
				return ParseString(fixture_.contentManifest.version);
			if (key == "sha256")
				return ParseString(fixture_.contentManifest.sha256);
			return SkipValue();
		});
	}

	bool ParseContentManifestPacks()
	{
		if (!Consume('['))
			return Fail("expected content manifest pack array");
		fixture_.contentManifest.packs.clear();
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			std::string pack;
			if (!ParseString(pack))
				return false;
			fixture_.contentManifest.packs.push_back(std::move(pack));
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
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
			if (key == "mana_maximum")
				return ParseSigned(fixture_.initialState.manaMaximum);
			if (key == "character_class")
				return ParseSigned(fixture_.initialState.characterClass);
			if (key == "character_level")
				return ParseUnsigned(fixture_.initialState.characterLevel);
			if (key == "position_x")
				return ParseSigned(fixture_.initialState.positionX);
			if (key == "position_y")
				return ParseSigned(fixture_.initialState.positionY);
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
		if (key == "payload")
			return ParseCommandPayload(command);
		return SkipValue();
	}

	bool ParseCommandPayload(ReplayFixtureCommand &command)
	{
		return ParseObject([this, &command](std::string_view key) {
			if (key == "store_id")
				return ParseUnsigned(command.storeId);
			if (key == "store_slot" || key == "item_index" || key == "inventory_index")
				return ParseUnsigned(command.storeSlot);
			if (key == "direction_x")
				return ParseSigned(command.directionX);
			if (key == "direction_y")
				return ParseSigned(command.directionY);
			if (key == "target_entity_id")
				return ParseUnsigned(command.targetEntityId);
			return SkipValue();
		});
	}

	bool ParseCheckpoints()
	{
		if (!Consume('['))
			return Fail("expected checkpoints array");
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			ReplayFixtureCheckpoint checkpoint;
			if (!ParseObject([this, &checkpoint](std::string_view key) {
				if (key == "tick")
					return ParseUnsigned(checkpoint.tick);
				if (key == "state_sha256")
					return ParseString(checkpoint.stateSha256);
				return SkipValue();
			}))
				return false;
			if (checkpoint.tick == 0)
				fixture_.initialStateSha256 = checkpoint.stateSha256;
			fixture_.checkpoints.push_back(std::move(checkpoint));
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

std::string ComputeReplayFixtureStateHash(const ReplayFixtureExecutionState &state)
{
	ReplayStateHasher hasher;
	AppendCanonicalState(hasher, state);
	return hasher.HexDigest();
}

bool ExecuteReplayFixture(
	const ReplayFixture &fixture,
	ReplayFixtureExecutionState &state,
	ReplayFixtureExecutionResult &result,
	std::string &error)
{
	result = {};
	error.clear();
	std::vector<ReplayFixtureCommand> commands = fixture.commands;
	std::stable_sort(commands.begin(), commands.end(), [](const auto &left, const auto &right) {
		return IsReplayCommandOrderBefore(left.order, right.order);
	});

	for (const ReplayFixtureCommand &command : commands) {
		bool accepted = false;
		if (command.kind == "OpenStore") {
			state.activeStoreId = command.storeId;
			if (command.storeId == 10)
				state.activeStoreItems.clear();
			accepted = true;
		} else if (command.kind == "BuyItem") {
			if (state.activeStoreId == command.storeId) {
				auto item = std::find_if(state.activeStoreItems.begin(), state.activeStoreItems.end(), [&](const auto &candidate) {
					return candidate.storeSlot == command.storeSlot;
				});
				if (item != state.activeStoreItems.end() && state.gold >= item->price) {
					state.gold -= item->price;
					state.inventory.push_back({ command.storeId, item->storeSlot, item->itemSeed, item->price, command.order.targetTick, item->state });
					state.activeStoreItems.erase(item);
					accepted = true;
				}
			}
		} else if (command.kind == "SellItem") {
			if (state.activeStoreId != 0 && command.storeSlot < state.inventory.size()) {
				auto item = state.inventory.begin() + static_cast<std::ptrdiff_t>(command.storeSlot);
				const int value = item->state.magical != 0 && item->state.identified ? item->state.identifiedValue : item->state.value;
				state.gold += static_cast<uint32_t>(std::max(value / 4, 1));
				state.inventory.erase(item);
				accepted = true;
			}
		} else if (command.kind == "RefillMana") {
			if (state.activeStoreId == 10 && state.mana < state.manaMaximum && state.gold >= 10) {
				state.gold -= 10;
				state.mana = state.manaMaximum;
				accepted = true;
			}
		} else if (command.kind == "Move") {
			const int32_t targetX = state.positionX + command.directionX;
			const int32_t targetY = state.positionY + command.directionY;
			if ((command.directionX != 0 || command.directionY != 0) && targetX >= 0 && targetX < 40 && targetY >= 0 && targetY < 40) {
				state.positionX = targetX;
				state.positionY = targetY;
				accepted = true;
			}
		} else if (command.kind == "Attack") {
			const int distance = std::abs(state.positionX - state.combatTargetPositionX) + std::abs(state.positionY - state.combatTargetPositionY);
			if (command.targetEntityId == state.combatTargetEntityId && state.combatTargetHitPoints > 0 && distance <= 1) {
				state.combatTargetHitPoints -= 8;
				if (state.combatTargetHitPoints <= 0)
					state.experience += 100;
				accepted = true;
			}
		} else {
			error = "Unsupported replay command kind: " + command.kind;
			return false;
		}

		const std::string hash = ComputeReplayFixtureStateHash(state);
		result.transitions.push_back({ command.order.targetTick, command.kind, accepted, hash });
		if (!fixture.contentManifest.id.empty()) {
			auto checkpoint = std::find_if(fixture.checkpoints.begin(), fixture.checkpoints.end(), [&](const auto &candidate) {
				return candidate.tick == command.order.targetTick;
			});
			if (checkpoint == fixture.checkpoints.end()) {
				error = "Replay fixture is missing a checkpoint at command tick " + std::to_string(command.order.targetTick);
				return false;
			}
			if (IsSha256(checkpoint->stateSha256) && checkpoint->stateSha256 != hash) {
				error = "Replay checkpoint hash mismatch at tick " + std::to_string(checkpoint->tick) + ": actual " + hash;
				return false;
			}
		}
	}
	return true;
}

} // namespace devilution
