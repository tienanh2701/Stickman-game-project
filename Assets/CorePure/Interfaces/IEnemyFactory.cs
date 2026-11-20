using StickmanBeatEmUp.CorePure.Entities.DTOs;

namespace StickmanBeatEmUp.CorePure.Interfaces
{
    /// <summary>
    /// Hợp đồng GoF Factory Method sinh bản thiết kế kẻ thù/Boss theo World và Wave từ dữ liệu cấu hình.
    /// </summary>
    public interface IEnemyFactory
    {
        bool CanCreate(string enemyId);

        EnemyBlueprint CreateEnemyBlueprint(string enemyId, int worldIndex, int waveIndex, bool isEndlessMode);
    }
}
