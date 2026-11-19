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
            ("A / D", "Di chuyển", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_Move_AD.png"),
            ("W / Space", "Nhảy (S + Space: Rơi xuống thềm)", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_Jump_WSpace.png"),
            ("A/A hoặc D/D", "Lướt (Dash)", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_Dash.png"),
            ("S", "Khiên đỡ đòn", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_BlockShield_S.png"),
            ("J", "Đánh thường", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_NormalAttack_J.png"),
            ("U", "Skill 1", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_Skill1_U.png"),
            ("I", "Skill 2", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_Skill2_I.png"),
            ("Dash + J", "Combo Dash + J", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_Combo_DashJ.png"),
            ("Q / E", "Dùng vật phẩm", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_UseItem_QE.png"),
            ("O", "Biến hình / Tuyệt kỹ (Dash + I: Chiêu cuối)", "Assets/Sprites/UI/SlicedUI/06_InstructionsGuide/Icon_Guide_TransformUltimate_O.png")
        };

        [SerializeField] private InstructionItem[] items = Array.Empty<InstructionItem>();

        [Header("SlicedUI Theme Sprites (06_InstructionsGuide)")]
        [SerializeField] private Sprite mainPanelFrameSprite;
        [SerializeField] private Sprite controlCardSprite;
        [SerializeField] private Sprite keycapBadgeSprite;
        [SerializeField] private Sprite[] guideActionIconSprites = Array.Empty<Sprite>();

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
                if (loaded == null)
                {
                    loaded = GlassHudTheme.LoadSprite(DefaultKeybindRows[i].defaultSpritePath);
                }
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

        public void ConfigureThemeSprites(
            Sprite mainPanel,
            Sprite controlCard,
            Sprite keycapBadge,
            Sprite[] actionIcons = null)
        {
            if (mainPanel != null) mainPanelFrameSprite = mainPanel;
            if (controlCard != null) controlCardSprite = controlCard;
            if (keycapBadge != null) keycapBadgeSprite = keycapBadge;
            if (actionIcons != null && actionIcons.Length > 0)
            {
                guideActionIconSprites = actionIcons;
            }
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

        private void Start()
        {
            ApplySprites();
        }

        private void OnEnable()
        {
            ApplySprites();
        }

        public void ApplySprites()
        {
            ApplyGlassStyling();

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

                Sprite resolvedIcon = ResolveGuideActionSprite(i, item);
                if (resolvedIcon != null)
                {
                    item.actionSprite = resolvedIcon;
                }

                if (item.previewImage == null) continue;
                if (item.actionSprite != null)
                {
                    item.previewImage.sprite = item.actionSprite;
                }
                item.previewImage.type = Image.Type.Simple;
                item.previewImage.preserveAspect = true;
                item.previewImage.color = Color.white;
                item.previewImage.raycastTarget = false;
            }
        }

        private static bool IsExpectedGuideSprite(Sprite sp, string expectedName)
        {
            return sp != null && string.Equals(sp.name, expectedName, StringComparison.OrdinalIgnoreCase);
        }

        private static Sprite ResolveGuideSprite(Sprite current, string relPath, string expectedName)
        {
            if (IsExpectedGuideSprite(current, expectedName))
            {
                return current;
            }
            Sprite loaded = GlassHudTheme.LoadSprite(relPath);
            return loaded != null ? loaded : current;
        }

        private static bool IsLegacyGuideActionSprite(Sprite sp)
        {
            if (sp == null) return true;
            string n = sp.name;
            return n.StartsWith("Goku_", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("FX_BlockShield_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "Icon_SenzuBean", StringComparison.OrdinalIgnoreCase);
        }

        private Sprite ResolveGuideActionSprite(int index, InstructionItem item)
        {
            if (index >= 0 && index < DefaultKeybindRows.Length)
            {
                if (item.actionSprite != null && !IsLegacyGuideActionSprite(item.actionSprite))
                {
                    return item.actionSprite;
                }
                if (guideActionIconSprites != null && index < guideActionIconSprites.Length && guideActionIconSprites[index] != null)
                {
                    return guideActionIconSprites[index];
                }
                Sprite loaded = GlassHudTheme.LoadSprite(DefaultKeybindRows[index].defaultSpritePath);
                if (loaded != null)
                {
                    return loaded;
                }
            }
            return item.actionSprite;
        }

        private void ApplyGlassStyling()
        {
            mainPanelFrameSprite = ResolveGuideSprite(mainPanelFrameSprite, "06_InstructionsGuide/Frame_Guide_MainPanel_Clean", "Frame_Guide_MainPanel_Clean");
            controlCardSprite = ResolveGuideSprite(controlCardSprite, "06_InstructionsGuide/Row_Guide_ControlCard_WithIconBox_Clean", "Row_Guide_ControlCard_WithIconBox_Clean");
            keycapBadgeSprite = ResolveGuideSprite(keycapBadgeSprite, "06_InstructionsGuide/Badge_Guide_Keycap_Clean", "Badge_Guide_Keycap_Clean");

            if (transform.parent != null)
            {
                Transform titleTf = transform.parent.Find("InstrTitle");
                if (titleTf != null && titleTf.TryGetComponent(out RectTransform titleRt))
                {
                    titleRt.anchoredPosition = new Vector2(0f, 452f);
                    titleRt.sizeDelta = new Vector2(1200f, 64f);
                }
            }

            if (TryGetComponent(out RectTransform containerRt))
            {
                containerRt.anchoredPosition = new Vector2(0f, -25f);
                containerRt.sizeDelta = new Vector2(1660f, 800f);
            }

            if (TryGetComponent(out Image containerImg))
            {
                if (mainPanelFrameSprite != null)
                {
                    GlassHudTheme.ApplySlicedSprite(containerImg, mainPanelFrameSprite, 1660f, 800f, Color.white);
                    if (TryGetComponent(out Outline containerOutline))
                    {
                        containerOutline.enabled = false;
                    }
                }
            }

            if (items == null) return;
            for (int i = 0; i < items.Length; i++)
            {
                InstructionItem item = items[i];
                if (item == null) continue;

                bool isUltimateRow = i == items.Length - 1;
                Transform rowTf = ResolveRowTransform(i, item);
                if (rowTf != null)
                {
                    if (rowTf.TryGetComponent(out RectTransform rowRt))
                    {
                        int col = i < 10 ? (i / 5) : (i % 2);
                        int row = i < 10 ? (i % 5) : (i / 2);
                        float x = col == 0 ? -390f : 390f;
                        float y = 272f - row * 136f;
                        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
                        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
                        rowRt.pivot = new Vector2(0.5f, 0.5f);
                        rowRt.anchoredPosition = new Vector2(x, y);
                        rowRt.sizeDelta = new Vector2(752f, 120f);
                    }

                    if (rowTf.TryGetComponent(out Image rowBg) && controlCardSprite != null)
                    {
                        Color rowTint = isUltimateRow ? new Color(1f, 0.94f, 0.82f, 1f) : Color.white;
                        GlassHudTheme.ApplySlicedSprite(rowBg, controlCardSprite, 752f, 120f, rowTint);
                    }
                }

                Transform spriteFrameTf = rowTf != null ? rowTf.Find("SpriteFrame") : null;
                if (spriteFrameTf == null && item.previewImage != null && item.previewImage.transform.parent != rowTf)
                {
                    spriteFrameTf = item.previewImage.transform.parent;
                }
                if (spriteFrameTf != null)
                {
                    if (spriteFrameTf.TryGetComponent(out RectTransform frameRt))
                    {
                        frameRt.anchorMin = new Vector2(0f, 0.5f);
                        frameRt.anchorMax = new Vector2(0f, 0.5f);
                        frameRt.pivot = new Vector2(0f, 0.5f);
                        frameRt.anchoredPosition = new Vector2(15f, 0f);
                        frameRt.sizeDelta = new Vector2(88f, 90f);
                    }
                    if (spriteFrameTf.TryGetComponent(out Image frameImg))
                    {
                        frameImg.sprite = null;
                        frameImg.color = Color.clear;
                        frameImg.raycastTarget = false;
                    }
                }

                if (item.previewImage != null)
                {
                    RectTransform iconRt = item.previewImage.rectTransform;
                    iconRt.anchorMin = Vector2.zero;
                    iconRt.anchorMax = Vector2.one;
                    iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.offsetMin = Vector2.zero;
                    iconRt.offsetMax = Vector2.zero;
                }

                Transform keyBadgeTf = rowTf != null ? (rowTf.Find("KeyBadge") ?? rowTf.Find("KeyPill")) : null;
                if (keyBadgeTf == null && item.keyLabel != null && item.keyLabel.transform.parent != rowTf)
                {
                    keyBadgeTf = item.keyLabel.transform.parent;
                }
                if (keyBadgeTf != null)
                {
                    if (keyBadgeTf.TryGetComponent(out RectTransform keyRt))
                    {
                        keyRt.anchorMin = new Vector2(0f, 0.5f);
                        keyRt.anchorMax = new Vector2(0f, 0.5f);
                        keyRt.pivot = new Vector2(0f, 0.5f);
                        keyRt.anchoredPosition = new Vector2(126f, 0f);
                        keyRt.sizeDelta = new Vector2(180f, 52f);
                    }
                    if (keyBadgeTf.TryGetComponent(out Image keyBg) && keycapBadgeSprite != null)
                    {
                        Color keyTint = isUltimateRow ? new Color(1f, 0.90f, 0.72f, 1f) : Color.white;
                        GlassHudTheme.ApplySlicedSprite(keyBg, keycapBadgeSprite, 180f, 52f, keyTint);
                    }
                }

                if (item.keyLabel != null)
                {
                    RectTransform kTxtRt = item.keyLabel.rectTransform;
                    kTxtRt.sizeDelta = new Vector2(170f, 46f);
                    Color keyColor = isUltimateRow
                        ? new Color(1f, 0.86f, 0.34f, 1f)
                        : new Color(0.96f, 0.98f, 1f, 1f);
                    StyleLegacyLabelCrisp(item.keyLabel, keyColor);
                }

                if (item.descriptionLabel != null)
                {
                    RectTransform descRt = item.descriptionLabel.rectTransform;
                    descRt.anchorMin = new Vector2(0f, 0.5f);
                    descRt.anchorMax = new Vector2(0f, 0.5f);
                    descRt.pivot = new Vector2(0f, 0.5f);
                    descRt.anchoredPosition = new Vector2(320f, 0f);
                    descRt.sizeDelta = new Vector2(412f, 104f);
                    item.descriptionLabel.fontSize = 22;
                    item.descriptionLabel.alignment = TextAnchor.MiddleLeft;
                    item.descriptionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                    StyleLegacyLabelCrisp(item.descriptionLabel, Color.white);
                }
            }
        }

        private Transform ResolveRowTransform(int index, InstructionItem item)
        {
            if (item.descriptionLabel != null && item.descriptionLabel.transform.parent != null)
            {
                return item.descriptionLabel.transform.parent;
            }
            if (item.previewImage != null && item.previewImage.transform.parent != null)
            {
                Transform frame = item.previewImage.transform.parent;
                if (frame.parent != null && frame.parent != transform)
                {
                    return frame.parent;
                }
            }
            if (item.keyLabel != null && item.keyLabel.transform.parent != null)
            {
                Transform keyParent = item.keyLabel.transform.parent;
                if (keyParent.parent != null && keyParent.parent != transform)
                {
                    return keyParent.parent;
                }
            }
            return transform.Find($"InstrRow_{index}");
        }

        private static void StyleLegacyLabelCrisp(Text txt, Color targetColor)
        {
            if (txt == null) return;
            GlassHudTheme.StyleLegacyText(txt, -1, targetColor, bold: true, addShadow: true);
            if (txt.GetComponent<Outline>() == null)
            {
                Outline outline = txt.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }
    }
}
