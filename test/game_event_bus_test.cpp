#include <cstdint>
#include <string>

#include <gtest/gtest.h>

#include "game/events/event_bus.hpp"

namespace devilution {
namespace {

class RecordingListener final : public GameEventListener {
public:
	void OnMonsterDataLoaded() override { ++monsterDataLoaded; }
	void OnUniqueMonsterDataLoaded() override { ++uniqueMonsterDataLoaded; }
	void OnItemDataLoaded() override { ++itemDataLoaded; }
	void OnUniqueItemDataLoaded() override { ++uniqueItemDataLoaded; }
	void OnStoreOpened(std::string_view storeName) override { openedStore = storeName; }
	void OnMonsterTakeDamage(size_t id, int amount, int type) override
	{
		monsterId = id;
		monsterDamage = amount;
		monsterDamageType = type;
	}
	void OnPlayerGainExperience(uint8_t id, uint32_t amount) override
	{
		playerId = id;
		experience = amount;
	}
	void OnPlayerTakeDamage(uint8_t id, int amount, int type) override
	{
		playerId = id;
		playerDamage = amount;
		playerDamageType = type;
	}
	void OnGameDataReloaded() override { ++gameDataReloaded; }
	void OnGameDrawComplete() override { ++gameDrawComplete; }
	void OnGameStart() override { ++gameStart; }

	int monsterDataLoaded = 0;
	int uniqueMonsterDataLoaded = 0;
	int itemDataLoaded = 0;
	int uniqueItemDataLoaded = 0;
	std::string openedStore;
	size_t monsterId = 0;
	int monsterDamage = 0;
	int monsterDamageType = 0;
	uint8_t playerId = 0;
	uint32_t experience = 0;
	int playerDamage = 0;
	int playerDamageType = 0;
	int gameDataReloaded = 0;
	int gameDrawComplete = 0;
	int gameStart = 0;
};

TEST(GameEventBus, PublishesStableValues)
{
	GameEventBus bus;
	RecordingListener listener;
	bus.Subscribe(listener);

	bus.StoreOpened("griswold");
	bus.MonsterTakeDamage(37, 128, 2);
	bus.PlayerGainExperience(3, 9001);
	bus.GameDataReloaded();

	EXPECT_EQ(listener.openedStore, "griswold");
	EXPECT_EQ(listener.monsterId, 37);
	EXPECT_EQ(listener.monsterDamage, 128);
	EXPECT_EQ(listener.monsterDamageType, 2);
	EXPECT_EQ(listener.playerId, 3);
	EXPECT_EQ(listener.experience, 9001);
	EXPECT_EQ(listener.gameDataReloaded, 1);
}

TEST(GameEventBus, SubscribeIsIdempotentAndUnsubscribeStopsNotifications)
{
	GameEventBus bus;
	RecordingListener listener;
	bus.Subscribe(listener);
	bus.Subscribe(listener);

	bus.GameStart();
	EXPECT_EQ(listener.gameStart, 1);

	bus.Unsubscribe(listener);
	bus.GameStart();
	EXPECT_EQ(listener.gameStart, 1);
}

} // namespace
} // namespace devilution
