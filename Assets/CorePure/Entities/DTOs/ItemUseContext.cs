namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Trạng thái nhân vật tại thời điểm người chơi kích hoạt vật phẩm tiêu hao (phím Q / E).
    /// </summary>
    public struct ItemUseContext
    {
        public float CurrentHp;
        public float MaxHp;
        public float CurrentRage;
        public float MaxRage;
        public int QuantityOwned;
        public bool IsPlayerDead;
        public bool HasActiveSpinalBuff;
        public bool HasActiveUltraWaterBuff;
    }
}
