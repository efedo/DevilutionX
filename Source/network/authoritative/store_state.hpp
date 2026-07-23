#pragma once

/**
 * @file network/authoritative/store_state.hpp
 *
 * Protocol-free routing and lifecycle state for authoritative stores.
 */

#include <cstddef>
#include <cstdint>
#include <map>
#include <optional>
#include <utility>

#include "network/authoritative/store_snapshot.hpp"

namespace devilution::authoritative {

enum class StoreIntentRoute {
	Local,
	Blocked,
	Pending,
};

enum class AuthoritativeStorePhase {
	Disabled,
	Disconnected,
	AwaitingSnapshot,
	Ready,
};

enum class PurchaseResolution {
	Accepted,
	Rejected,
};

/**
 * Owns protocol-independent store routing state.
 *
 * Disabled routing always returns Local, preserving the existing client path.
 * Enabled routing never falls back to local mutations.
 */
class AuthoritativeStoreState {
public:
	void SetEnabled(bool enabled) noexcept;
	void SetConnected(bool connected) noexcept;

	[[nodiscard]] AuthoritativeStorePhase Phase() const noexcept;
	[[nodiscard]] StoreIntentRoute OpenStore(uint32_t storeId);
	[[nodiscard]] StoreIntentRoute Purchase(uint32_t storeId, uint32_t storeSlot);

	/** Applies a validated snapshot and reconciles accepted purchases. */
	[[nodiscard]] bool ApplySnapshot(ProjectedStoreSnapshot snapshot);

	/** Applies a server result to an existing pending purchase. */
	[[nodiscard]] bool ResolvePurchase(uint32_t storeId, uint32_t storeSlot, PurchaseResolution resolution);

	[[nodiscard]] const ProjectedStoreItem *FindItem(uint32_t storeId, uint32_t storeSlot) const noexcept;
	[[nodiscard]] bool IsPurchasePending(uint32_t storeId, uint32_t storeSlot) const noexcept;
	[[nodiscard]] std::size_t PendingPurchaseCount() const noexcept { return pendingPurchases_.size(); }
	[[nodiscard]] std::optional<uint32_t> PendingOpenStoreId() const noexcept { return pendingOpenStoreId_; }

private:
	enum class PendingPurchaseState {
		AwaitingResult,
		Accepted,
	};

	using PurchaseKey = std::pair<uint32_t, uint32_t>;

	bool enabled_ = false;
	bool connected_ = false;
	bool ready_ = false;
	std::optional<uint32_t> pendingOpenStoreId_;
	std::optional<ProjectedStoreSnapshot> snapshot_;
	std::map<uint32_t, std::size_t> itemIndexBySlot_;
	std::map<PurchaseKey, PendingPurchaseState> pendingPurchases_;
};

} // namespace devilution::authoritative
