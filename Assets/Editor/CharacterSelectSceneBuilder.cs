using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.UI;

namespace StickmanBeatEmUp.Editor
{
    /// <summary>
    /// Tự động dựng Scene Chọn Nhân Vật & Chọn Màn Chơi (CharacterSelectScene.unity).
    /// </summary>
    public static class CharacterSelectSceneBuilder
    {
        public static string BuildCharacterSelectScene()
        {
            SpriteHelper.ConfigureAllProjectSprites();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera nền
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 0f, -10f);
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.14f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camObj.AddComponent<AudioListener>();

            CanvasUIBuilder.EnsureEventSystem();

            // Canvas
            GameObject canvasObj = new GameObject("CharacterSelectCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            CharacterSelectManager manager = canvasObj.AddComponent<CharacterSelectManager>();

            // Global Background + Light Dark Overlay (Alpha = 0.20f)
            GameObject bgRoot = CreateFullRect("MenuBackground", canvasObj.transform);
            Image bgImg = bgRoot.AddComponent<Image>();
            Sprite globalBgSprite = SpriteHelper.LoadSprite("Assets/Sprites/UI/MenuGlobalBackground.png")
                ?? SpriteHelper.LoadSprite("Assets/Sprites/UI/MainMenu_ThreeHeroes_Thumb.png");
            GameObject overlayObj = CreateFullRect("DarkOverlay", bgRoot.transform);
            Image overlayImg = overlayObj.AddComponent<Image>();
            MenuBackgroundLoader bgLoader = bgRoot.AddComponent<MenuBackgroundLoader>();
            bgLoader.ConfigureTargets(bgImg, overlayImg, globalBgSprite);
            bgLoader.SetOverlayAlpha(0.20f);

            // SlicedUI (03_CharacterAndItemSelect) & Stage WorldCard Sprites
            Sprite charSlotGlass = LoadSlicedUISprite("03_CharacterAndItemSelect/Slot_CharSelect_Unselected_Clean.png")
                ?? LoadGlassUISprite("CharSlot_Glass.png", new Vector4(28f, 28f, 28f, 28f));
            Sprite charSlotGoldSelected = LoadSlicedUISprite("03_CharacterAndItemSelect/Slot_CharSelect_SelectedGold_Clean.png")
                ?? LoadGlassUISprite("CharSlot_GoldSelected.png", new Vector4(28f, 28f, 28f, 28f));
            Sprite continueBtnSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Btn_CharSelect_Continue_Clean.png")
                ?? LoadGlassUISprite("TechCut_ActionButton.png", new Vector4(36f, 28f, 36f, 28f));
            Sprite mainPanelFrameSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Frame_CharSelect_MainPanel_Clean.png")
                ?? LoadGlassUISprite("ModalCard_DarkGlass_Cyan.png", Vector4.zero);
            Sprite backBtnSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Btn_CharSelect_Back_Clean.png", continueBtnSprite);
            Sprite resetFormIconSprite = LoadSlicedUISprite("03_CharacterAndItemSelect/Btn_CharSelect_ResetIcon.png", continueBtnSprite);

            Sprite modalVictoryGold = LoadGlassUISprite("ModalCard_Victory_Gold.png", Vector4.zero, charSlotGoldSelected);
            Sprite[] worldCardSprites =
            {
                LoadGlassUISprite("WorldCard_AoT_EmeraldAmber.png", Vector4.zero, charSlotGlass),
                LoadGlassUISprite("WorldCard_DB_AmberGold.png", Vector4.zero, charSlotGlass),
                LoadGlassUISprite("WorldCard_Diend_MagentaCyan.png", Vector4.zero, charSlotGlass)
            };

            // Title Header
            CreateText("TitleText", canvasObj.transform, new Vector2(0f, 470f), new Vector2(1400f, 70f),
                "STICKMAN BEAT 'EM UP  -  CHỌN NHÂN VẬT & MÀN CHƠI", 40f, Color.yellow);

            // Load Portraits
            Sprite[] normalSprites = new[]
            {
                SpriteHelper.LoadSprite("Assets/Sprites/UI/Goku_Normal_Select.png"),
                SpriteHelper.LoadSprite("Assets/Sprites/UI/Eren_Normal_Select.png"),
                SpriteHelper.LoadSprite("Assets/Sprites/UI/Diend_Normal_Select.png")
            };
            Sprite[] transSprites = new[]
            {
                SpriteHelper.LoadSprite("Assets/Sprites/UI/Goku_SSJ_Select.png"),
                SpriteHelper.LoadSprite("Assets/Sprites/UI/Eren_Titan_Select.png"),
                SpriteHelper.LoadSprite("Assets/Sprites/UI/Diend_Kiva_Select.png")
            };

            string[] charLabels = { "GOKU", "EREN", "DIEND" };
            Button[] charButtons = new Button[3];
            Image[] charPortraits = new Image[3];

            for (int i = 0; i < 3; i++)
            {
                float x = -560f + i * 320f;
                float y = 175f;
                GameObject card = CreateUIObject($"CharacterCard_{charLabels[i]}", canvasObj.transform, new Vector2(x, y), new Vector2(290f, 360f));
                card.transform.localScale = (i == 0) ? Vector3.one * 1.08f : Vector3.one;
                Image cardBg = card.AddComponent<Image>();
                if (charSlotGlass != null || charSlotGoldSelected != null)
                {
                    Sprite slotSp = (i == 0)
                        ? (charSlotGoldSelected ?? charSlotGlass)
                        : (charSlotGlass ?? charSlotGoldSelected);
                    GlassHudTheme.ApplySlicedSprite(cardBg, slotSp, 290f, 360f, Color.white);
                }
                else
                {
                    cardBg.color = new Color(0.22f, 0.24f, 0.30f, 1f);
                }
                Button cardBtn = card.AddComponent<Button>();
                cardBtn.targetGraphic = cardBg;
                charButtons[i] = cardBtn;

                GameObject portraitObj = CreateUIObject("Portrait", card.transform, new Vector2(0f, 24f), new Vector2(210f, 210f));
                portraitObj.transform.localScale = Vector3.one;
                Image portraitImg = portraitObj.AddComponent<Image>();
                portraitImg.preserveAspect = true;
                portraitImg.sprite = normalSprites[i];
                portraitImg.color = Color.white;
                charPortraits[i] = portraitImg;

                CreateText("NameLabel", card.transform, new Vector2(0f, -124f), new Vector2(230f, 42f), charLabels[i], 26f, Color.white);
                UIManager.EnsureGoldSelectionFrame(card.transform, null, isSelected: false, applyRuntimeSprite: false);
            }

            // Toggle Normal / Transformed Preview (Btn_CharSelect_ResetIcon)
            Button toggleFormBtn = CanvasUIBuilder.CreateButton(
                "ToggleFormPreviewButton", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-240f, -85f), new Vector2(88f, 80f),
                string.Empty, new Color(0.32f, 0.24f, 0.56f), 20f);
            if (toggleFormBtn.TryGetComponent(out Image toggleImg) && resetFormIconSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(toggleImg, resetFormIconSprite, 88f, 80f, Color.white);
            }
            TextMeshProUGUI toggleFormTxt = toggleFormBtn.GetComponentInChildren<TextMeshProUGUI>();
            UIManager.EnsureSwapFormButtonVisual(toggleFormBtn, toggleFormTxt, applyRuntimeSprite: false, Color.white);

            // Character Info Box (Frame_CharSelect_MainPanel_Clean)
            GameObject infoBox = CreateUIObject("CharacterInfoBox", canvasObj.transform, new Vector2(-240f, -250f), new Vector2(880f, 220f));
            Image infoBg = infoBox.AddComponent<Image>();
            if (mainPanelFrameSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(infoBg, mainPanelFrameSprite, 880f, 220f, Color.white);
            }
            else
            {
                infoBg.color = new Color(0.06f, 0.09f, 0.16f, 0.48f);
                Outline infoOutline = infoBox.AddComponent<Outline>();
                infoOutline.effectColor = new Color(0.45f, 0.72f, 1f, 0.30f);
                infoOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            TextMeshProUGUI selCharTxt = CreateText("SelectedCharacterText", infoBox.transform, new Vector2(0f, 70f), new Vector2(820f, 45f),
                "NHÂN VẬT: GOKU", 28f, new Color(1f, 0.85f, 0.2f));
            TextMeshProUGUI descTxt = CreateText("CharacterDescriptionText", infoBox.transform, new Vector2(0f, -20f), new Vector2(820f, 125f),
                "", 20f, Color.white);

            // Stage Selection Panel (Right Side: 3 Worlds x 4 Stages, Frame_CharSelect_MainPanel_Clean)
            GameObject stagePanel = CreateUIObject("StageSelectionPanel", canvasObj.transform, new Vector2(560f, 95f), new Vector2(620f, 560f));
            Image stageBg = stagePanel.AddComponent<Image>();
            if (mainPanelFrameSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(stageBg, mainPanelFrameSprite, 620f, 560f, Color.white);
            }
            else
            {
                stageBg.color = new Color(0.06f, 0.09f, 0.16f, 0.48f);
                Outline stageOutline = stagePanel.AddComponent<Outline>();
                stageOutline.effectColor = new Color(0.45f, 0.72f, 1f, 0.30f);
                stageOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            CreateText("StageHeader", stagePanel.transform, new Vector2(0f, 235f), new Vector2(580f, 46f),
                "CHỌN THẾ GIỚI & MÀN CHƠI (12 MÀN)", 26f, Color.cyan);

            string[] worldPrefixes = { "W1 (AoT)", "W2 (DBZ)", "W3 (Diend)" };
            Button[] stageButtons = new Button[12];
            for (int w = 0; w < 3; w++)
            {
                float rowY = 140f - w * 135f;
                CreateText($"WorldLabel_{w + 1}", stagePanel.transform, new Vector2(0f, rowY + 42f), new Vector2(560f, 32f),
                    worldPrefixes[w], 20f, new Color(0.85f, 0.85f, 0.95f));

                for (int s = 0; s < 4; s++)
                {
                    int idx = w * 4 + s;
                    float colX = -210f + s * 140f;
                    string roleTag = s == 0 ? "(Creep)" : (s == 1 ? "(Range)" : (s == 2 ? "(ELITE)" : "(BOSS)"));
                    string label = $"M{s + 1}\n{roleTag}";
                    stageButtons[idx] = CanvasUIBuilder.CreateButton(
                        $"StageButton_W{w + 1}_S{s + 1}", stagePanel.transform,
                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(colX, rowY - 12f), new Vector2(124f, 64f),
                        label, new Color(0.26f, 0.34f, 0.48f), 18f);

                    Sprite cardSp = s == 3
                        ? (modalVictoryGold ?? worldCardSprites[w] ?? charSlotGlass)
                        : (worldCardSprites[w] ?? charSlotGlass);
                    ApplyTechCutStyle(stageButtons[idx], cardSp);

                    if (s == 3)
                    {
                        TMP_Text bossLbl = stageButtons[idx].GetComponentInChildren<TMP_Text>(true);
                        if (bossLbl != null) bossLbl.color = new Color(1f, 0.90f, 0.35f, 1f);
                    }
                    UIManager.EnsureGoldSelectionFrame(stageButtons[idx].transform, null, isSelected: idx == 0, applyRuntimeSprite: false);
                }
            }

            TextMeshProUGUI selStageTxt = CreateText("SelectedStageText", canvasObj.transform, new Vector2(560f, -215f), new Vector2(620f, 44f),
                "MÀN CHƠI: W1 - Màn 1", 22f, Color.green);

            Button unlockAllBtn = CanvasUIBuilder.CreateButton(
                "UnlockAllButton", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(560f, -275f), new Vector2(300f, 58f),
                "Mở Khóa Toàn Bộ 12 Màn", new Color(0.45f, 0.32f, 0.15f), 18f);
            if (unlockAllBtn.TryGetComponent(out Image unlockImg) && backBtnSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(unlockImg, backBtnSprite, 300f, 58f, Color.white);
            }

            Button playBtn = CanvasUIBuilder.CreateButton(
                "PlayButton", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(560f, -358f), new Vector2(400f, 90f),
                "BẮT ĐẦU CHIẾN ĐẤU (PLAY)", new Color(0.15f, 0.7f, 0.25f), 28f);
            if (playBtn.TryGetComponent(out Image playImg) && continueBtnSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(playImg, continueBtnSprite, 400f, 90f, Color.white);
            }

            // Wire SerializedObject fields on CharacterSelectManager
            SerializedObject so = new SerializedObject(manager);
            BindObjectArray(so.FindProperty("characterButtons"), charButtons);
            BindObjectArray(so.FindProperty("characterPortraits"), charPortraits);
            BindObjectArray(so.FindProperty("normalPortraits"), normalSprites);
            BindObjectArray(so.FindProperty("transformedPortraits"), transSprites);
            so.FindProperty("selectedCharacterText").objectReferenceValue = selCharTxt;
            so.FindProperty("characterDescriptionText").objectReferenceValue = descTxt;
            so.FindProperty("toggleFormButton").objectReferenceValue = toggleFormBtn;
            so.FindProperty("toggleFormButtonText").objectReferenceValue = toggleFormTxt;
            BindObjectArray(so.FindProperty("stageButtons"), stageButtons);
            so.FindProperty("selectedStageText").objectReferenceValue = selStageTxt;
            so.FindProperty("playButton").objectReferenceValue = playBtn;
            so.FindProperty("unlockAllButton").objectReferenceValue = unlockAllBtn;
            so.FindProperty("battleSceneName").stringValue = "Stickman_DemoScene";
            so.FindProperty("charSlotGlassSprite").objectReferenceValue = charSlotGlass;
            so.FindProperty("charSlotGoldSelectedSprite").objectReferenceValue = charSlotGoldSelected;
            so.FindProperty("techCutActionButtonSprite").objectReferenceValue = continueBtnSprite;
            so.FindProperty("mainPanelFrameSprite").objectReferenceValue = mainPanelFrameSprite;
            so.FindProperty("backButtonSprite").objectReferenceValue = backBtnSprite;
            so.FindProperty("resetFormIconSprite").objectReferenceValue = resetFormIconSprite;
            so.ApplyModifiedProperties();

            SpriteHelper.EnsureFolder("Assets/Scenes");
            const string scenePath = "Assets/Scenes/CharacterSelectScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            return scenePath;
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

        private static void ApplyTechCutStyle(Button btn, Sprite techCutSprite)
        {
            if (btn == null || techCutSprite == null) return;
            if (btn.TryGetComponent(out Image img))
            {
                UIManager.ApplyGlassImageStyle(img, techCutSprite, sliced: true, preserveAspect: false);
            }
        }

        private static GameObject CreateUIObject(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return obj;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 pos, Vector2 size, string text, float fontSize, Color color)
        {
            GameObject obj = CreateUIObject(name, parent, pos, size);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset vnTmp = SpriteHelper.GetOrCreateVietnameseTmpFontAsset();
            if (vnTmp != null)
            {
                tmp.font = vnTmp;
            }
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            Outline outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.86f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            outline.useGraphicAlpha = true;

            Shadow shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
            shadow.effectDistance = new Vector2(1.8f, -2.2f);
            shadow.useGraphicAlpha = true;

            return tmp;
        }

        private static void BindObjectArray<T>(SerializedProperty prop, T[] items) where T : Object
        {
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }
    }
}
