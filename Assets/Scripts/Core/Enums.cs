using System;

namespace StickmanBeatEmUp.Core
{
    /// <summary>
    /// Các trạng thái trong Finite State Machine (FSM) của Nhân vật Player
    /// </summary>
    public enum CharacterState
    {
        Idle = 0,
        Run = 1,
        Dash = 2,
        Attack = 3,
        Skill = 4,
        Transform = 5,
        Hurt = 6,
        Dead = 7,
        Block = 8
    }

    /// <summary>
    /// Các chế độ chơi chính thức trong game
    /// </summary>
    public enum GameMode
    {
        Campaign = 0,
        Endless = 1,
        Training = 2
    }

    /// <summary>
    /// 3 vật phẩm tiêu hao bán trong Shop và dùng qua phím Q / E
    /// </summary>
    public enum ConsumableItemType
    {
        None = 0,
        SenzuBean = 1,
        SpinalFluid = 2,
        UltraDivineWater = 3
    }

    /// <summary>
    /// Các trạng thái trong Finite State Machine (FSM) của Kẻ thù
    /// </summary>
    public enum EnemyState
    {
        Idle,
        Chase,
        Flee,
        Telegraph,
        Attack,
        Stunned,
        Hurt,
        Dead
    }

    /// <summary>
    /// Phân loại đòn đánh để tính toán hiệu ứng khựng (Stagger) và Super Armor
    /// </summary>
    public enum AttackType
    {
        NormalAttack_J,       // Đánh thường J (+1 Rage khi trúng)
        Skill_U,              // Kỹ năng U (+1 Rage khi trúng, 0 cost)
        Skill_I,              // Kỹ năng I (Tốn 10 Rage, phá Super Armor nhẹ)
        Combo_DashJ,          // Combo cơ bản Dash + J (Phá Super Armor)
        Combo_DashI,          // Combo tối thượng Dash + I (Phá Super Armor, gây Stun lớn)
        TransformBurst,       // Nổ năng lượng khi biến hình
        EnemyAttack,          // Đòn đánh thường của quái
        EnemyHeavyAttack      // Đòn đánh nặng của Boss/Elite
    }

    /// <summary>
    /// 3 Thế giới chủ đề trong trò chơi
    /// </summary>
    public enum WorldType
    {
        World1_RuinedCity,    // Thế giới Attack on Titan
        World2_Wasteland,     // Thế giới Dragon Ball
        World3_NeoCity        // Thế giới Kamen Rider Diend
    }
}
