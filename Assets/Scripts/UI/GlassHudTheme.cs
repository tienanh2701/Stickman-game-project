using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Shared Dark Glassmorphism & Sci-Fi HUD sprite loader, 9-slice applier,
    /// filled energy bar helper, and Vietnamese-safe TMP/Legacy typography styler.
    /// </summary>
    public static class GlassHudTheme
    {
        // ----------------------------------------------------------------
        // Sci-Fi Palette — matches MenuGlobalBackground.png void/void-cyan
        // ----------------------------------------------------------------

        /// <summary>Deep obsidian-violet void background (#0A0914).</summary>
        public static readonly Color SciFiVoidColor = new Color(0.039f, 0.035f, 0.078f, 1f);

        /// <summary>Neon Cyan rim accent (#22D3EE).</summary>
        public static readonly Color SciFiCyanRim = new Color(0.133f, 0.827f, 0.933f, 1f);

        /// <summary>Electric Violet accent (#8B5CF6).</summary>
        public static readonly Color SciFiVioletAccent = new Color(0.545f, 0.361f, 0.965f, 1f);

        /// <summary>Solar Gold / Amber Neon primary (#F59E0B).</summary>
        public static readonly Color SciFiGoldPrimary = new Color(0.961f, 0.620f, 0.043f, 1f);

        /// <summary>Plasma Crimson danger (#FB3768).</summary>
        public static readonly Color SciFiCrimsonDanger = new Color(0.984f, 0.216f, 0.408f, 1f);

        // ----------------------------------------------------------------
        private static readonly string[] RelativeSearchFolders =
        {
            "Sprites/UI/SlicedUI/01_MainMenu/",
            "Sprites/UI/SlicedUI/02_ModeSelect/",
            "Sprites/UI/SlicedUI/03_CharacterAndItemSelect/",
            "Sprites/UI/SlicedUI/04_AuthRegisterLogin/",
            "Sprites/UI/SlicedUI/05_Shop/",
            "Sprites/UI/SlicedUI/06_InstructionsGuide/",
            "Sprites/UI/SlicedUI/07_BattleVitalsHUD/",
            "Sprites/UI/SlicedUI/08_BattleTopHUD/",
            "Sprites/UI/SlicedUI/09_TrainingSpawnerDock/",
            "Sprites/UI/GlassHUD/",
            "Sprites/UI/Glassmorphism/",
            "Sprites/UI/Items/",
            "Sprites/UI/"
        };

        private static readonly string[] KnownInputPrefixes =
        {
            "Assets/Sprites/UI/SlicedUI/",
            "Assets/Sprites/UI/GlassHUD/",
            "Assets/Sprites/UI/Glassmorphism/",
            "Assets/Sprites/UI/Items/",
            "Assets/Sprites/UI/",
            "Sprites/UI/SlicedUI/",
            "Sprites/UI/GlassHUD/",
            "Sprites/UI/Glassmorphism/",
            "Sprites/UI/Items/",
            "Sprites/UI/",
            "SlicedUI/",
            "GlassHUD/",
            "Glassmorphism/",
            "Items/"
        };

        private static readonly Dictionary<string, Vector4> DefaultSliceBorders =
            new Dictionary<string, Vector4>(StringComparer.OrdinalIgnoreCase)
            {
                { "VitalsCluster_Backplate_Glass.png", new Vector4(28f, 28f, 28f, 28f) },
                { "HPBar_Frame_Glass.png", Vector4.zero },
                { "HPBar_Fill_CrimsonGlow.png", Vector4.zero },
                { "HPBar_Trail_WhiteAmber.png", Vector4.zero },
                { "ShieldBar_Fill_CyanGlass.png", Vector4.zero },
                { "RageSegment_Empty_Glass.png", Vector4.zero },
                { "RageSegment_Fill_GoldAmber.png", Vector4.zero },
                { "RageSegment_FullGlow_Rim.png", Vector4.zero },
                { "BossHPBar_Frame.png", new Vector4(48f, 20f, 48f, 20f) },
                { "BossHPBar_Fill_PurpleRed.png", Vector4.zero },
                { "WorldHPBar_Frame_Dark.png", new Vector4(8f, 8f, 8f, 8f) },
                { "WorldHPBar_Fill_Red.png", Vector4.zero },
                { "StatusPill_Emerald.png", new Vector4(26f, 0f, 26f, 0f) },
                { "StatusPill_CyanGlow.png", new Vector4(28f, 0f, 28f, 0f) },
                { "WaveBanner_TechGlass.png", new Vector4(56f, 24f, 56f, 24f) },
                { "TopRight_InfoCluster_Glass.png", new Vector4(28f, 24f, 28f, 24f) },
                { "CurrencyBadge_GoldGlass.png", new Vector4(32f, 0f, 32f, 0f) },
                { "ControlHints_PillStrip.png", new Vector4(27f, 0f, 27f, 0f) },
                { "TrainingDock_GlassPanel.png", new Vector4(28f, 28f, 28f, 28f) },
                { "MiniGlassCard_SpawnSlot.png", new Vector4(18f, 18f, 18f, 18f) },
                { "ModalCard_DarkGlass_Cyan.png", new Vector4(36f, 36f, 36f, 36f) },
                { "ModalCard_Victory_Gold.png", new Vector4(38f, 38f, 38f, 38f) },
                { "ModalCard_Defeat_Crimson.png", new Vector4(38f, 38f, 38f, 38f) },
                { "WorldCard_AoT_EmeraldAmber.png", new Vector4(32f, 32f, 32f, 32f) },
                { "WorldCard_DB_AmberGold.png", new Vector4(32f, 32f, 32f, 32f) },
                { "WorldCard_Diend_MagentaCyan.png", new Vector4(32f, 32f, 32f, 32f) },
                { "PillBtn_OrangePrimary.png", new Vector4(44f, 0f, 44f, 0f) },
                { "PillBtn_GlassNeutral.png", new Vector4(42f, 0f, 42f, 0f) },
                { "PillBtn_RedDanger.png", new Vector4(42f, 0f, 42f, 0f) },
                { "PillBtn_GreenConfirm.png", new Vector4(42f, 0f, 42f, 0f) },
                { "TogglePill_On_Green.png", new Vector4(28f, 0f, 28f, 0f) },
                { "TogglePill_On_Orange.png", new Vector4(28f, 0f, 28f, 0f) },
                { "TogglePill_Off_DarkGlass.png", new Vector4(28f, 0f, 28f, 0f) },
                { "Keycap_Badge_Dark.png", new Vector4(14f, 14f, 14f, 14f) },
                { "HudOrb_Frame_GoldGlass.png", Vector4.zero },
                { "MiniPill_GoldBadge.png", new Vector4(20f, 0f, 20f, 0f) },
                { "Icon_Heart_Crimson.png", Vector4.zero },
                { "Icon_Shield_Emerald.png", Vector4.zero },
                { "Icon_Bolt_Cyan.png", Vector4.zero },
                { "Icon_Pause_Bars.png", Vector4.zero },
                { "Icon_Exit_Door.png", Vector4.zero },
                { "Icon_Trash_Red.png", Vector4.zero },
                // Glassmorphism sliced sprites
                { "PillButton_OrangeGlow.png", new Vector4(56f, 0f, 56f, 0f) },
                { "PillButton_SubtleGlass.png", new Vector4(52f, 0f, 52f, 0f) },
                { "TechCut_ActionButton.png", new Vector4(36f, 28f, 36f, 28f) },
                { "SoftGradient_GreenBuyBtn.png", new Vector4(36f, 0f, 36f, 0f) },
                { "Card3D_Blue.png", new Vector4(36f, 44f, 36f, 36f) },
                { "Card3D_Red.png", new Vector4(36f, 44f, 36f, 36f) },
                { "Card3D_Green.png", new Vector4(36f, 44f, 36f, 36f) },
                { "Card3D_BronzeGold.png", new Vector4(36f, 44f, 36f, 36f) },
                { "CharSlot_Glass.png", new Vector4(28f, 28f, 28f, 28f) },
                { "CharSlot_GoldSelected.png", new Vector4(28f, 28f, 28f, 28f) },
                { "CurrencyBadge_TopRight.png", new Vector4(36f, 0f, 36f, 0f) },
                { "ShopRow_GlassStrip.png", new Vector4(32f, 24f, 32f, 24f) },
                // SlicedUI 01_MainMenu
                { "Btn_MainMenu_Guide_Clean.png", new Vector4(420f, 100f, 420f, 100f) },
                { "Btn_MainMenu_Guide_Labeled.png", new Vector4(160f, 100f, 160f, 100f) },
                { "Btn_MainMenu_Play_Clean.png", new Vector4(420f, 100f, 420f, 100f) },
                { "Btn_MainMenu_Play_Labeled.png", new Vector4(160f, 100f, 160f, 100f) },
                { "Btn_MainMenu_Quit_Clean.png", new Vector4(420f, 100f, 420f, 100f) },
                { "Btn_MainMenu_Quit_Labeled.png", new Vector4(160f, 100f, 160f, 100f) },
                // SlicedUI 02_ModeSelect
                { "Badge_ModeSelect_CoinTopRight_Blank.png", new Vector4(75f, 32f, 75f, 32f) },
                { "Badge_ModeSelect_CoinTopRight_Clean.png", new Vector4(125f, 32f, 75f, 32f) },
                { "Btn_ModeSelect_Back_Clean.png", new Vector4(75f, 32f, 75f, 32f) },
                { "Btn_ModeSelect_Back_Labeled.png", new Vector4(75f, 32f, 75f, 32f) },
                { "Card_Mode_Campaign_BlankFrame.png", new Vector4(118f, 118f, 118f, 118f) },
                { "Card_Mode_Campaign_Clean.png", new Vector4(118f, 118f, 118f, 118f) },
                { "Card_Mode_Campaign_Labeled.png", new Vector4(80f, 80f, 80f, 80f) },
                { "Card_Mode_Endless_BlankFrame.png", new Vector4(118f, 118f, 118f, 118f) },
                { "Card_Mode_Endless_Clean.png", new Vector4(118f, 118f, 118f, 118f) },
                { "Card_Mode_Endless_Labeled.png", new Vector4(80f, 80f, 80f, 80f) },
                { "Card_Mode_Shop_BlankFrame.png", new Vector4(118f, 118f, 118f, 118f) },
                { "Card_Mode_Shop_Clean.png", new Vector4(118f, 118f, 118f, 118f) },
                { "Card_Mode_Shop_Labeled.png", new Vector4(80f, 80f, 80f, 80f) },
                { "Card_Mode_Training_BlankFrame.png", new Vector4(118f, 118f, 118f, 118f) },
                { "Card_Mode_Training_Clean.png", new Vector4(118f, 118f, 118f, 118f) },
                { "Card_Mode_Training_Labeled.png", new Vector4(80f, 80f, 80f, 80f) },
                // SlicedUI 03_CharacterAndItemSelect
                { "Badge_CharSelect_TopRightFrame_Clean.png", new Vector4(90f, 32f, 90f, 32f) },
                { "Btn_CharSelect_Back_Clean.png", new Vector4(42f, 28f, 42f, 28f) },
                { "Btn_CharSelect_Continue_Clean.png", new Vector4(84f, 36f, 84f, 36f) },
                { "Frame_CharSelect_MainPanel_Clean.png", new Vector4(96f, 80f, 96f, 80f) },
                { "Frame_ItemLoadout_Panel_Clean.png", new Vector4(380f, 48f, 380f, 68f) },
                { "Slot_CharSelect_ComingSoon_Clean.png", new Vector4(32f, 32f, 32f, 32f) },
                { "Slot_CharSelect_SelectedGold_Clean.png", new Vector4(64f, 64f, 64f, 64f) },
                { "Slot_CharSelect_Unselected_Clean.png", new Vector4(32f, 32f, 32f, 32f) },
                // SlicedUI 04_AuthRegisterLogin
                { "Btn_Auth_BackSecondary_Clean.png", new Vector4(150f, 36f, 150f, 36f) },
                { "Btn_Auth_RegisterPrimary_Clean.png", new Vector4(170f, 56f, 170f, 56f) },
                { "Frame_Auth_ModalPanel_Clean.png", new Vector4(340f, 80f, 340f, 80f) },
                { "Input_Auth_Field_Blank.png", new Vector4(240f, 44f, 360f, 44f) },
                { "Input_Auth_Password_Clean.png", new Vector4(240f, 44f, 360f, 44f) },
                { "Input_Auth_Username_Clean.png", new Vector4(240f, 44f, 360f, 44f) },
                { "Tab_Auth_Login_Inactive_Clean.png", new Vector4(190f, 42f, 190f, 42f) },
                { "Tab_Auth_Register_ActiveGold_Clean.png", new Vector4(165f, 60f, 165f, 60f) },
                // SlicedUI 05_Shop
                { "Badge_Shop_TopRightPill_Clean.png", new Vector4(90f, 32f, 90f, 32f) },
                { "Bar_Shop_InventorySummary_Clean.png", new Vector4(60f, 24f, 60f, 24f) },
                { "Btn_Shop_Back_Clean.png", new Vector4(80f, 32f, 80f, 32f) },
                { "Btn_Shop_Buy_Clean.png", new Vector4(115f, 36f, 115f, 36f) },
                { "Frame_Shop_MainPanel_Clean.png", new Vector4(240f, 120f, 240f, 120f) },
                { "Row_Shop_ItemStrip_Clean.png", new Vector4(64f, 48f, 64f, 48f) },
                { "Row_Shop_ItemStrip_WithIconSlot_Clean.png", new Vector4(300f, 48f, 64f, 48f) },
                { "Slot_Shop_ItemIcon_BlueGlow.png", new Vector4(36f, 36f, 36f, 36f) },
                // SlicedUI 06_InstructionsGuide
                { "Badge_Guide_Keycap_Clean.png", new Vector4(42f, 26f, 42f, 26f) },
                { "Frame_Guide_MainPanel_Clean.png", new Vector4(220f, 130f, 220f, 130f) },
                { "Row_Guide_ControlCard_Clean.png", new Vector4(64f, 44f, 410f, 44f) },
                { "Row_Guide_ControlCard_WithIconBox_Clean.png", new Vector4(230f, 44f, 410f, 44f) },
                // SlicedUI 07_BattleVitalsHUD
                { "Bar_HUD_HP_Fill_RedOrange_Clean.png", new Vector4(80f, 16f, 24f, 16f) },
                { "Bar_HUD_HP_Frame_Clean.png", new Vector4(65f, 30f, 65f, 30f) },
                { "Bar_HUD_Rage_3SegFrame_Clean.png", new Vector4(60f, 24f, 60f, 24f) },
                { "Bar_HUD_Rage_SegmentCyanGlow_Clean.png", new Vector4(24f, 18f, 24f, 18f) },
                { "Bar_HUD_Rage_SegmentDark_Clean.png", new Vector4(24f, 18f, 24f, 18f) },
                { "Frame_HUD_VitalsBackplate_Clean.png", new Vector4(200f, 170f, 200f, 170f) },
                { "Pill_HUD_ShieldReady_Blank.png", new Vector4(75f, 36f, 75f, 36f) },
                { "Pill_HUD_ShieldReady_Clean.png", new Vector4(180f, 36f, 75f, 36f) },
                { "Pill_HUD_ShieldReady_Labeled.png", new Vector4(180f, 36f, 75f, 36f) },
                // SlicedUI 08_BattleTopHUD
                { "Badge_HUD_InGameCoin_Clean.png", new Vector4(65f, 36f, 65f, 36f) },
                { "Btn_HUD_ExitMatch_Clean.png", new Vector4(65f, 36f, 65f, 36f) },
                { "Btn_HUD_ExitMatch_Labeled.png", new Vector4(65f, 36f, 65f, 36f) },
                { "Btn_HUD_Pause_Clean.png", new Vector4(65f, 36f, 65f, 36f) },
                { "Btn_HUD_Pause_Labeled.png", new Vector4(65f, 36f, 65f, 36f) },
                { "Frame_HUD_StageWaveBanner_Clean.png", new Vector4(180f, 150f, 180f, 150f) },
                { "Frame_HUD_TopRightCluster_Clean.png", new Vector4(180f, 150f, 180f, 150f) },
                // SlicedUI 09_TrainingSpawnerDock
                { "Divider_Training_PlasmaTube_Violet.png", new Vector4(0f, 40f, 0f, 40f) },
                { "Frame_Training_MainDock_Clean.png", new Vector4(220f, 170f, 220f, 170f) },
                { "Slot_Training_EnemySpawnCard_Clean.png", new Vector4(40f, 48f, 40f, 48f) },
                { "Toggle_Training_OptionPill_Blank.png", new Vector4(55f, 36f, 55f, 36f) },
                { "Toggle_Training_OptionPill_BlueLED_Clean.png", new Vector4(130f, 36f, 55f, 36f) }
            };

        private static readonly Dictionary<string, Sprite> CachedSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Sprite> LoadedSceneSpritesByName =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<UnityEngine.Object> CreatedRuntimeAssetSet =
            new HashSet<UnityEngine.Object>();

        private static readonly List<UnityEngine.Object> CreatedRuntimeAssets =
            new List<UnityEngine.Object>();

        private static bool loadedSpriteIndexBuilt;
        private static bool sceneLoadedHookRegistered;
        private static TMP_FontAsset cachedSharedTmpFont;
        private static Font cachedSharedLegacyFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (sceneLoadedHookRegistered)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                sceneLoadedHookRegistered = false;
            }

            for (int i = CreatedRuntimeAssets.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object asset = CreatedRuntimeAssets[i];
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            CreatedRuntimeAssets.Clear();
            CreatedRuntimeAssetSet.Clear();
            CachedSprites.Clear();
            LoadedSceneSpritesByName.Clear();
            loadedSpriteIndexBuilt = false;
            cachedSharedTmpFont = null;
            cachedSharedLegacyFont = null;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LoadedSceneSpritesByName.Clear();
            loadedSpriteIndexBuilt = false;
        }

        public static Sprite LoadSprite(string fileName, Vector4 sliceBorder = default)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            string norm = NormalizePngFileName(fileName);
            if (string.IsNullOrWhiteSpace(norm)) return null;

            Vector4 resolvedBorder = ResolveBorder(norm, sliceBorder);
            string cacheKey = $"{norm}|{resolvedBorder.x:0.##},{resolvedBorder.y:0.##},{resolvedBorder.z:0.##},{resolvedBorder.w:0.##}";

            if (CachedSprites.TryGetValue(cacheKey, out Sprite cached) && cached != null)
            {
                return cached;
            }

            string[] candidateRelPaths = BuildCandidateRelativePaths(norm);

            // Tier 1 (Editor AssetDatabase) + Tier 2 (Resources subpath)
            for (int i = 0; i < candidateRelPaths.Length; i++)
            {
                Sprite found = TryLoadEditorOrResourcesAtRelativePath(candidateRelPaths[i], resolvedBorder);
                if (found != null)
                {
                    CachedSprites[cacheKey] = found;
                    return found;
                }
            }

            // Tier 2b (Resources bare filename lookup once)
            string justNoExt = Path.GetFileNameWithoutExtension(norm);
            if (!string.IsNullOrEmpty(justNoExt))
            {
                Sprite resByName = Resources.Load<Sprite>(justNoExt);
                if (resByName != null)
                {
                    Sprite wrapped = Application.isPlaying
                        ? EnsureBorderOnLoadedSprite(resByName, resolvedBorder)
                        : resByName;
                    CachedSprites[cacheKey] = wrapped;
                    return wrapped;
                }
            }

            // Tier 3 (In-memory loaded scene sprite index for standalone builds / scene-serialized sprites)
            Sprite loadedSceneSprite = TryFindInLoadedSceneSprites(justNoExt, resolvedBorder);
            if (loadedSceneSprite != null)
            {
                CachedSprites[cacheKey] = loadedSceneSprite;
                return loadedSceneSprite;
            }

            // Tier 4 (Disk PNG fallback during Play Mode)
            if (Application.isPlaying)
            {
                for (int i = 0; i < candidateRelPaths.Length; i++)
                {
                    string diskPath = Path.Combine(Application.dataPath, candidateRelPaths[i].Replace('/', Path.DirectorySeparatorChar));
                    Sprite diskSprite = TryCreateSpriteFromDisk(diskPath, resolvedBorder);
                    if (diskSprite != null)
                    {
                        CachedSprites[cacheKey] = diskSprite;
                        return diskSprite;
                    }
                }
            }

            return null;
        }

        public static void ApplySprite(
            Image image,
            string fileName,
            Vector4 sliceBorder = default,
            bool sliced = true,
            bool preserveAspect = false)
        {
            if (image == null) return;
            Sprite sprite = LoadSprite(fileName, sliceBorder);
            if (sprite == null) return;
            ApplySprite(image, sprite, sliced, preserveAspect);
        }

        public static void ApplySprite(
            Image image,
            Sprite sprite,
            bool sliced = true,
            bool preserveAspect = false)
        {
            if (image == null || sprite == null) return;

            image.sprite = sprite;
            image.color = Color.white;

            if (sliced)
            {
                if (sprite.border == Vector4.zero)
                {
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    image.pixelsPerUnitMultiplier = 1f;
                    return;
                }

                image.type = Image.Type.Sliced;
                image.preserveAspect = preserveAspect;

                RectTransform rt = image.rectTransform;
                float effW = rt != null ? Mathf.Max(0f, rt.rect.width > 1f ? rt.rect.width : rt.sizeDelta.x) : 0f;
                float effH = rt != null ? Mathf.Max(0f, rt.rect.height > 1f ? rt.rect.height : rt.sizeDelta.y) : 0f;
                image.pixelsPerUnitMultiplier = ComputeSlicedPpuMultiplier(sprite, effW, effH);
            }
            else
            {
                image.type = Image.Type.Simple;
                image.preserveAspect = preserveAspect;
                image.pixelsPerUnitMultiplier = 1f;
            }
        }

        public static void ApplySlicedSprite(
            Image img,
            Sprite sprite,
            float targetWidth,
            float targetHeight,
            Color? tint = null)
        {
            if (img == null || sprite == null) return;

            img.sprite = sprite;
            img.color = tint ?? Color.white;

            if (sprite.border == Vector4.zero)
            {
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.pixelsPerUnitMultiplier = 1f;
                return;
            }

            img.type = Image.Type.Sliced;
            img.preserveAspect = false;

            RectTransform rt = img.rectTransform;
            float effW = targetWidth > 1f
                ? targetWidth
                : (rt != null ? Mathf.Max(0f, rt.rect.width > 1f ? rt.rect.width : rt.sizeDelta.x) : 0f);
            float effH = targetHeight > 1f
                ? targetHeight
                : (rt != null ? Mathf.Max(0f, rt.rect.height > 1f ? rt.rect.height : rt.sizeDelta.y) : 0f);

            img.pixelsPerUnitMultiplier = ComputeSlicedPpuMultiplier(sprite, effW, effH);
        }

        private static float ComputeSlicedPpuMultiplier(Sprite sprite, float targetWidth, float targetHeight)
        {
            if (sprite == null) return 1f;

            Vector4 b = sprite.border;
            if (b == Vector4.zero) return 1f;

            float candidate = 0f;
            if (targetHeight > 1f)
            {
                if (sprite.rect.height > 1f)
                {
                    candidate = Mathf.Max(candidate, sprite.rect.height / targetHeight);
                }
                if ((b.y + b.w) > 0f)
                {
                    candidate = Mathf.Max(candidate, (b.y + b.w) / (targetHeight * 0.72f));
                }
            }

            if (targetWidth > 1f && (b.x + b.z) > 0f)
            {
                candidate = Mathf.Max(candidate, (b.x + b.z) / (targetWidth * 0.72f));
            }

            bool isCompactTinyTarget = targetWidth > 1f && targetHeight > 1f && targetWidth < 58f && targetHeight < 58f;
            if (isCompactTinyTarget)
            {
                return candidate > 0f ? Mathf.Clamp(candidate, 0.25f, 64f) : 1f;
            }

            return candidate > 0f ? Mathf.Clamp(candidate, 0.25f, 12f) : 1f;
        }

        public static void ApplyFilledBar(Image image, string fileName)
        {
            if (image == null) return;

            Sprite sprite = LoadSprite(fileName);
            if (sprite == null) return;

            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.preserveAspect = false;
        }

        public static void StyleTmpText(
            TMP_Text tmp,
            float fontSize = -1f,
            Color? color = null,
            bool bold = true,
            bool addShadow = true)
        {
            if (tmp == null) return;

            if (tmp.font == null || IsDefaultLiberationSans(tmp.font))
            {
                TMP_FontAsset sharedFont = ResolveSharedTmpFont(tmp);
                if (sharedFont != null && (!IsDefaultLiberationSans(sharedFont) || (Application.isPlaying && tmp.font == null)))
                {
                    bool canAssign = Application.isPlaying;
#if UNITY_EDITOR
                    if (!canAssign && !IsDefaultLiberationSans(sharedFont) && UnityEditor.EditorUtility.IsPersistent(sharedFont))
                    {
                        canAssign = true;
                    }
#endif
                    if (canAssign)
                    {
                        tmp.font = sharedFont;
                    }
                }
            }

            tmp.fontStyle = bold ? (tmp.fontStyle | FontStyles.Bold) : (tmp.fontStyle & ~FontStyles.Bold);
            if (fontSize > 0f)
            {
                tmp.fontSize = fontSize;
            }

            if (color.HasValue)
            {
                tmp.color = color.Value;
            }

            ConfigureOutlineAndShadow(tmp.gameObject, addShadow);
        }

        public static void StyleLegacyText(
            Text txt,
            int fontSize = -1,
            Color? color = null,
            bool bold = true,
            bool addShadow = true)
        {
            if (txt == null) return;

            if (txt.font == null)
            {
                Font resolved = ResolveSharedLegacyFont();
                if (resolved != null)
                {
                    txt.font = resolved;
                }
            }

            txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            if (fontSize > 0)
            {
                txt.fontSize = fontSize;
            }

            if (color.HasValue)
            {
                txt.color = color.Value;
            }

            ConfigureOutlineAndShadow(txt.gameObject, addShadow);
        }

        public static TMP_FontAsset ResolveSharedTmpFont(Component context = null)
        {
            if (cachedSharedTmpFont != null && !IsDefaultLiberationSans(cachedSharedTmpFont))
            {
                return cachedSharedTmpFont;
            }

#if UNITY_EDITOR
            TMP_FontAsset editorPersistentFont =
                UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SegoeUIBoldVN SDF.asset") ??
                UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/SegoeUIBoldVN SDF.asset");
            if (editorPersistentFont != null && !IsDefaultLiberationSans(editorPersistentFont))
            {
                cachedSharedTmpFont = editorPersistentFont;
                return cachedSharedTmpFont;
            }
#endif

            TMP_FontAsset resTmpFont =
                Resources.Load<TMP_FontAsset>("Fonts/SegoeUIBoldVN SDF") ??
                Resources.Load<TMP_FontAsset>("SegoeUIBoldVN SDF");
            if (resTmpFont != null && !IsDefaultLiberationSans(resTmpFont))
            {
                cachedSharedTmpFont = resTmpFont;
                return cachedSharedTmpFont;
            }

            TMP_FontAsset fallbackLiberationFont = null;

            if (context != null)
            {
                TMP_Text direct = context as TMP_Text ?? context.GetComponent<TMP_Text>();
                if (IsUsableFontCandidate(direct, ref fallbackLiberationFont))
                {
                    cachedSharedTmpFont = direct.font;
                    return cachedSharedTmpFont;
                }

                TMP_Text parentTmp = context.GetComponentInParent<TMP_Text>();
                if (IsUsableFontCandidate(parentTmp, ref fallbackLiberationFont))
                {
                    cachedSharedTmpFont = parentTmp.font;
                    return cachedSharedTmpFont;
                }

                TMP_Text[] childTmps = context.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < childTmps.Length; i++)
                {
                    if (IsUsableFontCandidate(childTmps[i], ref fallbackLiberationFont))
                    {
                        cachedSharedTmpFont = childTmps[i].font;
                        return cachedSharedTmpFont;
                    }
                }
            }

            TMP_Text[] sceneTmps = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < sceneTmps.Length; i++)
            {
                if (IsUsableFontCandidate(sceneTmps[i], ref fallbackLiberationFont))
                {
                    cachedSharedTmpFont = sceneTmps[i].font;
                    return cachedSharedTmpFont;
                }
            }

            if (Application.isPlaying)
            {
                Font sourceFont = ResolveSharedLegacyFont();
                if (sourceFont != null)
                {
                    TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(sourceFont);
                    if (created != null)
                    {
                        created.name = "GlassHudRuntimeTMPFont";
                        created.hideFlags = HideFlags.DontSave;
                        CreatedRuntimeAssets.Add(created);
                        if (created.atlasTexture != null)
                        {
                            created.atlasTexture.hideFlags = HideFlags.DontSave;
                            CreatedRuntimeAssets.Add(created.atlasTexture);
                        }
                        if (created.material != null)
                        {
                            created.material.hideFlags = HideFlags.DontSave;
                            CreatedRuntimeAssets.Add(created.material);
                        }
                        cachedSharedTmpFont = created;
                        return cachedSharedTmpFont;
                    }
                }
            }

            if (fallbackLiberationFont != null)
            {
                if (Application.isPlaying)
                {
                    cachedSharedTmpFont = fallbackLiberationFont;
                }
                return fallbackLiberationFont;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static bool IsUsableFontCandidate(TMP_Text tmp, ref TMP_FontAsset fallbackLiberation)
        {
            if (tmp == null || tmp.font == null) return false;
            if (IsDefaultLiberationSans(tmp.font))
            {
                if (fallbackLiberation == null)
                {
                    fallbackLiberation = tmp.font;
                }
                return false;
            }
            return true;
        }

        private static bool IsDefaultLiberationSans(TMP_FontAsset fontAsset)
        {
            return fontAsset != null &&
                   string.Equals(fontAsset.name, "LiberationSans SDF", StringComparison.OrdinalIgnoreCase);
        }

        private static Font ResolveSharedLegacyFont()
        {
            if (cachedSharedLegacyFont != null)
            {
                return cachedSharedLegacyFont;
            }

#if UNITY_EDITOR
            Font editorFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SegoeUIBoldVN.ttf");
            if (editorFont != null)
            {
                cachedSharedLegacyFont = editorFont;
                return cachedSharedLegacyFont;
            }
#endif

            Font resFont = Resources.Load<Font>("Fonts/SegoeUIBoldVN") ?? Resources.Load<Font>("SegoeUIBoldVN");
            if (resFont != null)
            {
                cachedSharedLegacyFont = resFont;
                return cachedSharedLegacyFont;
            }

            if (Application.isPlaying)
            {
                Font osFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 32);
                if (osFont != null)
                {
                    osFont.hideFlags = HideFlags.DontSave;
                    CreatedRuntimeAssets.Add(osFont);
                    cachedSharedLegacyFont = osFont;
                    return cachedSharedLegacyFont;
                }
            }

            return null;
        }

        private static void ConfigureOutlineAndShadow(GameObject go, bool addShadow)
        {
            if (go == null) return;

            Outline outline = go.GetComponent<Outline>();
            Shadow exactShadow = FindExactShadowComponent(go);

            if (addShadow)
            {
                if (outline == null)
                {
                    outline = go.AddComponent<Outline>();
                }
                outline.enabled = true;
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1.1f, -1.1f);
                outline.useGraphicAlpha = true;

                if (exactShadow == null)
                {
                    exactShadow = go.AddComponent<Shadow>();
                }
                exactShadow.enabled = true;
                exactShadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
                exactShadow.effectDistance = new Vector2(1.5f, -2f);
                exactShadow.useGraphicAlpha = true;
            }
            else
            {
                if (outline != null) outline.enabled = false;
                if (exactShadow != null) exactShadow.enabled = false;
            }
        }

        private static Shadow FindExactShadowComponent(GameObject go)
        {
            Shadow[] shadows = go.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    return shadows[i];
                }
            }
            return null;
        }

        private static string NormalizePngFileName(string fileName)
        {
            string trimmed = fileName.Trim().Replace('\\', '/').TrimStart('/');
            for (int i = 0; i < KnownInputPrefixes.Length; i++)
            {
                if (trimmed.StartsWith(KnownInputPrefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(KnownInputPrefixes[i].Length).TrimStart('/');
                    break;
                }
            }

            if (string.IsNullOrEmpty(trimmed))
            {
                return string.Empty;
            }

            if (!Path.HasExtension(trimmed))
            {
                trimmed += ".png";
            }

            return trimmed;
        }

        private static string[] BuildCandidateRelativePaths(string normalizedFileName)
        {
            if (normalizedFileName.IndexOf('/') >= 0)
            {
                return new[]
                {
                    "Sprites/UI/SlicedUI/" + normalizedFileName,
                    "Sprites/UI/" + normalizedFileName
                };
            }

            string[] candidates = new string[RelativeSearchFolders.Length];
            for (int i = 0; i < RelativeSearchFolders.Length; i++)
            {
                candidates[i] = RelativeSearchFolders[i] + normalizedFileName;
            }
            return candidates;
        }

        private static Vector4 ResolveBorder(string normalizedFileName, Vector4 requestedBorder)
        {
            if (requestedBorder != default)
            {
                return requestedBorder;
            }

            if (DefaultSliceBorders.TryGetValue(normalizedFileName, out Vector4 knownBorder))
            {
                return knownBorder;
            }

            string justName = Path.GetFileName(normalizedFileName);
            if (!string.IsNullOrEmpty(justName) && DefaultSliceBorders.TryGetValue(justName, out Vector4 byFileName))
            {
                return byFileName;
            }

            return Vector4.zero;
        }

        private static Sprite TryLoadEditorOrResourcesAtRelativePath(string relPath, Vector4 sliceBorder)
        {
#if UNITY_EDITOR
            Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/" + relPath);
            if (editorSprite != null)
            {
                return Application.isPlaying
                    ? EnsureBorderOnLoadedSprite(editorSprite, sliceBorder)
                    : editorSprite;
            }
#endif

            Sprite resSprite = TryLoadFromResources(relPath);
            if (resSprite != null)
            {
                return Application.isPlaying
                    ? EnsureBorderOnLoadedSprite(resSprite, sliceBorder)
                    : resSprite;
            }

            return null;
        }

        private static Sprite TryLoadFromResources(string relPath)
        {
            string noExtFull = Path.ChangeExtension(relPath, null);
            Sprite direct = Resources.Load<Sprite>(noExtFull);
            if (direct != null) return direct;

            if (noExtFull.StartsWith("Sprites/", StringComparison.OrdinalIgnoreCase))
            {
                string withoutSprites = noExtFull.Substring("Sprites/".Length);
                Sprite sub = Resources.Load<Sprite>(withoutSprites);
                if (sub != null) return sub;

                if (withoutSprites.StartsWith("UI/", StringComparison.OrdinalIgnoreCase))
                {
                    string withoutUi = withoutSprites.Substring("UI/".Length);
                    Sprite uiSub = Resources.Load<Sprite>(withoutUi);
                    if (uiSub != null) return uiSub;

                    if (withoutUi.StartsWith("SlicedUI/", StringComparison.OrdinalIgnoreCase))
                    {
                        string withoutSlicedUi = withoutUi.Substring("SlicedUI/".Length);
                        Sprite slicedSub = Resources.Load<Sprite>(withoutSlicedUi);
                        if (slicedSub != null) return slicedSub;
                    }
                }
            }

            return null;
        }

        private static Sprite TryFindInLoadedSceneSprites(string spriteNameNoExt, Vector4 sliceBorder)
        {
            if (string.IsNullOrEmpty(spriteNameNoExt)) return null;

            if (Application.isPlaying && !sceneLoadedHookRegistered)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                sceneLoadedHookRegistered = true;
            }

            if (!loadedSpriteIndexBuilt)
            {
                RebuildLoadedSceneSpriteIndex();
            }

            if (LoadedSceneSpritesByName.TryGetValue(spriteNameNoExt, out Sprite found) && found != null)
            {
                return Application.isPlaying
                    ? EnsureBorderOnLoadedSprite(found, sliceBorder)
                    : found;
            }

            return null;
        }

        private static void RebuildLoadedSceneSpriteIndex()
        {
            LoadedSceneSpritesByName.Clear();
            loadedSpriteIndexBuilt = true;

            Sprite[] allLoaded = Resources.FindObjectsOfTypeAll<Sprite>();
            if (allLoaded == null || allLoaded.Length == 0) return;

            for (int i = 0; i < allLoaded.Length; i++)
            {
                Sprite sp = allLoaded[i];
                if (sp == null || sp.texture == null) continue;
                if (CreatedRuntimeAssetSet.Contains(sp)) continue;
                if ((sp.hideFlags & HideFlags.DontSave) != 0) continue;

                string spName = sp.name;
                if (string.IsNullOrEmpty(spName)) continue;

                if (!LoadedSceneSpritesByName.ContainsKey(spName))
                {
                    LoadedSceneSpritesByName[spName] = sp;
                }

                if (spName.EndsWith("_0", StringComparison.Ordinal) && spName.Length > 2)
                {
                    string baseName = spName.Substring(0, spName.Length - 2);
                    if (!LoadedSceneSpritesByName.ContainsKey(baseName))
                    {
                        LoadedSceneSpritesByName[baseName] = sp;
                    }
                }
            }
        }

        private static Sprite EnsureBorderOnLoadedSprite(Sprite source, Vector4 sliceBorder)
        {
            if (source == null) return null;
            if (sliceBorder == Vector4.zero || (source.border - sliceBorder).sqrMagnitude <= 0.25f)
            {
                return source;
            }

            if (source.texture == null)
            {
                return source;
            }

            Sprite wrapped = Sprite.Create(
                source.texture,
                source.rect,
                new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit > 0f ? source.pixelsPerUnit : 100f,
                0,
                SpriteMeshType.FullRect,
                sliceBorder);
            wrapped.name = source.name;
            wrapped.hideFlags = HideFlags.DontSave;
            CreatedRuntimeAssets.Add(wrapped);
            CreatedRuntimeAssetSet.Add(wrapped);
            return wrapped;
        }

        private static Sprite TryCreateSpriteFromDisk(string diskPath, Vector4 sliceBorder)
        {
            if (string.IsNullOrEmpty(diskPath) || !File.Exists(diskPath)) return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(diskPath);
                if (bytes == null || bytes.Length == 0) return null;

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                if (!tex.LoadImage(bytes, false))
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    return null;
                }

                tex.name = Path.GetFileNameWithoutExtension(diskPath);
                tex.hideFlags = HideFlags.DontSave;
                CreatedRuntimeAssets.Add(tex);
                CreatedRuntimeAssetSet.Add(tex);

                Sprite created = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    sliceBorder);
                created.name = tex.name;
                created.hideFlags = HideFlags.DontSave;
                CreatedRuntimeAssets.Add(created);
                CreatedRuntimeAssetSet.Add(created);
                return created;
            }
            catch (IOException ex)
            {
                Debug.Log($"[GlassHudTheme][SPRITE_IO_ERROR] Failed reading sprite '{diskPath}': {ex.Message}");
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.Log($"[GlassHudTheme][SPRITE_PERM_ERROR] Access denied reading sprite '{diskPath}': {ex.Message}");
                return null;
            }
            catch (ArgumentException ex)
            {
                Debug.Log($"[GlassHudTheme][SPRITE_ARG_ERROR] Invalid sprite data '{diskPath}': {ex.Message}");
                return null;
            }
        }
    }
}
