using System;
using NUnit.Framework;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;

namespace StickmanBeatEmUp.Tests.CoreUnit
{
    [TestFixture]
    public class CoreContractsSmokeTests
    {
        [Test]
        public void GameStateDTO_ValidState_PassesValidation()
        {
            GameStateDTO dto = new GameStateDTO
            {
                schemaVersion = 1,
                accountUsername = "player1",
                worldIndex = 1,
                stageIndex = 1,
                currentWaveIndex = 1,
                playerHp = 85f,
                playerMaxHp = 100f,
                playerRage = 10f,
                coins = 50,
                savedAtUtc = "2026-11-20T09:50:00Z"
            };

            Assert.DoesNotThrow(() => dto.Validate("slot_1"));
        }

        [Test]
        public void GameStateDTO_NegativeCoins_ThrowsSaveCorruptedException()
        {
            GameStateDTO dto = new GameStateDTO
            {
                schemaVersion = 1,
                coins = -5,
                savedAtUtc = "2026-11-20T09:50:00Z"
            };

            SaveCorruptedException ex = Assert.Throws<SaveCorruptedException>(() => dto.Validate("slot_1"));
            Assert.AreEqual("SAVE_CORRUPTED", ex.ErrorCode);
            Assert.AreEqual("slot_1", ex.StorageKey);
        }

        [Test]
        public void GameBalanceConfigDTO_InvalidBlockMultiplier_ThrowsInvalidGameConfigException()
        {
            GameBalanceConfigDTO cfg = new GameBalanceConfigDTO
            {
                blockDamageMultiplier = 1.5f
            };

            InvalidGameConfigException ex = Assert.Throws<InvalidGameConfigException>(() => cfg.Validate());
            Assert.AreEqual("INVALID_CONFIG", ex.ErrorCode);
            Assert.IsInstanceOf<StickmanGameException>(ex);
        }
    }
}
