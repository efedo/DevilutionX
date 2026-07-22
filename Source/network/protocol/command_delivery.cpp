/**
 * @file network/protocol/command_delivery.cpp
 *
 * Transport-independent client command delivery state.
 */

#include "network/protocol/command_delivery.hpp"

#include <algorithm>
#include <limits>

namespace devilution {

CommandDeliveryTracker::CommandDeliveryTracker(uint64_t initialRttMs)
    : smoothedRttMs_(std::max(initialRttMs, uint64_t { 1 }))
    , rttVarianceMs_(std::max(smoothedRttMs_ / 2, uint64_t { 1 }))
{
}

uint64_t CommandDeliveryTracker::RegisterCommand()
{
	while (FindPending(nextClientSequence_) != nullptr) {
		++nextClientSequence_;
		if (nextClientSequence_ == 0)
			nextClientSequence_ = 1;
	}

	const uint64_t sequence = nextClientSequence_++;
	if (nextClientSequence_ == 0)
		nextClientSequence_ = 1;
	pending_.push_back({ .clientSequence = sequence });
	return sequence;
}

bool CommandDeliveryTracker::MarkSent(uint64_t clientSequence, uint64_t nowMs)
{
	PendingCommand *pending = FindPending(clientSequence);
	if (pending == nullptr)
		return false;
	pending->lastSentAtMs = nowMs;
	++pending->attemptCount;
	pending->sent = true;
	return true;
}

std::vector<uint64_t> CommandDeliveryTracker::PrepareResubmissions(uint64_t nowMs)
{
	std::vector<uint64_t> resubmissions;
	for (PendingCommand &pending : pending_) {
		if (!pending.sent || nowMs < pending.lastSentAtMs || nowMs - pending.lastSentAtMs < RetryTimeoutMs())
			continue;
		pending.lastSentAtMs = nowMs;
		++pending.attemptCount;
		resubmissions.push_back(pending.clientSequence);
	}
	return resubmissions;
}

bool CommandDeliveryTracker::ApplyAcknowledgement(const CommandAcknowledgement &ack, uint64_t nowMs)
{
	const auto pending = std::find_if(pending_.begin(), pending_.end(), [&ack](const PendingCommand &command) {
		return command.clientSequence == ack.clientSequence;
	});
	if (pending == pending_.end())
		return false;

	if (pending->sent && nowMs >= pending->lastSentAtMs)
		ObserveRoundTrip(nowMs - pending->lastSentAtMs);
	pending_.erase(pending);
	return true;
}

uint64_t CommandDeliveryTracker::RetryTimeoutMs() const noexcept
{
	const uint64_t varianceWindow = rttVarianceMs_ > (std::numeric_limits<uint64_t>::max() / 4)
	    ? std::numeric_limits<uint64_t>::max()
	    : rttVarianceMs_ * 4;
	const uint64_t timeout = smoothedRttMs_ > std::numeric_limits<uint64_t>::max() - varianceWindow
	    ? std::numeric_limits<uint64_t>::max()
	    : smoothedRttMs_ + varianceWindow;
	return std::clamp(timeout, MinRetryTimeoutMs, MaxRetryTimeoutMs);
}

CommandDeliveryTracker::PendingCommand *CommandDeliveryTracker::FindPending(uint64_t clientSequence) noexcept
{
	for (PendingCommand &pending : pending_) {
		if (pending.clientSequence == clientSequence)
			return &pending;
	}
	return nullptr;
}

void CommandDeliveryTracker::ObserveRoundTrip(uint64_t sampleMs) noexcept
{
	const uint64_t delta = sampleMs > smoothedRttMs_ ? sampleMs - smoothedRttMs_ : smoothedRttMs_ - sampleMs;
	const uint64_t weightedVariance = rttVarianceMs_ > std::numeric_limits<uint64_t>::max() / 3
	    ? std::numeric_limits<uint64_t>::max()
	    : rttVarianceMs_ * 3;
	rttVarianceMs_ = weightedVariance > std::numeric_limits<uint64_t>::max() - delta
	    ? std::numeric_limits<uint64_t>::max()
	    : (weightedVariance + delta) / 4;
	const uint64_t weightedRtt = smoothedRttMs_ > std::numeric_limits<uint64_t>::max() / 7
	    ? std::numeric_limits<uint64_t>::max()
	    : smoothedRttMs_ * 7;
	smoothedRttMs_ = weightedRtt > std::numeric_limits<uint64_t>::max() - sampleMs
	    ? std::numeric_limits<uint64_t>::max()
	    : (weightedRtt + sampleMs) / 8;
}

} // namespace devilution
