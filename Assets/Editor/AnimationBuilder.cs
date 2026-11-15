using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace StickmanBeatEmUp.Editor
{
    /// <summary>
    /// Tự động tạo toàn bộ AnimationClip (2D Sprite frame-by-frame) và AnimatorController
    /// cho Goku, Eren, Diend và Lord Kiva từ các Sprite đã cắt.
    /// </summary>
    public static class AnimationBuilder
    {
        private const string ANIM_ROOT = "Assets/Animations";

        [MenuItem("Tools/Build Character Animations", false, 3)]
        public static void BuildAllAnimationsMenu()
        {
            SpriteHelper.ConfigureAllProjectSprites();
            BuildAllAnimations(out _, out _, out _, out _);
            BuildAllEnemyAnimations();
            EditorUtility.DisplayDialog("Animation Builder", "Da tao xong AnimationClip va AnimatorController cho MainChar + toan bo Quai/Elite/Boss!", "OK");
        }

        public static Dictionary<string, RuntimeAnimatorController> BuildAllEnemyAnimations()
        {
            const string enemiesAnimRoot = ANIM_ROOT + "/Enemies";
            const string enemySpriteRoot = "Assets/Sprites/Enemies";
            SpriteHelper.EnsureFolder(enemiesAnimRoot);
            SpriteHelper.EnsureFolder($"{enemiesAnimRoot}/Creeps");
            SpriteHelper.EnsureFolder($"{enemiesAnimRoot}/Elites");
            SpriteHelper.EnsureFolder($"{enemiesAnimRoot}/Bosses");

            var result = new Dictionary<string, RuntimeAnimatorController>();

            // 1. BaseCreep.controller (Strictly trimmed 5 states: 0=Move, 1=Attack1, 2=Attack2, 3=Hit, 4=Death)
            var baseClips = new Dictionary<string, AnimationClip>
            {
                ["Move"] = CreateSpriteClip($"{enemiesAnimRoot}/Creeps/Base_Move.anim", new[] { $"{enemySpriteRoot}/World1_AoT/PureTitan_Move_0.png", $"{enemySpriteRoot}/World1_AoT/PureTitan_Move_1.png" }, 6f, true),
                ["Attack1"] = CreateSpriteClip($"{enemiesAnimRoot}/Creeps/Base_Attack1.anim", new[] { $"{enemySpriteRoot}/World1_AoT/PureTitan_Lunge_0.png", $"{enemySpriteRoot}/World1_AoT/PureTitan_Bite_0.png" }, 8f, false),
                ["Attack2"] = CreateSpriteClip($"{enemiesAnimRoot}/Creeps/Base_Attack2.anim", new[] { $"{enemySpriteRoot}/World1_AoT/PureTitan_Lunge_0.png", $"{enemySpriteRoot}/World1_AoT/PureTitan_Grab_0.png" }, 8f, false),
                ["Hit"] = CreateSpriteClip($"{enemiesAnimRoot}/Creeps/Base_Hit.anim", new[] { $"{enemySpriteRoot}/World1_AoT/PureTitan_Hit_0.png" }, 6f, false),
                ["Death"] = CreateSpriteClip($"{enemiesAnimRoot}/Creeps/Base_Death.anim", new[] { $"{enemySpriteRoot}/World1_AoT/PureTitan_Death_0.png" }, 6f, false)
            };
            AnimatorController baseCreepCtrl = BuildIntStateController($"{enemiesAnimRoot}/BaseCreep.controller", new[]
            {
                ("Move", 0, baseClips["Move"]),
                ("Attack1", 1, baseClips["Attack1"]),
                ("Attack2", 2, baseClips["Attack2"]),
                ("Hit", 3, baseClips["Hit"]),
                ("Death", 4, baseClips["Death"])
            });
            result["BaseCreep"] = baseCreepCtrl;

            // 2. 6 Creep AnimatorOverrideControllers (wind-up/setup poses merged into Attack1/Attack2)
            string w1 = $"{enemySpriteRoot}/World1_AoT";
            string w2 = $"{enemySpriteRoot}/World2_DB";
            string w3 = $"{enemySpriteRoot}/World3_Diend";

            result["PureTitan"] = BuildCreepOverride("PureTitan", baseCreepCtrl, baseClips,
                new[] { $"{w1}/PureTitan_Move_0.png", $"{w1}/PureTitan_Move_1.png" },
                new[] { $"{w1}/PureTitan_Lunge_0.png", $"{w1}/PureTitan_Bite_0.png" },
                new[] { $"{w1}/PureTitan_Lunge_0.png", $"{w1}/PureTitan_Grab_0.png" },
                $"{w1}/PureTitan_Hit_0.png", $"{w1}/PureTitan_Death_0.png");

            result["BeastTitan"] = BuildCreepOverride("BeastTitan", baseCreepCtrl, baseClips,
                new[] { $"{w1}/BeastTitan_Move_0.png", $"{w1}/BeastTitan_Move_1.png" },
                new[] { $"{w1}/BeastTitan_Crouch_0.png", $"{w1}/BeastTitan_DrawBack_0.png", $"{w1}/BeastTitan_HeavyThrow_0.png" },
                new[] { $"{w1}/BeastTitan_DrawBack_0.png", $"{w1}/BeastTitan_HeavyThrow_0.png" },
                $"{w1}/BeastTitan_Hit_0.png", $"{w1}/BeastTitan_Death_0.png");

            result["Saibamen"] = BuildCreepOverride("Saibamen", baseCreepCtrl, baseClips,
                new[] { $"{w2}/Saibamen_Move_0.png", $"{w2}/Saibamen_Move_1.png" },
                new[] { $"{w2}/Saibamen_WindUp_0.png", $"{w2}/Saibamen_DashSlash_0.png" },
                new[] { $"{w2}/Saibamen_WindUp_0.png", $"{w2}/Saibamen_Headbutt_0.png" },
                $"{w2}/Saibamen_Hit_0.png", $"{w2}/Saibamen_Death_0.png");

            result["FriezaSoldier"] = BuildCreepOverride("FriezaSoldier", baseCreepCtrl, baseClips,
                new[] { $"{w2}/FriezaSoldier_Move_0.png", $"{w2}/FriezaSoldier_Move_1.png" },
                new[] { $"{w2}/FriezaSoldier_LockOn_0.png", $"{w2}/FriezaSoldier_KiBlast_0.png" },
                new[] { $"{w2}/FriezaSoldier_LockOn_0.png", $"{w2}/FriezaSoldier_ContFire_0.png" },
                $"{w2}/FriezaSoldier_Hit_0.png", $"{w2}/FriezaSoldier_Death_0.png");

            result["Shocker"] = BuildCreepOverride("Shocker", baseCreepCtrl, baseClips,
                new[] { $"{w3}/Shocker_Move_0.png", $"{w3}/Shocker_Move_1.png" },
                new[] { $"{w3}/Shocker_WindUp_0.png", $"{w3}/Shocker_LungeStrike_0.png" },
                new[] { $"{w3}/Shocker_WindUp_0.png", $"{w3}/Shocker_RapidStab_0.png" },
                $"{w3}/Shocker_Hit_0.png", $"{w3}/Shocker_Death_0.png");

            result["Kasshin"] = BuildCreepOverride("Kasshin", baseCreepCtrl, baseClips,
                new[] { $"{w3}/Kasshin_Move_0.png", $"{w3}/Kasshin_Move_1.png" },
                new[] { $"{w3}/Kasshin_PlantStaff_0.png", $"{w3}/Kasshin_EnergySpear_0.png" },
                new[] { $"{w3}/Kasshin_PlantStaff_0.png", $"{w3}/Kasshin_LaserSweep_0.png" },
                $"{w3}/Kasshin_Hit_0.png", $"{w3}/Kasshin_Death_0.png");

            // 3. 3 Elite Controllers
            const string ef = enemiesAnimRoot + "/Elites";
            result["FemaleTitan"] = BuildIntStateController($"{ef}/FemaleTitan.controller", new[]
            {
                ("FightStance", 0, CreateSpriteClip($"{ef}/FT_Stance.anim", new[] { $"{w1}/FemaleTitan_FightStance_0.png", $"{w1}/FemaleTitan_FightStance_1.png" }, 6f, true)),
                ("DashRush", 1, CreateSpriteClip($"{ef}/FT_DashRush.anim", new[] { $"{w1}/FemaleTitan_DashRush_0.png" }, 8f, false)),
                ("HighKick", 2, CreateSpriteClip($"{ef}/FT_HighKick.anim", new[] { $"{w1}/FemaleTitan_HighKick_0.png" }, 8f, false)),
                ("HardenedPunch", 3, CreateSpriteClip($"{ef}/FT_Punch.anim", new[] { $"{w1}/FemaleTitan_HardenedPunch_0.png" }, 8f, false)),
                ("Parry", 4, CreateSpriteClip($"{ef}/FT_Parry.anim", new[] { $"{w1}/FemaleTitan_Parry_0.png" }, 8f, false)),
                ("Death", 5, CreateSpriteClip($"{ef}/FT_Death.anim", new[] { $"{w1}/FemaleTitan_Death_0.png" }, 6f, false))
            });

            result["Nappa"] = BuildIntStateController($"{ef}/Nappa.controller", new[]
            {
                ("Advance", 0, CreateSpriteClip($"{ef}/Nappa_Advance.anim", new[] { $"{w2}/Nappa_Advance_0.png", $"{w2}/Nappa_Advance_1.png" }, 5f, true)),
                ("ChargeUp", 1, CreateSpriteClip($"{ef}/Nappa_Charge.anim", new[] { $"{w2}/Nappa_ChargeUp_0.png" }, 6f, false)),
                ("HeavyHaymaker", 2, CreateSpriteClip($"{ef}/Nappa_Haymaker.anim", new[] { $"{w2}/Nappa_HeavyHaymaker_0.png" }, 8f, false)),
                ("GroundEruption", 3, CreateSpriteClip($"{ef}/Nappa_Eruption.anim", new[] { $"{w2}/Nappa_GroundEruption_0.png" }, 8f, false)),
                ("Hit", 4, CreateSpriteClip($"{ef}/Nappa_Hit.anim", new[] { $"{w2}/Nappa_Hit_0.png" }, 6f, false)),
                ("Death", 5, CreateSpriteClip($"{ef}/Nappa_Death.anim", new[] { $"{w2}/Nappa_Death_0.png" }, 6f, false))
            });

            result["ShadowMoon"] = BuildIntStateController($"{ef}/ShadowMoon.controller", new[]
            {
                ("Walk", 0, CreateSpriteClip($"{ef}/SM_Walk.anim", new[] { $"{w3}/ShadowMoon_Walk_0.png", $"{w3}/ShadowMoon_Walk_1.png" }, 6f, true)),
                ("SwordCharge", 1, CreateSpriteClip($"{ef}/SM_Charge.anim", new[] { $"{w3}/ShadowMoon_SwordCharge_0.png" }, 8f, false)),
                ("CrescentSlash", 2, CreateSpriteClip($"{ef}/SM_Slash.anim", new[] { $"{w3}/ShadowMoon_CrescentSlash_0.png" }, 8f, false)),
                ("Teleport", 3, CreateSpriteClip($"{ef}/SM_Teleport.anim", new[] { $"{w3}/ShadowMoon_Teleport_0.png" }, 10f, false)),
                ("Hit", 4, CreateSpriteClip($"{ef}/SM_Hit.anim", new[] { $"{w3}/ShadowMoon_Hit_0.png" }, 6f, false)),
                ("Death", 5, CreateSpriteClip($"{ef}/SM_Death.anim", new[] { $"{w3}/ShadowMoon_Death_0.png" }, 6f, false))
            });

            // 4. 3 Boss Controllers
            const string bf = enemiesAnimRoot + "/Bosses";
            result["WarHammer"] = BuildIntStateController($"{bf}/WarHammer.controller", new[]
            {
                ("HammerSmash", 0, CreateSpriteClip($"{bf}/WH_Smash.anim", new[] { $"{w1}/WarHammer_HammerSmash_0.png" }, 8f, false)),
                ("GroundSpikes", 1, CreateSpriteClip($"{bf}/WH_Spikes.anim", new[] { $"{w1}/WarHammer_GroundSpikes_0.png" }, 8f, false)),
                ("Hit", 2, CreateSpriteClip($"{bf}/WH_Hit.anim", new[] { $"{w1}/WarHammer_Hit_0.png" }, 6f, false)),
                ("Destroyed", 3, CreateSpriteClip($"{bf}/WH_Destroyed.anim", new[] { $"{w1}/WarHammer_Destroyed_0.png" }, 6f, false))
            });

            result["Broly"] = BuildIntStateController($"{bf}/Broly.controller", new[]
            {
                ("HeavyStomp", 0, CreateSpriteClip($"{bf}/Broly_Stomp.anim", new[] { $"{w2}/Broly_HeavyStomp_0.png", $"{w2}/Broly_HeavyStomp_1.png" }, 6f, true)),
                ("Roar", 1, CreateSpriteClip($"{bf}/Broly_Roar.anim", new[] { $"{w2}/Broly_Roar_0.png" }, 6f, false)),
                ("LariatCharge", 2, CreateSpriteClip($"{bf}/Broly_Lariat.anim", new[] { $"{w2}/Broly_LariatCharge_0.png" }, 10f, false)),
                ("GiganticSlam", 3, CreateSpriteClip($"{bf}/Broly_Slam.anim", new[] { $"{w2}/Broly_GiganticSlam_0.png" }, 8f, false)),
                ("Stagger", 4, CreateSpriteClip($"{bf}/Broly_Stagger.anim", new[] { $"{w2}/Broly_Stagger_0.png" }, 6f, false)),
                ("Defeated", 5, CreateSpriteClip($"{bf}/Broly_Defeated.anim", new[] { $"{w2}/Broly_Defeated_0.png" }, 6f, false))
            });

            AnimationClip mechaBodyClip = CreateSpriteClip($"{bf}/Mecha_Idle.anim", new[] { $"{w3}/Mecha_Body.png" }, 6f, true);
            result["MechaColossus"] = BuildIntStateController($"{bf}/MechaColossus.controller", new[]
            {
                ("Idle", 0, mechaBodyClip),
                ("RocketPunch", 1, mechaBodyClip),
                ("LaserSweep", 2, mechaBodyClip),
                ("CoreExposed", 3, mechaBodyClip),
                ("Destroyed", 4, mechaBodyClip)
            });

            AssetDatabase.SaveAssets();
            return result;
        }

        private static AnimatorOverrideController BuildCreepOverride(
            string creepName, AnimatorController baseCtrl, Dictionary<string, AnimationClip> baseClips,
            string[] movePaths, string[] atk1Paths, string[] atk2Paths, string hitPath, string deathPath)
        {
            string cf = $"{ANIM_ROOT}/Enemies/Creeps";
            string ovPath = $"{cf}/{creepName}_Override.overrideController";
            AnimatorOverrideController ov = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ovPath);
            if (ov == null)
            {
                ov = new AnimatorOverrideController(baseCtrl);
                AssetDatabase.CreateAsset(ov, ovPath);
            }
            ov.runtimeAnimatorController = baseCtrl;

            var map = new Dictionary<AnimationClip, AnimationClip>
            {
                [baseClips["Move"]] = CreateSpriteClip($"{cf}/{creepName}_Move.anim", movePaths, 7f, true),
                [baseClips["Attack1"]] = CreateSpriteClip($"{cf}/{creepName}_Attack1.anim", atk1Paths, 8f, false),
                [baseClips["Attack2"]] = CreateSpriteClip($"{cf}/{creepName}_Attack2.anim", atk2Paths, 8f, false),
                [baseClips["Hit"]] = CreateSpriteClip($"{cf}/{creepName}_Hit.anim", new[] { hitPath }, 6f, false),
                [baseClips["Death"]] = CreateSpriteClip($"{cf}/{creepName}_Death.anim", new[] { deathPath }, 6f, false)
            };

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            ov.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                if (map.TryGetValue(overrides[i].Key, out AnimationClip repl))
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, repl);
                }
            }
            ov.ApplyOverrides(overrides);
            EditorUtility.SetDirty(ov);
            return ov;
        }

        private static AnimatorController BuildIntStateController(string ctrlPath, (string name, int stateVal, AnimationClip clip)[] states)
        {
            AnimatorController controller = PrepareController(ctrlPath);
            controller.AddParameter("State", AnimatorControllerParameterType.Int);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            for (int i = 0; i < states.Length; i++)
            {
                AnimatorState st = sm.AddState(states[i].name);
                st.motion = states[i].clip;
                if (i == 0) sm.defaultState = st;
                AddAnyStateTransition(sm, st, ("State", states[i].stateVal));
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        public static void BuildAllAnimations(
            out RuntimeAnimatorController gokuCtrl,
            out RuntimeAnimatorController erenCtrl,
            out RuntimeAnimatorController diendCtrl,
            out RuntimeAnimatorController lordKivaCtrl)
        {
            SpriteHelper.EnsureFolder(ANIM_ROOT);
            SpriteHelper.EnsureFolder($"{ANIM_ROOT}/Goku");
            SpriteHelper.EnsureFolder($"{ANIM_ROOT}/Eren");
            SpriteHelper.EnsureFolder($"{ANIM_ROOT}/Diend");
            SpriteHelper.EnsureFolder($"{ANIM_ROOT}/LordKiva");

            gokuCtrl = BuildGokuController();
            erenCtrl = BuildErenController();
            diendCtrl = BuildDiendController();
            lordKivaCtrl = BuildLordKivaController();

            AssetDatabase.SaveAssets();
        }

        private static RuntimeAnimatorController BuildGokuController()
        {
            const string folder = ANIM_ROOT + "/Goku";
            const string sprites = "Assets/Sprites/Characters/Goku";

            var clips = new Dictionary<string, AnimationClip>
            {
                ["Normal_Idle"] = CreateSpriteClip($"{folder}/Goku_Normal_Idle.anim", new[] { $"{sprites}/Goku_Idle_0.png", $"{sprites}/Goku_Idle_1.png" }, 6f, true),
                ["Trans_Idle"] = CreateSpriteClip($"{folder}/Goku_Trans_Idle.anim", new[] { $"{sprites}/Goku_SSJ_Idle_0.png", $"{sprites}/Goku_SSJ_Idle_1.png" }, 6f, true),
                ["Normal_Run"] = CreateSpriteClip($"{folder}/Goku_Normal_Run.anim", RangePaths($"{sprites}/Goku_Run_", 6), 12f, true),
                ["Trans_Run"] = CreateSpriteClip($"{folder}/Goku_Trans_Run.anim", RangePaths($"{sprites}/Goku_SSJ_Run_", 6), 12f, true),
                ["Normal_Dash"] = CreateSpriteClip($"{folder}/Goku_Normal_Dash.anim", new[] { $"{sprites}/Goku_Dash_0.png" }, 12f, false),
                ["Trans_Dash"] = CreateSpriteClip($"{folder}/Goku_Trans_Dash.anim", new[] { $"{sprites}/Goku_SSJ_Dash_0.png" }, 12f, false),
                ["Normal_Attack"] = CreateSpriteClip($"{folder}/Goku_Normal_Attack.anim", RangePaths($"{sprites}/Goku_Punch_", 6), 16f, false),
                ["Trans_Attack"] = CreateSpriteClip($"{folder}/Goku_Trans_Attack.anim", RangePaths($"{sprites}/Goku_SSJ_Punch_", 6), 16f, false),
                ["Normal_DashJ"] = CreateSpriteClip($"{folder}/Goku_Normal_DashJ.anim", RangePaths($"{sprites}/Goku_DashJ_", 6), 14f, false),
                ["Trans_DashJ"] = CreateSpriteClip($"{folder}/Goku_Trans_DashJ.anim", RangePaths($"{sprites}/Goku_SSJ_DashJ_", 6), 14f, false),
                ["Normal_Skill1"] = CreateSpriteClip($"{folder}/Goku_Normal_Skill1.anim", RangePaths($"{sprites}/Goku_KiBlast_", 6), 14f, false),
                ["Trans_Skill1"] = CreateSpriteClip($"{folder}/Goku_Trans_Skill1.anim", RangePaths($"{sprites}/Goku_SSJ_MultiHit_", 6), 16f, false),
                ["Normal_Skill2"] = CreateSpriteClip($"{folder}/Goku_Normal_Skill2.anim", RangePaths($"{sprites}/Goku_Kaioken_", 6), 14f, false),
                ["Trans_Skill2"] = CreateSpriteClip($"{folder}/Goku_Trans_Skill2.anim", RangePaths($"{sprites}/Goku_Kamehameha_", 6), 14f, false),
                ["SpecialCombo"] = CreateSpriteClip($"{folder}/Goku_SpecialCombo.anim", RangePaths($"{sprites}/Goku_GenkiDama_", 6), 12f, false),
                ["Transform"] = CreateSpriteClip($"{folder}/Goku_Transform.anim", new[]
                {
                    $"{sprites}/Goku_Kaioken_2.png", $"{sprites}/Goku_Kaioken_3.png",
                    $"{sprites}/Goku_SSJ_Idle_0.png", $"{sprites}/Goku_SSJ_Idle_1.png"
                }, 10f, false),
                ["Normal_Hurt"] = CreateSpriteClip($"{folder}/Goku_Normal_Hurt.anim", new[] { $"{sprites}/Goku_Idle_0.png" }, 8f, false),
                ["Trans_Hurt"] = CreateSpriteClip($"{folder}/Goku_Trans_Hurt.anim", new[] { $"{sprites}/Goku_SSJ_Idle_0.png" }, 8f, false),
                ["Normal_Dead"] = CreateSpriteClip($"{folder}/Goku_Normal_Dead.anim", new[] { $"{sprites}/Goku_Idle_0.png" }, 4f, false),
                ["Trans_Dead"] = CreateSpriteClip($"{folder}/Goku_Trans_Dead.anim", new[] { $"{sprites}/Goku_SSJ_Idle_0.png" }, 4f, false),
                ["Normal_Block"] = CreateSpriteClip($"{folder}/Goku_Normal_Block.anim", new[] { $"{sprites}/Goku_Idle_0.png" }, 8f, true),
                ["Trans_Block"] = CreateSpriteClip($"{folder}/Goku_Trans_Block.anim", new[] { $"{sprites}/Goku_SSJ_Idle_0.png" }, 8f, true)
            };

            return BuildCharacterController($"{folder}/Goku.controller", clips);
        }

        private static RuntimeAnimatorController BuildErenController()
        {
            const string folder = ANIM_ROOT + "/Eren";
            const string sprites = "Assets/Sprites/Characters/Eren";

            var clips = new Dictionary<string, AnimationClip>
            {
                ["Normal_Idle"] = CreateSpriteClip($"{folder}/Eren_Normal_Idle.anim", new[] { $"{sprites}/Eren_Idle_0.png", $"{sprites}/Eren_Idle_1.png" }, 6f, true),
                ["Trans_Idle"] = CreateSpriteClip($"{folder}/Eren_Trans_Idle.anim", new[] { $"{sprites}/Eren_Titan_Idle_0.png", $"{sprites}/Eren_Titan_Idle_1.png" }, 6f, true),
                ["Normal_Run"] = CreateSpriteClip($"{folder}/Eren_Normal_Run.anim", RangePaths($"{sprites}/Eren_Run_", 6), 12f, true),
                ["Trans_Run"] = CreateSpriteClip($"{folder}/Eren_Trans_Run.anim", RangePaths($"{sprites}/Eren_Titan_Run_", 6), 12f, true),
                ["Normal_Dash"] = CreateSpriteClip($"{folder}/Eren_Normal_Dash.anim", RangePaths($"{sprites}/Eren_Dash_", 2), 12f, false),
                ["Trans_Dash"] = CreateSpriteClip($"{folder}/Eren_Trans_Dash.anim", new[] { $"{sprites}/Eren_Titan_Dash_0.png", $"{sprites}/Eren_Titan_Dash_1.png" }, 12f, false),
                ["Normal_Attack"] = CreateSpriteClip($"{folder}/Eren_Normal_Attack.anim", RangePaths($"{sprites}/Eren_SwordSlash_", 6), 16f, false),
                ["Trans_Attack"] = CreateSpriteClip($"{folder}/Eren_Trans_Attack.anim", RangePaths($"{sprites}/Eren_Titan_GroundSlam_", 6), 16f, false),
                ["Normal_DashJ"] = CreateSpriteClip($"{folder}/Eren_Normal_DashJ.anim", RangePaths($"{sprites}/Eren_SpinSlash_", 6), 14f, false),
                ["Trans_DashJ"] = CreateSpriteClip($"{folder}/Eren_Trans_DashJ.anim", new[]
                {
                    $"{sprites}/Eren_Titan_GroundSlam_2.png", $"{sprites}/Eren_Titan_GroundSlam_3.png",
                    $"{sprites}/Eren_Titan_GroundSlam_4.png", $"{sprites}/Eren_Titan_GroundSlam_5.png"
                }, 12f, false),
                ["Normal_Skill1"] = CreateSpriteClip($"{folder}/Eren_Normal_Skill1.anim", RangePaths($"{sprites}/Eren_DashSlash_", 6), 14f, false),
                ["Trans_Skill1"] = CreateSpriteClip($"{folder}/Eren_Trans_Skill1.anim", RangePaths($"{sprites}/Eren_Titan_GroundSlam_", 6), 16f, false),
                ["Normal_Skill2"] = CreateSpriteClip($"{folder}/Eren_Normal_Skill2.anim", RangePaths($"{sprites}/Eren_ThunderSpear_", 6), 14f, false),
                ["Trans_Skill2"] = CreateSpriteClip($"{folder}/Eren_Trans_Skill2.anim", RangePaths($"{sprites}/Eren_Titan_RockThrow_", 6), 12f, false),
                ["SpecialCombo"] = CreateSpriteClip($"{folder}/Eren_SpecialCombo.anim", RangePaths($"{sprites}/Eren_Titan_Stomp_", 6), 12f, false),
                ["Transform"] = CreateSpriteClip($"{folder}/Eren_Transform.anim", new[]
                {
                    $"{sprites}/Eren_Idle_0.png", $"{sprites}/Eren_Titan_Idle_0.png", $"{sprites}/Eren_Titan_Idle_1.png"
                }, 10f, false),
                ["Normal_Hurt"] = CreateSpriteClip($"{folder}/Eren_Normal_Hurt.anim", new[] { $"{sprites}/Eren_Dash_0.png" }, 8f, false),
                ["Trans_Hurt"] = CreateSpriteClip($"{folder}/Eren_Trans_Hurt.anim", new[] { $"{sprites}/Eren_Titan_Idle_0.png" }, 8f, false),
                ["Normal_Dead"] = CreateSpriteClip($"{folder}/Eren_Normal_Dead.anim", new[] { $"{sprites}/Eren_Dash_0.png" }, 4f, false),
                ["Trans_Dead"] = CreateSpriteClip($"{folder}/Eren_Trans_Dead.anim", new[] { $"{sprites}/Eren_Titan_Idle_0.png" }, 4f, false),
                ["Normal_Block"] = CreateSpriteClip($"{folder}/Eren_Normal_Block.anim", new[] { $"{sprites}/Eren_Block_0.png", $"{sprites}/Eren_Block_1.png" }, 8f, true),
                ["Trans_Block"] = CreateSpriteClip($"{folder}/Eren_Trans_Block.anim", new[] { $"{sprites}/Eren_Titan_Idle_0.png" }, 8f, true)
            };

            return BuildCharacterController($"{folder}/Eren.controller", clips);
        }

        private static RuntimeAnimatorController BuildDiendController()
        {
            const string folder = ANIM_ROOT + "/Diend";
            const string sprites = "Assets/Sprites/Characters/Diend";

            AnimationClip idle = CreateSpriteClip($"{folder}/Diend_Idle.anim", new[] { $"{sprites}/Diend_Idle_0.png", $"{sprites}/Diend_Idle_1.png" }, 6f, true);
            AnimationClip run = CreateSpriteClip($"{folder}/Diend_Run.anim", RangePaths($"{sprites}/Diend_Run_", 6), 12f, true);
            AnimationClip dash = CreateSpriteClip($"{folder}/Diend_Dash.anim", new[] { $"{sprites}/Diend_Dash_0.png" }, 12f, false);
            AnimationClip attack = CreateSpriteClip($"{folder}/Diend_Attack.anim", RangePaths($"{sprites}/Diend_GunShoot_", 6), 16f, false);
            AnimationClip dashJ = CreateSpriteClip($"{folder}/Diend_DashJ.anim", RangePaths($"{sprites}/Diend_FanShot_", 6), 14f, false);
            AnimationClip skill1 = CreateSpriteClip($"{folder}/Diend_Skill1.anim", RangePaths($"{sprites}/Diend_PhantomSlash_", 6), 14f, false);
            AnimationClip skill2 = CreateSpriteClip($"{folder}/Diend_Skill2.anim", RangePaths($"{sprites}/Diend_CardShot_", 6), 14f, false);
            AnimationClip transformClip = CreateSpriteClip($"{folder}/Diend_Transform.anim", new[]
            {
                $"{sprites}/Diend_CardShot_0.png", $"{sprites}/Diend_Idle_0.png", $"{sprites}/Diend_Idle_1.png"
            }, 10f, false);
            AnimationClip hurtClip = CreateSpriteClip($"{folder}/Diend_Hurt.anim", new[] { $"{sprites}/Diend_Idle_0.png" }, 8f, false);

            var clips = new Dictionary<string, AnimationClip>
            {
                ["Normal_Idle"] = idle,
                ["Trans_Idle"] = idle,
                ["Normal_Run"] = run,
                ["Trans_Run"] = run,
                ["Normal_Dash"] = dash,
                ["Trans_Dash"] = dash,
                ["Normal_Attack"] = attack,
                ["Trans_Attack"] = attack,
                ["Normal_DashJ"] = dashJ,
                ["Trans_DashJ"] = dashJ,
                ["Normal_Skill1"] = skill1,
                ["Trans_Skill1"] = skill1,
                ["Normal_Skill2"] = skill2,
                ["Trans_Skill2"] = skill2,
                ["SpecialCombo"] = transformClip,
                ["Transform"] = transformClip,
                ["Normal_Hurt"] = hurtClip,
                ["Trans_Hurt"] = hurtClip,
                ["Normal_Dead"] = hurtClip,
                ["Trans_Dead"] = hurtClip,
                ["Normal_Block"] = idle,
                ["Trans_Block"] = idle
            };

            return BuildCharacterController($"{folder}/Diend.controller", clips);
        }

        private static RuntimeAnimatorController BuildLordKivaController()
        {
            const string folder = ANIM_ROOT + "/LordKiva";
            const string sprites = "Assets/Sprites/Characters/Diend";

            AnimationClip idle = CreateSpriteClip($"{folder}/LordKiva_Idle.anim", new[] { $"{sprites}/LordKiva_Idle_0.png", $"{sprites}/LordKiva_Idle_1.png" }, 6f, true);
            AnimationClip attack = CreateSpriteClip($"{folder}/LordKiva_Attack.anim", new[] { $"{sprites}/LordKiva_Attack_0.png" }, 10f, false);
            AnimationClip freeze = CreateSpriteClip($"{folder}/LordKiva_WingFreeze.anim", new[] { $"{sprites}/LordKiva_WingFreeze_0.png" }, 8f, false);

            AnimatorController controller = PrepareController($"{folder}/LordKiva.controller");
            controller.AddParameter("State", AnimatorControllerParameterType.Int);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idleState = sm.AddState("Idle");
            idleState.motion = idle;
            sm.defaultState = idleState;

            AnimatorState attackState = sm.AddState("Attack");
            attackState.motion = attack;

            AnimatorState freezeState = sm.AddState("WingFreeze");
            freezeState.motion = freeze;

            AddAnyStateTransition(sm, idleState, ("State", 0));
            AddAnyStateTransition(sm, attackState, ("State", 1));
            AddAnyStateTransition(sm, freezeState, ("State", 2));

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static RuntimeAnimatorController BuildCharacterController(string ctrlPath, Dictionary<string, AnimationClip> clips)
        {
            AnimatorController controller = PrepareController(ctrlPath);
            controller.AddParameter("State", AnimatorControllerParameterType.Int);
            controller.AddParameter("IsTransformed", AnimatorControllerParameterType.Bool);
            controller.AddParameter("SkillIndex", AnimatorControllerParameterType.Int);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState AddClipState(string key)
            {
                AnimatorState st = sm.AddState(key);
                st.motion = clips[key];
                return st;
            }

            AnimatorState normalIdle = AddClipState("Normal_Idle");
            sm.defaultState = normalIdle;

            AnimatorState transIdle = AddClipState("Trans_Idle");
            AnimatorState normalRun = AddClipState("Normal_Run");
            AnimatorState transRun = AddClipState("Trans_Run");
            AnimatorState normalDash = AddClipState("Normal_Dash");
            AnimatorState transDash = AddClipState("Trans_Dash");
            AnimatorState normalAttack = AddClipState("Normal_Attack");
            AnimatorState transAttack = AddClipState("Trans_Attack");
            AnimatorState normalDashJ = AddClipState("Normal_DashJ");
            AnimatorState transDashJ = AddClipState("Trans_DashJ");
            AnimatorState normalSkill1 = AddClipState("Normal_Skill1");
            AnimatorState transSkill1 = AddClipState("Trans_Skill1");
            AnimatorState normalSkill2 = AddClipState("Normal_Skill2");
            AnimatorState transSkill2 = AddClipState("Trans_Skill2");
            AnimatorState specialCombo = AddClipState("SpecialCombo");
            AnimatorState transformState = AddClipState("Transform");
            AnimatorState normalHurt = AddClipState("Normal_Hurt");
            AnimatorState transHurt = AddClipState("Trans_Hurt");
            AnimatorState normalDead = AddClipState("Normal_Dead");
            AnimatorState transDead = AddClipState("Trans_Dead");
            AnimatorState normalBlock = AddClipState("Normal_Block");
            AnimatorState transBlock = AddClipState("Trans_Block");

            AddCharTransition(sm, normalIdle, 0, false, null);
            AddCharTransition(sm, transIdle, 0, true, null);
            AddCharTransition(sm, normalRun, 1, false, null);
            AddCharTransition(sm, transRun, 1, true, null);
            AddCharTransition(sm, normalDash, 2, false, null);
            AddCharTransition(sm, transDash, 2, true, null);
            AddCharTransition(sm, normalAttack, 3, false, 0);
            AddCharTransition(sm, transAttack, 3, true, 0);
            AddCharTransition(sm, normalDashJ, 3, false, 1);
            AddCharTransition(sm, transDashJ, 3, true, 1);
            AddCharTransition(sm, normalSkill1, 4, false, 1);
            AddCharTransition(sm, transSkill1, 4, true, 1);
            AddCharTransition(sm, normalSkill2, 4, false, 2);
            AddCharTransition(sm, transSkill2, 4, true, 2);
            AddCharTransition(sm, specialCombo, 4, null, 3);
            AddCharTransition(sm, transformState, 5, null, null);
            AddCharTransition(sm, normalHurt, 6, false, null);
            AddCharTransition(sm, transHurt, 6, true, null);
            AddCharTransition(sm, normalDead, 7, false, null);
            AddCharTransition(sm, transDead, 7, true, null);
            AddCharTransition(sm, normalBlock, 8, false, null);
            AddCharTransition(sm, transBlock, 8, true, null);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorController PrepareController(string ctrlPath)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (controller == null)
            {
                return AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            sm.anyStateTransitions = new AnimatorStateTransition[0];
            foreach (ChildAnimatorState child in sm.states)
            {
                sm.RemoveState(child.state);
            }
            for (int i = controller.parameters.Length - 1; i >= 0; i--)
            {
                controller.RemoveParameter(i);
            }
            return controller;
        }

        private static void AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState target, (string name, int val) intCond)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(target);
            t.hasExitTime = false;
            t.duration = 0f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Equals, intCond.val, intCond.name);
        }

        private static void AddCharTransition(AnimatorStateMachine sm, AnimatorState target, int stateVal, bool? isTransformed, int? skillIndex)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(target);
            t.hasExitTime = false;
            t.duration = 0f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Equals, stateVal, "State");

            if (isTransformed.HasValue)
            {
                t.AddCondition(isTransformed.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsTransformed");
            }
            if (skillIndex.HasValue)
            {
                t.AddCondition(AnimatorConditionMode.Equals, skillIndex.Value, "SkillIndex");
            }
        }

        private static AnimationClip CreateSpriteClip(string clipPath, string[] spritePaths, float fps, bool loop)
        {
            List<Sprite> sprites = new List<Sprite>();
            foreach (string path in spritePaths)
            {
                Sprite sp = SpriteHelper.LoadSprite(path);
                if (sp != null) sprites.Add(sp);
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.ClearCurves();
            clip.frameRate = fps;

            if (sprites.Count > 0)
            {
                EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
                int keyCount = sprites.Count == 1 ? 2 : sprites.Count;
                ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[keyCount];
                for (int i = 0; i < sprites.Count; i++)
                {
                    keyframes[i] = new ObjectReferenceKeyframe
                    {
                        time = i / fps,
                        value = sprites[i]
                    };
                }
                if (sprites.Count == 1)
                {
                    keyframes[1] = new ObjectReferenceKeyframe
                    {
                        time = 1f / fps,
                        value = sprites[0]
                    };
                }
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static string[] RangePaths(string prefix, int count)
        {
            string[] paths = new string[count];
            for (int i = 0; i < count; i++)
            {
                paths[i] = $"{prefix}{i}.png";
            }
            return paths;
        }
    }
}
