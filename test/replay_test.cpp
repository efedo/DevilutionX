#include <cstdint>

#include <gtest/gtest.h>

#include "game/replay/replay.hpp"

namespace devilution {
namespace {

TEST(ReplayStateHasher, EmptyStateUsesSha256)
{
	ReplayStateHasher hasher;

	EXPECT_EQ(hasher.HexDigest(), "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
}

TEST(ReplayStateHasher, CanonicalFieldsAreStable)
{
	ReplayStateHasher first;
	first.AppendBool(true);
	first.AppendUint8(7);
	first.AppendInt32(-42);
	first.AppendUint32(9001);
	first.AppendUint64(123456789);
	first.AppendString("griswold");

	ReplayStateHasher second;
	second.AppendBool(true);
	second.AppendUint8(7);
	second.AppendInt32(-42);
	second.AppendUint32(9001);
	second.AppendUint64(123456789);
	second.AppendString("griswold");

	EXPECT_EQ(first.Digest(), second.Digest());

	second.AppendString("different");
	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayStateHasher, StringsAreLengthPrefixed)
{
	ReplayStateHasher first;
	first.AppendString("ab");

	ReplayStateHasher second;
	second.AppendString("a");
	second.AppendString("b");

	EXPECT_NE(first.Digest(), second.Digest());
}

TEST(ReplayCommands, SortsByTargetTickThenServerReceiptSequence)
{
	const std::vector<ReplayCommand> sorted = SortReplayCommands({
	    { .clientSequence = 3, .order = { .targetTick = 2, .serverReceiptSequence = 1 } },
	    { .clientSequence = 1, .order = { .targetTick = 1, .serverReceiptSequence = 5 } },
	    { .clientSequence = 2, .order = { .targetTick = 1, .serverReceiptSequence = 2 } },
	});

	ASSERT_EQ(sorted.size(), 3U);
	EXPECT_EQ(sorted[0].clientSequence, 2U);
	EXPECT_EQ(sorted[1].clientSequence, 1U);
	EXPECT_EQ(sorted[2].clientSequence, 3U);
}

TEST(ReplayCommands, PreservesInputOrderForDuplicateReceiptSequences)
{
	const std::vector<ReplayCommand> sorted = SortReplayCommands({
	    { .clientSequence = 1, .order = { .targetTick = 1, .serverReceiptSequence = 4 } },
	    { .clientSequence = 2, .order = { .targetTick = 1, .serverReceiptSequence = 4 } },
	});

	ASSERT_EQ(sorted.size(), 2U);
	EXPECT_EQ(sorted[0].clientSequence, 1U);
	EXPECT_EQ(sorted[1].clientSequence, 2U);
}

} // namespace
} // namespace devilution
