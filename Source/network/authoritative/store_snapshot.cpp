/**
 * @file network/authoritative/store_snapshot.cpp
 *
 * Native projection of authoritative vendor-stock snapshots.
 */

#include "network/authoritative/store_snapshot.hpp"

#include <limits>
#include <type_traits>
#include <unordered_set>
#include <utility>

#include "devilution.pb.h"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

template <typename Target, typename Source>
bool AssignIfRepresentable(Target &target, Source source)
{
	if constexpr (std::is_enum_v<Target>) {
		using Underlying = std::underlying_type_t<Target>;
		if (!std::in_range<Underlying>(source))
			return false;
	} else if constexpr (std::is_integral_v<Target> && std::is_integral_v<Source>) {
		if (!std::in_range<Target>(source))
			return false;
	}
	target = static_cast<Target>(source);
	return true;
}

tl::expected<Item, std::string> ProjectItem(const protocol::StoreItemSnapshot &source)
{
	const protocol::ItemStateSnapshot &state = source.state();
	Item item;
	item._iSeed = source.item_seed();
	if (!AssignIfRepresentable(item._iCreateInfo, state.create_info())
	    || !AssignIfRepresentable(item._itype, state.item_type())
	    || !AssignIfRepresentable(item._iMagical, state.magical())
	    || !AssignIfRepresentable(item._iLoc, state.equip_location())
	    || !AssignIfRepresentable(item._iClass, state.item_class())
	    || !AssignIfRepresentable(item._iMinDam, state.min_damage())
	    || !AssignIfRepresentable(item._iMaxDam, state.max_damage())
	    || !AssignIfRepresentable(item._iAC, state.armor_class())
	    || !AssignIfRepresentable(item._iFlags, state.flags())
	    || !AssignIfRepresentable(item._iMiscId, state.misc_id())
	    || !AssignIfRepresentable(item._iSpell, state.spell_id())
	    || !AssignIfRepresentable(item.IDidx, state.item_index())
	    || !AssignIfRepresentable(item._iPLDam, state.plus_damage())
	    || !AssignIfRepresentable(item._iPLToHit, state.plus_to_hit())
	    || !AssignIfRepresentable(item._iPLAC, state.plus_armor_class())
	    || !AssignIfRepresentable(item._iPLStr, state.plus_strength())
	    || !AssignIfRepresentable(item._iPLMag, state.plus_magic())
	    || !AssignIfRepresentable(item._iPLDex, state.plus_dexterity())
	    || !AssignIfRepresentable(item._iPLVit, state.plus_vitality())
	    || !AssignIfRepresentable(item._iPLFR, state.plus_fire_resistance())
	    || !AssignIfRepresentable(item._iPLLR, state.plus_lightning_resistance())
	    || !AssignIfRepresentable(item._iPLMR, state.plus_magic_resistance())
	    || !AssignIfRepresentable(item._iPLMana, state.plus_mana())
	    || !AssignIfRepresentable(item._iPLHP, state.plus_hit_points())
	    || !AssignIfRepresentable(item._iPLDamMod, state.plus_damage_modifier())
	    || !AssignIfRepresentable(item._iPLGetHit, state.plus_get_hit())
	    || !AssignIfRepresentable(item._iPLLight, state.plus_light())
	    || !AssignIfRepresentable(item._iSplLvlAdd, state.spell_level_add())
	    || !AssignIfRepresentable(item._iFMinDam, state.fire_min_damage())
	    || !AssignIfRepresentable(item._iFMaxDam, state.fire_max_damage())
	    || !AssignIfRepresentable(item._iLMinDam, state.lightning_min_damage())
	    || !AssignIfRepresentable(item._iLMaxDam, state.lightning_max_damage())
	    || !AssignIfRepresentable(item._iPLEnAc, state.plus_enemy_armor_class())
	    || !AssignIfRepresentable(item._iPrePower, state.prefix_power())
	    || !AssignIfRepresentable(item._iSufPower, state.suffix_power())
	    || !AssignIfRepresentable(item._iMinStr, state.minimum_strength())
	    || !AssignIfRepresentable(item._iMinMag, state.minimum_magic())
	    || !AssignIfRepresentable(item._iMinDex, state.minimum_dexterity())
	    || !AssignIfRepresentable(item._iDamAcFlags, state.hellfire_damage_armor_flags())) {
		return tl::make_unexpected("Authoritative item state contains a value outside the native representation.");
	}
	item.position = { state.position_x(), state.position_y() };
	item._iDelFlag = state.deleted();
	item._iIdentified = state.identified();
	item._ivalue = state.value();
	item._iIvalue = static_cast<int>(source.price());
	item._iCharges = state.charges();
	item._iMaxCharges = state.max_charges();
	item._iDurability = state.durability();
	item._iMaxDur = state.max_durability();
	item._iUid = state.unique_id();
	item._iVAdd1 = state.value_add_1();
	item._iVMult1 = state.value_multiply_1();
	item._iVAdd2 = state.value_add_2();
	item._iVMult2 = state.value_multiply_2();
	item._iStatFlag = state.stat_flag();
	item.dwBuff = state.buff();
	return item;
}

} // namespace

tl::expected<ProjectedStoreSnapshot, std::string> ProjectStoreSnapshot(const protocol::Snapshot &snapshot)
{
	if (!snapshot.has_active_store())
		return tl::make_unexpected("Authoritative snapshot has no active store.");
	if (snapshot.active_store().store_id() == 0)
		return tl::make_unexpected("Authoritative store snapshot has an invalid store identifier.");

	ProjectedStoreSnapshot projected { .storeId = snapshot.active_store().store_id() };
	std::unordered_set<uint32_t> slots;
	projected.items.reserve(snapshot.active_store().items_size());
	for (const protocol::StoreItemSnapshot &source : snapshot.active_store().items()) {
		if (!slots.insert(source.store_slot()).second)
			return tl::make_unexpected("Authoritative store snapshot contains a duplicate slot.");
		if (source.price() > static_cast<uint32_t>(std::numeric_limits<int>::max()))
			return tl::make_unexpected("Authoritative store snapshot contains an unsupported price.");
		auto item = ProjectItem(source);
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.items.push_back({
		    .storeSlot = source.store_slot(),
		    .itemSeed = source.item_seed(),
		    .price = source.price(),
		    .item = std::move(*item),
		});
	}
	return projected;
}

} // namespace devilution::authoritative
