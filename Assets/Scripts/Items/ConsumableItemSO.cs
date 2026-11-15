using UnityEngine;

namespace StickmanBeatEmUp.Items
{
    /// <summary>
    /// ScriptableObject cấu hình dữ liệu cho 3 vật phẩm tiêu hao (SenzuBean, SpinalFluid, UltraDivineWater).
    /// </summary>
    [CreateAssetMenu(fileName = "NewConsumableItem", menuName = "Stickman BeatEmUp/Consumable Item SO")]
    public class ConsumableItemSO : ScriptableObject
    {
        [Header("Identity & Shop")]
        public StickmanBeatEmUp.Core.ConsumableItemType itemType = StickmanBeatEmUp.Core.ConsumableItemType.SenzuBean;
        public string itemName;
        [TextArea(2, 4)] public string description;
        public int price = 30;
        public Sprite icon;

        [Header("Visual Effects")]
        public Sprite[] vfxFrames;
        public float vfxFps = 8f;

        [Header("Combat Effects")]
        public float healAmount = 10f;
        public float buffDuration = 0f;
        public float shieldAmount = 0f;
        public float reflectDamage = 0f;
        public float damageMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float ragePerSecond = 0f;

        /// <summary>
        /// Tạo cấu hình mặc định tại runtime nếu chưa gán asset trong Resources/Items.
        /// </summary>
        public static ConsumableItemSO CreateRuntimeDefault(StickmanBeatEmUp.Core.ConsumableItemType type)
        {
            ConsumableItemSO item = CreateInstance<ConsumableItemSO>();
            item.itemType = type;

            switch (type)
            {
                case StickmanBeatEmUp.Core.ConsumableItemType.SenzuBean:
                    item.name = "Item_SenzuBean";
                    item.itemName = "Đậu Thần";
                    item.description = "Hồi 10 HP và xóa toàn bộ hiệu ứng bất lợi ngay lập tức.";
                    item.price = 30;
                    item.vfxFps = 8f;
                    item.healAmount = 10f;
                    item.buffDuration = 0f;
                    item.shieldAmount = 0f;
                    item.reflectDamage = 0f;
                    item.damageMultiplier = 1f;
                    item.speedMultiplier = 1f;
                    item.ragePerSecond = 0f;
                    break;

                case StickmanBeatEmUp.Core.ConsumableItemType.SpinalFluid:
                    item.name = "Item_SpinalFluid";
                    item.itemName = "Dịch Tủy Titan";
                    item.description = "Tạo giáp ảo 40 HP và phản 5 sát thương khi bị đánh trong 30 giây.";
                    item.price = 60;
                    item.vfxFps = 8f;
                    item.healAmount = 0f;
                    item.buffDuration = 30f;
                    item.shieldAmount = 40f;
                    item.reflectDamage = 5f;
                    item.damageMultiplier = 1f;
                    item.speedMultiplier = 1f;
                    item.ragePerSecond = 0f;
                    break;

                case StickmanBeatEmUp.Core.ConsumableItemType.UltraDivineWater:
                    item.name = "Item_UltraDivineWater";
                    item.itemName = "Nước Siêu Thần";
                    item.description = "Tăng 50% sát thương, +30% tốc độ và hồi +2 Nộ/giây trong 60 giây.";
                    item.price = 50;
                    item.vfxFps = 8f;
                    item.healAmount = 0f;
                    item.buffDuration = 60f;
                    item.shieldAmount = 0f;
                    item.reflectDamage = 0f;
                    item.damageMultiplier = 1.5f;
                    item.speedMultiplier = 1.3f;
                    item.ragePerSecond = 2f;
                    break;

                default:
                    item.name = "Item_None";
                    item.itemName = "Trống";
                    item.description = string.Empty;
                    item.price = 0;
                    item.healAmount = 0f;
                    break;
            }

            PopulateDefaultSpritesInEditor(item);
            return item;
        }

        /// <summary>
        /// Hỗ trợ nạp sprite mặc định trong Unity Editor khi chưa build asset ScriptableObject.
        /// </summary>
        public static void PopulateDefaultSpritesInEditor(ConsumableItemSO item)
        {
            if (item == null) return;
#if UNITY_EDITOR
            switch (item.itemType)
            {
                case StickmanBeatEmUp.Core.ConsumableItemType.SenzuBean:
                    if (item.icon == null)
                    {
                        item.icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Items/Icon_SenzuBean.png");
                    }
                    if (item.vfxFrames == null || item.vfxFrames.Length == 0)
                    {
                        item.vfxFrames = LoadSpriteSequence(
                            "Assets/Sprites/Combat/Items/VFX_SenzuHeal_0.png",
                            "Assets/Sprites/Combat/Items/VFX_SenzuHeal_1.png",
                            "Assets/Sprites/Combat/Items/VFX_SenzuHeal_2.png");
                    }
                    break;

                case StickmanBeatEmUp.Core.ConsumableItemType.SpinalFluid:
                    if (item.icon == null)
                    {
                        item.icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Items/Icon_SpinalFluid.png");
                    }
                    if (item.vfxFrames == null || item.vfxFrames.Length == 0)
                    {
                        item.vfxFrames = LoadSpriteSequence(
                            "Assets/Sprites/Combat/Items/VFX_SpinalRibcage_0.png",
                            "Assets/Sprites/Combat/Items/VFX_SpinalRibcage_1.png");
                    }
                    break;

                case StickmanBeatEmUp.Core.ConsumableItemType.UltraDivineWater:
                    if (item.icon == null)
                    {
                        item.icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Items/Icon_UltraDivineWater.png");
                    }
                    if (item.vfxFrames == null || item.vfxFrames.Length == 0)
                    {
                        item.vfxFrames = LoadSpriteSequence(
                            "Assets/Sprites/Combat/Items/VFX_UltraWaterAura_0.png",
                            "Assets/Sprites/Combat/Items/VFX_UltraWaterAura_1.png",
                            "Assets/Sprites/Combat/Items/VFX_UltraWaterAura_2.png");
                    }
                    break;
            }
#endif
        }

#if UNITY_EDITOR
        private static Sprite[] LoadSpriteSequence(params string[] paths)
        {
            if (paths == null || paths.Length == 0) return System.Array.Empty<Sprite>();
            System.Collections.Generic.List<Sprite> loaded = new System.Collections.Generic.List<Sprite>(paths.Length);
            for (int i = 0; i < paths.Length; i++)
            {
                Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
                if (sp != null)
                {
                    loaded.Add(sp);
                }
            }
            return loaded.ToArray();
        }
#endif
    }
}
