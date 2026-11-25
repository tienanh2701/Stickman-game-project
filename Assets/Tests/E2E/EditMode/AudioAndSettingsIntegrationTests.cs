using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StickmanBeatEmUp.Tests.E2E.EditMode
{
    public class AudioAndSettingsIntegrationTests
    {
        private static readonly string[] BgmNames =
        {
            "bgm_world1_aot_stage",
            "bgm_world1_aot_boss",
            "bgm_world2_db_stage",
            "bgm_world2_db_boss",
            "bgm_world3_diend_stage",
            "bgm_world3_diend_boss",
        };

        private static readonly string[] SfxSubPaths =
        {
            // Goku (17)
            "Goku/goku_J_normal_attack_swing",
            "Goku/goku_J_normal_attack_hit",
            "Goku/goku_J_ssj_attack_hit",
            "Goku/goku_U_normal_ki_blast",
            "Goku/goku_U_ssj_multihit_flurry",
            "Goku/goku_I_normal_kaioken",
            "Goku/goku_I_ssj_kamehameha",
            "Goku/goku_dashJ_ki_explosion",
            "Goku/goku_O_transform_ssj",
            "Goku/goku_dashI_genkidama_charge",
            "Goku/goku_dashI_genkidama_launch",
            "Goku/goku_dashI_genkidama_explode",
            "Goku/goku_state_jump",
            "Goku/goku_state_dash",
            "Goku/goku_state_hurt",
            "Goku/goku_state_hurt_heavy",
            "Goku/goku_state_die",

            // Eren (17)
            "Eren/eren_J_normal_slash_swing",
            "Eren/eren_J_normal_slash_hit",
            "Eren/eren_J_titan_punch_hit",
            "Eren/eren_U_normal_dash_slash",
            "Eren/eren_U_titan_ground_slam",
            "Eren/eren_I_normal_thunder_spear_fire",
            "Eren/eren_I_normal_thunder_spear_explode",
            "Eren/eren_I_titan_rock_throw",
            "Eren/eren_I_titan_rock_smash",
            "Eren/eren_dashJ_spin_slash",
            "Eren/eren_O_transform_titan",
            "Eren/eren_dashI_rumbling_roar",
            "Eren/eren_dashI_rumbling_stomp",
            "Eren/eren_state_jump",
            "Eren/eren_state_dash",
            "Eren/eren_state_hurt",
            "Eren/eren_state_die",

            // Diend (11)
            "Diend/diend_J_normal_gun_shot",
            "Diend/diend_J_bullet_hit",
            "Diend/diend_U_phantom_slash",
            "Diend/diend_I_piercing_shot",
            "Diend/diend_dashJ_fan_spread_shot",
            "Diend/diend_O_transform_summon_kiva",
            "Diend/diend_dashI_dark_kiva_freeze",
            "Diend/diend_state_jump",
            "Diend/diend_state_dash",
            "Diend/diend_state_hurt",
            "Diend/diend_state_die",

            // UI (5)
            "UI/ui_hover",
            "UI/ui_click",
            "UI/ui_confirm",
            "UI/ui_back",
            "UI/ui_error",
        };

        [Test]
        [Category("Tier1")]
        [Category("Audio")]
        public void Audio_All56ClipsExist_LoadViaResources_AndHaveOptimalImportSettings()
        {
            Assert.AreEqual(6, BgmNames.Length);
            Assert.AreEqual(50, SfxSubPaths.Length);

            for (int i = 0; i < BgmNames.Length; i++)
            {
                string resourcePath = "Audio/BGM/" + BgmNames[i];
                string assetPath = "Assets/Resources/" + resourcePath + ".ogg";
                VerifyImportedClip(assetPath, resourcePath, AudioClipLoadType.Streaming);
            }

            for (int i = 0; i < SfxSubPaths.Length; i++)
            {
                string resourcePath = "Audio/SFX/" + SfxSubPaths[i];
                string assetPath = "Assets/Resources/" + resourcePath + ".ogg";
                VerifyImportedClip(assetPath, resourcePath, AudioClipLoadType.DecompressOnLoad);
            }
        }

        private static void VerifyImportedClip(string assetPath, string resourcePath, AudioClipLoadType expectedLoadType)
        {
            Assert.IsTrue(File.Exists(assetPath), $"Missing audio file on disk: '{assetPath}'.");

            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            Assert.IsNotNull(clip, $"Resources.Load<AudioClip>('{resourcePath}') returned null.");
            Assert.Greater(clip.length, 0.03f, $"AudioClip '{resourcePath}' length invalid: {clip.length}");
            Assert.GreaterOrEqual(clip.channels, 1, $"AudioClip '{resourcePath}' channels invalid.");

            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            Assert.IsNotNull(importer, $"AudioImporter missing for '{assetPath}'.");
            Assert.IsFalse(importer.loadInBackground, $"loadInBackground should be false for '{assetPath}' to avoid first-play silence.");
            Assert.AreEqual(
                expectedLoadType,
                importer.defaultSampleSettings.loadType,
                $"Unexpected loadType on '{assetPath}'.");
            Assert.AreEqual(
                AudioCompressionFormat.Vorbis,
                importer.defaultSampleSettings.compressionFormat,
                $"Expected Vorbis compression on '{assetPath}'.");
        }

        [Test]
        [Category("Tier1")]
        [Category("Audio")]
        public void Audio_AllBuildScenes_HaveExactlyOneAudioListener()
        {
            for (int i = 0; i < EditModeTierSupport.ExpectedBuildScenes.Length; i++)
            {
                string scenePath = EditModeTierSupport.ExpectedBuildScenes[i];
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Assert.IsTrue(scene.IsValid() && scene.isLoaded, $"Failed to open scene '{scenePath}'.");

                int listenerCount = 0;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    AudioListener[] found = roots[r].GetComponentsInChildren<AudioListener>(true);
                    if (found != null)
                    {
                        listenerCount += found.Length;
                    }
                }

                Assert.AreEqual(1, listenerCount, $"Expected exactly 1 AudioListener in '{scenePath}', found {listenerCount}.");
            }
        }
    }
}
