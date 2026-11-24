using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using StickmanBeatEmUp.Auth;
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
        Shop = 6,
        Auth = 7
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
        public GameObject goldSelectionFrame;
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
            ConsumableItemType.UltraDivineWater,
            ConsumableItemType.RageElixir
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
        [SerializeField] private GameObject authPanel;

        [Header("Screen Auth (04_AuthRegisterLogin)")]
        [SerializeField] private AuthUIController authUIController;
        [SerializeField] private Button accountPillButton;
        [SerializeField] private Image accountStatusIcon;
        [SerializeField] private TMP_Text accountStatusText;
        [SerializeField] private Button accountActionButton;
        [SerializeField] private TMP_Text accountActionLabel;
        [SerializeField] private Button accountLogoutButton;

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

        [Header("Glassmorphism Sprites (Optional)")]
        [SerializeField] private Sprite pillOrangeGlowSprite;
        [SerializeField] private Sprite pillSubtleGlassSprite;
        [SerializeField] private Sprite currencyBadgeSprite;
        [SerializeField] private Sprite modeCampaignCardSprite;
        [SerializeField] private Sprite modeEndlessCardSprite;
        [SerializeField] private Sprite modeTrainingCardSprite;
        [SerializeField] private Sprite modeShopCardSprite;
        [SerializeField] private Sprite charSlotGlassSprite;
        [SerializeField] private Sprite charSlotGoldSelectedSprite;
        [SerializeField] private Sprite energyOrbSlotSprite;
        [SerializeField] private Sprite techCutActionButtonSprite;

        [Header("SlicedUI Sprites (01, 02, 03, 05)")]
        [SerializeField] private Sprite mainMenuPlaySprite;
        [SerializeField] private Sprite mainMenuGuideSprite;
        [SerializeField] private Sprite mainMenuQuitSprite;
        [SerializeField] private Sprite modeSelectBackSprite;
        [SerializeField] private Sprite modeSelectCoinBadgeSprite;
        [SerializeField] private Sprite coinXuIconSprite;
        [SerializeField] private Sprite modeCampaignIconSprite;
        [SerializeField] private Sprite modeEndlessIconSprite;
        [SerializeField] private Sprite modeTrainingIconSprite;
        [SerializeField] private Sprite modeShopIconSprite;
        [SerializeField] private Sprite charSelectMainPanelSprite;
        [SerializeField] private Sprite charSelectSelectedGoldSprite;
        [SerializeField] private Sprite charSelectUnselectedSprite;
        [SerializeField] private Sprite charSelectComingSoonSprite;
        [SerializeField] private Sprite itemLoadoutPanelSprite;
        [SerializeField] private Sprite itemLoadoutQSlotSprite;
        [SerializeField] private Sprite itemLoadoutESlotSprite;
        [SerializeField] private Sprite charSelectContinueBtnSprite;
        [SerializeField] private Sprite charSelectBackBtnSprite;
        [SerializeField] private Sprite charSelectResetIconSprite;
        [SerializeField] private Sprite charSelectTopRightBadgeSprite;
        [SerializeField] private Sprite shopBackButtonSprite;
        [SerializeField] private Sprite shopCoinPillSprite;

        [Header("SlicedUI Sprites (04_AuthRegisterLogin)")]
        [SerializeField] private Sprite authModalPanelSprite;
        [SerializeField] private Sprite authTabLoginInactiveSprite;
        [SerializeField] private Sprite authTabRegisterActiveGoldSprite;
        [SerializeField] private Sprite authInputFieldBlankSprite;
        [SerializeField] private Sprite authUserIconSprite;
        [SerializeField] private Sprite authLockIconSprite;
        [SerializeField] private Sprite authPrimaryBtnSprite;
        [SerializeField] private Sprite authSecondaryBtnSprite;
        [SerializeField] private Sprite authInputUsernameCleanBackup;
        [SerializeField] private Sprite authInputPasswordCleanBackup;

        private MainMenuScreen currentScreen = MainMenuScreen.MainMenu;
        private int selectedCharIndex;
        private int selectedWorldIndex;
        private bool previewShowingTransform;
        private float feedbackTimer;
        private TMP_FontAsset runtimeTmpFont;
        private bool ownsRuntimeTmpFont;
        private Texture2D runtimeCircleTexture;
        private Sprite runtimeCircleSprite;
        private bool isInventorySubscribed;
        private bool isAccountSubscribed;
        private bool authAutoAdvanceAfterAuth;

        private static readonly Dictionary<string, Sprite> cachedRuntimeSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<UnityEngine.Object> createdRuntimeSpriteAssets = new List<UnityEngine.Object>();
        private static Texture2D runtimeGoldSelectionTexture;
        private static Sprite runtimeGoldSelectionSprite;
        private static Texture2D runtimeSwapFormTexture;
        private static Sprite runtimeSwapFormSprite;

        private static readonly Vector4 PillOrangeSlice = new Vector4(68f, 0f, 68f, 0f);
        private static readonly Vector4 PillSubtleSlice = new Vector4(62f, 0f, 62f, 0f);
        private static readonly Vector4 CurrencyBadgeSlice = new Vector4(46f, 0f, 46f, 0f);
        private static readonly Vector4 ModeCardSlice = new Vector4(36f, 44f, 36f, 36f);
        private static readonly Vector4 CharSlotSlice = new Vector4(28f, 28f, 28f, 28f);
        private static readonly Vector4 TechCutSlice = new Vector4(36f, 28f, 36f, 28f);

        private static readonly Color SelectedHighlightColor = new Color(1f, 0.85f, 0.25f, 1f);
        private static readonly Color IdleBorderColor = new Color(0.28f, 0.34f, 0.45f, 0.75f);
        private static readonly Color LockedBorderColor = new Color(0.22f, 0.22f, 0.26f, 0.65f);
        private static readonly Color EmptyIconDimColor = new Color(0.45f, 0.45f, 0.45f, 0.42f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticSpriteCache()
        {
            for (int i = 0; i < createdRuntimeSpriteAssets.Count; i++)
            {
                if (createdRuntimeSpriteAssets[i] != null)
                {
                    DestroyImmediate(createdRuntimeSpriteAssets[i]);
                }
            }
            createdRuntimeSpriteAssets.Clear();
            cachedRuntimeSprites.Clear();

            if (runtimeGoldSelectionSprite != null)
            {
                DestroyImmediate(runtimeGoldSelectionSprite);
                runtimeGoldSelectionSprite = null;
            }
            if (runtimeGoldSelectionTexture != null)
            {
                DestroyImmediate(runtimeGoldSelectionTexture);
                runtimeGoldSelectionTexture = null;
            }
            if (runtimeSwapFormSprite != null)
            {
                DestroyImmediate(runtimeSwapFormSprite);
                runtimeSwapFormSprite = null;
            }
            if (runtimeSwapFormTexture != null)
            {
                DestroyImmediate(runtimeSwapFormTexture);
                runtimeSwapFormTexture = null;
            }
        }

        public static Sprite GetOrCreateGoldSelectionFrameSprite()
        {
            if (runtimeGoldSelectionSprite != null)
            {
                return runtimeGoldSelectionSprite;
            }

            const int size = 64;
            const float half = (size - 1) * 0.5f;
            const float cornerRadius = 11f;

            runtimeGoldSelectionTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            runtimeGoldSelectionTexture.name = "Runtime_GoldSelectionFrameTex";
            runtimeGoldSelectionTexture.wrapMode = TextureWrapMode.Clamp;
            runtimeGoldSelectionTexture.filterMode = FilterMode.Bilinear;
            runtimeGoldSelectionTexture.hideFlags = HideFlags.DontSave;

            Color[] pixels = new Color[size * size];
            Color royalGold = new Color(1f, 0.85f, 0.16f, 1f);
            Color goldHighlight = new Color(1f, 0.96f, 0.58f, 1f);
            Color glowGold = new Color(1f, 0.78f, 0.10f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x - half) - (half - cornerRadius);
                    float qy = Mathf.Abs(y - half) - (half - cornerRadius);
                    float outsideCorner = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float sdf = outsideCorner + Mathf.Min(Mathf.Max(qx, qy), 0f) - cornerRadius;
                    float distInward = -sdf;

                    Color c = Color.clear;
                    if (distInward > -1.0f && distInward < 4.0f)
                    {
                        float t = Mathf.Clamp01(distInward / 4.0f);
                        c = new Color(glowGold.r, glowGold.g, glowGold.b, t * t * 0.72f);
                    }
                    else if (distInward >= 4.0f && distInward <= 9.0f)
                    {
                        float ridge = 1f - Mathf.Clamp01(Mathf.Abs(distInward - 6.5f) / 2.2f);
                        c = Color.Lerp(royalGold, goldHighlight, ridge * 0.35f);
                        c.a = 1f;
                    }
                    else if (distInward > 9.0f && distInward < 13.5f)
                    {
                        float t = Mathf.Clamp01(1f - (distInward - 9.0f) / 4.5f);
                        c = new Color(royalGold.r, royalGold.g, royalGold.b, t * t * 0.58f);
                    }

                    pixels[y * size + x] = c;
                }
            }

            runtimeGoldSelectionTexture.SetPixels(pixels);
            runtimeGoldSelectionTexture.Apply();

            runtimeGoldSelectionSprite = Sprite.Create(
                runtimeGoldSelectionTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(18f, 18f, 18f, 18f)
            );
            runtimeGoldSelectionSprite.name = "Runtime_GoldSelectionFrameSprite";
            runtimeGoldSelectionSprite.hideFlags = HideFlags.DontSave;
            return runtimeGoldSelectionSprite;
        }

        public static Sprite GetOrCreateSwapFormIconSprite()
        {
            if (runtimeSwapFormSprite != null)
            {
                return runtimeSwapFormSprite;
            }

            const int size = 64;
            const float half = (size - 1) * 0.5f;
            const float arcRadius = 19.5f;
            const float arcHalfWidth = 2.2f;

            runtimeSwapFormTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            runtimeSwapFormTexture.name = "Runtime_SwapFormIconTex";
            runtimeSwapFormTexture.wrapMode = TextureWrapMode.Clamp;
            runtimeSwapFormTexture.filterMode = FilterMode.Bilinear;
            runtimeSwapFormTexture.hideFlags = HideFlags.DontSave;

            Vector2 center = new Vector2(half, half);
            Vector2 topTip, topV1, topV2;
            ComputeArcArrowTriangle(center, arcRadius, 28f * Mathf.Deg2Rad, out topTip, out topV1, out topV2);
            Vector2 botTip, botV1, botV2;
            ComputeArcArrowTriangle(center, arcRadius, 208f * Mathf.Deg2Rad, out botTip, out botV1, out botV2);

            Vector2 topTail = center + new Vector2(Mathf.Cos(155f * Mathf.Deg2Rad), Mathf.Sin(155f * Mathf.Deg2Rad)) * arcRadius;
            Vector2 botTail = center + new Vector2(Mathf.Cos(335f * Mathf.Deg2Rad), Mathf.Sin(335f * Mathf.Deg2Rad)) * arcRadius;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float dx = x - half;
                    float dy = y - half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (ang < 0f) ang += 360f;

                    float sd = 999f;
                    if (ang >= 28f && ang <= 155f)
                    {
                        sd = Mathf.Min(sd, Mathf.Abs(r - arcRadius) - arcHalfWidth);
                    }
                    if (ang >= 208f && ang <= 335f)
                    {
                        sd = Mathf.Min(sd, Mathf.Abs(r - arcRadius) - arcHalfWidth);
                    }

                    sd = Mathf.Min(sd, Vector2.Distance(p, topTail) - arcHalfWidth);
                    sd = Mathf.Min(sd, Vector2.Distance(p, botTail) - arcHalfWidth);
                    sd = Mathf.Min(sd, SignedDistanceToTriangle(p, topTip, topV1, topV2));
                    sd = Mathf.Min(sd, SignedDistanceToTriangle(p, botTip, botV1, botV2));

                    float arcAlpha = Mathf.Clamp01(0.8f - sd);

                    float sx = Mathf.Abs(dx) / 9.2f;
                    float sy = Mathf.Abs(dy) / 9.2f;
                    float starVal = Mathf.Pow(sx, 0.62f) + Mathf.Pow(sy, 0.62f);
                    float starAlpha = Mathf.Clamp01((1.0f - starVal) * 6.0f);

                    float alpha = Mathf.Clamp01(Mathf.Max(arcAlpha, starAlpha));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            runtimeSwapFormTexture.SetPixels(pixels);
            runtimeSwapFormTexture.Apply();

            runtimeSwapFormSprite = Sprite.Create(
                runtimeSwapFormTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f
            );
            runtimeSwapFormSprite.name = "Runtime_SwapFormIconSprite";
            runtimeSwapFormSprite.hideFlags = HideFlags.DontSave;
            return runtimeSwapFormSprite;
        }

        private static void ComputeArcArrowTriangle(
            Vector2 center, float radius, float endRad,
            out Vector2 tip, out Vector2 v1, out Vector2 v2)
        {
            Vector2 normal = new Vector2(Mathf.Cos(endRad), Mathf.Sin(endRad));
            Vector2 cwTangent = new Vector2(Mathf.Sin(endRad), -Mathf.Cos(endRad));
            Vector2 endPoint = center + normal * radius;
            tip = endPoint + cwTangent * 7.2f;
            v1 = endPoint - cwTangent * 1.2f + normal * 5.6f;
            v2 = endPoint - cwTangent * 1.2f - normal * 5.6f;
        }

        private static float SignedDistanceToTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a; Vector2 v0 = p - a;
            Vector2 e1 = c - b; Vector2 v1 = p - b;
            Vector2 e2 = a - c; Vector2 v2 = p - c;

            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Mathf.Max(0.0001f, Vector2.Dot(e0, e0)));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Mathf.Max(0.0001f, Vector2.Dot(e1, e1)));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Mathf.Max(0.0001f, Vector2.Dot(e2, e2)));

            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float d0 = Vector2.Dot(pq0, pq0);
            float d1 = Vector2.Dot(pq1, pq1);
            float d2 = Vector2.Dot(pq2, pq2);
            float minD = Mathf.Min(d0, Mathf.Min(d1, d2));

            float c0 = s * (v0.x * e0.y - v0.y * e0.x);
            float c1 = s * (v1.x * e1.y - v1.y * e1.x);
            float c2 = s * (v2.x * e2.y - v2.y * e2.x);
            bool inside = Mathf.Min(c0, Mathf.Min(c1, c2)) >= 0f;

            return inside ? -Mathf.Sqrt(minD) : Mathf.Sqrt(minD);
        }

        public static GameObject EnsureGoldSelectionFrame(
            Transform cardRoot, GameObject existingFrame, bool isSelected, bool applyRuntimeSprite)
        {
            if (cardRoot == null) return existingFrame;

            GameObject frameObj = existingFrame;
            if (frameObj == null)
            {
                Transform found = cardRoot.Find("GoldSelectionFrame");
                if (found != null)
                {
                    frameObj = found.gameObject;
                }
            }

            if (frameObj == null)
            {
                frameObj = new GameObject("GoldSelectionFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
                frameObj.transform.SetParent(cardRoot, false);
            }

            RectTransform rt = frameObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(-4f, -4f);
                rt.offsetMax = new Vector2(4f, 4f);
                rt.localScale = Vector3.one;
            }

            Image img;
            if (!frameObj.TryGetComponent(out img))
            {
                img = frameObj.AddComponent<Image>();
            }
            img.raycastTarget = false;
            img.type = Image.Type.Sliced;
            if (applyRuntimeSprite)
            {
                img.sprite = GetOrCreateGoldSelectionFrameSprite();
                img.color = Color.white;
            }
            else if (img.sprite == null)
            {
                img.color = Color.clear;
            }

            Outline outline;
            if (!frameObj.TryGetComponent(out outline))
            {
                outline = frameObj.AddComponent<Outline>();
            }
            outline.effectColor = new Color(1f, 0.72f, 0.08f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);

            frameObj.transform.SetAsLastSibling();
            frameObj.SetActive(isSelected);
            return frameObj;
        }

        public static void EnsureSwapFormButtonVisual(
            Button toggleBtn, TMP_Text toggleText, bool applyRuntimeSprite, Color iconTint)
        {
            if (toggleText != null)
            {
                toggleText.text = string.Empty;
                toggleText.gameObject.SetActive(false);
            }

            if (toggleBtn == null) return;

            Text[] legacyTexts = toggleBtn.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacyTexts.Length; i++)
            {
                if (legacyTexts[i] != null)
                {
                    legacyTexts[i].text = string.Empty;
                    legacyTexts[i].gameObject.SetActive(false);
                }
            }

            TMP_Text[] tmpTexts = toggleBtn.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                if (tmpTexts[i] != null)
                {
                    tmpTexts[i].text = string.Empty;
                    tmpTexts[i].gameObject.SetActive(false);
                }
            }

            RectTransform btnRt = toggleBtn.GetComponent<RectTransform>();
            if (btnRt != null && btnRt.sizeDelta.x > 120f)
            {
                btnRt.sizeDelta = new Vector2(88f, btnRt.sizeDelta.y > 0f ? btnRt.sizeDelta.y : 80f);
            }

            Image btnImg = toggleBtn.GetComponent<Image>();
            bool hasDedicatedResetIcon = btnImg != null && btnImg.sprite != null &&
                string.Equals(btnImg.sprite.name, "Btn_CharSelect_ResetIcon", StringComparison.OrdinalIgnoreCase);

            Transform iconTf = toggleBtn.transform.Find("SwapFormIcon");
            if (hasDedicatedResetIcon)
            {
                btnImg.type = Image.Type.Simple;
                btnImg.preserveAspect = true;
                btnImg.color = iconTint;
                if (iconTf != null)
                {
                    iconTf.gameObject.SetActive(false);
                }
                return;
            }

            GameObject iconObj = iconTf != null
                ? iconTf.gameObject
                : new GameObject("SwapFormIcon", typeof(RectTransform), typeof(Image));
            if (iconTf == null)
            {
                iconObj.transform.SetParent(toggleBtn.transform, false);
            }
            iconObj.SetActive(true);

            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            if (iconRt != null)
            {
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.offsetMin = new Vector2(14f, 10f);
                iconRt.offsetMax = new Vector2(-14f, -10f);
                iconRt.localScale = Vector3.one;
            }

            Image iconImg;
            if (!iconObj.TryGetComponent(out iconImg))
            {
                iconImg = iconObj.AddComponent<Image>();
            }
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            if (applyRuntimeSprite)
            {
                iconImg.sprite = GetOrCreateSwapFormIconSprite();
                iconImg.color = iconTint;
                iconImg.enabled = true;
            }
            else if (iconImg.sprite == null)
            {
                iconImg.color = Color.clear;
            }
        }

        public static Sprite LoadGlassSprite(string fileName)
        {
            return GlassHudTheme.LoadSprite(fileName);
        }

        public static Sprite LoadGlassSprite(string fileName, Vector4 sliceBorder)
        {
            return GlassHudTheme.LoadSprite(fileName, sliceBorder);
        }

        public static Sprite LoadItemIconSprite(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            string normalized = NormalizePngFileName(fileName);
            string key = $"icon|{normalized}";
            if (cachedRuntimeSprites.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            string[] relativeAssetPaths =
            {
                $"Sprites/UI/Items/{normalized}",
                $"Sprites/UI/{normalized}"
            };

            Sprite resolved = LoadSpriteFromPaths(relativeAssetPaths, Vector4.zero, isItemIcon: true);
            if (resolved != null)
            {
                cachedRuntimeSprites[key] = resolved;
            }
            return resolved;
        }

        public static void ApplyGlassImageStyle(Image target, Sprite sprite, bool sliced, bool preserveAspect = false)
        {
            if (target == null || sprite == null) return;
            if (sliced)
            {
                RectTransform rt = target.rectTransform;
                float effW = rt != null ? (rt.rect.width > 1f ? rt.rect.width : rt.sizeDelta.x) : 0f;
                float effH = rt != null ? (rt.rect.height > 1f ? rt.rect.height : rt.sizeDelta.y) : 0f;
                GlassHudTheme.ApplySlicedSprite(target, sprite, effW, effH);
            }
            else
            {
                GlassHudTheme.ApplySprite(target, sprite, sliced: false, preserveAspect: preserveAspect);
            }
        }

        private static string NormalizePngFileName(string fileName)
        {
            string trimmed = fileName.Trim().Replace('\\', '/');
            string[] prefixes =
            {
                "Assets/Sprites/UI/GlassHUD/",
                "Assets/Sprites/UI/Glassmorphism/",
                "Assets/Sprites/UI/Glass/",
                "Assets/Sprites/UI/Items/",
                "Assets/Sprites/UI/",
                "GlassHUD/",
                "Glassmorphism/",
                "Glass/"
            };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (trimmed.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(prefixes[i].Length);
                    break;
                }
            }

            if (!Path.HasExtension(trimmed))
            {
                trimmed += ".png";
            }
            return trimmed;
        }

        private static Sprite LoadSpriteFromPaths(string[] relativePaths, Vector4 sliceBorder, bool isItemIcon)
        {
            for (int i = 0; i < relativePaths.Length; i++)
            {
                string relPath = relativePaths[i];
                string assetPath = "Assets/" + relPath;

#if UNITY_EDITOR
                Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (editorSprite != null)
                {
                    if (sliceBorder == Vector4.zero || (editorSprite.border - sliceBorder).sqrMagnitude <= 0.25f)
                    {
                        return editorSprite;
                    }
                    if (editorSprite.texture != null)
                    {
                        Sprite slicedWrapper = Sprite.Create(
                            editorSprite.texture,
                            editorSprite.rect,
                            new Vector2(0.5f, 0.5f),
                            editorSprite.pixelsPerUnit > 0f ? editorSprite.pixelsPerUnit : 100f,
                            0,
                            SpriteMeshType.FullRect,
                            sliceBorder
                        );
                        slicedWrapper.name = editorSprite.name;
                        slicedWrapper.hideFlags = HideFlags.DontSave;
                        createdRuntimeSpriteAssets.Add(slicedWrapper);
                        return slicedWrapper;
                    }
                    return editorSprite;
                }
#endif

                string noExt = Path.ChangeExtension(Path.GetFileName(relPath), null);
                Sprite resSprite = isItemIcon
                    ? (Resources.Load<Sprite>("Items/" + noExt) ?? Resources.Load<Sprite>("UI/Items/" + noExt))
                    : (Resources.Load<Sprite>("UI/GlassHUD/" + noExt)
                        ?? Resources.Load<Sprite>("UI/Glassmorphism/" + noExt)
                        ?? Resources.Load<Sprite>("UI/" + noExt)
                        ?? Resources.Load<Sprite>(noExt));
                if (resSprite != null)
                {
                    if (sliceBorder == Vector4.zero || (resSprite.border - sliceBorder).sqrMagnitude <= 0.25f)
                    {
                        return resSprite;
                    }
                    if (resSprite.texture != null)
                    {
                        Sprite slicedRes = Sprite.Create(
                            resSprite.texture,
                            resSprite.rect,
                            new Vector2(0.5f, 0.5f),
                            resSprite.pixelsPerUnit > 0f ? resSprite.pixelsPerUnit : 100f,
                            0,
                            SpriteMeshType.FullRect,
                            sliceBorder
                        );
                        slicedRes.name = resSprite.name;
                        slicedRes.hideFlags = HideFlags.DontSave;
                        createdRuntimeSpriteAssets.Add(slicedRes);
                        return slicedRes;
                    }
                    return resSprite;
                }

                string diskPath = Path.Combine(Application.dataPath, relPath.Replace('/', Path.DirectorySeparatorChar));
                Sprite diskSprite = TryCreateSpriteFromDisk(diskPath, sliceBorder);
                if (diskSprite != null)
                {
                    return diskSprite;
                }
            }

            return null;
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
                    DestroyImmediate(tex);
                    return null;
                }

                tex.hideFlags = HideFlags.DontSave;
                Sprite created = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    sliceBorder
                );
                created.name = Path.GetFileNameWithoutExtension(diskPath);
                created.hideFlags = HideFlags.DontSave;
                createdRuntimeSpriteAssets.Add(tex);
                createdRuntimeSpriteAssets.Add(created);
                return created;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                EnsureAuthUIInitialized();
            }
            ApplyVietnameseFont();
            BindButtons();
            if (Application.isPlaying)
            {
                try
                {
                    ApplyGlassmorphismTheme();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UIManager] Theme awake warning: {ex.Message}");
                }
            }
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
                TMP_FontAsset sharedTmp = GlassHudTheme.ResolveSharedTmpFont(this);
                if (sharedTmp != null &&
                    !string.Equals(sharedTmp.name, "LiberationSans SDF", StringComparison.OrdinalIgnoreCase))
                {
                    runtimeTmpFont = sharedTmp;
                    ownsRuntimeTmpFont = false;
                }
                else
                {
                    runtimeTmpFont = TMP_FontAsset.CreateFontAsset(resolvedFont);
                    ownsRuntimeTmpFont = runtimeTmpFont != null;
                }
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

                TMP_InputField[] tmpInputs = GetComponentsInChildren<TMP_InputField>(true);
                for (int i = 0; i < tmpInputs.Length; i++)
                {
                    TMP_InputField field = tmpInputs[i];
                    if (field == null) continue;
                    field.fontAsset = runtimeTmpFont;
                    if (field.textComponent != null)
                    {
                        field.textComponent.font = runtimeTmpFont;
                    }
                    if (field.placeholder is TMP_Text placeholderTmp)
                    {
                        placeholderTmp.font = runtimeTmpFont;
                    }
                }

                if (authUIController != null)
                {
                    authUIController.ApplyFont(runtimeTmpFont);
                }
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                _ = AccountManager.Instance;
                EnsureAuthUIInitialized();
            }
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
            if (IsCharacterSlotLocked(selectedCharIndex))
            {
                selectedCharIndex = 0;
            }
            previewShowingTransform = false;
            selectedWorldIndex = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_WORLD_KEY, 0), 0, 2);

            if (characterSelectFeedbackText != null)
            {
                characterSelectFeedbackText.text = string.Empty;
            }

            SubscribeInventoryEvents();
            SubscribeAccountEvents();
            BindButtons();
            if (Application.isPlaying)
            {
                try
                {
                    ApplyGlassmorphismTheme();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UIManager] Theme start warning: {ex.Message}");
                }
            }
            ApplySmoothCircleLoadoutSprites();
            RefreshCharacterCardsUI();
            RefreshCharacterPreviewUI();
            RefreshLoadoutSlotsUI();
            RefreshWorldCardsUI();
            RefreshStageHeaderUI();
            RefreshAccountStatusPill();
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
            if (Application.isPlaying)
            {
                SubscribeAccountEvents();
            }
        }

        private void OnDisable()
        {
            UnsubscribeInventoryEvents();
            UnsubscribeAccountEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeInventoryEvents();
            UnsubscribeAccountEvents();
            if (runtimeTmpFont != null)
            {
                if (ownsRuntimeTmpFont)
                {
                    Destroy(runtimeTmpFont);
                }
                runtimeTmpFont = null;
                ownsRuntimeTmpFont = false;
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

            itemLoadoutQSlotSprite = ResolveSlicedSprite(itemLoadoutQSlotSprite, "03_CharacterAndItemSelect/Slot_ItemLoadout_Q_CyanRing_Clean", "Slot_ItemLoadout_Q_CyanRing_Clean");
            itemLoadoutESlotSprite = ResolveSlicedSprite(itemLoadoutESlotSprite, "03_CharacterAndItemSelect/Slot_ItemLoadout_E_VioletRing_Clean", "Slot_ItemLoadout_E_VioletRing_Clean");

            if (energyOrbSlotSprite == null)
            {
                energyOrbSlotSprite = LoadGlassSprite("EnergyOrb_Slot.png", Vector4.zero);
            }

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
            if (slotButton == null) return;

            Sprite preferredRing = null;
            if (slotButton == loadoutQButton) preferredRing = itemLoadoutQSlotSprite;
            else if (slotButton == loadoutEButton) preferredRing = itemLoadoutESlotSprite;

            if (slotButton.TryGetComponent(out RectTransform slotRt))
            {
                slotRt.anchoredPosition = new Vector2(0f, 14f);
                slotRt.sizeDelta = new Vector2(112f, 112f);
            }

            Image borderImg = slotButton.GetComponent<Image>();
            bool hasEnergyOrb = false;

            if (borderImg != null)
            {
                bool isLegacyOrPlain = borderImg.sprite == null
                    || borderImg.sprite == runtimeCircleSprite
                    || string.Equals(borderImg.sprite.name, "Circle", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(borderImg.sprite.name, "EnergyOrb_Slot", StringComparison.OrdinalIgnoreCase);

                if (preferredRing != null)
                {
                    borderImg.sprite = preferredRing;
                    borderImg.type = Image.Type.Simple;
                    borderImg.preserveAspect = true;
                    borderImg.color = Color.white;
                    hasEnergyOrb = true;
                }
                else if (energyOrbSlotSprite != null && isLegacyOrPlain)
                {
                    ApplyGlassImageStyle(borderImg, energyOrbSlotSprite, sliced: false, preserveAspect: true);
                    hasEnergyOrb = true;
                }
                else if (borderImg.sprite != null && !isLegacyOrPlain)
                {
                    borderImg.type = Image.Type.Simple;
                    borderImg.preserveAspect = true;
                    borderImg.color = Color.white;
                    hasEnergyOrb = true;
                }
                else if (borderImg.sprite == null && runtimeCircleSprite != null)
                {
                    borderImg.sprite = runtimeCircleSprite;
                }
            }

            Transform maskTf = slotButton.transform.Find("CircleMask");
            if (maskTf != null)
            {
                if (maskTf.TryGetComponent(out RectTransform maskRt))
                {
                    maskRt.anchorMin = Vector2.zero;
                    maskRt.anchorMax = Vector2.one;
                    maskRt.offsetMin = new Vector2(16f, 16f);
                    maskRt.offsetMax = new Vector2(-16f, -16f);
                }

                Image maskImg = maskTf.GetComponent<Image>();
                Mask mask = maskTf.GetComponent<Mask>();

                if (maskImg != null && (maskImg.sprite == null || string.Equals(maskImg.sprite.name, "Circle", StringComparison.OrdinalIgnoreCase)))
                {
                    if (runtimeCircleSprite != null)
                    {
                        maskImg.sprite = runtimeCircleSprite;
                    }
                }

                if (hasEnergyOrb)
                {
                    if (maskImg != null)
                    {
                        maskImg.color = Color.white;
                    }
                    if (mask != null)
                    {
                        mask.showMaskGraphic = false;
                    }
                }
                else if (maskImg != null && maskImg.sprite == null && runtimeCircleSprite != null)
                {
                    maskImg.sprite = runtimeCircleSprite;
                }

                Transform itemIconTf = maskTf.Find("ItemIcon");
                if (itemIconTf != null && itemIconTf.TryGetComponent(out RectTransform itemIconRt))
                {
                    itemIconRt.anchorMin = Vector2.zero;
                    itemIconRt.anchorMax = Vector2.one;
                    itemIconRt.offsetMin = new Vector2(6f, 6f);
                    itemIconRt.offsetMax = new Vector2(-6f, -6f);
                }
            }

            Transform keyBadgeTf = slotButton.transform.Find("KeyBadge");
            if (keyBadgeTf != null)
            {
                if (keyBadgeTf.TryGetComponent(out RectTransform keyRt))
                {
                    keyRt.anchorMin = new Vector2(0f, 1f);
                    keyRt.anchorMax = new Vector2(0f, 1f);
                    keyRt.pivot = new Vector2(0.5f, 0.5f);
                    keyRt.anchoredPosition = new Vector2(8f, -8f);
                    keyRt.sizeDelta = new Vector2(34f, 34f);
                }
                Image keyImg = keyBadgeTf.GetComponent<Image>();
                if (keyImg != null)
                {
                    keyImg.sprite = null;
                    keyImg.color = Color.clear;
                }
                TMP_Text keyTxt = keyBadgeTf.GetComponentInChildren<TMP_Text>(true);
                if (keyTxt != null)
                {
                    GlassHudTheme.StyleTmpText(keyTxt, 26f, new Color(1f, 0.80f, 0.22f, 1f), bold: true, addShadow: true);
                }
            }

            Transform countBadgeTf = slotButton.transform.Find("CountBadge");
            if (countBadgeTf != null)
            {
                if (countBadgeTf.TryGetComponent(out RectTransform countRt))
                {
                    countRt.anchorMin = new Vector2(1f, 0f);
                    countRt.anchorMax = new Vector2(1f, 0f);
                    countRt.pivot = new Vector2(0.5f, 0.5f);
                    countRt.anchoredPosition = new Vector2(-8f, 8f);
                    countRt.sizeDelta = new Vector2(44f, 24f);
                }
                Image countImg = countBadgeTf.GetComponent<Image>();
                if (countImg != null && countImg.sprite == null && runtimeCircleSprite != null)
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
                ApplyGlassmorphismTheme();
                ApplySmoothCircleLoadoutSprites();
                BindButtons();
            }
            RefreshCharacterCardsUI();
            RefreshCharacterPreviewUI();
            RefreshLoadoutSlotsUI();
        }

        public void ConfigureGlassmorphismSprites(
            Sprite pillOrangeGlow,
            Sprite pillSubtleGlass,
            Sprite currencyBadge,
            Sprite modeCampaignCard,
            Sprite modeEndlessCard,
            Sprite modeTrainingCard,
            Sprite modeShopCard,
            Sprite charSlotGlass,
            Sprite charSlotGoldSelected,
            Sprite energyOrbSlot,
            Sprite techCutActionButton)
        {
            if (pillOrangeGlow != null) pillOrangeGlowSprite = pillOrangeGlow;
            if (pillSubtleGlass != null) pillSubtleGlassSprite = pillSubtleGlass;
            if (currencyBadge != null) currencyBadgeSprite = currencyBadge;
            if (modeCampaignCard != null) modeCampaignCardSprite = modeCampaignCard;
            if (modeEndlessCard != null) modeEndlessCardSprite = modeEndlessCard;
            if (modeTrainingCard != null) modeTrainingCardSprite = modeTrainingCard;
            if (modeShopCard != null) modeShopCardSprite = modeShopCard;
            if (charSlotGlass != null) charSlotGlassSprite = charSlotGlass;
            if (charSlotGoldSelected != null) charSlotGoldSelectedSprite = charSlotGoldSelected;
            if (energyOrbSlot != null) energyOrbSlotSprite = energyOrbSlot;
            if (techCutActionButton != null) techCutActionButtonSprite = techCutActionButton;

            ApplyGlassmorphismTheme();
            RefreshCharacterCardsUI();
            RefreshLoadoutSlotsUI();
        }

        public void ConfigureSlicedUISprites(
            Sprite playMain,
            Sprite guideMain,
            Sprite quitMain,
            Sprite modeBack,
            Sprite modeCoinBadge,
            Sprite coinXuIcon,
            Sprite modeCampaignCard,
            Sprite modeEndlessCard,
            Sprite modeTrainingCard,
            Sprite modeShopCard,
            Sprite modeCampaignIcon,
            Sprite modeEndlessIcon,
            Sprite modeTrainingIcon,
            Sprite modeShopIcon,
            Sprite charMainPanel,
            Sprite charSelectedGold,
            Sprite charUnselected,
            Sprite charComingSoon,
            Sprite loadoutPanel,
            Sprite loadoutQSlot,
            Sprite loadoutESlot,
            Sprite charContinueBtn,
            Sprite charBackBtn,
            Sprite charResetIcon,
            Sprite charTopRightBadge,
            Sprite shopBackBtn,
            Sprite shopCoinPill)
        {
            if (playMain != null) mainMenuPlaySprite = playMain;
            if (guideMain != null) mainMenuGuideSprite = guideMain;
            if (quitMain != null) mainMenuQuitSprite = quitMain;
            if (modeBack != null) modeSelectBackSprite = modeBack;
            if (modeCoinBadge != null) modeSelectCoinBadgeSprite = modeCoinBadge;
            if (coinXuIcon != null) coinXuIconSprite = coinXuIcon;
            if (modeCampaignCard != null) modeCampaignCardSprite = modeCampaignCard;
            if (modeEndlessCard != null) modeEndlessCardSprite = modeEndlessCard;
            if (modeTrainingCard != null) modeTrainingCardSprite = modeTrainingCard;
            if (modeShopCard != null) modeShopCardSprite = modeShopCard;
            if (modeCampaignIcon != null) modeCampaignIconSprite = modeCampaignIcon;
            if (modeEndlessIcon != null) modeEndlessIconSprite = modeEndlessIcon;
            if (modeTrainingIcon != null) modeTrainingIconSprite = modeTrainingIcon;
            if (modeShopIcon != null) modeShopIconSprite = modeShopIcon;
            if (charMainPanel != null) charSelectMainPanelSprite = charMainPanel;
            if (charSelectedGold != null) charSelectSelectedGoldSprite = charSelectedGold;
            if (charUnselected != null) charSelectUnselectedSprite = charUnselected;
            if (charComingSoon != null) charSelectComingSoonSprite = charComingSoon;
            if (loadoutPanel != null) itemLoadoutPanelSprite = loadoutPanel;
            if (loadoutQSlot != null) itemLoadoutQSlotSprite = loadoutQSlot;
            if (loadoutESlot != null) itemLoadoutESlotSprite = loadoutESlot;
            if (charContinueBtn != null) charSelectContinueBtnSprite = charContinueBtn;
            if (charBackBtn != null) charSelectBackBtnSprite = charBackBtn;
            if (charResetIcon != null) charSelectResetIconSprite = charResetIcon;
            if (charTopRightBadge != null) charSelectTopRightBadgeSprite = charTopRightBadge;
            if (shopBackBtn != null) shopBackButtonSprite = shopBackBtn;
            if (shopCoinPill != null) shopCoinPillSprite = shopCoinPill;

            ApplyGlassmorphismTheme();
            RefreshCharacterCardsUI();
            RefreshLoadoutSlotsUI();
        }

        public void ConfigureAuthUI(
            GameObject pAuth,
            AuthUIController authController,
            Button pillBtn,
            Image pillUserIcon,
            TMP_Text pillStatusText,
            Button pillActionBtn,
            TMP_Text pillActionLabel,
            Button pillLogoutBtn)
        {
            authPanel = pAuth;
            authUIController = authController;
            accountPillButton = pillBtn;
            accountStatusIcon = pillUserIcon;
            accountStatusText = pillStatusText;
            accountActionButton = pillActionBtn;
            accountActionLabel = pillActionLabel;
            accountLogoutButton = pillLogoutBtn;
        }

        public void ConfigureAuthSprites(AuthUIController.AuthThemeSprites sprites)
        {
            if (sprites.modalPanelSprite != null) authModalPanelSprite = sprites.modalPanelSprite;
            if (sprites.tabLoginInactiveSprite != null) authTabLoginInactiveSprite = sprites.tabLoginInactiveSprite;
            if (sprites.tabRegisterActiveGoldSprite != null) authTabRegisterActiveGoldSprite = sprites.tabRegisterActiveGoldSprite;
            if (sprites.inputFieldBlankSprite != null) authInputFieldBlankSprite = sprites.inputFieldBlankSprite;
            if (sprites.userIconSprite != null) authUserIconSprite = sprites.userIconSprite;
            if (sprites.lockIconSprite != null) authLockIconSprite = sprites.lockIconSprite;
            if (sprites.primaryButtonSprite != null) authPrimaryBtnSprite = sprites.primaryButtonSprite;
            if (sprites.secondaryButtonSprite != null) authSecondaryBtnSprite = sprites.secondaryButtonSprite;
            if (sprites.inputUsernameCleanBackup != null) authInputUsernameCleanBackup = sprites.inputUsernameCleanBackup;
            if (sprites.inputPasswordCleanBackup != null) authInputPasswordCleanBackup = sprites.inputPasswordCleanBackup;
            if (sprites.accountPillBgSprite != null && modeSelectCoinBadgeSprite == null)
            {
                modeSelectCoinBadgeSprite = sprites.accountPillBgSprite;
            }
        }

        private AuthUIController.AuthThemeSprites BuildAuthThemeSprites()
        {
            return new AuthUIController.AuthThemeSprites
            {
                modalPanelSprite = authModalPanelSprite,
                tabLoginInactiveSprite = authTabLoginInactiveSprite,
                tabRegisterActiveGoldSprite = authTabRegisterActiveGoldSprite,
                inputFieldBlankSprite = authInputFieldBlankSprite,
                userIconSprite = authUserIconSprite,
                lockIconSprite = authLockIconSprite,
                primaryButtonSprite = authPrimaryBtnSprite,
                secondaryButtonSprite = authSecondaryBtnSprite,
                accountPillBgSprite = modeSelectCoinBadgeSprite ?? currencyBadgeSprite,
                inputUsernameCleanBackup = authInputUsernameCleanBackup,
                inputPasswordCleanBackup = authInputPasswordCleanBackup
            };
        }

        private void EnsureAuthUIInitialized()
        {
            if (!Application.isPlaying) return;

            ResolveGlassSprites();
            AuthUIController.AuthThemeSprites sprites = BuildAuthThemeSprites();

            if (authPanel == null || authUIController == null)
            {
                Transform existingAuth = transform.Find("SubScreen_Auth");
                if (existingAuth != null)
                {
                    authPanel = existingAuth.gameObject;
                    authUIController = existingAuth.GetComponent<AuthUIController>();
                }
            }

            if (authPanel == null || authUIController == null)
            {
                authUIController = AuthUIController.BuildAuthPanel(transform, sprites, runtimeTmpFont);
                authPanel = authUIController.gameObject;
                authPanel.SetActive(false);
            }
            else
            {
                authUIController.ConfigureThemeSprites(sprites);
            }

            if (mainMenuPanel != null && (accountActionButton == null || accountStatusText == null))
            {
                Transform existingPill = mainMenuPanel.transform.Find("AccountStatusPill");
                if (existingPill != null)
                {
                    accountPillButton = existingPill.GetComponent<Button>();
                    Transform iconTf = existingPill.Find("UserIcon");
                    if (iconTf != null) accountStatusIcon = iconTf.GetComponent<Image>();
                    Transform textTf = existingPill.Find("AccountStatusText");
                    if (textTf != null) accountStatusText = textTf.GetComponent<TMP_Text>();
                    Transform actionTf = existingPill.Find("AccountActionButton");
                    if (actionTf != null)
                    {
                        accountActionButton = actionTf.GetComponent<Button>();
                        accountActionLabel = actionTf.GetComponentInChildren<TMP_Text>(true);
                    }
                    Transform logoutTf = existingPill.Find("AccountLogoutButton");
                    if (logoutTf != null) accountLogoutButton = logoutTf.GetComponent<Button>();
                }
                else
                {
                    AuthUIController.AccountStatusPillRefs pillRefs = AuthUIController.BuildAccountStatusPill(
                        mainMenuPanel.transform,
                        sprites.accountPillBgSprite,
                        sprites.userIconSprite,
                        sprites.secondaryButtonSprite,
                        runtimeTmpFont);
                    accountPillButton = pillRefs.pillButton;
                    accountStatusIcon = pillRefs.userIcon;
                    accountStatusText = pillRefs.statusText;
                    accountActionButton = pillRefs.actionButton;
                    accountActionLabel = pillRefs.actionLabel;
                    accountLogoutButton = pillRefs.logoutButton;
                }
            }
        }

        private void SubscribeAccountEvents()
        {
            if (isAccountSubscribed) return;

            AccountManager mgr = AccountManager.Instance;
            if (mgr == null) return;

            mgr.OnActiveAccountChanged -= HandleActiveAccountChanged;
            mgr.OnActiveAccountChanged += HandleActiveAccountChanged;
            isAccountSubscribed = true;
        }

        private void UnsubscribeAccountEvents()
        {
            if (!isAccountSubscribed) return;

            AccountManager mgr = AccountManager.HasInstance ? AccountManager.Instance : null;
            if (mgr != null)
            {
                mgr.OnActiveAccountChanged -= HandleActiveAccountChanged;
            }
            isAccountSubscribed = false;
        }

        private void HandleActiveAccountChanged(UserAccountRecord _)
        {
            selectedCharIndex = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_CHAR_KEY, 0), 0, 2);
            if (IsCharacterSlotLocked(selectedCharIndex))
            {
                selectedCharIndex = 0;
            }
            selectedWorldIndex = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_WORLD_KEY, 0), 0, 2);
            previewShowingTransform = false;

            if (characterCards != null)
            {
                for (int i = 0; i < characterCards.Length; i++)
                {
                    if (characterCards[i] != null)
                    {
                        characterCards[i].showingTransform = false;
                    }
                }
            }

            RefreshAccountStatusPill();
            RefreshCharacterCardsUI();
            RefreshCharacterPreviewUI();
            RefreshLoadoutSlotsUI();
            RefreshWorldCardsUI();
            RefreshStageHeaderUI();

            if (coinCounterUI != null)
            {
                coinCounterUI.RefreshImmediate();
            }
            if (authUIController != null)
            {
                authUIController.RefreshSessionBanner();
            }
            if (shopManager != null)
            {
                shopManager.RefreshShopUI();
            }
        }

        private void RefreshAccountStatusPill()
        {
            if (accountStatusText == null && accountActionButton == null) return;

            AccountManager mgr = Application.isPlaying
                ? AccountManager.Instance
                : (AccountManager.HasInstance ? AccountManager.Instance : null);
            bool hasSession = mgr != null && mgr.HasActiveSession && mgr.ActiveAccount != null;

            if (accountPillButton != null && accountPillButton.TryGetComponent(out Image pillBg))
            {
                Sprite bgSprite = modeSelectCoinBadgeSprite ?? currencyBadgeSprite;
                if (bgSprite != null)
                {
                    GlassHudTheme.ApplySlicedSprite(pillBg, bgSprite, 540f, 74f, Color.white);
                }
            }

            if (accountStatusIcon != null)
            {
                if (authUserIconSprite != null) accountStatusIcon.sprite = authUserIconSprite;
                accountStatusIcon.type = Image.Type.Simple;
                accountStatusIcon.preserveAspect = true;
                accountStatusIcon.color = hasSession
                    ? new Color(0.133f, 0.827f, 0.933f, 1f)
                    : new Color(0.580f, 0.639f, 0.722f, 1f);
                accountStatusIcon.enabled = accountStatusIcon.sprite != null;
            }

            if (accountStatusText != null)
            {
                accountStatusText.text = hasSession
                    ? $"Tài khoản: {mgr.ActiveAccount.username}"
                    : "Chưa đăng nhập";
                accountStatusText.color = hasSession
                    ? new Color(0.973f, 0.980f, 0.988f, 1f)
                    : new Color(0.984f, 0.749f, 0.141f, 1f);

                RectTransform statusRt = accountStatusText.rectTransform;
                statusRt.offsetMin = new Vector2(58f, 6f);
                statusRt.offsetMax = new Vector2(hasSession ? -236f : -162f, -6f);
            }

            if (accountActionButton != null)
            {
                if (accountActionButton.TryGetComponent(out RectTransform actionRt))
                {
                    Vector2 targetPos = hasSession ? new Vector2(-124f, 0f) : new Vector2(-14f, 0f);
                    Vector2 targetSize = hasSession ? new Vector2(104f, 44f) : new Vector2(138f, 44f);
                    actionRt.anchoredPosition = targetPos;
                    actionRt.sizeDelta = targetSize;
                    if (accountActionButton.TryGetComponent(out Image actionBg) && authSecondaryBtnSprite != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(actionBg, authSecondaryBtnSprite, targetSize.x, targetSize.y, Color.white);
                    }
                    if (accountActionButton.TryGetComponent(out ModeCardHoverEffect actionHover))
                    {
                        actionHover.SetBaseAnchoredPosition(targetPos);
                    }
                }
            }

            if (accountActionLabel != null)
            {
                accountActionLabel.text = hasSession ? "ĐỔI TK" : "ĐĂNG NHẬP";
            }

            if (accountLogoutButton != null)
            {
                accountLogoutButton.gameObject.SetActive(hasSession);
                if (hasSession && accountLogoutButton.TryGetComponent(out Image logoutBg) && authSecondaryBtnSprite != null)
                {
                    GlassHudTheme.ApplySlicedSprite(logoutBg, authSecondaryBtnSprite, 106f, 44f, new Color(1f, 0.85f, 0.85f, 1f));
                }
            }
        }

        private void OpenAuthScreen(bool autoAdvance)
        {
            EnsureAuthUIInitialized();
            authAutoAdvanceAfterAuth = autoAdvance;
            ShowScreen(MainMenuScreen.Auth);
        }

        private void HandleAuthSucceeded()
        {
            bool advance = authAutoAdvanceAfterAuth;
            authAutoAdvanceAfterAuth = false;
            RefreshAccountStatusPill();

            if (advance)
            {
                if (modeSelectPanel != null)
                {
                    ShowScreen(MainMenuScreen.ModeSelect);
                }
                else
                {
                    SyncCharacterSelectionFromPrefs();
                    ShowScreen(MainMenuScreen.CharacterSelect);
                }
            }
            else
            {
                ShowScreen(MainMenuScreen.MainMenu);
            }
        }

        private void HandleAuthBackClicked()
        {
            authAutoAdvanceAfterAuth = false;
            ShowScreen(MainMenuScreen.MainMenu);
        }

        private void HandlePillLogoutClicked()
        {
            if (AccountManager.HasInstance)
            {
                AccountManager.Instance.Logout();
            }
            else if (AccountManager.Instance != null)
            {
                AccountManager.Instance.Logout();
            }
            RefreshAccountStatusPill();
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

            InventoryManager inv = InventoryManager.HasInstance ? InventoryManager.Instance : null;
            if (inv != null)
            {
                inv.OnInventoryChanged -= HandleInventoryChanged;
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
                {
                    bool hasSession = AccountManager.Instance != null && AccountManager.Instance.HasActiveSession;
                    if (!hasSession)
                    {
                        OpenAuthScreen(autoAdvance: true);
                        return;
                    }

                    if (modeSelectPanel != null)
                    {
                        ShowScreen(MainMenuScreen.ModeSelect);
                    }
                    else
                    {
                        SyncCharacterSelectionFromPrefs();
                        ShowScreen(MainMenuScreen.CharacterSelect);
                    }
                });
            }
            if (accountPillButton != null)
            {
                accountPillButton.onClick.RemoveAllListeners();
                accountPillButton.onClick.AddListener(() => OpenAuthScreen(autoAdvance: false));
            }
            if (accountActionButton != null)
            {
                accountActionButton.onClick.RemoveAllListeners();
                accountActionButton.onClick.AddListener(() => OpenAuthScreen(autoAdvance: false));
            }
            if (accountLogoutButton != null)
            {
                accountLogoutButton.onClick.RemoveAllListeners();
                accountLogoutButton.onClick.AddListener(HandlePillLogoutClicked);
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
                EnsureModeHoverEffect(campaignModeButton);
                campaignModeButton.onClick.RemoveAllListeners();
                campaignModeButton.onClick.AddListener(() => SelectGameModeAndContinue(GameMode.Campaign));
            }
            if (endlessModeButton != null)
            {
                EnsureModeHoverEffect(endlessModeButton);
                endlessModeButton.onClick.RemoveAllListeners();
                endlessModeButton.onClick.AddListener(() => SelectGameModeAndContinue(GameMode.Endless));
            }
            if (trainingModeButton != null)
            {
                EnsureModeHoverEffect(trainingModeButton);
                trainingModeButton.onClick.RemoveAllListeners();
                trainingModeButton.onClick.AddListener(() => SelectGameModeAndContinue(GameMode.Training));
            }
            if (openShopButton != null)
            {
                EnsureModeHoverEffect(openShopButton);
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
            if (screen != MainMenuScreen.Auth)
            {
                authAutoAdvanceAfterAuth = false;
            }
            currentScreen = screen;
            if (Application.isPlaying && screen == MainMenuScreen.Auth)
            {
                EnsureAuthUIInitialized();
            }

            if (mainMenuPanel != null) mainMenuPanel.SetActive(screen == MainMenuScreen.MainMenu);
            if (modeSelectPanel != null) modeSelectPanel.SetActive(screen == MainMenuScreen.ModeSelect);
            if (shopPanel != null) shopPanel.SetActive(screen == MainMenuScreen.Shop);
            if (instructionsPanel != null) instructionsPanel.SetActive(screen == MainMenuScreen.Instructions);
            if (characterSelectPanel != null) characterSelectPanel.SetActive(screen == MainMenuScreen.CharacterSelect);
            if (worldSelectPanel != null) worldSelectPanel.SetActive(screen == MainMenuScreen.WorldSelect);
            if (stageSelectPanel != null) stageSelectPanel.SetActive(screen == MainMenuScreen.StageSelect);
            if (authPanel != null) authPanel.SetActive(screen == MainMenuScreen.Auth);

            if (backButton != null)
            {
                backButton.gameObject.SetActive(screen != MainMenuScreen.MainMenu && screen != MainMenuScreen.Auth);
            }

            ApplyTopBarSpritesForScreen(screen);

            if (screen == MainMenuScreen.MainMenu)
            {
                RefreshAccountStatusPill();
            }
            else if (screen == MainMenuScreen.Auth && authUIController != null)
            {
                AccountManager mgr = Application.isPlaying ? AccountManager.Instance : null;
                bool startInRegister = mgr == null || mgr.Accounts.Count == 0;
                authUIController.Open(startInRegister, HandleAuthSucceeded, HandleAuthBackClicked);
                if (runtimeTmpFont != null)
                {
                    authUIController.ApplyFont(runtimeTmpFont);
                }
            }
            else if (screen == MainMenuScreen.CharacterSelect)
            {
                RefreshCharacterCardsUI();
                RefreshCharacterPreviewUI();
                RefreshLoadoutSlotsUI();
            }
            else if (screen == MainMenuScreen.WorldSelect)
            {
                RefreshWorldCardsUI();
            }
            else if (screen == MainMenuScreen.StageSelect)
            {
                RefreshStageHeaderUI();
            }
            else if (screen == MainMenuScreen.Instructions && instructionsPanel != null)
            {
                InstructionsUI instrUi = instructionsPanel.GetComponentInChildren<InstructionsUI>(true);
                if (instrUi != null)
                {
                    instrUi.ApplySprites();
                }
            }
            else if (screen == MainMenuScreen.Shop)
            {
                ShopManager activeShop = shopManager != null
                    ? shopManager
                    : (shopPanel != null ? shopPanel.GetComponent<ShopManager>() : null);
                if (activeShop != null)
                {
                    activeShop.ApplyShopGlassStyling();
                    activeShop.RefreshShopUI();
                }
            }

            if (coinCounterUI != null)
            {
                bool showCoinBadge = screen != MainMenuScreen.Auth;
                coinCounterUI.gameObject.SetActive(showCoinBadge);
                if (showCoinBadge)
                {
                    coinCounterUI.RefreshImmediate();
                }
            }
        }

        public void HandleBackClicked()
        {
            switch (currentScreen)
            {
                case MainMenuScreen.Auth:
                    HandleAuthBackClicked();
                    break;
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
            SyncCharacterSelectionFromPrefs();
            ShowScreen(MainMenuScreen.CharacterSelect);
        }

        private void SyncCharacterSelectionFromPrefs()
        {
            selectedCharIndex = Mathf.Clamp(PlayerPrefs.GetInt(SELECTED_CHAR_KEY, 0), 0, 2);
            if (IsCharacterSlotLocked(selectedCharIndex))
            {
                selectedCharIndex = 0;
            }
            previewShowingTransform = false;
            if (characterCards != null)
            {
                for (int i = 0; i < characterCards.Length; i++)
                {
                    if (characterCards[i] != null)
                    {
                        characterCards[i].showingTransform = false;
                    }
                }
            }
            RefreshCharacterCardsUI();
            RefreshCharacterPreviewUI();
            RefreshStageHeaderUI();
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
            if (AccountManager.HasInstance && AccountManager.Instance.HasActiveSession)
            {
                AccountManager.Instance.SyncActiveProfileFromPrefs(saveToDisk: true);
            }

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
            if (AccountManager.HasInstance && AccountManager.Instance.HasActiveSession)
            {
                AccountManager.Instance.SyncActiveProfileFromPrefs(saveToDisk: true);
            }

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
            else
            {
                nextItem = (idx + 1 < ownedItems.Count) ? ownedItems[idx + 1] : ConsumableItemType.None;
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
            if (AccountManager.HasInstance && AccountManager.Instance.HasActiveSession)
            {
                AccountManager.Instance.SyncActiveProfileFromPrefs(saveToDisk: true);
            }

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
            if (AccountManager.HasInstance && AccountManager.Instance.HasActiveSession)
            {
                AccountManager.Instance.SyncActiveProfileFromPrefs(saveToDisk: true);
            }
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
            if (AccountManager.HasInstance && AccountManager.Instance.HasActiveSession)
            {
                AccountManager.Instance.SyncActiveProfileFromPrefs(saveToDisk: true);
            }

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

            ResolveGlassSprites();
            Transform selectedRootToBringFront = null;

            Vector2[] topSlotPositions =
            {
                new Vector2(-224f, 108f),
                new Vector2(0f, 108f),
                new Vector2(224f, 108f)
            };
            Vector2[] lockedSlotPositions =
            {
                new Vector2(-168f, -110f),
                new Vector2(168f, -110f)
            };

            for (int i = 0; i < characterCards.Length; i++)
            {
                MenuCharacterCardUI card = characterCards[i];
                if (card == null) continue;

                bool locked = IsCharacterSlotLocked(i);
                bool isSelected = !locked && i == selectedCharIndex;

                Transform cardRoot = card.highlightBorder != null
                    ? card.highlightBorder.transform
                    : (card.selectButton != null ? card.selectButton.transform.parent : null);

                float targetW = locked ? 318f : 202f;
                float targetH = locked ? 200f : 210f;

                if (cardRoot != null)
                {
                    if (cardRoot is RectTransform cardRt)
                    {
                        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                        cardRt.pivot = new Vector2(0.5f, 0.5f);
                        cardRt.sizeDelta = new Vector2(targetW, targetH);
                        if (!locked && i < topSlotPositions.Length)
                        {
                            cardRt.anchoredPosition = topSlotPositions[i];
                        }
                        else if (locked)
                        {
                            int lockIdx = Mathf.Clamp(i - 3, 0, lockedSlotPositions.Length - 1);
                            cardRt.anchoredPosition = lockedSlotPositions[lockIdx];
                        }
                    }

                    cardRoot.localScale = isSelected ? Vector3.one * 1.08f : Vector3.one;
                    if (isSelected)
                    {
                        selectedRootToBringFront = cardRoot;
                    }
                }

                card.goldSelectionFrame = EnsureGoldSelectionFrame(
                    cardRoot,
                    card.goldSelectionFrame,
                    false,
                    Application.isPlaying
                );

                if (card.selectButton != null)
                {
                    card.selectButton.interactable = !locked;
                }

                if (card.lockOverlay != null)
                {
                    card.lockOverlay.SetActive(locked);
                    if (card.lockOverlay.TryGetComponent(out Image lockBg))
                    {
                        lockBg.color = Color.clear;
                        lockBg.raycastTarget = false;
                    }
                }

                if (card.lockedLabelText != null)
                {
                    card.lockedLabelText.text = locked ? "Đang phát triển" : string.Empty;
                    card.lockedLabelText.gameObject.SetActive(locked);
                    if (locked)
                    {
                        StyleLegacyTextCrisp(card.lockedLabelText, 22, new Color(0.96f, 0.70f, 0.22f, 1f), bold: true);
                    }
                }

                if (card.portraitBackdrop != null)
                {
                    card.portraitBackdrop.SetActive(!locked);
                    if (!locked && card.portraitBackdrop.TryGetComponent(out RectTransform pbRt))
                    {
                        pbRt.anchorMin = new Vector2(0.5f, 1f);
                        pbRt.anchorMax = new Vector2(0.5f, 1f);
                        pbRt.pivot = new Vector2(0.5f, 1f);
                        pbRt.anchoredPosition = new Vector2(0f, -24f);
                        pbRt.sizeDelta = new Vector2(132f, 108f);
                    }
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
                    Sprite frameSprite = locked
                        ? charSelectComingSoonSprite
                        : (isSelected
                            ? (charSelectSelectedGoldSprite ?? charSlotGoldSelectedSprite)
                            : (charSelectUnselectedSprite ?? charSlotGlassSprite));

                    if (frameSprite != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(card.highlightBorder, frameSprite, targetW, targetH, Color.white);

                        if (card.selectButton != null &&
                            card.selectButton.gameObject != card.highlightBorder.gameObject &&
                            card.selectButton.TryGetComponent(out Image innerImg) &&
                            innerImg != card.highlightBorder)
                        {
                            innerImg.color = Color.clear;
                        }

                        if (card.portraitBackdrop != null &&
                            card.portraitBackdrop.TryGetComponent(out Image pBackImg))
                        {
                            pBackImg.color = new Color(0.05f, 0.08f, 0.14f, 0.16f);
                        }
                    }
                    else
                    {
                        card.highlightBorder.color = locked
                            ? LockedBorderColor
                            : (isSelected ? SelectedHighlightColor : IdleBorderColor);
                    }
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
                        card.characterNameText.rectTransform.anchoredPosition = new Vector2(0f, -62f);
                        card.characterNameText.rectTransform.sizeDelta = new Vector2(180f, 38f);
                        card.characterNameText.text = !string.IsNullOrEmpty(card.characterName)
                            ? card.characterName
                            : DefaultCharacterNames[Mathf.Clamp(i, 0, DefaultCharacterNames.Length - 1)];
                        StyleLegacyTextCrisp(card.characterNameText, 20, Color.white, bold: true);
                    }
                }

                if (card.formBadgeText != null)
                {
                    card.formBadgeText.text = string.Empty;
                    card.formBadgeText.gameObject.SetActive(false);
                }

                if (card.toggleButtonText != null)
                {
                    card.toggleButtonText.text = string.Empty;
                    card.toggleButtonText.gameObject.SetActive(false);
                }

                if (card.skillsDescText != null)
                {
                    card.skillsDescText.text = string.Empty;
                    card.skillsDescText.gameObject.SetActive(false);
                }
            }

            if (selectedRootToBringFront != null)
            {
                selectedRootToBringFront.SetAsLastSibling();
            }
        }

        private void RefreshCharacterPreviewUI()
        {
            int idx = Mathf.Clamp(selectedCharIndex, 0, 2);

            ResolveGlassSprites();
            Color formTint = previewShowingTransform ? new Color(1f, 0.90f, 0.45f, 1f) : Color.white;
            if (previewFormToggleButton != null && charSelectResetIconSprite != null)
            {
                if (previewFormToggleButton.TryGetComponent(out RectTransform toggleRt))
                {
                    toggleRt.sizeDelta = new Vector2(88f, 80f);
                }
                if (previewFormToggleButton.TryGetComponent(out Image toggleImg))
                {
                    GlassHudTheme.ApplySlicedSprite(toggleImg, charSelectResetIconSprite, 88f, 80f, formTint);
                }
            }
            EnsureSwapFormButtonVisual(
                previewFormToggleButton,
                previewFormToggleText,
                Application.isPlaying,
                formTint
            );

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
                Vector2 previewSize = bgCharacterPreviewImage.rectTransform.sizeDelta;
                if (previewSize.y > 0f && previewSize.x > 0f && previewSize.x < previewSize.y * 0.92f)
                {
                    bgCharacterPreviewImage.rectTransform.sizeDelta = new Vector2(previewSize.y, previewSize.y);
                }
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
                selectedCharacterNameText.text = baseName;
                StyleTmpTextCrisp(selectedCharacterNameText, -1f, selectedCharacterNameText.color, bold: true);
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
                StyleTmpTextCrisp(nameTxt, -1f, nameTxt.color, bold: true);
            }

            if (countTxt != null)
            {
                countTxt.text = $"x{owned}";
                Color countColor = (itemType != ConsumableItemType.None && owned > 0)
                    ? new Color(0.95f, 0.88f, 0.45f, 1f)
                    : new Color(0.95f, 0.45f, 0.45f, 0.9f);
                StyleTmpTextCrisp(countTxt, -1f, countColor, bold: true);
            }

            if (iconImg != null)
            {
                bool isEmpty = itemType == ConsumableItemType.None || owned <= 0;
                Sprite itemIcon = (data != null && data.icon != null)
                    ? data.icon
                    : GetFallbackItemIcon(itemType);

                if (itemType != ConsumableItemType.None && itemIcon != null)
                {
                    iconImg.sprite = itemIcon;
                    iconImg.enabled = true;
                    iconImg.preserveAspect = true;
                    iconImg.rectTransform.localScale = Vector3.one;
                    iconImg.color = isEmpty ? EmptyIconDimColor : Color.white;
                }
                else
                {
                    iconImg.enabled = false;
                }
            }
        }

        public void ApplyGlassmorphismTheme()
        {
            ResolveGlassSprites();

            MenuBackgroundLoader bgLoader = GetComponentInChildren<MenuBackgroundLoader>(true);
            if (bgLoader != null)
            {
                bgLoader.ApplyVisuals();
            }

            // Ensure full-screen backdrops across pre-match panels never hide global background
            TransparentizeFullScreenBackdrops(mainMenuPanel);
            TransparentizeFullScreenBackdrops(instructionsPanel);
            TransparentizeFullScreenBackdrops(modeSelectPanel);
            TransparentizeFullScreenBackdrops(shopPanel);
            TransparentizeFullScreenBackdrops(characterSelectPanel);
            TransparentizeFullScreenBackdrops(worldSelectPanel);
            TransparentizeFullScreenBackdrops(stageSelectPanel);
            TransparentizeFullScreenBackdrops(authPanel);

            // Screen 1: Main Menu
            if (mainMenuPanel != null)
            {
                StyleLegacyChildAt(mainMenuPanel.transform, "GameTitle");
            }
            RefreshAccountStatusPill();
            if (authUIController != null)
            {
                authUIController.ConfigureThemeSprites(BuildAuthThemeSprites());
            }

            ApplyMainMenuButtonStyle(playButton, mainMenuPlaySprite, new Vector2(0f, 45f), new Vector2(460f, 106f));
            ApplyMainMenuButtonStyle(instructionsButton, mainMenuGuideSprite, new Vector2(0f, -72f), new Vector2(430f, 98f));
            ApplyMainMenuButtonStyle(exitButton, mainMenuQuitSprite, new Vector2(0f, -184f), new Vector2(430f, 98f));

            if (coinCounterUI != null)
            {
                Transform coinIconTf = coinCounterUI.transform.Find("CoinIcon");
                if (coinIconTf != null)
                {
                    if (coinIconTf.TryGetComponent(out RectTransform coinIconRt))
                    {
                        coinIconRt.anchorMin = new Vector2(0f, 0.5f);
                        coinIconRt.anchorMax = new Vector2(0f, 0.5f);
                        coinIconRt.pivot = new Vector2(0.5f, 0.5f);
                        coinIconRt.anchoredPosition = new Vector2(38f, 0f);
                        coinIconRt.sizeDelta = new Vector2(40f, 40f);
                    }
                    if (coinIconTf.TryGetComponent(out Image coinIconImg))
                    {
                        Sprite coinSprite = coinXuIconSprite ?? LoadItemIconSprite("Icon_CoinXu.png");
                        if (coinSprite != null)
                        {
                            coinIconImg.sprite = coinSprite;
                        }
                        coinIconImg.type = Image.Type.Simple;
                        coinIconImg.color = Color.white;
                        coinIconImg.preserveAspect = true;
                    }
                }
                TMP_Text coinTmp = coinCounterUI.GetComponentInChildren<TMP_Text>(true);
                if (coinTmp != null)
                {
                    RectTransform coinTmpRt = coinTmp.rectTransform;
                    coinTmpRt.anchoredPosition = new Vector2(22f, 0f);
                    coinTmpRt.sizeDelta = new Vector2(158f, 48f);
                    StyleTmpTextCrisp(coinTmp, -1f, coinTmp.color, bold: true);
                }
            }

            ApplyTopBarSpritesForScreen(currentScreen);

            // Screen 1B: Mode Select (4 BlankFrame Cards + Dedicated ModeIcon + Crisp Vietnamese Labels)
            if (modeSelectPanel != null)
            {
                StyleLegacyChildAt(modeSelectPanel.transform, "ModeSelectTitle");
            }
            StyleModeCard(campaignModeButton, modeCampaignCardSprite, modeCampaignIconSprite, new Vector2(-594f, -35f));
            StyleModeCard(endlessModeButton, modeEndlessCardSprite, modeEndlessIconSprite, new Vector2(-198f, -35f));
            StyleModeCard(trainingModeButton, modeTrainingCardSprite, modeTrainingIconSprite, new Vector2(198f, -35f));
            StyleModeCard(openShopButton, modeShopCardSprite, modeShopIconSprite, new Vector2(594f, -35f));

            // Instructions Glass Panel
            if (instructionsPanel != null)
            {
                StyleLegacyChildAt(instructionsPanel.transform, "InstrTitle");
                InstructionsUI instrUi = instructionsPanel.GetComponentInChildren<InstructionsUI>(true);
                if (instrUi != null)
                {
                    instrUi.ApplySprites();
                }
                else
                {
                    ApplyModalGlassPanel(
                        instructionsPanel.transform.Find("InstrGridContainer"),
                        new Color(0.06f, 0.08f, 0.14f, 0.44f)
                    );
                }
            }

            // Shop Glass Panel
            if (shopPanel != null)
            {
                StyleLegacyChildAt(shopPanel.transform, "ShopTitle");
                StyleLegacyChildAt(shopPanel.transform, "ShopFeedbackText");

                Transform rowsContainer = shopPanel.transform.Find("ShopRowsContainer");
                if (rowsContainer != null && rowsContainer.TryGetComponent(out Image rowsContainerImg))
                {
                    rowsContainerImg.color = Color.clear;
                    rowsContainerImg.raycastTarget = false;
                }

                ShopManager activeShop = shopManager != null ? shopManager : shopPanel.GetComponent<ShopManager>();
                if (activeShop != null)
                {
                    activeShop.ApplyShopGlassStyling();
                }
            }

            // Screen 2: Character Select + Q/E Loadout + Action Buttons
            if (characterSelectPanel != null)
            {
                StyleLegacyChildAt(characterSelectPanel.transform, "Title");

                Transform leftFrameTf = characterSelectPanel.transform.Find("CharacterSelectLeftFrame");
                if (leftFrameTf != null)
                {
                    if (leftFrameTf.TryGetComponent(out RectTransform lfRt))
                    {
                        lfRt.anchoredPosition = new Vector2(-460f, 128f);
                        lfRt.sizeDelta = new Vector2(820f, 556f);
                    }
                    if (leftFrameTf.TryGetComponent(out Image lfImg) && charSelectMainPanelSprite != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(lfImg, charSelectMainPanelSprite, 820f, 556f, Color.white);
                        if (leftFrameTf.TryGetComponent(out Outline lfOutline))
                        {
                            lfOutline.enabled = false;
                        }
                    }
                    else
                    {
                        ApplyModalGlassPanel(leftFrameTf, new Color(0.05f, 0.08f, 0.14f, 0.46f));
                    }
                }

                Transform loadoutBarTf = characterSelectPanel.transform.Find("LoadoutBar");
                if (loadoutBarTf != null)
                {
                    if (loadoutBarTf.TryGetComponent(out RectTransform lbRt))
                    {
                        lbRt.anchoredPosition = new Vector2(-460f, -295f);
                        lbRt.sizeDelta = new Vector2(820f, 244f);
                    }
                    if (loadoutBarTf.TryGetComponent(out Image lbImg) && itemLoadoutPanelSprite != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(lbImg, itemLoadoutPanelSprite, 820f, 244f, Color.white);
                        if (loadoutBarTf.TryGetComponent(out Outline lbOutline))
                        {
                            lbOutline.enabled = false;
                        }
                    }
                    else
                    {
                        ApplyModalGlassPanel(loadoutBarTf, new Color(0.05f, 0.08f, 0.14f, 0.46f));
                    }

                    Transform headerTf = loadoutBarTf.Find("LoadoutHeader");
                    if (headerTf != null)
                    {
                        if (headerTf.TryGetComponent(out RectTransform hRt))
                        {
                            hRt.anchoredPosition = new Vector2(0f, 94f);
                            hRt.sizeDelta = new Vector2(310f, 32f);
                        }
                        if (headerTf.TryGetComponent(out TMP_Text headerTmp))
                        {
                            headerTmp.text = "VẬT PHẨM TRANG BỊ";
                            GlassHudTheme.StyleTmpText(headerTmp, 21f, new Color(0.09f, 0.12f, 0.22f, 1f), bold: true, addShadow: false);
                            if (headerTf.TryGetComponent(out Outline hOutline))
                            {
                                hOutline.enabled = false;
                            }
                        }
                    }
                }

                if (characterSelectFeedbackText != null)
                {
                    characterSelectFeedbackText.rectTransform.anchoredPosition = new Vector2(-460f, -452f);
                    characterSelectFeedbackText.rectTransform.sizeDelta = new Vector2(800f, 36f);
                }
                StyleTmpTextCrisp(selectedCharacterNameText, -1f, selectedCharacterNameText != null ? selectedCharacterNameText.color : Color.white, bold: true);
                StyleTmpTextCrisp(characterSelectFeedbackText, -1f, characterSelectFeedbackText != null ? characterSelectFeedbackText.color : Color.white, bold: true);
            }

            StyleLoadoutHolderLayout(loadoutQButton, new Vector2(-165f, -14f));
            StyleLoadoutHolderLayout(loadoutEButton, new Vector2(165f, -14f));
            ApplyEnergyOrbButtonStyle(loadoutQButton, itemLoadoutQSlotSprite ?? energyOrbSlotSprite);
            ApplyEnergyOrbButtonStyle(loadoutEButton, itemLoadoutESlotSprite ?? energyOrbSlotSprite);

            if (previewFormToggleButton != null)
            {
                if (previewFormToggleButton.TryGetComponent(out RectTransform pRt))
                {
                    pRt.anchoredPosition = new Vector2(-404f, 78f);
                    pRt.sizeDelta = new Vector2(88f, 80f);
                }
                Sprite resetSp = charSelectResetIconSprite ?? techCutActionButtonSprite;
                if (resetSp != null && previewFormToggleButton.TryGetComponent(out Image pImg))
                {
                    GlassHudTheme.ApplySlicedSprite(pImg, resetSp, 88f, 80f, Color.white);
                }
                ModeCardHoverEffect pHover = EnsureButtonHoverEffect(previewFormToggleButton);
                if (pHover != null) pHover.SetBaseAnchoredPosition(new Vector2(-404f, 78f));
            }

            ApplyContinueButtonStyle(characterNextButton, new Vector2(-205f, 78f), new Vector2(252f, 88f));
            if (confirmCharacterButton != null && confirmCharacterButton != characterNextButton)
            {
                ApplyContinueButtonStyle(confirmCharacterButton, new Vector2(-205f, 78f), new Vector2(252f, 88f));
            }

            if (worldNextButton != null)
            {
                Sprite contSp = charSelectContinueBtnSprite ?? techCutActionButtonSprite;
                if (contSp != null && worldNextButton.TryGetComponent(out Image wnImg))
                {
                    RectTransform wnRt = worldNextButton.GetComponent<RectTransform>();
                    float w = wnRt != null && wnRt.sizeDelta.x > 1f ? wnRt.sizeDelta.x : 250f;
                    float h = wnRt != null && wnRt.sizeDelta.y > 1f ? wnRt.sizeDelta.y : 68f;
                    GlassHudTheme.ApplySlicedSprite(wnImg, contSp, w, h, Color.white);
                }
                StyleButtonLabels(worldNextButton, Color.white);
                EnsureButtonHoverEffect(worldNextButton);
            }

            // Screen 3: World Select
            if (worldSelectPanel != null)
            {
                StyleLegacyChildAt(worldSelectPanel.transform, "Title");
            }
            if (worldCards != null)
            {
                RefreshWorldCardsUI();
            }

            // Screen 4: Stage Cards (World-Themed Glass Frames + Boss Gold Frame)
            RefreshStageHeaderUI();
        }

        private static Sprite ResolveSlicedSprite(Sprite current, string relPath, string expectedName)
        {
            if (current != null && string.Equals(current.name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
            Sprite loaded = GlassHudTheme.LoadSprite(relPath);
            return loaded != null ? loaded : current;
        }

        private void ApplyMainMenuButtonStyle(Button btn, Sprite sprite, Vector2 pos, Vector2 size)
        {
            if (btn == null) return;
            if (btn.TryGetComponent(out RectTransform rt))
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }
            if (sprite != null && btn.TryGetComponent(out Image img))
            {
                GlassHudTheme.ApplySlicedSprite(img, sprite, size.x, size.y, Color.white);
            }
            StyleButtonLabels(btn, Color.white);
            ModeCardHoverEffect hover = EnsureButtonHoverEffect(btn);
            if (hover != null)
            {
                hover.SetBaseAnchoredPosition(pos);
            }
        }

        private void ApplyContinueButtonStyle(Button btn, Vector2 pos, Vector2 size)
        {
            if (btn == null) return;
            if (btn.TryGetComponent(out RectTransform rt))
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }
            Sprite sp = charSelectContinueBtnSprite ?? techCutActionButtonSprite;
            if (sp != null && btn.TryGetComponent(out Image img))
            {
                GlassHudTheme.ApplySlicedSprite(img, sp, size.x, size.y, Color.white);
            }
            StyleButtonLabels(btn, Color.white);
            ModeCardHoverEffect hover = EnsureButtonHoverEffect(btn);
            if (hover != null)
            {
                hover.SetBaseAnchoredPosition(pos);
            }
        }

        private static void StyleLoadoutHolderLayout(Button slotBtn, Vector2 holderPos)
        {
            if (slotBtn == null || slotBtn.transform.parent == null) return;
            Transform holderTf = slotBtn.transform.parent;
            if (holderTf.TryGetComponent(out RectTransform hRt))
            {
                hRt.anchoredPosition = holderPos;
                hRt.sizeDelta = new Vector2(260f, 152f);
            }
            Transform nameTf = holderTf.Find("ItemNameTxt");
            if (nameTf != null && nameTf.TryGetComponent(out RectTransform nRt))
            {
                nRt.anchoredPosition = new Vector2(0f, -58f);
                nRt.sizeDelta = new Vector2(260f, 26f);
                if (nameTf.TryGetComponent(out TMP_Text nTmp))
                {
                    nTmp.fontSize = 18f;
                }
            }
        }

        private void ApplyTopBarSpritesForScreen(MainMenuScreen screen)
        {
            ResolveGlassSprites();

            Sprite targetBackSprite;
            Vector2 backPos;
            Vector2 backSize;
            Sprite targetCoinSprite;
            Vector2 coinSize;

            switch (screen)
            {
                case MainMenuScreen.CharacterSelect:
                case MainMenuScreen.WorldSelect:
                case MainMenuScreen.StageSelect:
                    targetBackSprite = charSelectBackBtnSprite ?? modeSelectBackSprite ?? techCutActionButtonSprite;
                    backPos = new Vector2(148f, -62f);
                    backSize = new Vector2(224f, 76f);
                    targetCoinSprite = charSelectTopRightBadgeSprite ?? modeSelectCoinBadgeSprite ?? currencyBadgeSprite;
                    coinSize = new Vector2(250f, 82f);
                    break;

                case MainMenuScreen.Shop:
                    targetBackSprite = shopBackButtonSprite ?? modeSelectBackSprite ?? techCutActionButtonSprite;
                    backPos = new Vector2(152f, -56f);
                    backSize = new Vector2(232f, 74f);
                    targetCoinSprite = shopCoinPillSprite ?? modeSelectCoinBadgeSprite ?? currencyBadgeSprite;
                    coinSize = new Vector2(256f, 74f);
                    break;

                default:
                    targetBackSprite = modeSelectBackSprite ?? techCutActionButtonSprite;
                    backPos = new Vector2(152f, -56f);
                    backSize = new Vector2(232f, 74f);
                    targetCoinSprite = modeSelectCoinBadgeSprite ?? currencyBadgeSprite;
                    coinSize = new Vector2(252f, 74f);
                    break;
            }

            if (backButton != null)
            {
                if (backButton.TryGetComponent(out RectTransform bRt))
                {
                    bRt.anchoredPosition = backPos;
                    bRt.sizeDelta = backSize;
                }
                if (targetBackSprite != null && backButton.TryGetComponent(out Image bImg))
                {
                    GlassHudTheme.ApplySlicedSprite(bImg, targetBackSprite, backSize.x, backSize.y, Color.white);
                }
                StyleButtonLabels(backButton, Color.white);
                ModeCardHoverEffect bHover = EnsureButtonHoverEffect(backButton);
                if (bHover != null)
                {
                    bHover.SetBaseAnchoredPosition(backPos);
                }
            }

            if (coinCounterUI != null)
            {
                if (coinCounterUI.TryGetComponent(out RectTransform cRt))
                {
                    cRt.anchoredPosition = new Vector2(-36f, -24f);
                    cRt.sizeDelta = coinSize;
                }
                if (targetCoinSprite != null && coinCounterUI.TryGetComponent(out Image cImg))
                {
                    GlassHudTheme.ApplySlicedSprite(cImg, targetCoinSprite, coinSize.x, coinSize.y, Color.white);
                }
            }
        }

        private void ResolveGlassSprites()
        {
            mainMenuPlaySprite = ResolveSlicedSprite(mainMenuPlaySprite, "01_MainMenu/Btn_MainMenu_Play_Clean", "Btn_MainMenu_Play_Clean");
            mainMenuGuideSprite = ResolveSlicedSprite(mainMenuGuideSprite, "01_MainMenu/Btn_MainMenu_Guide_Clean", "Btn_MainMenu_Guide_Clean");
            mainMenuQuitSprite = ResolveSlicedSprite(mainMenuQuitSprite, "01_MainMenu/Btn_MainMenu_Quit_Clean", "Btn_MainMenu_Quit_Clean");

            modeSelectBackSprite = ResolveSlicedSprite(modeSelectBackSprite, "02_ModeSelect/Btn_ModeSelect_Back_Clean", "Btn_ModeSelect_Back_Clean");
            modeSelectCoinBadgeSprite = ResolveSlicedSprite(modeSelectCoinBadgeSprite, "02_ModeSelect/Badge_ModeSelect_CoinTopRight_Blank", "Badge_ModeSelect_CoinTopRight_Blank");
            coinXuIconSprite = ResolveSlicedSprite(coinXuIconSprite, "02_ModeSelect/Icon_Currency_CoinXu", "Icon_Currency_CoinXu");

            modeCampaignCardSprite = ResolveSlicedSprite(modeCampaignCardSprite, "02_ModeSelect/Card_Mode_Campaign_BlankFrame", "Card_Mode_Campaign_BlankFrame");
            modeEndlessCardSprite = ResolveSlicedSprite(modeEndlessCardSprite, "02_ModeSelect/Card_Mode_Endless_BlankFrame", "Card_Mode_Endless_BlankFrame");
            modeTrainingCardSprite = ResolveSlicedSprite(modeTrainingCardSprite, "02_ModeSelect/Card_Mode_Training_BlankFrame", "Card_Mode_Training_BlankFrame");
            modeShopCardSprite = ResolveSlicedSprite(modeShopCardSprite, "02_ModeSelect/Card_Mode_Shop_BlankFrame", "Card_Mode_Shop_BlankFrame");

            modeCampaignIconSprite = ResolveSlicedSprite(modeCampaignIconSprite, "02_ModeSelect/Icon_Mode_Campaign_ShieldSwords", "Icon_Mode_Campaign_ShieldSwords");
            modeEndlessIconSprite = ResolveSlicedSprite(modeEndlessIconSprite, "02_ModeSelect/Icon_Mode_Endless_InfinityGear", "Icon_Mode_Endless_InfinityGear");
            modeTrainingIconSprite = ResolveSlicedSprite(modeTrainingIconSprite, "02_ModeSelect/Icon_Mode_Training_CyberFist", "Icon_Mode_Training_CyberFist");
            modeShopIconSprite = ResolveSlicedSprite(modeShopIconSprite, "02_ModeSelect/Icon_Mode_Shop_CyberCart", "Icon_Mode_Shop_CyberCart");

            charSelectMainPanelSprite = ResolveSlicedSprite(charSelectMainPanelSprite, "03_CharacterAndItemSelect/Frame_CharSelect_MainPanel_Clean", "Frame_CharSelect_MainPanel_Clean");
            charSelectSelectedGoldSprite = ResolveSlicedSprite(charSelectSelectedGoldSprite, "03_CharacterAndItemSelect/Slot_CharSelect_SelectedGold_Clean", "Slot_CharSelect_SelectedGold_Clean");
            charSelectUnselectedSprite = ResolveSlicedSprite(charSelectUnselectedSprite, "03_CharacterAndItemSelect/Slot_CharSelect_Unselected_Clean", "Slot_CharSelect_Unselected_Clean");
            charSelectComingSoonSprite = ResolveSlicedSprite(charSelectComingSoonSprite, "03_CharacterAndItemSelect/Slot_CharSelect_ComingSoon_Clean", "Slot_CharSelect_ComingSoon_Clean");
            itemLoadoutPanelSprite = ResolveSlicedSprite(itemLoadoutPanelSprite, "03_CharacterAndItemSelect/Frame_ItemLoadout_Panel_Clean", "Frame_ItemLoadout_Panel_Clean");
            itemLoadoutQSlotSprite = ResolveSlicedSprite(itemLoadoutQSlotSprite, "03_CharacterAndItemSelect/Slot_ItemLoadout_Q_CyanRing_Clean", "Slot_ItemLoadout_Q_CyanRing_Clean");
            itemLoadoutESlotSprite = ResolveSlicedSprite(itemLoadoutESlotSprite, "03_CharacterAndItemSelect/Slot_ItemLoadout_E_VioletRing_Clean", "Slot_ItemLoadout_E_VioletRing_Clean");
            charSelectContinueBtnSprite = ResolveSlicedSprite(charSelectContinueBtnSprite, "03_CharacterAndItemSelect/Btn_CharSelect_Continue_Clean", "Btn_CharSelect_Continue_Clean");
            charSelectBackBtnSprite = ResolveSlicedSprite(charSelectBackBtnSprite, "03_CharacterAndItemSelect/Btn_CharSelect_Back_Clean", "Btn_CharSelect_Back_Clean");
            charSelectResetIconSprite = ResolveSlicedSprite(charSelectResetIconSprite, "03_CharacterAndItemSelect/Btn_CharSelect_ResetIcon", "Btn_CharSelect_ResetIcon");
            charSelectTopRightBadgeSprite = ResolveSlicedSprite(charSelectTopRightBadgeSprite, "03_CharacterAndItemSelect/Badge_CharSelect_TopRightFrame_Clean", "Badge_CharSelect_TopRightFrame_Clean");

            shopBackButtonSprite = ResolveSlicedSprite(shopBackButtonSprite, "05_Shop/Btn_Shop_Back_Clean", "Btn_Shop_Back_Clean");
            shopCoinPillSprite = ResolveSlicedSprite(shopCoinPillSprite, "05_Shop/Badge_Shop_TopRightPill_Clean", "Badge_Shop_TopRightPill_Clean");

            authModalPanelSprite = ResolveSlicedSprite(authModalPanelSprite, "04_AuthRegisterLogin/Frame_Auth_ModalPanel_Clean", "Frame_Auth_ModalPanel_Clean");
            authTabLoginInactiveSprite = ResolveSlicedSprite(authTabLoginInactiveSprite, "04_AuthRegisterLogin/Tab_Auth_Login_Inactive_Clean", "Tab_Auth_Login_Inactive_Clean");
            authTabRegisterActiveGoldSprite = ResolveSlicedSprite(authTabRegisterActiveGoldSprite, "04_AuthRegisterLogin/Tab_Auth_Register_ActiveGold_Clean", "Tab_Auth_Register_ActiveGold_Clean");
            authInputFieldBlankSprite = ResolveSlicedSprite(authInputFieldBlankSprite, "04_AuthRegisterLogin/Input_Auth_Field_Blank", "Input_Auth_Field_Blank");
            authUserIconSprite = ResolveSlicedSprite(authUserIconSprite, "04_AuthRegisterLogin/Icon_Auth_User", "Icon_Auth_User");
            authLockIconSprite = ResolveSlicedSprite(authLockIconSprite, "04_AuthRegisterLogin/Icon_Auth_Lock", "Icon_Auth_Lock");
            authPrimaryBtnSprite = ResolveSlicedSprite(authPrimaryBtnSprite, "04_AuthRegisterLogin/Btn_Auth_RegisterPrimary_Clean", "Btn_Auth_RegisterPrimary_Clean");
            authSecondaryBtnSprite = ResolveSlicedSprite(authSecondaryBtnSprite, "04_AuthRegisterLogin/Btn_Auth_BackSecondary_Clean", "Btn_Auth_BackSecondary_Clean");
            authInputUsernameCleanBackup = ResolveSlicedSprite(authInputUsernameCleanBackup, "04_AuthRegisterLogin/Input_Auth_Username_Clean", "Input_Auth_Username_Clean");
            authInputPasswordCleanBackup = ResolveSlicedSprite(authInputPasswordCleanBackup, "04_AuthRegisterLogin/Input_Auth_Password_Clean", "Input_Auth_Password_Clean");

            if (pillOrangeGlowSprite == null) pillOrangeGlowSprite = LoadGlassSprite("PillButton_OrangeGlow.png", PillOrangeSlice);
            if (pillSubtleGlassSprite == null) pillSubtleGlassSprite = LoadGlassSprite("PillButton_SubtleGlass.png", PillSubtleSlice);
            if (currencyBadgeSprite == null) currencyBadgeSprite = LoadGlassSprite("CurrencyBadge_TopRight.png", CurrencyBadgeSlice);
            if (charSlotGlassSprite == null) charSlotGlassSprite = LoadGlassSprite("CharSlot_Glass.png", CharSlotSlice);
            if (charSlotGoldSelectedSprite == null) charSlotGoldSelectedSprite = LoadGlassSprite("CharSlot_GoldSelected.png", CharSlotSlice);
            if (energyOrbSlotSprite == null) energyOrbSlotSprite = LoadGlassSprite("EnergyOrb_Slot.png", Vector4.zero);
            if (techCutActionButtonSprite == null) techCutActionButtonSprite = LoadGlassSprite("TechCut_ActionButton.png", TechCutSlice);
        }

        private static bool TryApplyHudSprite(
            Image target, string fileName, bool sliced = true, bool preserveAspect = false, Vector4 sliceBorder = default)
        {
            if (target == null || string.IsNullOrEmpty(fileName)) return false;
            if (GlassHudTheme.LoadSprite(fileName, sliceBorder) == null) return false;
            GlassHudTheme.ApplySprite(target, fileName, sliceBorder, sliced, preserveAspect);
            return true;
        }

        private static bool TryApplyHudSprite(
            Button btn, string fileName, bool sliced = true, bool preserveAspect = false, Vector4 sliceBorder = default)
        {
            return btn != null &&
                   btn.TryGetComponent(out Image img) &&
                   TryApplyHudSprite(img, fileName, sliced, preserveAspect, sliceBorder);
        }

        private static void ApplyModalGlassPanel(Transform target, Color fallbackColor)
        {
            if (target == null || !target.TryGetComponent(out Image img)) return;

            if (TryApplyHudSprite(img, "ModalCard_DarkGlass_Cyan.png", sliced: true, preserveAspect: false))
            {
                img.color = Color.white;
                if (target.TryGetComponent(out Outline outline))
                {
                    outline.enabled = false;
                }
            }
            else
            {
                img.color = fallbackColor;
            }
        }

        private static void StyleLegacyChildAt(Transform parent, string childName)
        {
            if (parent == null) return;
            Transform child = parent.Find(childName);
            if (child != null && child.TryGetComponent(out Text txt))
            {
                StyleLegacyTextCrisp(txt, -1, txt.color, bold: true);
            }
        }

        private static void StyleLegacyTextCrisp(Text txt, int fontSize = -1, Color? colorOverride = null, bool bold = true)
        {
            if (txt == null) return;
            Color targetColor = colorOverride ?? txt.color;
            GlassHudTheme.StyleLegacyText(txt, fontSize, targetColor, bold, addShadow: true);
            if ((targetColor.r + targetColor.g + targetColor.b) > 0.45f && txt.GetComponent<Outline>() == null)
            {
                Outline outline = txt.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        private static void StyleTmpTextCrisp(TMP_Text tmp, float fontSize = -1f, Color? colorOverride = null, bool bold = true)
        {
            if (tmp == null) return;
            Color targetColor = colorOverride ?? tmp.color;
            GlassHudTheme.StyleTmpText(tmp, fontSize, targetColor, bold, addShadow: true);
        }

        private static void StyleButtonLabels(Button btn, Color? colorOverride = null)
        {
            if (btn == null) return;

            Text[] legacyTexts = btn.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacyTexts.Length; i++)
            {
                Text txt = legacyTexts[i];
                if (txt != null && !string.IsNullOrEmpty(txt.text))
                {
                    StyleLegacyTextCrisp(txt, -1, colorOverride ?? txt.color, bold: true);
                }
            }

            TMP_Text[] tmpTexts = btn.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text tmp = tmpTexts[i];
                if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                {
                    StyleTmpTextCrisp(tmp, -1f, colorOverride ?? tmp.color, bold: true);
                }
            }
        }

        private static string GetWorldCardSpriteName(int worldIndex)
        {
            return worldIndex switch
            {
                0 => "WorldCard_AoT_EmeraldAmber.png",
                1 => "WorldCard_DB_AmberGold.png",
                2 => "WorldCard_Diend_MagentaCyan.png",
                _ => "WorldCard_AoT_EmeraldAmber.png"
            };
        }

        private static void ApplyButtonGlassSprite(Button btn, Sprite sprite, bool sliced)
        {
            if (btn == null || sprite == null) return;
            if (btn.TryGetComponent(out Image img))
            {
                ApplyGlassImageStyle(img, sprite, sliced, preserveAspect: false);
            }
        }

        private static void ApplyEnergyOrbButtonStyle(Button slotBtn, Sprite orbSprite)
        {
            if (slotBtn == null || orbSprite == null) return;
            if (slotBtn.TryGetComponent(out Image borderImg))
            {
                ApplyGlassImageStyle(borderImg, orbSprite, sliced: false, preserveAspect: true);
            }
            Transform maskTf = slotBtn.transform.Find("CircleMask");
            if (maskTf != null)
            {
                if (maskTf.TryGetComponent(out Image maskImg))
                {
                    maskImg.color = Color.white;
                }
                if (maskTf.TryGetComponent(out Mask mask))
                {
                    mask.showMaskGraphic = false;
                }
            }
            EnsureButtonHoverEffect(slotBtn, 4f, 1.05f, 15f);
        }

        public static ModeCardHoverEffect EnsureButtonHoverEffect(
            Button btn, float liftY = 0f, float scale = 1.035f, float speed = 15f)
        {
            if (btn == null) return null;
            ModeCardHoverEffect hover = btn.GetComponent<ModeCardHoverEffect>();
            if (hover == null)
            {
                hover = btn.gameObject.AddComponent<ModeCardHoverEffect>();
            }
            hover.Configure(liftY, scale, speed);
            return hover;
        }

        private static ModeCardHoverEffect EnsureModeHoverEffect(Button modeBtn)
        {
            return EnsureButtonHoverEffect(modeBtn, 8f, 1.045f, 14f);
        }

        private static void StyleModeCard(Button cardBtn, Sprite cardSprite, Sprite iconSprite, Vector2 targetPos)
        {
            if (cardBtn == null) return;

            ModeCardHoverEffect hover = EnsureModeHoverEffect(cardBtn);
            RectTransform rt = cardBtn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = targetPos;
                rt.sizeDelta = new Vector2(368f, 520f);
                if (hover != null)
                {
                    hover.SetBaseAnchoredPosition(targetPos);
                }
            }

            if (cardBtn.TryGetComponent(out Image bg) && cardSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(bg, cardSprite, 368f, 520f, Color.white);
            }

            Transform iconTf = cardBtn.transform.Find("ModeIcon");
            if (iconSprite != null)
            {
                GameObject iconObj = iconTf != null
                    ? iconTf.gameObject
                    : new GameObject("ModeIcon", typeof(RectTransform), typeof(Image));
                if (iconTf == null)
                {
                    iconObj.transform.SetParent(cardBtn.transform, false);
                }
                iconObj.transform.SetAsFirstSibling();
                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                if (iconRt != null)
                {
                    iconRt.anchorMin = new Vector2(0.14f, 0.42f);
                    iconRt.anchorMax = new Vector2(0.86f, 0.84f);
                    iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.offsetMin = Vector2.zero;
                    iconRt.offsetMax = Vector2.zero;
                    iconRt.localScale = Vector3.one;
                }
                if (iconObj.TryGetComponent(out Image iconImg))
                {
                    iconImg.sprite = iconSprite;
                    iconImg.type = Image.Type.Simple;
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
                    iconImg.color = Color.white;
                }
            }

            Transform titleTf = cardBtn.transform.Find("Title");
            if (titleTf != null && titleTf.TryGetComponent(out RectTransform titleRt))
            {
                titleRt.anchorMin = new Vector2(0.08f, 0.22f);
                titleRt.anchorMax = new Vector2(0.92f, 0.36f);
                titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.offsetMin = Vector2.zero;
                titleRt.offsetMax = Vector2.zero;
                if (titleTf.TryGetComponent(out Text titleTxt))
                {
                    titleTxt.alignment = TextAnchor.MiddleCenter;
                    titleTxt.resizeTextForBestFit = true;
                    titleTxt.resizeTextMinSize = 20;
                    titleTxt.resizeTextMaxSize = 36;
                    StyleLegacyTextCrisp(titleTxt, -1, new Color(0.97f, 0.98f, 1f, 1f), bold: true);
                }
            }

            Transform badgeTf = cardBtn.transform.Find("ModeBadge");
            if (badgeTf != null && badgeTf.TryGetComponent(out RectTransform badgeRt))
            {
                badgeRt.anchorMin = new Vector2(0.08f, 0.09f);
                badgeRt.anchorMax = new Vector2(0.92f, 0.20f);
                badgeRt.pivot = new Vector2(0.5f, 0.5f);
                badgeRt.offsetMin = Vector2.zero;
                badgeRt.offsetMax = Vector2.zero;
                if (badgeTf.TryGetComponent(out Text badgeTxt))
                {
                    badgeTxt.alignment = TextAnchor.MiddleCenter;
                    badgeTxt.resizeTextForBestFit = true;
                    badgeTxt.resizeTextMinSize = 14;
                    badgeTxt.resizeTextMaxSize = 22;
                    StyleLegacyTextCrisp(badgeTxt, -1, new Color(0.84f, 0.89f, 0.96f, 0.95f), bold: true);
                }
            }
        }

        private static void TransparentizeFullScreenBackdrops(GameObject panel)
        {
            if (panel == null) return;

            if (panel.TryGetComponent(out Image rootImg))
            {
                rootImg.color = Color.clear;
                rootImg.raycastTarget = false;
            }

            Image[] images = panel.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image childImg = images[i];
                if (childImg == null || childImg.transform == panel.transform) continue;

                string childName = childImg.name;
                if (string.Equals(childName, "MenuBackground", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(childName, "DarkOverlay", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(childName, "LockOverlay", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(childName, "GoldSelectionFrame", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(childName, "BgCharacterPreview", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (childImg.GetComponent<Selectable>() != null || childImg.GetComponent<Mask>() != null)
                {
                    continue;
                }

                bool looksLikeBackdrop =
                    childName.IndexOf("Backdrop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("Dimmer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("Overlay", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("PanelBg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("ScreenBg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("Fade", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!looksLikeBackdrop) continue;

                if (childImg.TryGetComponent(out RectTransform rt) &&
                    rt.anchorMin == Vector2.zero &&
                    rt.anchorMax == Vector2.one)
                {
                    childImg.color = Color.clear;
                    childImg.raycastTarget = false;
                }
            }
        }

        private static Sprite GetFallbackItemIcon(ConsumableItemType itemType)
        {
            return itemType switch
            {
                ConsumableItemType.SenzuBean => LoadItemIconSprite("Icon_SenzuBean.png"),
                ConsumableItemType.SpinalFluid => LoadItemIconSprite("Icon_SpinalFluid.png"),
                ConsumableItemType.UltraDivineWater => LoadItemIconSprite("Icon_UltraDivineWater.png"),
                _ => null
            };
        }

        private void RefreshWorldCardsUI()
        {
            if (worldCards == null) return;
            if (charSlotGlassSprite == null) charSlotGlassSprite = LoadGlassSprite("CharSlot_Glass.png", CharSlotSlice);
            if (charSlotGoldSelectedSprite == null) charSlotGoldSelectedSprite = LoadGlassSprite("CharSlot_GoldSelected.png", CharSlotSlice);

            for (int i = 0; i < worldCards.Length; i++)
            {
                MenuWorldCardUI wCard = worldCards[i];
                if (wCard == null) continue;

                bool isSelected = i == selectedWorldIndex;
                Transform cardRoot = wCard.highlightBorder != null
                    ? wCard.highlightBorder.transform
                    : (wCard.selectButton != null ? wCard.selectButton.transform.parent : null);

                if (cardRoot != null)
                {
                    cardRoot.localScale = isSelected ? Vector3.one * 1.05f : Vector3.one;
                }

                EnsureGoldSelectionFrame(cardRoot, null, isSelected, Application.isPlaying);

                if (wCard.selectButton != null && wCard.selectButton.TryGetComponent(out Image innerImg))
                {
                    innerImg.color = new Color(0.05f, 0.07f, 0.12f, 0.36f);
                }

                if (wCard.previewImage != null && wCard.worldBackgroundSprite != null)
                {
                    wCard.previewImage.sprite = wCard.worldBackgroundSprite;
                }

                if (wCard.highlightBorder != null)
                {
                    string worldSpriteName = GetWorldCardSpriteName(i);
                    if (TryApplyHudSprite(wCard.highlightBorder, worldSpriteName, sliced: true, preserveAspect: false))
                    {
                        wCard.highlightBorder.color = Color.white;
                    }
                    else if (charSlotGlassSprite != null || charSlotGoldSelectedSprite != null)
                    {
                        Sprite frame = isSelected
                            ? (charSlotGoldSelectedSprite ?? charSlotGlassSprite)
                            : (charSlotGlassSprite ?? charSlotGoldSelectedSprite);
                        ApplyGlassImageStyle(wCard.highlightBorder, frame, sliced: true, preserveAspect: false);
                    }
                    else
                    {
                        wCard.highlightBorder.color = isSelected ? SelectedHighlightColor : IdleBorderColor;
                    }
                }

                if (wCard.selectButton != null)
                {
                    Transform titleTf = wCard.selectButton.transform.Find("WorldTitle");
                    if (titleTf != null && titleTf.TryGetComponent(out Text titleTxt))
                    {
                        StyleLegacyTextCrisp(titleTxt, -1, titleTxt.color, bold: true);
                    }
                }

                if (wCard.selectedBadge != null)
                {
                    wCard.selectedBadge.SetActive(isSelected);
                }
            }
        }

        private void RefreshStageCardsUI()
        {
            if (stageButtons == null) return;

            string worldSpriteName = GetWorldCardSpriteName(selectedWorldIndex);
            string[] stageRoleTags = { "[MELEE]", "[RANGED]", "[ELITE]", "[BOSS]" };

            for (int i = 0; i < stageButtons.Length; i++)
            {
                Button sBtn = stageButtons[i];
                if (sBtn == null) continue;

                bool isBoss = i == 3 || i == stageButtons.Length - 1;
                if (sBtn.TryGetComponent(out Image sImg))
                {
                    if (isBoss)
                    {
                        if (TryApplyHudSprite(sImg, "ModalCard_Victory_Gold.png", sliced: true, preserveAspect: false))
                        {
                            sImg.color = Color.white;
                        }
                        else if (TryApplyHudSprite(sImg, worldSpriteName, sliced: true, preserveAspect: false))
                        {
                            sImg.color = Color.white;
                        }
                        else if (charSlotGoldSelectedSprite != null || charSlotGlassSprite != null)
                        {
                            ApplyGlassImageStyle(sImg, charSlotGoldSelectedSprite ?? charSlotGlassSprite, sliced: true, preserveAspect: false);
                        }
                    }
                    else
                    {
                        if (TryApplyHudSprite(sImg, worldSpriteName, sliced: true, preserveAspect: false))
                        {
                            sImg.color = Color.white;
                        }
                        else if (charSlotGlassSprite != null || charSlotGoldSelectedSprite != null)
                        {
                            ApplyGlassImageStyle(sImg, charSlotGlassSprite ?? charSlotGoldSelectedSprite, sliced: true, preserveAspect: false);
                        }
                    }
                }

                EnsureGoldSelectionFrame(sBtn.transform, null, isBoss, Application.isPlaying);
                EnsureButtonHoverEffect(sBtn, 6f, 1.04f, 14f);

                Transform stageNameTf = sBtn.transform.Find("StageName");
                Text stageTitleTxt = stageNameTf != null
                    ? stageNameTf.GetComponent<Text>()
                    : sBtn.GetComponentInChildren<Text>(true);

                if (stageTitleTxt != null)
                {
                    string roleTag = stageRoleTags[Mathf.Clamp(i, 0, stageRoleTags.Length - 1)];
                    stageTitleTxt.supportRichText = true;
                    stageTitleTxt.lineSpacing = 1.08f;
                    stageTitleTxt.text = $"STAGE {i + 1}\n<size=24>{roleTag}</size>";
                    Color textColor = isBoss ? new Color(1f, 0.90f, 0.34f, 1f) : Color.white;
                    StyleLegacyTextCrisp(stageTitleTxt, -1, textColor, bold: true);
                }
            }
        }

        private void RefreshStageHeaderUI()
        {
            if (stageSelectionHeaderText != null)
            {
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
                StyleLegacyTextCrisp(stageSelectionHeaderText, -1, stageSelectionHeaderText.color, bold: true);
            }

            RefreshStageCardsUI();
        }
    }
}
