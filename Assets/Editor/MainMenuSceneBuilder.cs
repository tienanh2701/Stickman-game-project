using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Auth;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.UI;

namespace StickmanBeatEmUp.Editor
{
    /// <summary>
    /// Tự động dựng Scene MainMenu.unity hoàn chỉnh (1920x1080) với luồng 4 màn hình + Instructions + Fixed Top-Left Back.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        public const string SCENE_PATH = "Assets/Scenes/MainMenu.unity";

        private static Font cachedFont;

        private static Font GetUIFont()
        {
            if (cachedFont == null)
            {
                cachedFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SegoeUIBoldVN.ttf");
            }
            if (cachedFont == null)
            {
                cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 24);
            }
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return cachedFont;
        }

        [MenuItem("Tools/Build Main Menu Scene (4-Screen Flow)")]
        public static void BuildMainMenuMenuCommand()
        {
            BuildMainMenuScene(true);
        }

        public static string BuildMainMenuScene(bool interactive = true)
        {
            SpriteHelper.ConfigureAllProjectSprites();
            TMP_FontAsset vnTmpFont = SpriteHelper.GetOrCreateVietnameseTmpFontAsset();
            PlayerSettings.productName = "MULTI-DIMENSION STICKMAN";

            if (!Directory.Exists("Assets/Scenes"))
            {
                Directory.CreateDirectory("Assets/Scenes");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera & EventSystem
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 0f, -10f);
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camObj.AddComponent<AudioListener>();

            CanvasUIBuilder.EnsureEventSystem();

            // 2. Canvas (1920 x 1080)
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            UIManager uiManager = canvasObj.AddComponent<UIManager>();

            // 3. Background + Light Dark Overlay (Alpha = 0.20f)
            GameObject bgRoot = CreateFullRect("MenuBackground", canvasObj.transform);
            Image bgImg = bgRoot.AddComponent<Image>();
            Sprite thumbSprite = SpriteHelper.LoadSprite("Assets/Sprites/UI/MenuGlobalBackground.png")
                ?? SpriteHelper.LoadSprite("Assets/Sprites/UI/MainMenu_ThreeHeroes_Thumb.png");

            GameObject overlayObj = CreateFullRect("DarkOverlay", bgRoot.transform);
            Image overlayImg = overlayObj.AddComponent<Image>();

            MenuBackgroundLoader bgLoader = bgRoot.AddComponent<MenuBackgroundLoader>();
            bgLoader.ConfigureTargets(bgImg, overlayImg, thumbSprite);
            bgLoader.SetOverlayAlpha(0.20f);

            // 4. Load SlicedUI, Legacy WorldCard & Gameplay Sprites (sau NewScene!)
            Sprite charSlotGlass = LoadGlassUISprite("CharSlot_Glass.png", new Vector4(28f, 28f, 28f, 28f));
            Sprite charSlotGoldSelected = LoadGlassUISprite("CharSlot_GoldSelected.png", new Vector4(28f, 28f, 28f, 28f));
            Sprite energyOrbSprite = LoadGlassUISprite("EnergyOrb_Slot.png", Vector4.zero);
            Sprite techCutBtnSprite = LoadGlassUISprite("TechCut_ActionButton.png", new Vector4(36f, 28f, 36f, 28f));
            Sprite modalVictoryGold = LoadGlassUISprite("ModalCard_Victory_Gold.png", Vector4.zero, charSlotGoldSelected);
            Sprite miniPillGold = LoadGlassUISprite("MiniPill_GoldBadge.png", Vector4.zero);
            Sprite[] worldCardSprites =
            {
                LoadGlassUISprite("WorldCard_AoT_EmeraldAmber.png", Vector4.zero, charSlotGlass),
                LoadGlassUISprite("WorldCard_DB_AmberGold.png", Vector4.zero, charSlotGlass),
                LoadGlassUISprite("WorldCard_Diend_MagentaCyan.png", Vector4.zero, charSlotGlass)
            };

            // 01_MainMenu
            Sprite mainMenuPlaySprite = LoadSlicedUISprite("01_MainMenu/Btn_MainMenu_Play_Clean.png");
            Sprite mainMenuGuideSprite = LoadSlicedUISprite("01_MainMenu/Btn_MainMenu_Guide_Clean.png");
            Sprite mainMenuQuitSprite = LoadSlicedUISprite("01_MainMenu/Btn_MainMenu_Quit_Clean.png");

            // 02_ModeSelect
            Sprite modeBackBtnSprite = LoadSlicedUISprite("02_ModeSelect/Btn_ModeSelect_Back_Clean.png");
            Sprite modeCoinBadgeSprite = LoadSlicedUISprite("02_ModeSelect/Badge_ModeSelect_CoinTopRight_Blank.png");
            Sprite coinXuIconSprite = LoadSlicedUISprite("02_ModeSelect/Icon_Currency_CoinXu.png",
                SpriteHelper.LoadSprite("Assets/Sprites/UI/Items/Icon_CoinXu.png", SpriteHelper.GetOrCreateCircleSprite()));
            Sprite modeCampaignCard = LoadSlicedUISprite("02_ModeSelect/Card_Mode_Campaign_BlankFrame.png");
            Sprite modeEndlessCard = LoadSlicedUISprite("02_ModeSelect/Card_Mode_Endless_BlankFrame.png");
            Sprite modeTrainingCard = LoadSlicedUISprite("02_ModeSelect/Card_Mode_Training_BlankFrame.png");
            Sprite modeShopCard = LoadSlicedUISprite("02_ModeSelect/Card_Mode_Shop_BlankFrame.png");
            Sprite modeCampaignIcon = LoadSlicedUISprite("02_ModeSelect/Icon_Mode_Campaign_ShieldSwords.png");
            Sprite modeEndlessIcon = LoadSlicedUISprite("02_ModeSelect/Icon_Mode_Endless_InfinityGear.png");
            Sprite modeTrainingIcon = LoadSlicedUISprite("02_ModeSelect/Icon_Mode_Training_CyberFist.png");
            Sprite modeShopIcon = LoadSlicedUISprite("02_ModeSelect/Icon_Mode_Shop_CyberCart.png");

            // 03_CharacterAndItemSelect
            Sprite charMainPanelSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Frame_CharSelect_MainPanel_Clean.png");
            Sprite charSelectedGoldSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Slot_CharSelect_SelectedGold_Clean.png", charSlotGoldSelected);
            Sprite charUnselectedSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Slot_CharSelect_Unselected_Clean.png", charSlotGlass);
            Sprite charComingSoonSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Slot_CharSelect_ComingSoon_Clean.png", charSlotGlass);
            Sprite loadoutPanelSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Frame_ItemLoadout_Panel_Clean.png");
            Sprite loadoutQSlotSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Slot_ItemLoadout_Q_CyanRing_Clean.png", energyOrbSprite);
            Sprite loadoutESlotSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Slot_ItemLoadout_E_VioletRing_Clean.png", energyOrbSprite);
            Sprite charContinueBtnSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Btn_CharSelect_Continue_Clean.png", techCutBtnSprite);
            Sprite charBackBtnSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Btn_CharSelect_Back_Clean.png", modeBackBtnSprite);
            Sprite charResetIconSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Btn_CharSelect_ResetIcon.png", techCutBtnSprite);
            Sprite charTopRightBadgeSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Badge_CharSelect_TopRightFrame_Clean.png", modeCoinBadgeSprite);

            // 04_AuthRegisterLogin
            AuthUIController.AuthThemeSprites authSprites = new AuthUIController.AuthThemeSprites
            {
                modalPanelSprite = LoadSlicedUISprite("04_AuthRegisterLogin/Frame_Auth_ModalPanel_Clean.png"),
                tabLoginInactiveSprite = LoadSlicedUISprite("04_AuthRegisterLogin/Tab_Auth_Login_Inactive_Clean.png"),
                tabRegisterActiveGoldSprite = LoadSlicedUISprite("04_AuthRegisterLogin/Tab_Auth_Register_ActiveGold_Clean.png"),
                inputFieldBlankSprite = LoadSlicedUISprite("04_AuthRegisterLogin/Input_Auth_Field_Blank.png"),
                userIconSprite = LoadSlicedUISprite("04_AuthRegisterLogin/Icon_Auth_User.png"),
                lockIconSprite = LoadSlicedUISprite("04_AuthRegisterLogin/Icon_Auth_Lock.png"),
                primaryButtonSprite = LoadSlicedUISprite("04_AuthRegisterLogin/Btn_Auth_RegisterPrimary_Clean.png"),
                secondaryButtonSprite = LoadSlicedUISprite("04_AuthRegisterLogin/Btn_Auth_BackSecondary_Clean.png"),
                accountPillBgSprite = modeCoinBadgeSprite,
                inputUsernameCleanBackup = LoadSlicedUISprite("04_AuthRegisterLogin/Input_Auth_Username_Clean.png"),
                inputPasswordCleanBackup = LoadSlicedUISprite("04_AuthRegisterLogin/Input_Auth_Password_Clean.png")
            };

            // 05_Shop
            Sprite shopMainPanelSprite = LoadSlicedUISprite("05_Shop/Frame_Shop_MainPanel_Clean.png");
            Sprite shopSummaryBarSprite = LoadSlicedUISprite("05_Shop/Bar_Shop_InventorySummary_Clean.png");
            Sprite shopRowStripSprite = LoadSlicedUISprite("05_Shop/Row_Shop_ItemStrip_Clean.png");
            Sprite shopIconSlotSprite = LoadSlicedUISprite("05_Shop/Slot_Shop_ItemIcon_BlueGlow.png");
            Sprite shopBuyBtnSprite = LoadSlicedUISprite("05_Shop/Btn_Shop_Buy_Clean.png");
            Sprite shopBackBtnSprite = LoadSlicedUISprite("05_Shop/Btn_Shop_Back_Clean.png", modeBackBtnSprite);
            Sprite shopCoinPillSprite = LoadSlicedUISprite("05_Shop/Badge_Shop_TopRightPill_Clean.png", modeCoinBadgeSprite);

            // 06_InstructionsGuide
            Sprite guideMainPanelSprite = LoadSlicedUISprite("06_InstructionsGuide/Frame_Guide_MainPanel_Clean.png");
            Sprite guideControlCardSprite = LoadSlicedUISprite("06_InstructionsGuide/Row_Guide_ControlCard_WithIconBox_Clean.png");
            Sprite guideKeycapSprite = LoadSlicedUISprite("06_InstructionsGuide/Badge_Guide_Keycap_Clean.png");

            Sprite gokuNorm = SpriteHelper.LoadSprite("Assets/Sprites/UI/Goku_Normal_Select.png");
            Sprite gokuTrans = SpriteHelper.LoadSprite("Assets/Sprites/UI/Goku_SSJ_Select.png");
            Sprite erenNorm = SpriteHelper.LoadSprite("Assets/Sprites/UI/Eren_Normal_Select.png");
            Sprite erenTrans = SpriteHelper.LoadSprite("Assets/Sprites/UI/Eren_Titan_Select.png");
            Sprite diendNorm = SpriteHelper.LoadSprite("Assets/Sprites/UI/Diend_Normal_Select.png");
            Sprite diendTrans = SpriteHelper.LoadSprite("Assets/Sprites/UI/Diend_Kiva_Select.png");

            Sprite mapW1 = SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_World1_AoT.png")
                ?? SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_W1_S1.png");
            Sprite mapW2 = SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_World2_DB.png")
                ?? SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_W2_S1.png");
            Sprite mapW3 = SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_World3_Diend.png")
                ?? SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_W3_S1.png");

            // 5. Screen 1: Main Menu Panel
            GameObject mainMenuPanel = CreateFullRect("Screen1_MainMenu", canvasObj.transform);
            CreateLabel("GameTitle", mainMenuPanel.transform, new Vector2(0f, 275f), new Vector2(1720f, 110f),
                "MULTI-DIMENSION STICKMAN", 76, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.25f, 1f), FontStyle.Bold);

            Button playBtn = CreateStyledButton("PlayButton", mainMenuPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 45f), new Vector2(460f, 106f), "CHƠI",
                mainMenuPlaySprite, true, new Color(0.88f, 0.55f, 0.12f, 0.96f), 34);
            Button instrBtn = CreateStyledButton("InstructionsButton", mainMenuPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -72f), new Vector2(430f, 98f), "HƯỚNG DẪN",
                mainMenuGuideSprite, true, new Color(0.18f, 0.42f, 0.68f, 0.96f), 30);
            Button exitBtn = CreateStyledButton("ExitButton", mainMenuPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -184f), new Vector2(430f, 98f), "THOÁT",
                mainMenuQuitSprite, true, new Color(0.58f, 0.18f, 0.20f, 0.96f), 28);

            AuthUIController.AccountStatusPillRefs accountPillRefs = AuthUIController.BuildAccountStatusPill(
                mainMenuPanel.transform,
                authSprites.accountPillBgSprite,
                authSprites.userIconSprite,
                authSprites.secondaryButtonSprite,
                vnTmpFont);

            // 6. Sub-Screen: Instructions Panel
            GameObject instructionsPanel = CreateFullRect("SubScreen_Instructions", canvasObj.transform);
            BuildInstructionsPanel(
                instructionsPanel.transform,
                guideMainPanelSprite,
                guideControlCardSprite,
                guideKeycapSprite,
                out InstructionsUI instructionsUI
            );

            // 6B. Sub-Screen: Mode Select (Campaign / Endless / Training / Shop)
            GameObject modeSelectPanel = BuildModeSelectPanel(
                canvasObj.transform,
                modeCampaignCard, modeEndlessCard, modeTrainingCard, modeShopCard,
                modeCampaignIcon, modeEndlessIcon, modeTrainingIcon, modeShopIcon,
                out Button campaignModeBtn,
                out Button endlessModeBtn,
                out Button trainingModeBtn,
                out Button openShopBtn
            );

            // 6C. Sub-Screen: Shop Panel (Senzu Bean, Spinal Fluid, Ultra Divine Water)
            Sprite emptySlotIcon = SpriteHelper.LoadSprite("Assets/Sprites/UI/Items/Icon_LockedSlot.png");
            GameObject shopPanel = BuildShopPanel(
                canvasObj.transform,
                shopMainPanelSprite,
                shopSummaryBarSprite,
                shopRowStripSprite,
                shopIconSlotSprite,
                shopBuyBtnSprite,
                out ShopManager shopManager
            );

            // 6D. Sub-Screen: Auth Register/Login Panel (04_AuthRegisterLogin)
            AuthUIController authController = AuthUIController.BuildAuthPanel(
                canvasObj.transform,
                authSprites,
                vnTmpFont);

            // 7. Screen 2: Left-Right Split Character Select + Circular Q/E Loadout + Right Full-Body Preview
            GameObject charSelectPanel = CreateFullRect("Screen2_CharacterSelect", canvasObj.transform);

            CreateLabel("Title", charSelectPanel.transform, new Vector2(0f, 455f), new Vector2(1100f, 60f),
                "CHỌN NHÂN VẬT & TRANG BỊ", 42, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            // RIGHT PREVIEW AREA: Large semi-transparent full-body preview (uniform height across Goku/Eren/Diend)
            GameObject bgPreviewObj = CreateUIObject("BgCharacterPreview", charSelectPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(430f, 35f), new Vector2(760f, 760f));
            bgPreviewObj.transform.localScale = Vector3.one;
            Image bgPreviewImg = bgPreviewObj.AddComponent<Image>();
            bgPreviewImg.sprite = gokuNorm;
            bgPreviewImg.color = new Color(1f, 1f, 1f, 0.82f);
            bgPreviewImg.preserveAspect = true;
            bgPreviewImg.raycastTarget = false;

            TMP_Text charNameTxt = CreateTmpLabel("SelectedCharName", charSelectPanel.transform,
                new Vector2(430f, -372f), new Vector2(580f, 48f),
                "GOKU", 32, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.35f, 1f));
            charNameTxt.raycastTarget = false;

            // LEFT PANEL: Frame_CharSelect_MainPanel_Clean container with 3 top + 2 bottom slots
            GameObject leftFrameObj = CreateUIObject("CharacterSelectLeftFrame", charSelectPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-460f, 128f), new Vector2(820f, 556f));
            Image leftFrameBg = leftFrameObj.AddComponent<Image>();
            if (charMainPanelSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(leftFrameBg, charMainPanelSprite, 820f, 556f, Color.white);
            }
            else
            {
                leftFrameBg.color = new Color(0.05f, 0.08f, 0.14f, 0.46f);
                Outline leftFrameOutline = leftFrameObj.AddComponent<Outline>();
                leftFrameOutline.effectColor = new Color(0.42f, 0.62f, 0.88f, 0.35f);
                leftFrameOutline.effectDistance = new Vector2(2f, -2f);
            }

            MenuCharacterCardUI[] charCards = new MenuCharacterCardUI[5];
            string[] charNames = { "GOKU", "EREN", "DIEND", string.Empty, string.Empty };
            string[] cardObjectKeys = { "Goku", "Eren", "Diend", "Locked1", "Locked2" };
            Sprite[] charBaseSprites = { gokuNorm, erenNorm, diendNorm, null, null };
            Sprite[] charTransSprites = { gokuTrans, erenTrans, diendTrans, null, null };
            Vector2[] slotPositions =
            {
                new Vector2(-224f, 108f),
                new Vector2(0f, 108f),
                new Vector2(224f, 108f),
                new Vector2(-168f, -110f),
                new Vector2(168f, -110f)
            };

            for (int i = 0; i < 5; i++)
            {
                bool lockedSlot = i >= 3;
                bool initialSelected = i == 0;
                charCards[i] = BuildCharacterCard(
                    leftFrameObj.transform,
                    cardObjectKeys[i],
                    slotPositions[i],
                    charNames[i],
                    charBaseSprites[i],
                    charTransSprites[i],
                    charUnselectedSprite,
                    charSelectedGoldSprite,
                    charComingSoonSprite,
                    initialSelected,
                    lockedSlot
                );
            }

            if (charCards[0] != null && charCards[0].highlightBorder != null)
            {
                charCards[0].highlightBorder.transform.SetAsLastSibling();
            }

            // 2 NEON RING ITEM LOADOUT SLOTS (Q & E) DIRECTLY UNDERNEATH LEFT FRAME
            GameObject loadoutStrip = CreateUIObject("LoadoutBar", charSelectPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-460f, -295f), new Vector2(820f, 244f));
            Image loadoutBg = loadoutStrip.AddComponent<Image>();
            if (loadoutPanelSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(loadoutBg, loadoutPanelSprite, 820f, 244f, Color.white);
            }
            else
            {
                loadoutBg.color = new Color(0.05f, 0.08f, 0.14f, 0.46f);
                Outline loadoutOutline = loadoutStrip.AddComponent<Outline>();
                loadoutOutline.effectColor = new Color(0.42f, 0.62f, 0.88f, 0.35f);
                loadoutOutline.effectDistance = new Vector2(2f, -2f);
            }

            TMP_Text loadoutHeader = CreateTmpLabel("LoadoutHeader", loadoutStrip.transform,
                new Vector2(0f, 94f), new Vector2(310f, 32f),
                "VẬT PHẨM TRANG BỊ", 21, TextAlignmentOptions.Center, new Color(0.09f, 0.12f, 0.22f, 1f));
            GlassHudTheme.StyleTmpText(loadoutHeader, 21f, new Color(0.09f, 0.12f, 0.22f, 1f), bold: true, addShadow: false);
            if (loadoutHeader.TryGetComponent(out Outline headerOutline))
            {
                headerOutline.enabled = false;
            }

            BuildCircularLoadoutSlot(
                loadoutStrip.transform, "LoadoutSlotQ", new Vector2(-165f, -14f), "Q",
                new Color(0.28f, 0.58f, 0.90f, 0.96f), loadoutQSlotSprite,
                out Button qSlotBtn, out Image qSlotIcon, out TMP_Text qCountTxt, out TMP_Text qNameTxt
            );
            BuildCircularLoadoutSlot(
                loadoutStrip.transform, "LoadoutSlotE", new Vector2(165f, -14f), "E",
                new Color(0.58f, 0.36f, 0.86f, 0.96f), loadoutESlotSprite,
                out Button eSlotBtn, out Image eSlotIcon, out TMP_Text eCountTxt, out TMP_Text eNameTxt
            );

            TMP_Text charFeedbackTxt = CreateTmpLabel("CharSelectFeedback", charSelectPanel.transform,
                new Vector2(-460f, -452f), new Vector2(800f, 36f),
                string.Empty, 20, TextAlignmentOptions.Center, new Color(1f, 0.72f, 0.28f, 1f));

            // Bottom-Right Navigation Controls (Btn_CharSelect_ResetIcon & Btn_CharSelect_Continue_Clean)
            Button togglePreviewBtn = CreateStyledButton("TogglePreviewFormButton", charSelectPanel.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-404f, 78f), new Vector2(88f, 80f), string.Empty,
                charResetIconSprite, false, new Color(0.36f, 0.24f, 0.64f, 0.96f), 22);
            if (togglePreviewBtn.TryGetComponent(out Image togglePreviewBg) && charResetIconSprite != null)
            {
                togglePreviewBg.preserveAspect = true;
            }
            TMP_Text togglePreviewTxt = CreateTmpLabel("ToggleFormText", togglePreviewBtn.transform,
                Vector2.zero, new Vector2(72f, 56f),
                string.Empty, 20, TextAlignmentOptions.Center, Color.white);
            togglePreviewTxt.raycastTarget = false;
            togglePreviewTxt.gameObject.SetActive(false);
            UIManager.EnsureSwapFormButtonVisual(togglePreviewBtn, togglePreviewTxt, applyRuntimeSprite: false, Color.white);

            Button charNextBtn = CreateStyledButton("CharacterNextButton", charSelectPanel.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-205f, 78f), new Vector2(252f, 88f), "TIẾP TỤC >",
                charContinueBtnSprite, true, new Color(0.18f, 0.65f, 0.36f, 0.98f), 26);

            // 8. Screen 3: World Select Panel
            GameObject worldSelectPanel = CreateFullRect("Screen3_WorldSelect", canvasObj.transform);
            CreateLabel("Title", worldSelectPanel.transform, new Vector2(0f, 445f), new Vector2(1200f, 64f),
                "CHỌN THẾ GIỚI", 44, TextAnchor.MiddleCenter, new Color(0.45f, 0.92f, 1f, 1f), FontStyle.Bold);

            MenuWorldCardUI[] worldCards = new MenuWorldCardUI[3];
            string[] worldTitles =
            {
                "THẾ GIỚI 1\nATTACK ON TITAN",
                "THẾ GIỚI 2\nDRAGON BALL",
                "THẾ GIỚI 3\nKAMEN RIDER DIEND"
            };
            Sprite[] worldSprites = { mapW1, mapW2, mapW3 };

            for (int i = 0; i < 3; i++)
            {
                worldCards[i] = BuildWorldCard(
                    worldSelectPanel.transform,
                    new Vector2(-490f + i * 490f, 15f),
                    worldTitles[i],
                    worldSprites[i],
                    worldCardSprites[i] ?? charSlotGlass,
                    charSlotGoldSelected,
                    miniPillGold,
                    i == 0
                );
            }

            Button worldNextBtn = CreateStyledButton("WorldNextButton", worldSelectPanel.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-180f, 76f), new Vector2(250f, 68f), "TIẾP TỤC >",
                charContinueBtnSprite, true, new Color(0.18f, 0.65f, 0.36f, 0.98f), 28);

            // 9. Screen 4: Stage Select Panel (Chỉ hiển thị STAGE 1..4)
            GameObject stageSelectPanel = CreateFullRect("Screen4_StageSelect", canvasObj.transform);
            Text stageHeaderText = CreateLabel("StageSelectionHeader", stageSelectPanel.transform, new Vector2(0f, 415f), new Vector2(1200f, 64f),
                "GOKU  |  THẾ GIỚI 1: ATTACK ON TITAN  —  CHỌN MÀN CHƠI", 38, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            Button[] stageButtons = new Button[4];
            string[] stageNames =
            {
                "STAGE 1\n<size=24>[MELEE]</size>",
                "STAGE 2\n<size=24>[RANGED]</size>",
                "STAGE 3\n<size=24>[ELITE]</size>",
                "STAGE 4\n<size=24>[BOSS]</size>"
            };
            Color[] stageColors =
            {
                new Color(0.14f, 0.36f, 0.56f, 0.78f),
                new Color(0.16f, 0.42f, 0.38f, 0.78f),
                new Color(0.48f, 0.30f, 0.14f, 0.78f),
                new Color(0.56f, 0.14f, 0.20f, 0.78f)
            };

            for (int i = 0; i < 4; i++)
            {
                bool isBossStage = i == 3;
                Sprite stageGlassSprite = isBossStage
                    ? (modalVictoryGold ?? charSlotGoldSelected ?? charSlotGlass)
                    : (worldCardSprites[0] ?? charSlotGlass ?? charSlotGoldSelected);
                Color stageTextColor = isBossStage ? new Color(1f, 0.90f, 0.34f, 1f) : Color.white;
                stageButtons[i] = BuildStageCard(
                    stageSelectPanel.transform,
                    new Vector2(-570f + i * 380f, 0f),
                    stageNames[i],
                    stageGlassSprite,
                    Color.white,
                    stageColors[i],
                    stageTextColor,
                    isBossStage
                );
            }

            // 10. Fixed Top-Left Back Button + Persistent Top-Right Coin Counter
            Button backBtn = CreateStyledButton("BackButton_FixedTopLeft", canvasObj.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(152f, -56f), new Vector2(232f, 74f), "< QUAY LẠI",
                modeBackBtnSprite, true, new Color(0.22f, 0.25f, 0.34f, 0.95f), 24);

            CoinCounterUI coinCounterUI = BuildPersistentCoinCounter(canvasObj.transform, modeCoinBadgeSprite, coinXuIconSprite);

            // 11. Bind to UIManager & default panel visibility
            uiManager.ConfigureBuilderReferences(
                backBtn,
                mainMenuPanel, instructionsPanel, charSelectPanel, worldSelectPanel, stageSelectPanel,
                playBtn, instrBtn, exitBtn,
                charCards, charNextBtn,
                worldCards, worldNextBtn,
                stageButtons, stageHeaderText,
                GetUIFont()
            );

            uiManager.ConfigureExpandedUI(
                modeSelectPanel, shopPanel,
                campaignModeBtn, endlessModeBtn, trainingModeBtn, openShopBtn, charNextBtn,
                bgPreviewImg, charBaseSprites, charTransSprites,
                togglePreviewBtn, togglePreviewTxt,
                charNameTxt, null, charFeedbackTxt,
                qSlotBtn, qSlotIcon, qCountTxt, qNameTxt,
                eSlotBtn, eSlotIcon, eCountTxt, eNameTxt,
                coinCounterUI, shopManager, emptySlotIcon
            );

            uiManager.ConfigureGlassmorphismSprites(
                mainMenuPlaySprite,
                mainMenuGuideSprite,
                modeCoinBadgeSprite,
                modeCampaignCard,
                modeEndlessCard,
                modeTrainingCard,
                modeShopCard,
                charSlotGlass,
                charSlotGoldSelected,
                energyOrbSprite,
                techCutBtnSprite
            );

            uiManager.ConfigureSlicedUISprites(
                mainMenuPlaySprite,
                mainMenuGuideSprite,
                mainMenuQuitSprite,
                modeBackBtnSprite,
                modeCoinBadgeSprite,
                coinXuIconSprite,
                modeCampaignCard,
                modeEndlessCard,
                modeTrainingCard,
                modeShopCard,
                modeCampaignIcon,
                modeEndlessIcon,
                modeTrainingIcon,
                modeShopIcon,
                charMainPanelSprite,
                charSelectedGoldSprite,
                charUnselectedSprite,
                charComingSoonSprite,
                loadoutPanelSprite,
                loadoutQSlotSprite,
                loadoutESlotSprite,
                charContinueBtnSprite,
                charBackBtnSprite,
                charResetIconSprite,
                charTopRightBadgeSprite,
                shopBackBtnSprite,
                shopCoinPillSprite
            );

            uiManager.ConfigureAuthUI(
                authController.gameObject,
                authController,
                accountPillRefs.pillButton,
                accountPillRefs.userIcon,
                accountPillRefs.statusText,
                accountPillRefs.actionButton,
                accountPillRefs.actionLabel,
                accountPillRefs.logoutButton
            );
            uiManager.ConfigureAuthSprites(authSprites);

            uiManager.ApplyGlassmorphismTheme();
            uiManager.ShowScreen(MainMenuScreen.MainMenu);
            EditorUtility.SetDirty(uiManager);
            EditorUtility.SetDirty(authController);
            EditorUtility.SetDirty(shopManager);
            EditorUtility.SetDirty(coinCounterUI);
            if (instructionsUI != null)
            {
                EditorUtility.SetDirty(instructionsUI);
            }

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = $"MainMenuSceneBuilder OK! Saved: {SCENE_PATH}";
            if (interactive)
            {
                Debug.Log($"<color=cyan>[MainMenuSceneBuilder]</color> {msg}");
            }
            return SCENE_PATH;
        }

        private static void BuildInstructionsPanel(
            Transform parent,
            Sprite guideMainPanelSprite,
            Sprite controlCardSprite,
            Sprite keycapBadgeSprite,
            out InstructionsUI instructionsUI)
        {
            CreateLabel("InstrTitle", parent, new Vector2(0f, 452f), new Vector2(1200f, 64f),
                "HƯỚNG DẪN ĐIỀU KHIỂN", 42, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            GameObject container = CreateUIObject("InstrGridContainer", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -25f), new Vector2(1660f, 800f));
            Image containerBg = container.AddComponent<Image>();
            if (guideMainPanelSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(containerBg, guideMainPanelSprite, 1660f, 800f, Color.white);
            }
            else
            {
                containerBg.color = new Color(0.06f, 0.08f, 0.14f, 0.44f);
                Outline containerOutline = container.AddComponent<Outline>();
                containerOutline.effectColor = new Color(0.45f, 0.72f, 1f, 0.25f);
                containerOutline.effectDistance = new Vector2(2f, -2f);
            }

            instructionsUI = container.AddComponent<InstructionsUI>();

            var rows = InstructionsUI.DefaultKeybindRows;
            InstructionItem[] boundItems = new InstructionItem[rows.Length];
            Sprite[] actionIcons = new Sprite[rows.Length];

            for (int i = 0; i < rows.Length; i++)
            {
                int col = i / 5;
                int row = i % 5;
                float x = col == 0 ? -390f : 390f;
                float y = 272f - row * 136f;
                const float cardWidth = 752f;
                const float cardHeight = 120f;
                bool isUltimateRow = i == rows.Length - 1;

                GameObject card = CreateUIObject($"InstrRow_{i}", container.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, y), new Vector2(cardWidth, cardHeight));
                Image cardBg = card.AddComponent<Image>();
                if (controlCardSprite != null)
                {
                    Color cardTint = isUltimateRow ? new Color(1f, 0.94f, 0.82f, 1f) : Color.white;
                    GlassHudTheme.ApplySlicedSprite(cardBg, controlCardSprite, cardWidth, cardHeight, cardTint);
                }
                else
                {
                    cardBg.color = isUltimateRow
                        ? new Color(0.22f, 0.16f, 0.30f, 0.96f)
                        : new Color(0.14f, 0.17f, 0.25f, 0.94f);
                }

                // Action Sprite Preview inside metallic octagonal bezel of Row_Guide_ControlCard_WithIconBox_Clean
                GameObject iconFrame = CreateUIObject("SpriteFrame", card.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(15f, 0f), new Vector2(88f, 90f));
                Image iconFrameBg = iconFrame.AddComponent<Image>();
                iconFrameBg.color = Color.clear;
                iconFrameBg.raycastTarget = false;

                GameObject iconObj = CreateFullRect("ActionSprite", iconFrame.transform);
                Image iconImg = iconObj.AddComponent<Image>();
                Sprite actionSprite = AssetDatabase.LoadAssetAtPath<Sprite>(rows[i].defaultSpritePath)
                    ?? GlassHudTheme.LoadSprite(rows[i].defaultSpritePath);
                actionIcons[i] = actionSprite;
                iconImg.sprite = actionSprite;
                iconImg.type = Image.Type.Simple;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconImg.color = Color.white;

                // Key Pill
                GameObject keyPill = CreateUIObject("KeyPill", card.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(126f, 0f), new Vector2(180f, 52f));
                Image keyBg = keyPill.AddComponent<Image>();
                if (keycapBadgeSprite != null)
                {
                    Color keyBgTint = isUltimateRow ? new Color(1f, 0.90f, 0.72f, 1f) : Color.white;
                    GlassHudTheme.ApplySlicedSprite(keyBg, keycapBadgeSprite, 180f, 52f, keyBgTint);
                }
                else
                {
                    keyBg.color = isUltimateRow
                        ? new Color(0.86f, 0.42f, 0.10f, 0.98f)
                        : new Color(0.92f, 0.58f, 0.14f, 0.98f);
                }
                Color keyTextColor = isUltimateRow ? new Color(1f, 0.86f, 0.34f, 1f) : new Color(0.96f, 0.98f, 1f, 1f);
                Text keyTxt = CreateLabel("KeyText", keyPill.transform, Vector2.zero, new Vector2(170f, 46f),
                    rows[i].keyCombo, 21, TextAnchor.MiddleCenter, keyTextColor, FontStyle.Bold);

                // Action Text
                GameObject descObj = CreateUIObject("ActionDesc", card.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(320f, 0f), new Vector2(412f, 104f));
                Text descText = descObj.AddComponent<Text>();
                descText.font = GetUIFont();
                descText.text = rows[i].actionDescription;
                descText.fontSize = 22;
                descText.alignment = TextAnchor.MiddleLeft;
                descText.color = Color.white;
                descText.fontStyle = FontStyle.Bold;
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;
                AddTextShadowAndOutline(descObj);

                boundItems[i] = new InstructionItem
                {
                    keyCombo = rows[i].keyCombo,
                    actionDescription = rows[i].actionDescription,
                    previewImage = iconImg,
                    actionSprite = actionSprite,
                    keyLabel = keyTxt,
                    descriptionLabel = descText
                };
            }

            instructionsUI.ConfigureItems(boundItems);
            instructionsUI.ConfigureThemeSprites(guideMainPanelSprite, controlCardSprite, keycapBadgeSprite, actionIcons);
        }

        private static MenuCharacterCardUI BuildCharacterCard(
            Transform parent, string cardKey, Vector2 pos, string charName,
            Sprite baseSprite, Sprite transSprite,
            Sprite unselectedSlotSprite, Sprite goldSelectedSlotSprite, Sprite comingSoonSlotSprite,
            bool isSelected, bool isLocked = false)
        {
            float cardW = isLocked ? 318f : 202f;
            float cardH = isLocked ? 200f : 210f;
            GameObject cardObj = CreateUIObject($"CharCard_{cardKey}", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(cardW, cardH));
            cardObj.transform.localScale = (isSelected && !isLocked) ? Vector3.one * 1.08f : Vector3.one;

            // Base slot frame (Slot_CharSelect_SelectedGold_Clean / Unselected_Clean / ComingSoon_Clean)
            Image highlightImg = cardObj.AddComponent<Image>();
            highlightImg.raycastTarget = false;
            Sprite chosenFrame = isLocked
                ? comingSoonSlotSprite
                : (isSelected ? (goldSelectedSlotSprite ?? unselectedSlotSprite) : (unselectedSlotSprite ?? goldSelectedSlotSprite));
            bool hasGlassSlot = chosenFrame != null;
            if (hasGlassSlot)
            {
                GlassHudTheme.ApplySlicedSprite(highlightImg, chosenFrame, cardW, cardH, Color.white);
            }
            else
            {
                highlightImg.color = isLocked
                    ? new Color(0.20f, 0.22f, 0.26f, 0.65f)
                    : (isSelected ? new Color(1f, 0.85f, 0.25f, 1f) : new Color(0.28f, 0.34f, 0.45f, 0.75f));
            }

            GameObject innerObj = CreateUIObject("CardInner", cardObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f));
            Image innerBg = innerObj.AddComponent<Image>();
            innerBg.color = hasGlassSlot
                ? Color.clear
                : (isLocked ? new Color(0.08f, 0.09f, 0.13f, 0.96f) : new Color(0.11f, 0.14f, 0.21f, 0.94f));
            innerBg.raycastTarget = true;
            innerBg.raycastPadding = new Vector4(-8f, -8f, -8f, -8f);

            Button selectBtn = innerObj.AddComponent<Button>();
            selectBtn.targetGraphic = hasGlassSlot ? highlightImg : innerBg;
            selectBtn.interactable = !isLocked;

            // Portrait Backdrop & Image (uniform localScale = Vector3.one, preserveAspect = true)
            GameObject portraitBg = CreateUIObject("PortraitBackdrop", innerObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(132f, 108f));
            Image pBackImg = portraitBg.AddComponent<Image>();
            pBackImg.color = hasGlassSlot
                ? new Color(0.05f, 0.08f, 0.14f, 0.16f)
                : new Color(0.86f, 0.90f, 0.96f, 0.92f);
            pBackImg.raycastTarget = false;
            portraitBg.SetActive(!isLocked);

            GameObject portraitObj = CreateUIObject("PortraitImage", portraitBg.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -8f));
            portraitObj.transform.localScale = Vector3.one;
            Image portraitImg = portraitObj.AddComponent<Image>();
            portraitImg.sprite = isLocked ? null : baseSprite;
            portraitImg.color = Color.white;
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            portraitImg.enabled = !isLocked;
            portraitObj.SetActive(!isLocked);

            Text nameTxt = CreateLabel("CharacterName", innerObj.transform, new Vector2(0f, -62f), new Vector2(180f, 38f),
                isLocked ? string.Empty : charName, 20, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            nameTxt.raycastTarget = false;
            nameTxt.gameObject.SetActive(!isLocked);

            Text badgeTxt = CreateLabel("FormBadge", innerObj.transform, new Vector2(0f, -79f), new Vector2(184f, 22f),
                string.Empty, 13, TextAnchor.MiddleCenter,
                new Color(0.65f, 0.85f, 1f, 1f), FontStyle.Bold);
            badgeTxt.raycastTarget = false;
            badgeTxt.gameObject.SetActive(false);

            // Locked overlay: clear bg so Slot_CharSelect_ComingSoon_Clean perspective grid shows + centered amber-gold text
            GameObject lockOverlay = CreateFullRect("LockOverlay", innerObj.transform);
            Image lockBg = lockOverlay.AddComponent<Image>();
            lockBg.color = Color.clear;
            lockBg.raycastTarget = false;
            Text lockedTxt = CreateLabel("LockedLabel", lockOverlay.transform, Vector2.zero, new Vector2(260f, 120f),
                "Đang phát triển", 22, TextAnchor.MiddleCenter, new Color(0.96f, 0.70f, 0.22f, 1f), FontStyle.Bold);
            RectTransform lrt = lockedTxt.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 12f);
            lrt.offsetMax = new Vector2(-12f, -12f);
            lockedTxt.raycastTarget = false;
            lockOverlay.SetActive(isLocked);

            GameObject goldFrameObj = UIManager.EnsureGoldSelectionFrame(
                cardObj.transform,
                null,
                false,
                applyRuntimeSprite: false
            );

            return new MenuCharacterCardUI
            {
                characterName = charName,
                selectButton = selectBtn,
                toggleFormButton = null,
                portraitImage = portraitImg,
                portraitBackdrop = portraitBg,
                highlightBorder = highlightImg,
                goldSelectionFrame = goldFrameObj,
                characterNameText = nameTxt,
                formBadgeText = badgeTxt,
                toggleButtonText = null,
                skillsDescText = null,
                baseSprite = baseSprite,
                transformSprite = transSprite,
                baseSkillsSummary = string.Empty,
                transformSkillsSummary = string.Empty,
                showingTransform = false,
                isLocked = isLocked,
                lockOverlay = lockOverlay,
                lockedLabelText = lockedTxt
            };
        }

        private static CoinCounterUI BuildPersistentCoinCounter(Transform canvasTransform, Sprite badgeSprite, Sprite coinIconSprite)
        {
            Sprite coinSprite = coinIconSprite ?? SpriteHelper.LoadSprite("Assets/Sprites/UI/Items/Icon_CoinXu.png", SpriteHelper.GetOrCreateCircleSprite());

            GameObject badgeObj = CreateUIObject("PersistentCoinBadge", canvasTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-36f, -24f), new Vector2(252f, 74f));
            Image badgeBg = badgeObj.AddComponent<Image>();
            if (badgeSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(badgeBg, badgeSprite, 252f, 74f, Color.white);
            }
            else
            {
                badgeBg.color = new Color(0.10f, 0.12f, 0.19f, 0.92f);
            }

            GameObject iconObj = CreateUIObject("CoinIcon", badgeObj.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(38f, 0f), new Vector2(40f, 40f));
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = coinSprite;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            TMP_Text coinLabel = CreateTmpLabel("CoinsText", badgeObj.transform,
                new Vector2(22f, 0f), new Vector2(158f, 48f),
                "0 XU", 25, TextAlignmentOptions.MidlineRight, new Color(1f, 0.86f, 0.28f, 1f));
            coinLabel.raycastTarget = false;

            GameObject flyLayerObj = CreateFullRect("CoinFlyOverlayLayer", canvasTransform);
            RectTransform flyRt = flyLayerObj.GetComponent<RectTransform>();

            CoinCounterUI counter = badgeObj.AddComponent<CoinCounterUI>();
            counter.Configure(coinLabel, iconImg, badgeObj.GetComponent<RectTransform>(), flyRt, coinSprite);
            return counter;
        }

        private static GameObject BuildModeSelectPanel(
            Transform parent,
            Sprite blueCardSprite, Sprite redCardSprite, Sprite greenCardSprite, Sprite bronzeCardSprite,
            Sprite campaignIcon, Sprite endlessIcon, Sprite trainingIcon, Sprite shopIcon,
            out Button campaignBtn, out Button endlessBtn, out Button trainingBtn, out Button shopBtn)
        {
            GameObject panel = CreateFullRect("SubScreen_ModeSelect", parent);
            CreateLabel("ModeSelectTitle", panel.transform, new Vector2(0f, 385f), new Vector2(1200f, 68f),
                "CHỌN CHẾ ĐỘ CHƠI", 46, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            campaignBtn = CreateModeCard(panel.transform, "ModeCard_Campaign", new Vector2(-594f, -35f),
                "CỐT TRUYỆN",
                "CAMPAIGN  >",
                blueCardSprite,
                campaignIcon,
                new Color(0.18f, 0.44f, 0.72f, 0.90f));

            endlessBtn = CreateModeCard(panel.transform, "ModeCard_Endless", new Vector2(-198f, -35f),
                "VÔ TẬN",
                "ENDLESS WAVE  >",
                redCardSprite,
                endlessIcon,
                new Color(0.68f, 0.24f, 0.20f, 0.90f));

            trainingBtn = CreateModeCard(panel.transform, "ModeCard_Training", new Vector2(198f, -35f),
                "LUYỆN TẬP",
                "TRAINING  >",
                greenCardSprite,
                trainingIcon,
                new Color(0.18f, 0.56f, 0.38f, 0.90f));

            shopBtn = CreateModeCard(panel.transform, "ModeCard_Shop", new Vector2(594f, -35f),
                "CỬA HÀNG",
                "SHOP  >",
                bronzeCardSprite,
                shopIcon,
                new Color(0.64f, 0.46f, 0.14f, 0.90f));

            return panel;
        }

        private static Button CreateModeCard(
            Transform parent, string name, Vector2 pos, string title, string ctaLabel,
            Sprite cardSprite, Sprite iconSprite, Color fallbackBgColor)
        {
            GameObject cardObj = CreateUIObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(368f, 520f));
            Image bg = cardObj.AddComponent<Image>();
            if (cardSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(bg, cardSprite, 368f, 520f, Color.white);
            }
            else
            {
                bg.color = fallbackBgColor;
            }

            Button btn = cardObj.AddComponent<Button>();
            btn.targetGraphic = bg;

            ModeCardHoverEffect hover = cardObj.AddComponent<ModeCardHoverEffect>();
            hover.Configure(10f, 1.05f, 14f);

            if (iconSprite != null)
            {
                GameObject iconObj = CreateUIObject("ModeIcon", cardObj.transform,
                    new Vector2(0.14f, 0.42f), new Vector2(0.86f, 0.84f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = iconSprite;
                iconImg.type = Image.Type.Simple;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconImg.color = Color.white;
            }

            // Title & subtitle aligned cleanly in the lower ~36% below the emblem
            GameObject titleObj = CreateUIObject("Title", cardObj.transform,
                new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.36f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Text titleTxt = titleObj.AddComponent<Text>();
            titleTxt.font = GetUIFont();
            titleTxt.text = title;
            titleTxt.fontSize = 34;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.97f, 0.98f, 1f, 1f);
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.resizeTextForBestFit = true;
            titleTxt.resizeTextMinSize = 20;
            titleTxt.resizeTextMaxSize = 36;
            titleTxt.raycastTarget = false;

            GameObject ctaObj = CreateUIObject("ModeBadge", cardObj.transform,
                new Vector2(0.08f, 0.09f), new Vector2(0.92f, 0.20f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Text ctaTxt = ctaObj.AddComponent<Text>();
            ctaTxt.font = GetUIFont();
            ctaTxt.text = ctaLabel;
            ctaTxt.fontSize = 20;
            ctaTxt.alignment = TextAnchor.MiddleCenter;
            ctaTxt.color = new Color(0.84f, 0.89f, 0.96f, 0.95f);
            ctaTxt.fontStyle = FontStyle.Bold;
            ctaTxt.resizeTextForBestFit = true;
            ctaTxt.resizeTextMinSize = 14;
            ctaTxt.resizeTextMaxSize = 22;
            ctaTxt.raycastTarget = false;

            return btn;
        }

        private static GameObject BuildShopPanel(
            Transform parent,
            Sprite mainFrameSprite,
            Sprite summaryBarSprite,
            Sprite rowGlassStripSprite,
            Sprite iconSlotBlueGlowSprite,
            Sprite buyBtnSprite,
            out ShopManager shopManager)
        {
            GameObject panel = CreateFullRect("SubScreen_Shop", parent);
            shopManager = panel.AddComponent<ShopManager>();

            CreateLabel("ShopTitle", panel.transform, new Vector2(0f, 456f), new Vector2(1200f, 64f),
                "CỬA HÀNG VẬT PHẨM", 42, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            // Top Summary Strip (Bar_Shop_InventorySummary_Clean)
            GameObject summaryBar = CreateUIObject("ShopSummaryBar", panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 368f), new Vector2(1460f, 76f));
            Image summaryBg = summaryBar.AddComponent<Image>();
            if (summaryBarSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(summaryBg, summaryBarSprite, 1460f, 76f, Color.white);
            }
            else
            {
                summaryBg.color = new Color(0.06f, 0.09f, 0.16f, 0.56f);
                Outline summaryOutline = summaryBar.AddComponent<Outline>();
                summaryOutline.effectColor = new Color(0.52f, 0.78f, 1f, 0.28f);
                summaryOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            Text balanceTxt = CreateLabel("ShopBalanceText", summaryBar.transform, new Vector2(-500f, 0f), new Vector2(380f, 52f),
                "0 Xu", 28, TextAnchor.MiddleLeft, new Color(1f, 0.86f, 0.25f, 1f), FontStyle.Bold);

            GameObject qIconObj = CreateUIObject("SlotQIcon", summaryBar.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-175f, 0f), new Vector2(54f, 54f));
            Image slotQIcon = qIconObj.AddComponent<Image>();
            slotQIcon.preserveAspect = true;
            slotQIcon.enabled = false;
            Text slotQTxt = CreateLabel("SlotQSummary", summaryBar.transform, new Vector2(10f, 0f), new Vector2(290f, 48f),
                "[Q] Trống (x0)", 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);

            GameObject eIconObj = CreateUIObject("SlotEIcon", summaryBar.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(265f, 0f), new Vector2(54f, 54f));
            Image slotEIcon = eIconObj.AddComponent<Image>();
            slotEIcon.preserveAspect = true;
            slotEIcon.enabled = false;
            Text slotETxt = CreateLabel("SlotESummary", summaryBar.transform, new Vector2(455f, 0f), new Vector2(290f, 48f),
                "[E] Trống (x0)", 22, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);

            // Outer Shop Frame (Frame_Shop_MainPanel_Clean) behind ShopRowsContainer
            GameObject shopMainFrame = CreateUIObject("ShopMainFrame", panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -32f), new Vector2(1640f, 700f));
            Image mainFrameBg = shopMainFrame.AddComponent<Image>();
            mainFrameBg.raycastTarget = false;
            if (mainFrameSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(mainFrameBg, mainFrameSprite, 1640f, 700f, Color.white);
            }
            else
            {
                mainFrameBg.color = new Color(0.06f, 0.09f, 0.16f, 0.46f);
            }

            // Transparent container with VerticalLayoutGroup.spacing = 16f
            GameObject rowsContainer = CreateUIObject("ShopRowsContainer", panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -32f), new Vector2(1440f, 548f));
            VerticalLayoutGroup rowsVlg = rowsContainer.AddComponent<VerticalLayoutGroup>();
            rowsVlg.spacing = 16f;
            rowsVlg.childAlignment = TextAnchor.UpperCenter;
            rowsVlg.childControlWidth = true;
            rowsVlg.childControlHeight = false;
            rowsVlg.childForceExpandWidth = true;
            rowsVlg.childForceExpandHeight = false;

            // 3 Concise Shop Rows: Icon (giữ nguyên Asset Icon gốc) | Name | Price | Owned | Buy Button ("MUA")
            var itemSpecs = new (ConsumableItemType type, string title, int price, string iconPath)[]
            {
                (ConsumableItemType.SenzuBean, "ĐẬU THẦN SENZU", 30, "Assets/Sprites/UI/Items/Icon_SenzuBean.png"),
                (ConsumableItemType.SpinalFluid, "DỊCH TỦY TITAN", 60, "Assets/Sprites/UI/Items/Icon_SpinalFluid.png"),
                (ConsumableItemType.UltraDivineWater, "NƯỚC THẦN SIÊU PHÀM", 50, "Assets/Sprites/UI/Items/Icon_UltraDivineWater.png")
            };

            ShopItemRow[] rows = new ShopItemRow[itemSpecs.Length];
            for (int i = 0; i < itemSpecs.Length; i++)
            {
                var spec = itemSpecs[i];
                GameObject rowObj = CreateUIObject($"ShopRow_{spec.type}", rowsContainer.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(1440f, 168f));
                LayoutElement le = rowObj.AddComponent<LayoutElement>();
                le.preferredWidth = 1440f;
                le.preferredHeight = 168f;

                Image rowBg = rowObj.AddComponent<Image>();
                if (rowGlassStripSprite != null)
                {
                    GlassHudTheme.ApplySlicedSprite(rowBg, rowGlassStripSprite, 1440f, 168f, Color.white);
                }
                else
                {
                    rowBg.color = new Color(0.11f, 0.14f, 0.22f, 0.85f);
                }

                GameObject iconFrame = CreateUIObject("IconFrame", rowObj.transform,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(34f, 0f), new Vector2(116f, 116f));
                Image iconFrameBg = iconFrame.AddComponent<Image>();
                if (iconSlotBlueGlowSprite != null)
                {
                    GlassHudTheme.ApplySlicedSprite(iconFrameBg, iconSlotBlueGlowSprite, 116f, 116f, Color.white);
                }
                else
                {
                    iconFrameBg.color = new Color(0.05f, 0.08f, 0.14f, 0.42f);
                }

                GameObject iconChild = CreateUIObject("ItemIcon", iconFrame.transform,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-36f, -36f));
                RectTransform iconChildRt = iconChild.GetComponent<RectTransform>();
                iconChildRt.offsetMin = new Vector2(18f, 18f);
                iconChildRt.offsetMax = new Vector2(-18f, -18f);
                Image iconImg = iconChild.AddComponent<Image>();
                iconImg.sprite = SpriteHelper.LoadSprite(spec.iconPath);
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;

                Text nameTxt = CreateLabel("NameText", rowObj.transform, new Vector2(-280f, 0f), new Vector2(430f, 52f),
                    spec.title, 30, TextAnchor.MiddleLeft, new Color(1f, 0.90f, 0.42f, 1f), FontStyle.Bold);
                Text priceTxt = CreateLabel("PriceText", rowObj.transform, new Vector2(65f, 0f), new Vector2(170f, 44f),
                    $"{spec.price} Xu", 26, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.25f, 1f), FontStyle.Bold);
                Text ownedTxt = CreateLabel("OwnedText", rowObj.transform, new Vector2(265f, 0f), new Vector2(210f, 44f),
                    "Sở hữu: 0", 25, TextAnchor.MiddleLeft, new Color(0.60f, 0.94f, 0.72f, 1f), FontStyle.Bold);

                Button buyBtn = CreateStyledButton("BuyButton", rowObj.transform,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-144f, 0f), new Vector2(224f, 84f), "MUA",
                    buyBtnSprite, true, new Color(0.18f, 0.62f, 0.34f, 0.98f), 28);

                rows[i] = new ShopItemRow
                {
                    itemType = spec.type,
                    buyButton = buyBtn,
                    iconImage = iconImg,
                    nameText = nameTxt,
                    priceText = priceTxt,
                    ownedCountText = ownedTxt,
                    rowBackground = rowBg
                };
            }

            Text feedbackTxt = CreateLabel("ShopFeedbackText", panel.transform, new Vector2(0f, -424f), new Vector2(1200f, 48f),
                string.Empty, 26, TextAnchor.MiddleCenter, new Color(0.45f, 0.95f, 0.55f, 1f), FontStyle.Bold);

            shopManager.ConfigureBuilderReferences(balanceTxt, feedbackTxt, slotQTxt, slotETxt, slotQIcon, slotEIcon, rows);
            shopManager.ConfigureThemeSprites(mainFrameSprite, summaryBarSprite, rowGlassStripSprite, iconSlotBlueGlowSprite, buyBtnSprite, rowsVlg);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowsContainer.GetComponent<RectTransform>());
            return panel;
        }

        private static void BuildCircularLoadoutSlot(
            Transform parent, string name, Vector2 pos, string keyText, Color borderColor, Sprite energyOrbSprite,
            out Button slotBtn, out Image slotIcon, out TMP_Text countTxt, out TMP_Text nameTxt)
        {
            Sprite circleSprite = SpriteHelper.GetOrCreateCircleSprite();
            bool hasEnergyOrb = energyOrbSprite != null;

            GameObject holder = CreateUIObject(name, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(260f, 152f));

            GameObject circleObj = CreateUIObject("CircleSlot", holder.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 14f), new Vector2(112f, 112f));
            Image circleBorder = circleObj.AddComponent<Image>();
            if (hasEnergyOrb)
            {
                circleBorder.sprite = energyOrbSprite;
                circleBorder.type = Image.Type.Simple;
                circleBorder.preserveAspect = true;
                circleBorder.color = Color.white;
            }
            else
            {
                circleBorder.sprite = circleSprite;
                circleBorder.color = borderColor;
            }
            slotBtn = circleObj.AddComponent<Button>();
            slotBtn.targetGraphic = circleBorder;

            GameObject maskObj = CreateUIObject("CircleMask", circleObj.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32f, -32f));
            RectTransform maskRt = maskObj.GetComponent<RectTransform>();
            maskRt.offsetMin = new Vector2(16f, 16f);
            maskRt.offsetMax = new Vector2(-16f, -16f);
            Image maskImg = maskObj.AddComponent<Image>();
            maskImg.sprite = circleSprite;
            maskImg.color = hasEnergyOrb ? Color.white : new Color(0.10f, 0.14f, 0.22f, 0.98f);
            maskImg.raycastTarget = false;
            Mask mask = maskObj.AddComponent<Mask>();
            mask.showMaskGraphic = !hasEnergyOrb;

            GameObject iconObj = CreateUIObject("ItemIcon", maskObj.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, -12f));
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.offsetMin = new Vector2(6f, 6f);
            iconRt.offsetMax = new Vector2(-6f, -6f);
            slotIcon = iconObj.AddComponent<Image>();
            slotIcon.preserveAspect = true;
            slotIcon.raycastTarget = false;

            GameObject keyBadge = CreateUIObject("KeyBadge", circleObj.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(8f, -8f), new Vector2(34f, 34f));
            Image keyBg = keyBadge.AddComponent<Image>();
            keyBg.sprite = null;
            keyBg.color = Color.clear;
            keyBg.raycastTarget = false;
            TMP_Text keyTmp = CreateTmpLabel("KeyTxt", keyBadge.transform, Vector2.zero, new Vector2(32f, 32f),
                keyText, 26, TextAlignmentOptions.Center, new Color(1f, 0.80f, 0.22f, 1f));
            keyTmp.raycastTarget = false;

            GameObject countBadge = CreateUIObject("CountBadge", circleObj.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-8f, 8f), new Vector2(44f, 24f));
            Image countBg = countBadge.AddComponent<Image>();
            countBg.sprite = circleSprite;
            countBg.color = new Color(0.06f, 0.08f, 0.13f, 0.96f);
            countBg.raycastTarget = false;
            countTxt = CreateTmpLabel("ItemCountTxt", countBadge.transform, Vector2.zero, new Vector2(40f, 22f),
                "x0", 15, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.32f, 1f));
            countTxt.raycastTarget = false;

            nameTxt = CreateTmpLabel("ItemNameTxt", holder.transform,
                new Vector2(0f, -58f), new Vector2(260f, 26f),
                "Trống", 18, TextAlignmentOptions.Center, Color.white);
            nameTxt.raycastTarget = false;
        }

        private static TMP_Text CreateTmpLabel(
            string name, Transform parent, Vector2 pos, Vector2 size,
            string content, float fontSize, TextAlignmentOptions align, Color color)
        {
            GameObject obj = CreateUIObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset vnTmp = SpriteHelper.GetOrCreateVietnameseTmpFontAsset();
            if (vnTmp != null)
            {
                tmp.font = vnTmp;
            }
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            AddTextShadowAndOutline(obj);
            return tmp;
        }

        private static MenuWorldCardUI BuildWorldCard(
            Transform parent, Vector2 pos, string worldTitle, Sprite previewSprite,
            Sprite glassSlotSprite, Sprite goldSelectedSlotSprite, Sprite miniPillGold, bool isSelected)
        {
            GameObject cardObj = CreateUIObject($"WorldCard_{worldTitle.Replace('\n', '_')}", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(450f, 470f));
            cardObj.transform.localScale = isSelected ? Vector3.one * 1.05f : Vector3.one;

            Image highlightImg = cardObj.AddComponent<Image>();
            highlightImg.raycastTarget = false;
            if (glassSlotSprite != null || goldSelectedSlotSprite != null)
            {
                Sprite frame = glassSlotSprite ?? goldSelectedSlotSprite;
                UIManager.ApplyGlassImageStyle(highlightImg, frame, sliced: true, preserveAspect: false);
            }
            else
            {
                highlightImg.color = isSelected
                    ? new Color(1f, 0.85f, 0.25f, 1f)
                    : new Color(0.28f, 0.34f, 0.45f, 0.75f);
            }

            GameObject innerObj = CreateUIObject("CardInner", cardObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14f, -14f));
            Image innerBg = innerObj.AddComponent<Image>();
            innerBg.color = new Color(0.05f, 0.07f, 0.12f, 0.36f);
            innerBg.raycastTarget = true;
            innerBg.raycastPadding = new Vector4(-8f, -8f, -8f, -8f);

            Button selectBtn = innerObj.AddComponent<Button>();
            selectBtn.targetGraphic = innerBg;

            GameObject previewObj = CreateUIObject("WorldPreviewImage", innerObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(404f, 250f));
            Image previewImg = previewObj.AddComponent<Image>();
            previewImg.sprite = previewSprite;
            previewImg.preserveAspect = false;
            previewImg.raycastTarget = false;

            Text titleTxt = CreateLabel("WorldTitle", innerObj.transform, new Vector2(0f, -95f), new Vector2(400f, 84f),
                worldTitle, 28, TextAnchor.MiddleCenter, new Color(1f, 0.90f, 0.35f, 1f), FontStyle.Bold);
            titleTxt.raycastTarget = false;

            GameObject badgeObj = CreateUIObject("SelectedBadge", innerObj.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 18f), new Vector2(220f, 44f));
            Image badgeBg = badgeObj.AddComponent<Image>();
            if (miniPillGold != null)
            {
                UIManager.ApplyGlassImageStyle(badgeBg, miniPillGold, sliced: true, preserveAspect: false);
            }
            else
            {
                badgeBg.color = new Color(0.92f, 0.62f, 0.12f, 1f);
            }
            badgeBg.raycastTarget = false;
            Text badgeLabel = CreateLabel("SelectedText", badgeObj.transform, Vector2.zero, new Vector2(210f, 40f),
                "ĐÃ CHỌN", 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            badgeLabel.raycastTarget = false;
            badgeObj.SetActive(isSelected);

            UIManager.EnsureGoldSelectionFrame(cardObj.transform, null, isSelected, applyRuntimeSprite: false);

            return new MenuWorldCardUI
            {
                worldTitle = worldTitle,
                selectButton = selectBtn,
                previewImage = previewImg,
                highlightBorder = highlightImg,
                selectedBadge = badgeObj,
                worldBackgroundSprite = previewSprite
            };
        }

        private static Button BuildStageCard(
            Transform parent, Vector2 pos, string stageName,
            Sprite glassSprite, Color glassTint, Color fallbackBgColor,
            Color textColor, bool isBossStage)
        {
            GameObject cardObj = CreateUIObject($"StageCard_{stageName.Split('\n')[0]}", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(340f, 240f));
            Image bg = cardObj.AddComponent<Image>();
            if (glassSprite != null)
            {
                UIManager.ApplyGlassImageStyle(bg, glassSprite, sliced: true, preserveAspect: false);
                bg.color = glassTint;
            }
            else
            {
                bg.color = fallbackBgColor;
            }
            Button btn = cardObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            UIManager.EnsureButtonHoverEffect(btn, 6f, 1.04f, 14f);

            Text titleTxt = CreateLabel("StageName", cardObj.transform, Vector2.zero, new Vector2(320f, 220f),
                stageName, 44, TextAnchor.MiddleCenter, textColor, FontStyle.Bold);
            titleTxt.supportRichText = true;
            titleTxt.lineSpacing = 1.08f;
            RectTransform trt = titleTxt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            titleTxt.raycastTarget = false;

            UIManager.EnsureGoldSelectionFrame(cardObj.transform, null, isBossStage, applyRuntimeSprite: false);

            return btn;
        }

        private static GameObject CreateFullRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return obj;
        }

        private static GameObject CreateUIObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return obj;
        }

        private static Text CreateLabel(string name, Transform parent, Vector2 pos, Vector2 size, string content, int fontSize, TextAnchor align, Color color, FontStyle style)
        {
            GameObject obj = CreateUIObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            Text txt = obj.AddComponent<Text>();
            txt.font = GetUIFont();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color;
            txt.fontStyle = style;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            AddTextShadowAndOutline(obj);
            return txt;
        }

        private static void AddTextShadowAndOutline(GameObject go)
        {
            if (go == null) return;
            if (!go.TryGetComponent(out Outline outline))
            {
                outline = go.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0f, 0f, 0f, 0.86f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            outline.useGraphicAlpha = true;

            Shadow exactShadow = null;
            Shadow[] shadows = go.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    exactShadow = shadows[i];
                    break;
                }
            }
            if (exactShadow == null)
            {
                exactShadow = go.AddComponent<Shadow>();
            }
            exactShadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
            exactShadow.effectDistance = new Vector2(1.8f, -2.2f);
            exactShadow.useGraphicAlpha = true;
        }

        private static Sprite LoadSlicedUISprite(string relPathUnderSlicedUI, Sprite fallback = null)
        {
            if (string.IsNullOrEmpty(relPathUnderSlicedUI)) return fallback;
            string assetPath = "Assets/Sprites/UI/SlicedUI/" + relPathUnderSlicedUI.TrimStart('/');
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)
                ?? GlassHudTheme.LoadSprite(relPathUnderSlicedUI);
            return sp != null ? sp : fallback;
        }

        private static Sprite LoadGlassUISprite(string fileName, Vector4 sliceBorder, Sprite fallback = null)
        {
            string[] candidates =
            {
                $"Assets/Sprites/UI/GlassHUD/{fileName}",
                $"Assets/Sprites/UI/Glassmorphism/{fileName}",
                $"Assets/Sprites/UI/{fileName}",
                $"Assets/Sprites/UI/Glass/{fileName}"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string assetPath = candidates[i];
                Sprite sp = SpriteHelper.LoadSprite(assetPath);
                if (sp == null) continue;

                if (sliceBorder != Vector4.zero)
                {
                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer != null)
                    {
                        TextureImporterSettings settings = new TextureImporterSettings();
                        importer.ReadTextureSettings(settings);
                        bool borderDirty = (importer.spriteBorder - sliceBorder).sqrMagnitude > 0.01f;
                        bool meshDirty = settings.spriteMeshType != SpriteMeshType.FullRect;
                        if (borderDirty || meshDirty)
                        {
                            importer.spriteBorder = sliceBorder;
                            settings.spriteMeshType = SpriteMeshType.FullRect;
                            importer.SetTextureSettings(settings);
                            importer.SaveAndReimport();
                            sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                        }
                    }
                }
                return sp != null ? sp : fallback;
            }

            return fallback;
        }

        private static Button CreateStyledButton(
            string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size, string label, Sprite sprite, bool sliced, Color fallbackColor, int fontSize)
        {
            GameObject btnObj = CreateUIObject(name, parent, anchorMin, anchorMax, pivot, pos, size);
            Image bg = btnObj.AddComponent<Image>();
            if (sprite != null)
            {
                UIManager.ApplyGlassImageStyle(bg, sprite, sliced, preserveAspect: false);
            }
            else
            {
                bg.color = fallbackColor;
            }

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            UIManager.EnsureButtonHoverEffect(btn, 0f, 1.035f, 15f);

            Text txt = CreateLabel("Label", btnObj.transform, Vector2.zero, size, label, fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            txt.raycastTarget = false;

            return btn;
        }
    }
}
