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
    /// Tự động sinh hệ thống Canvas UI Beat 'em up Dark Glassmorphism & Sci-Fi HUD chuẩn chỉnh:
    /// - Cụm Sinh Lực Player (Backplate + Thanh Máu 15° + Delayed Trail + Shield + 3-Segment Rage + Status Pills)
    /// - Cụm Unified Top-Center Wave Banner (Wave + Quái còn lại)
    /// - Cụm Top-Right ngang hàng (BattleCoinBadge + PauseHudButton + HudMenuButton)
    /// - Thanh Máu Boss (BossHPBar_Frame + PurpleRed Fill)
    /// - Thanh Control Hints + Nút [?] thu gọn + Bottom-Left Vật phẩm Q/E (giữ nguyên icon gốc)
    /// - Top-Dock Training Control Bar (2x2 LED Toggles + Carousel cuộn < / > + Xóa Quái)
    /// - Pause, Victory & Defeat Floating Glass Modals + Tự động gắn PlayerVitalsHudUI & GameOverModalUI
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

            // 1. Tạo Canvas gốc (1920 x 1080)
            GameObject canvasObj = new GameObject("GameCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            GameUIManager uiManager = canvasObj.AddComponent<GameUIManager>();
            SerializedObject so = new SerializedObject(uiManager);

            // Nạp trước các Sprite SlicedUI (07, 08, 09) + fallback GlassHUD
            Sprite vitalsBackplateSp = LoadHudSprite("07_BattleVitalsHUD/Frame_HUD_VitalsBackplate_Clean.png");
            Sprite hpFrameSp = LoadHudSprite("07_BattleVitalsHUD/Bar_HUD_HP_Frame_Clean.png");
            Sprite hpFillSp = LoadHudSprite("07_BattleVitalsHUD/Bar_HUD_HP_Fill_RedOrange_Clean.png");
            Sprite shieldFillSp = LoadHudSprite("ShieldBar_Fill_CyanGlass.png");
            Sprite rage3SegFrameSp = LoadHudSprite("07_BattleVitalsHUD/Bar_HUD_Rage_3SegFrame_Clean.png");
            Sprite rageSegCyanSp = LoadHudSprite("07_BattleVitalsHUD/Bar_HUD_Rage_SegmentCyanGlow_Clean.png");
            Sprite rageSegDarkSp = LoadHudSprite("07_BattleVitalsHUD/Bar_HUD_Rage_SegmentDark_Clean.png");
            Sprite heartIconSp = LoadHudSprite("07_BattleVitalsHUD/Icon_HUD_HeartCrystal.png");
            Sprite shieldIconSp = LoadHudSprite("07_BattleVitalsHUD/Icon_HUD_ShieldOrb_Green.png");
            Sprite shieldPillReadySp = LoadHudSprite("07_BattleVitalsHUD/Pill_HUD_ShieldReady_Clean.png");
            Sprite shieldPillBlankSp = LoadHudSprite("07_BattleVitalsHUD/Pill_HUD_ShieldReady_Blank.png");
            Sprite hpTrailSp = LoadHudSprite("HPBar_Trail_WhiteAmber.png");
            Sprite boltIconSp = LoadHudSprite("Icon_Bolt_Cyan.png");
            Sprite pauseIconSp = LoadHudSprite("Icon_Pause_Bars.png");
            Sprite exitIconSp = LoadHudSprite("Icon_Exit_Door.png");
            Sprite trashIconSp = LoadHudSprite("Icon_Trash_Red.png");

            Sprite statusCyanSp = LoadHudSprite("StatusPill_CyanGlow.png");
            Sprite bossFrameSp = LoadHudSprite("BossHPBar_Frame.png");
            Sprite bossFillSp = LoadHudSprite("BossHPBar_Fill_PurpleRed.png");
            Sprite waveBannerSp = LoadHudSprite("08_BattleTopHUD/Frame_HUD_StageWaveBanner_Clean.png");
            Sprite topRightClusterSp = LoadHudSprite("08_BattleTopHUD/Frame_HUD_TopRightCluster_Clean.png");
            Sprite currencyBadgeSp = LoadHudSprite("08_BattleTopHUD/Badge_HUD_InGameCoin_Clean.png");
            Sprite pauseBtnSp = LoadHudSprite("08_BattleTopHUD/Btn_HUD_Pause_Clean.png");
            Sprite exitBtnSp = LoadHudSprite("08_BattleTopHUD/Btn_HUD_ExitMatch_Clean.png");
            Sprite controlStripSp = LoadHudSprite("ControlHints_PillStrip.png");
            Sprite keycapDarkSp = LoadHudSprite("Keycap_Badge_Dark.png");

            Sprite pillOrangeSp = LoadHudSprite("PillBtn_OrangePrimary.png");
            Sprite pillNeutralSp = LoadHudSprite("PillBtn_GlassNeutral.png");
            Sprite pillRedSp = LoadHudSprite("PillBtn_RedDanger.png");
            Sprite pillGreenSp = LoadHudSprite("PillBtn_GreenConfirm.png");

            Sprite modalDarkSp = LoadHudSprite("ModalCard_DarkGlass_Cyan.png");
            Sprite modalVictorySp = LoadHudSprite("ModalCard_Victory_Gold.png");
            Sprite modalDefeatSp = LoadHudSprite("ModalCard_Defeat_Crimson.png");

            // 2. Cụm Player HUD (Góc trên bên trái, x = 20..452)
            GameObject playerHud = CreateUIObject("PlayerHUD", canvasObj.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(432f, 196f));

            GameObject backplateObj = CreateFullRect("VitalsBackplate", playerHud.transform);
            Image backplateImg = backplateObj.AddComponent<Image>();
            ApplyImageSprite(backplateImg, vitalsBackplateSp, sliced: true, new Color(0.06f, 0.09f, 0.14f, 0.72f));
            backplateImg.raycastTarget = false;

            GameObject heartObj = CreateUIObject("HeartIcon", playerHud.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(38f, -42f), new Vector2(28f, 28f));
            Image heartImg = heartObj.AddComponent<Image>();
            ApplyImageSprite(heartImg, heartIconSp, sliced: false, new Color(0.95f, 0.20f, 0.30f, 1f), preserveAspect: true);
            heartImg.raycastTarget = false;

            // Thanh máu chính (inside VitalsBackplate inner viewport: x=56..406)
            GameObject hpSliderObj = CreateSlider(
                "PlayerHPBar", playerHud.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(56f, -26f), new Vector2(350f, 32f),
                hpFrameSp, frameSliced: true, new Color(0.10f, 0.13f, 0.18f, 0.88f),
                hpFillSp, Color.white,
                new Vector2(5f, 4f), new Vector2(-5f, -4f));
            Slider hpSlider = hpSliderObj.GetComponent<Slider>();

            GameObject hpTextObj = CreateText("HPText", hpSliderObj.transform, "100 / 100", 18f, TextAlignmentOptions.Center, Color.white);
            RectTransform hpTextRect = hpTextObj.GetComponent<RectTransform>();
            SetFullStretch(hpTextRect, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            TextMeshProUGUI hpTxtTmp = hpTextObj.GetComponent<TextMeshProUGUI>();
            hpTxtTmp.enableAutoSizing = true;
            hpTxtTmp.fontSizeMin = 12.5f;
            hpTxtTmp.fontSizeMax = 18f;
            hpTxtTmp.raycastTarget = false;

            // Thanh Giáp Ảo (Dịch Tủy Titan)
            GameObject shieldContainer = CreateUIObject("ShieldBar", playerHud.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -61f), new Vector2(350f, 18f));
            Image shieldBg = shieldContainer.AddComponent<Image>();
            ApplyImageSprite(shieldBg, hpFrameSp, sliced: true, new Color(0.08f, 0.14f, 0.18f, 0.92f));
            if (hpFrameSp != null) shieldBg.color = new Color(0.55f, 0.92f, 1f, 0.92f);
            shieldBg.raycastTarget = false;

            GameObject shieldFillObj = CreateFullRect("ShieldFill", shieldContainer.transform);
            SetFullStretch(shieldFillObj.GetComponent<RectTransform>(), new Vector2(5f, 2.5f), new Vector2(-5f, -2.5f));
            Image shieldFillImg = shieldFillObj.AddComponent<Image>();
            ApplyFilledSprite(shieldFillImg, shieldFillSp ?? SpriteHelper.GetOrCreateSquareSprite(), new Color(0.22f, 0.90f, 1f, 0.95f));
            shieldFillImg.fillAmount = 0f;
            shieldFillImg.raycastTarget = false;

            GameObject shieldTxtObj = CreateText("ShieldText", shieldContainer.transform, "GIÁP ẢO: 0", 13f, TextAlignmentOptions.Center, Color.white);
            SetFullStretch(shieldTxtObj.GetComponent<RectTransform>(), new Vector2(6f, 0f), new Vector2(-6f, 0f));
            TextMeshProUGUI shieldTxt = shieldTxtObj.GetComponent<TextMeshProUGUI>();
            shieldTxt.raycastTarget = false;
            shieldContainer.SetActive(false);

            // Thanh Nộ 3 mốc (Rage3SegFrame behind 3x RageSegment + RagePointsText)
            GameObject rageContainer = CreateUIObject("RageContainer", playerHud.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -84f), new Vector2(384f, 34f));
            GameObject rageFrameObj = CreateUIObject("Rage3SegFrame", rageContainer.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(304f, 28f));
            Image rageFrameImg = rageFrameObj.AddComponent<Image>();
            ApplyImageSprite(rageFrameImg, rage3SegFrameSp, sliced: true, new Color(0.08f, 0.11f, 0.16f, 0.90f));
            rageFrameImg.raycastTarget = false;

            Slider[] rageSliders = new Slider[3];
            const float segWidth = 95f;
            const float segHeight = 21f;
            const float segStartX = 6f;
            const float segStepX = 98.5f;

            for (int i = 0; i < 3; i++)
            {
                float posX = segStartX + i * segStepX;
                GameObject segObj = CreateSlider(
                    $"RageSegment_{i + 1}", rageContainer.transform,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(posX, 0f), new Vector2(segWidth, segHeight),
                    rageSegDarkSp, frameSliced: true, new Color(0.12f, 0.13f, 0.18f, 0.88f),
                    rageSegCyanSp, Color.white,
                    Vector2.zero, Vector2.zero);
                rageSliders[i] = segObj.GetComponent<Slider>();
                rageSliders[i].maxValue = 10f;
                rageSliders[i].value = 0f;
            }

            GameObject rageTextObj = CreateText("RagePointsText", rageContainer.transform, "NỘ: 0 / 30", 15.5f, TextAlignmentOptions.MidlineRight, new Color(1f, 0.847f, 0.302f, 1f));
            RectTransform rageTextRect = rageTextObj.GetComponent<RectTransform>();
            rageTextRect.anchorMin = new Vector2(1f, 0.5f);
            rageTextRect.anchorMax = new Vector2(1f, 0.5f);
            rageTextRect.pivot = new Vector2(1f, 0.5f);
            rageTextRect.anchoredPosition = Vector2.zero;
            rageTextRect.sizeDelta = new Vector2(76f, 30f);
            rageTextObj.GetComponent<TextMeshProUGUI>().raycastTarget = false;

            // [S] Block Status Pill Badge
            GameObject blockContainer = CreateUIObject("BlockStatusBadge", playerHud.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -126f), new Vector2(188f, 36f));
            Image blockBg = blockContainer.AddComponent<Image>();
            ApplyImageSprite(blockBg, shieldPillReadySp ?? shieldPillBlankSp, sliced: true, new Color(0.08f, 0.24f, 0.18f, 0.90f));
            blockBg.raycastTarget = false;

            GameObject blockFillObj = CreateFullRect("BlockFill", blockContainer.transform);
            SetFullStretch(blockFillObj.GetComponent<RectTransform>(), new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Image blockFillImg = blockFillObj.AddComponent<Image>();
            ApplyFilledSprite(blockFillImg, shieldPillBlankSp ?? shieldPillReadySp ?? SpriteHelper.GetOrCreateSquareSprite(), new Color(0.30f, 0.92f, 0.58f, 0.34f));
            blockFillImg.color = new Color(0.30f, 0.92f, 0.58f, 0.34f);
            blockFillImg.fillAmount = 1f;
            blockFillImg.raycastTarget = false;

            GameObject sIconObj = CreateUIObject("ShieldIcon", blockContainer.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(17f, 0f), new Vector2(24f, 24f));
            Image sIconImg = sIconObj.AddComponent<Image>();
            ApplyImageSprite(sIconImg, shieldIconSp, sliced: false, new Color(0.35f, 1f, 0.65f, 1f), preserveAspect: true);
            sIconImg.raycastTarget = false;
            sIconImg.enabled = shieldPillReadySp == null && sIconImg.sprite != null;

            GameObject blockTxtObj = CreateText("BlockStatusText", blockContainer.transform, "[S] ĐỠ: SẴN SÀNG", 13f, TextAlignmentOptions.Center, new Color(0.72f, 1f, 0.82f, 1f));
            SetFullStretch(blockTxtObj.GetComponent<RectTransform>(), new Vector2(34f, 0f), new Vector2(-8f, 0f));
            TextMeshProUGUI blockTxt = blockTxtObj.GetComponent<TextMeshProUGUI>();
            blockTxt.enableAutoSizing = true;
            blockTxt.fontSizeMin = 9.5f;
            blockTxt.fontSizeMax = 13f;
            blockTxt.raycastTarget = false;

            // Đèn báo Biến hình & Tuyệt kỹ Đặc biệt (ẩn mặc định khi bắt đầu scene)
            GameObject transformPrompt = CreatePromptBadge(
                "TransformReadyPrompt", playerHud.transform, new Vector2(310f, -144f),
                "[O] BIẾN HÌNH", Color.white, new Color(0.68f, 0.98f, 1f, 1f), statusCyanSp, boltIconSp);
            transformPrompt.SetActive(false);
            GameObject comboPrompt = CreatePromptBadge(
                "SpecialComboReadyPrompt", playerHud.transform, new Vector2(310f, -144f),
                "DASH+[I] TUYỆT KỸ", new Color(0.92f, 0.90f, 1f, 1f), new Color(1f, 0.88f, 0.45f, 1f), statusCyanSp, boltIconSp);
            comboPrompt.SetActive(false);

            // 2B. Bottom-Left Circular Q & E Item Slots (giữ nguyên Asset Icon gốc)
            Sprite emptySlotSprite = SpriteHelper.LoadSprite("Assets/Sprites/UI/Items/Icon_LockedSlot.png", SpriteHelper.GetOrCreateCircleSprite());
            Sprite hudOrbSp = LoadHudSprite("HudOrb_Frame_GoldGlass.png");
            Sprite miniPillSp = LoadHudSprite("MiniPill_GoldBadge.png");
            GameObject itemPanel = CreateUIObject("HudItemSlotsPanel", canvasObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(22f, 16f), new Vector2(230f, 96f));
            HudItemSlotsUI itemSlotsUI = itemPanel.AddComponent<HudItemSlotsUI>();
            BuildHudCircleSlot(itemPanel.transform, "SlotQ", new Vector2(48f, 48f), "Q", hudOrbSp, miniPillSp, out Image qBorder, out Image qIcon, out TMP_Text qKeyText, out TMP_Text qCountText);
            BuildHudCircleSlot(itemPanel.transform, "SlotE", new Vector2(154f, 48f), "E", hudOrbSp, miniPillSp, out Image eBorder, out Image eIcon, out TMP_Text eKeyText, out TMP_Text eCountText);
            itemSlotsUI.ConfigureSlots(qBorder, qIcon, qKeyText, qCountText, eBorder, eIcon, eKeyText, eCountText, blockTxt, blockFillImg, blockContainer, emptySlotSprite);
            SerializedObject itemSlotsSo = new SerializedObject(itemSlotsUI);
            itemSlotsSo.FindProperty("hudOrbFrameSprite").objectReferenceValue = hudOrbSp;
            itemSlotsSo.FindProperty("miniPillBadgeSprite").objectReferenceValue = miniPillSp;
            itemSlotsSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemSlotsUI);

            // 3. Cụm Boss Health Bar (Ở giữa phía trên màn hình, dưới Unified Wave Banner / Top Bar)
            GameObject bossPanel = CreateUIObject("BossHealthPanel", canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(680f, 70f));

            GameObject bossNameObj = CreateText("BossNameText", bossPanel.transform, "TRÙM THẾ GIỚI", 20f, TextAlignmentOptions.Center, new Color(1f, 0.94f, 0.72f, 1f));
            RectTransform bossNameRect = bossNameObj.GetComponent<RectTransform>();
            bossNameRect.anchorMin = new Vector2(0.5f, 1f);
            bossNameRect.anchorMax = new Vector2(0.5f, 1f);
            bossNameRect.pivot = new Vector2(0.5f, 1f);
            bossNameRect.anchoredPosition = Vector2.zero;
            bossNameRect.sizeDelta = new Vector2(660f, 25f);
            bossNameObj.GetComponent<TextMeshProUGUI>().raycastTarget = false;

            GameObject bossSliderObj = CreateSlider(
                "BossHPSlider", bossPanel.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -25f), new Vector2(660f, 26f),
                bossFrameSp, frameSliced: true, new Color(0.14f, 0.06f, 0.14f, 0.92f),
                bossFillSp, new Color(0.84f, 0.14f, 0.36f, 1f),
                new Vector2(10f, 4.5f), new Vector2(-10f, -4.5f));
            Slider bossSlider = bossSliderObj.GetComponent<Slider>();

            GameObject bossStatusObj = CreateText("BossStatusText", bossPanel.transform, string.Empty, 15.5f, TextAlignmentOptions.Center, new Color(1f, 0.78f, 0.28f, 1f));
            RectTransform bossStatusRect = bossStatusObj.GetComponent<RectTransform>();
            bossStatusRect.anchorMin = new Vector2(0.5f, 1f);
            bossStatusRect.anchorMax = new Vector2(0.5f, 1f);
            bossStatusRect.pivot = new Vector2(0.5f, 1f);
            bossStatusRect.anchoredPosition = new Vector2(0f, -51f);
            bossStatusRect.sizeDelta = new Vector2(680f, 21f);
            bossStatusObj.GetComponent<TextMeshProUGUI>().raycastTarget = false;
            bossPanel.SetActive(false);

            // 3B. Unified Top-Center Wave Banner (Campaign + Endless, x = 690..1230)
            GameObject endlessBanner = CreateUIObject("EndlessWaveBanner", canvasObj.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(540f, 82f));
            Image endlessBg = endlessBanner.AddComponent<Image>();
            ApplyImageSprite(endlessBg, waveBannerSp, sliced: true, new Color(0.08f, 0.12f, 0.20f, 0.90f));
            endlessBg.raycastTarget = false;

            GameObject waveTextObj = CreateText("WaveText", endlessBanner.transform, "CHIẾN DỊCH - MÀN 1 | ĐỢT: 1 / 3", 20f, TextAlignmentOptions.Center, new Color(1f, 0.847f, 0.294f, 1f));
            RectTransform waveRect = waveTextObj.GetComponent<RectTransform>();
            waveRect.anchorMin = new Vector2(0f, 0.46f);
            waveRect.anchorMax = new Vector2(1f, 0.84f);
            waveRect.pivot = new Vector2(0.5f, 0.5f);
            waveRect.offsetMin = new Vector2(26f, 0f);
            waveRect.offsetMax = new Vector2(-26f, 0f);
            TextMeshProUGUI waveTmp = waveTextObj.GetComponent<TextMeshProUGUI>();
            waveTmp.enableAutoSizing = true;
            waveTmp.fontSizeMin = 14f;
            waveTmp.fontSizeMax = 20.5f;
            waveTmp.raycastTarget = false;

            GameObject enemiesTextObj = CreateText("EnemiesText", endlessBanner.transform, "Quái còn lại: 0", 15.5f, TextAlignmentOptions.Center, new Color(1f, 0.502f, 0.259f, 1f));
            RectTransform enemiesRect = enemiesTextObj.GetComponent<RectTransform>();
            enemiesRect.anchorMin = new Vector2(0f, 0.17f);
            enemiesRect.anchorMax = new Vector2(1f, 0.46f);
            enemiesRect.pivot = new Vector2(0.5f, 0.5f);
            enemiesRect.offsetMin = new Vector2(26f, 0f);
            enemiesRect.offsetMax = new Vector2(-26f, 0f);
            TextMeshProUGUI enemiesTmp = enemiesTextObj.GetComponent<TextMeshProUGUI>();
            enemiesTmp.enableAutoSizing = true;
            enemiesTmp.fontSizeMin = 12f;
            enemiesTmp.fontSizeMax = 16f;
            enemiesTmp.raycastTarget = false;

            // 4. Cụm Top-Right ngang hàng (TopRightClusterBg + BattleCoinBadge + PauseHudButton + HudMenuButton, x = 1440..1906)
            GameObject topRightClusterBg = CreateUIObject("TopRightClusterBg", canvasObj.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(466f, 72f));
            Image topRightClusterImg = topRightClusterBg.AddComponent<Image>();
            ApplyImageSprite(topRightClusterImg, topRightClusterSp, sliced: true, new Color(0.06f, 0.09f, 0.15f, 0.78f));
            topRightClusterImg.raycastTarget = false;

            Sprite coinSprite = LoadHudSprite("02_ModeSelect/Icon_Currency_CoinXu.png")
                ?? SpriteHelper.LoadSprite("Assets/Sprites/UI/Items/Icon_CoinXu.png", SpriteHelper.GetOrCreateCircleSprite());
            GameObject coinBadge = CreateUIObject("BattleCoinBadge", canvasObj.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-308f, -22f), new Vector2(156f, 44f));
            Image coinBadgeBg = coinBadge.AddComponent<Image>();
            ApplyImageSprite(coinBadgeBg, currencyBadgeSp, sliced: true, new Color(0.09f, 0.12f, 0.18f, 0.90f));
            coinBadgeBg.raycastTarget = false;

            GameObject coinIconObj = CreateUIObject("CoinIcon", coinBadge.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(24f, 24f));
            Image coinIconImg = coinIconObj.AddComponent<Image>();
            coinIconImg.sprite = coinSprite;
            coinIconImg.color = Color.white;
            coinIconImg.preserveAspect = true;
            coinIconImg.raycastTarget = false;

            GameObject coinLblObj = CreateText("CoinsText", coinBadge.transform, "0 XU", 18f, TextAlignmentOptions.MidlineRight, new Color(1f, 0.847f, 0.302f, 1f));
            SetFullStretch(coinLblObj.GetComponent<RectTransform>(), new Vector2(38f, 0f), new Vector2(-12f, 0f));
            TextMeshProUGUI coinLblTmp = coinLblObj.GetComponent<TextMeshProUGUI>();
            coinLblTmp.enableAutoSizing = true;
            coinLblTmp.fontSizeMin = 12f;
            coinLblTmp.fontSizeMax = 18.5f;
            coinLblTmp.raycastTarget = false;

            GameObject flyOverlay = CreateUIObject("CoinFlyOverlayLayer", canvasObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            CoinCounterUI coinUi = coinBadge.AddComponent<CoinCounterUI>();
            coinUi.Configure(coinLblTmp, coinIconImg, coinBadge.GetComponent<RectTransform>(), flyOverlay.GetComponent<RectTransform>(), coinSprite);
            SerializedObject coinSo = new SerializedObject(coinUi);
            coinSo.FindProperty("battleBadgeSprite").objectReferenceValue = currencyBadgeSp;
            coinSo.FindProperty("battleCoinIconSprite").objectReferenceValue = coinSprite;
            coinSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coinUi);

            Button pauseHudBtn = CreateTopRightPillButton(
                "PauseHudButton", canvasObj.transform, new Vector2(-228f, -44f), new Vector2(144f, 44f),
                "TẠM DỪNG", pauseBtnSp, new Color(0.20f, 0.26f, 0.38f, 0.95f), pauseIconSp);

            Button hudMenuBtn = CreateTopRightPillButton(
                "HudMenuButton", canvasObj.transform, new Vector2(-90f, -44f), new Vector2(116f, 44f),
                "THOÁT", exitBtnSp, new Color(0.58f, 0.20f, 0.24f, 0.95f), exitIconSp);

            // 4B. Top-Dock Training Mode Control Bar (x = 464..1432)
            trainingRefs = BuildTrainingTopBar(canvasObj.transform, keycapDarkSp, pillRedSp, trashIconSp);

            // 4C. Thanh Hướng Dẫn Phím dưới đáy + Nút [?] thu gọn góc phải dưới
            GameObject hintBar = CreateUIObject("ControlsHint", canvasObj.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(64f, 12f), new Vector2(1380f, 38f));
            Image hintBg = hintBar.AddComponent<Image>();
            ApplyImageSprite(hintBg, controlStripSp, sliced: true, new Color(0.05f, 0.08f, 0.14f, 0.78f));
            hintBg.raycastTarget = false;

            GameObject hintText = CreateText(
                "ControlsHintText",
                hintBar.transform,
                "<color=#FFD84D>[A/D]</color> Di chuyển   •   <color=#FFD84D>[2x A/D]</color> Lướt   •   <color=#FFD84D>[W/Space]</color> Nhảy   •   <color=#FFD84D>[S+Space]</color> Rơi thềm   •   <color=#FFD84D>[J]</color> Đánh   •   <color=#FFD84D>[S]</color> Khiên   •   <color=#FFD84D>[U/I]</color> Kỹ năng   •   <color=#FFD84D>[O]</color> Biến hình   •   <color=#FFD84D>[Q/E]</color> Vật phẩm",
                14.5f, TextAlignmentOptions.Center, Color.white);
            SetFullStretch(hintText.GetComponent<RectTransform>(), new Vector2(16f, 2f), new Vector2(-16f, -2f));
            TextMeshProUGUI hintTmp = hintText.GetComponent<TextMeshProUGUI>();
            hintTmp.richText = true;
            hintTmp.raycastTarget = false;

            Button hintToggleBtn = CreateGlassButton(
                "ControlsHintToggleButton", canvasObj.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-39f, 31f), new Vector2(38f, 38f), "?",
                keycapDarkSp, sliced: true, new Color(0.10f, 0.14f, 0.22f, 0.90f), 18f, new Color(1f, 0.847f, 0.302f, 1f));

            // 5. Cụm Victory & Defeat Floating Glass Modals + Pause Modal Panel
            GameObject victoryPanel = CreateModalOverlay("VictoryPanel", canvasObj.transform, new Color(0.02f, 0.04f, 0.09f, 0.56f));
            GameObject victoryCard = CreateModalCard("VictoryModalCard", victoryPanel.transform, new Vector2(640f, 470f), modalVictorySp, new Color(0.14f, 0.11f, 0.05f, 0.94f));
            GameObject victoryTitleObj = CreateText("TitleText", victoryCard.transform, "CHIẾN THẮNG", 52f, TextAlignmentOptions.Center, new Color(1f, 0.8862745f, 0.2901961f, 1f));
            RectTransform vTitleRt = victoryTitleObj.GetComponent<RectTransform>();
            vTitleRt.anchoredPosition = new Vector2(0f, 158f);
            vTitleRt.sizeDelta = new Vector2(560f, 76f);
            TextMeshProUGUI victoryTitle = victoryTitleObj.GetComponent<TextMeshProUGUI>();
            victoryTitle.raycastTarget = false;

            Button victoryNextBtn = CreateGlassButton("NextStageButton", victoryCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(280f, 58f), "MÀN TIẾP THEO", pillGreenSp, true, new Color(0.18f, 0.65f, 0.28f), 22f, Color.white);
            Button victoryRetryBtn = CreateGlassButton("VictoryRetryButton", victoryCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-134f, -104f), new Vector2(240f, 56f), "CHƠI LẠI", pillOrangeSp, true, new Color(0.75f, 0.42f, 0.12f), 22f, Color.white);
            Button victoryMenuBtn = CreateGlassButton("VictoryMenuButton", victoryCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(134f, -104f), new Vector2(240f, 56f), "VỀ MENU", pillNeutralSp, true, new Color(0.24f, 0.30f, 0.40f), 22f, Color.white);
            victoryPanel.SetActive(false);

            GameObject defeatPanel = CreateModalOverlay("DefeatPanel", canvasObj.transform, new Color(0.04f, 0.02f, 0.04f, 0.58f));
            GameObject defeatCard = CreateModalCard("DefeatModalCard", defeatPanel.transform, new Vector2(600f, 420f), modalDefeatSp, new Color(0.14f, 0.05f, 0.07f, 0.94f));
            GameObject defeatTitleObj = CreateText("TitleText", defeatCard.transform, "THẤT BẠI", 52f, TextAlignmentOptions.Center, new Color(1f, 0.28f, 0.38f, 1f));
            RectTransform dTitleRt = defeatTitleObj.GetComponent<RectTransform>();
            dTitleRt.anchoredPosition = new Vector2(0f, 134f);
            dTitleRt.sizeDelta = new Vector2(540f, 76f);
            TextMeshProUGUI defeatTitle = defeatTitleObj.GetComponent<TextMeshProUGUI>();
            defeatTitle.raycastTarget = false;

            Button defeatRetryBtn = CreateGlassButton("DefeatRetryButton", defeatCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-132f, -88f), new Vector2(238f, 62f), "CHƠI LẠI", pillOrangeSp, true, new Color(0.75f, 0.42f, 0.12f), 22f, Color.white);
            Button defeatMenuBtn = CreateGlassButton("DefeatMenuButton", defeatCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(132f, -88f), new Vector2(238f, 62f), "VỀ MENU", pillNeutralSp, true, new Color(0.24f, 0.30f, 0.40f), 22f, Color.white);
            defeatPanel.SetActive(false);

            // Pause Modal
            GameObject pauseModalPanel = CreateModalOverlay("PauseModalPanel", canvasObj.transform, new Color(0.02f, 0.04f, 0.08f, 0.56f));
            GameObject pauseBox = CreateModalCard("PauseBox", pauseModalPanel.transform, new Vector2(540f, 430f), modalDarkSp, new Color(0.07f, 0.10f, 0.16f, 0.94f));

            GameObject pauseTitleObj = CreateText("PauseTitle", pauseBox.transform, "TẠM DỪNG", 40f, TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.32f, 1f));
            RectTransform ptRt = pauseTitleObj.GetComponent<RectTransform>();
            ptRt.anchoredPosition = new Vector2(0f, 148f);
            ptRt.sizeDelta = new Vector2(460f, 60f);

            Button resumeBtn = CreateGlassButton("ResumeButton", pauseBox.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(360f, 66f), "TIẾP TỤC", pillOrangeSp, true, new Color(0.84f, 0.46f, 0.12f, 0.98f), 24f, Color.white);
            Button pauseRetryBtn = CreateGlassButton("PauseRetryButton", pauseBox.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(360f, 66f), "CHƠI LẠI", pillNeutralSp, true, new Color(0.22f, 0.34f, 0.48f, 0.98f), 24f, Color.white);
            Button pauseExitBtn = CreateGlassButton("PauseExitMenuButton", pauseBox.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(360f, 66f), "VỀ MENU", pillRedSp, true, new Color(0.58f, 0.20f, 0.24f, 0.98f), 24f, Color.white);
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

            so.FindProperty("waveText").objectReferenceValue = waveTmp;
            so.FindProperty("enemiesRemainingText").objectReferenceValue = enemiesTmp;
            so.FindProperty("victoryPanel").objectReferenceValue = victoryPanel;
            so.FindProperty("defeatPanel").objectReferenceValue = defeatPanel;

            so.FindProperty("hudMenuButton").objectReferenceValue = hudMenuBtn;
            so.FindProperty("victoryNextButton").objectReferenceValue = victoryNextBtn;
            so.FindProperty("victoryRetryButton").objectReferenceValue = victoryRetryBtn;
            so.FindProperty("victoryMenuButton").objectReferenceValue = victoryMenuBtn;
            so.FindProperty("defeatRetryButton").objectReferenceValue = defeatRetryBtn;
            so.FindProperty("defeatMenuButton").objectReferenceValue = defeatMenuBtn;
            so.FindProperty("vietnameseFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SegoeUIBoldVN.ttf");

            so.FindProperty("waveBannerSprite").objectReferenceValue = waveBannerSp;
            so.FindProperty("topRightClusterSprite").objectReferenceValue = topRightClusterSp;
            so.FindProperty("pauseButtonSprite").objectReferenceValue = pauseBtnSp;
            so.FindProperty("exitButtonSprite").objectReferenceValue = exitBtnSp;
            so.FindProperty("pauseIconSprite").objectReferenceValue = pauseIconSp;
            so.FindProperty("exitIconSprite").objectReferenceValue = exitIconSp;

            so.ApplyModifiedProperties();

            uiManager.ConfigureExpandedHud(
                shieldFillImg, shieldTxt, blockTxt, shieldContainer,
                victoryTitle, defeatTitle, endlessBanner, waveTmp,
                itemSlotsUI, coinUi, pauseUi
            );
            EditorUtility.SetDirty(uiManager);

            // Gán sẵn PlayerVitalsHudUI & GameOverModalUI lên GameCanvas
            PlayerVitalsHudUI vitalsHud = canvasObj.AddComponent<PlayerVitalsHudUI>();
            SerializedObject vitalsSo = new SerializedObject(vitalsHud);
            vitalsSo.FindProperty("playerHudRect").objectReferenceValue = playerHud.GetComponent<RectTransform>();
            vitalsSo.FindProperty("vitalsBackplate").objectReferenceValue = backplateImg;
            vitalsSo.FindProperty("playerHpSlider").objectReferenceValue = hpSlider;
            vitalsSo.FindProperty("hpFrameImage").objectReferenceValue = hpSliderObj.transform.Find("Background")?.GetComponent<Image>();
            vitalsSo.FindProperty("hpFillImage").objectReferenceValue = hpSliderObj.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
            vitalsSo.FindProperty("hpHeartIcon").objectReferenceValue = heartImg;
            vitalsSo.FindProperty("hpText").objectReferenceValue = hpTxtTmp;
            vitalsSo.FindProperty("shieldBarObject").objectReferenceValue = shieldContainer;
            vitalsSo.FindProperty("shieldFrameImage").objectReferenceValue = shieldBg;
            vitalsSo.FindProperty("shieldFillImage").objectReferenceValue = shieldFillImg;
            vitalsSo.FindProperty("shieldText").objectReferenceValue = shieldTxt;
            vitalsSo.FindProperty("rageContainerRect").objectReferenceValue = rageContainer.GetComponent<RectTransform>();
            SerializedProperty vRageSegs = vitalsSo.FindProperty("rageSegmentSliders");
            vRageSegs.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                vRageSegs.GetArrayElementAtIndex(i).objectReferenceValue = rageSliders[i];
            }
            vitalsSo.FindProperty("ragePointsText").objectReferenceValue = rageTextObj.GetComponent<TMP_Text>();
            vitalsSo.FindProperty("blockBadgeObject").objectReferenceValue = blockContainer;
            vitalsSo.FindProperty("blockBadgeBg").objectReferenceValue = blockBg;
            vitalsSo.FindProperty("blockFillImage").objectReferenceValue = blockFillImg;
            vitalsSo.FindProperty("blockShieldIcon").objectReferenceValue = sIconImg;
            vitalsSo.FindProperty("blockStatusText").objectReferenceValue = blockTxt;
            vitalsSo.FindProperty("transformPromptObject").objectReferenceValue = transformPrompt;
            vitalsSo.FindProperty("specialComboPromptObject").objectReferenceValue = comboPrompt;
            vitalsSo.FindProperty("bossHealthPanel").objectReferenceValue = bossPanel;
            vitalsSo.FindProperty("bossSlider").objectReferenceValue = bossSlider;
            vitalsSo.FindProperty("bossNameText").objectReferenceValue = bossNameObj.GetComponent<TMP_Text>();
            vitalsSo.FindProperty("bossStatusText").objectReferenceValue = bossStatusObj.GetComponent<TMP_Text>();

            vitalsSo.FindProperty("vitalsBackplateSprite").objectReferenceValue = vitalsBackplateSp;
            vitalsSo.FindProperty("hpHeartIconSprite").objectReferenceValue = heartIconSp;
            vitalsSo.FindProperty("hpFrameSprite").objectReferenceValue = hpFrameSp;
            vitalsSo.FindProperty("hpFillSprite").objectReferenceValue = hpFillSp;
            vitalsSo.FindProperty("rage3SegFrameSprite").objectReferenceValue = rage3SegFrameSp;
            vitalsSo.FindProperty("rageSegmentCyanGlowSprite").objectReferenceValue = rageSegCyanSp;
            vitalsSo.FindProperty("rageSegmentDarkSprite").objectReferenceValue = rageSegDarkSp;
            vitalsSo.FindProperty("shieldOrbIconSprite").objectReferenceValue = shieldIconSp;
            vitalsSo.FindProperty("shieldPillReadySprite").objectReferenceValue = shieldPillReadySp;
            vitalsSo.FindProperty("shieldPillBlankSprite").objectReferenceValue = shieldPillBlankSp;
            vitalsSo.FindProperty("shieldBarFillSprite").objectReferenceValue = shieldFillSp;
            vitalsSo.FindProperty("promptPillSprite").objectReferenceValue = statusCyanSp;
            vitalsSo.FindProperty("promptBoltIconSprite").objectReferenceValue = boltIconSp;
            vitalsSo.FindProperty("bossFrameSprite").objectReferenceValue = bossFrameSp;
            vitalsSo.FindProperty("bossFillSprite").objectReferenceValue = bossFillSp;
            vitalsSo.FindProperty("hpTrailSprite").objectReferenceValue = hpTrailSp;
            vitalsSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(vitalsHud);

            GameOverModalUI gameOverModal = canvasObj.AddComponent<GameOverModalUI>();
            EditorUtility.SetDirty(gameOverModal);

            return canvasObj;
        }

        private static void BuildHudCircleSlot(
            Transform parent, string name, Vector2 anchoredPos, string keyLabel,
            Sprite orbFrameSprite, Sprite miniPillSprite,
            out Image borderImg, out Image iconImg, out TMP_Text keyTmp, out TMP_Text countTmp)
        {
            Sprite circleSprite = SpriteHelper.GetOrCreateCircleSprite();
            GameObject slotObj = CreateUIObject(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(84f, 84f));
            borderImg = slotObj.AddComponent<Image>();
            if (orbFrameSprite != null)
            {
                borderImg.sprite = orbFrameSprite;
                borderImg.type = Image.Type.Simple;
                borderImg.preserveAspect = true;
                borderImg.color = Color.white;
            }
            else
            {
                borderImg.sprite = circleSprite;
                borderImg.color = new Color(0.95f, 0.78f, 0.25f, 0.96f);
            }
            borderImg.raycastTarget = false;

            GameObject innerObj = CreateUIObject("InnerBg", slotObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, -16f));
            Image innerImg = innerObj.AddComponent<Image>();
            innerImg.sprite = circleSprite;
            innerImg.color = new Color(0.05f, 0.08f, 0.14f, 0.30f);
            innerImg.enabled = orbFrameSprite == null;
            innerImg.raycastTarget = false;

            GameObject iconObj = CreateUIObject("ItemIcon", innerObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f));
            iconImg = iconObj.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            GameObject keyBadge = CreateUIObject("KeyBadge", slotObj.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(12f, -10f), new Vector2(30f, 26f));
            Image keyBg = keyBadge.AddComponent<Image>();
            ApplyImageSprite(keyBg, miniPillSprite ?? circleSprite, sliced: miniPillSprite != null, new Color(0.92f, 0.62f, 0.14f, 1f));
            keyBg.raycastTarget = false;

            GameObject keyTxtObj = CreateText("KeyText", keyBadge.transform, keyLabel, 14f, TextAlignmentOptions.Center, Color.white);
            SetFullStretch(keyTxtObj.GetComponent<RectTransform>());
            keyTmp = keyTxtObj.GetComponent<TextMeshProUGUI>();
            keyTmp.raycastTarget = false;

            GameObject countPill = CreateUIObject("CountPill", slotObj.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-10f, 8f), new Vector2(48f, 24f));
            Image countBg = countPill.AddComponent<Image>();
            ApplyImageSprite(countBg, miniPillSprite ?? circleSprite, sliced: miniPillSprite != null, new Color(0.06f, 0.08f, 0.14f, 0.95f));
            countBg.raycastTarget = false;

            GameObject countTxtObj = CreateText("CountText", countPill.transform, "x0", 13f, TextAlignmentOptions.Center, Color.white);
            SetFullStretch(countTxtObj.GetComponent<RectTransform>());
            countTmp = countTxtObj.GetComponent<TextMeshProUGUI>();
            countTmp.raycastTarget = false;
        }

        private static TrainingHudRefs BuildTrainingTopBar(Transform canvasTransform, Sprite keycapDarkSp, Sprite pillRedSp, Sprite trashIconSp)
        {
            Sprite dockSp = LoadHudSprite("09_TrainingSpawnerDock/Frame_Training_MainDock_Clean.png");
            Sprite toggleOffSp = LoadHudSprite("09_TrainingSpawnerDock/Toggle_Training_OptionPill_Blank.png");
            Sprite toggleOnSp = LoadHudSprite("09_TrainingSpawnerDock/Toggle_Training_OptionPill_BlueLED_Clean.png");
            Sprite blueLedOrbSp = LoadHudSprite("09_TrainingSpawnerDock/Icon_Training_BlueLEDOrb.png");
            Sprite plasmaDivSp = LoadHudSprite("09_TrainingSpawnerDock/Divider_Training_PlasmaTube_Violet.png");
            Sprite spawnCardSp = LoadHudSprite("09_TrainingSpawnerDock/Slot_Training_EnemySpawnCard_Clean.png");
            Sprite clearAllSp = LoadHudSprite("09_TrainingSpawnerDock/Btn_Training_ClearAll_RedRing_Clean.png");

            // Top-center giữa PlayerHUD (x=452) và TopRightClusterBg (x=1440): x = 464..1432
            GameObject panel = CreateUIObject("TrainingTopBar", canvasTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-12f, -8f), new Vector2(968f, 98f));
            Image panelBg = panel.AddComponent<Image>();
            ApplyImageSprite(panelBg, dockSp, sliced: true, new Color(0.06f, 0.09f, 0.15f, 0.92f));

            // Group 1: Player Controls (Container trong suốt để lộ nền Dock Glass)
            GameObject group1 = CreateUIObject("Group1_PlayerControls", panel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(164f, 78f));
            Button playerInvincibleBtn = CreateTogglePill("PlayerInvincibleButton", group1.transform, new Vector2(0f, 20f), "BẤT TỬ: TẮT", toggleOffSp, blueLedOrbSp, isOn: false, new Color(0.14f, 0.18f, 0.25f, 0.95f), new Color(0.80f, 0.87f, 0.96f, 1f));
            TMP_Text playerInvincibleTxt = playerInvincibleBtn.GetComponentInChildren<TMP_Text>();

            Button autoMaxRageBtn = CreateTogglePill("AutoMaxRageButton", group1.transform, new Vector2(0f, -20f), "MAX NỘ: BẬT", toggleOnSp, blueLedOrbSp, isOn: true, new Color(0.16f, 0.60f, 0.34f, 0.96f), Color.white);
            TMP_Text autoMaxRageTxt = autoMaxRageBtn.GetComponentInChildren<TMP_Text>();

            // Group 2: Enemy Controls
            GameObject group2 = CreateUIObject("Group2_EnemyControls", panel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(182f, 0f), new Vector2(164f, 78f));
            Button enemyAiBtn = CreateTogglePill("EnemyAiButton", group2.transform, new Vector2(0f, 20f), "AI QUÁI: BẬT", toggleOnSp, blueLedOrbSp, isOn: true, new Color(0.16f, 0.60f, 0.34f, 0.96f), Color.white);
            TMP_Text enemyAiTxt = enemyAiBtn.GetComponentInChildren<TMP_Text>();

            Button enemyInvincibleBtn = CreateTogglePill("EnemyInvincibleButton", group2.transform, new Vector2(0f, -20f), "QUÁI BẤT TỬ: TẮT", toggleOffSp, blueLedOrbSp, isOn: false, new Color(0.14f, 0.18f, 0.25f, 0.95f), new Color(0.80f, 0.87f, 0.96f, 1f));
            TMP_Text enemyInvincibleTxt = enemyInvincibleBtn.GetComponentInChildren<TMP_Text>();

            // Divider_Left
            GameObject divLeftObj = CreateUIObject("Divider_Left", panel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(349f, 0f), new Vector2(16f, 78f));
            Image divLeftImg = divLeftObj.AddComponent<Image>();
            ApplyImageSprite(divLeftImg, plasmaDivSp, sliced: true, new Color(0.55f, 0.30f, 0.92f, 0.75f));
            divLeftImg.raycastTarget = false;

            // Group 3: Carousel [< | Spawn Cards | >] + Divider_Right + [XÓA QUÁI]
            GameObject group3 = CreateUIObject("Group3_SpawnAndClear", panel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(368f, 0f), new Vector2(588f, 80f));

            CreateGlassButton(
                "ScrollLeftButton", group3.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(13f, 0f), new Vector2(26f, 70f), "<",
                keycapDarkSp, sliced: true, new Color(0.14f, 0.18f, 0.28f, 0.96f), 16f, new Color(0.75f, 0.96f, 1f, 1f));

            GameObject scrollObj = CreateUIObject("SpawnListScroll", group3.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(29f, 0f), new Vector2(432f, 78f));
            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.01f);
            scrollBg.raycastTarget = true;
            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.08f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 28f;

            CreateGlassButton(
                "ScrollRightButton", group3.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(478f, 0f), new Vector2(26f, 70f), ">",
                keycapDarkSp, sliced: true, new Color(0.14f, 0.18f, 0.28f, 0.96f), 16f, new Color(0.75f, 0.96f, 1f, 1f));

            GameObject divRightObj = CreateUIObject("Divider_Right", group3.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(493f, 0f), new Vector2(16f, 78f));
            Image divRightImg = divRightObj.AddComponent<Image>();
            ApplyImageSprite(divRightImg, plasmaDivSp, sliced: true, new Color(0.55f, 0.30f, 0.92f, 0.75f));
            divRightImg.raycastTarget = false;

            GameObject viewportObj = CreateUIObject("Viewport", scrollObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform viewportRt = viewportObj.GetComponent<RectTransform>();
            SetFullStretch(viewportRt, new Vector2(2f, 1f), new Vector2(-2f, -1f));
            Image viewportBg = viewportObj.AddComponent<Image>();
            viewportBg.color = new Color(0f, 0f, 0f, 0.01f);
            viewportBg.raycastTarget = true;
            viewportObj.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRt;

            const int unitCount = 13;
            GameObject contentObj = CreateUIObject("Content", viewportObj.transform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform contentRt = contentObj.GetComponent<RectTransform>();
            HorizontalLayoutGroup hlg = contentObj.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(4, 6, 2, 2);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            scrollRect.content = contentRt;

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
                GameObject btnObj = CreateUIObject($"SpawnUnit_{i}", contentObj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 74f));
                LayoutElement le = btnObj.AddComponent<LayoutElement>();
                le.minWidth = 64f;
                le.preferredWidth = 64f;
                le.minHeight = 74f;
                le.preferredHeight = 74f;
                le.flexibleWidth = 0f;

                Image btnBg = btnObj.AddComponent<Image>();
                ApplyImageSprite(btnBg, spawnCardSp, sliced: true, new Color(0.18f, 0.25f, 0.38f, 0.96f));
                Button btn = btnObj.AddComponent<Button>();
                btn.targetGraphic = btnBg;
                ScrollEventForwarder forwarder = btnObj.AddComponent<ScrollEventForwarder>();
                forwarder.Initialize(scrollRect);

                GameObject iconObj = CreateFullRect("Icon", btnObj.transform);
                SetFullStretch(iconObj.GetComponent<RectTransform>(), new Vector2(7f, 20f), new Vector2(-7f, -6f));
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(defaultPrefabPaths[i]);
                Sprite unitSprite = prefabAsset != null ? prefabAsset.GetComponentInChildren<SpriteRenderer>(true)?.sprite : null;
                iconImg.sprite = unitSprite;
                iconImg.enabled = unitSprite != null;

                GameObject lblObj = CreateText("Label", btnObj.transform, defaultShortTags[i], 9.5f, TextAlignmentOptions.Center, new Color(0.933f, 0.965f, 1f, 1f));
                RectTransform lblRt = lblObj.GetComponent<RectTransform>();
                lblRt.anchorMin = new Vector2(0f, 0f);
                lblRt.anchorMax = new Vector2(1f, 0f);
                lblRt.pivot = new Vector2(0.5f, 0f);
                lblRt.anchoredPosition = new Vector2(0f, 3f);
                lblRt.sizeDelta = new Vector2(-10f, 15f);
                TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
                lblTmp.textWrappingMode = TextWrappingModes.NoWrap;
                lblTmp.overflowMode = TextOverflowModes.Ellipsis;
                lblTmp.enableAutoSizing = true;
                lblTmp.fontSizeMin = 7.5f;
                lblTmp.fontSizeMax = 10f;
                lblTmp.raycastTarget = false;

                spawnBtns[i] = btn;
                spawnLbls[i] = lblTmp;
                spawnIcons[i] = iconImg;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            scrollRect.horizontalNormalizedPosition = 0f;

            Button clearBtn = CreateGlassButton(
                "ClearAllEnemiesButton", group3.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-39f, 0f), new Vector2(76f, 76f), "XÓA\nQUÁI",
                clearAllSp ?? pillRedSp, sliced: true, new Color(0.72f, 0.16f, 0.20f, 0.96f), 10f, Color.white);

            Transform clearLblTr = clearBtn.transform.Find("Label");
            if (clearLblTr != null && clearLblTr.TryGetComponent(out TextMeshProUGUI clearLblTmp))
            {
                clearLblTmp.enableAutoSizing = true;
                clearLblTmp.fontSizeMin = 8f;
                clearLblTmp.fontSizeMax = 10.5f;
            }

            if (trashIconSp != null)
            {
                GameObject trashObj = CreateUIObject("TrashIcon", clearBtn.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(18f, 18f));
                Image trashImg = trashObj.AddComponent<Image>();
                ApplyImageSprite(trashImg, trashIconSp, sliced: false, Color.white, preserveAspect: true);
                trashImg.raycastTarget = false;

                if (clearLblTr != null)
                {
                    SetFullStretch(clearLblTr.GetComponent<RectTransform>(), new Vector2(10f, 10f), new Vector2(-10f, -32f));
                }
            }

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

        private static Sprite LoadHudSprite(string fileName, Sprite fallback = null)
        {
            if (string.IsNullOrEmpty(fileName)) return fallback;
            Sprite sp = GlassHudTheme.LoadSprite(fileName);
            if (sp == null && fileName.IndexOf('/') >= 0)
            {
                sp = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/UI/SlicedUI/{fileName}");
            }
            if (sp == null)
            {
                sp = SpriteHelper.LoadSprite($"Assets/Sprites/UI/GlassHUD/{fileName}");
            }
            if (sp == null)
            {
                sp = SpriteHelper.LoadSprite($"Assets/Sprites/UI/Glassmorphism/{fileName}");
            }
            return sp != null ? sp : fallback;
        }

        private static float ResolveTargetDim(RectTransform rt, bool horizontal)
        {
            if (rt == null) return 0f;
            float rectVal = horizontal ? rt.rect.width : rt.rect.height;
            if (rectVal > 1f) return rectVal;

            bool stretched = horizontal
                ? !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x)
                : !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);
            float sd = horizontal ? rt.sizeDelta.x : rt.sizeDelta.y;
            if (!stretched) return Mathf.Abs(sd);

            if (rt.parent is RectTransform parentRt)
            {
                float parentDim = ResolveTargetDim(parentRt, horizontal);
                if (parentDim > 1f) return Mathf.Max(1f, parentDim + sd);
            }
            return 0f;
        }

        private static void ApplyImageSprite(Image img, Sprite sprite, bool sliced, Color fallbackColor, bool preserveAspect = false)
        {
            if (img == null) return;
            if (sprite != null)
            {
                if (sliced)
                {
                    RectTransform rt = img.rectTransform;
                    float targetW = ResolveTargetDim(rt, true);
                    float targetH = ResolveTargetDim(rt, false);
                    GlassHudTheme.ApplySlicedSprite(img, sprite, targetW, targetH, Color.white);
                }
                else
                {
                    img.sprite = sprite;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = preserveAspect;
                    img.color = Color.white;
                    img.pixelsPerUnitMultiplier = 1f;
                }
            }
            else
            {
                img.color = fallbackColor;
            }
        }

        private static void ApplyFilledSprite(Image img, Sprite sprite, Color fallbackColor)
        {
            if (img == null) return;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = sprite.name == "Square" ? fallbackColor : Color.white;
            }
            else
            {
                img.color = fallbackColor;
            }
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.preserveAspect = false;
        }

        private static void SetFullStretch(RectTransform rt, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void AddTextShadowAndOutline(GameObject go)
        {
            if (go == null) return;

            if (!go.TryGetComponent(out Outline outline))
            {
                outline = go.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.1f, -1.1f);
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
            exactShadow.effectDistance = new Vector2(1.5f, -2f);
            exactShadow.useGraphicAlpha = true;
        }

        private static GameObject CreateFullRect(string name, Transform parent)
        {
            return CreateUIObject(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
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

        private static GameObject CreateSlider(
            string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size,
            Sprite bgSprite, bool frameSliced, Color bgFallbackColor,
            Sprite fillSprite, Color fillFallbackColor,
            Vector2 fillAreaOffsetMin, Vector2 fillAreaOffsetMax)
        {
            GameObject sliderObj = CreateUIObject(name, parent, anchorMin, anchorMax, pivot, pos, size);
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            GameObject bgObj = CreateFullRect("Background", sliderObj.transform);
            Image bgImg = bgObj.AddComponent<Image>();
            ApplyImageSprite(bgImg, bgSprite, frameSliced, bgFallbackColor);
            bgImg.raycastTarget = false;

            GameObject fillArea = CreateFullRect("Fill Area", sliderObj.transform);
            SetFullStretch(fillArea.GetComponent<RectTransform>(), fillAreaOffsetMin, fillAreaOffsetMax);

            GameObject fillObj = CreateFullRect("Fill", fillArea.transform);
            Image fillImg = fillObj.AddComponent<Image>();
            ApplyImageSprite(fillImg, fillSprite, sliced: false, fillFallbackColor);
            fillImg.raycastTarget = false;

            slider.targetGraphic = bgImg;
            slider.fillRect = fillObj.GetComponent<RectTransform>();
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
            TMP_FontAsset vnTmp = SpriteHelper.GetOrCreateVietnameseTmpFontAsset();
            if (vnTmp != null)
            {
                tmp.font = vnTmp;
            }
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            AddTextShadowAndOutline(obj);
            return obj;
        }

        private static GameObject CreatePromptBadge(
            string name, Transform parent, Vector2 pos, string text,
            Color pillTint, Color textColor, Sprite pillSprite, Sprite boltIconSp)
        {
            GameObject obj = CreateUIObject(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), pos, new Vector2(192f, 34f));
            Image img = obj.AddComponent<Image>();
            ApplyImageSprite(img, pillSprite, sliced: true, new Color(0.08f, 0.25f, 0.34f, 0.92f));
            if (pillSprite != null) img.color = pillTint;
            img.raycastTarget = false;

            if (boltIconSp != null)
            {
                GameObject boltObj = CreateUIObject("BoltIcon", obj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(18f, 18f));
                Image boltImg = boltObj.AddComponent<Image>();
                ApplyImageSprite(boltImg, boltIconSp, sliced: false, new Color(0.35f, 0.95f, 1f, 1f), preserveAspect: true);
                boltImg.raycastTarget = false;
            }

            GameObject txtObj = CreateText("PromptText", obj.transform, text, 13f, TextAlignmentOptions.Center, textColor);
            SetFullStretch(txtObj.GetComponent<RectTransform>(), new Vector2(30f, 0f), new Vector2(-8f, 0f));
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 9.5f;
            tmp.fontSizeMax = 13f;
            tmp.raycastTarget = false;

            return obj;
        }

        private static Button CreateTogglePill(
            string name, Transform parent, Vector2 pos, string label,
            Sprite pillSprite, Sprite blueLedOrbSp, bool isOn, Color fallbackBg, Color textColor)
        {
            GameObject btnObj = CreateUIObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(164f, 36f));
            Image bgImg = btnObj.AddComponent<Image>();
            ApplyImageSprite(bgImg, pillSprite, sliced: true, fallbackBg);
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bgImg;

            GameObject orbObj = CreateUIObject("LedOrb", btnObj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(20f, 20f));
            Image orbImg = orbObj.AddComponent<Image>();
            ApplyImageSprite(orbImg, blueLedOrbSp, sliced: false, new Color(0.45f, 0.52f, 0.62f, 0.45f), preserveAspect: true);
            if (blueLedOrbSp != null) orbImg.color = new Color(0.45f, 0.52f, 0.62f, 0.45f);
            orbImg.raycastTarget = false;
            orbImg.enabled = !isOn && blueLedOrbSp != null;

            GameObject txtObj = CreateText("Label", btnObj.transform, label, 11.5f, TextAlignmentOptions.MidlineLeft, textColor);
            SetFullStretch(txtObj.GetComponent<RectTransform>(), new Vector2(26f, 0f), new Vector2(-6f, 0f));
            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 9.5f;
            tmp.fontSizeMax = 11.5f;
            tmp.raycastTarget = false;

            return btn;
        }

        private static Button CreateTopRightPillButton(
            string name, Transform parent, Vector2 centeredPos, Vector2 size,
            string label, Sprite bgSprite, Color fallbackBg, Sprite iconSp)
        {
            Button btn = CreateGlassButton(
                name, parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                centeredPos, size, label, bgSprite, sliced: true, fallbackBg, 15f, Color.white);

            if (iconSp != null)
            {
                GameObject iconObj = CreateUIObject("Icon", btn.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(16f, 16f));
                Image iconImg = iconObj.AddComponent<Image>();
                ApplyImageSprite(iconImg, iconSp, sliced: false, Color.white, preserveAspect: true);
                iconImg.raycastTarget = false;

                Transform lblTr = btn.transform.Find("Label");
                if (lblTr != null)
                {
                    SetFullStretch(lblTr.GetComponent<RectTransform>(), new Vector2(26f, 0f), new Vector2(-8f, 0f));
                    if (lblTr.TryGetComponent(out TextMeshProUGUI lblTmp))
                    {
                        lblTmp.enableAutoSizing = true;
                        lblTmp.fontSizeMin = 11f;
                        lblTmp.fontSizeMax = 15.5f;
                    }
                }
            }

            return btn;
        }

        private static Button CreateGlassButton(
            string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size, string label, Sprite bgSprite, bool sliced,
            Color fallbackColor, float fontSize, Color textColor)
        {
            GameObject btnObj = CreateUIObject(name, parent, anchorMin, anchorMax, pivot, pos, size);
            Image img = btnObj.AddComponent<Image>();
            ApplyImageSprite(img, bgSprite, sliced, fallbackColor);
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject txtObj = CreateText("Label", btnObj.transform, label, fontSize, TextAlignmentOptions.Center, textColor);
            SetFullStretch(txtObj.GetComponent<RectTransform>());
            txtObj.GetComponent<TextMeshProUGUI>().raycastTarget = false;

            return btn;
        }

        public static Button CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, string label, Color bgColor, float fontSize = 20)
        {
            GameObject btnObj = CreateUIObject(name, parent, anchorMin, anchorMax, pivot, pos, size);
            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject txtObj = CreateText("Label", btnObj.transform, label, fontSize, TextAlignmentOptions.Center, Color.white);
            SetFullStretch(txtObj.GetComponent<RectTransform>());

            return btn;
        }

        private static GameObject CreateModalOverlay(string name, Transform parent, Color overlayColor)
        {
            GameObject panel = CreateFullRect(name, parent);
            Image img = panel.AddComponent<Image>();
            img.color = overlayColor;
            img.raycastTarget = true;
            return panel;
        }

        private static GameObject CreateModalCard(string name, Transform parent, Vector2 size, Sprite cardSprite, Color fallbackColor)
        {
            GameObject cardObj = CreateUIObject(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Image bg = cardObj.AddComponent<Image>();
            ApplyImageSprite(bg, cardSprite, sliced: true, fallbackColor);
            bg.raycastTarget = true;
            CanvasGroup cg = cardObj.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            return cardObj;
        }
    }
}
