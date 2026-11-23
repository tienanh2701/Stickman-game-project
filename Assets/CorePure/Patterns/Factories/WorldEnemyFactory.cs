using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.AI;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Patterns.Factories
{
    /// <summary>
    /// Pure C# GoF Factory Method implementation of IEnemyFactory.
    /// Produces scaled EnemyBlueprint instances for all 13 canonical enemies/bosses across Worlds 1-3
    /// for both Campaign and Endless modes, with OCP template registration and optional difficulty tuning.
    /// </summary>
    public sealed class WorldEnemyFactory : IEnemyFactory
    {
        public const int MIN_WORLD_INDEX = 1;
        public const int MAX_WORLD_INDEX = 3;

        private readonly Dictionary<string, EnemyBlueprint> catalog =
            new Dictionary<string, EnemyBlueprint>(StringComparer.OrdinalIgnoreCase);

        private readonly GameBalanceConfigDTO balanceConfig;

        public WorldEnemyFactory(GameBalanceConfigDTO balanceConfig = null)
        {
            if (balanceConfig != null)
            {
                balanceConfig.Validate();
                this.balanceConfig = balanceConfig;
            }

            RegisterDefaultCatalog();
        }

        public int RegisteredTemplateCount => catalog.Count;

        public void RegisterTemplate(EnemyBlueprint template)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.enemyId))
            {
                throw new InvalidGameConfigException("enemyId", "EnemyBlueprint template must have a non-empty enemyId.");
            }

            if (template.worldIndex < MIN_WORLD_INDEX || template.worldIndex > MAX_WORLD_INDEX)
            {
                throw new InvalidGameConfigException("worldIndex", $"Template worldIndex must be in [{MIN_WORLD_INDEX}, {MAX_WORLD_INDEX}], got {template.worldIndex}.");
            }

            if (float.IsNaN(template.maxHealth) || float.IsInfinity(template.maxHealth) || template.maxHealth <= 0f ||
                float.IsNaN(template.moveSpeed) || float.IsInfinity(template.moveSpeed) || template.moveSpeed < 0f ||
                float.IsNaN(template.attackDamage) || float.IsInfinity(template.attackDamage) || template.attackDamage < 0f ||
                float.IsNaN(template.heavyAttackDamage) || float.IsInfinity(template.heavyAttackDamage) || template.heavyAttackDamage < 0f ||
                float.IsNaN(template.attackCooldown) || float.IsInfinity(template.attackCooldown) || template.attackCooldown < 0f)
            {
                throw new InvalidGameConfigException(template.enemyId, "EnemyBlueprint stats are out of valid bounds.");
            }

            EnemyBlueprint copy = CloneBlueprint(template);
            copy.enemyId = template.enemyId.Trim();
            copy.strategyId = string.IsNullOrWhiteSpace(template.strategyId)
                ? MeleeChaseStrategy.ID
                : template.strategyId.Trim();
            catalog[copy.enemyId] = copy;
        }

        public bool CanCreate(string enemyId)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                return false;
            }

            return catalog.ContainsKey(enemyId.Trim());
        }

        public IReadOnlyList<string> GetEnemyIdsForWorld(int worldIndex)
        {
            if (worldIndex < MIN_WORLD_INDEX || worldIndex > MAX_WORLD_INDEX)
            {
                throw new InvalidGameConfigException("worldIndex", $"worldIndex must be in [{MIN_WORLD_INDEX}, {MAX_WORLD_INDEX}], got {worldIndex}.");
            }

            List<string> ids = new List<string>();
            foreach (KeyValuePair<string, EnemyBlueprint> kv in catalog)
            {
                if (kv.Value.worldIndex == worldIndex)
                {
                    ids.Add(kv.Key);
                }
            }

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }

        public EnemyBlueprint CreateEnemyBlueprint(string enemyId, int worldIndex, int waveIndex, bool isEndlessMode)
        {
            return CreateEnemyBlueprint(enemyId, worldIndex, waveIndex, isEndlessMode, AIDifficultyPreset.Normal);
        }

        public EnemyBlueprint CreateEnemyBlueprint(
            string enemyId,
            int worldIndex,
            int waveIndex,
            bool isEndlessMode,
            AIDifficultyPreset difficulty)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                throw new InvalidGameConfigException("enemyId", "enemyId must not be null or empty.");
            }

            if (worldIndex < MIN_WORLD_INDEX || worldIndex > MAX_WORLD_INDEX)
            {
                throw new InvalidGameConfigException("worldIndex", $"worldIndex must be in [{MIN_WORLD_INDEX}, {MAX_WORLD_INDEX}], got {worldIndex}.");
            }

            if (waveIndex < 1)
            {
                throw new InvalidGameConfigException("waveIndex", $"waveIndex must be >= 1, got {waveIndex}.");
            }

            string normalizedId = enemyId.Trim();
            if (!catalog.TryGetValue(normalizedId, out EnemyBlueprint template))
            {
                throw new InvalidGameConfigException("enemyId", $"Unknown enemyId '{normalizedId}' in WorldEnemyFactory.");
            }

            EnemyBlueprint result = CloneBlueprint(template);
            result.worldIndex = worldIndex;

            ComputeMultipliers(result.isBoss, worldIndex, waveIndex, isEndlessMode, out float hpMult, out float dmgMult);
            result.healthMultiplier = hpMult;
            result.damageMultiplier = dmgMult;
            result.maxHealth = template.maxHealth * hpMult;
            result.attackDamage = template.attackDamage * dmgMult;
            result.heavyAttackDamage = template.heavyAttackDamage * dmgMult;

            int clampedDiff = AIDifficultyTuning.ClampDifficulty((int)difficulty);
            result.moveSpeed = template.moveSpeed * AIDifficultyTuning.GetMoveSpeedScale(clampedDiff);
            result.attackCooldown = template.attackCooldown * AIDifficultyTuning.GetAttackCooldownScale(clampedDiff);

            return result;
        }

        /// <summary>
        /// Maps a prefab name, EnemyDataSO display name, or canonical ID to a registered enemyId.
        /// Returns null when no canonical match is found.
        /// </summary>
        public string ResolveCanonicalEnemyId(string rawNameOrId)
        {
            if (string.IsNullOrWhiteSpace(rawNameOrId))
            {
                return null;
            }

            string trimmed = rawNameOrId.Trim();
            if (catalog.ContainsKey(trimmed))
            {
                return trimmed;
            }

            string lower = trimmed.ToLowerInvariant();
            if (lower.Contains("puretitan") || lower.Contains("pure_titan") || lower.Contains("pure titan")) return "w1_pure_titan";
            if (lower.Contains("beasttitan") || lower.Contains("beast_titan") || lower.Contains("beast titan")) return "w1_beast_titan";
            if (lower.Contains("femaletitan") || lower.Contains("female_titan") || lower.Contains("female titan")) return "w1_female_titan";
            if (lower.Contains("warhammer") || lower.Contains("war_hammer") || lower.Contains("war hammer")) return "w1_warhammer_boss";

            if (lower.Contains("kamikaze")) return "w2_saibaman_kamikaze";
            if (lower.Contains("saibam")) return "w2_saibamen";
            if (lower.Contains("frieza")) return "w2_frieza_soldier";
            if (lower.Contains("nappa")) return "w2_nappa";
            if (lower.Contains("broly")) return "w2_broly_boss";

            if (lower.Contains("shocker") && !lower.Contains("mecha") && !lower.Contains("dai")) return "w3_shocker";
            if (lower.Contains("kasshin")) return "w3_kasshin";
            if (lower.Contains("shadowmoon") || lower.Contains("shadow_moon") || lower.Contains("shadow moon")) return "w3_shadow_moon";
            if (lower.Contains("mecha")) return "w3_mecha_boss";

            return null;
        }

        private void ComputeMultipliers(
            bool isBoss,
            int worldIndex,
            int waveOrStageIndex,
            bool isEndlessMode,
            out float hpMult,
            out float dmgMult)
        {
            if (isEndlessMode)
            {
                float hpGrowth = balanceConfig != null ? balanceConfig.endlessWaveHpGrowth : 0.03f;
                float maxDmg = balanceConfig != null ? balanceConfig.endlessMaxDamageMultiplier : 1.75f;

                if (isBoss)
                {
                    int bossTier = Math.Max(0, (waveOrStageIndex / 5) - 1);
                    hpMult = 1f + 0.10f * bossTier;
                    dmgMult = waveOrStageIndex <= 5
                        ? 1f
                        : Math.Min(maxDmg, 1f + 0.015f * (waveOrStageIndex - 5));
                }
                else
                {
                    hpMult = 1f + hpGrowth * (waveOrStageIndex - 1);
                    dmgMult = Math.Min(maxDmg, 1f + 0.015f * (waveOrStageIndex - 1));
                }

                return;
            }

            int clampedStage = Math.Max(1, Math.Min(4, waveOrStageIndex));
            if (balanceConfig != null && balanceConfig.levels != null)
            {
                for (int i = 0; i < balanceConfig.levels.Count; i++)
                {
                    LevelBalanceDTO lvl = balanceConfig.levels[i];
                    if (lvl != null && lvl.worldIndex == worldIndex && lvl.stageIndex == clampedStage)
                    {
                        hpMult = lvl.enemyHpMultiplier > 0f ? lvl.enemyHpMultiplier : 1f;
                        dmgMult = lvl.enemyDamageMultiplier > 0f ? lvl.enemyDamageMultiplier : 1f;
                        return;
                    }
                }
            }

            hpMult = (float)Math.Round(
                1.00f + 0.30f * (worldIndex - 1) + (clampedStage == 4 ? 0.25f : 0.08f * (clampedStage - 1)),
                2);
            dmgMult = (float)Math.Round(
                1.00f + 0.20f * (worldIndex - 1) + 0.05f * (clampedStage - 1),
                2);
        }

        private void RegisterDefaultCatalog()
        {
            // World 1 - Attack on Titan (aligned with Assets/Data/Enemies/W1_*.asset)
            RegisterTemplate(CreateTemplate("w1_pure_titan", 1, MeleeChaseStrategy.ID, 10f, 5f, 5f, 3.5f, 1.5f, false, false));
            RegisterTemplate(CreateTemplate("w1_beast_titan", 1, RangedKiteStrategy.ID, 10f, 3f, 3f, 3.5f, 2.1f, false, false));
            RegisterTemplate(CreateTemplate("w1_female_titan", 1, MeleeChaseStrategy.ID, 50f, 10f, 15f, 2.45f, 1.5f, false, false));
            RegisterTemplate(CreateTemplate("w1_warhammer_boss", 1, BossPhaseStrategy.ID, 150f, 15f, 20f, 2.45f, 1.85f, true, true));

            // World 2 - Dragon Ball Wasteland (aligned with Assets/Data/Enemies/W2_*.asset)
            RegisterTemplate(CreateTemplate("w2_saibamen", 2, MeleeChaseStrategy.ID, 5f, 12f, 12f, 5.25f, 1.3f, false, false));
            RegisterTemplate(CreateTemplate("w2_frieza_soldier", 2, RangedKiteStrategy.ID, 10f, 3f, 3f, 3.5f, 1.7f, false, false));
            RegisterTemplate(CreateTemplate("w2_saibaman_kamikaze", 2, KamikazeRushStrategy.ID, 5f, 12f, 12f, 5.25f, 1.2f, false, false));
            RegisterTemplate(CreateTemplate("w2_nappa", 2, MeleeChaseStrategy.ID, 50f, 10f, 18f, 2.45f, 1.65f, true, false));
            RegisterTemplate(CreateTemplate("w2_broly_boss", 2, BossPhaseStrategy.ID, 150f, 15f, 25f, 3.0f, 1.65f, true, true));

            // World 3 - Diend Neo Ruins (aligned with Assets/Data/Enemies/W3_*.asset)
            RegisterTemplate(CreateTemplate("w3_shocker", 3, MeleeChaseStrategy.ID, 10f, 5f, 5f, 3.5f, 1.25f, false, false));
            RegisterTemplate(CreateTemplate("w3_kasshin", 3, RangedKiteStrategy.ID, 10f, 3f, 3f, 3.5f, 1.65f, false, false));
            RegisterTemplate(CreateTemplate("w3_shadow_moon", 3, MeleeChaseStrategy.ID, 50f, 10f, 15f, 2.45f, 1.45f, false, false));
            RegisterTemplate(CreateTemplate("w3_mecha_boss", 3, BossPhaseStrategy.ID, 150f, 15f, 20f, 1.5f, 1.9f, true, true));
        }

        private static EnemyBlueprint CreateTemplate(
            string enemyId,
            int worldIndex,
            string strategyId,
            float maxHealth,
            float attackDamage,
            float heavyAttackDamage,
            float moveSpeed,
            float attackCooldown,
            bool hasSuperArmor,
            bool isBoss)
        {
            return new EnemyBlueprint
            {
                enemyId = enemyId,
                worldIndex = worldIndex,
                strategyId = strategyId,
                maxHealth = maxHealth,
                attackDamage = attackDamage,
                heavyAttackDamage = heavyAttackDamage,
                moveSpeed = moveSpeed,
                attackCooldown = attackCooldown,
                hasSuperArmor = hasSuperArmor,
                isBoss = isBoss,
                healthMultiplier = 1f,
                damageMultiplier = 1f
            };
        }

        private static EnemyBlueprint CloneBlueprint(EnemyBlueprint src)
        {
            return new EnemyBlueprint
            {
                enemyId = src.enemyId,
                worldIndex = src.worldIndex,
                strategyId = src.strategyId,
                maxHealth = src.maxHealth,
                attackDamage = src.attackDamage,
                heavyAttackDamage = src.heavyAttackDamage,
                moveSpeed = src.moveSpeed,
                attackCooldown = src.attackCooldown,
                hasSuperArmor = src.hasSuperArmor,
                isBoss = src.isBoss,
                healthMultiplier = src.healthMultiplier,
                damageMultiplier = src.damageMultiplier
            };
        }
    }
}
