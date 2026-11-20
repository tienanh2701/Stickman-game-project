namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Kết quả quyết định từ IAIStrategy để tầng Adapter (EnemyBase/BossBase) thực thi trên Unity.
    /// </summary>
    public struct AIActionDecision
    {
        public AIActionType ActionType;
        public float MoveDirectionX;
        public bool ShouldFacePlayer;
        public bool ShouldActivateShield;

        public AIActionDecision(
            AIActionType actionType,
            float moveDirectionX = 0f,
            bool shouldFacePlayer = true,
            bool shouldActivateShield = false)
        {
            ActionType = actionType;
            MoveDirectionX = moveDirectionX;
            ShouldFacePlayer = shouldFacePlayer;
            ShouldActivateShield = shouldActivateShield;
        }

        public static AIActionDecision IdleDecision => new AIActionDecision(AIActionType.Idle, 0f, false, false);
    }
}
