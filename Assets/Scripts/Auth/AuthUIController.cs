using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StickmanBeatEmUp.UI;

namespace StickmanBeatEmUp.Auth
{
    public enum AuthTabMode
    {
        Login = 0,
        Register = 1
    }

    /// <summary>
    /// Điều khiển panel Đăng nhập & Đăng ký (04_AuthRegisterLogin) và cung cấp pure-runtime builders
    /// dùng chung cho cả MainMenuSceneBuilder (Editor) và UIManager (Runtime fallback).
    /// </summary>
    public class AuthUIController : MonoBehaviour
    {
        [Serializable]
        public struct AuthThemeSprites
        {
            public Sprite modalPanelSprite;
            public Sprite tabLoginInactiveSprite;
            public Sprite tabRegisterActiveGoldSprite;
            public Sprite inputFieldBlankSprite;
            public Sprite userIconSprite;
            public Sprite lockIconSprite;
            public Sprite primaryButtonSprite;
            public Sprite secondaryButtonSprite;
            public Sprite accountPillBgSprite;
            public Sprite inputUsernameCleanBackup;
            public Sprite inputPasswordCleanBackup;
        }

        public struct AccountStatusPillRefs
        {
            public GameObject pillRoot;
            public Button pillButton;
            public Image userIcon;
            public TMP_Text statusText;
            public Button actionButton;
            public TMP_Text actionLabel;
            public Button logoutButton;
        }

        private static readonly Color ActiveTabLabelColor = new Color(0.996f, 0.953f, 0.780f, 1f);   // #FEF3C7
        private static readonly Color InactiveTabLabelColor = new Color(0.796f, 0.835f, 0.882f, 1f); // #CBD5E1
        private static readonly Color ErrorFeedbackColor = new Color(0.984f, 0.443f, 0.522f, 1f);    // #FB7185
        private static readonly Color SuccessFeedbackColor = new Color(0.204f, 0.827f, 0.600f, 1f);  // #34D399
        private static readonly Color SessionBannerColor = new Color(0.404f, 0.910f, 0.976f, 1f);    // #67E8F9

        private static readonly Vector2 ActiveTabSize = new Vector2(388f, 146f);
        private static readonly Vector2 InactiveTabSize = new Vector2(356f, 114f);

        [Header("Modal & Controls")]
        [SerializeField] private Image modalFrameImage;
        [SerializeField] private Button loginTabButton;
        [SerializeField] private TMP_Text loginTabLabel;
        [SerializeField] private Button registerTabButton;
        [SerializeField] private TMP_Text registerTabLabel;

        [Header("Active Session Banner")]
        [SerializeField] private GameObject activeSessionBanner;
        [SerializeField] private TMP_Text activeSessionText;
        [SerializeField] private Button logoutButton;

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField confirmPasswordInput;
        [SerializeField] private Image usernameFieldBg;
        [SerializeField] private Image passwordFieldBg;
        [SerializeField] private Image confirmFieldBg;
        [SerializeField] private Image usernameIconImage;
        [SerializeField] private Image passwordIconImage;
        [SerializeField] private Image confirmIconImage;

        [Header("Action & Feedback")]
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text submitButtonLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text backButtonLabel;

        [Header("Serialized Sprites (04_AuthRegisterLogin)")]
        [SerializeField] private Sprite modalPanelSprite;
        [SerializeField] private Sprite tabLoginInactiveSprite;
        [SerializeField] private Sprite tabRegisterActiveGoldSprite;
        [SerializeField] private Sprite inputFieldBlankSprite;
        [SerializeField] private Sprite userIconSprite;
        [SerializeField] private Sprite lockIconSprite;
        [SerializeField] private Sprite primaryButtonSprite;
        [SerializeField] private Sprite secondaryButtonSprite;
        [SerializeField] private Sprite inputUsernameCleanBackup;
        [SerializeField] private Sprite inputPasswordCleanBackup;

        private AuthTabMode currentTab = AuthTabMode.Register;
        private Action onAuthenticatedCallback;
        private Action onBackCallback;
        private bool isBound;

        public AuthTabMode CurrentTab => currentTab;
        public TMP_InputField UsernameInput => usernameInput;
        public TMP_InputField PasswordInput => passwordInput;
        public TMP_InputField ConfirmPasswordInput => confirmPasswordInput;
        public Button SubmitButton => submitButton;
        public Button BackButton => backButton;
        public Button LoginTabButton => loginTabButton;
        public Button RegisterTabButton => registerTabButton;
        public Button LogoutButton => logoutButton;
        public TMP_Text FeedbackText => feedbackText;

        private void Awake()
        {
            BindListeners();
            if (Application.isPlaying)
            {
                ResolveThemeSprites();
                ApplyVisualTheme();
                TMP_FontAsset sharedFont = GlassHudTheme.ResolveSharedTmpFont(this);
                if (sharedFont != null)
                {
                    ApplyFont(sharedFont);
                }
            }
        }

        private void OnEnable()
        {
            BindListeners();
            if (Application.isPlaying)
            {
                ResolveThemeSprites();
                ApplyVisualTheme();
                RefreshSessionBanner();
            }
        }

        public void ConfigureReferences(
            Image modalFrame,
            Button loginTabBtn,
            TMP_Text loginTabTxt,
            Button registerTabBtn,
            TMP_Text registerTabTxt,
            GameObject sessionBanner,
            TMP_Text sessionTxt,
            Button sessionLogoutBtn,
            TMP_InputField userInput,
            TMP_InputField passInput,
            TMP_InputField confirmInput,
            Image userBg,
            Image passBg,
            Image confirmBg,
            Image userIcon,
            Image passIcon,
            Image confirmIcon,
            TMP_Text feedbackTxt,
            Button submitBtn,
            TMP_Text submitTxt,
            Button backBtn,
            TMP_Text backTxt)
        {
            modalFrameImage = modalFrame;
            loginTabButton = loginTabBtn;
            loginTabLabel = loginTabTxt;
            registerTabButton = registerTabBtn;
            registerTabLabel = registerTabTxt;
            activeSessionBanner = sessionBanner;
            activeSessionText = sessionTxt;
            logoutButton = sessionLogoutBtn;
            usernameInput = userInput;
            passwordInput = passInput;
            confirmPasswordInput = confirmInput;
            usernameFieldBg = userBg;
            passwordFieldBg = passBg;
            confirmFieldBg = confirmBg;
            usernameIconImage = userIcon;
            passwordIconImage = passIcon;
            confirmIconImage = confirmIcon;
            feedbackText = feedbackTxt;
            submitButton = submitBtn;
            submitButtonLabel = submitTxt;
            backButton = backBtn;
            backButtonLabel = backTxt;

            isBound = false;
            BindListeners();
            SetTab(AuthTabMode.Register);
        }

        public void ConfigureThemeSprites(AuthThemeSprites sprites)
        {
            if (sprites.modalPanelSprite != null) modalPanelSprite = sprites.modalPanelSprite;
            if (sprites.tabLoginInactiveSprite != null) tabLoginInactiveSprite = sprites.tabLoginInactiveSprite;
            if (sprites.tabRegisterActiveGoldSprite != null) tabRegisterActiveGoldSprite = sprites.tabRegisterActiveGoldSprite;
            if (sprites.inputFieldBlankSprite != null) inputFieldBlankSprite = sprites.inputFieldBlankSprite;
            if (sprites.userIconSprite != null) userIconSprite = sprites.userIconSprite;
            if (sprites.lockIconSprite != null) lockIconSprite = sprites.lockIconSprite;
            if (sprites.primaryButtonSprite != null) primaryButtonSprite = sprites.primaryButtonSprite;
            if (sprites.secondaryButtonSprite != null) secondaryButtonSprite = sprites.secondaryButtonSprite;
            if (sprites.inputUsernameCleanBackup != null) inputUsernameCleanBackup = sprites.inputUsernameCleanBackup;
            if (sprites.inputPasswordCleanBackup != null) inputPasswordCleanBackup = sprites.inputPasswordCleanBackup;

            ApplyVisualTheme();
        }

        public void ApplyFont(TMP_FontAsset font)
        {
            if (font == null) return;

            TMP_Text[] allTexts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] != null)
                {
                    allTexts[i].font = font;
                }
            }

            ApplyFontToInputField(usernameInput, font);
            ApplyFontToInputField(passwordInput, font);
            ApplyFontToInputField(confirmPasswordInput, font);
        }

        public void Open(bool startInRegisterTab, Action onAuthenticated, Action onBack)
        {
            onAuthenticatedCallback = onAuthenticated;
            onBackCallback = onBack;

            ResolveThemeSprites();
            ApplyVisualTheme();

            if (usernameInput != null) usernameInput.SetTextWithoutNotify(string.Empty);
            if (passwordInput != null) passwordInput.SetTextWithoutNotify(string.Empty);
            if (confirmPasswordInput != null) confirmPasswordInput.SetTextWithoutNotify(string.Empty);

            AccountManager mgr = Application.isPlaying ? AccountManager.Instance : null;
            if (mgr != null && mgr.HasActiveSession && mgr.ActiveAccount != null && usernameInput != null)
            {
                usernameInput.SetTextWithoutNotify(mgr.ActiveAccount.username);
            }

            SetTab(startInRegisterTab ? AuthTabMode.Register : AuthTabMode.Login);
            RefreshSessionBanner();
            SetFeedback(string.Empty, false);
        }

        public void SetTab(AuthTabMode mode)
        {
            currentTab = mode;
            bool isRegister = mode == AuthTabMode.Register;

            if (confirmPasswordInput != null)
            {
                confirmPasswordInput.gameObject.SetActive(isRegister);
            }

            if (submitButtonLabel != null)
            {
                submitButtonLabel.text = isRegister ? "ĐĂNG KÝ" : "ĐĂNG NHẬP";
            }

            if (passwordInput != null) passwordInput.SetTextWithoutNotify(string.Empty);
            if (confirmPasswordInput != null) confirmPasswordInput.SetTextWithoutNotify(string.Empty);
            SetFeedback(string.Empty, false);

            UpdateTabVisuals();
        }

        public void RefreshSessionBanner()
        {
            AccountManager mgr = (Application.isPlaying || AccountManager.HasInstance)
                ? AccountManager.Instance
                : null;
            bool hasSession = mgr != null && mgr.HasActiveSession && mgr.ActiveAccount != null;

            if (activeSessionBanner != null)
            {
                activeSessionBanner.SetActive(hasSession);
            }

            if (hasSession && activeSessionText != null)
            {
                activeSessionText.text = $"Đang đăng nhập: {mgr.ActiveAccount.username}";
                activeSessionText.color = SessionBannerColor;
            }
        }

        public void ResolveThemeSprites()
        {
            if (modalPanelSprite == null)
                modalPanelSprite = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Frame_Auth_ModalPanel_Clean");
            if (tabLoginInactiveSprite == null)
                tabLoginInactiveSprite = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Tab_Auth_Login_Inactive_Clean");
            if (tabRegisterActiveGoldSprite == null)
                tabRegisterActiveGoldSprite = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Tab_Auth_Register_ActiveGold_Clean");
            if (inputFieldBlankSprite == null)
                inputFieldBlankSprite = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Input_Auth_Field_Blank");
            if (userIconSprite == null)
                userIconSprite = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Icon_Auth_User");
            if (lockIconSprite == null)
                lockIconSprite = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Icon_Auth_Lock");
            if (primaryButtonSprite == null)
                primaryButtonSprite = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Btn_Auth_RegisterPrimary_Clean");
            if (secondaryButtonSprite == null)
                secondaryButtonSprite = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Btn_Auth_BackSecondary_Clean");
            if (inputUsernameCleanBackup == null)
                inputUsernameCleanBackup = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Input_Auth_Username_Clean");
            if (inputPasswordCleanBackup == null)
                inputPasswordCleanBackup = GlassHudTheme.LoadSprite("04_AuthRegisterLogin/Input_Auth_Password_Clean");
        }

        public void ApplyVisualTheme()
        {
            if (modalFrameImage != null && modalPanelSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(modalFrameImage, modalPanelSprite, 1060f, 720f, Color.white);
            }

            ApplyInputVisual(usernameFieldBg, usernameIconImage, userIconSprite, isLeftIcon: true);
            ApplyInputVisual(passwordFieldBg, passwordIconImage, lockIconSprite, isLeftIcon: false);
            ApplyInputVisual(confirmFieldBg, confirmIconImage, lockIconSprite, isLeftIcon: false);

            if (submitButton != null && primaryButtonSprite != null && submitButton.TryGetComponent(out Image subImg))
            {
                GlassHudTheme.ApplySlicedSprite(subImg, primaryButtonSprite, 376f, 120f, Color.white);
            }

            if (backButton != null && secondaryButtonSprite != null && backButton.TryGetComponent(out Image backImg))
            {
                GlassHudTheme.ApplySlicedSprite(backImg, secondaryButtonSprite, 312f, 98f, Color.white);
            }

            if (logoutButton != null && secondaryButtonSprite != null && logoutButton.TryGetComponent(out Image logoutImg))
            {
                GlassHudTheme.ApplySlicedSprite(logoutImg, secondaryButtonSprite, 156f, 34f, Color.white);
            }

            if (submitButtonLabel != null)
            {
                GlassHudTheme.StyleTmpText(submitButtonLabel, 28f, Color.white, bold: true, addShadow: true);
            }

            if (backButtonLabel != null)
            {
                GlassHudTheme.StyleTmpText(backButtonLabel, 24f, new Color(0.886f, 0.910f, 0.941f, 1f), bold: true, addShadow: true);
            }

            UpdateTabVisuals();
        }

        private void ApplyInputVisual(Image fieldBg, Image iconImg, Sprite iconSprite, bool isLeftIcon)
        {
            if (fieldBg != null)
            {
                if (inputFieldBlankSprite != null)
                {
                    GlassHudTheme.ApplySlicedSprite(fieldBg, inputFieldBlankSprite, 740f, 104f, Color.white);
                }
                else
                {
                    Sprite fallback = isLeftIcon ? inputUsernameCleanBackup : inputPasswordCleanBackup;
                    if (fallback != null)
                    {
                        GlassHudTheme.ApplySlicedSprite(fieldBg, fallback, 740f, 104f, Color.white);
                    }
                }
            }

            if (iconImg != null)
            {
                if (iconSprite != null)
                {
                    iconImg.sprite = iconSprite;
                    iconImg.type = Image.Type.Simple;
                    iconImg.preserveAspect = true;
                    iconImg.color = Color.white;
                    iconImg.enabled = true;
                }
                else
                {
                    iconImg.enabled = false;
                }
            }
        }

        private void UpdateTabVisuals()
        {
            bool isRegister = currentTab == AuthTabMode.Register;
            ApplyTabVisual(loginTabButton, loginTabLabel, isActive: !isRegister);
            ApplyTabVisual(registerTabButton, registerTabLabel, isActive: isRegister);

            Button activeBtn = isRegister ? registerTabButton : loginTabButton;
            if (activeBtn != null)
            {
                activeBtn.transform.SetAsLastSibling();
            }
        }

        private void ApplyTabVisual(Button tabBtn, TMP_Text tabLabel, bool isActive)
        {
            if (tabBtn == null) return;

            Vector2 size = isActive ? ActiveTabSize : InactiveTabSize;
            if (tabBtn.TryGetComponent(out RectTransform rt))
            {
                rt.sizeDelta = size;
            }

            Sprite sp = isActive
                ? (tabRegisterActiveGoldSprite ?? tabLoginInactiveSprite)
                : (tabLoginInactiveSprite ?? tabRegisterActiveGoldSprite);

            if (sp != null && tabBtn.TryGetComponent(out Image bg))
            {
                GlassHudTheme.ApplySlicedSprite(bg, sp, size.x, size.y, Color.white);
            }

            if (tabLabel != null)
            {
                Color textColor = isActive ? ActiveTabLabelColor : InactiveTabLabelColor;
                GlassHudTheme.StyleTmpText(tabLabel, 26f, textColor, bold: true, addShadow: true);
            }
        }

        private void BindListeners()
        {
            if (isBound) return;

            bool anyBound = false;

            if (loginTabButton != null)
            {
                loginTabButton.onClick.RemoveAllListeners();
                loginTabButton.onClick.AddListener(() => SetTab(AuthTabMode.Login));
                anyBound = true;
            }

            if (registerTabButton != null)
            {
                registerTabButton.onClick.RemoveAllListeners();
                registerTabButton.onClick.AddListener(() => SetTab(AuthTabMode.Register));
                anyBound = true;
            }

            if (submitButton != null)
            {
                submitButton.onClick.RemoveAllListeners();
                submitButton.onClick.AddListener(HandleSubmitClicked);
                anyBound = true;
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(HandleBackClicked);
                anyBound = true;
            }

            if (logoutButton != null)
            {
                logoutButton.onClick.RemoveAllListeners();
                logoutButton.onClick.AddListener(HandleLogoutClicked);
                anyBound = true;
            }

            isBound = anyBound;
        }

        private void HandleSubmitClicked()
        {
            AccountManager mgr = AccountManager.Instance;
            if (mgr == null) return;

            string user = usernameInput != null ? usernameInput.text : string.Empty;
            string pass = passwordInput != null ? passwordInput.text : string.Empty;
            string confirm = confirmPasswordInput != null ? confirmPasswordInput.text : string.Empty;

            bool success;
            string errorMessage;
            if (currentTab == AuthTabMode.Register)
            {
                success = mgr.TryRegister(user, pass, confirm, out errorMessage);
            }
            else
            {
                success = mgr.TryLogin(user, pass, out errorMessage);
            }

            if (success)
            {
                if (passwordInput != null) passwordInput.SetTextWithoutNotify(string.Empty);
                if (confirmPasswordInput != null) confirmPasswordInput.SetTextWithoutNotify(string.Empty);
                RefreshSessionBanner();
                SetFeedback(
                    currentTab == AuthTabMode.Register ? "Đăng ký thành công!" : "Đăng nhập thành công!",
                    isSuccess: true);
                onAuthenticatedCallback?.Invoke();
            }
            else
            {
                SetFeedback(errorMessage, isSuccess: false);
            }
        }

        private void HandleBackClicked()
        {
            onBackCallback?.Invoke();
        }

        private void HandleLogoutClicked()
        {
            if (AccountManager.HasInstance)
            {
                AccountManager.Instance.Logout();
            }
            else if (AccountManager.Instance != null)
            {
                AccountManager.Instance.Logout();
            }

            if (usernameInput != null) usernameInput.SetTextWithoutNotify(string.Empty);
            if (passwordInput != null) passwordInput.SetTextWithoutNotify(string.Empty);
            if (confirmPasswordInput != null) confirmPasswordInput.SetTextWithoutNotify(string.Empty);
            RefreshSessionBanner();
            SetFeedback("Đã đăng xuất tài khoản.", isSuccess: true);
        }

        private void SetFeedback(string message, bool isSuccess)
        {
            if (feedbackText == null) return;
            feedbackText.text = message ?? string.Empty;
            feedbackText.color = isSuccess ? SuccessFeedbackColor : ErrorFeedbackColor;
        }

        private static void ApplyFontToInputField(TMP_InputField field, TMP_FontAsset font)
        {
            if (field == null || font == null) return;
            field.fontAsset = font;
            if (field.textComponent != null)
            {
                field.textComponent.font = font;
            }
            if (field.placeholder is TMP_Text placeholderTmp)
            {
                placeholderTmp.font = font;
            }
        }

        // ----------------------------------------------------------------
        // Pure-Runtime Shared Builders (Lockstep Editor + Runtime Fallback)
        // ----------------------------------------------------------------

        public static AuthUIController BuildAuthPanel(
            Transform canvasTransform,
            AuthThemeSprites sprites,
            TMP_FontAsset tmpFont)
        {
            GameObject panelObj = CreateFullRect("SubScreen_Auth", canvasTransform);
            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.color = Color.clear;
            panelBg.raycastTarget = false;

            GameObject scrimObj = CreateFullRect("AuthModalScrim", panelObj.transform);
            Image scrimImg = scrimObj.AddComponent<Image>();
            scrimImg.color = new Color(0.02f, 0.03f, 0.07f, 0.38f);
            scrimImg.raycastTarget = true;

            GameObject modalObj = CreateUIObject("AuthModalContainer", panelObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -28f), new Vector2(1060f, 720f));
            Image modalFrameImg = modalObj.AddComponent<Image>();
            if (sprites.modalPanelSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(modalFrameImg, sprites.modalPanelSprite, 1060f, 720f, Color.white);
            }
            else
            {
                modalFrameImg.color = new Color(0.11f, 0.14f, 0.22f, 0.96f);
            }

            Button loginTabBtn = CreateAuthButton(
                "TabLoginButton",
                modalObj.transform,
                new Vector2(-198f, 362f),
                InactiveTabSize,
                "ĐĂNG NHẬP",
                26f,
                InactiveTabLabelColor,
                sprites.tabLoginInactiveSprite,
                new Color(0.18f, 0.22f, 0.32f, 0.96f),
                hoverScale: 1.02f,
                out TMP_Text loginTabTxt);

            Button registerTabBtn = CreateAuthButton(
                "TabRegisterButton",
                modalObj.transform,
                new Vector2(198f, 362f),
                ActiveTabSize,
                "ĐĂNG KÝ",
                26f,
                ActiveTabLabelColor,
                sprites.tabRegisterActiveGoldSprite,
                new Color(0.55f, 0.39f, 0.10f, 0.98f),
                hoverScale: 1.02f,
                out TMP_Text registerTabTxt);

            // Active Session Banner (visible only when logged in)
            GameObject sessionBannerObj = CreateUIObject("ActiveSessionBanner", modalObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 266f), new Vector2(740f, 36f));
            GameObject sessionTxtObj = CreateUIObject("ActiveSessionText", sessionBannerObj.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            RectTransform sessionTxtRt = sessionTxtObj.GetComponent<RectTransform>();
            sessionTxtRt.offsetMin = new Vector2(0f, 0f);
            sessionTxtRt.offsetMax = new Vector2(-168f, 0f);
            TextMeshProUGUI sessionTmp = sessionTxtObj.AddComponent<TextMeshProUGUI>();
            sessionTmp.text = string.Empty;
            sessionTmp.fontSize = 20f;
            sessionTmp.alignment = TextAlignmentOptions.MidlineLeft;
            sessionTmp.fontStyle = FontStyles.Bold;
            sessionTmp.color = SessionBannerColor;
            sessionTmp.overflowMode = TextOverflowModes.Ellipsis;
            sessionTmp.raycastTarget = false;
            if (tmpFont != null) sessionTmp.font = tmpFont;
            GlassHudTheme.StyleTmpText(sessionTmp, 20f, SessionBannerColor, bold: true, addShadow: true);

            Button logoutBtn = CreateAuthButton(
                "AuthLogoutButton",
                sessionBannerObj.transform,
                new Vector2(292f, 0f),
                new Vector2(156f, 34f),
                "ĐĂNG XUẤT",
                17f,
                new Color(0.97f, 0.98f, 0.99f, 1f),
                sprites.secondaryButtonSprite,
                new Color(0.42f, 0.18f, 0.22f, 0.95f),
                hoverScale: 1.035f,
                out TMP_Text _);
            sessionBannerObj.SetActive(false);

            // Input 1: Username (LeftIcon)
            TMP_InputField userInput = CreateAuthInputField(
                "InputUsername",
                modalObj.transform,
                new Vector2(0f, 194f),
                "TÊN ĐĂNG NHẬP",
                isPassword: false,
                characterLimit: 20,
                isLeftIcon: true,
                sprites.inputFieldBlankSprite,
                sprites.userIconSprite,
                tmpFont,
                out Image userBg,
                out Image userIcon);

            // Input 2: Password (RightIcon)
            TMP_InputField passInput = CreateAuthInputField(
                "InputPassword",
                modalObj.transform,
                new Vector2(0f, 74f),
                "MẬT KHẨU",
                isPassword: true,
                characterLimit: 32,
                isLeftIcon: false,
                sprites.inputFieldBlankSprite,
                sprites.lockIconSprite,
                tmpFont,
                out Image passBg,
                out Image passIcon);

            // Input 3: Confirm Password (RightIcon, Register mode only)
            TMP_InputField confirmInput = CreateAuthInputField(
                "InputConfirmPassword",
                modalObj.transform,
                new Vector2(0f, -46f),
                "NHẬP LẠI MẬT KHẨU",
                isPassword: true,
                characterLimit: 32,
                isLeftIcon: false,
                sprites.inputFieldBlankSprite,
                sprites.lockIconSprite,
                tmpFont,
                out Image confirmBg,
                out Image confirmIcon);

            // Feedback Label
            GameObject feedbackObj = CreateUIObject("AuthFeedbackText", modalObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -132f), new Vector2(760f, 44f));
            TextMeshProUGUI feedbackTmp = feedbackObj.AddComponent<TextMeshProUGUI>();
            feedbackTmp.text = string.Empty;
            feedbackTmp.fontSize = 21f;
            feedbackTmp.alignment = TextAlignmentOptions.Center;
            feedbackTmp.fontStyle = FontStyles.Bold;
            feedbackTmp.color = ErrorFeedbackColor;
            feedbackTmp.raycastTarget = false;
            if (tmpFont != null) feedbackTmp.font = tmpFont;
            GlassHudTheme.StyleTmpText(feedbackTmp, 21f, ErrorFeedbackColor, bold: true, addShadow: true);

            // Action Buttons
            Button submitBtn = CreateAuthButton(
                "AuthSubmitButton",
                modalObj.transform,
                new Vector2(-168f, -234f),
                new Vector2(376f, 120f),
                "ĐĂNG KÝ",
                28f,
                Color.white,
                sprites.primaryButtonSprite,
                new Color(0.18f, 0.52f, 0.78f, 0.98f),
                hoverScale: 1.035f,
                out TMP_Text submitTxt);

            Button backBtn = CreateAuthButton(
                "AuthBackButton",
                modalObj.transform,
                new Vector2(196f, -234f),
                new Vector2(312f, 98f),
                "< QUAY LẠI",
                24f,
                new Color(0.886f, 0.910f, 0.941f, 1f),
                sprites.secondaryButtonSprite,
                new Color(0.22f, 0.25f, 0.34f, 0.95f),
                hoverScale: 1.035f,
                out TMP_Text backTxt);

            AuthUIController controller = panelObj.AddComponent<AuthUIController>();
            controller.ConfigureThemeSprites(sprites);
            controller.ConfigureReferences(
                modalFrameImg,
                loginTabBtn, loginTabTxt,
                registerTabBtn, registerTabTxt,
                sessionBannerObj, sessionTmp, logoutBtn,
                userInput, passInput, confirmInput,
                userBg, passBg, confirmBg,
                userIcon, passIcon, confirmIcon,
                feedbackTmp,
                submitBtn, submitTxt,
                backBtn, backTxt);
            if (tmpFont != null)
            {
                controller.ApplyFont(tmpFont);
            }

            return controller;
        }

        public static AccountStatusPillRefs BuildAccountStatusPill(
            Transform mainMenuTransform,
            Sprite pillBgSprite,
            Sprite userIconSprite,
            Sprite actionBtnSprite,
            TMP_FontAsset tmpFont)
        {
            GameObject pillObj = CreateUIObject("AccountStatusPill", mainMenuTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -24f), new Vector2(540f, 74f));

            Image pillBg = pillObj.AddComponent<Image>();
            if (pillBgSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(pillBg, pillBgSprite, 540f, 74f, Color.white);
            }
            else
            {
                pillBg.color = new Color(0.10f, 0.12f, 0.19f, 0.92f);
            }

            Button pillBtn = pillObj.AddComponent<Button>();
            pillBtn.targetGraphic = pillBg;
            pillBtn.transition = Selectable.Transition.None;
            EnsureHoverEffect(pillBtn, 0f, 1.015f, 15f);

            GameObject iconObj = CreateUIObject("UserIcon", pillObj.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(32f, 0f), new Vector2(32f, 36f));
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = userIconSprite;
            iconImg.type = Image.Type.Simple;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.color = new Color(0.580f, 0.639f, 0.722f, 1f);
            iconImg.enabled = userIconSprite != null;

            GameObject statusTxtObj = CreateUIObject("AccountStatusText", pillObj.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            RectTransform statusRt = statusTxtObj.GetComponent<RectTransform>();
            statusRt.offsetMin = new Vector2(58f, 6f);
            statusRt.offsetMax = new Vector2(-162f, -6f);
            TextMeshProUGUI statusTmp = statusTxtObj.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "Chưa đăng nhập";
            statusTmp.fontSize = 19f;
            statusTmp.enableAutoSizing = true;
            statusTmp.fontSizeMin = 13f;
            statusTmp.fontSizeMax = 19f;
            statusTmp.alignment = TextAlignmentOptions.MidlineLeft;
            statusTmp.textWrappingMode = TextWrappingModes.NoWrap;
            statusTmp.overflowMode = TextOverflowModes.Ellipsis;
            statusTmp.fontStyle = FontStyles.Bold;
            statusTmp.raycastTarget = false;
            if (tmpFont != null) statusTmp.font = tmpFont;
            GlassHudTheme.StyleTmpText(statusTmp, 19f, new Color(0.984f, 0.749f, 0.141f, 1f), bold: true, addShadow: true);

            GameObject actionBtnObj = CreateUIObject("AccountActionButton", pillObj.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-14f, 0f), new Vector2(138f, 44f));
            Image actionBg = actionBtnObj.AddComponent<Image>();
            if (actionBtnSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(actionBg, actionBtnSprite, 138f, 44f, Color.white);
            }
            else
            {
                actionBg.color = new Color(0.20f, 0.28f, 0.42f, 0.96f);
            }
            Button actionBtn = actionBtnObj.AddComponent<Button>();
            actionBtn.targetGraphic = actionBg;
            EnsureHoverEffect(actionBtn, 0f, 1.035f, 15f);

            GameObject actionLblObj = CreateFullRect("Label", actionBtnObj.transform);
            TextMeshProUGUI actionLbl = actionLblObj.AddComponent<TextMeshProUGUI>();
            actionLbl.text = "ĐĂNG NHẬP";
            actionLbl.fontSize = 16f;
            actionLbl.alignment = TextAlignmentOptions.Center;
            actionLbl.fontStyle = FontStyles.Bold;
            actionLbl.raycastTarget = false;
            if (tmpFont != null) actionLbl.font = tmpFont;
            GlassHudTheme.StyleTmpText(actionLbl, 16f, new Color(0.973f, 0.980f, 0.988f, 1f), bold: true, addShadow: true);

            GameObject logoutBtnObj = CreateUIObject("AccountLogoutButton", pillObj.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(106f, 44f));
            Image logoutBg = logoutBtnObj.AddComponent<Image>();
            if (actionBtnSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(logoutBg, actionBtnSprite, 106f, 44f, new Color(1f, 0.85f, 0.85f, 1f));
            }
            else
            {
                logoutBg.color = new Color(0.45f, 0.16f, 0.20f, 0.96f);
            }
            Button logoutBtn = logoutBtnObj.AddComponent<Button>();
            logoutBtn.targetGraphic = logoutBg;
            EnsureHoverEffect(logoutBtn, 0f, 1.035f, 15f);

            GameObject logoutLblObj = CreateFullRect("Label", logoutBtnObj.transform);
            TextMeshProUGUI logoutLbl = logoutLblObj.AddComponent<TextMeshProUGUI>();
            logoutLbl.text = "ĐĂNG XUẤT";
            logoutLbl.fontSize = 15f;
            logoutLbl.alignment = TextAlignmentOptions.Center;
            logoutLbl.fontStyle = FontStyles.Bold;
            logoutLbl.raycastTarget = false;
            if (tmpFont != null) logoutLbl.font = tmpFont;
            GlassHudTheme.StyleTmpText(logoutLbl, 15f, new Color(0.988f, 0.647f, 0.647f, 1f), bold: true, addShadow: true);
            logoutBtnObj.SetActive(false);

            return new AccountStatusPillRefs
            {
                pillRoot = pillObj,
                pillButton = pillBtn,
                userIcon = iconImg,
                statusText = statusTmp,
                actionButton = actionBtn,
                actionLabel = actionLbl,
                logoutButton = logoutBtn
            };
        }

        private static TMP_InputField CreateAuthInputField(
            string name,
            Transform parent,
            Vector2 pos,
            string placeholderText,
            bool isPassword,
            int characterLimit,
            bool isLeftIcon,
            Sprite bgSprite,
            Sprite iconSprite,
            TMP_FontAsset tmpFont,
            out Image bgImg,
            out Image iconImg)
        {
            GameObject fieldObj = CreateUIObject(name, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, new Vector2(740f, 104f));

            bgImg = fieldObj.AddComponent<Image>();
            bgImg.raycastTarget = true;
            if (bgSprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(bgImg, bgSprite, 740f, 104f, Color.white);
            }
            else
            {
                bgImg.color = new Color(0.08f, 0.11f, 0.18f, 0.96f);
            }

            // Icon
            string iconObjName = isLeftIcon ? "LeftIcon" : "RightIcon";
            Vector2 iconAnchor = isLeftIcon ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            Vector2 iconPos = isLeftIcon ? new Vector2(58f, 0f) : new Vector2(-58f, 0f);
            Vector2 iconSize = isLeftIcon ? new Vector2(38f, 44f) : new Vector2(42f, 46f);

            GameObject iconObj = CreateUIObject(iconObjName, fieldObj.transform, iconAnchor, iconAnchor, new Vector2(0.5f, 0.5f), iconPos, iconSize);
            iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.type = Image.Type.Simple;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.color = Color.white;
            iconImg.enabled = iconSprite != null;

            // TextArea (RectMask2D)
            GameObject textAreaObj = CreateFullRect("TextArea", fieldObj.transform);
            RectTransform textAreaRt = textAreaObj.GetComponent<RectTransform>();
            if (isLeftIcon)
            {
                textAreaRt.offsetMin = new Vector2(102f, 16f);
                textAreaRt.offsetMax = new Vector2(-48f, -16f);
            }
            else
            {
                textAreaRt.offsetMin = new Vector2(58f, 16f);
                textAreaRt.offsetMax = new Vector2(-104f, -16f);
            }
            textAreaObj.AddComponent<RectMask2D>();

            GameObject placeholderObj = CreateFullRect("Placeholder", textAreaObj.transform);
            TextMeshProUGUI placeholderTmp = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholderTmp.text = placeholderText;
            placeholderTmp.fontSize = 24f;
            placeholderTmp.fontStyle = FontStyles.Bold;
            placeholderTmp.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderTmp.color = new Color(0.580f, 0.639f, 0.722f, 0.85f);
            placeholderTmp.textWrappingMode = TextWrappingModes.NoWrap;
            placeholderTmp.raycastTarget = false;
            if (tmpFont != null) placeholderTmp.font = tmpFont;

            GameObject textObj = CreateFullRect("Text", textAreaObj.transform);
            TextMeshProUGUI textTmp = textObj.AddComponent<TextMeshProUGUI>();
            textTmp.text = string.Empty;
            textTmp.fontSize = 24f;
            textTmp.fontStyle = FontStyles.Bold;
            textTmp.alignment = TextAlignmentOptions.MidlineLeft;
            textTmp.color = new Color(0.973f, 0.980f, 0.988f, 1f);
            textTmp.textWrappingMode = TextWrappingModes.NoWrap;
            textTmp.overflowMode = TextOverflowModes.Overflow;
            textTmp.raycastTarget = false;
            if (tmpFont != null) textTmp.font = tmpFont;

            TMP_InputField inputField = fieldObj.AddComponent<TMP_InputField>();
            inputField.targetGraphic = bgImg;
            inputField.textViewport = textAreaRt;
            inputField.textComponent = textTmp;
            inputField.placeholder = placeholderTmp;
            if (tmpFont != null) inputField.fontAsset = tmpFont;
            inputField.pointSize = 24f;
            inputField.characterLimit = characterLimit;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.contentType = isPassword
                ? TMP_InputField.ContentType.Password
                : TMP_InputField.ContentType.Standard;
            if (isPassword)
            {
                inputField.asteriskChar = '*';
            }
            inputField.caretColor = new Color(0.404f, 0.910f, 0.976f, 1f);
            inputField.customCaretColor = true;
            inputField.selectionColor = new Color(0.22f, 0.65f, 0.88f, 0.35f);

            return inputField;
        }

        private static Button CreateAuthButton(
            string name,
            Transform parent,
            Vector2 pos,
            Vector2 size,
            string label,
            float fontSize,
            Color labelColor,
            Sprite sprite,
            Color fallbackBgColor,
            float hoverScale,
            out TMP_Text labelTmp)
        {
            GameObject btnObj = CreateUIObject(name, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, size);

            Image bg = btnObj.AddComponent<Image>();
            if (sprite != null)
            {
                GlassHudTheme.ApplySlicedSprite(bg, sprite, size.x, size.y, Color.white);
            }
            else
            {
                bg.color = fallbackBgColor;
            }

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            EnsureHoverEffect(btn, 0f, hoverScale, 15f);

            GameObject lblObj = CreateFullRect("Label", btnObj.transform);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.offsetMin = new Vector2(12f, 4f);
            lblRt.offsetMax = new Vector2(-12f, -4f);

            TextMeshProUGUI tmp = lblObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = labelColor;
            tmp.raycastTarget = false;
            GlassHudTheme.StyleTmpText(tmp, fontSize, labelColor, bold: true, addShadow: true);

            labelTmp = tmp;
            return btn;
        }

        private static void EnsureHoverEffect(Button btn, float liftY, float scale, float speed)
        {
            if (btn == null) return;
            ModeCardHoverEffect hover = btn.GetComponent<ModeCardHoverEffect>();
            if (hover == null)
            {
                hover = btn.gameObject.AddComponent<ModeCardHoverEffect>();
            }
            hover.Configure(liftY, scale, speed);
        }

        private static GameObject CreateFullRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return obj;
        }

        private static GameObject CreateUIObject(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size)
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
    }
}
