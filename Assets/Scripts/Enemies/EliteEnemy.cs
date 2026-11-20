using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Economy;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Mid-boss Elite tier inheriting EnemyBase.
    /// Defaults to Super Armor (light J attacks do not interrupt) and uses 6-state Elite Animator indices (4=Hit/Parry, 5=Death).
    /// </summary>
    public abstract class EliteEnemy : EnemyBase
    {
        protected float nextSpecialTime;

        public override bool IsEliteOrBoss => true;

        protected override void Awake()
        {
            base.Awake();
            lockToGroundY = true;
        }

        protected override void Start()
        {
            lockToGroundY = true;
            base.Start();
        }

        protected override int GetHitAnimState() => 4;
        protected override int GetDeathAnimState() => 5;

        protected override int GetKillCoinReward()
        {
            if (enemyData != null && enemyData.isBoss)
            {
                return CurrencyManager.REWARD_BOSS;
            }
            return CurrencyManager.REWARD_ELITE;
        }

        public override void TakeDamage(DamageData damageData)
        {
            base.TakeDamage(damageData);
        }
    }
}
