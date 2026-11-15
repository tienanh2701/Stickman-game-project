using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StickmanBeatEmUp.UI
{
    [Serializable]
    public class InstructionItem
    {
        public string keyCombo;
        public string actionDescription;
        public Image previewImage;
        public Sprite actionSprite;
        public Text keyLabel;
        public Text descriptionLabel;
    }

    public class InstructionsUI : MonoBehaviour
    {
        public static readonly (string keyCombo, string actionDescription, string defaultSpritePath)[] DefaultKeybindRows =
        {
            ("A / D", "Di chuyển", "Assets/Sprites/Characters/Goku/Goku_Run_2.png"),
            ("W / Space", "Nhảy (S + Space: Rơi xuống thềm)", "Assets/Sprites/Characters/Goku/Goku_Idle_1.png"),
            ("A/A hoặc D/D", "Lướt (Dash)", "Assets/Sprites/Characters/Goku/Goku_Dash_0.png"),
            ("S", "Khiên đỡ đòn", "Assets/Sprites/Combat/FX_BlockShield_Cyan.png"),
            ("J", "Đánh thường", "Assets/Sprites/Characters/Goku/Goku_Punch_2.png"),
            ("U", "Skill 1", "Assets/Sprites/Characters/Goku/Goku_KiBlast_3.png"),
            ("I", "Skill 2", "Assets/Sprites/Characters/Goku/Goku_Kaioken_3.png"),
            ("Dash + J", "Combo Dash + J", "Assets/Sprites/Characters/Goku/Goku_DashJ_3.png"),
            ("Q / E", "Dùng vật phẩm", "Assets/Sprites/UI/Items/Icon_SenzuBean.png"),
            ("O", "Biến hình / Tuyệt kỹ (Dash + I: Chiêu cuối)", "Assets/Sprites/Characters/Goku/Goku_GenkiDama_3.png")
        };

        [SerializeField] private InstructionItem[] items = Array.Empty<InstructionItem>();

        public InstructionItem[] Items => items;

        public static InstructionItem[] CreateDefaultInstructionItems()
        {
            InstructionItem[] defaults = new InstructionItem[DefaultKeybindRows.Length];
            for (int i = 0; i < DefaultKeybindRows.Length; i++)
            {
                Sprite loaded = null;
#if UNITY_EDITOR
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(DefaultKeybindRows[i].defaultSpritePath);
#endif
                defaults[i] = new InstructionItem
                {
                    keyCombo = DefaultKeybindRows[i].keyCombo,
                    actionDescription = DefaultKeybindRows[i].actionDescription,
                    actionSprite = loaded
                };
            }
            return defaults;
        }

        public void ConfigureItems(InstructionItem[] configuredItems)
        {
            items = configuredItems ?? Array.Empty<InstructionItem>();
            ApplySprites();
        }

        public void EnsureCoreKeybindsPresent(Sprite blockSprite = null, Sprite itemSprite = null)
        {
            List<InstructionItem> list = items != null
                ? new List<InstructionItem>(items)
                : new List<InstructionItem>();

            bool hasBlock = false;
            bool hasConsumable = false;

            for (int i = 0; i < list.Count; i++)
            {
                InstructionItem item = list[i];
                if (item == null || string.IsNullOrEmpty(item.keyCombo)) continue;

                string normalized = item.keyCombo.Trim().ToUpperInvariant();
                if (normalized == "S" || normalized.Contains("PHÍM S"))
                {
                    hasBlock = true;
                }
                if (normalized.Contains("Q / E") || normalized.Contains("Q/E"))
                {
                    hasConsumable = true;
                }
            }

            if (!hasBlock)
            {
                list.Add(new InstructionItem
                {
                    keyCombo = "S",
                    actionDescription = "Khiên đỡ đòn",
                    actionSprite = blockSprite
                });
            }

            if (!hasConsumable)
            {
                list.Add(new InstructionItem
                {
                    keyCombo = "Q / E",
                    actionDescription = "Dùng vật phẩm",
                    actionSprite = itemSprite
                });
            }

            items = list.ToArray();
            ApplySprites();
        }

        private void Awake()
        {
            if (items == null || items.Length == 0)
            {
                items = CreateDefaultInstructionItems();
            }
            ApplySprites();
        }

        private void OnEnable()
        {
            ApplySprites();
        }

        public void ApplySprites()
        {
            if (items == null) return;
            for (int i = 0; i < items.Length; i++)
            {
                InstructionItem item = items[i];
                if (item == null) continue;

                if (item.keyLabel != null && !string.IsNullOrEmpty(item.keyCombo))
                {
                    item.keyLabel.text = item.keyCombo;
                }
                if (item.descriptionLabel != null && !string.IsNullOrEmpty(item.actionDescription))
                {
                    item.descriptionLabel.text = item.actionDescription;
                }

                if (item.previewImage == null) continue;
                if (item.actionSprite != null)
                {
                    item.previewImage.sprite = item.actionSprite;
                }
                item.previewImage.preserveAspect = true;
                item.previewImage.color = Color.white;
            }
        }
    }
}
