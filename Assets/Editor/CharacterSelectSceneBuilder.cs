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

            // Title Header
            CreateText("TitleText", canvasObj.transform, new Vector2(0f, 470f), new Vector2(1400f, 70f),
                "STICKMAN BEAT 'EM UP  -  CHON NHAN VAT & MAN CHOI", 40f, Color.yellow);

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
                float x = -520f + i * 330f;
                float y = 175f;
                GameObject card = CreateUIObject($"CharacterCard_{charLabels[i]}", canvasObj.transform, new Vector2(x, y), new Vector2(290f, 430f));
                Image cardBg = card.AddComponent<Image>();
                cardBg.color = new Color(0.22f, 0.24f, 0.30f, 1f);
                Button cardBtn = card.AddComponent<Button>();
                cardBtn.targetGraphic = cardBg;
                charButtons[i] = cardBtn;

                GameObject portraitObj = CreateUIObject("Portrait", card.transform, new Vector2(0f, 25f), new Vector2(250f, 320f));
                Image portraitImg = portraitObj.AddComponent<Image>();
                portraitImg.preserveAspect = true;
                portraitImg.sprite = normalSprites[i];
                charPortraits[i] = portraitImg;

                CreateText("NameLabel", card.transform, new Vector2(0f, -175f), new Vector2(260f, 44f), charLabels[i], 28f, Color.white);
            }

            // Toggle Normal / Transformed Preview
            Button toggleFormBtn = CanvasUIBuilder.CreateButton(
                "ToggleFormPreviewButton", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-190f, -85f), new Vector2(300f, 52f),
                "Xem Dang Bien Hinh", new Color(0.32f, 0.24f, 0.56f), 20f);
            TextMeshProUGUI toggleFormTxt = toggleFormBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Character Info Box
            GameObject infoBox = CreateUIObject("CharacterInfoBox", canvasObj.transform, new Vector2(-190f, -250f), new Vector2(960f, 220f));
            Image infoBg = infoBox.AddComponent<Image>();
            infoBg.color = new Color(0.12f, 0.14f, 0.20f, 0.9f);

            TextMeshProUGUI selCharTxt = CreateText("SelectedCharacterText", infoBox.transform, new Vector2(0f, 70f), new Vector2(900f, 45f),
                "NHAN VAT: GOKU STICKMAN", 28f, new Color(1f, 0.85f, 0.2f));
            TextMeshProUGUI descTxt = CreateText("CharacterDescriptionText", infoBox.transform, new Vector2(0f, -20f), new Vector2(900f, 125f),
                "", 20f, Color.white);

            // Stage Selection Panel (Right Side: 3 Worlds x 4 Stages)
            GameObject stagePanel = CreateUIObject("StageSelectionPanel", canvasObj.transform, new Vector2(560f, 95f), new Vector2(620f, 560f));
            Image stageBg = stagePanel.AddComponent<Image>();
            stageBg.color = new Color(0.12f, 0.14f, 0.20f, 0.9f);

            CreateText("StageHeader", stagePanel.transform, new Vector2(0f, 235f), new Vector2(580f, 46f),
                "CHON THE GIOI & MAN CHOI (12 MAN)", 26f, Color.cyan);

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
                }
            }

            TextMeshProUGUI selStageTxt = CreateText("SelectedStageText", canvasObj.transform, new Vector2(560f, -215f), new Vector2(620f, 44f),
                "MAN CHOI: W1 - Man 1", 22f, Color.green);

            Button unlockAllBtn = CanvasUIBuilder.CreateButton(
                "UnlockAllButton", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(560f, -275f), new Vector2(280f, 44f),
                "Mo Khoa Toan Bo 12 Man", new Color(0.45f, 0.32f, 0.15f), 18f);

            Button playBtn = CanvasUIBuilder.CreateButton(
                "PlayButton", canvasObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(560f, -355f), new Vector2(460f, 84f),
                "BAT DAU CHIEN DAU (PLAY)", new Color(0.15f, 0.7f, 0.25f), 30f);

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
            so.ApplyModifiedProperties();

            SpriteHelper.EnsureFolder("Assets/Scenes");
            const string scenePath = "Assets/Scenes/CharacterSelectScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            return scenePath;
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
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.Normal;
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
