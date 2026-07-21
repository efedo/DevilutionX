/**
 * @file game/replay/replay.cpp
 *
 * Deterministic replay primitives.
 */


#include "game/replay/replay.hpp"

#include <algorithm>

#include <picosha2.h>

namespace devilution {

bool IsReplayCommandOrderBefore(const ReplayCommandOrder &left, const ReplayCommandOrder &right) noexcept
{
	if (left.targetTick != right.targetTick)
		return left.targetTick < right.targetTick;
	return left.serverReceiptSequence < right.serverReceiptSequence;
}

std::vector<ReplayCommand> SortReplayCommands(std::vector<ReplayCommand> commands)
{
	std::stable_sort(commands.begin(), commands.end(), [](const ReplayCommand &left, const ReplayCommand &right) {
		return IsReplayCommandOrderBefore(left.order, right.order);
	});
	return commands;
}

void ReplayStateHasher::AppendBool(bool value)
{
	AppendUint8(value ? 1 : 0);
}

void ReplayStateHasher::AppendUint8(uint8_t value)
{
	bytes_.push_back(value);
}

void ReplayStateHasher::AppendInt32(int32_t value)
{
	AppendUint32(static_cast<uint32_t>(value));
}

void ReplayStateHasher::AppendUint32(uint32_t value)
{
	for (size_t byte = 0; byte < sizeof(value); ++byte)
		bytes_.push_back(static_cast<uint8_t>(value >> (byte * 8)));
}

void ReplayStateHasher::AppendUint64(uint64_t value)
{
	for (size_t byte = 0; byte < sizeof(value); ++byte)
		bytes_.push_back(static_cast<uint8_t>(value >> (byte * 8)));
}

void ReplayStateHasher::AppendString(std::string_view value)
{
	AppendUint64(value.size());
	bytes_.insert(bytes_.end(), value.begin(), value.end());
}

std::array<uint8_t, 32> ReplayStateHasher::Digest() const
{
	std::array<uint8_t, 32> digest;
	picosha2::hash256(bytes_.begin(), bytes_.end(), digest.begin(), digest.end());
	return digest;
}

std::string ReplayStateHasher::HexDigest() const
{
	const std::array<uint8_t, 32> digest = Digest();
	return picosha2::bytes_to_hex_string(digest.begin(), digest.end());
}

} // namespace devilution
