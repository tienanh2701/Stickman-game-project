using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Auth;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Quản lý màn hình Chọn Nhân Vật (Goku, Eren, Diend) & Chọn Màn (3 Thế giới x 4 Màn = 12 Màn).
    /// </summary>
    public class CharacterSelectManager : MonoBehaviour
    {
        private const string SELECTED_CHAR_KEY = "SelectedCharacter";
        private const string SELECTED_STAGE_KEY = "SelectedStageIndex";
        private const string UNLOCKED_STAGE_KEY = "Stickman_UnlockedStage";

        [Header("Character Cards (0=Goku, 1=Eren, 2=Diend)")]
        [SerializeField] private Button[] characterButtons;
        [SerializeField] private Image[] characterPortraits;
        [SerializeField] private Sprite[] normalPortraits;
        [SerializeField] private Sprite[] transformedPortraits;
        [SerializeField] private TextMeshProUGUI selectedCharacterText;
        [SerializeField] private TextMeshProUGUI characterDescriptionText;
        [SerializeField] private Button toggleFormButton;
        [SerializeField] private TextMeshProUGUI toggleFormButtonText;

        [Header("Stage Selection (12 Stages = 3 Worlds x 4 Stages)")]
        [SerializeField] private Button[] stageButtons;
        [SerializeField] private TextMeshProUGUI selectedStageText;

        [Header("Play & Debug")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button unlockAllButton;

        [Header("Target Scene")]
        [SerializeField] private string battleSceneName = "Stickman_DemoScene";

        [Header("Glassmorphism Sprites (Optional)")]
        [SerializeField] private Sprite charSlotGlassSprite;
        [SerializeField] private Sprite charSlotGoldSelectedSprite;
        [SerializeField] private Sprite techCutActionButtonSprite;

        [Header("SlicedUI Sprites (03_CharacterAndItemSelect)")]
        [SerializeField] private Sprite mainPanelFrameSprite;
        [SerializeField] private Sprite backButtonSprite;
        [SerializeField] private Sprite resetFormIconSprite;

        private int selectedCharacter = 0;
        private int selectedStageIndex = 0;
        private bool showTransformedPortrait = false;

        private static readonly string[] CharacterNames =
        {
            "GOKU",
            "EREN",
            "DIEND"
        };

        private static readonly string[] WorldNames =
        {
            "W1: Attack on Titan",
            "W2: Dragon Ball",
            "W3: Kamen Rider Diend"
        };

        private static readonly string[] StageRoles =
        {
            "Stage 1 [Melee]",
            "Stage 2 [Ranged]",
            "Stage 3 [Elite]",
            "Stage 4 [BOSS]"
        };

        private void Start()
        {
            selectedCharacter = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_CHAR_KEY, 0), 0, 2);
            showTransformedPortrait = false;
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, selectedCharacter);
            int unlockedStage = Mathf.Max(1, PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 12));
            selectedStageIndex = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_STAGE_KEY, 0), 0, Mathf.Min(11, unlockedStage - 1));

            ApplyGlassmorphismStyling();
            WireButtons();
            RefreshUI();
        }

        public void ConfigureGlassSprites(Sprite glassSlot, Sprite goldSlot, Sprite techCutButton)
        {
            if (glassSlot != null) charSlotGlassSprite = glassSlot;
            if (goldSlot != null) charSlotGoldSelectedSprite = goldSlot;
            if (techCutButton != null) techCutActionButtonSprite = techCutButton;
            ApplyGlassmorphismStyling();
            RefreshUI();
        }

        public void ConfigureSlicedUISprites(
            Sprite glassSlot,
            Sprite goldSlot,
            Sprite continueButton,
            Sprite mainPanelFrame,
            Sprite backButton,
            Sprite resetFormIcon)
        {
            if (glassSlot != null) charSlotGlassSprite = glassSlot;
            if (goldSlot != null) charSlotGoldSelectedSprite = goldSlot;
            if (continueButton != null) techCutActionButtonSprite = continueButton;
            if (mainPanelFrame != null) mainPanelFrameSprite = mainPanelFrame;
            if (backButton != null) backButtonSprite = backButton;
            if (resetFormIcon != null) resetFormIconSprite = resetFormIcon;
            ApplyGlassmorphismStyling();
            RefreshUI();
        }

        private void WireButtons()
        {
            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    int index = i;
                    if (characterButtons[i] != null)
                    {
                        characterButtons[i].onClick.RemoveAllListeners();
                        characterButtons[i].onClick.AddListener(() => SelectCharacter(index));
                    }
                }
            }

            if (stageButtons != null)
            {
                for (int i = 0; i < stageButtons.Length; i++)
                {
                    int stageIdx = i;
                    if (stageButtons[i] != null)
                    {
                        stageButtons[i].onClick.RemoveAllListeners();
                        stageButtons[i].onClick.AddListener(() => SelectStage(stageIdx));
                    }
                }
            }

            if (toggleFormButton != null)
            {
                toggleFormButton.onClick.RemoveAllListeners();
                toggleFormButton.onClick.AddListener(TogglePortraitForm);
            }

            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(PlaySelectedGame);
            }

            if (unlockAllButton != null)
            {
                unlockAllButton.onClick.RemoveAllListeners();
                unlockAllButton.onClick.AddListener(UnlockAllStages);
            }
        }

        public void SelectCharacter(int index)
        {
            selectedCharacter = Mathf.Clamp(index, 0, 2);
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, selectedCharacter);
            PlayerPrefs.Save();
            if (AccountManager.HasInstance && AccountManager.Instance.HasActiveSession)
            {
                AccountManager.Instance.SyncActiveProfileFromPrefs(saveToDisk: true);
            }
            RefreshUI();
        }

        public void SelectStage(int stageIndex)
        {
            int unlockedStage = Mathf.Max(1, PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 12));
            if (stageIndex + 1 > unlockedStage) return;

            selectedStageIndex = Mathf.Clamp(stageIndex, 0, 11);
            PlayerPrefs.SetInt(SELECTED_STAGE_KEY, selectedStageIndex);
            PlayerPrefs.Save();
            if (AccountManager.HasInstance && AccountManager.Instance.HasActiveSession)
            {
                AccountManager.Instance.SyncActiveProfileFromPrefs(saveToDisk: true);
            }
            RefreshUI();
        }

        public void TogglePortraitForm()
        {
            showTransformedPortrait = !showTransformedPortrait;
            RefreshUI();
        }

        public void UnlockAllStages()
        {
            PlayerPrefs.SetInt(UNLOCKED_STAGE_KEY, 12);
            PlayerPrefs.Save();
            if (AccountManager.HasInstance && AccountManager.Instance.HasActiveSession)
            {
                AccountManager.Instance.SyncActiveProfileFromPrefs(saveToDisk: true);
            }
            RefreshUI();
        }

        public void PlaySelectedGame()
        {
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, selectedCharacter);
            PlayerPrefs.SetInt(SELECTED_STAGE_KEY, selectedStageIndex);
            PlayerPrefs.Save();
            if (AccountManager.HasInstance && AccountManager.Instance.HasActiveSession)
            {
                AccountManager.Instance.SyncActiveProfileFromPrefs(saveToDisk: true);
            }

            if (!string.IsNullOrEmpty(battleSceneName) && Application.CanStreamedLevelBeLoaded(battleSceneName))
            {
                SceneManager.LoadScene(battleSceneName);
            }
            else if (SceneManager.sceneCountInBuildSettings > 1)
            {
                SceneManager.LoadScene(1);
            }
            else
            {
                Debug.LogError("[CharacterSelectManager] Battle scene is not registered in Build Settings!");
            }
        }

        private void RefreshUI()
        {
            EnsureGlassSpritesResolved();

            if (selectedCharacterText != null)
            {
                selectedCharacterText.text = $"NHÂN VẬT: {CharacterNames[selectedCharacter]}";
                GlassHudTheme.StyleTmpText(selectedCharacterText, -1f, selectedCharacterText.color, bold: true, addShadow: true);
            }

            if (characterDescriptionText != null)
            {
                characterDescriptionText.text = string.Empty;
                characterDescriptionText.gameObject.SetActive(false);
            }

            Color formTint = showTransformedPortrait ? new Color(1f, 0.90f, 0.45f, 1f) : Color.white;
            ApplyResetFormButtonSprite(formTint);
            UIManager.EnsureSwapFormButtonVisual(
                toggleFormButton,
                toggleFormButtonText,
                Application.isPlaying,
                formTint
            );

            if (characterPortraits != null)
            {
                for (int i = 0; i < characterPortraits.Length; i++)
                {
                    if (characterPortraits[i] == null) continue;
                    Sprite chosen = showTransformedPortrait && transformedPortraits != null && i < transformedPortraits.Length
                        ? transformedPortraits[i]
                        : (normalPortraits != null && i < normalPortraits.Length ? normalPortraits[i] : null);
                    if (chosen != null) characterPortraits[i].sprite = chosen;
                    characterPortraits[i].preserveAspect = true;
                    characterPortraits[i].rectTransform.localScale = Vector3.one;
                    characterPortraits[i].color = Color.white;
                }
            }

            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    if (characterButtons[i] == null) continue;
                    bool isSel = (i == selectedCharacter);
                    characterButtons[i].transform.localScale = isSel ? Vector3.one * 1.08f : Vector3.one;
                    UIManager.EnsureGoldSelectionFrame(
                        characterButtons[i].transform,
                        null,
                        false,
                        Application.isPlaying
                    );
                    Image bg = characterButtons[i].GetComponent<Image>();
                    if (bg != null)
                    {
                        if (charSlotGlassSprite != null || charSlotGoldSelectedSprite != null)
                        {
                            Sprite frame = isSel
                                ? (charSlotGoldSelectedSprite ?? charSlotGlassSprite)
                                : (charSlotGlassSprite ?? charSlotGoldSelectedSprite);
                            RectTransform bgRt = bg.rectTransform;
                            float w = bgRt != null && bgRt.sizeDelta.x > 1f ? bgRt.sizeDelta.x : 290f;
                            float h = bgRt != null && bgRt.sizeDelta.y > 1f ? bgRt.sizeDelta.y : 360f;
                            GlassHudTheme.ApplySlicedSprite(bg, frame, w, h, Color.white);
                        }
                        else
                        {
                            bg.color = isSel ? new Color(0.95f, 0.72f, 0.15f, 1f) : new Color(0.22f, 0.24f, 0.30f, 1f);
                        }
                    }

                    TMP_Text nameLbl = characterButtons[i].GetComponentInChildren<TMP_Text>(true);
                    if (nameLbl != null)
                    {
                        GlassHudTheme.StyleTmpText(nameLbl, -1f, isSel ? new Color(1f, 0.90f, 0.35f, 1f) : Color.white, bold: true, addShadow: true);
                    }
                }
            }

            int unlockedStage = Mathf.Max(1, PlayerPrefs.GetInt(UNLOCKED_STAGE_KEY, 12));
            if (stageButtons != null)
            {
                for (int i = 0; i < stageButtons.Length; i++)
                {
                    if (stageButtons[i] == null) continue;
                    bool isUnlocked = (i + 1) <= unlockedStage;
                    bool isSel = (i == selectedStageIndex);
                    bool isBossStage = (i % 4) == 3;
                    int worldIdx = i / 4;
                    string worldSpriteName = GetWorldStageCardSpriteName(worldIdx);

                    stageButtons[i].interactable = isUnlocked;
                    stageButtons[i].transform.localScale = isSel ? Vector3.one * 1.05f : Vector3.one;
                    UIManager.EnsureGoldSelectionFrame(
                        stageButtons[i].transform,
                        null,
                        isSel,
                        Application.isPlaying
                    );

                    Image bg = stageButtons[i].GetComponent<Image>();
                    if (bg != null)
                    {
                        if (GlassHudTheme.LoadSprite(worldSpriteName) != null)
                        {
                            GlassHudTheme.ApplySprite(bg, worldSpriteName, default, sliced: true, preserveAspect: false);
                            bg.color = !isUnlocked
                                ? new Color(0.50f, 0.54f, 0.62f, 0.55f)
                                : (isSel ? Color.white : new Color(0.92f, 0.95f, 1f, 0.92f));
                        }
                        else if (charSlotGlassSprite != null || charSlotGoldSelectedSprite != null)
                        {
                            Sprite stageFrame = isSel
                                ? (charSlotGoldSelectedSprite ?? charSlotGlassSprite)
                                : (charSlotGlassSprite ?? charSlotGoldSelectedSprite);
                            RectTransform bgRt = bg.rectTransform;
                            float w = bgRt != null && bgRt.sizeDelta.x > 1f ? bgRt.sizeDelta.x : 124f;
                            float h = bgRt != null && bgRt.sizeDelta.y > 1f ? bgRt.sizeDelta.y : 64f;
                            Color stTint = !isUnlocked
                                ? new Color(0.55f, 0.58f, 0.65f, 0.55f)
                                : (isSel ? Color.white : new Color(0.85f, 0.92f, 1f, 0.92f));
                            GlassHudTheme.ApplySlicedSprite(bg, stageFrame, w, h, stTint);
                        }
                        else
                        {
                            if (!isUnlocked)
                                bg.color = new Color(0.18f, 0.18f, 0.18f, 0.7f);
                            else if (isSel)
                                bg.color = new Color(0.2f, 0.78f, 0.35f, 1f);
                            else
                                bg.color = new Color(0.26f, 0.34f, 0.48f, 1f);
                        }
                    }

                    TMP_Text stageLbl = stageButtons[i].GetComponentInChildren<TMP_Text>(true);
                    if (stageLbl != null)
                    {
                        Color labelColor = !isUnlocked
                            ? new Color(0.70f, 0.74f, 0.80f, 0.85f)
                            : (isBossStage ? new Color(1f, 0.90f, 0.35f, 1f) : Color.white);
                        GlassHudTheme.StyleTmpText(stageLbl, -1f, labelColor, bold: true, addShadow: true);
                    }
                }
            }

            if (selectedStageText != null)
            {
                int worldIdx = selectedStageIndex / 4;
                int stageIdxInWorld = selectedStageIndex % 4;
                selectedStageText.text = $"MÀN CHƠI: {WorldNames[Mathf.Clamp(worldIdx, 0, 2)]} - {StageRoles[Mathf.Clamp(stageIdxInWorld, 0, 3)]}";
                GlassHudTheme.StyleTmpText(selectedStageText, -1f, selectedStageText.color, bold: true, addShadow: true);
            }
        }

        private static Sprite ResolveSlicedSprite(Sprite current, string relPath, string expectedName)
        {
            if (current != null && string.Equals(current.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
            Sprite loaded = GlassHudTheme.LoadSprite(relPath);
            return loaded != null ? loaded : current;
        }

        private void EnsureGlassSpritesResolved()
        {
            charSlotGlassSprite = ResolveSlicedSprite(charSlotGlassSprite, "03_CharacterAndItemSelect/Slot_CharSelect_Unselected_Clean", "Slot_CharSelect_Unselected_Clean");
            charSlotGoldSelectedSprite = ResolveSlicedSprite(charSlotGoldSelectedSprite, "03_CharacterAndItemSelect/Slot_CharSelect_SelectedGold_Clean", "Slot_CharSelect_SelectedGold_Clean");
            techCutActionButtonSprite = ResolveSlicedSprite(techCutActionButtonSprite, "03_CharacterAndItemSelect/Btn_CharSelect_Continue_Clean", "Btn_CharSelect_Continue_Clean");
            mainPanelFrameSprite = ResolveSlicedSprite(mainPanelFrameSprite, "03_CharacterAndItemSelect/Frame_CharSelect_MainPanel_Clean", "Frame_CharSelect_MainPanel_Clean");
            backButtonSprite = ResolveSlicedSprite(backButtonSprite, "03_CharacterAndItemSelect/Btn_CharSelect_Back_Clean", "Btn_CharSelect_Back_Clean");
            resetFormIconSprite = ResolveSlicedSprite(resetFormIconSprite, "03_CharacterAndItemSelect/Btn_CharSelect_ResetIcon", "Btn_CharSelect_ResetIcon");
        }

        private void ApplyGlassmorphismStyling()
        {
            EnsureGlassSpritesResolved();

            MenuBackgroundLoader bgLoader = GetComponentInChildren<MenuBackgroundLoader>(true);
            if (bgLoader != null)
            {
                bgLoader.ApplyVisuals();
            }

            if (TryGetComponent(out Image canvasRootImg))
            {
                canvasRootImg.color = Color.clear;
                canvasRootImg.raycastTarget = false;
            }

            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    if (characterButtons[i] == null) continue;
                    RectTransform cardRt = characterButtons[i].GetComponent<RectTransform>();
                    if (cardRt != null)
                    {
                        if (i < 3)
                        {
                            cardRt.anchoredPosition = new Vector2(-560f + i * 320f, 175f);
                        }
                        cardRt.sizeDelta = new Vector2(290f, 360f);
                    }
                    Transform portraitTf = characterButtons[i].transform.Find("Portrait");
                    if (portraitTf != null && portraitTf.TryGetComponent(out RectTransform prt))
                    {
                        prt.anchoredPosition = new Vector2(0f, 24f);
                        prt.sizeDelta = new Vector2(210f, 210f);
                    }
                    Transform nameTf = characterButtons[i].transform.Find("NameLabel");
                    if (nameTf != null && nameTf.TryGetComponent(out RectTransform nrt))
                    {
                        nrt.anchoredPosition = new Vector2(0f, -124f);
                        nrt.sizeDelta = new Vector2(230f, 42f);
                    }
                }
            }

            Transform infoBoxTf = transform.Find("CharacterInfoBox");
            if (infoBoxTf != null && infoBoxTf.TryGetComponent(out RectTransform infoRt))
            {
                infoRt.anchoredPosition = new Vector2(-240f, -250f);
                infoRt.sizeDelta = new Vector2(880f, 220f);
            }
            if (selectedCharacterText != null)
            {
                RectTransform selCharRt = selectedCharacterText.rectTransform;
                if (selCharRt != null && selCharRt.sizeDelta.x > 820f)
                {
                    selCharRt.sizeDelta = new Vector2(820f, selCharRt.sizeDelta.y);
                }
            }
            if (characterDescriptionText != null)
            {
                RectTransform descRt = characterDescriptionText.rectTransform;
                if (descRt != null && descRt.sizeDelta.x > 820f)
                {
                    descRt.sizeDelta = new Vector2(820f, descRt.sizeDelta.y);
                }
            }
            Transform stagePanelTf = transform.Find("StageSelectionPanel");
            ApplyMainPanelFrame(infoBoxTf, new Color(0.06f, 0.09f, 0.16f, 0.48f));
            ApplyMainPanelFrame(stagePanelTf, new Color(0.06f, 0.09f, 0.16f, 0.48f));

            Transform titleTf = transform.Find("TitleText");
            if (titleTf != null && titleTf.TryGetComponent(out TMP_Text titleTmp))
            {
                if (titleTmp.text.Contains("CHON NHAN VAT"))
                {
                    titleTmp.text = "STICKMAN BEAT 'EM UP  -  CHỌN NHÂN VẬT & MÀN CHƠI";
                }
                GlassHudTheme.StyleTmpText(titleTmp, -1f, titleTmp.color, bold: true, addShadow: true);
            }

            if (stagePanelTf != null)
            {
                Transform stageHeaderTf = stagePanelTf.Find("StageHeader");
                if (stageHeaderTf != null && stageHeaderTf.TryGetComponent(out TMP_Text stageHeaderTmp))
                {
                    if (stageHeaderTmp.text.Contains("CHON THE GIOI"))
                    {
                        stageHeaderTmp.text = "CHỌN THẾ GIỚI & MÀN CHƠI (12 MÀN)";
                    }
                    GlassHudTheme.StyleTmpText(stageHeaderTmp, -1f, stageHeaderTmp.color, bold: true, addShadow: true);
                }
                for (int w = 1; w <= 3; w++)
                {
                    StyleTmpAt(stagePanelTf.Find($"WorldLabel_{w}"));
                }
            }

            if (playButton != null)
            {
                if (playButton.TryGetComponent(out RectTransform playRt))
                {
                    playRt.anchoredPosition = new Vector2(560f, -358f);
                    playRt.sizeDelta = new Vector2(400f, 90f);
                }
                if (playButton.TryGetComponent(out Image playImg) && techCutActionButtonSprite != null)
                {
                    GlassHudTheme.ApplySlicedSprite(playImg, techCutActionButtonSprite, 400f, 90f, Color.white);
                }
                TMP_Text playTmp = playButton.GetComponentInChildren<TMP_Text>(true);
                if (playTmp != null)
                {
                    if (playTmp.text.Contains("BAT DAU"))
                    {
                        playTmp.text = "BẮT ĐẦU CHIẾN ĐẤU (PLAY)";
                    }
                    GlassHudTheme.StyleTmpText(playTmp, -1f, Color.white, bold: true, addShadow: true);
                }
            }

            if (unlockAllButton != null)
            {
                if (unlockAllButton.TryGetComponent(out RectTransform unlockRt))
                {
                    unlockRt.anchoredPosition = new Vector2(560f, -275f);
                    unlockRt.sizeDelta = new Vector2(300f, 58f);
                }
                Sprite unlockSp = backButtonSprite ?? techCutActionButtonSprite;
                if (unlockAllButton.TryGetComponent(out Image unlockImg) && unlockSp != null)
                {
                    GlassHudTheme.ApplySlicedSprite(unlockImg, unlockSp, 300f, 58f, Color.white);
                }
                TMP_Text unlockTmp = unlockAllButton.GetComponentInChildren<TMP_Text>(true);
                if (unlockTmp != null)
                {
                    if (unlockTmp.text.Contains("Mo Khoa"))
                    {
                        unlockTmp.text = "Mở Khóa Toàn Bộ 12 Màn";
                    }
                    GlassHudTheme.StyleTmpText(unlockTmp, -1f, Color.white, bold: true, addShadow: true);
                }
            }

            ApplyResetFormButtonSprite(showTransformedPortrait ? new Color(1f, 0.90f, 0.45f, 1f) : Color.white);
        }

        private void ApplyResetFormButtonSprite(Color tint)
        {
            if (toggleFormButton == null) return;
            if (toggleFormButton.TryGetComponent(out RectTransform rt))
            {
                rt.anchoredPosition = new Vector2(-240f, -85f);
                rt.sizeDelta = new Vector2(88f, 80f);
            }
            Sprite btnSprite = resetFormIconSprite ?? techCutActionButtonSprite;
            if (btnSprite != null && toggleFormButton.TryGetComponent(out Image img))
            {
                GlassHudTheme.ApplySlicedSprite(img, btnSprite, 88f, 80f, tint);
            }
        }

        private void ApplyMainPanelFrame(Transform tf, Color fallbackColor)
        {
            if (tf == null || !tf.TryGetComponent(out Image img)) return;

            Sprite panelSp = mainPanelFrameSprite ?? GlassHudTheme.LoadSprite("03_CharacterAndItemSelect/Frame_CharSelect_MainPanel_Clean");
            if (panelSp != null)
            {
                RectTransform rt = img.rectTransform;
                float w = rt != null && rt.sizeDelta.x > 1f ? rt.sizeDelta.x : 620f;
                float h = rt != null && rt.sizeDelta.y > 1f ? rt.sizeDelta.y : 220f;
                GlassHudTheme.ApplySlicedSprite(img, panelSp, w, h, Color.white);
                if (tf.TryGetComponent(out Outline outline))
                {
                    outline.enabled = false;
                }
            }
            else
            {
                img.color = fallbackColor;
            }
        }

        private static string GetWorldStageCardSpriteName(int worldIdx)
        {
            return worldIdx switch
            {
                0 => "WorldCard_AoT_EmeraldAmber.png",
                1 => "WorldCard_DB_AmberGold.png",
                2 => "WorldCard_Diend_MagentaCyan.png",
                _ => "WorldCard_AoT_EmeraldAmber.png"
            };
        }

        private static void StyleTmpAt(Transform tf)
        {
            if (tf != null && tf.TryGetComponent(out TMP_Text tmp))
            {
                GlassHudTheme.StyleTmpText(tmp, -1f, tmp.color, bold: true, addShadow: true);
            }
        }

        private static void StyleButtonTmpLabel(Button btn)
        {
            if (btn == null) return;
            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
            {
                GlassHudTheme.StyleTmpText(tmp, -1f, Color.white, bold: true, addShadow: true);
            }
        }
    }
}
