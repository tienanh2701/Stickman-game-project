namespace StickmanBeatEmUp.CorePure.Entities.DTOs
{
    /// <summary>
    /// Kết quả thuần C# trả về sau khi IItemEffect xử lý một vật phẩm tiêu hao.
    /// </summary>
    public struct ItemEffectResult
    {
        public bool Applied;
        public float HealAmount;
        public float RageDelta;
        public float ShieldAmount;
        public float ReflectDamage;
        public float BuffDuration;
        public float DamageMultiplier;
        public float SpeedMultiplier;
        public float RagePerSecond;
        public bool ClearNegativeEffects;
        public string FeedbackMessage;

        public static ItemEffectResult Rejected(string reason)
        {
            return new ItemEffectResult
            {
                Applied = false,
                DamageMultiplier = 1f,
                SpeedMultiplier = 1f,
                FeedbackMessage = reason ?? string.Empty
            };
        }
    }
}
