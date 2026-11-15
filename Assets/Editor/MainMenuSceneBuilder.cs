using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
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

            // 3. Background + Dark Overlay (Alpha = 0.6f)
            GameObject bgRoot = CreateFullRect("MenuBackground", canvasObj.transform);
            Image bgImg = bgRoot.AddComponent<Image>();
            Sprite thumbSprite = SpriteHelper.LoadSprite("Assets/Sprites/UI/MainMenu_ThreeHeroes_Thumb.png");

            GameObject overlayObj = CreateFullRect("DarkOverlay", bgRoot.transform);
            Image overlayImg = overlayObj.AddComponent<Image>();

            MenuBackgroundLoader bgLoader = bgRoot.AddComponent<MenuBackgroundLoader>();
            bgLoader.ConfigureTargets(bgImg, overlayImg, thumbSprite);

            // 4. Load UI & Gameplay Sprites (sau NewScene!)
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

            Button playBtn = CreateSolidButton("PlayButton", mainMenuPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 45f), new Vector2(400f, 84f), "CHƠI", new Color(0.88f, 0.55f, 0.12f, 0.96f), 34);
            Button instrBtn = CreateSolidButton("InstructionsButton", mainMenuPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -65f), new Vector2(400f, 76f), "HƯỚNG DẪN", new Color(0.18f, 0.42f, 0.68f, 0.96f), 30);
            Button exitBtn = CreateSolidButton("ExitButton", mainMenuPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -170f), new Vector2(400f, 72f), "THOÁT", new Color(0.58f, 0.18f, 0.20f, 0.96f), 28);

            // 6. Sub-Screen: Instructions Panel
            GameObject instructionsPanel = CreateFullRect("SubScreen_Instructions", canvasObj.transform);
            BuildInstructionsPanel(instructionsPanel.transform);

            // 6B. Sub-Screen: Mode Select (Campaign / Endless / Training / Shop)
            GameObject modeSelectPanel = BuildModeSelectPanel(
                canvasObj.transform,
                out Button campaignModeBtn,
                out Button endlessModeBtn,
                out Button trainingModeBtn,
                out Button openShopBtn
            );

            // 6C. Sub-Screen: Shop Panel (Senzu Bean, Spinal Fluid, Ultra Divine Water)
            Sprite emptySlotIcon = SpriteHelper.LoadSprite("Assets/Sprites/UI/Items/Icon_LockedSlot.png");
            GameObject shopPanel = BuildShopPanel(canvasObj.transform, out ShopManager shopManager);

            // 7. Screen 2: Left-Right Split Character Select + Circular Q/E Loadout + Right Full-Body Preview
            GameObject charSelectPanel = CreateFullRect("Screen2_CharacterSelect", canvasObj.transform);

            CreateLabel("Title", charSelectPanel.transform, new Vector2(0f, 455f), new Vector2(1100f, 60f),
                "CHỌN NHÂN VẬT & TRANG BỊ", 42, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            // RIGHT PREVIEW AREA: Large semi-transparent full-body preview (alpha = 0.82f)
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
                "GOKU  —  DẠNG THƯỜNG", 32, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.35f, 1f));
            charNameTxt.raycastTarget = false;

            // LEFT PANEL: Semi-transparent dark frame with subtle outline containing 5 square slots
            GameObject leftFrameObj = CreateUIObject("CharacterSelectLeftFrame", charSelectPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-450f, 75f), new Vector2(780f, 470f));
            Image leftFrameBg = leftFrameObj.AddComponent<Image>();
            leftFrameBg.color = new Color(0.07f, 0.10f, 0.16f, 0.86f);
            Outline leftFrameOutline = leftFrameObj.AddComponent<Outline>();
            leftFrameOutline.effectColor = new Color(0.42f, 0.62f, 0.88f, 0.45f);
            leftFrameOutline.effectDistance = new Vector2(2f, -2f);

            MenuCharacterCardUI[] charCards = new MenuCharacterCardUI[5];
            string[] charNames = { "GOKU", "EREN", "DIEND", string.Empty, string.Empty };
            string[] cardObjectKeys = { "Goku", "Eren", "Diend", "Locked1", "Locked2" };
            Sprite[] charBaseSprites = { gokuNorm, erenNorm, diendNorm, null, null };
            Sprite[] charTransSprites = { gokuTrans, erenTrans, diendTrans, null, null };
            Vector2[] slotPositions =
            {
                new Vector2(-237f, 112.5f),
                new Vector2(0f, 112.5f),
                new Vector2(237f, 112.5f),
                new Vector2(-118.5f, -112.5f),
                new Vector2(118.5f, -112.5f)
            };

            for (int i = 0; i < 5; i++)
            {
                bool lockedSlot = i >= 3;
                charCards[i] = BuildCharacterCard(
                    leftFrameObj.transform,
                    cardObjectKeys[i],
                    slotPositions[i],
                    charNames[i],
                    charBaseSprites[i],
                    charTransSprites[i],
                    lockedSlot
                );
            }

            // 2 CIRCULAR ITEM LOADOUT SLOTS (Q & E) DIRECTLY UNDERNEATH LEFT FRAME
            GameObject loadoutStrip = CreateUIObject("LoadoutBar", charSelectPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-450f, -245f), new Vector2(780f, 158f));
            Image loadoutBg = loadoutStrip.AddComponent<Image>();
            loadoutBg.color = new Color(0.07f, 0.10f, 0.16f, 0.86f);
            Outline loadoutOutline = loadoutStrip.AddComponent<Outline>();
            loadoutOutline.effectColor = new Color(0.42f, 0.62f, 0.88f, 0.45f);
            loadoutOutline.effectDistance = new Vector2(2f, -2f);

            CreateTmpLabel("LoadoutHeader", loadoutStrip.transform,
                new Vector2(0f, 62f), new Vector2(740f, 28f),
                "VẬT PHẨM TRANG BỊ (BẤM ĐỂ ĐỔI)", 18, TextAlignmentOptions.Center, new Color(0.75f, 0.92f, 1f, 1f));

            BuildCircularLoadoutSlot(
                loadoutStrip.transform, "LoadoutSlotQ", new Vector2(-155f, -2f), "Q",
                new Color(0.28f, 0.58f, 0.90f, 0.96f),
                out Button qSlotBtn, out Image qSlotIcon, out TMP_Text qCountTxt, out TMP_Text qNameTxt
            );
            BuildCircularLoadoutSlot(
                loadoutStrip.transform, "LoadoutSlotE", new Vector2(155f, -2f), "E",
                new Color(0.58f, 0.36f, 0.86f, 0.96f),
                out Button eSlotBtn, out Image eSlotIcon, out TMP_Text eCountTxt, out TMP_Text eNameTxt
            );

            TMP_Text charFeedbackTxt = CreateTmpLabel("CharSelectFeedback", charSelectPanel.transform,
                new Vector2(-450f, -355f), new Vector2(760f, 36f),
                string.Empty, 20, TextAlignmentOptions.Center, new Color(1f, 0.72f, 0.28f, 1f));

            // Bottom-Right Navigation Controls ("XEM BIẾN HÌNH" & "TIẾP TỤC >")
            Button togglePreviewBtn = CreateSolidButton("TogglePreviewFormButton", charSelectPanel.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-355f, 42f), new Vector2(280f, 70f), string.Empty, new Color(0.36f, 0.24f, 0.64f, 0.96f), 22);
            TMP_Text togglePreviewTxt = CreateTmpLabel("ToggleFormText", togglePreviewBtn.transform,
                Vector2.zero, new Vector2(260f, 56f),
                "XEM BIẾN HÌNH", 23, TextAlignmentOptions.Center, Color.white);
            togglePreviewTxt.raycastTarget = false;

            Button charNextBtn = CreateSolidButton("CharacterNextButton", charSelectPanel.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-55f, 42f), new Vector2(280f, 70f), "TIẾP TỤC >", new Color(0.18f, 0.65f, 0.36f, 0.98f), 26);

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
                    worldSprites[i]
                );
            }

            Button worldNextBtn = CreateSolidButton("WorldNextButton", worldSelectPanel.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-55f, 42f), new Vector2(250f, 68f), "TIẾP TỤC >", new Color(0.18f, 0.65f, 0.36f, 0.98f), 28);

            // 9. Screen 4: Stage Select Panel (Chỉ hiển thị STAGE 1..4)
            GameObject stageSelectPanel = CreateFullRect("Screen4_StageSelect", canvasObj.transform);
            Text stageHeaderText = CreateLabel("StageSelectionHeader", stageSelectPanel.transform, new Vector2(0f, 415f), new Vector2(1400f, 64f),
                "GOKU  |  THẾ GIỚI 1: ATTACK ON TITAN  —  CHỌN MÀN CHƠI", 38, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            Button[] stageButtons = new Button[4];
            string[] stageNames = { "STAGE 1", "STAGE 2", "STAGE 3", "STAGE 4" };
            Color[] stageColors =
            {
                new Color(0.18f, 0.42f, 0.62f, 0.95f),
                new Color(0.22f, 0.48f, 0.44f, 0.95f),
                new Color(0.55f, 0.36f, 0.16f, 0.95f),
                new Color(0.62f, 0.16f, 0.22f, 0.95f)
            };

            for (int i = 0; i < 4; i++)
            {
                stageButtons[i] = BuildStageCard(
                    stageSelectPanel.transform,
                    new Vector2(-570f + i * 380f, 0f),
                    stageNames[i],
                    stageColors[i]
                );
            }

            // 10. Fixed Top-Left Back Button + Persistent Top-Right Coin Counter
            Button backBtn = CreateSolidButton("BackButton_FixedTopLeft", canvasObj.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -32f), new Vector2(220f, 60f), "< QUAY LẠI", new Color(0.22f, 0.25f, 0.34f, 0.95f), 26);

            CoinCounterUI coinCounterUI = BuildPersistentCoinCounter(canvasObj.transform);

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

            EditorUtility.SetDirty(uiManager);
            EditorUtility.SetDirty(shopManager);
            EditorUtility.SetDirty(coinCounterUI);
            uiManager.ShowScreen(MainMenuScreen.MainMenu);

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

        private static void BuildInstructionsPanel(Transform parent)
        {
            CreateLabel("InstrTitle", parent, new Vector2(0f, 445f), new Vector2(1400f, 64f),
                "HƯỚNG DẪN ĐIỀU KHIỂN", 42, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            GameObject container = CreateUIObject("InstrGridContainer", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -20f), new Vector2(1680f, 780f));
            Image containerBg = container.AddComponent<Image>();
            containerBg.color = new Color(0.08f, 0.10f, 0.16f, 0.85f);

            InstructionsUI instructionsUI = container.AddComponent<InstructionsUI>();

            var rows = new (string keyLabel, string actionDesc, string spritePath)[]
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

            InstructionItem[] boundItems = new InstructionItem[rows.Length];

            for (int i = 0; i < rows.Length; i++)
            {
                int col = i / 5;
                int row = i % 5;
                float x = col == 0 ? -405f : 405f;
                float y = 272f - row * 136f;
                const float cardWidth = 760f;
                const float cardHeight = 114f;
                bool isUltimateRow = i == rows.Length - 1;

                GameObject card = CreateUIObject($"InstrRow_{i}", container.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, y), new Vector2(cardWidth, cardHeight));
                Image cardBg = card.AddComponent<Image>();
                cardBg.color = isUltimateRow
                    ? new Color(0.22f, 0.16f, 0.30f, 0.96f)
                    : new Color(0.14f, 0.17f, 0.25f, 0.94f);

                // Action Sprite Preview (preserveAspect = true)
                GameObject iconFrame = CreateUIObject("SpriteFrame", card.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(18f, 0f), new Vector2(96f, 96f));
                Image iconFrameBg = iconFrame.AddComponent<Image>();
                iconFrameBg.color = new Color(0.88f, 0.92f, 0.98f, 0.95f);

                GameObject iconObj = CreateFullRect("ActionSprite", iconFrame.transform);
                Image iconImg = iconObj.AddComponent<Image>();
                Sprite actionSprite = SpriteHelper.LoadSprite(rows[i].spritePath);
                iconImg.sprite = actionSprite;
                iconImg.preserveAspect = true;

                // Key Pill
                GameObject keyPill = CreateUIObject("KeyPill", card.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(130f, 0f), new Vector2(196f, 54f));
                Image keyBg = keyPill.AddComponent<Image>();
                keyBg.color = isUltimateRow
                    ? new Color(0.86f, 0.42f, 0.10f, 0.98f)
                    : new Color(0.92f, 0.58f, 0.14f, 0.98f);
                Text keyTxt = CreateLabel("KeyText", keyPill.transform, Vector2.zero, new Vector2(190f, 48f),
                    rows[i].keyLabel, 21, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);

                // Action Text
                float descWidth = cardWidth - 360f;
                GameObject descObj = CreateUIObject("ActionDesc", card.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(342f, 0f), new Vector2(descWidth, 106f));
                Text descText = descObj.AddComponent<Text>();
                descText.font = GetUIFont();
                descText.text = rows[i].actionDesc;
                descText.fontSize = 23;
                descText.alignment = TextAnchor.MiddleLeft;
                descText.color = Color.white;
                descText.fontStyle = FontStyle.Bold;
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;

                boundItems[i] = new InstructionItem
                {
                    keyCombo = rows[i].keyLabel,
                    actionDescription = rows[i].actionDesc,
                    previewImage = iconImg,
                    actionSprite = actionSprite,
                    keyLabel = keyTxt,
                    descriptionLabel = descText
                };
            }

            instructionsUI.ConfigureItems(boundItems);
        }

        private static MenuCharacterCardUI BuildCharacterCard(
            Transform parent, string cardKey, Vector2 pos, string charName,
            Sprite baseSprite, Sprite transSprite, bool isLocked = false)
        {
            GameObject cardObj = CreateUIObject($"CharCard_{cardKey}", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(205f, 205f));

            // Glowing outer highlight border
            Image highlightImg = cardObj.AddComponent<Image>();
            highlightImg.color = isLocked
                ? new Color(0.20f, 0.22f, 0.26f, 0.65f)
                : new Color(0.28f, 0.34f, 0.45f, 0.75f);

            GameObject innerObj = CreateUIObject("CardInner", cardObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f));
            Image innerBg = innerObj.AddComponent<Image>();
            innerBg.color = isLocked
                ? new Color(0.08f, 0.09f, 0.13f, 0.96f)
                : new Color(0.11f, 0.14f, 0.21f, 0.94f);

            Button selectBtn = innerObj.AddComponent<Button>();
            selectBtn.targetGraphic = innerBg;
            selectBtn.interactable = !isLocked;

            // Portrait Backdrop & Image (uniform localScale = Vector3.one, preserveAspect = true)
            GameObject portraitBg = CreateUIObject("PortraitBackdrop", innerObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(168f, 126f));
            Image pBackImg = portraitBg.AddComponent<Image>();
            pBackImg.color = new Color(0.86f, 0.90f, 0.96f, 0.92f);
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

            Text nameTxt = CreateLabel("CharacterName", innerObj.transform, new Vector2(0f, -54f), new Vector2(184f, 30f),
                isLocked ? string.Empty : charName, 20, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            nameTxt.raycastTarget = false;
            nameTxt.gameObject.SetActive(!isLocked);

            Text badgeTxt = CreateLabel("FormBadge", innerObj.transform, new Vector2(0f, -79f), new Vector2(184f, 22f),
                isLocked ? string.Empty : "DẠNG THƯỜNG", 13, TextAnchor.MiddleCenter,
                new Color(0.65f, 0.85f, 1f, 1f), FontStyle.Bold);
            badgeTxt.raycastTarget = false;
            badgeTxt.gameObject.SetActive(!isLocked);

            // Locked overlay: ONLY ONE single centered Text reading "Đang phát triển"
            GameObject lockOverlay = CreateFullRect("LockOverlay", innerObj.transform);
            Image lockBg = lockOverlay.AddComponent<Image>();
            lockBg.color = new Color(0.05f, 0.06f, 0.10f, 0.85f);
            lockBg.raycastTarget = false;
            Text lockedTxt = CreateLabel("LockedLabel", lockOverlay.transform, Vector2.zero, new Vector2(176f, 120f),
                "Đang phát triển", 20, TextAnchor.MiddleCenter, new Color(1f, 0.72f, 0.28f, 1f), FontStyle.Bold);
            RectTransform lrt = lockedTxt.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 12f);
            lrt.offsetMax = new Vector2(-12f, -12f);
            lockedTxt.raycastTarget = false;
            lockOverlay.SetActive(isLocked);

            return new MenuCharacterCardUI
            {
                characterName = charName,
                selectButton = selectBtn,
                toggleFormButton = null,
                portraitImage = portraitImg,
                portraitBackdrop = portraitBg,
                highlightBorder = highlightImg,
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

        private static CoinCounterUI BuildPersistentCoinCounter(Transform canvasTransform)
        {
            Sprite coinSprite = SpriteHelper.LoadSprite("Assets/Sprites/UI/Items/Icon_CoinXu.png", SpriteHelper.GetOrCreateCircleSprite());

            GameObject badgeObj = CreateUIObject("PersistentCoinBadge", canvasTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-36f, -30f), new Vector2(236f, 58f));
            Image badgeBg = badgeObj.AddComponent<Image>();
            badgeBg.color = new Color(0.10f, 0.12f, 0.19f, 0.92f);

            GameObject iconObj = CreateUIObject("CoinIcon", badgeObj.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(32f, 0f), new Vector2(40f, 40f));
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = coinSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            TMP_Text coinLabel = CreateTmpLabel("CoinsText", badgeObj.transform,
                new Vector2(24f, 0f), new Vector2(160f, 44f),
                "0 XU", 26, TextAlignmentOptions.MidlineRight, new Color(1f, 0.86f, 0.28f, 1f));
            coinLabel.raycastTarget = false;

            GameObject flyLayerObj = CreateFullRect("CoinFlyOverlayLayer", canvasTransform);
            RectTransform flyRt = flyLayerObj.GetComponent<RectTransform>();

            CoinCounterUI counter = badgeObj.AddComponent<CoinCounterUI>();
            counter.Configure(coinLabel, iconImg, badgeObj.GetComponent<RectTransform>(), flyRt, coinSprite);
            return counter;
        }

        private static GameObject BuildModeSelectPanel(
            Transform parent,
            out Button campaignBtn, out Button endlessBtn, out Button trainingBtn, out Button shopBtn)
        {
            GameObject panel = CreateFullRect("SubScreen_ModeSelect", parent);
            CreateLabel("ModeSelectTitle", panel.transform, new Vector2(0f, 420f), new Vector2(1200f, 68f),
                "CHỌN CHẾ ĐỘ CHƠI", 46, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            campaignBtn = CreateModeCard(panel.transform, "ModeCard_Campaign", new Vector2(-340f, 110f),
                "CHIẾN DỊCH",
                "CAMPAIGN  >",
                new Color(0.18f, 0.44f, 0.72f, 0.96f));

            endlessBtn = CreateModeCard(panel.transform, "ModeCard_Endless", new Vector2(340f, 110f),
                "VÔ TẬN",
                "ENDLESS WAVE  >",
                new Color(0.68f, 0.24f, 0.20f, 0.96f));

            trainingBtn = CreateModeCard(panel.transform, "ModeCard_Training", new Vector2(-340f, -155f),
                "LUYỆN TẬP",
                "TRAINING  >",
                new Color(0.18f, 0.56f, 0.38f, 0.96f));

            shopBtn = CreateModeCard(panel.transform, "ModeCard_Shop", new Vector2(340f, -155f),
                "CỬA HÀNG",
                "SHOP  >",
                new Color(0.64f, 0.46f, 0.14f, 0.96f));

            return panel;
        }

        private static Button CreateModeCard(Transform parent, string name, Vector2 pos, string title, string ctaLabel, Color bgColor)
        {
            GameObject cardObj = CreateUIObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(580f, 200f));
            Image bg = cardObj.AddComponent<Image>();
            bg.color = bgColor;
            Button btn = cardObj.AddComponent<Button>();
            btn.targetGraphic = bg;

            Text titleTxt = CreateLabel("Title", cardObj.transform, new Vector2(0f, 22f), new Vector2(540f, 64f),
                title, 40, TextAnchor.MiddleCenter, new Color(1f, 0.94f, 0.55f, 1f), FontStyle.Bold);
            titleTxt.raycastTarget = false;

            Text ctaTxt = CreateLabel("ModeBadge", cardObj.transform, new Vector2(0f, -42f), new Vector2(420f, 36f),
                ctaLabel, 22, TextAnchor.MiddleCenter, new Color(0.95f, 0.97f, 1f, 0.92f), FontStyle.Bold);
            ctaTxt.raycastTarget = false;

            return btn;
        }

        private static GameObject BuildShopPanel(Transform parent, out ShopManager shopManager)
        {
            GameObject panel = CreateFullRect("SubScreen_Shop", parent);
            shopManager = panel.AddComponent<ShopManager>();

            CreateLabel("ShopTitle", panel.transform, new Vector2(0f, 445f), new Vector2(1200f, 64f),
                "CỬA HÀNG VẬT PHẨM", 42, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f), FontStyle.Bold);

            // Top Summary Strip
            GameObject summaryBar = CreateUIObject("ShopSummaryBar", panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 345f), new Vector2(1480f, 84f));
            Image summaryBg = summaryBar.AddComponent<Image>();
            summaryBg.color = new Color(0.09f, 0.12f, 0.19f, 0.92f);

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

            // 3 Concise Shop Rows: Icon | Name | Price | Owned | Buy Button ("MUA")
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
                float yPos = 160f - i * 180f;
                GameObject rowObj = CreateUIObject($"ShopRow_{spec.type}", panel.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, yPos), new Vector2(1480f, 150f));
                Image rowBg = rowObj.AddComponent<Image>();
                rowBg.color = new Color(0.11f, 0.14f, 0.22f, 0.94f);

                GameObject iconFrame = CreateUIObject("IconFrame", rowObj.transform,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(24f, 0f), new Vector2(114f, 114f));
                Image iconFrameBg = iconFrame.AddComponent<Image>();
                iconFrameBg.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);

                GameObject iconChild = CreateUIObject("ItemIcon", iconFrame.transform,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14f, -14f));
                Image iconImg = iconChild.AddComponent<Image>();
                iconImg.sprite = SpriteHelper.LoadSprite(spec.iconPath);
                iconImg.preserveAspect = true;

                Text nameTxt = CreateLabel("NameText", rowObj.transform, new Vector2(-315f, 0f), new Vector2(490f, 48f),
                    spec.title, 30, TextAnchor.MiddleLeft, new Color(1f, 0.90f, 0.42f, 1f), FontStyle.Bold);
                Text priceTxt = CreateLabel("PriceText", rowObj.transform, new Vector2(55f, 0f), new Vector2(190f, 44f),
                    $"{spec.price} Xu", 26, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.25f, 1f), FontStyle.Bold);
                Text ownedTxt = CreateLabel("OwnedText", rowObj.transform, new Vector2(280f, 0f), new Vector2(220f, 44f),
                    "Sở hữu: 0", 25, TextAnchor.MiddleLeft, new Color(0.60f, 0.94f, 0.72f, 1f), FontStyle.Bold);

                Button buyBtn = CreateSolidButton("BuyButton", rowObj.transform,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-34f, 0f), new Vector2(200f, 76f), "MUA", new Color(0.18f, 0.62f, 0.34f, 0.98f), 28);

                rows[i] = new ShopItemRow
                {
                    itemType = spec.type,
                    buyButton = buyBtn,
                    iconImage = iconImg,
                    nameText = nameTxt,
                    priceText = priceTxt,
                    ownedCountText = ownedTxt
                };
            }

            Text feedbackTxt = CreateLabel("ShopFeedbackText", panel.transform, new Vector2(0f, -380f), new Vector2(1200f, 48f),
                string.Empty, 26, TextAnchor.MiddleCenter, new Color(0.45f, 0.95f, 0.55f, 1f), FontStyle.Bold);

            shopManager.ConfigureBuilderReferences(balanceTxt, feedbackTxt, slotQTxt, slotETxt, slotQIcon, slotEIcon, rows);
            return panel;
        }

        private static void BuildCircularLoadoutSlot(
            Transform parent, string name, Vector2 pos, string keyText, Color borderColor,
            out Button slotBtn, out Image slotIcon, out TMP_Text countTxt, out TMP_Text nameTxt)
        {
            Sprite circleSprite = SpriteHelper.GetOrCreateCircleSprite();

            GameObject holder = CreateUIObject(name, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(240f, 128f));

            GameObject circleObj = CreateUIObject("CircleSlot", holder.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 12f), new Vector2(106f, 106f));
            Image circleBorder = circleObj.AddComponent<Image>();
            circleBorder.sprite = circleSprite;
            circleBorder.color = borderColor;
            slotBtn = circleObj.AddComponent<Button>();
            slotBtn.targetGraphic = circleBorder;

            GameObject maskObj = CreateUIObject("CircleMask", circleObj.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -8f));
            Image maskImg = maskObj.AddComponent<Image>();
            maskImg.sprite = circleSprite;
            maskImg.color = new Color(0.10f, 0.14f, 0.22f, 0.98f);
            maskImg.raycastTarget = false;
            Mask mask = maskObj.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            GameObject iconObj = CreateUIObject("ItemIcon", maskObj.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-18f, -18f));
            slotIcon = iconObj.AddComponent<Image>();
            slotIcon.preserveAspect = true;
            slotIcon.raycastTarget = false;

            GameObject keyBadge = CreateUIObject("KeyBadge", circleObj.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(16f, -14f), new Vector2(34f, 34f));
            Image keyBg = keyBadge.AddComponent<Image>();
            keyBg.sprite = circleSprite;
            keyBg.color = new Color(0.95f, 0.68f, 0.16f, 1f);
            keyBg.raycastTarget = false;
            TMP_Text keyTmp = CreateTmpLabel("KeyTxt", keyBadge.transform, Vector2.zero, new Vector2(32f, 32f),
                keyText, 18, TextAlignmentOptions.Center, Color.black);
            keyTmp.raycastTarget = false;

            GameObject countBadge = CreateUIObject("CountBadge", circleObj.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-14f, 12f), new Vector2(44f, 28f));
            Image countBg = countBadge.AddComponent<Image>();
            countBg.sprite = circleSprite;
            countBg.color = new Color(0.06f, 0.08f, 0.13f, 0.96f);
            countBg.raycastTarget = false;
            countTxt = CreateTmpLabel("ItemCountTxt", countBadge.transform, Vector2.zero, new Vector2(42f, 26f),
                "x0", 16, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.32f, 1f));
            countTxt.raycastTarget = false;

            nameTxt = CreateTmpLabel("ItemNameTxt", holder.transform,
                new Vector2(0f, -52f), new Vector2(230f, 24f),
                "Trống", 17, TextAlignmentOptions.Center, Color.white);
            nameTxt.raycastTarget = false;
        }

        private static TMP_Text CreateTmpLabel(
            string name, Transform parent, Vector2 pos, Vector2 size,
            string content, float fontSize, TextAlignmentOptions align, Color color)
        {
            GameObject obj = CreateUIObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private static MenuWorldCardUI BuildWorldCard(
            Transform parent, Vector2 pos, string worldTitle, Sprite previewSprite)
        {
            GameObject cardObj = CreateUIObject($"WorldCard_{worldTitle.Replace('\n', '_')}", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(450f, 470f));

            Image highlightImg = cardObj.AddComponent<Image>();
            highlightImg.color = new Color(0.28f, 0.34f, 0.45f, 0.75f);

            GameObject innerObj = CreateUIObject("CardInner", cardObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14f, -14f));
            Image innerBg = innerObj.AddComponent<Image>();
            innerBg.color = new Color(0.11f, 0.14f, 0.21f, 0.96f);

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
            badgeBg.color = new Color(0.92f, 0.62f, 0.12f, 1f);
            badgeBg.raycastTarget = false;
            Text badgeLabel = CreateLabel("SelectedText", badgeObj.transform, Vector2.zero, new Vector2(210f, 40f),
                "ĐÃ CHỌN", 22, TextAnchor.MiddleCenter, Color.black, FontStyle.Bold);
            badgeLabel.raycastTarget = false;

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

        private static Button BuildStageCard(Transform parent, Vector2 pos, string stageName, Color bgColor)
        {
            GameObject cardObj = CreateUIObject($"StageCard_{stageName}", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(340f, 240f));
            Image bg = cardObj.AddComponent<Image>();
            bg.color = bgColor;
            Button btn = cardObj.AddComponent<Button>();
            btn.targetGraphic = bg;

            Text titleTxt = CreateLabel("StageName", cardObj.transform, Vector2.zero, new Vector2(320f, 220f),
                stageName, 46, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            RectTransform trt = titleTxt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            titleTxt.raycastTarget = false;

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
            return txt;
        }

        private static Button CreateSolidButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, string label, Color bgColor, int fontSize)
        {
            GameObject btnObj = CreateUIObject(name, parent, anchorMin, anchorMax, pivot, pos, size);
            Image bg = btnObj.AddComponent<Image>();
            bg.color = bgColor;
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;

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
