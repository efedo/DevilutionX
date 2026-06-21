/**
 * @file portal.cpp
 *
 * Implementation of functionality for handling town portals.
 */
#include "portal.h"

#include "engine/world.hpp"
#include "lighting.h"
#include "missiles.h"
#include "multi.h"
#include "player.h"
#include "tables/misdat.h"

namespace devilution {

/** In-game state of portals. */
Portal Portals[MAXPORTAL];

namespace {

/** Current portal number (a portal array index). */
size_t portalindex;

/** Coordinate of each player's portal in town. */
Point PortalTownPosition[MAXPORTAL] = {
	{ 57, 40 },
	{ 59, 40 },
	{ 61, 40 },
	{ 63, 40 },
};

} // namespace

void InitPortals()
{
	for (auto &portal : Portals) {
		portal.open = false;
	}
}

void SetPortalStats(int i, bool o, Point position, int lvl, dungeon_type lvltype, bool isSetLevel)
{
	Portals[i].open = o;
	Portals[i].position = position;
	Portals[i].level = lvl;
	Portals[i].ltype = lvltype;
	Portals[i].setlvl = isSetLevel;
}

void AddPortalMissile(const Player &player, Point position, bool sync)
{
	auto *missile = AddMissile({ 0, 0 }, position, Direction::South, MissileID::TownPortal, TARGET_MONSTERS, player, 0, 0, /*parent=*/nullptr, SfxID::None);
	if (missile != nullptr) {
		// Don't show portal opening animation if we sync existing portals
		if (sync)
			missile->setFrameGroup<PortalFrame>(PortalFrame::Idle);

		if (levelType() != DTYPE_TOWN)
			missile->_mlid = AddLight(missile->position.tile, 15);
	}
}

void SyncPortals()
{
	for (int i = 0; i < MAXPORTAL; i++) {
		if (!Portals[i].open)
			continue;
		const Player &player = Players[i];
		if (levelType() == DTYPE_TOWN)
			AddPortalMissile(player, PortalTownPosition[i], true);
		else {
			int lvl = currentLevelNumber();
			if (isSetLevel())
				lvl = setLevelNumber();
			if (Portals[i].level == lvl && Portals[i].setlvl == isSetLevel())
				AddPortalMissile(player, Portals[i].position, true);
		}
	}
}

void AddPortalInTown(const Player &player)
{
	AddPortalMissile(player, PortalTownPosition[player.getId()], false);
}

void ActivatePortal(const Player &player, Point position, int lvl, dungeon_type dungeonType, bool isSetLevel)
{
	Portal &portal = Portals[player.getId()];
	portal.open = true;

	if (lvl != 0) {
		portal.position = position;
		portal.level = lvl;
		portal.ltype = dungeonType;
		portal.setlvl = isSetLevel;
	}
}

void DeactivatePortal(const Player &player)
{
	Portals[player.getId()].open = false;
}

bool PortalOnLevel(const Player &player)
{
	const Portal &portal = Portals[player.getId()];
	if (portal.setlvl == isSetLevel() && portal.level == (isSetLevel() ? static_cast<int>(setLevelNumber()) : currentLevelNumber()))
		return true;

	return levelType() == DTYPE_TOWN;
}

void RemovePortalMissile(const Player &player)
{
	const size_t id = player.getId();
	Missiles.remove_if([id](Missile &missile) {
		if (missile._mitype == MissileID::TownPortal && missile._misource == static_cast<int>(id)) {
			tileAt(missile.position.tile).removeFlags(DungeonFlag::Missile);

			if (Portals[id].level != 0)
				AddUnLight(missile._mlid);

			return true;
		}
		return false;
	});
}

void SetCurrentPortal(size_t p)
{
	portalindex = p;
}

void GetPortalLevel()
{
	if (levelType() != DTYPE_TOWN) {
		SwitchCurrentLevel(0);
		isSetLevel() = false;
		currentLevelNumber() = 0;
		MyPlayer->setLevel(0);
		levelType() = DTYPE_TOWN;
		return;
	}

	if (Portals[portalindex].setlvl) {
		SwitchCurrentLevel(static_cast<LevelIndex>(-1 - static_cast<int>(Portals[portalindex].level)));
		isSetLevel() = true;
		setLevelNumber() = (_setlevels)Portals[portalindex].level;
		currentLevelNumber() = Portals[portalindex].level;
		MyPlayer->setLevel(setLevelNumber());
		setLevelType() = levelType() = Portals[portalindex].ltype;
	} else {
		SwitchCurrentLevel(static_cast<LevelIndex>(Portals[portalindex].level));
		isSetLevel() = false;
		currentLevelNumber() = Portals[portalindex].level;
		MyPlayer->setLevel(currentLevelNumber());
		levelType() = Portals[portalindex].ltype;
	}

	if (portalindex == MyPlayerId) {
		NetSendCmd(true, CMD_DEACTIVATEPORTAL);
		DeactivatePortal(*MyPlayer);
	}
}

void GetPortalLvlPos()
{
	if (levelType() == DTYPE_TOWN) {
		viewPosition() = PortalTownPosition[portalindex] + Displacement { 1, 1 };
	} else {
		viewPosition() = Portals[portalindex].position;

		if (portalindex != MyPlayerId) {
			viewPosition().x++;
			viewPosition().y++;
		}
	}
}

bool PosOkPortal(int lvl, Point position)
{
	for (auto &portal : Portals) {
		if (portal.open
		    && portal.level == lvl
		    && ((portal.position == position)
		        || (portal.position == position - Displacement { 1, 1 })))
			return true;
	}
	return false;
}

} // namespace devilution
