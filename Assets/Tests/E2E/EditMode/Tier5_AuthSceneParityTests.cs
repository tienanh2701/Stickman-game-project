using System;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.EditMode
{
    public class Tier5_AuthSceneParityTests
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [Test]
        [Category("Tier5")]
        [Category("F7")]
        public void T5_A11_EditMode_MainMenuScene_Complete04AuthSprites_Fonts_And_GeometryNonOverlap()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Canvas canvas = EditModeTierSupport.FindRootCanvas(scene);
            Assert.IsNotNull(canvas, "Root Canvas missing in MainMenu.unity.");

            GameObject subScreenAuth = EditModeTierSupport.RequireUIElement(scene, "MainMenuCanvas/SubScreen_Auth");
            GameObject modalContainer = EditModeTierSupport.RequireUIElement(scene, "SubScreen_Auth/AuthModalContainer");
            GameObject accountPill = EditModeTierSupport.RequireUIElement(scene, "Screen1_MainMenu/AccountStatusPill");

            // 1. Verify all 8 used 04_AuthRegisterLogin sprites & Image.Types
            AssertImageSpriteAndType(modalContainer, "Frame_Auth_ModalPanel_Clean", Image.Type.Sliced, expectPreserveAspect: false);
            AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, "AuthModalContainer/TabLoginButton"), "Tab_Auth_Login_Inactive_Clean", Image.Type.Sliced, expectPreserveAspect: false);
            AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, "AuthModalContainer/TabRegisterButton"), "Tab_Auth_Register_ActiveGold_Clean", Image.Type.Sliced, expectPreserveAspect: false);

            string[] inputNames =
            {
                "AuthModalContainer/InputUsername",
                "AuthModalContainer/InputPassword",
                "AuthModalContainer/InputConfirmPassword",
            };
            for (int i = 0; i < inputNames.Length; i++)
            {
                AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, inputNames[i]), "Input_Auth_Field_Blank", Image.Type.Sliced, expectPreserveAspect: false);
            }

            AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, "InputUsername/LeftIcon"), "Icon_Auth_User", Image.Type.Simple, expectPreserveAspect: true);
            AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, "InputPassword/RightIcon"), "Icon_Auth_Lock", Image.Type.Simple, expectPreserveAspect: true);
            AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, "InputConfirmPassword/RightIcon"), "Icon_Auth_Lock", Image.Type.Simple, expectPreserveAspect: true);

            AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, "AuthModalContainer/AuthSubmitButton"), "Btn_Auth_RegisterPrimary_Clean", Image.Type.Sliced, expectPreserveAspect: false);
            AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, "AuthModalContainer/AuthBackButton"), "Btn_Auth_BackSecondary_Clean", Image.Type.Sliced, expectPreserveAspect: false);
            AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, "AccountStatusPill/UserIcon"), "Icon_Auth_User", Image.Type.Simple, expectPreserveAspect: true);
            AssertImageSpriteAndType(EditModeTierSupport.RequireUIElement(scene, "AccountStatusPill/AccountActionButton"), "Btn_Auth_BackSecondary_Clean", Image.Type.Sliced, expectPreserveAspect: false);

            // 2. Verify every TMP_Text and TMP_InputField (fontAsset, textComponent.font, placeholder.font) uses Vietnamese font
            AssertAllTmpFontsValid(subScreenAuth, "SubScreen_Auth");
            AssertAllTmpFontsValid(accountPill, "AccountStatusPill");

            // 3. Verify 1920x1080 bounds & non-overlap inside AuthModalContainer and AccountStatusPill
            Rect tabLogin = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AuthModalContainer/TabLoginButton");
            Rect tabRegister = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AuthModalContainer/TabRegisterButton");
            Rect sessionBanner = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AuthModalContainer/ActiveSessionBanner");
            Rect inputUser = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AuthModalContainer/InputUsername");
            Rect inputPass = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AuthModalContainer/InputPassword");
            Rect inputConfirm = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AuthModalContainer/InputConfirmPassword");
            Rect feedback = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AuthModalContainer/AuthFeedbackText");
            Rect submitBtn = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AuthModalContainer/AuthSubmitButton");
            Rect backBtn = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AuthModalContainer/AuthBackButton");

            Rect[] authElements = { tabLogin, tabRegister, sessionBanner, inputUser, inputPass, inputConfirm, feedback, submitBtn, backBtn };
            string[] authNames = { "TabLogin", "TabRegister", "ActiveSessionBanner", "InputUsername", "InputPassword", "InputConfirmPassword", "AuthFeedbackText", "AuthSubmitButton", "AuthBackButton" };
            for (int i = 0; i < authElements.Length; i++)
            {
                EditModeTierSupport.AssertRectInside1920x1080(authElements[i], authNames[i]);
            }

            EditModeTierSupport.AssertRectsDoNotOverlap(tabLogin, tabRegister, "TabLogin", "TabRegister");
            EditModeTierSupport.AssertRectsDoNotOverlap(submitBtn, backBtn, "AuthSubmitButton", "AuthBackButton");

            Rect[] verticalStack = { sessionBanner, inputUser, inputPass, inputConfirm, feedback, submitBtn };
            string[] stackNames = { "ActiveSessionBanner", "InputUsername", "InputPassword", "InputConfirmPassword", "AuthFeedbackText", "AuthSubmitButton" };
            for (int i = 0; i < verticalStack.Length; i++)
            {
                EditModeTierSupport.AssertRectsDoNotOverlap(tabLogin, verticalStack[i], "TabLogin", stackNames[i]);
                EditModeTierSupport.AssertRectsDoNotOverlap(tabRegister, verticalStack[i], "TabRegister", stackNames[i]);
                for (int j = i + 1; j < verticalStack.Length; j++)
                {
                    EditModeTierSupport.AssertRectsDoNotOverlap(verticalStack[i], verticalStack[j], stackNames[i], stackNames[j]);
                }
            }
            EditModeTierSupport.AssertRectsDoNotOverlap(feedback, backBtn, "AuthFeedbackText", "AuthBackButton");

            Rect pillRect = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "Screen1_MainMenu/AccountStatusPill");
            Rect userIconRect = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AccountStatusPill/UserIcon");
            Rect statusTextRect = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AccountStatusPill/AccountStatusText");
            Rect actionBtnRect = EditModeTierSupport.GetElementBounds1920x1080(scene, canvas, "AccountStatusPill/AccountActionButton");

            EditModeTierSupport.AssertRectInside1920x1080(pillRect, "AccountStatusPill");
            EditModeTierSupport.AssertRectContainedIn(userIconRect, pillRect, "UserIcon", "AccountStatusPill");
            EditModeTierSupport.AssertRectContainedIn(statusTextRect, pillRect, "AccountStatusText", "AccountStatusPill");
            EditModeTierSupport.AssertRectContainedIn(actionBtnRect, pillRect, "AccountActionButton", "AccountStatusPill");
            EditModeTierSupport.AssertRectsDoNotOverlap(userIconRect, statusTextRect, "UserIcon", "AccountStatusText");
            EditModeTierSupport.AssertRectsDoNotOverlap(statusTextRect, actionBtnRect, "AccountStatusText", "AccountActionButton");
            EditModeTierSupport.AssertRectsDoNotOverlap(userIconRect, actionBtnRect, "UserIcon", "AccountActionButton");
        }

        private static void AssertImageSpriteAndType(GameObject go, string expectedSpriteName, Image.Type expectedType, bool expectPreserveAspect)
        {
            Assert.IsNotNull(go);
            Image img = go.GetComponent<Image>();
            Assert.IsNotNull(img, $"Image missing on '{go.name}'.");
            Assert.IsNotNull(img.sprite, $"Sprite missing on '{go.name}'.");
            Assert.AreEqual(expectedSpriteName, img.sprite.name, $"Sprite mismatch on '{go.name}'.");
            Assert.AreEqual(expectedType, img.type, $"Image.Type mismatch on '{go.name}'.");
            if (expectPreserveAspect)
            {
                Assert.IsTrue(img.preserveAspect, $"preserveAspect must be true on '{go.name}'.");
            }
        }

        private static void AssertAllTmpFontsValid(GameObject root, string label)
        {
            TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
            Assert.Greater(tmps.Length, 0, $"Expected TMP_Text elements under {label}.");
            for (int i = 0; i < tmps.Length; i++)
            {
                TMP_Text t = tmps[i];
                Assert.IsNotNull(t.font, $"TMP_Text '{t.name}' under {label} has null font.");
                Assert.IsFalse(t.font.name.Contains("LiberationSans"), $"TMP_Text '{t.name}' under {label} must not use LiberationSans SDF.");
            }

            TMP_InputField[] inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                TMP_InputField inp = inputs[i];
                Assert.IsNotNull(inp.fontAsset, $"TMP_InputField '{inp.name}' fontAsset is null.");
                Assert.IsFalse(inp.fontAsset.name.Contains("LiberationSans"), $"TMP_InputField '{inp.name}' fontAsset uses LiberationSans.");
                Assert.IsNotNull(inp.textComponent, $"TMP_InputField '{inp.name}' textComponent is null.");
                Assert.IsNotNull(inp.textComponent.font, $"TMP_InputField '{inp.name}' textComponent.font is null.");
                Assert.IsFalse(inp.textComponent.font.name.Contains("LiberationSans"), $"TMP_InputField '{inp.name}' textComponent.font uses LiberationSans.");
                if (inp.placeholder is TMP_Text ph)
                {
                    Assert.IsNotNull(ph.font, $"TMP_InputField '{inp.name}' placeholder.font is null.");
                    Assert.IsFalse(ph.font.name.Contains("LiberationSans"), $"TMP_InputField '{inp.name}' placeholder.font uses LiberationSans.");
                }
            }
        }
    }
}
