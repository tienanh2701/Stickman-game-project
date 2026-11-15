using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Economy;
using StickmanBeatEmUp.Items;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.UI
{
    public enum MainMenuScreen
    {
        MainMenu = 0,
        Instructions = 1,
        CharacterSelect = 2,
        WorldSelect = 3,
        StageSelect = 4,
        ModeSelect = 5,
        Shop = 6
    }

    [Serializable]
    public class MenuCharacterCardUI
    {
        public string characterName;
        public Button selectButton;
        public Button toggleFormButton;
        public Image portraitImage;
        public GameObject portraitBackdrop;
        public Image highlightBorder;
        public Text characterNameText;
        public Text formBadgeText;
        public Text toggleButtonText;
        public Text skillsDescText;
        public Sprite baseSprite;
        public Sprite transformSprite;
        [TextArea(2, 4)] public string baseSkillsSummary;
        [TextArea(2, 4)] public string transformSkillsSummary;
        public bool isLocked;
        public GameObject lockOverlay;
        public Text lockedLabelText;
        [HideInInspector] public bool showingTransform;
    }

    [Serializable]
    public class MenuWorldCardUI
    {
        public string worldTitle;
        public Button selectButton;
        public Image previewImage;
        public Image highlightBorder;
        public GameObject selectedBadge;
        public Sprite worldBackgroundSprite;
    }

    /// <summary>
    /// Quản lý luồng màn hình của MainMenu (1920x1080):
    /// MainMenu (CHƠI, HƯỚNG DẪN, THOÁT) -> ModeSelect (Chiến dịch, Vô tận, Luyện tập, Cửa hàng)
    /// -> Compact 5-Slot CharacterSelect + Q/E Loadout + Preview Toàn Thân
    /// -> WorldSelect -> StageSelect (Campaign) hoặc vào thẳng Stickman_DemoScene (Endless/Training).
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private const string SELECTED_CHAR_KEY = "SelectedCharacter";
        private const string SELECTED_WORLD_KEY = "SelectedWorld";
        private const string SELECTED_STAGE_INDEX_KEY = "SelectedStageIndex";
        private const string BATTLE_SCENE_NAME = "Stickman_DemoScene";
        private const float FULL_PREVIEW_ALPHA = 0.82f;

        private static readonly ConsumableItemType[] LoadoutCycleOrder =
        {
            ConsumableItemType.SenzuBean,
            ConsumableItemType.SpinalFluid,
            ConsumableItemType.UltraDivineWater
        };

        private static readonly string[] DefaultCharacterNames =
        {
            "GOKU",
            "EREN",
            "DIEND"
        };

        [Header("Fixed Navigation")]
        [SerializeField] private Button backButton;

        [Header("Screen Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject instructionsPanel;
        [SerializeField] private GameObject characterSelectPanel;
        [SerializeField] private GameObject worldSelectPanel;
        [SerializeField] private GameObject stageSelectPanel;
        [SerializeField] private GameObject modeSelectPanel;
        [SerializeField] private GameObject shopPanel;

        [Header("Screen 1: Main Menu")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button instructionsButton;
        [SerializeField] private Button exitButton;

        [Header("Screen 1B: Mode Select & Shop")]
        [SerializeField] private Button campaignModeButton;
        [SerializeField] private Button endlessModeButton;
        [SerializeField] private Button trainingModeButton;
        [SerializeField] private Button openShopButton;
        [SerializeField] private ShopManager shopManager;
        [SerializeField] private CoinCounterUI coinCounterUI;

        [Header("Screen 2: Compact 5-Slot Character Select")]
        [SerializeField] private MenuCharacterCardUI[] characterCards = new MenuCharacterCardUI[5];
        [SerializeField] private Button characterNextButton;
        [SerializeField] private Button confirmCharacterButton;

        [Header("Character Full-Body Preview & Details")]
        [SerializeField] private Image bgCharacterPreviewImage;
        [SerializeField] private Sprite[] charNormalFullSprites = new Sprite[3];
        [SerializeField] private Sprite[] charTransFullSprites = new Sprite[3];
        [SerializeField] private Button previewFormToggleButton;
        [SerializeField] private TMP_Text previewFormToggleText;
        [SerializeField] private TMP_Text selectedCharacterNameText;
        [SerializeField] private TMP_Text selectedCharacterSkillsText;
        [SerializeField] private TMP_Text characterSelectFeedbackText;

        [Header("Character Select Loadout Slots (Q & E)")]
        [SerializeField] private Button loadoutQButton;
        [SerializeField] private Image loadoutQIcon;
        [SerializeField] private TMP_Text loadoutQCountText;
        [SerializeField] private TMP_Text loadoutQNameText;
        [SerializeField] private Button loadoutEButton;
        [SerializeField] private Image loadoutEIcon;
        [SerializeField] private TMP_Text loadoutECountText;
        [SerializeField] private TMP_Text loadoutENameText;
        [SerializeField] private Sprite lockedSlotSprite;

        [Header("Screen 3: World Select")]
        [SerializeField] private MenuWorldCardUI[] worldCards = new MenuWorldCardUI[3];
        [SerializeField] private Button worldNextButton;

        [Header("Typography")]
        [SerializeField] private Font vietnameseFont;

        [Header("Screen 4: Stage Select")]
        [SerializeField] private Button[] stageButtons = new Button[4];
        [SerializeField] private Text stageSelectionHeaderText;

        private MainMenuScreen currentScreen = MainMenuScreen.MainMenu;
        private int selectedCharIndex;
        private int selectedWorldIndex;
        private bool previewShowingTransform;
        private float feedbackTimer;
        private TMP_FontAsset runtimeTmpFont;
        private Texture2D runtimeCircleTexture;
        private Sprite runtimeCircleSprite;
        private bool isInventorySubscribed;

        private static readonly Color SelectedHighlightColor = new Color(1f, 0.85f, 0.25f, 1f);
        private static readonly Color IdleBorderColor = new Color(0.28f, 0.34f, 0.45f, 0.75f);
        private static readonly Color LockedBorderColor = new Color(0.22f, 0.22f, 0.26f, 0.65f);
        private static readonly Color EmptyIconDimColor = new Color(0.45f, 0.45f, 0.45f, 0.42f);

        private void Awake()
        {
            ApplyVietnameseFont();
            ApplySmoothCircleLoadoutSprites();
        }

        private void ApplyVietnameseFont()
        {
            Font resolvedFont = vietnameseFont != null
                ? vietnameseFont
                : Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 24);
            if (resolvedFont == null) return;

            Text[] labels = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].font == null)
                {
                    labels[i].font = resolvedFont;
                }
            }

            if (!Application.isPlaying) return;

            if (runtimeTmpFont == null)
            {
                runtimeTmpFont = TMP_FontAsset.CreateFontAsset(resolvedFont);
            }

            if (runtimeTmpFont != null)
            {
                TMP_Text[] tmpLabels = GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < tmpLabels.Length; i++)
                {
                    if (tmpLabels[i] != null)
                    {
                        tmpLabels[i].font = runtimeTmpFont;
                    }
                }
            }
        }

        private void Start()
        {
            ApplyVietnameseFont();
            Time.timeScale = 1f;

            if (CurrencyManager.HasInstance)
            {
                CurrencyManager.Instance.SuppressEnemyRewards = false;
            }
            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.IsTrainingFreeUseMode = false;
            }

            StageManager.LoadSelectedGameMode();
            selectedCharIndex = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_CHAR_KEY, 0), 0, 2);
            selectedWorldIndex = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_WORLD_KEY, 0), 0, 2);

            if (characterSelectFeedbackText != null)
            {
                characterSelectFeedbackText.text = string.Empty;
            }

            SubscribeInventoryEvents();
            BindButtons();
            RefreshCharacterCardsUI();
            RefreshCharacterPreviewUI();
            RefreshLoadoutSlotsUI();
            RefreshWorldCardsUI();
            RefreshStageHeaderUI();
            ShowScreen(MainMenuScreen.MainMenu);
        }

        private void Update()
        {
            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.unscaledDeltaTime;
                if (feedbackTimer <= 0f && characterSelectFeedbackText != null)
                {
                    characterSelectFeedbackText.text = string.Empty;
                }
            }
        }

        private void OnEnable()
        {
            SubscribeInventoryEvents();
        }

        private void OnDisable()
        {
            UnsubscribeInventoryEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeInventoryEvents();
            if (runtimeTmpFont != null)
            {
                Destroy(runtimeTmpFont);
                runtimeTmpFont = null;
            }
            if (runtimeCircleSprite != null)
            {
                Destroy(runtimeCircleSprite);
                runtimeCircleSprite = null;
            }
            if (runtimeCircleTexture != null)
            {
                Destroy(runtimeCircleTexture);
                runtimeCircleTexture = null;
            }
        }

        private void ApplySmoothCircleLoadoutSprites()
        {
            if (!Application.isPlaying) return;

            if (runtimeCircleSprite == null)
            {
                const int size = 128;
                runtimeCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                runtimeCircleTexture.filterMode = FilterMode.Bilinear;
                runtimeCircleTexture.wrapMode = TextureWrapMode.Clamp;

                Color32[] pixels = new Color32[size * size];
                float half = (size - 1) * 0.5f;
                float radius = size * 0.48f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - half;
                        float dy = y - half;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float alpha = Mathf.Clamp01(radius - dist + 0.75f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                }

                runtimeCircleTexture.SetPixels32(pixels);
                runtimeCircleTexture.Apply();
                runtimeCircleSprite = Sprite.Create(
                    runtimeCircleTexture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
            }

            ApplyCircleSpriteToSlot(loadoutQButton);
            ApplyCircleSpriteToSlot(loadoutEButton);
        }

        private void ApplyCircleSpriteToSlot(Button slotButton)
        {
            if (slotButton == null || runtimeCircleSprite == null) return;

            Image borderImg = slotButton.GetComponent<Image>();
            if (borderImg != null)
            {
                borderImg.sprite = runtimeCircleSprite;
            }

            Transform maskTf = slotButton.transform.Find("CircleMask");
            if (maskTf != null)
            {
                Image maskImg = maskTf.GetComponent<Image>();
                if (maskImg != null)
                {
                    maskImg.sprite = runtimeCircleSprite;
                }
            }

            Transform keyBadgeTf = slotButton.transform.Find("KeyBadge");
            if (keyBadgeTf != null)
            {
                Image keyImg = keyBadgeTf.GetComponent<Image>();
                if (keyImg != null)
                {
                    keyImg.sprite = runtimeCircleSprite;
                }
            }

            Transform countBadgeTf = slotButton.transform.Find("CountBadge");
            if (countBadgeTf != null)
            {
                Image countImg = countBadgeTf.GetComponent<Image>();
                if (countImg != null)
                {
                    countImg.sprite = runtimeCircleSprite;
                }
            }
        }

        public void ConfigureBuilderReferences(
            Button backBtn,
            GameObject pMain, GameObject pInstr, GameObject pChar, GameObject pWorld, GameObject pStage,
            Button playBtn, Button instrBtn, Button exitBtn,
            MenuCharacterCardUI[] chars, Button charNextBtn,
            MenuWorldCardUI[] worlds, Button worldNextBtn,
            Button[] stages, Text stageHeader,
            Font vnFont = null)
        {
            backButton = backBtn;
            mainMenuPanel = pMain;
            instructionsPanel = pInstr;
            characterSelectPanel = pChar;
            worldSelectPanel = pWorld;
            stageSelectPanel = pStage;
            playButton = playBtn;
            instructionsButton = instrBtn;
            exitButton = exitBtn;
            characterCards = chars;
            characterNextButton = charNextBtn;
            worldCards = worlds;
            worldNextButton = worldNextBtn;
            stageButtons = stages;
            stageSelectionHeaderText = stageHeader;
            if (vnFont != null)
            {
                vietnameseFont = vnFont;
            }
            ApplyVietnameseFont();
        }

        public void ConfigureExpandedUI(
            GameObject pModeSelect,
            GameObject pShop,
            Button campaignBtn,
            Button endlessBtn,
            Button trainingBtn,
            Button shopOpenBtn,
            Button confirmCharBtn,
            Image bgPreviewImg,
            Sprite[] normalFullSprites,
            Sprite[] transFullSprites,
            Button togglePreviewBtn,
            TMP_Text togglePreviewTxt,
            TMP_Text charNameTxt,
            TMP_Text charSkillsTxt,
            TMP_Text charFeedbackTxt,
            Button qSlotBtn,
            Image qSlotIcon,
            TMP_Text qCountTxt,
            TMP_Text qNameTxt,
            Button eSlotBtn,
            Image eSlotIcon,
            TMP_Text eCountTxt,
            TMP_Text eNameTxt,
            CoinCounterUI coinUi = null,
            ShopManager shopMgr = null,
            Sprite emptySlotIcon = null)
        {
            modeSelectPanel = pModeSelect;
            shopPanel = pShop;
            campaignModeButton = campaignBtn;
            endlessModeButton = endlessBtn;
            trainingModeButton = trainingBtn;
            openShopButton = shopOpenBtn;
            confirmCharacterButton = confirmCharBtn;
            bgCharacterPreviewImage = bgPreviewImg;
            charNormalFullSprites = normalFullSprites ?? new Sprite[3];
            charTransFullSprites = transFullSprites ?? new Sprite[3];
            previewFormToggleButton = togglePreviewBtn;
            previewFormToggleText = togglePreviewTxt;
            selectedCharacterNameText = charNameTxt;
            selectedCharacterSkillsText = charSkillsTxt;
            characterSelectFeedbackText = charFeedbackTxt;
            loadoutQButton = qSlotBtn;
            loadoutQIcon = qSlotIcon;
            loadoutQCountText = qCountTxt;
            loadoutQNameText = qNameTxt;
            loadoutEButton = eSlotBtn;
            loadoutEIcon = eSlotIcon;
            loadoutECountText = eCountTxt;
            loadoutENameText = eNameTxt;
            if (coinUi != null) coinCounterUI = coinUi;
            if (shopMgr != null) shopManager = shopMgr;
            if (emptySlotIcon != null) lockedSlotSprite = emptySlotIcon;

            ApplyVietnameseFont();
            if (Application.isPlaying)
            {
                ApplySmoothCircleLoadoutSprites();
                BindButtons();
            }
            RefreshCharacterCardsUI();
            RefreshCharacterPreviewUI();
            RefreshLoadoutSlotsUI();
        }

        private void SubscribeInventoryEvents()
        {
            if (isInventorySubscribed) return;

            InventoryManager inv = InventoryManager.Instance;
            if (inv == null) return;

            inv.OnInventoryChanged -= HandleInventoryChanged;
            inv.OnInventoryChanged += HandleInventoryChanged;
            isInventorySubscribed = true;
        }

        private void UnsubscribeInventoryEvents()
        {
            if (!isInventorySubscribed) return;

            if (InventoryManager.HasInstance)
            {
                InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
            }
            isInventorySubscribed = false;
        }

        private void HandleInventoryChanged()
        {
            RefreshLoadoutSlotsUI();
        }

        private void BindButtons()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(HandleBackClicked);
            }
            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(() =>
                    ShowScreen(modeSelectPanel != null ? MainMenuScreen.ModeSelect : MainMenuScreen.CharacterSelect));
            }
            if (instructionsButton != null)
            {
                instructionsButton.onClick.RemoveAllListeners();
                instructionsButton.onClick.AddListener(() => ShowScreen(MainMenuScreen.Instructions));
            }
            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(HandleExitClicked);
            }

            if (campaignModeButton != null)
            {
                campaignModeButton.onClick.RemoveAllListeners();
                campaignModeButton.onClick.AddListener(() => SelectGameModeAndContinue(GameMode.Campaign));
            }
            if (endlessModeButton != null)
            {
                endlessModeButton.onClick.RemoveAllListeners();
                endlessModeButton.onClick.AddListener(() => SelectGameModeAndContinue(GameMode.Endless));
            }
            if (trainingModeButton != null)
            {
                trainingModeButton.onClick.RemoveAllListeners();
                trainingModeButton.onClick.AddListener(() => SelectGameModeAndContinue(GameMode.Training));
            }
            if (openShopButton != null)
            {
                openShopButton.onClick.RemoveAllListeners();
                openShopButton.onClick.AddListener(() => ShowScreen(MainMenuScreen.Shop));
            }

            if (characterCards != null)
            {
                for (int i = 0; i < characterCards.Length; i++)
                {
                    int idx = i;
                    MenuCharacterCardUI card = characterCards[i];
                    if (card == null) continue;

                    if (card.selectButton != null)
                    {
                        card.selectButton.onClick.RemoveAllListeners();
                        card.selectButton.onClick.AddListener(() => SelectCharacter(idx));
                    }
                    if (card.toggleFormButton != null)
                    {
                        card.toggleFormButton.onClick.RemoveAllListeners();
                        card.toggleFormButton.onClick.AddListener(() => ToggleCharacterCardForm(idx));
                    }
                }
            }

            if (previewFormToggleButton != null)
            {
                previewFormToggleButton.onClick.RemoveAllListeners();
                previewFormToggleButton.onClick.AddListener(ToggleCharacterPreviewForm);
            }

            if (loadoutQButton != null)
            {
                loadoutQButton.onClick.RemoveAllListeners();
                loadoutQButton.onClick.AddListener(() => CycleLoadoutSlot(InventoryManager.SLOT_Q));
            }
            if (loadoutEButton != null)
            {
                loadoutEButton.onClick.RemoveAllListeners();
                loadoutEButton.onClick.AddListener(() => CycleLoadoutSlot(InventoryManager.SLOT_E));
            }

            if (confirmCharacterButton != null)
            {
                confirmCharacterButton.onClick.RemoveAllListeners();
                confirmCharacterButton.onClick.AddListener(HandleConfirmCharacterClicked);
            }
            if (characterNextButton != null && characterNextButton != confirmCharacterButton)
            {
                characterNextButton.onClick.RemoveAllListeners();
                characterNextButton.onClick.AddListener(HandleConfirmCharacterClicked);
            }

            if (worldCards != null)
            {
                for (int i = 0; i < worldCards.Length; i++)
                {
                    int idx = i;
                    MenuWorldCardUI wCard = worldCards[i];
                    if (wCard != null && wCard.selectButton != null)
                    {
                        wCard.selectButton.onClick.RemoveAllListeners();
                        wCard.selectButton.onClick.AddListener(() => SelectWorld(idx));
                    }
                }
            }

            if (worldNextButton != null)
            {
                worldNextButton.onClick.RemoveAllListeners();
                worldNextButton.onClick.AddListener(() =>
                {
                    RefreshStageHeaderUI();
                    ShowScreen(MainMenuScreen.StageSelect);
                });
            }

            if (stageButtons != null)
            {
                for (int i = 0; i < stageButtons.Length; i++)
                {
                    int stageIdx = i;
                    if (stageButtons[i] != null)
                    {
                        stageButtons[i].onClick.RemoveAllListeners();
                        stageButtons[i].onClick.AddListener(() => LaunchStage(stageIdx));
                    }
                }
            }
        }

        public void ShowScreen(MainMenuScreen screen)
        {
            currentScreen = screen;

            if (mainMenuPanel != null) mainMenuPanel.SetActive(screen == MainMenuScreen.MainMenu);
            if (modeSelectPanel != null) modeSelectPanel.SetActive(screen == MainMenuScreen.ModeSelect);
            if (shopPanel != null) shopPanel.SetActive(screen == MainMenuScreen.Shop);
            if (instructionsPanel != null) instructionsPanel.SetActive(screen == MainMenuScreen.Instructions);
            if (characterSelectPanel != null) characterSelectPanel.SetActive(screen == MainMenuScreen.CharacterSelect);
            if (worldSelectPanel != null) worldSelectPanel.SetActive(screen == MainMenuScreen.WorldSelect);
            if (stageSelectPanel != null) stageSelectPanel.SetActive(screen == MainMenuScreen.StageSelect);

            if (backButton != null)
            {
                backButton.gameObject.SetActive(screen != MainMenuScreen.MainMenu);
            }

            if (screen == MainMenuScreen.CharacterSelect)
            {
                RefreshCharacterCardsUI();
                RefreshCharacterPreviewUI();
                RefreshLoadoutSlotsUI();
            }
            else if (screen == MainMenuScreen.Shop && shopManager != null)
            {
                shopManager.RefreshShopUI();
            }

            if (coinCounterUI != null)
            {
                coinCounterUI.RefreshImmediate();
            }
        }

        public void HandleBackClicked()
        {
            switch (currentScreen)
            {
                case MainMenuScreen.ModeSelect:
                case MainMenuScreen.Instructions:
                    ShowScreen(MainMenuScreen.MainMenu);
                    break;
                case MainMenuScreen.Shop:
                    ShowScreen(modeSelectPanel != null ? MainMenuScreen.ModeSelect : MainMenuScreen.MainMenu);
                    break;
                case MainMenuScreen.CharacterSelect:
                    ShowScreen(modeSelectPanel != null ? MainMenuScreen.ModeSelect : MainMenuScreen.MainMenu);
                    break;
                case MainMenuScreen.WorldSelect:
                    ShowScreen(MainMenuScreen.CharacterSelect);
                    break;
                case MainMenuScreen.StageSelect:
                    ShowScreen(MainMenuScreen.WorldSelect);
                    break;
            }
        }

        public void SelectGameModeAndContinue(GameMode mode)
        {
            StageManager.SetSelectedGameMode(mode);
            ShowScreen(MainMenuScreen.CharacterSelect);
        }

        public void SelectCharacter(int index)
        {
            if (IsCharacterSlotLocked(index))
            {
                ShowCharacterSelectFeedback("Nhân vật đang phát triển!");
                return;
            }

            selectedCharIndex = index;
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, selectedCharIndex);
            PlayerPrefs.Save();

            if (characterCards != null)
            {
                for (int i = 0; i < characterCards.Length; i++)
                {
                    if (characterCards[i] != null)
                    {
                        characterCards[i].showingTransform = (i == selectedCharIndex) && previewShowingTransform;
                    }
                }
            }

            if (characterSelectFeedbackText != null)
            {
                characterSelectFeedbackText.text = string.Empty;
                feedbackTimer = 0f;
            }

            RefreshCharacterCardsUI();
            RefreshCharacterPreviewUI();
            RefreshStageHeaderUI();
        }

        public void ToggleCharacterCardForm(int index)
        {
            if (IsCharacterSlotLocked(index))
            {
                ShowCharacterSelectFeedback("Nhân vật đang phát triển!");
                return;
            }

            if (characterCards == null || index < 0 || index >= characterCards.Length) return;
            MenuCharacterCardUI card = characterCards[index];
            if (card == null) return;

            card.showingTransform = !card.showingTransform;
            previewShowingTransform = card.showingTransform;
            selectedCharIndex = index;
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, selectedCharIndex);
            PlayerPrefs.Save();

            RefreshCharacterCardsUI();
            RefreshCharacterPreviewUI();
            RefreshStageHeaderUI();
        }

        public void ToggleCharacterPreviewForm()
        {
            previewShowingTransform = !previewShowingTransform;
            if (characterCards != null && selectedCharIndex >= 0 && selectedCharIndex < characterCards.Length)
            {
                if (characterCards[selectedCharIndex] != null)
                {
                    characterCards[selectedCharIndex].showingTransform = previewShowingTransform;
                }
            }

            RefreshCharacterCardsUI();
            RefreshCharacterPreviewUI();
        }

        public void CycleLoadoutSlot(int slotIndex)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return;

            System.Collections.Generic.List<ConsumableItemType> ownedItems = new System.Collections.Generic.List<ConsumableItemType>(LoadoutCycleOrder.Length);
            for (int i = 0; i < LoadoutCycleOrder.Length; i++)
            {
                ConsumableItemType candidate = LoadoutCycleOrder[i];
                if (candidate != ConsumableItemType.None && inventory.GetOwnedCount(candidate) > 0)
                {
                    ownedItems.Add(candidate);
                }
            }

            ConsumableItemType current = inventory.GetEquippedItem(slotIndex);

            if (ownedItems.Count == 0)
            {
                if (current != ConsumableItemType.None)
                {
                    inventory.SetEquippedItem(slotIndex, ConsumableItemType.None);
                }
                ShowCharacterSelectFeedback("Chưa sở hữu vật phẩm! Hãy mua trong Cửa Hàng.");
                RefreshLoadoutSlotsUI();
                return;
            }

            ConsumableItemType nextItem;
            int idx = ownedItems.IndexOf(current);
            if (idx < 0)
            {
                nextItem = ownedItems[0];
            }
            else if (ownedItems.Count == 1)
            {
                nextItem = ConsumableItemType.None;
            }
            else
            {
                nextItem = ownedItems[(idx + 1) % ownedItems.Count];
            }

            inventory.SetEquippedItem(slotIndex, nextItem);
            if (characterSelectFeedbackText != null)
            {
                characterSelectFeedbackText.text = string.Empty;
                feedbackTimer = 0f;
            }
            RefreshLoadoutSlotsUI();
        }

        public void HandleConfirmCharacterClicked()
        {
            selectedCharIndex = Mathf.Clamp(selectedCharIndex, 0, 2);
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, selectedCharIndex);
            PlayerPrefs.Save();

            if (StageManager.CurrentGameMode == GameMode.Campaign)
            {
                ShowScreen(MainMenuScreen.WorldSelect);
            }
            else
            {
                StageManager.SetSelectedGameMode(StageManager.CurrentGameMode);
                LaunchBattleScene();
            }
        }

        public void SelectWorld(int index)
        {
            if (index < 0 || index >= 3) return;
            selectedWorldIndex = index;
            PlayerPrefs.SetInt(SELECTED_WORLD_KEY, selectedWorldIndex);
            PlayerPrefs.Save();
            RefreshWorldCardsUI();
            RefreshStageHeaderUI();
        }

        public void LaunchStage(int stageOffset)
        {
            int clampedStage = Mathf.Clamp(stageOffset, 0, 3);
            int globalStageIndex = selectedWorldIndex * 4 + clampedStage;

            StageManager.SetSelectedGameMode(GameMode.Campaign);
            PlayerPrefs.SetInt(SELECTED_CHAR_KEY, selectedCharIndex);
            PlayerPrefs.SetInt(SELECTED_WORLD_KEY, selectedWorldIndex);
            PlayerPrefs.SetInt(SELECTED_STAGE_INDEX_KEY, globalStageIndex);
            PlayerPrefs.Save();

            LaunchBattleScene();
        }

        private void LaunchBattleScene()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(BATTLE_SCENE_NAME))
            {
                SceneManager.LoadScene(BATTLE_SCENE_NAME);
            }
            else if (SceneManager.sceneCountInBuildSettings > 1)
            {
                SceneManager.LoadScene(1);
            }
        }

        private void HandleExitClicked()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private bool IsCharacterSlotLocked(int index)
        {
            if (index < 0 || index >= 3) return true;
            if (characterCards != null && index < characterCards.Length && characterCards[index] != null)
            {
                return characterCards[index].isLocked;
            }
            return false;
        }

        private void ShowCharacterSelectFeedback(string message)
        {
            if (characterSelectFeedbackText == null) return;
            characterSelectFeedbackText.text = message;
            characterSelectFeedbackText.color = new Color(1f, 0.72f, 0.28f, 1f);
            feedbackTimer = 2.4f;
        }

        private void RefreshCharacterCardsUI()
        {
            if (characterCards == null) return;
            for (int i = 0; i < characterCards.Length; i++)
            {
                MenuCharacterCardUI card = characterCards[i];
                if (card == null) continue;

                bool locked = IsCharacterSlotLocked(i);
                bool isSelected = !locked && i == selectedCharIndex;

                if (card.selectButton != null)
                {
                    card.selectButton.interactable = !locked;
                }

                if (card.lockOverlay != null)
                {
                    card.lockOverlay.SetActive(locked);
                }

                if (card.lockedLabelText != null)
                {
                    card.lockedLabelText.text = locked ? "Đang phát triển" : string.Empty;
                    card.lockedLabelText.gameObject.SetActive(locked);
                }

                if (card.portraitBackdrop != null)
                {
                    card.portraitBackdrop.SetActive(!locked);
                }

                if (card.portraitImage != null)
                {
                    if (locked)
                    {
                        card.portraitImage.enabled = false;
                        card.portraitImage.gameObject.SetActive(false);
                    }
                    else
                    {
                        card.portraitImage.gameObject.SetActive(true);
                        card.portraitImage.enabled = true;
                        Sprite targetSprite = (card.showingTransform && card.transformSprite != null)
                            ? card.transformSprite
                            : card.baseSprite;
                        if (targetSprite != null) card.portraitImage.sprite = targetSprite;
                        card.portraitImage.color = Color.white;
                        card.portraitImage.preserveAspect = true;
                        card.portraitImage.rectTransform.localScale = Vector3.one;
                    }
                }

                if (card.highlightBorder != null)
                {
                    card.highlightBorder.color = locked
                        ? LockedBorderColor
                        : (isSelected ? SelectedHighlightColor : IdleBorderColor);
                }

                if (card.characterNameText != null)
                {
                    if (locked)
                    {
                        card.characterNameText.text = string.Empty;
                        card.characterNameText.gameObject.SetActive(false);
                    }
                    else
                    {
                        card.characterNameText.gameObject.SetActive(true);
                        card.characterNameText.text = !string.IsNullOrEmpty(card.characterName)
                            ? card.characterName
                            : DefaultCharacterNames[Mathf.Clamp(i, 0, DefaultCharacterNames.Length - 1)];
                    }
                }

                if (card.formBadgeText != null)
                {
                    if (locked)
                    {
                        card.formBadgeText.text = string.Empty;
                        card.formBadgeText.gameObject.SetActive(false);
                    }
                    else
                    {
                        card.formBadgeText.gameObject.SetActive(true);
                        card.formBadgeText.text = card.showingTransform ? "DẠNG BIẾN HÌNH" : "DẠNG THƯỜNG";
                        card.formBadgeText.color = card.showingTransform
                            ? new Color(1f, 0.85f, 0.25f, 1f)
                            : new Color(0.65f, 0.85f, 1f, 1f);
                    }
                }

                if (card.toggleButtonText != null)
                {
                    card.toggleButtonText.text = card.showingTransform
                        ? "DẠNG THƯỜNG"
                        : "XEM BIẾN HÌNH";
                }

                if (card.skillsDescText != null)
                {
                    card.skillsDescText.text = string.Empty;
                    card.skillsDescText.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshCharacterPreviewUI()
        {
            int idx = Mathf.Clamp(selectedCharIndex, 0, 2);

            if (previewFormToggleText != null)
            {
                previewFormToggleText.text = previewShowingTransform ? "XEM DẠNG THƯỜNG" : "XEM BIẾN HÌNH";
            }

            if (bgCharacterPreviewImage != null)
            {
                Sprite chosen = null;
                if (previewShowingTransform)
                {
                    if (charTransFullSprites != null && idx < charTransFullSprites.Length)
                    {
                        chosen = charTransFullSprites[idx];
                    }
                    if (chosen == null && characterCards != null && idx < characterCards.Length && characterCards[idx] != null)
                    {
                        chosen = characterCards[idx].transformSprite;
                    }
                }
                else
                {
                    if (charNormalFullSprites != null && idx < charNormalFullSprites.Length)
                    {
                        chosen = charNormalFullSprites[idx];
                    }
                    if (chosen == null && characterCards != null && idx < characterCards.Length && characterCards[idx] != null)
                    {
                        chosen = characterCards[idx].baseSprite;
                    }
                }

                if (chosen != null)
                {
                    bgCharacterPreviewImage.sprite = chosen;
                    bgCharacterPreviewImage.enabled = true;
                }
                else
                {
                    bgCharacterPreviewImage.enabled = false;
                }
                bgCharacterPreviewImage.preserveAspect = true;
                bgCharacterPreviewImage.rectTransform.localScale = Vector3.one;
                bgCharacterPreviewImage.raycastTarget = false;
                bgCharacterPreviewImage.color = new Color(1f, 1f, 1f, FULL_PREVIEW_ALPHA);
            }

            MenuCharacterCardUI activeCard = (characterCards != null && idx < characterCards.Length)
                ? characterCards[idx]
                : null;

            if (selectedCharacterNameText != null)
            {
                string baseName = (activeCard != null && !string.IsNullOrEmpty(activeCard.characterName))
                    ? activeCard.characterName
                    : DefaultCharacterNames[idx];
                string formTag = previewShowingTransform ? "DẠNG BIẾN HÌNH" : "DẠNG THƯỜNG";
                selectedCharacterNameText.text = $"{baseName}  —  {formTag}";
            }

            if (selectedCharacterSkillsText != null)
            {
                selectedCharacterSkillsText.text = string.Empty;
                selectedCharacterSkillsText.gameObject.SetActive(false);
            }
        }

        private void RefreshLoadoutSlotsUI()
        {
            InventoryManager inventory = (Application.isPlaying || InventoryManager.HasInstance)
                ? InventoryManager.Instance
                : null;
            RefreshSingleLoadoutSlot(InventoryManager.SLOT_Q, loadoutQIcon, loadoutQCountText, loadoutQNameText, inventory);
            RefreshSingleLoadoutSlot(InventoryManager.SLOT_E, loadoutEIcon, loadoutECountText, loadoutENameText, inventory);
        }

        private void RefreshSingleLoadoutSlot(
            int slotIndex,
            Image iconImg,
            TMP_Text countTxt,
            TMP_Text nameTxt,
            InventoryManager inventory)
        {
            ConsumableItemType itemType = inventory != null ? inventory.GetEquippedItem(slotIndex) : ConsumableItemType.None;
            ConsumableItemSO data = inventory != null ? inventory.GetItemData(itemType) : null;
            int owned = inventory != null ? inventory.GetOwnedCount(itemType) : 0;

            if (nameTxt != null)
            {
                if (itemType == ConsumableItemType.None)
                {
                    nameTxt.text = "Trống";
                }
                else
                {
                    string itemName = data != null && !string.IsNullOrEmpty(data.itemName)
                        ? data.itemName
                        : itemType.ToString();
                    nameTxt.text = itemName;
                }
            }

            if (countTxt != null)
            {
                countTxt.text = $"x{owned}";
                countTxt.color = (itemType != ConsumableItemType.None && owned > 0)
                    ? new Color(0.95f, 0.88f, 0.45f, 1f)
                    : new Color(0.95f, 0.45f, 0.45f, 0.9f);
            }

            if (iconImg != null)
            {
                bool isEmpty = itemType == ConsumableItemType.None || owned <= 0;
                if (itemType != ConsumableItemType.None && data != null && data.icon != null)
                {
                    iconImg.sprite = data.icon;
                    iconImg.enabled = true;
                    iconImg.preserveAspect = true;
                    iconImg.color = isEmpty ? EmptyIconDimColor : Color.white;
                }
                else
                {
                    iconImg.enabled = false;
                }
            }
        }

        private void RefreshWorldCardsUI()
        {
            if (worldCards == null) return;
            for (int i = 0; i < worldCards.Length; i++)
            {
                MenuWorldCardUI wCard = worldCards[i];
                if (wCard == null) continue;

                bool isSelected = i == selectedWorldIndex;
                if (wCard.previewImage != null && wCard.worldBackgroundSprite != null)
                {
                    wCard.previewImage.sprite = wCard.worldBackgroundSprite;
                }
                if (wCard.highlightBorder != null)
                {
                    wCard.highlightBorder.color = isSelected ? SelectedHighlightColor : IdleBorderColor;
                }
                if (wCard.selectedBadge != null)
                {
                    wCard.selectedBadge.SetActive(isSelected);
                }
            }
        }

        private void RefreshStageHeaderUI()
        {
            if (stageSelectionHeaderText == null) return;

            string charLabel = selectedCharIndex switch
            {
                0 => "GOKU",
                1 => "EREN",
                2 => "DIEND",
                _ => "GOKU"
            };
            string worldLabel = selectedWorldIndex switch
            {
                0 => "THẾ GIỚI 1: ATTACK ON TITAN",
                1 => "THẾ GIỚI 2: DRAGON BALL",
                2 => "THẾ GIỚI 3: KAMEN RIDER DIEND",
                _ => "THẾ GIỚI 1"
            };
            stageSelectionHeaderText.text = $"{charLabel}  |  {worldLabel}  —  CHỌN MÀN CHƠI";
        }
    }
}
