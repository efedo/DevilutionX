#pragma once

/**
 * @file game/replay/replay.hpp
 *
 * Deterministic replay primitives.
 */


#include <array>
#include <cstddef>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace devilution {

struct ReplayCommandOrder {
	uint64_t targetTick = 0;
	uint64_t serverReceiptSequence = 0;
};

[[nodiscard]] bool IsReplayCommandOrderBefore(const ReplayCommandOrder &left, const ReplayCommandOrder &right) noexcept;

struct ReplayCommand {
	uint64_t clientSequence = 0;
	ReplayCommandOrder order;
};

/**
 * Returns commands in authoritative processing order.
 *
 * The server receipt sequence is part of the replay record so the same order
 * can be reproduced independently of network timing.
 */
[[nodiscard]] std::vector<ReplayCommand> SortReplayCommands(std::vector<ReplayCommand> commands);

/**
 * Builds a canonical byte stream and returns its SHA-256 digest.
 *
 * Integers are encoded as fixed-width little-endian values. Strings are
 * prefixed with a uint64 length. Callers are responsible for appending fields
 * in the schema-defined order.
 */
class ReplayStateHasher {
public:
	void AppendBool(bool value);
	void AppendUint8(uint8_t value);
	void AppendInt32(int32_t value);
	void AppendUint32(uint32_t value);
	void AppendUint64(uint64_t value);
	void AppendString(std::string_view value);

	[[nodiscard]] std::array<uint8_t, 32> Digest() const;
	[[nodiscard]] std::string HexDigest() const;

private:
	std::vector<uint8_t> bytes_;
};

} // namespace devilution
