using NUnit.Framework;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Interfaces;
using StickmanBeatEmUp.CorePure.Patterns.Items;
using StickmanBeatEmUp.CorePure.Rules.Economy;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV2
{
    [TestFixture]
    public class Tv2ItemAndEconomyTests
    {
        private sealed class CustomTestItemEffect : IItemEffect
        {
            public int ItemTypeId => 99;
            public string ItemName => "Custom OCP Elixir";

            public bool CanApply(ItemUseContext context) => !context.IsPlayerDead && context.QuantityOwned > 0;

            public ItemEffectResult ApplyEffect(ItemUseContext context)
            {
                return new ItemEffectResult
                {
                    Applied = true,
                    HealAmount = 25f,
                    DamageMultiplier = 1f,
                    SpeedMultiplier = 1f,
                    FeedbackMessage = "Custom applied!"
                };
            }
        }

        private static ItemUseContext CreateAliveContext(int qty = 1)
        {
            return new ItemUseContext
            {
                CurrentHp = 50f,
                MaxHp = 100f,
                CurrentRage = 5f,
                MaxRage = 30f,
                QuantityOwned = qty,
                IsPlayerDead = false
            };
        }

        [Test]
        public void ItemEffectRegistry_DefaultRegistry_ExecutesAllFourItems()
        {
            ItemEffectRegistry registry = ItemEffectRegistry.CreateDefault();
            ItemUseContext ctx = CreateAliveContext(1);

            Assert.AreEqual(4, registry.RegisteredCount);

            ItemEffectResult senzu = registry.Execute(ConsumableItemIds.SENZU_BEAN, ctx);
            Assert.IsTrue(senzu.Applied);
            Assert.AreEqual(10f, senzu.HealAmount, 0.0001f);
            Assert.IsTrue(senzu.ClearNegativeEffects);

            ItemEffectResult spinal = registry.Execute(ConsumableItemIds.SPINAL_FLUID, ctx);
            Assert.IsTrue(spinal.Applied);
            Assert.AreEqual(40f, spinal.ShieldAmount, 0.0001f);
            Assert.AreEqual(5f, spinal.ReflectDamage, 0.0001f);

            ItemEffectResult water = registry.Execute(ConsumableItemIds.ULTRA_DIVINE_WATER, ctx);
            Assert.IsTrue(water.Applied);
            Assert.AreEqual(1.5f, water.DamageMultiplier, 0.0001f);
            Assert.AreEqual(1.3f, water.SpeedMultiplier, 0.0001f);
            Assert.AreEqual(2f, water.RagePerSecond, 0.0001f);

            ItemEffectResult elixir = registry.Execute(ConsumableItemIds.RAGE_ELIXIR, ctx);
            Assert.IsTrue(elixir.Applied);
            Assert.AreEqual(5f, elixir.RageDelta, 0.0001f);
            Assert.AreEqual(3f, elixir.RagePerSecond, 0.0001f);
            Assert.AreEqual(20f, elixir.BuffDuration, 0.0001f);
        }

        [Test]
        public void ItemEffectRegistry_WhenPlayerDeadOrZeroQty_ReturnsRejected()
        {
            ItemEffectRegistry registry = ItemEffectRegistry.CreateDefault();

            ItemUseContext deadCtx = CreateAliveContext(1);
            deadCtx.IsPlayerDead = true;
            Assert.IsFalse(registry.Execute(ConsumableItemIds.SENZU_BEAN, deadCtx).Applied);

            ItemUseContext emptyCtx = CreateAliveContext(0);
            Assert.IsFalse(registry.Execute(ConsumableItemIds.RAGE_ELIXIR, emptyCtx).Applied);
        }

        [Test]
        public void ItemEffectRegistry_LiveOcpCustomEffect_RegistersAndExecutesWithoutModifyingRegistry()
        {
            ItemEffectRegistry registry = ItemEffectRegistry.CreateDefault();
            registry.Register(new CustomTestItemEffect());

            Assert.AreEqual(5, registry.RegisteredCount);
            ItemEffectResult result = registry.Execute(99, CreateAliveContext(1));
            Assert.IsTrue(result.Applied);
            Assert.AreEqual(25f, result.HealAmount, 0.0001f);
        }

        [Test]
        public void ItemEffects_NaNOrNegativeParams_FallbackSafely()
        {
            ItemUseContext ctx = CreateAliveContext(1);

            SenzuBeanItemEffect senzu = new SenzuBeanItemEffect(float.NaN);
            Assert.AreEqual(10f, senzu.ApplyEffect(ctx).HealAmount, 0.0001f);

            SpinalFluidItemEffect spinal = new SpinalFluidItemEffect(float.NaN, float.PositiveInfinity, -5f);
            ItemEffectResult spinalRes = spinal.ApplyEffect(ctx);
            Assert.AreEqual(30f, spinalRes.BuffDuration, 0.0001f);
            Assert.AreEqual(0f, spinalRes.ShieldAmount, 0.0001f);
            Assert.AreEqual(0f, spinalRes.ReflectDamage, 0.0001f);

            UltraDivineWaterItemEffect water = new UltraDivineWaterItemEffect(-1f, float.NaN, float.NegativeInfinity, float.NaN);
            ItemEffectResult waterRes = water.ApplyEffect(ctx);
            Assert.AreEqual(60f, waterRes.BuffDuration, 0.0001f);
            Assert.AreEqual(1.5f, waterRes.DamageMultiplier, 0.0001f);
            Assert.AreEqual(1.3f, waterRes.SpeedMultiplier, 0.0001f);
            Assert.AreEqual(0f, waterRes.RagePerSecond, 0.0001f);
        }

        [Test]
        public void InventoryRuleEngine_AddItem_ClampsAtMaxStack99()
        {
            InventoryRuleEngine engine = new InventoryRuleEngine();

            int added1 = engine.AddItem(ConsumableItemIds.SENZU_BEAN, 98);
            int added2 = engine.AddItem(ConsumableItemIds.SENZU_BEAN, 5);
            int added3 = engine.AddItem(ConsumableItemIds.SENZU_BEAN, 1);

            Assert.AreEqual(98, added1);
            Assert.AreEqual(1, added2);
            Assert.AreEqual(0, added3);
            Assert.AreEqual(InventoryRuleEngine.MAX_STACK_PER_ITEM, engine.GetOwnedCount(ConsumableItemIds.SENZU_BEAN));
        }

        [Test]
        public void InventoryRuleEngine_ConsumeSlot_NormalVsFreeTrainingMode()
        {
            InventoryRuleEngine engine = new InventoryRuleEngine();
            engine.AddItem(ConsumableItemIds.SENZU_BEAN, 1);
            engine.TrySetEquippedItem(InventoryRuleEngine.SLOT_Q, ConsumableItemIds.SENZU_BEAN);

            // Free mode does not decrement stock
            Assert.IsTrue(engine.TryConsumeSlot(InventoryRuleEngine.SLOT_Q, true, out int usedFree));
            Assert.AreEqual(ConsumableItemIds.SENZU_BEAN, usedFree);
            Assert.AreEqual(1, engine.GetOwnedCount(ConsumableItemIds.SENZU_BEAN));

            // Normal mode decrements to 0, second attempt fails
            Assert.IsTrue(engine.TryConsumeSlot(InventoryRuleEngine.SLOT_Q, false, out int usedNormal));
            Assert.AreEqual(ConsumableItemIds.SENZU_BEAN, usedNormal);
            Assert.AreEqual(0, engine.GetOwnedCount(ConsumableItemIds.SENZU_BEAN));
            Assert.IsFalse(engine.TryConsumeSlot(InventoryRuleEngine.SLOT_Q, false, out _));
        }

        [Test]
        public void InventoryRuleEngine_InvalidSlotIndex_ThrowsInvalidPurchaseException()
        {
            InventoryRuleEngine engine = new InventoryRuleEngine();

            Assert.Throws<InvalidPurchaseException>(() =>
                engine.SetEquippedItemOrThrow(2, ConsumableItemIds.SENZU_BEAN));
            Assert.Throws<InvalidPurchaseException>(() =>
                engine.SetEquippedItemOrThrow(-1, ConsumableItemIds.SENZU_BEAN));
        }

        [Test]
        public void ShopRuleEngine_BvaCoinsEqualsPrice_SucceedsWithZeroBalance()
        {
            ShopRuleEngine shop = new ShopRuleEngine();
            ShopPurchaseResult res = shop.EvaluatePurchase(
                ConsumableItemIds.RAGE_ELIXIR,
                "Tinh Chất Cuồng Nộ",
                price: 40,
                currentCoins: 40,
                currentOwned: 0);

            Assert.IsTrue(res.Succeeded);
            Assert.AreEqual(0, res.RemainingCoins);
            Assert.AreEqual(1, res.NewOwnedCount);
        }

        [Test]
        public void ShopRuleEngine_BvaCoinsBelowPrice_FailsWithExpectedVietnameseMessage()
        {
            ShopRuleEngine shop = new ShopRuleEngine();
            ShopPurchaseResult res = shop.EvaluatePurchase(
                ConsumableItemIds.SENZU_BEAN,
                "Đậu Thần",
                price: 30,
                currentCoins: 29,
                currentOwned: 0);

            Assert.IsFalse(res.Succeeded);
            Assert.AreEqual(29, res.RemainingCoins);
            Assert.AreEqual(0, res.NewOwnedCount);
            Assert.AreEqual("Không đủ Xu!", res.FeedbackMessage);
        }

        [Test]
        public void ShopRuleEngine_ValidatePurchaseOrThrow_ThrowsOnInvalidItemNegativePriceOrFullStack()
        {
            ShopRuleEngine shop = new ShopRuleEngine();

            Assert.Throws<InvalidPurchaseException>(() =>
                shop.ValidatePurchaseOrThrow(ConsumableItemIds.NONE, 30, 100, 0));
            Assert.Throws<InvalidPurchaseException>(() =>
                shop.ValidatePurchaseOrThrow(ConsumableItemIds.SENZU_BEAN, -1, 100, 0));
            Assert.Throws<InvalidPurchaseException>(() =>
                shop.ValidatePurchaseOrThrow(ConsumableItemIds.SENZU_BEAN, 30, 100, InventoryRuleEngine.MAX_STACK_PER_ITEM));
            Assert.Throws<InvalidPurchaseException>(() =>
                shop.ValidatePurchaseOrThrow(ConsumableItemIds.SENZU_BEAN, 30, 29, 0));
        }
    }
}
