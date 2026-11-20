namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Ngữ cảnh đầu vào thuần C# (không phụ thuộc Transform/Physics2D) cho IAIStrategy tính toán hành động.
    /// </summary>
    public struct EnemyAIContext
    {
        public float EnemyX;
        public float EnemyY;
        public float PlayerX;
        public float PlayerY;
        public float EnemyHp;
        public float EnemyMaxHp;
        public float EnemyRage;
        public float AttackRange;
        public float PreferredRange;
        public float FleeRange;
        public float AttackCooldownRemaining;
        public bool IsPlayerDead;
        public bool IsPlayerAttacking;
        public int LightHitsReceivedInWindow;
        public float DeltaTime;
        public int DifficultyLevel;
    }
}
