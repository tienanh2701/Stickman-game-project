using System;
using System.Collections.Generic;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Auth
{
    /// <summary>
    /// Hồ sơ tài khoản người chơi cục bộ, lưu trữ thông tin xác thực đã băm kèm salt
    /// và snapshot tiến trình riêng cho từng tài khoản.
    /// </summary>
    [Serializable]
    public class UserAccountRecord
    {
        public string username;
        public string saltBase64;
        public string passwordHashHex;
        public string createdAtUtc;
        public string lastLoginUtc;

        public int coins;
        public int senzuCount;
        public int spinalFluidCount;
        public int ultraWaterCount;
        public int equippedSlotQ = (int)ConsumableItemType.SenzuBean;
        public int equippedSlotE = (int)ConsumableItemType.SpinalFluid;
        public int unlockedStage = 1;
        public int selectedCharacter;
        public int selectedWorld;
        public int selectedStageIndex;
        public int selectedGameMode;

        public static UserAccountRecord CreateDefault(
            string username,
            string saltBase64,
            string passwordHashHex,
            string utcNowIso)
        {
            return new UserAccountRecord
            {
                username = username,
                saltBase64 = saltBase64,
                passwordHashHex = passwordHashHex,
                createdAtUtc = utcNowIso,
                lastLoginUtc = utcNowIso,
                coins = 0,
                senzuCount = 0,
                spinalFluidCount = 0,
                ultraWaterCount = 0,
                equippedSlotQ = (int)ConsumableItemType.SenzuBean,
                equippedSlotE = (int)ConsumableItemType.SpinalFluid,
                unlockedStage = 1,
                selectedCharacter = 0,
                selectedWorld = 0,
                selectedStageIndex = 0,
                selectedGameMode = 0
            };
        }
    }

    /// <summary>
    /// Cơ sở dữ liệu danh sách tài khoản cục bộ tuần tự hóa qua Unity JsonUtility.
    /// </summary>
    [Serializable]
    public class AccountSaveDatabase
    {
        public int version = 1;
        public List<UserAccountRecord> accounts = new List<UserAccountRecord>();
    }
}
