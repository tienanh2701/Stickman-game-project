using System;
using NUnit.Framework;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;
using StickmanBeatEmUp.CorePure.Patterns.Decorators;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV2
{
    [TestFixture]
    public class Tv2DecoratorTests
    {
        [Test]
        public void DefaultStatSource_DefaultInstance_HasNeutralMultipliersAndZeroBonuses()
        {
            DefaultStatSource stats = DefaultStatSource.Default;

            Assert.AreEqual(1f, stats.AttackDamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, stats.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0f, stats.MaxShieldCapacity, 0.0001f);
            Assert.AreEqual(0f, stats.DamageReflectAmount, 0.0001f);
            Assert.AreEqual(0f, stats.RagePerSecondBonus, 0.0001f);
        }

        [Test]
        public void SpinalFluidShieldDecorator_ValidValues_AddsShieldAndReflect()
        {
            IStatSource baseStats = DefaultStatSource.Default;
            SpinalFluidShieldDecorator decorator = new SpinalFluidShieldDecorator(baseStats, 40f, 5f);

            Assert.AreEqual(1f, decorator.AttackDamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, decorator.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(40f, decorator.MaxShieldCapacity, 0.0001f);
            Assert.AreEqual(5f, decorator.DamageReflectAmount, 0.0001f);
            Assert.AreEqual(0f, decorator.RagePerSecondBonus, 0.0001f);
        }

        [Test]
        public void SpinalFluidShieldDecorator_NegativeOrNaNOrInfinity_ClampsToZero()
        {
            IStatSource baseStats = DefaultStatSource.Default;
            SpinalFluidShieldDecorator nanDecorator = new SpinalFluidShieldDecorator(baseStats, float.NaN, float.PositiveInfinity);
            SpinalFluidShieldDecorator negDecorator = new SpinalFluidShieldDecorator(baseStats, -10f, -5f);

            Assert.AreEqual(0f, nanDecorator.MaxShieldCapacity, 0.0001f);
            Assert.AreEqual(0f, nanDecorator.DamageReflectAmount, 0.0001f);
            Assert.AreEqual(0f, negDecorator.MaxShieldCapacity, 0.0001f);
            Assert.AreEqual(0f, negDecorator.DamageReflectAmount, 0.0001f);
        }

        [Test]
        public void UltraDivineWaterDecorator_ValidValues_ScalesMultipliersAndAddsRage()
        {
            IStatSource baseStats = DefaultStatSource.Default;
            UltraDivineWaterDecorator decorator = new UltraDivineWaterDecorator(baseStats, 1.5f, 1.3f, 2f);

            Assert.AreEqual(1.5f, decorator.AttackDamageMultiplier, 0.0001f);
            Assert.AreEqual(1.3f, decorator.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0f, decorator.MaxShieldCapacity, 0.0001f);
            Assert.AreEqual(0f, decorator.DamageReflectAmount, 0.0001f);
            Assert.AreEqual(2f, decorator.RagePerSecondBonus, 0.0001f);
        }

        [Test]
        public void UltraDivineWaterDecorator_NegativeOrNaN_FallsBackToNeutralMultiplier()
        {
            IStatSource baseStats = DefaultStatSource.Default;
            UltraDivineWaterDecorator nanDecorator = new UltraDivineWaterDecorator(baseStats, float.NaN, float.NegativeInfinity, float.NaN);
            UltraDivineWaterDecorator negDecorator = new UltraDivineWaterDecorator(baseStats, -2f, -1f, -5f);

            Assert.AreEqual(1f, nanDecorator.AttackDamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, nanDecorator.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0f, nanDecorator.RagePerSecondBonus, 0.0001f);

            Assert.AreEqual(1f, negDecorator.AttackDamageMultiplier, 0.0001f);
            Assert.AreEqual(1f, negDecorator.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0f, negDecorator.RagePerSecondBonus, 0.0001f);
        }

        [Test]
        public void RageRegenDecorator_ValidAndEdgeValues_AddsRageSafely()
        {
            IStatSource baseStats = DefaultStatSource.Default;
            RageRegenDecorator valid = new RageRegenDecorator(baseStats, 3f);
            RageRegenDecorator nan = new RageRegenDecorator(baseStats, float.NaN);

            Assert.AreEqual(3f, valid.RagePerSecondBonus, 0.0001f);
            Assert.AreEqual(0f, nan.RagePerSecondBonus, 0.0001f);
        }

        [Test]
        public void BuffStackResolver_ApplyAndBuildStatChain_StacksDecoratorsCorrectly()
        {
            BuffStackResolver resolver = new BuffStackResolver();

            resolver.ApplyOrRefresh(
                "spinal_fluid",
                30f,
                40f,
                true,
                inner => new SpinalFluidShieldDecorator(inner, 40f, 5f));

            resolver.ApplyOrRefresh(
                "ultra_water",
                60f,
                0f,
                false,
                inner => new UltraDivineWaterDecorator(inner, 1.5f, 1.3f, 2f));

            IStatSource finalStats = resolver.BuildStatChain();

            Assert.AreEqual(2, resolver.ActiveCount);
            Assert.AreEqual(1.5f, finalStats.AttackDamageMultiplier, 0.0001f);
            Assert.AreEqual(1.3f, finalStats.MoveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(40f, finalStats.MaxShieldCapacity, 0.0001f);
            Assert.AreEqual(5f, finalStats.DamageReflectAmount, 0.0001f);
            Assert.AreEqual(2f, finalStats.RagePerSecondBonus, 0.0001f);
        }

        [Test]
        public void BuffStackResolver_Tick_ExpiresBuffWhenTimeElapses()
        {
            BuffStackResolver resolver = new BuffStackResolver();
            resolver.ApplyOrRefresh(
                "buff_temp",
                5f,
                0f,
                false,
                inner => new RageRegenDecorator(inner, 2f));

            Assert.AreEqual(1, resolver.ActiveCount);

            resolver.Tick(2f);
            Assert.AreEqual(1, resolver.ActiveCount);
            Assert.AreEqual(3f, resolver.GetRemainingSeconds("buff_temp"), 0.0001f);

            resolver.Tick(3.1f);
            Assert.AreEqual(0, resolver.ActiveCount);
            Assert.IsFalse(resolver.HasBuff("buff_temp"));
        }

        [Test]
        public void BuffStackResolver_DepleteShield_ExpiresShieldDependentBuff()
        {
            BuffStackResolver resolver = new BuffStackResolver();
            resolver.ApplyOrRefresh(
                "spinal_shield",
                30f,
                40f,
                true,
                inner => new SpinalFluidShieldDecorator(inner, 40f, 5f));

            Assert.IsTrue(resolver.HasBuff("spinal_shield"));

            resolver.UpdateShield("spinal_shield", 15f);
            Assert.IsTrue(resolver.HasBuff("spinal_shield"));

            resolver.UpdateShield("spinal_shield", 0f);
            Assert.IsFalse(resolver.HasBuff("spinal_shield"));
            Assert.AreEqual(0, resolver.ActiveCount);
        }

        [Test]
        public void BuffStackResolver_ReapplySameBuff_RefreshesDurationWithoutDoubling()
        {
            BuffStackResolver resolver = new BuffStackResolver();

            resolver.ApplyOrRefresh(
                "ultra_water",
                30f,
                0f,
                false,
                inner => new UltraDivineWaterDecorator(inner, 1.5f, 1.3f, 2f));

            resolver.Tick(10f);
            Assert.AreEqual(20f, resolver.GetRemainingSeconds("ultra_water"), 0.0001f);

            // Re-apply same buff with 40s
            resolver.ApplyOrRefresh(
                "ultra_water",
                40f,
                0f,
                false,
                inner => new UltraDivineWaterDecorator(inner, 1.5f, 1.3f, 2f));

            Assert.AreEqual(1, resolver.ActiveCount);
            Assert.AreEqual(40f, resolver.GetRemainingSeconds("ultra_water"), 0.0001f);

            IStatSource stats = resolver.BuildStatChain();
            // Multiplier should still be 1.5f, not 1.5 * 1.5 = 2.25f
            Assert.AreEqual(1.5f, stats.AttackDamageMultiplier, 0.0001f);
        }

        [Test]
        public void BuffStackResolver_NaNDeltaTimeOrDuration_DoesNotCorruptResolverState()
        {
            BuffStackResolver resolver = new BuffStackResolver();

            // NaN duration is ignored
            resolver.ApplyOrRefresh("invalid_buff", float.NaN, 0f, false, inner => inner);
            Assert.AreEqual(0, resolver.ActiveCount);

            // Add valid buff
            resolver.ApplyOrRefresh("valid_buff", 10f, 0f, false, inner => inner);
            Assert.AreEqual(1, resolver.ActiveCount);

            // NaN deltaTime is ignored, remaining seconds untouched
            resolver.Tick(float.NaN);
            Assert.AreEqual(1, resolver.ActiveCount);
            Assert.AreEqual(10f, resolver.GetRemainingSeconds("valid_buff"), 0.0001f);
        }

        [Test]
        public void BuffStackResolver_ExportAndRestoreSnapshots_PreservesActiveBuffState()
        {
            BuffStackResolver sourceResolver = new BuffStackResolver();
            sourceResolver.ApplyOrRefresh(
                "spinal_fluid",
                25f,
                30f,
                true,
                inner => new SpinalFluidShieldDecorator(inner, 40f, 5f));

            sourceResolver.ApplyOrRefresh(
                "ultra_water",
                50f,
                0f,
                false,
                inner => new UltraDivineWaterDecorator(inner, 1.5f, 1.3f, 2f));

            var snapshots = sourceResolver.ExportSnapshots();
            Assert.AreEqual(2, snapshots.Count);

            BuffStackResolver restoredResolver = new BuffStackResolver();
            restoredResolver.RestoreSnapshots(
                snapshots,
                dto =>
                {
                    if (dto.buffId == "spinal_fluid") return inner => new SpinalFluidShieldDecorator(inner, 40f, 5f);
                    if (dto.buffId == "ultra_water") return inner => new UltraDivineWaterDecorator(inner, 1.5f, 1.3f, 2f);
                    return null;
                },
                buffId => buffId == "spinal_fluid");

            Assert.AreEqual(2, restoredResolver.ActiveCount);
            Assert.AreEqual(25f, restoredResolver.GetRemainingSeconds("spinal_fluid"), 0.0001f);
            Assert.AreEqual(50f, restoredResolver.GetRemainingSeconds("ultra_water"), 0.0001f);

            IStatSource restoredStats = restoredResolver.BuildStatChain();
            Assert.AreEqual(1.5f, restoredStats.AttackDamageMultiplier, 0.0001f);
            Assert.AreEqual(40f, restoredStats.MaxShieldCapacity, 0.0001f);
        }
    }
}
