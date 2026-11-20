namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Interface đa hình cho các đối tượng có thể bị đóng băng (Freeze),
    /// thay thế chuỗi downcast as BaseEnemy/EnemyBase/BossBase trong LordKivaAI.
    /// </summary>
    public interface IFreezable
    {
        void Freeze(float duration);
    }
}
