using System.Collections.Generic;
using NUnit.Framework;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Rules.Persistence;
using StickmanBeatEmUp.Tests.CoreUnit.TV2.TestDoubles;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV2
{
    [TestFixture]
    public class PersistenceAndConfigTests
    {
        [Test]
        public void MidMatchSaveService_SaveAndLoadRoundTrip_RestoresAllFieldsExactly()
        {
            // Arrange
            InMemorySaveStorage storage = new InMemorySaveStorage();
            FakeClock clock = new FakeClock("2026-11-22T12:30:00Z", 95.5f);
            MidMatchSaveService service = new MidMatchSaveService(storage, clock);

            GameStateDTO original = new GameStateDTO
            {
                schemaVersion = 1,
                accountUsername = "tv2_tester",
                gameMode = 1,
                worldIndex = 2,
                stageIndex = 3,
                currentWaveIndex = 7,
                playerCharacterIndex = 1,
                playerHp = 64.5f,
                playerMaxHp = 100f,
                playerPosX = 12.25f,
                playerPosY = -2.5f,
                playerRage = 18.5f,
                isTransformed = true,
                virtualShieldHp = 22f,
                coins = 185,
                senzuCount = 3,
                spinalFluidCount = 2,
                ultraWaterCount = 1,
                equippedSlotQ = 1,
                equippedSlotE = 4,
                score = 4200,
                extraItemCounts = new List<ItemCountEntryDTO> { new ItemCountEntryDTO { itemTypeId = 4, count = 2 } },
                activeBuffs = new List<BuffSnapshotDTO> { new BuffSnapshotDTO { buffId = "SpinalFluid", remainingSeconds = 19.5f, remainingShield = 22f } }
            };

            // Act
            service.SaveState(original, "slot_roundtrip");
            GameStateDTO loaded = service.LoadState("slot_roundtrip");

            // Assert
            Assert.AreEqual(1, storage.SaveCallCount);
            Assert.AreEqual(1, storage.LoadCallCount);
            Assert.AreEqual("tv2_tester", loaded.accountUsername);
            Assert.AreEqual(7, loaded.currentWaveIndex);
            Assert.AreEqual(64.5f, loaded.playerHp, 0.001f);
            Assert.AreEqual(18.5f, loaded.playerRage, 0.001f);
            Assert.AreEqual(185, loaded.coins);
            Assert.AreEqual(4, loaded.equippedSlotE);
            Assert.AreEqual(1, loaded.extraItemCounts.Count);
            Assert.AreEqual(2, loaded.extraItemCounts[0].count);
            Assert.AreEqual(1, loaded.activeBuffs.Count);
            Assert.AreEqual("SpinalFluid", loaded.activeBuffs[0].buffId);
            Assert.AreEqual("2026-11-22T12:30:00Z", loaded.savedAtUtc);
            Assert.AreEqual(95.5f, loaded.elapsedSeconds, 0.001f);
        }

        [Test]
        public void MidMatchSaveService_CorruptedJson_ThrowsSaveCorruptedException()
        {
            // Arrange
            InMemorySaveStorage storage = new InMemorySaveStorage();
            storage.SeedRaw("broken_slot", "{ \"schemaVersion\": 1, \"coins\": BROKEN ");
            MidMatchSaveService service = new MidMatchSaveService(storage, new FakeClock());

            // Act & Assert
            SaveCorruptedException ex = Assert.Throws<SaveCorruptedException>(() => service.LoadState("broken_slot"));
            Assert.AreEqual("SAVE_CORRUPTED", ex.ErrorCode);
            Assert.AreEqual("broken_slot", ex.StorageKey);
        }

        [Test]
        public void MidMatchSaveService_MissingSaveKey_ThrowsSaveCorruptedException()
        {
            // Arrange
            InMemorySaveStorage storage = new InMemorySaveStorage();
            MidMatchSaveService service = new MidMatchSaveService(storage, new FakeClock());

            // Act & Assert
            Assert.IsFalse(service.HasSave("missing_slot"));
            SaveCorruptedException ex = Assert.Throws<SaveCorruptedException>(() => service.LoadState("missing_slot"));
            Assert.AreEqual("SAVE_CORRUPTED", ex.ErrorCode);
            Assert.AreEqual("missing_slot", ex.StorageKey);
        }

        [Test]
        public void GameConfigService_ValidJson_ParsesAllShopItemsAndLevels()
        {
            // Arrange
            const string json =
                "{\"version\":1,\"playerBaseMaxHp\":120,\"maxRage\":30,\"blockDamageMultiplier\":0.2," +
                "\"endlessWaveHpGrowth\":0.03,\"endlessMaxDamageMultiplier\":1.75," +
                "\"shopItems\":[{\"itemTypeId\":1,\"itemName\":\"Đậu Thần\",\"price\":30,\"healAmount\":10,\"damageMultiplier\":1,\"speedMultiplier\":1}]," +
                "\"levels\":[{\"worldIndex\":1,\"stageIndex\":1,\"enemyHpMultiplier\":1.0,\"enemyDamageMultiplier\":1.0,\"bonusClearCoins\":20}]}";

            // Act
            GameBalanceConfigDTO cfg = GameConfigService.ParseAndValidate(json);

            // Assert
            Assert.AreEqual(120f, cfg.playerBaseMaxHp, 0.001f);
            Assert.AreEqual(1, cfg.shopItems.Count);
            Assert.AreEqual(30, cfg.shopItems[0].price);
            Assert.AreEqual(1, cfg.levels.Count);
        }

        [Test]
        public void GameConfigService_OutOfRangeParameter_ThrowsInvalidGameConfigException()
        {
            // Arrange
            const string invalidJson =
                "{\"version\":1,\"playerBaseMaxHp\":-50,\"maxRage\":30,\"blockDamageMultiplier\":0.2," +
                "\"endlessWaveHpGrowth\":0.03,\"endlessMaxDamageMultiplier\":1.75,\"shopItems\":[],\"levels\":[]}";

            // Act & Assert
            InvalidGameConfigException ex = Assert.Throws<InvalidGameConfigException>(
                () => GameConfigService.ParseAndValidate(invalidJson));
            Assert.AreEqual("INVALID_CONFIG", ex.ErrorCode);
            Assert.AreEqual("playerBaseMaxHp", ex.ConfigKey);
        }

        [Test]
        public void HighScoreRanker_DeduplicatesUsersAndReturnsSortedTopKWithTimeTieBreak()
        {
            // Arrange
            List<HighScoreEntryDTO> raw = new List<HighScoreEntryDTO>
            {
                new HighScoreEntryDTO { username = "Alice", gameMode = 1, worldOrWaveReached = 5, score = 1000, clearTimeSeconds = 90f },
                new HighScoreEntryDTO { username = "alice", gameMode = 1, worldOrWaveReached = 8, score = 1500, clearTimeSeconds = 110f },
                new HighScoreEntryDTO { username = "Bob", gameMode = 1, worldOrWaveReached = 7, score = 1500, clearTimeSeconds = 85f },
                new HighScoreEntryDTO { username = "Carol", gameMode = 1, worldOrWaveReached = 4, score = 800, clearTimeSeconds = 60f },
                new HighScoreEntryDTO { username = "Dave", gameMode = 1, worldOrWaveReached = 2, score = 400, clearTimeSeconds = 40f }
            };

            // Act
            List<HighScoreEntryDTO> top3 = HighScoreRanker.RankTopK(raw, topK: 3);

            // Assert
            Assert.AreEqual(3, top3.Count);
            Assert.AreEqual("Bob", top3[0].username);   // Hòa 1500 điểm nhưng thời gian 85s < 110s
            Assert.AreEqual("alice", top3[1].username); // Chỉ giữ bản 1500, loại bản trùng 1000
            Assert.AreEqual("Carol", top3[2].username);
        }

        [Test]
        public void HighScoreService_RecordAndReload_PersistsRankedBoardAndRejectsCorruptedJson()
        {
            // Arrange
            InMemorySaveStorage storage = new InMemorySaveStorage();
            FakeClock clock = new FakeClock("2026-11-25T10:00:00Z", 60f);
            HighScoreService service = new HighScoreService(storage, clock, maxEntries: 2);

            // Act
            service.RecordScore(new HighScoreEntryDTO { username = "PlayerA", gameMode = 0, worldOrWaveReached = 3, score = 900, clearTimeSeconds = 50f });
            service.RecordScore(new HighScoreEntryDTO { username = "PlayerB", gameMode = 0, worldOrWaveReached = 4, score = 1200, clearTimeSeconds = 70f });
            service.RecordScore(new HighScoreEntryDTO { username = "PlayerC", gameMode = 0, worldOrWaveReached = 2, score = 500, clearTimeSeconds = 30f });
            List<HighScoreEntryDTO> loaded = service.LoadTopScores();

            // Assert
            Assert.AreEqual(2, loaded.Count);
            Assert.AreEqual("PlayerB", loaded[0].username);
            Assert.AreEqual("PlayerA", loaded[1].username);
            Assert.AreEqual("2026-11-25T10:00:00Z", loaded[0].achievedAtUtc);

            // Corrupt storage and verify SaveCorruptedException
            storage.SeedRaw(HighScoreService.HIGHSCORE_STORAGE_KEY, "{ invalid json");
            Assert.Throws<SaveCorruptedException>(() => service.LoadTopScores());
        }
    }
}
