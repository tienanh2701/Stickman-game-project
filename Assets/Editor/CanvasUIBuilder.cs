using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using TMPro;
using StickmanBeatEmUp.UI;

namespace StickmanBeatEmUp.Editor
{
    /// <summary>
    /// Tự động sinh hệ thống Canvas UI Beat 'em up chuẩn chỉnh:
    /// - Thanh Máu Player (Slider + Text)
    /// - Thanh Nộ 30 điểm chia làm 3 mốc (3 Segment Sliders + Text)
    /// - Đèn báo biến hình & Special Combo
    /// - Thanh Máu Boss (Panel + Slider)
    /// - Thông tin Wave & Số quái còn lại
    /// - Bảng Victory & Game Over
    /// Tự động liên kết tất cả với GameUIManager
    /// </summary>
    public static class CanvasUIBuilder
    {
        public struct TrainingHudRefs
        {
            public GameObject panel;
            public Button autoHpBtn;
            public TMP_Text autoHpTxt;
            public Button autoRageBtn;
            public TMP_Text autoRageTxt;
            public Button clearBtn;
            public Button[] spawnBtns;
            public TMP_Text[] spawnLbls;

            // Top-Dock Training Control Bar fields
            public Button playerInvincibleBtn;
            public TMP_Text playerInvincibleTxt;
            public Button autoMaxRageBtn;
            public TMP_Text autoMaxRageTxt;
            public Button enemyAiBtn;
            public TMP_Text enemyAiTxt;
            public Button enemyInvincibleBtn;
            public TMP_Text enemyInvincibleTxt;
            public Image[] spawnIcons;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esObj.AddComponent<InputSystemUIInputModule>();
#else
            esObj.AddComponent<StandaloneInputModule>();
#endif
        }

        public static GameObject CreateGameUICanvas() => CreateGameUICanvas(out _);

        public static GameObject CreateGameUICanvas(out TrainingHudRefs trainingRefs)
        {
            EnsureEventSystem();

            // 1. Tạo Canvas gốc
            GameObject canvasObj = new GameObject("GameCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            GameUIManager uiManager = canvasObj.AddComponent<GameUIManager>();

            SerializedObject so = new SerializedObject(uiManager);

            // 2. Tạo cụm Player HUD (Góc trên bên trái)
            GameObject playerHud = CreateUIObject("PlayerHUD", canvasObj.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(250, -105), new Vector2(450, 195));

            // Thanh máu
            GameObject hpSliderObj = CreateSlider("PlayerHPBar", playerHud.transform, new Vector2(0, 56), new Vector2(400, 32), new Color(0.15f, 0.15f, 0.15f), new Color(0.85f, 0.15f, 0.15f));
            Slider hpSlider = hpSliderObj.GetComponent<Slider>();
            GameObject hpTextObj = CreateText("HPText", hpSliderObj.transform, "MÁU: 100 / 100", 20, TextAlignmentOptions.Center, Color.white);
            RectTransform hpTextRect = hpTextObj.GetComponent<RectTransform>();
            hpTextRect.anchorMin = Vector2.zero; hpTextRect.anchorMax = Vector2.one; hpTextRect.offsetMin = Vector2.zero; hpTextRect.offsetMax = Vector2.zero;

            // Thanh Giáp Ảo (Dịch Tủy Titan)
            GameObject shieldContainer = CreateUIObject("ShieldBar", playerHud.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(400, 22));
            Image shieldBg = shieldContainer.AddComponent<Image>();
            shieldBg.color = new Color(0.08f, 0.14f, 0.18f, 0.92f);

            GameObject shieldFillObj = new GameObject("ShieldFill", typeof(RectTransform), typeof(Image));
            shieldFillObj.transform.SetParent(shieldContainer.transform, false);
            RectTransform sfRt = shieldFillObj.GetComponent<RectTransform>();
            sfRt.anchorMin = Vector2.zero; sfRt.anchorMax = Vector2.one; sfRt.offsetMin = Vector2.zero; sfRt.offsetMax = Vector2.zero;
            Image shieldFillImg = shieldFillObj.GetComponent<Image>();
            shieldFillImg.sprite = SpriteHelper.GetOrCreateSquareSprite();
            shieldFillImg.type = Image.Type.Filled;
            shieldFillImg.fillMethod = Image.FillMethod.Horizontal;
            shieldFillImg.fillOrigin = 0;
            shieldFillImg.fillAmount = 0f;
            shieldFillImg.color = new Color(0.20f, 0.88f, 0.96f, 0.95f);

            GameObject shieldTxtObj = CreateText("ShieldText", shieldContainer.transform, "GIÁP ẢO: 0", 15, TextAlignmentOptions.Center, Color.white);
            RectTransform stRt = shieldTxtObj.GetComponent<RectTransform>();
            stRt.anchorMin = Vector2.zero; stRt.anchorMax = Vector2.one; stRt.offsetMin = Vector2.zero; stRt.offsetMax = Vector2.zero;
            TextMeshProUGUI shieldTxt = shieldTxtObj.GetComponent<TextMeshProUGUI>();
            shieldContainer.SetActive(false);

            // Thanh Nộ (Chia làm 3 Segment mốc 10)
            GameObject rageContainer = CreateUIObject("RageContainer", playerHud.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(400, 40));
            Slider[] rageSliders = new Slider[3];
            float segWidth = 125f;
            float gap = 8f;

            for (int i = 0; i < 3; i++)
            {
                float posX = -segWidth - gap + (i * (segWidth + gap));
                GameObject segObj = CreateSlider($"RageSegment_{i + 1}", rageContainer.transform, new Vector2(posX, 0), new Vector2(segWidth, 24), new Color(0.2f, 0.2f, 0.2f), new Color(1f, 0.65f, 0f));
                rageSliders[i] = segObj.GetComponent<Slider>();
            }

            GameObject rageTextObj = CreateText("RagePointsText", rageContainer.transform, "NỘ: 0 / 30", 18, TextAlignmentOptions.Center, Color.yellow);
            RectTransform rageTextRect = rageTextObj.GetComponent<RectTransform>();
            rageTextRect.anchoredPosition = new Vector2(0, -28);
            rageTextRect.sizeDelta = new Vector2(300, 30);

            // [S] Block Status Badge
            GameObject blockContainer = CreateUIObject("BlockStatusBadge", playerHud.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-115, -72), new Vector2(180, 26));
            Image blockBg = blockContainer.AddComponent<Image>();
            blockBg.color = new Color(0.08f, 0.14f, 0.22f, 0.90f);

            GameObject blockFillObj = new GameObject("BlockFill", typeof(RectTransform), typeof(Image));
            blockFillObj.transform.SetParent(blockContainer.transform, false);
            RectTransform bfRt = blockFillObj.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero; bfRt.anchorMax = Vector2.one; bfRt.offsetMin = Vector2.zero; bfRt.offsetMax = Vector2.zero;
            Image blockFillImg = blockFillObj.GetComponent<Image>();
            blockFillImg.sprite = SpriteHelper.GetOrCreateSquareSprite();
            blockFillImg.type = Image.Type.Filled;
            blockFillImg.fillMethod = Image.FillMethod.Horizontal;
            blockFillImg.fillOrigin = 0;
            blockFillImg.fillAmount = 1f;
            blockFillImg.color = new Color(0.15f, 0.72f, 0.95f, 0.45f);

            GameObject blockTxtObj = CreateText("BlockStatusText", blockContainer.transform, "[S] ĐỠ ĐÒN: SẴN SÀNG", 14, TextAlignmentOptions.Center, new Color(0.72f, 0.95f, 1f, 1f));
            RectTransform btRt = blockTxtObj.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero; btRt.anchorMax = Vector2.one; btRt.offsetMin = Vector2.zero; btRt.offsetMax = Vector2.zero;
            TextMeshProUGUI blockTxt = blockTxtObj.GetComponent<TextMeshProUGUI>();

            // Đèn báo Transform & Combo
            GameObject transformPrompt = CreatePromptBadge("TransformReadyPrompt", playerHud.transform, new Vector2(0, -108), "NHẤN [O] ĐỂ BIẾN HÌNH!", Color.cyan);
            GameObject comboPrompt = CreatePromptBadge("SpecialComboReadyPrompt", playerHud.transform, new Vector2(0, -140), "DASH + [I] TUYỆT KỸ ĐẶC BIỆT!", Color.magenta);

            // 2B. Bottom-Left Circular Q & E Item Slots
            Sprite emptySlotSprite = SpriteHelper.LoadSprite("Assets/Sprites/UI/Items/Icon_LockedSlot.png", SpriteHelper.GetOrCreateCircleSprite());
            GameObject itemPanel = CreateUIObject("HudItemSlotsPanel", canvasObj.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 62), new Vector2(230, 96));
            HudItemSlotsUI itemSlotsUI = itemPanel.AddComponent<HudItemSlotsUI>();
            BuildHudCircleSlot(itemPanel.transform, "SlotQ", new Vector2(48, 48), "Q", out Image qBorder, out Image qIcon, out TMP_Text qKeyText, out TMP_Text qCountText);
            BuildHudCircleSlot(itemPanel.transform, "SlotE", new Vector2(154, 48), "E", out Image eBorder, out Image eIcon, out TMP_Text eKeyText, out TMP_Text eCountText);
            itemSlotsUI.ConfigureSlots(qBorder, qIcon, qKeyText, qCountText, eBorder, eIcon, eKeyText, eCountText, blockTxt, blockFillImg, blockContainer, emptySlotSprite);
            EditorUtility.SetDirty(itemSlotsUI);

            // 3. Cụm Boss Health Bar (Ở giữa phía trên màn hình, hạ xuống dưới Top Control Bar)
            GameObject bossPanel = CreateUIObject("BossHealthPanel", canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -112), new Vector2(700, 60));
            GameObject bossSliderObj = CreateSlider("BossHPSlider", bossPanel.transform, Vector2.zero, new Vector2(680, 36), new Color(0.2f, 0.05f, 0.05f), new Color(0.9f, 0.1f, 0.1f));
            Slider bossSlider = bossSliderObj.GetComponent<Slider>();
            GameObject bossNameObj = CreateText("BossNameText", bossPanel.transform, "TRÙM THẾ GIỚI", 22, TextAlignmentOptions.Center, Color.white);
            RectTransform bossNameRect = bossNameObj.GetComponent<RectTransform>();
            bossNameRect.anchoredPosition = new Vector2(0, 32);
            bossNameRect.sizeDelta = new Vector2(680, 30);

            GameObject bossStatusObj = CreateText("BossStatusText", bossPanel.transform, "", 18, TextAlignmentOptions.Center, Color.yellow);
            RectTransform bossStatusRect = bossStatusObj.GetComponent<RectTransform>();
            bossStatusRect.anchoredPosition = new Vector2(0, -32);
            bossStatusRect.sizeDelta = new Vector2(720, 28);

            // 3B. Top-Center Endless Wave Banner
            GameObject endlessBanner = CreateUIObject("EndlessWaveBanner", canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(420, 44));
            Image endlessBg = endlessBanner.AddComponent<Image>();
            endlessBg.color = new Color(0.28f, 0.08f, 0.10f, 0.88f);
            GameObject endlessTxtObj = CreateText("EndlessWaveBannerText", endlessBanner.transform, "LÀN SÓNG 1", 24, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.25f, 1f));
            RectTransform ebRt = endlessTxtObj.GetComponent<RectTransform>();
            ebRt.anchorMin = Vector2.zero; ebRt.anchorMax = Vector2.one; ebRt.offsetMin = Vector2.zero; ebRt.offsetMax = Vector2.zero;
            TextMeshProUGUI endlessBannerTxt = endlessTxtObj.GetComponent<TextMeshProUGUI>();
            endlessBanner.SetActive(false);

            // 4. Cụm Top-Right: Coin Counter + [|| TẠM DỪNG] + WaveInfoPanel + Compact Menu Exit
            Sprite coinSprite = SpriteHelper.LoadSprite("Assets/Sprites/UI/Items/Icon_CoinXu.png", SpriteHelper.GetOrCreateCircleSprite());
            GameObject coinBadge = CreateUIObject("BattleCoinBadge", canvasObj.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-210, -18), new Vector2(196, 46));
            Image coinBadgeBg = coinBadge.AddComponent<Image>();
            coinBadgeBg.color = new Color(0.09f, 0.12f, 0.18f, 0.90f);

            GameObject coinIconObj = CreateUIObject("CoinIcon", coinBadge.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(32f, 32f));
            Image coinIconImg = coinIconObj.AddComponent<Image>();
            coinIconImg.sprite = coinSprite;
            coinIconImg.preserveAspect = true;
            coinIconImg.raycastTarget = false;

            GameObject coinLblObj = CreateText("CoinsText", coinBadge.transform, "0 XU", 21, TextAlignmentOptions.MidlineRight, new Color(1f, 0.86f, 0.28f, 1f));
            RectTransform coinLblRt = coinLblObj.GetComponent<RectTransform>();
            coinLblRt.anchorMin = Vector2.zero; coinLblRt.anchorMax = Vector2.one;
            coinLblRt.offsetMin = new Vector2(46, 0); coinLblRt.offsetMax = new Vector2(-12, 0);

            GameObject flyOverlay = CreateUIObject("CoinFlyOverlayLayer", canvasObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            CoinCounterUI coinUi = coinBadge.AddComponent<CoinCounterUI>();
            coinUi.Configure(coinLblObj.GetComponent<TextMeshProUGUI>(), coinIconImg, coinBadge.GetComponent<RectTransform>(), flyOverlay.GetComponent<RectTransform>(), coinSprite);
            EditorUtility.SetDirty(coinUi);

            Button pauseHudBtn = CreateButton("PauseHudButton", canvasObj.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30, -18), new Vector2(168, 46), "|| TẠM DỪNG", new Color(0.20f, 0.26f, 0.38f, 0.95f), 19);

            GameObject waveInfoPanel = CreateUIObject("WaveInfoPanel", canvasObj.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30, -76), new Vector2(480, 76));
            GameObject waveTextObj = CreateText("WaveText", waveInfoPanel.transform, "TG1-M1 | ĐỢT: 1 / 3", 21, TextAlignmentOptions.Right, Color.white);
            RectTransform waveRect = waveTextObj.GetComponent<RectTransform>();
            waveRect.anchorMin = new Vector2(0f, 0.5f); waveRect.anchorMax = new Vector2(1f, 1f);
            waveRect.offsetMin = Vector2.zero; waveRect.offsetMax = Vector2.zero;

            GameObject enemiesTextObj = CreateText("EnemiesText", waveInfoPanel.transform, "Quái còn lại: 0", 20, TextAlignmentOptions.Right, Color.red);
            RectTransform enemiesRect = enemiesTextObj.GetComponent<RectTransform>();
            enemiesRect.anchorMin = new Vector2(0f, 0f); enemiesRect.anchorMax = new Vector2(1f, 0.5f);
            enemiesRect.offsetMin = Vector2.zero; enemiesRect.offsetMax = Vector2.zero;

            // Nút Thoát nhanh về Menu (nhỏ gọn dưới WaveInfoPanel để không tranh chỗ nút Tạm Dừng)
            Button hudMenuBtn = CreateButton("HudMenuButton", canvasObj.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30, -162), new Vector2(140, 36), "THOÁT", new Color(0.22f, 0.25f, 0.35f, 0.85f), 17);

            // 4B. Top-Dock Training Mode Control Bar (3 nhóm: Player Controls | Enemy Controls | Spawn Icons + Clear)
            trainingRefs = BuildTrainingTopBar(canvasObj.transform);

            // Hướng dẫn phím điều khiển (Góc dưới bên trái)
            GameObject hintBar = CreateUIObject("ControlsHint", canvasObj.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30, 14), new Vector2(1860, 34));
            Image hintBg = hintBar.AddComponent<Image>();
            hintBg.color = new Color(0f, 0f, 0f, 0.55f);
            GameObject hintText = CreateText("ControlsHintText", hintBar.transform, "[A/D] Di chuyển | [2x A/D] Lướt | [W/Space] Nhảy | [S+Space] Rơi thềm | [J] Đánh | [S] Khiên (0.75s) | [U/I] Kỹ năng | [O] Biến hình | [Q/E] Vật phẩm | [ESC] Tạm dừng", 14.5f, TextAlignmentOptions.Center, new Color(0.92f, 0.92f, 0.92f));
            RectTransform htrt = hintText.GetComponent<RectTransform>();
            htrt.anchorMin = Vector2.zero; htrt.anchorMax = Vector2.one;
            htrt.offsetMin = new Vector2(8, 0); htrt.offsetMax = new Vector2(-8, 0);

            // 5. Cụm Victory & Defeat Panel + Pause Modal Panel (Cuối Hierarchy để phủ toàn màn hình)
            GameObject victoryPanel = CreateOverlayPanel("VictoryPanel", canvasObj.transform, "CHIẾN THẮNG!", Color.green, new Color(0, 0, 0, 0.65f));
            TextMeshProUGUI victoryTitle = victoryPanel.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            Button victoryNextBtn = CreateButton("NextStageButton", victoryPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(260, 56), "MÀN KẾ TIẾP", new Color(0.18f, 0.65f, 0.28f), 22);
            Button victoryRetryBtn = CreateButton("VictoryRetryButton", victoryPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150, -135), new Vector2(220, 50), "CHƠI LẠI", new Color(0.25f, 0.45f, 0.75f), 20);
            Button victoryMenuBtn = CreateButton("VictoryMenuButton", victoryPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150, -135), new Vector2(240, 50), "THOÁT", new Color(0.32f, 0.32f, 0.38f), 20);

            GameObject defeatPanel = CreateOverlayPanel("DefeatPanel", canvasObj.transform, "THẤT BẠI!", Color.red, new Color(0, 0, 0, 0.75f));
            TextMeshProUGUI defeatTitle = defeatPanel.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            Button defeatRetryBtn = CreateButton("DefeatRetryButton", defeatPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-140, -70), new Vector2(230, 56), "THỬ LẠI", new Color(0.75f, 0.22f, 0.22f), 22);
            Button defeatMenuBtn = CreateButton("DefeatMenuButton", defeatPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(140, -70), new Vector2(250, 56), "THOÁT", new Color(0.3f, 0.3f, 0.36f), 22);

            // Pause Modal
            GameObject pauseModalPanel = CreateUIObject("PauseModalPanel", canvasObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image pauseDim = pauseModalPanel.AddComponent<Image>();
            pauseDim.color = new Color(0f, 0f, 0f, 0.78f);

            GameObject pauseBox = CreateUIObject("PauseBox", pauseModalPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 410));
            Image pauseBoxBg = pauseBox.AddComponent<Image>();
            pauseBoxBg.color = new Color(0.10f, 0.13f, 0.20f, 0.98f);

            GameObject pauseTitleObj = CreateText("PauseTitle", pauseBox.transform, "TẠM DỪNG", 42, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.32f, 1f));
            RectTransform ptRt = pauseTitleObj.GetComponent<RectTransform>();
            ptRt.anchoredPosition = new Vector2(0, 145);
            ptRt.sizeDelta = new Vector2(460, 60);

            Button resumeBtn = CreateButton("ResumeButton", pauseBox.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 48), new Vector2(360, 66), "TIẾP TỤC", new Color(0.18f, 0.62f, 0.34f, 0.98f), 25);
            Button pauseRetryBtn = CreateButton("PauseRetryButton", pauseBox.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(360, 66), "CHƠI LẠI", new Color(0.22f, 0.46f, 0.76f, 0.98f), 25);
            Button pauseExitBtn = CreateButton("PauseExitMenuButton", pauseBox.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -116), new Vector2(360, 66), "THOÁT RA MENU", new Color(0.58f, 0.20f, 0.24f, 0.98f), 24);
            pauseModalPanel.SetActive(false);

            PauseMenuUI pauseUi = canvasObj.AddComponent<PauseMenuUI>();
            pauseUi.Configure(pauseModalPanel, pauseHudBtn, resumeBtn, pauseRetryBtn, pauseExitBtn);
            EditorUtility.SetDirty(pauseUi);

            // Gán SerializedProperties vào GameUIManager
            so.FindProperty("playerHealthSlider").objectReferenceValue = hpSlider;
            so.FindProperty("playerHealthText").objectReferenceValue = hpTextObj.GetComponent<TextMeshProUGUI>();

            SerializedProperty rageSegsProp = so.FindProperty("rageSegmentSliders");
            rageSegsProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                rageSegsProp.GetArrayElementAtIndex(i).objectReferenceValue = rageSliders[i];
            }

            so.FindProperty("ragePointsText").objectReferenceValue = rageTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("transformReadyPrompt").objectReferenceValue = transformPrompt;
            so.FindProperty("specialComboReadyPrompt").objectReferenceValue = comboPrompt;

            so.FindProperty("bossHealthPanel").objectReferenceValue = bossPanel;
            so.FindProperty("bossHealthSlider").objectReferenceValue = bossSlider;
            so.FindProperty("bossNameText").objectReferenceValue = bossNameObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("bossStatusText").objectReferenceValue = bossStatusObj.GetComponent<TextMeshProUGUI>();

            so.FindProperty("waveText").objectReferenceValue = waveTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("enemiesRemainingText").objectReferenceValue = enemiesTextObj.GetComponent<TextMeshProUGUI>();
            so.FindProperty("victoryPanel").objectReferenceValue = victoryPanel;
            so.FindProperty("defeatPanel").objectReferenceValue = defeatPanel;

            so.FindProperty("hudMenuButton").objectReferenceValue = hudMenuBtn;
            so.FindProperty("victoryNextButton").objectReferenceValue = victoryNextBtn;
            so.FindProperty("victoryRetryButton").objectReferenceValue = victoryRetryBtn;
            so.FindProperty("victoryMenuButton").objectReferenceValue = victoryMenuBtn;
            so.FindProperty("defeatRetryButton").objectReferenceValue = defeatRetryBtn;
            so.FindProperty("defeatMenuButton").objectReferenceValue = defeatMenuBtn;
            so.FindProperty("vietnameseFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SegoeUIBoldVN.ttf");

            so.ApplyModifiedProperties();

            uiManager.ConfigureExpandedHud(
                shieldFillImg, shieldTxt, blockTxt, shieldContainer,
                victoryTitle, defeatTitle, endlessBanner, endlessBannerTxt,
                itemSlotsUI, coinUi, pauseUi
            );
            EditorUtility.SetDirty(uiManager);

            return canvasObj;
        }

        private static void BuildHudCircleSlot(
            Transform parent, string name, Vector2 anchoredPos, string keyLabel,
            out Image borderImg, out Image iconImg, out TMP_Text keyTmp, out TMP_Text countTmp)
        {
            Sprite circleSprite = SpriteHelper.GetOrCreateCircleSprite();
            GameObject slotObj = CreateUIObject(name, parent, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(84, 84));
            borderImg = slotObj.AddComponent<Image>();
            borderImg.sprite = circleSprite;
            borderImg.color = new Color(0.95f, 0.78f, 0.25f, 0.96f);

            GameObject innerObj = CreateUIObject("InnerBg", slotObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8, -8));
            Image innerImg = innerObj.AddComponent<Image>();
            innerImg.sprite = circleSprite;
            innerImg.color = new Color(0.10f, 0.13f, 0.20f, 0.96f);

            GameObject iconObj = CreateUIObject("ItemIcon", innerObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14, -14));
            iconImg = iconObj.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            GameObject keyBadge = CreateUIObject("KeyBadge", slotObj.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(10, -10), new Vector2(28, 28));
            Image keyBg = keyBadge.AddComponent<Image>();
            keyBg.sprite = circleSprite;
            keyBg.color = new Color(0.92f, 0.62f, 0.14f, 1f);
            GameObject keyTxtObj = CreateText("KeyText", keyBadge.transform, keyLabel, 15, TextAlignmentOptions.Center, Color.black);
            RectTransform kRt = keyTxtObj.GetComponent<RectTransform>();
            kRt.anchorMin = Vector2.zero; kRt.anchorMax = Vector2.one; kRt.offsetMin = Vector2.zero; kRt.offsetMax = Vector2.zero;
            keyTmp = keyTxtObj.GetComponent<TextMeshProUGUI>();

            GameObject countPill = CreateUIObject("CountPill", slotObj.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-12, 8), new Vector2(36, 24));
            Image countBg = countPill.AddComponent<Image>();
            countBg.color = new Color(0.06f, 0.08f, 0.14f, 0.95f);
            GameObject countTxtObj = CreateText("CountText", countPill.transform, "x0", 14, TextAlignmentOptions.Center, Color.white);
            RectTransform cRt = countTxtObj.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero; cRt.anchorMax = Vector2.one; cRt.offsetMin = Vector2.zero; cRt.offsetMax = Vector2.zero;
            countTmp = countTxtObj.GetComponent<TextMeshProUGUI>();
        }

        private static TrainingHudRefs BuildTrainingTopBar(Transform canvasTransform)
        {
            // Top-center between PlayerHUD (right edge x=475) and BattleCoinBadge (left edge x=1514)
            GameObject panel = CreateUIObject("TrainingTopBar", canvasTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(34f, -6f), new Vector2(1016f, 88f));
            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.09f, 0.15f, 0.92f);

            // Group 1: Player Controls
            GameObject group1 = CreateUIObject("Group1_PlayerControls", panel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(184f, 76f));
            Image g1Bg = group1.AddComponent<Image>();
            g1Bg.color = new Color(0.11f, 0.15f, 0.22f, 0.92f);

            Button playerInvincibleBtn = CreateButton("PlayerInvincibleButton", group1.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(172f, 32f), "BẤT TỬ PLAYER: TẮT", new Color(0.28f, 0.31f, 0.37f, 0.95f), 11f);
            TMP_Text playerInvincibleTxt = playerInvincibleBtn.GetComponentInChildren<TMP_Text>();

            Button autoMaxRageBtn = CreateButton("AutoMaxRageButton", group1.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(172f, 32f), "MAX NỘ: BẬT", new Color(0.82f, 0.46f, 0.10f, 0.96f), 11f);
            TMP_Text autoMaxRageTxt = autoMaxRageBtn.GetComponentInChildren<TMP_Text>();

            // Group 2: Enemy Controls
            GameObject group2 = CreateUIObject("Group2_EnemyControls", panel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(176f, 76f));
            Image g2Bg = group2.AddComponent<Image>();
            g2Bg.color = new Color(0.11f, 0.15f, 0.22f, 0.92f);

            Button enemyAiBtn = CreateButton("EnemyAiButton", group2.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(164f, 32f), "AI QUÁI: BẬT", new Color(0.16f, 0.58f, 0.34f, 0.96f), 11f);
            TMP_Text enemyAiTxt = enemyAiBtn.GetComponentInChildren<TMP_Text>();

            Button enemyInvincibleBtn = CreateButton("EnemyInvincibleButton", group2.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(164f, 32f), "BẤT TỬ QUÁI: TẮT", new Color(0.28f, 0.31f, 0.37f, 0.95f), 11f);
            TMP_Text enemyInvincibleTxt = enemyInvincibleBtn.GetComponentInChildren<TMP_Text>();

            // Group 3: Spawn Icon Bar + Clear
            GameObject group3 = CreateUIObject("Group3_SpawnAndClear", panel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(384f, 0f), new Vector2(624f, 76f));
            Image g3Bg = group3.AddComponent<Image>();
            g3Bg.color = new Color(0.09f, 0.12f, 0.19f, 0.90f);

            GameObject scrollObj = CreateUIObject("SpawnListScroll", group3.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(504f, 70f));
            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.07f, 0.12f, 0.85f);
            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.scrollSensitivity = 28f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportObj = CreateUIObject("Viewport", scrollObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8f, -6f));
            viewportObj.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportObj.GetComponent<RectTransform>();

            const int unitCount = 13;
            const float tileWidth = 64f;
            const float tileSpacing = 4f;
            const float tileStep = tileWidth + tileSpacing;
            GameObject contentObj = CreateUIObject("Content", viewportObj.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(unitCount * tileStep + 4f, 0f));
            scrollRect.content = contentObj.GetComponent<RectTransform>();

            string[] defaultPrefabPaths =
            {
                "Assets/Prefabs/Enemies/World1_AoT/W1_Creep_PureTitan.prefab",
                "Assets/Prefabs/Enemies/World1_AoT/W1_Range_BeastTitan.prefab",
                "Assets/Prefabs/Enemies/World1_AoT/W1_Elite_FemaleTitan.prefab",
                "Assets/Prefabs/Enemies/World1_AoT/W1_Boss_WarHammer.prefab",
                "Assets/Prefabs/Enemies/World2_DB/W2_Creep_Saibamen.prefab",
                "Assets/Prefabs/Enemies/World2_DB/W2_Creep_SaibamanKamikaze.prefab",
                "Assets/Prefabs/Enemies/World2_DB/W2_Range_FriezaSoldier.prefab",
                "Assets/Prefabs/Enemies/World2_DB/W2_Elite_Nappa.prefab",
                "Assets/Prefabs/Enemies/World2_DB/W2_Boss_Broly.prefab",
                "Assets/Prefabs/Enemies/World3_Diend/W3_Creep_Shocker.prefab",
                "Assets/Prefabs/Enemies/World3_Diend/W3_Range_Kasshin.prefab",
                "Assets/Prefabs/Enemies/World3_Diend/W3_Elite_ShadowMoon.prefab",
                "Assets/Prefabs/Enemies/World3_Diend/W3_Boss_MechaColossus.prefab"
            };
            string[] defaultShortTags =
            {
                "Pure Titan", "Beast", "Female (E)", "WarHam (B)",
                "Saibaman", "Kamikaze", "Frieza", "Nappa (E)", "Broly (B)",
                "Shocker", "Kasshin", "Shadow (E)", "Mecha (B)"
            };

            Button[] spawnBtns = new Button[unitCount];
            TMP_Text[] spawnLbls = new TMP_Text[unitCount];
            Image[] spawnIcons = new Image[unitCount];

            for (int i = 0; i < unitCount; i++)
            {
                float xPos = 4f + i * tileStep;
                GameObject btnObj = CreateUIObject($"SpawnUnit_{i}", contentObj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(xPos, 0f), new Vector2(tileWidth, 60f));
                Image btnBg = btnObj.AddComponent<Image>();
                btnBg.color = new Color(0.18f, 0.25f, 0.38f, 0.96f);
                Button btn = btnObj.AddComponent<Button>();
                btn.targetGraphic = btnBg;

                GameObject iconObj = CreateUIObject("Icon", btnObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 7f), new Vector2(-12f, -20f));
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(defaultPrefabPaths[i]);
                Sprite unitSprite = prefabAsset != null ? prefabAsset.GetComponentInChildren<SpriteRenderer>(true)?.sprite : null;
                iconImg.sprite = unitSprite;
                iconImg.enabled = unitSprite != null;

                GameObject lblObj = CreateText("Label", btnObj.transform, defaultShortTags[i], 9.5f, TextAlignmentOptions.Center, Color.white);
                RectTransform lblRt = lblObj.GetComponent<RectTransform>();
                lblRt.anchorMin = new Vector2(0f, 0f);
                lblRt.anchorMax = new Vector2(1f, 0f);
                lblRt.pivot = new Vector2(0.5f, 0f);
                lblRt.anchoredPosition = new Vector2(0f, 2f);
                lblRt.sizeDelta = new Vector2(-4f, 16f);
                TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
                lblTmp.textWrappingMode = TextWrappingModes.NoWrap;
                lblTmp.overflowMode = TextOverflowModes.Ellipsis;
                lblTmp.raycastTarget = false;

                spawnBtns[i] = btn;
                spawnLbls[i] = lblTmp;
                spawnIcons[i] = iconImg;
            }

            Button clearBtn = CreateButton("ClearAllEnemiesButton", group3.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(108f, 68f), "XÓA SẠCH\nQUÁI", new Color(0.72f, 0.16f, 0.20f, 0.96f), 12f);

            panel.SetActive(false);
            return new TrainingHudRefs
            {
                panel = panel,
                autoHpBtn = playerInvincibleBtn,
                autoHpTxt = playerInvincibleTxt,
                autoRageBtn = autoMaxRageBtn,
                autoRageTxt = autoMaxRageTxt,
                clearBtn = clearBtn,
                spawnBtns = spawnBtns,
                spawnLbls = spawnLbls,
                playerInvincibleBtn = playerInvincibleBtn,
                playerInvincibleTxt = playerInvincibleTxt,
                autoMaxRageBtn = autoMaxRageBtn,
                autoMaxRageTxt = autoMaxRageTxt,
                enemyAiBtn = enemyAiBtn,
                enemyAiTxt = enemyAiTxt,
                enemyInvincibleBtn = enemyInvincibleBtn,
                enemyInvincibleTxt = enemyInvincibleTxt,
                spawnIcons = spawnIcons
            };
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

        private static GameObject CreateSlider(string name, Transform parent, Vector2 pos, Vector2 size, Color bgColor, Color fillColor)
        {
            GameObject sliderObj = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchoredPosition = pos;
            sliderRect.sizeDelta = size;

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;

            // Background
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
            bgObj.GetComponent<Image>().color = bgColor;

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero; fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero; fillAreaRect.offsetMax = Vector2.zero;

            // Fill
            GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
            fillObj.GetComponent<Image>().color = fillColor;

            slider.targetGraphic = bgObj.GetComponent<Image>();
            slider.fillRect = fillRect;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 100f;

            return sliderObj;
        }

        private static GameObject CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions align, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            return obj;
        }

        private static GameObject CreatePromptBadge(string name, Transform parent, Vector2 pos, string text, Color color)
        {
            GameObject obj = CreateUIObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(360, 30));
            Image img = obj.AddComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, 0.25f);

            GameObject txtObj = CreateText("PromptText", obj.transform, text, 16, TextAlignmentOptions.Center, color);
            RectTransform trt = txtObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            return obj;
        }

        public static Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, string label, Color bgColor, float fontSize = 20)
        {
            GameObject btnObj = CreateUIObject(name, parent, anchorMin, anchorMax, pivot, pos, size);
            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject txtObj = CreateText("Label", btnObj.transform, label, fontSize, TextAlignmentOptions.Center, Color.white);
            RectTransform trt = txtObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            return btn;
        }

        private static GameObject CreateOverlayPanel(string name, Transform parent, string title, Color titleColor, Color overlayColor)
        {
            GameObject panel = CreateUIObject(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image img = panel.AddComponent<Image>();
            img.color = overlayColor;

            GameObject titleObj = CreateText("TitleText", panel.transform, title, 56, TextAlignmentOptions.Center, titleColor);
            RectTransform trt = titleObj.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0, 30);
            trt.sizeDelta = new Vector2(600, 80);

            return panel;
        }
    }
}
