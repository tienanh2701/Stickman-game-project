using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Characters.Stats;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Enemies.Stats;
using StickmanBeatEmUp.Combat;
using StickmanBeatEmUp.Level;
using StickmanBeatEmUp.Environment;
using StickmanBeatEmUp.Items;

namespace StickmanBeatEmUp.Editor
{
    /// <summary>
    /// Chuyên trách tự động sinh toàn bộ ScriptableObjects và Prefabs:
    /// - 3 Nhân vật Player (Goku, Eren, Diend)
    /// - 4 Quái thường (Grunt, Ranger, Kamikaze, Elite Bruiser)
    /// - 1 World Boss 2 Phase (Warlord)
    /// - Toàn bộ FX/Đạn/Chưởng/Vùng nổ/Sóng xung kích/Lord Kiva AI
    /// </summary>
    public static class PrefabBuilder
    {
        public const string PREFABS_ROOT = "Assets/Prefabs";
        public const string DATA_ROOT = "Assets/Data";

        public static void GenerateAllAssets(
            out GameObject gokuPrefab, out GameObject erenPrefab, out GameObject diendPrefab,
            out GameObject gruntPrefab, out GameObject rangerPrefab, out GameObject kamikazePrefab, out GameObject elitePrefab, out GameObject bossPrefab,
            out StageDataSO stageData)
        {
            GenerateAllAssets(
                out gokuPrefab, out erenPrefab, out diendPrefab,
                out gruntPrefab, out rangerPrefab, out kamikazePrefab, out elitePrefab, out bossPrefab,
                out List<StageDataSO> allStages
            );
            stageData = allStages != null && allStages.Count > 0 ? allStages[0] : null;
        }

        public static void GenerateAllAssets(
            out GameObject gokuPrefab, out GameObject erenPrefab, out GameObject diendPrefab,
            out GameObject gruntPrefab, out GameObject rangerPrefab, out GameObject kamikazePrefab, out GameObject elitePrefab, out GameObject bossPrefab,
            out List<StageDataSO> allStageDataList)
        {
            GenerateAllAssets(
                out gokuPrefab, out erenPrefab, out diendPrefab,
                out gruntPrefab, out rangerPrefab, out kamikazePrefab, out elitePrefab, out bossPrefab,
                out allStageDataList,
                out List<LevelDataSO> _
            );
        }

        public static void GenerateAllAssets(
            out GameObject gokuPrefab, out GameObject erenPrefab, out GameObject diendPrefab,
            out GameObject gruntPrefab, out GameObject rangerPrefab, out GameObject kamikazePrefab, out GameObject elitePrefab, out GameObject bossPrefab,
            out List<StageDataSO> allStageDataList,
            out List<LevelDataSO> allLevelDataList)
        {
            SpriteHelper.EnsureFolder(PREFABS_ROOT);
            SpriteHelper.EnsureFolder($"{PREFABS_ROOT}/Combat");
            SpriteHelper.EnsureFolder($"{PREFABS_ROOT}/Characters");
            SpriteHelper.EnsureFolder($"{PREFABS_ROOT}/Enemies");
            SpriteHelper.EnsureFolder($"{PREFABS_ROOT}/Enemies/World1_AoT");
            SpriteHelper.EnsureFolder($"{PREFABS_ROOT}/Enemies/World2_DB");
            SpriteHelper.EnsureFolder($"{PREFABS_ROOT}/Enemies/World3_Diend");
            SpriteHelper.EnsureFolder($"{PREFABS_ROOT}/Hazards");
            SpriteHelper.EnsureFolder("Assets/Resources");
            SpriteHelper.EnsureFolder("Assets/Resources/Items");
            SpriteHelper.EnsureFolder(DATA_ROOT);
            SpriteHelper.EnsureFolder($"{DATA_ROOT}/Stages");
            SpriteHelper.EnsureFolder($"{DATA_ROOT}/Levels");
            SpriteHelper.EnsureFolder($"{DATA_ROOT}/Enemies");

            SpriteHelper.ConfigureAllProjectSprites();
            AnimationBuilder.BuildAllAnimations(
                out RuntimeAnimatorController gokuAnim,
                out RuntimeAnimatorController erenAnim,
                out RuntimeAnimatorController diendAnim,
                out RuntimeAnimatorController kivaAnim
            );
            Dictionary<string, RuntimeAnimatorController> enemyAnims = AnimationBuilder.BuildAllEnemyAnimations();

            Sprite squareSprite = SpriteHelper.GetOrCreateSquareSprite();
            Sprite circleSprite = SpriteHelper.GetOrCreateCircleSprite();

            const string combatSprites = "Assets/Sprites/Combat";
            const string enemySprites = "Assets/Sprites/Enemies";
            const string gokuSprites = "Assets/Sprites/Characters/Goku";
            const string erenSprites = "Assets/Sprites/Characters/Eren";
            const string diendSprites = "Assets/Sprites/Characters/Diend";

            LayerMask groundLayer = LayerMask.GetMask("Ground", "Default");
            LayerMask enemyLayer = LayerMask.GetMask("Enemy");
            LayerMask playerLayer = LayerMask.GetMask("Player");

            // 1. Tạo ScriptableObjects cho Stats
            CharacterStatsSO gokuStats = CreateOrLoadSO<CharacterStatsSO>($"{DATA_ROOT}/GokuStats.asset");
            gokuStats.characterName = "Goku Stickman";
            gokuStats.maxHealth = 100f;
            gokuStats.moveSpeed = 6.5f;
            gokuStats.jumpForce = 12.0f;
            gokuStats.dashSpeed = 16f;
            gokuStats.normalAttackDamage = 2f;
            gokuStats.skill1Damage = 3f;
            gokuStats.skill2Damage = 10f;
            gokuStats.dashAttackComboDamage = 6f;
            gokuStats.transformBonusHealth = 0f;
            gokuStats.transformedNormalAttackDamage = 4f;
            gokuStats.transformedSkill1Damage = 6f;
            gokuStats.transformedSkill2Damage = 15f;
            gokuStats.specialComboDamage = 35f;
            EditorUtility.SetDirty(gokuStats);

            CharacterStatsSO erenStats = CreateOrLoadSO<CharacterStatsSO>($"{DATA_ROOT}/ErenStats.asset");
            erenStats.characterName = "Eren Stickman";
            erenStats.maxHealth = 100f;
            erenStats.moveSpeed = 6f;
            erenStats.jumpForce = 12.0f;
            erenStats.dashSpeed = 15f;
            erenStats.normalAttackDamage = 2f;
            erenStats.skill1Damage = 3f;
            erenStats.skill2Damage = 10f;
            erenStats.dashAttackComboDamage = 6f;
            erenStats.transformBonusHealth = 0f;
            erenStats.transformedNormalAttackDamage = 4f;
            erenStats.transformedSkill1Damage = 6f;
            erenStats.transformedSkill2Damage = 22f;
            erenStats.specialComboDamage = 35f;
            EditorUtility.SetDirty(erenStats);

            CharacterStatsSO diendStats = CreateOrLoadSO<CharacterStatsSO>($"{DATA_ROOT}/DiendStats.asset");
            diendStats.characterName = "Diend Stickman";
            diendStats.maxHealth = 100f;
            diendStats.moveSpeed = 6f;
            diendStats.jumpForce = 12.0f;
            diendStats.dashSpeed = 15f;
            diendStats.normalAttackDamage = 1f;
            diendStats.skill1Damage = 3f;
            diendStats.skill2Damage = 10f;
            diendStats.dashAttackComboDamage = 6f;
            diendStats.transformBonusHealth = 0f;
            diendStats.transformedNormalAttackDamage = 4f;
            diendStats.transformedSkill1Damage = 6f;
            diendStats.transformedSkill2Damage = 15f;
            diendStats.specialComboDamage = 35f;
            EditorUtility.SetDirty(diendStats);

            EnemyStatsSO gruntStats = CreateOrLoadSO<EnemyStatsSO>($"{DATA_ROOT}/GruntStats.asset");
            gruntStats.enemyName = "Grunt";
            gruntStats.maxHealth = 40f;
            gruntStats.moveSpeed = 3.5f;
            gruntStats.attackDamage = 8f;
            gruntStats.hasSuperArmor = false;
            EditorUtility.SetDirty(gruntStats);

            EnemyStatsSO rangerStats = CreateOrLoadSO<EnemyStatsSO>($"{DATA_ROOT}/RangerStats.asset");
            rangerStats.enemyName = "Ranger";
            rangerStats.maxHealth = 30f;
            rangerStats.moveSpeed = 4f;
            rangerStats.attackDamage = 12f;
            rangerStats.preferredDistance = 7f;
            rangerStats.fleeDistance = 3.5f;
            rangerStats.hasSuperArmor = false;
            EditorUtility.SetDirty(rangerStats);

            EnemyStatsSO kamikazeStats = CreateOrLoadSO<EnemyStatsSO>($"{DATA_ROOT}/KamikazeStats.asset");
            kamikazeStats.enemyName = "Kamikaze";
            kamikazeStats.maxHealth = 25f;
            kamikazeStats.moveSpeed = 5.5f;
            kamikazeStats.attackDamage = 50f;
            kamikazeStats.hasSuperArmor = false;
            EditorUtility.SetDirty(kamikazeStats);

            EnemyStatsSO eliteStats = CreateOrLoadSO<EnemyStatsSO>($"{DATA_ROOT}/EliteStats.asset");
            eliteStats.enemyName = "Elite Bruiser";
            eliteStats.maxHealth = 180f;
            eliteStats.moveSpeed = 2.8f;
            eliteStats.attackDamage = 25f;
            eliteStats.hasSuperArmor = true; // Bật Super Armor!
            EditorUtility.SetDirty(eliteStats);

            EnemyStatsSO bossStats = CreateOrLoadSO<EnemyStatsSO>($"{DATA_ROOT}/BossStats.asset");
            bossStats.enemyName = "World Boss Warlord";
            bossStats.maxHealth = 600f;
            bossStats.moveSpeed = 3.2f;
            bossStats.attackDamage = 35f;
            bossStats.hasSuperArmor = true; // Bật Super Armor!
            EditorUtility.SetDirty(bossStats);

            // 2. Tạo Combat Prefabs với Sprite thực tế
            GameObject kiBlastPrefab = CreateProjectilePrefab("KiBlast", SpriteHelper.LoadSprite($"{combatSprites}/FX_KiOrb.png", circleSprite), Color.white, 14f, 2.5f, 0.9f, false);
            GameObject kiExplosionPrefab = CreateExplosionPrefab("KiExplosion", SpriteHelper.LoadSprite($"{combatSprites}/FX_Explosion_0.png", circleSprite), Color.white, 3.5f, 1.4f);
            GameObject kamehamehaPrefab = CreateBeamPrefab("KamehamehaBeam", new Color(0.2f, 0.8f, 1f), 20f, 2.5f);
            GameObject genkiDamaPrefab = CreateExplosionPrefab("GenkiDama", SpriteHelper.LoadSprite($"{combatSprites}/FX_GenkiDamaOrb.png", circleSprite), Color.white, 8f, 2.8f);

            GameObject thunderSpearPrefab = CreateProjectilePrefab("ThunderSpear", SpriteHelper.LoadSprite($"{combatSprites}/FX_ThunderSpear.png", squareSprite), Color.white, 16f, 2f, 1.0f, false);
            GameObject titanShockwavePrefab = CreateShockwavePrefab("TitanShockwave", SpriteHelper.LoadSprite($"{combatSprites}/FX_Shockwave.png", squareSprite), Color.white, 9f);
            GameObject rockProjectilePrefab = CreateProjectilePrefab("RockProjectile", SpriteHelper.LoadSprite($"{combatSprites}/FX_RockImpact.png", circleSprite), Color.white, 10f, 3f, 1.2f, false);
            GameObject colossalStompPrefab = CreateTitanStompPrefab("ColossalStomp", SpriteHelper.LoadSprite($"{combatSprites}/FX_TitanFoot.png", squareSprite));

            GameObject diendBulletPrefab = CreateProjectilePrefab("DiendBullet", SpriteHelper.LoadSprite($"{combatSprites}/FX_DiendCard.png", squareSprite), Color.white, 18f, 2f, 0.9f, false);
            GameObject phantomSlashPrefab = CreatePhantomSlashPrefab("PhantomSlashVisual", SpriteHelper.LoadSprite($"{combatSprites}/FX_PhantomKivaSlash.png", squareSprite), Color.white);
            GameObject piercingBeamPrefab = CreateBeamPrefab("PiercingBeam", new Color(0f, 0.9f, 1f), 18f, 1.2f);
            GameObject wingFreezeVfxPrefab = CreateSimpleVfxPrefab("DarkKivaFreezeVfx", SpriteHelper.LoadSprite($"{combatSprites}/FX_DarkKivaFreezeBurst.png", circleSprite), Color.white, new Vector3(1.8f, 1.8f, 1f));
            GameObject lordKivaPrefab = CreateLordKivaPrefab(
                "LordKivaAI",
                SpriteHelper.LoadSprite($"{diendSprites}/LordKiva_Idle_0.png", squareSprite),
                kivaAnim,
                wingFreezeVfxPrefab,
                enemyLayer
            );

            GameObject groundLaserPrefab = CreateBeamPrefab("BossGroundLaser", new Color(1f, 0.1f, 0.1f), 22f, 1.8f);
            GameObject carpetBombPrefab = CreateExplosionPrefab("CarpetBombExplosion", SpriteHelper.LoadSprite($"{combatSprites}/FX_Explosion_1.png", circleSprite), Color.white, 3.2f, 1.3f);
            GameObject aoeWarningPrefab = CreateWarningIndicatorPrefab("AOEWarningIndicator", SpriteHelper.LoadSprite($"{combatSprites}/FX_WarningCircle.png", circleSprite), new Color(1f, 1f, 1f, 0.75f));

            // 3. Tạo Enemy Prefabs với Sprite thực tế
            gruntPrefab = CreateEnemyPrefab<GruntEnemy>(
                "Enemy_Grunt",
                SpriteHelper.LoadSprite($"{enemySprites}/Enemy_Grunt.png", squareSprite),
                circleSprite,
                Color.white,
                Vector3.one,
                new Vector2(0.75f, 1.5f),
                gruntStats,
                playerLayer,
                8
            );
            rangerPrefab = CreateRangerEnemyPrefab(
                "Enemy_Ranger",
                SpriteHelper.LoadSprite($"{enemySprites}/Enemy_Ranger.png", squareSprite),
                circleSprite,
                Color.white,
                rangerStats,
                diendBulletPrefab,
                playerLayer,
                8
            );
            kamikazePrefab = CreateKamikazeEnemyPrefab(
                "Enemy_Kamikaze",
                SpriteHelper.LoadSprite($"{enemySprites}/Enemy_Kamikaze.png", circleSprite),
                circleSprite,
                Color.white,
                kamikazeStats,
                kiExplosionPrefab,
                playerLayer,
                8
            );
            elitePrefab = CreateEliteEnemyPrefab(
                "Enemy_EliteBruiser",
                SpriteHelper.LoadSprite($"{enemySprites}/Enemy_Elite.png", squareSprite),
                circleSprite,
                Color.white,
                eliteStats,
                titanShockwavePrefab,
                playerLayer,
                8
            );

            bossPrefab = CreateBossPrefab(
                "Boss_Warlord",
                SpriteHelper.LoadSprite($"{enemySprites}/Enemy_Boss.png", squareSprite),
                circleSprite,
                Color.white,
                bossStats,
                groundLaserPrefab,
                kamikazePrefab,
                carpetBombPrefab,
                aoeWarningPrefab,
                playerLayer,
                8
            );

            // 4. Tạo Character Prefabs với Sprite + AnimatorController thực tế
            gokuPrefab = CreateCharacterPrefab<GokuController>(
                "Player_Goku",
                SpriteHelper.LoadSprite($"{gokuSprites}/Goku_Idle_0.png", squareSprite),
                circleSprite,
                new Color(1f, 0.55f, 0.1f, 0.6f),
                gokuAnim,
                gokuStats,
                groundLayer,
                enemyLayer,
                (goku, so) =>
                {
                    so.FindProperty("kiBlastPrefab").objectReferenceValue = kiBlastPrefab;
                    so.FindProperty("kiExplosionPrefab").objectReferenceValue = kiExplosionPrefab;
                    so.FindProperty("kamehamehaPrefab").objectReferenceValue = kamehamehaPrefab;
                    so.FindProperty("genkiDamaPrefab").objectReferenceValue = genkiDamaPrefab;
                }
            );

            erenPrefab = CreateCharacterPrefab<ErenController>(
                "Player_Eren",
                SpriteHelper.LoadSprite($"{erenSprites}/Eren_Idle_0.png", squareSprite),
                circleSprite,
                new Color(0.25f, 0.85f, 0.35f, 0.6f),
                erenAnim,
                erenStats,
                groundLayer,
                enemyLayer,
                (eren, so) =>
                {
                    so.FindProperty("thunderSpearPrefab").objectReferenceValue = thunderSpearPrefab;
                    so.FindProperty("titanShockwavePrefab").objectReferenceValue = titanShockwavePrefab;
                    so.FindProperty("rockProjectilePrefab").objectReferenceValue = rockProjectilePrefab;
                    so.FindProperty("colossalStompPrefab").objectReferenceValue = colossalStompPrefab;
                },
                false
            );

            diendPrefab = CreateCharacterPrefab<DiendController>(
                "Player_Diend",
                SpriteHelper.LoadSprite($"{diendSprites}/Diend_Idle_0.png", squareSprite),
                circleSprite,
                new Color(0.1f, 0.85f, 0.95f, 0.6f),
                diendAnim,
                diendStats,
                groundLayer,
                enemyLayer,
                (diend, so) =>
                {
                    so.FindProperty("bulletPrefab").objectReferenceValue = diendBulletPrefab;
                    so.FindProperty("phantomSlashVisualPrefab").objectReferenceValue = phantomSlashPrefab;
                    so.FindProperty("piercingBeamPrefab").objectReferenceValue = piercingBeamPrefab;
                    so.FindProperty("lordKivaPrefab").objectReferenceValue = lordKivaPrefab;
                }
            );

            // 5. Tạo Consumable Items (Resources/Items) & 14 Campaign Hazard Prefabs
            CreateConsumableItemAssets(squareSprite);
            CreateAllHazardPrefabs(squareSprite, circleSprite, kiExplosionPrefab);

            // 6. Tạo Enemy FX & 13 Prefabs Quái/Elite/Boss chuẩn hóa theo 3 World + 12 LevelDataSO
            allLevelDataList = BuildWorldEnemiesAnd12Levels(
                enemyAnims, squareSprite, circleSprite, kiExplosionPrefab, playerLayer
            );

            // Giữ tương thích StageDataSO list từ 12 LevelDataSO
            allStageDataList = new List<StageDataSO>();
            foreach (LevelDataSO lvl in allLevelDataList)
            {
                allStageDataList.Add(lvl);
            }

            // Vẫn cập nhật các StageDataSO cũ nếu có reference sẵn
            BuildAll12Stages(gruntPrefab, rangerPrefab, kamikazePrefab, elitePrefab, bossPrefab);
            AssetDatabase.SaveAssets();
        }

        private static List<StageDataSO> BuildAll12Stages(
            GameObject gruntPrefab, GameObject rangerPrefab, GameObject kamikazePrefab, GameObject elitePrefab, GameObject bossPrefab)
        {
            var stages = new List<StageDataSO>();
            string[] worldLabels =
            {
                "World 1 - Ruined City (AoT)",
                "World 2 - Wasteland (Dragon Ball)",
                "World 3 - Neo City (Diend World)"
            };

            for (int w = 0; w < 3; w++)
            {
                WorldType worldType = (WorldType)w;
                for (int s = 1; s <= 4; s++)
                {
                    string assetPath = (w == 0 && s == 1)
                        ? $"{DATA_ROOT}/World1_Stage1_Data.asset"
                        : $"{DATA_ROOT}/Stages/World{w + 1}_Stage{s}_Data.asset";

                    StageDataSO st = CreateOrLoadSO<StageDataSO>(assetPath);
                    st.worldType = worldType;
                    st.stageNumber = s;
                    st.stageName = $"{worldLabels[w]} - Stage {s}" + (s == 4 ? " [BOSS]" : "");
                    st.lockTriggerX = 8f;
                    st.lockedCameraMinX = 5f;
                    st.lockedCameraMaxX = 15f;
                    st.isBossStage = (s == 4);
                    st.bossPrefab = s == 4 ? bossPrefab : null;
                    st.bossSpawnPosition = new Vector2(14f, 0.2f);
                    st.waves.Clear();

                    int extra = w;
                    if (s == 1)
                    {
                        WaveData w1 = new WaveData { waveName = "Wave 1 - Grunts", delayBeforeWave = 1.0f };
                        for (int i = 0; i < 2 + extra; i++)
                        {
                            float sign = (i % 2 == 0) ? 1f : -1f;
                            w1.enemies.Add(new EnemySpawnEntry { enemyPrefab = gruntPrefab, spawnOffset = new Vector2(sign * (5.5f + i * 0.5f), 0f), spawnDelay = i * 0.4f });
                        }
                        st.waves.Add(w1);

                        WaveData w2 = new WaveData { waveName = "Wave 2 - Rangers", delayBeforeWave = 1.2f };
                        w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = gruntPrefab, spawnOffset = new Vector2(-5.5f, 0f), spawnDelay = 0f });
                        w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = rangerPrefab, spawnOffset = new Vector2(6.5f, 0f), spawnDelay = 0.6f });
                        if (w >= 1) w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = rangerPrefab, spawnOffset = new Vector2(-6.5f, 0f), spawnDelay = 0.8f });
                        st.waves.Add(w2);
                    }
                    else if (s == 2)
                    {
                        WaveData w1 = new WaveData { waveName = "Wave 1 - Ambush", delayBeforeWave = 1.0f };
                        w1.enemies.Add(new EnemySpawnEntry { enemyPrefab = gruntPrefab, spawnOffset = new Vector2(5.5f, 0f), spawnDelay = 0f });
                        w1.enemies.Add(new EnemySpawnEntry { enemyPrefab = kamikazePrefab, spawnOffset = new Vector2(-6.0f, 0f), spawnDelay = 0.5f });
                        w1.enemies.Add(new EnemySpawnEntry { enemyPrefab = rangerPrefab, spawnOffset = new Vector2(6.5f, 0f), spawnDelay = 0.8f });
                        st.waves.Add(w1);

                        WaveData w2 = new WaveData { waveName = "Wave 2 - Bomb Squad", delayBeforeWave = 1.5f };
                        w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = kamikazePrefab, spawnOffset = new Vector2(6.0f, 0f), spawnDelay = 0f });
                        w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = kamikazePrefab, spawnOffset = new Vector2(-6.0f, 0f), spawnDelay = 0.5f });
                        for (int i = 0; i < 1 + extra; i++)
                        {
                            w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = gruntPrefab, spawnOffset = new Vector2(4.5f + i, 0f), spawnDelay = 0.8f });
                        }
                        st.waves.Add(w2);
                    }
                    else if (s == 3)
                    {
                        WaveData w1 = new WaveData { waveName = "Wave 1 - Vanguard", delayBeforeWave = 1.0f };
                        w1.enemies.Add(new EnemySpawnEntry { enemyPrefab = gruntPrefab, spawnOffset = new Vector2(5.5f, 0f), spawnDelay = 0f });
                        w1.enemies.Add(new EnemySpawnEntry { enemyPrefab = rangerPrefab, spawnOffset = new Vector2(-6.5f, 0f), spawnDelay = 0.4f });
                        w1.enemies.Add(new EnemySpawnEntry { enemyPrefab = elitePrefab, spawnOffset = new Vector2(6.8f, 0f), spawnDelay = 1.0f });
                        st.waves.Add(w1);

                        WaveData w2 = new WaveData { waveName = "Wave 2 - Heavy Assault", delayBeforeWave = 1.5f };
                        w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = kamikazePrefab, spawnOffset = new Vector2(-5.5f, 0f), spawnDelay = 0f });
                        w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = elitePrefab, spawnOffset = new Vector2(6.5f, 0f), spawnDelay = 0.7f });
                        if (w >= 1) w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = rangerPrefab, spawnOffset = new Vector2(-6.5f, 0f), spawnDelay = 1.0f });
                        st.waves.Add(w2);
                    }
                    else
                    {
                        WaveData w1 = new WaveData { waveName = "Wave 1 - Honor Guard", delayBeforeWave = 1.0f };
                        w1.enemies.Add(new EnemySpawnEntry { enemyPrefab = gruntPrefab, spawnOffset = new Vector2(5.5f, 0f), spawnDelay = 0f });
                        w1.enemies.Add(new EnemySpawnEntry { enemyPrefab = rangerPrefab, spawnOffset = new Vector2(-6.5f, 0f), spawnDelay = 0.5f });
                        st.waves.Add(w1);

                        WaveData w2 = new WaveData { waveName = "Wave 2 - Gatekeeper", delayBeforeWave = 1.2f };
                        w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = kamikazePrefab, spawnOffset = new Vector2(-5.5f, 0f), spawnDelay = 0f });
                        w2.enemies.Add(new EnemySpawnEntry { enemyPrefab = elitePrefab, spawnOffset = new Vector2(6.5f, 0f), spawnDelay = 0.8f });
                        st.waves.Add(w2);
                    }

                    EditorUtility.SetDirty(st);
                    stages.Add(st);
                }
            }

            return stages;
        }

        #region World-Exclusive Enemies & 12 Levels
        private static List<LevelDataSO> BuildWorldEnemiesAnd12Levels(
            Dictionary<string, RuntimeAnimatorController> anims,
            Sprite squareSprite, Sprite circleSprite,
            GameObject explosionPrefab, LayerMask playerLayer)
        {
            const string combatSprites = "Assets/Sprites/Combat";
            const string w1Sprites = "Assets/Sprites/Enemies/World1_AoT";
            const string w2Sprites = "Assets/Sprites/Enemies/World2_DB";
            const string w3Sprites = "Assets/Sprites/Enemies/World3_Diend";

            // New World-specific projectiles & FX
            GameObject beastBoulderPrefab = CreateProjectilePrefab("BeastBoulderProjectile", SpriteHelper.LoadSprite($"{combatSprites}/FX_BeastBoulderVolley.png", circleSprite), Color.white, 11f, 3.2f, 1.0f, false);
            GameObject friezaKiPrefab = CreateProjectilePrefab("FriezaKiBolt", SpriteHelper.LoadSprite($"{combatSprites}/FX_KiOrb.png", circleSprite), new Color(1f, 0.35f, 0.7f, 1f), 12.5f, 2.5f, 0.75f, false);
            GameObject kashinSpearPrefab = CreateProjectilePrefab("KasshinSpearProjectile", SpriteHelper.LoadSprite($"{combatSprites}/FX_KasshinSpear.png", squareSprite), Color.white, 14f, 2.5f, 0.9f, false);
            GameObject crescentWavePrefab = CreateProjectilePrefab("ShadowCrescentWave", SpriteHelper.LoadSprite($"{combatSprites}/FX_CrescentSlashWave.png", squareSprite), Color.white, 13.5f, 2.5f, 1.15f, true);
            GameObject nappaPillarVfx = CreateSimpleVfxPrefab("NappaStormPillarVfx", SpriteHelper.LoadSprite($"{combatSprites}/FX_NappaPillar.png", squareSprite), Color.white, new Vector3(1.5f, 1.8f, 1f));
            GameObject warHammerSpikesVfx = CreateSimpleVfxPrefab("WarHammerSpikesVfx", SpriteHelper.LoadSprite($"{combatSprites}/FX_GroundSpikes.png", squareSprite), Color.white, new Vector3(1.6f, 1.5f, 1f));

            // 1. EnemyDataSO configs
            EnemyDataSO pureTitanData = CreateEnemyDataSO("W1_PureTitanData", "Pure Titan", WorldType.World1_RuinedCity, false, false, 10f, 3.5f, 5f, 5f, 1.55f, 1.5f, false, 0f, 0f);
            EnemyDataSO beastTitanData = CreateEnemyDataSO("W1_BeastTitanData", "Beast Titan", WorldType.World1_RuinedCity, false, false, 10f, 3.5f, 3f, 3f, 1.6f, 2.1f, false, 6.8f, 2.8f);
            EnemyDataSO femaleTitanData = CreateEnemyDataSO("W1_FemaleTitanData", "Female Titan", WorldType.World1_RuinedCity, true, false, 50f, 2.45f, 10f, 15f, 1.9f, 1.5f, false, 0f, 0f);
            EnemyDataSO warHammerData = CreateEnemyDataSO("W1_WarHammerData", "War Hammer Titan", WorldType.World1_RuinedCity, false, true, 150f, 2.45f, 15f, 20f, 2.6f, 1.85f, true, 0f, 0f);

            EnemyDataSO saibamenData = CreateEnemyDataSO("W2_SaibamenData", "Saibaman", WorldType.World2_Wasteland, false, false, 5f, 5.25f, 12f, 12f, 1.45f, 1.3f, false, 0f, 0f);
            EnemyDataSO saibamanKamiData = CreateEnemyDataSO("W2_SaibamanKamikazeData", "Saibaman Kamikaze", WorldType.World2_Wasteland, false, false, 5f, 5.25f, 12f, 12f, 1.75f, 1.2f, false, 0f, 0f);
            EnemyDataSO friezaSoldierData = CreateEnemyDataSO("W2_FriezaSoldierData", "Frieza Soldier", WorldType.World2_Wasteland, false, false, 10f, 3.5f, 3f, 3f, 1.6f, 1.7f, false, 6.5f, 2.9f);
            EnemyDataSO nappaData = CreateEnemyDataSO("W2_NappaData", "Elite Nappa", WorldType.World2_Wasteland, true, false, 50f, 2.45f, 10f, 18f, 2.1f, 1.65f, true, 0f, 0f);
            EnemyDataSO brolyData = CreateEnemyDataSO("W2_BrolyData", "Berserker Broly", WorldType.World2_Wasteland, false, true, 150f, 3.0f, 15f, 25f, 2.3f, 1.65f, true, 0f, 0f);

            EnemyDataSO shockerData = CreateEnemyDataSO("W3_ShockerData", "Shocker Combatman", WorldType.World3_NeoCity, false, false, 10f, 3.5f, 5f, 5f, 1.5f, 1.25f, false, 0f, 0f);
            EnemyDataSO kashinData = CreateEnemyDataSO("W3_KasshinData", "Kasshin Trooper", WorldType.World3_NeoCity, false, false, 10f, 3.5f, 3f, 3f, 1.6f, 1.65f, false, 7.0f, 3.0f);
            EnemyDataSO shadowMoonData = CreateEnemyDataSO("W3_ShadowMoonData", "Shadow Moon", WorldType.World3_NeoCity, true, false, 50f, 2.45f, 10f, 15f, 1.95f, 1.45f, false, 0f, 0f);
            EnemyDataSO mechaData = CreateEnemyDataSO("W3_MechaData", "Dai-Shocker Mecha", WorldType.World3_NeoCity, false, true, 150f, 1.5f, 15f, 20f, 2.8f, 1.9f, true, 0f, 0f);

            // 2. Build all 13 World Prefabs
            // --- World 1: Attack on Titan ---
            GameObject w1PureTitan = CreateWorldCreepPrefab<MeleeCreep>("World1_AoT/W1_Creep_PureTitan",
                SpriteHelper.LoadSprite($"{w1Sprites}/PureTitan_Move_0.png", squareSprite),
                anims["PureTitan"], pureTitanData, new Vector2(0.95f, 1.8f), new Vector3(1.1f, 1.1f, 1f), playerLayer,
                (c, so) => { so.FindProperty("useAlternatingAttack2").boolValue = true; so.FindProperty("isKamikaze").boolValue = false; });

            GameObject w1BeastTitan = CreateWorldCreepPrefab<RangedCreep>("World1_AoT/W1_Range_BeastTitan",
                SpriteHelper.LoadSprite($"{w1Sprites}/BeastTitan_Move_0.png", squareSprite),
                anims["BeastTitan"], beastTitanData, new Vector2(1.1f, 2.0f), new Vector3(1.25f, 1.25f, 1f), playerLayer,
                (c, so) => { so.FindProperty("projectilePrefab").objectReferenceValue = beastBoulderPrefab; so.FindProperty("volleyCount").intValue = 1; so.FindProperty("volleySpreadDeg").floatValue = 7f; });

            GameObject w1FemaleTitan = CreateWorldCreepPrefab<CounterElite>("World1_AoT/W1_Elite_FemaleTitan",
                SpriteHelper.LoadSprite($"{w1Sprites}/FemaleTitan_FightStance_0.png", squareSprite),
                anims["FemaleTitan"], femaleTitanData, new Vector2(1.0f, 2.0f), new Vector3(1.25f, 1.25f, 1f), playerLayer, null);

            GameObject w1WarHammerBoss = CreateWarHammerBossPrefab(
                anims["WarHammer"], warHammerData,
                SpriteHelper.LoadSprite($"{w1Sprites}/WarHammer_HammerSmash_0.png", squareSprite),
                SpriteHelper.LoadSprite($"{combatSprites}/FX_CrystalCore.png", circleSprite),
                SpriteHelper.LoadSprite($"{w1Sprites}/CartTitan_Idle_0.png", squareSprite),
                warHammerSpikesVfx, beastBoulderPrefab, playerLayer);

            // --- World 2: Dragon Ball ---
            GameObject w2Saibamen = CreateWorldCreepPrefab<KamikazeCreep>("World2_DB/W2_Creep_Saibamen",
                SpriteHelper.LoadSprite($"{w2Sprites}/Saibamen_Move_0.png", squareSprite),
                anims["Saibamen"], saibamenData, new Vector2(0.8f, 1.4f), Vector3.one, playerLayer,
                (c, so) => { so.FindProperty("explosionVfxPrefab").objectReferenceValue = explosionPrefab; });

            GameObject w2SaibamanKamikaze = CreateWorldCreepPrefab<KamikazeCreep>("World2_DB/W2_Creep_SaibamanKamikaze",
                SpriteHelper.LoadSprite($"{w2Sprites}/Saibamen_WindUp_0.png", squareSprite),
                anims["Saibamen"], saibamanKamiData, new Vector2(0.8f, 1.4f), Vector3.one, playerLayer,
                (c, so) => { so.FindProperty("explosionVfxPrefab").objectReferenceValue = explosionPrefab; });

            GameObject w2FriezaSoldier = CreateWorldCreepPrefab<RangedCreep>("World2_DB/W2_Range_FriezaSoldier",
                SpriteHelper.LoadSprite($"{w2Sprites}/FriezaSoldier_Move_0.png", squareSprite),
                anims["FriezaSoldier"], friezaSoldierData, new Vector2(0.8f, 1.55f), Vector3.one, playerLayer,
                (c, so) => { so.FindProperty("projectilePrefab").objectReferenceValue = friezaKiPrefab; so.FindProperty("volleyCount").intValue = 1; });

            GameObject w2Nappa = CreateWorldCreepPrefab<BrawlerElite>("World2_DB/W2_Elite_Nappa",
                SpriteHelper.LoadSprite($"{w2Sprites}/Nappa_Advance_0.png", squareSprite),
                anims["Nappa"], nappaData, new Vector2(1.05f, 1.95f), new Vector3(1.25f, 1.25f, 1f), playerLayer,
                (c, so) =>
                {
                    so.FindProperty("giantStormPillarPrefab").objectReferenceValue = nappaPillarVfx;
                    so.FindProperty("bomberDxPrefab").objectReferenceValue = friezaKiPrefab;
                    so.FindProperty("summonSaibamanPrefab").objectReferenceValue = w2Saibamen;
                    so.FindProperty("bomberDxDamage").floatValue = 12f;
                });

            GameObject w2BrolyBoss = CreateBrolyBossPrefab(
                anims["Broly"], brolyData,
                SpriteHelper.LoadSprite($"{w2Sprites}/Broly_HeavyStomp_0.png", squareSprite),
                SpriteHelper.LoadSprite($"{combatSprites}/FX_GreenKiShield.png", circleSprite),
                playerLayer);

            // --- World 3: Kamen Rider Diend ---
            GameObject w3Shocker = CreateWorldCreepPrefab<MeleeCreep>("World3_Diend/W3_Creep_Shocker",
                SpriteHelper.LoadSprite($"{w3Sprites}/Shocker_Move_0.png", squareSprite),
                anims["Shocker"], shockerData, new Vector2(0.8f, 1.55f), Vector3.one, playerLayer,
                (c, so) => { so.FindProperty("useAlternatingAttack2").boolValue = true; so.FindProperty("isKamikaze").boolValue = false; });

            GameObject w3Kasshin = CreateWorldCreepPrefab<RangedCreep>("World3_Diend/W3_Range_Kasshin",
                SpriteHelper.LoadSprite($"{w3Sprites}/Kasshin_Move_0.png", squareSprite),
                anims["Kasshin"], kashinData, new Vector2(0.85f, 1.65f), new Vector3(1.05f, 1.05f, 1f), playerLayer,
                (c, so) => { so.FindProperty("projectilePrefab").objectReferenceValue = kashinSpearPrefab; so.FindProperty("volleyCount").intValue = 1; });

            GameObject w3ShadowMoon = CreateWorldCreepPrefab<TeleportElite>("World3_Diend/W3_Elite_ShadowMoon",
                SpriteHelper.LoadSprite($"{w3Sprites}/ShadowMoon_Walk_0.png", squareSprite),
                anims["ShadowMoon"], shadowMoonData, new Vector2(0.95f, 1.85f), new Vector3(1.2f, 1.2f, 1f), playerLayer,
                (c, so) => { so.FindProperty("crescentWavePrefab").objectReferenceValue = crescentWavePrefab; });

            GameObject w3MechaBoss = CreateMechaBossPrefab(
                anims["MechaColossus"], mechaData,
                SpriteHelper.LoadSprite($"{w3Sprites}/Mecha_Body.png", squareSprite),
                SpriteHelper.LoadSprite($"{w3Sprites}/Mecha_LeftArm.png", squareSprite),
                SpriteHelper.LoadSprite($"{w3Sprites}/Mecha_RightArm.png", squareSprite),
                SpriteHelper.LoadSprite($"{w3Sprites}/Mecha_ChestCore.png", circleSprite),
                kashinSpearPrefab, playerLayer);

            // 3. Build all 12 World-Exclusive LevelDataSO assets
            Sprite mapW1 = SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_World1_AoT.png", squareSprite);
            Sprite mapW2 = SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_World2_DB.png", squareSprite);
            Sprite mapW3 = SpriteHelper.LoadSprite("Assets/Sprites/Maps/Map_World3_Diend.png", squareSprite);

            var levels = new List<LevelDataSO>();
            levels.AddRange(Create4WorldLevels(1, WorldType.World1_RuinedCity, "Attack on Titan", mapW1, new Color(0.22f, 0.21f, 0.19f, 1f), w1PureTitan, w1BeastTitan, null, w1FemaleTitan, w1WarHammerBoss));
            levels.AddRange(Create4WorldLevels(2, WorldType.World2_Wasteland, "Dragon Ball Wasteland", mapW2, new Color(0.27f, 0.20f, 0.14f, 1f), w2Saibamen, w2FriezaSoldier, w2SaibamanKamikaze, w2Nappa, w2BrolyBoss));
            levels.AddRange(Create4WorldLevels(3, WorldType.World3_NeoCity, "Diend Neo Ruins", mapW3, new Color(0.16f, 0.16f, 0.24f, 1f), w3Shocker, w3Kasshin, null, w3ShadowMoon, w3MechaBoss));

            return levels;
        }

        private static void AddWaveEntries(
            WaveData wave, GameObject prefab, int count, float delayStart, float delayStep,
            float startOffsetX, float xStep, bool alternateSides, float maxHealthOverride = 0f)
        {
            if (prefab == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                float sign = alternateSides && (i % 2 == 1) ? -1f : 1f;
                float xOff = sign * (startOffsetX + (i / (alternateSides ? 2 : 1)) * xStep);
                wave.enemies.Add(new EnemySpawnEntry
                {
                    enemyPrefab = prefab,
                    spawnOffset = new Vector2(xOff, 0f),
                    spawnDelay = delayStart + i * delayStep,
                    maxHealthOverride = maxHealthOverride
                });
            }
        }

        private static List<LevelDataSO> Create4WorldLevels(
            int worldNum, WorldType worldType, string worldTitle, Sprite bgSprite, Color groundColor,
            GameObject meleeCreep, GameObject rangedCreep, GameObject extraCreep, GameObject elite, GameObject boss)
        {
            var list = new List<LevelDataSO>();
            for (int s = 1; s <= 4; s++)
            {
                LevelDataSO lvl = CreateOrLoadSO<LevelDataSO>($"{DATA_ROOT}/Levels/Level_W{worldNum}_S{s}.asset");
                lvl.worldType = worldType;
                lvl.stageNumber = s;
                lvl.stageName = $"World {worldNum} ({worldTitle}) - Stage {s}" + (s == 4 ? " [BOSS]" : (s == 3 ? " [ELITE]" : ""));
                Sprite stageBg = SpriteHelper.LoadSprite($"Assets/Sprites/Maps/Map_W{worldNum}_S{s}.png", bgSprite);
                lvl.backgroundSprite = stageBg != null ? stageBg : bgSprite;
                lvl.groundColor = s == 1
                    ? Color.Lerp(groundColor, Color.white, 0.2f)
                    : (s == 2
                        ? Color.Lerp(groundColor, new Color(0.45f, 0.30f, 0.22f, 1f), 0.45f)
                        : (s == 3
                            ? Color.Lerp(groundColor, new Color(0.20f, 0.24f, 0.36f, 1f), 0.45f)
                            : Color.Lerp(groundColor, new Color(0.36f, 0.16f, 0.18f, 1f), 0.50f)));
                lvl.enemyHpMultiplier = 1f;
                lvl.enemyDamageMultiplier = 1f;
                lvl.supportMeleeCreepPrefab = meleeCreep;
                lvl.supportRangedCreepPrefab = rangedCreep;
                lvl.supportElitePrefab = elite;
                lvl.lockTriggerX = 0.5f;
                lvl.lockedCameraMinX = 1.5f;
                lvl.lockedCameraMaxX = 15.0f;
                lvl.isBossStage = (s == 4);
                lvl.bossPrefab = (s == 4) ? boss : null;
                lvl.bossSpawnPosition = new Vector2(9.5f, BossBase.GROUND_Y);
                lvl.waves.Clear();

                if (s == 1)
                {
                    // Stage 1: 3 Waves (+4 per enemy type)
                    WaveData w1 = new WaveData { waveName = "Wave 1 - 6 Melee", delayBeforeWave = 0.6f };
                    AddWaveEntries(w1, meleeCreep, 6, 0f, 0.25f, 4.8f, 1.1f, true);
                    lvl.waves.Add(w1);

                    if (extraCreep != null)
                    {
                        WaveData w2 = new WaveData { waveName = "Wave 2 - 6 Melee + 5 Extra", delayBeforeWave = 1.0f };
                        AddWaveEntries(w2, meleeCreep, 6, 0f, 0.25f, 4.8f, 1.0f, true);
                        AddWaveEntries(w2, extraCreep, 5, 0.45f, 0.25f, 5.6f, 0.9f, true);
                        lvl.waves.Add(w2);

                        WaveData w3 = new WaveData { waveName = "Wave 3 - 7 Melee + 5 Extra", delayBeforeWave = 1.1f };
                        AddWaveEntries(w3, meleeCreep, 7, 0f, 0.22f, 4.6f, 1.0f, true);
                        AddWaveEntries(w3, extraCreep, 5, 0.55f, 0.25f, 5.6f, 0.9f, true);
                        lvl.waves.Add(w3);
                    }
                    else
                    {
                        WaveData w2 = new WaveData { waveName = "Wave 2 - 7 Melee", delayBeforeWave = 1.0f };
                        AddWaveEntries(w2, meleeCreep, 7, 0f, 0.25f, 4.8f, 1.0f, true);
                        lvl.waves.Add(w2);

                        WaveData w3 = new WaveData { waveName = "Wave 3 - 8 Melee", delayBeforeWave = 1.1f };
                        AddWaveEntries(w3, meleeCreep, 8, 0f, 0.22f, 4.6f, 1.0f, true);
                        lvl.waves.Add(w3);
                    }
                }
                else if (s == 2)
                {
                    // Stage 2: 4 Waves (+4 per enemy type)
                    WaveData w1 = new WaveData { waveName = "Wave 1 - 7 Melee", delayBeforeWave = 0.6f };
                    AddWaveEntries(w1, meleeCreep, 7, 0f, 0.24f, 4.6f, 1.0f, true);
                    lvl.waves.Add(w1);

                    WaveData w2 = new WaveData { waveName = "Wave 2 - 6 Melee + 6 Ranged", delayBeforeWave = 1.0f };
                    AddWaveEntries(w2, meleeCreep, 6, 0f, 0.24f, 4.5f, 0.95f, true);
                    AddWaveEntries(w2, rangedCreep, 6, 0.35f, 0.28f, 6.4f, 0.85f, true);
                    lvl.waves.Add(w2);

                    WaveData w3 = new WaveData { waveName = "Wave 3 - 7 Melee + 6 Ranged", delayBeforeWave = 1.0f };
                    AddWaveEntries(w3, meleeCreep, 7, 0f, 0.22f, 4.5f, 0.95f, true);
                    AddWaveEntries(w3, rangedCreep, 6, 0.4f, 0.28f, 6.4f, 0.85f, true);
                    lvl.waves.Add(w3);

                    WaveData w4 = new WaveData { waveName = "Wave 4 - 8 Melee + 7 Ranged", delayBeforeWave = 1.1f };
                    AddWaveEntries(w4, meleeCreep, 8, 0f, 0.20f, 4.4f, 0.95f, true);
                    AddWaveEntries(w4, rangedCreep, 7, 0.45f, 0.26f, 6.4f, 0.8f, true);
                    lvl.waves.Add(w4);
                }
                else if (s == 3)
                {
                    // Stage 3: 6 Waves (Waves 1-4 preserved + Wave 5 & Wave 6)
                    WaveData w1 = new WaveData { waveName = "Wave 1 - 3 Melee + 1 Ranged", delayBeforeWave = 0.6f };
                    AddWaveEntries(w1, meleeCreep, 3, 0f, 0.25f, 4.8f, 1.0f, true);
                    AddWaveEntries(w1, rangedCreep, 1, 0.4f, 0.2f, 7.0f, 0f, false);
                    lvl.waves.Add(w1);

                    WaveData w2 = new WaveData { waveName = "Wave 2 - 3 Melee + 2 Ranged", delayBeforeWave = 1.0f };
                    AddWaveEntries(w2, meleeCreep, 3, 0f, 0.25f, 4.6f, 1.0f, true);
                    AddWaveEntries(w2, rangedCreep, 2, 0.35f, 0.3f, 6.8f, 0.9f, true);
                    lvl.waves.Add(w2);

                    WaveData w3 = new WaveData { waveName = "Wave 3 - 4 Melee + 2 Ranged", delayBeforeWave = 1.0f };
                    AddWaveEntries(w3, meleeCreep, 4, 0f, 0.22f, 4.5f, 1.0f, true);
                    AddWaveEntries(w3, rangedCreep, 2, 0.4f, 0.3f, 7.0f, 0.8f, true);
                    lvl.waves.Add(w3);

                    WaveData w4 = new WaveData { waveName = "Wave 4 - Elite + 2 Melee + 2 Ranged", delayBeforeWave = 1.1f };
                    AddWaveEntries(w4, elite, 1, 0f, 0f, 6.2f, 0f, false);
                    AddWaveEntries(w4, meleeCreep, 2, 0.25f, 0.25f, 4.5f, 1.0f, true);
                    AddWaveEntries(w4, rangedCreep, 2, 0.5f, 0.3f, 7.2f, 0.8f, true);
                    lvl.waves.Add(w4);

                    WaveData w5 = new WaveData { waveName = "Wave 5 - 2 Elites + 3 Melee + 3 Ranged", delayBeforeWave = 1.2f };
                    AddWaveEntries(w5, elite, 2, 0f, 0.35f, 5.8f, 0.9f, true);
                    AddWaveEntries(w5, meleeCreep, 3, 0.3f, 0.24f, 4.5f, 0.95f, true);
                    AddWaveEntries(w5, rangedCreep, 3, 0.55f, 0.28f, 6.6f, 0.8f, true);
                    lvl.waves.Add(w5);

                    WaveData w6 = new WaveData { waveName = "Wave 6 - 3 Elites + 5 Melee + 5 Ranged", delayBeforeWave = 1.3f };
                    AddWaveEntries(w6, elite, 3, 0f, 0.35f, 5.6f, 1.0f, true);
                    AddWaveEntries(w6, meleeCreep, 5, 0.35f, 0.22f, 4.4f, 0.9f, true);
                    AddWaveEntries(w6, rangedCreep, 5, 0.65f, 0.26f, 6.6f, 0.8f, true);
                    lvl.waves.Add(w6);
                }
                else
                {
                    // Stage 4: Wave 1 (2 Melee + 1 Ranged), Wave 2 (1 Elite 35 HP + 2 Melee), Wave 3 (World Boss 150 HP + BossMinionSpawner)
                    WaveData w1 = new WaveData { waveName = "Wave 1 - 2 Melee + 1 Ranged", delayBeforeWave = 0.6f };
                    AddWaveEntries(w1, meleeCreep, 2, 0f, 0.28f, 4.8f, 1.0f, true);
                    AddWaveEntries(w1, rangedCreep, 1, 0.35f, 0f, 6.8f, 0f, false);
                    lvl.waves.Add(w1);

                    WaveData w2 = new WaveData { waveName = "Wave 2 - Vanguard Elite (35 HP) + 2 Melee", delayBeforeWave = 1.0f };
                    AddWaveEntries(w2, elite, 1, 0f, 0f, 6.0f, 0f, false, 35f);
                    AddWaveEntries(w2, meleeCreep, 2, 0.25f, 0.25f, 4.6f, 1.0f, true);
                    lvl.waves.Add(w2);
                }

                EditorUtility.SetDirty(lvl);
                list.Add(lvl);
            }
            return list;
        }

        private static EnemyDataSO CreateEnemyDataSO(
            string assetName, string displayName, WorldType world, bool isElite, bool isBoss,
            float hp, float speed, float dmg, float heavyDmg, float range, float cd, bool armor, float prefRange, float fleeDist)
        {
            EnemyDataSO so = CreateOrLoadSO<EnemyDataSO>($"{DATA_ROOT}/Enemies/{assetName}.asset");
            so.enemyName = displayName;
            so.worldTheme = world;
            so.isElite = isElite;
            so.isBoss = isBoss;
            so.maxHealth = hp;
            so.moveSpeed = speed;
            so.attackDamage = dmg;
            so.heavyAttackDamage = heavyDmg;
            so.attackRange = range;
            so.attackCooldown = cd;
            so.telegraphDuration = 0.5f;
            so.hasSuperArmor = armor;
            so.staggerDuration = isElite || isBoss ? 0.5f : 0.35f;
            so.preferredRange = prefRange > 0f ? prefRange : 6.5f;
            so.fleeDistance = fleeDist > 0f ? fleeDist : 2.6f;
            so.projectileSpeed = 11.5f;
            EditorUtility.SetDirty(so);
            return so;
        }

        private static GameObject CreateWorldCreepPrefab<T>(
            string relName, Sprite sprite, RuntimeAnimatorController animCtrl, EnemyDataSO data,
            Vector2 colliderSize, Vector3 scale, LayerMask playerLayer,
            System.Action<T, SerializedObject> extraWire) where T : EnemyBase
        {
            string path = $"{PREFABS_ROOT}/Enemies/{relName}.prefab";
            GameObject go = new GameObject(System.IO.Path.GetFileName(relName));
            go.layer = 8;
            go.transform.localScale = scale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 4;

            Animator anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = animCtrl;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = colliderSize;
            col.offset = new Vector2(0f, colliderSize.y * 0.5f);

            GameObject hitObj = new GameObject("AttackHitbox");
            hitObj.layer = 0;
            hitObj.transform.SetParent(go.transform, false);
            hitObj.transform.localPosition = new Vector3(0.75f, colliderSize.y * 0.5f, 0f);
            BoxCollider2D hbCol = hitObj.AddComponent<BoxCollider2D>();
            hbCol.isTrigger = true;
            hbCol.size = new Vector2(1.25f, 1.3f);
            Hitbox hb = hitObj.AddComponent<Hitbox>();
            SerializedObject hbSO = new SerializedObject(hb);
            hbSO.FindProperty("targetLayer").intValue = playerLayer.value;
            hbSO.ApplyModifiedProperties();

            T comp = go.AddComponent<T>();
            SerializedObject so = new SerializedObject(comp);
            so.FindProperty("enemyData").objectReferenceValue = data;
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            so.FindProperty("animator").objectReferenceValue = anim;
            so.FindProperty("meleeHitbox").objectReferenceValue = hb;
            so.FindProperty("playerLayer").intValue = playerLayer.value;
            extraWire?.Invoke(comp, so);
            so.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateWarHammerBossPrefab(
            RuntimeAnimatorController animCtrl, EnemyDataSO data,
            Sprite bossSprite, Sprite crystalSprite, Sprite cartSprite,
            GameObject spikesPrefab, GameObject cartProjPrefab, LayerMask playerLayer)
        {
            string path = $"{PREFABS_ROOT}/Enemies/World1_AoT/W1_Boss_WarHammer.prefab";
            GameObject go = new GameObject("W1_Boss_WarHammer");
            go.layer = 8;
            go.transform.localScale = new Vector3(1.35f, 1.35f, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = bossSprite;
            sr.sortingOrder = 5;

            Animator anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = animCtrl;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.25f, 2.2f);
            col.offset = new Vector2(0f, 1.1f);

            GameObject hitObj = new GameObject("HammerHitbox");
            hitObj.layer = 0;
            hitObj.transform.SetParent(go.transform, false);
            hitObj.transform.localPosition = new Vector3(1.1f, 1.0f, 0f);
            BoxCollider2D hbCol = hitObj.AddComponent<BoxCollider2D>();
            hbCol.isTrigger = true;
            hbCol.size = new Vector2(1.8f, 1.8f);
            Hitbox hb = hitObj.AddComponent<Hitbox>();
            SerializedObject hbSO = new SerializedObject(hb);
            hbSO.FindProperty("targetLayer").intValue = playerLayer.value;
            hbSO.ApplyModifiedProperties();

            LineRenderer tetherLine = go.AddComponent<LineRenderer>();
            tetherLine.material = new Material(Shader.Find("Sprites/Default"));
            tetherLine.startColor = Color.white;
            tetherLine.endColor = Color.cyan;
            tetherLine.sortingOrder = 3;

            // Destructible Tether Cable Hitbox
            GameObject cableObj = new GameObject("TetherCableHitbox");
            cableObj.layer = 8;
            cableObj.transform.SetParent(go.transform, false);
            cableObj.transform.localPosition = new Vector3(2.2f, 0.7f, 0f);
            BoxCollider2D cableCol = cableObj.AddComponent<BoxCollider2D>();
            cableCol.isTrigger = true;
            cableCol.size = new Vector2(1.8f, 0.7f);
            TetherCableHitbox cableComp = cableObj.AddComponent<TetherCableHitbox>();
            cableComp.SetCableHealth(20f);

            // Underground Crystal Core
            GameObject coreObj = new GameObject("CrystalCore");
            coreObj.layer = 8;
            coreObj.transform.SetParent(go.transform, false);
            coreObj.transform.localPosition = new Vector3(4.5f, 0f, 0f);
            SpriteRenderer coreSr = coreObj.AddComponent<SpriteRenderer>();
            coreSr.sprite = crystalSprite;
            coreSr.sortingOrder = 5;
            BoxCollider2D coreCol = coreObj.AddComponent<BoxCollider2D>();
            coreCol.isTrigger = true;
            coreCol.size = new Vector2(1.2f, 1.4f);
            coreCol.offset = new Vector2(0f, 0.7f);
            CrystalCore coreComp = coreObj.AddComponent<CrystalCore>();
            coreComp.SetMaxHealth(50f);
            SerializedObject cSO = new SerializedObject(coreComp);
            cSO.FindProperty("spriteRenderer").objectReferenceValue = coreSr;
            cSO.ApplyModifiedProperties();

            // Cart Titan Artillery Partner Visual + FirePoint
            GameObject cartObj = new GameObject("CartTitanVisual");
            cartObj.transform.SetParent(go.transform, false);
            cartObj.transform.localPosition = new Vector3(4.8f, 0f, 0f);
            SpriteRenderer cartSr = cartObj.AddComponent<SpriteRenderer>();
            cartSr.sprite = cartSprite;
            cartSr.sortingOrder = 4;
            GameObject cartFire = new GameObject("CartTurretFirePoint");
            cartFire.transform.SetParent(cartObj.transform, false);
            cartFire.transform.localPosition = new Vector3(-0.9f, 0.85f, 0f);

            TetherBossController boss = go.AddComponent<TetherBossController>();
            SerializedObject bSO = new SerializedObject(boss);
            bSO.FindProperty("bossData").objectReferenceValue = data;
            bSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            bSO.FindProperty("animator").objectReferenceValue = anim;
            bSO.FindProperty("meleeHitbox").objectReferenceValue = hb;
            bSO.FindProperty("playerLayer").intValue = playerLayer.value;
            bSO.FindProperty("crystalCore").objectReferenceValue = coreComp;
            bSO.FindProperty("tetherCable").objectReferenceValue = cableComp;
            bSO.FindProperty("cartTitanVisual").objectReferenceValue = cartObj.transform;
            bSO.FindProperty("tetherLine").objectReferenceValue = tetherLine;
            bSO.FindProperty("groundSpikesPrefab").objectReferenceValue = spikesPrefab;
            bSO.FindProperty("cartTurretProjectilePrefab").objectReferenceValue = cartProjPrefab;
            bSO.FindProperty("cartTurretFirePoint").objectReferenceValue = cartFire.transform;
            bSO.FindProperty("cartCannonDamage").floatValue = 8f;
            bSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateBrolyBossPrefab(
            RuntimeAnimatorController animCtrl, EnemyDataSO data,
            Sprite brolySprite, Sprite shieldSprite, LayerMask playerLayer)
        {
            string path = $"{PREFABS_ROOT}/Enemies/World2_DB/W2_Boss_Broly.prefab";
            GameObject go = new GameObject("W2_Boss_Broly");
            go.layer = 8;
            go.transform.localScale = new Vector3(1.45f, 1.45f, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = brolySprite;
            sr.sortingOrder = 5;

            Animator anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = animCtrl;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.25f, 2.2f);
            col.offset = new Vector2(0f, 1.1f);

            GameObject hitObj = new GameObject("BrolyHitbox");
            hitObj.layer = 0;
            hitObj.transform.SetParent(go.transform, false);
            hitObj.transform.localPosition = new Vector3(1.0f, 1.0f, 0f);
            BoxCollider2D hbCol = hitObj.AddComponent<BoxCollider2D>();
            hbCol.isTrigger = true;
            hbCol.size = new Vector2(1.75f, 1.9f);
            Hitbox hb = hitObj.AddComponent<Hitbox>();
            SerializedObject hbSO = new SerializedObject(hb);
            hbSO.FindProperty("targetLayer").intValue = playerLayer.value;
            hbSO.ApplyModifiedProperties();

            GameObject shieldObj = new GameObject("GreenKiShield");
            shieldObj.transform.SetParent(go.transform, false);
            shieldObj.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            shieldObj.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            SpriteRenderer shieldSr = shieldObj.AddComponent<SpriteRenderer>();
            shieldSr.sprite = shieldSprite;
            shieldSr.color = new Color(0.25f, 1f, 0.45f, 0.52f);
            shieldSr.sortingOrder = 6;
            shieldSr.enabled = false;

            BrolyBossController broly = go.AddComponent<BrolyBossController>();
            SerializedObject bSO = new SerializedObject(broly);
            bSO.FindProperty("bossData").objectReferenceValue = data;
            bSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            bSO.FindProperty("animator").objectReferenceValue = anim;
            bSO.FindProperty("meleeHitbox").objectReferenceValue = hb;
            bSO.FindProperty("playerLayer").intValue = playerLayer.value;
            bSO.FindProperty("kiShieldVisual").objectReferenceValue = shieldSr;
            bSO.FindProperty("lariatDamage").floatValue = 18f;
            bSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateMechaBossPrefab(
            RuntimeAnimatorController animCtrl, EnemyDataSO data,
            Sprite bodySprite, Sprite leftArmSprite, Sprite rightArmSprite, Sprite coreSprite,
            GameObject laserPrefab, LayerMask playerLayer)
        {
            string path = $"{PREFABS_ROOT}/Enemies/World3_Diend/W3_Boss_MechaColossus.prefab";
            GameObject go = new GameObject("W3_Boss_MechaColossus");
            go.layer = 8;
            go.transform.localScale = new Vector3(4.5f, 4.5f, 1.0f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = bodySprite;
            sr.sortingOrder = 4;

            Animator anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = animCtrl;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.85f, 1.75f);
            col.offset = new Vector2(0f, 0.88f);

            GameObject hitObj = new GameObject("MechaSmashHitbox");
            hitObj.layer = 0;
            hitObj.transform.SetParent(go.transform, false);
            hitObj.transform.localPosition = new Vector3(-0.65f, 0.72f, 0f);
            BoxCollider2D hbCol = hitObj.AddComponent<BoxCollider2D>();
            hbCol.isTrigger = true;
            hbCol.size = new Vector2(0.95f, 1.1f);
            Hitbox hb = hitObj.AddComponent<Hitbox>();
            SerializedObject hbSO = new SerializedObject(hb);
            hbSO.FindProperty("targetLayer").intValue = playerLayer.value;
            hbSO.ApplyModifiedProperties();

            // Child Body (Torso anchor at chest center, trực tiếp làm parent cho LeftArm, RightArm & ChestCore)
            GameObject bodyNode = new GameObject("Body");
            bodyNode.transform.SetParent(go.transform, false);
            bodyNode.transform.localPosition = new Vector3(0f, 1.21f, 0f);

            BossPartHitbox CreatePart(string partName, Sprite partSp, Vector3 localPos, Vector2 size, Vector2 colOffset, int sortOrder, BossPartRole role)
            {
                GameObject pObj = new GameObject(partName);
                pObj.layer = 8;
                pObj.transform.SetParent(bodyNode.transform, false);
                pObj.transform.localPosition = localPos;
                SpriteRenderer pSr = pObj.AddComponent<SpriteRenderer>();
                pSr.sprite = partSp;
                pSr.sortingOrder = sortOrder;
                BoxCollider2D pCol = pObj.AddComponent<BoxCollider2D>();
                pCol.isTrigger = true;
                pCol.size = size;
                pCol.offset = colOffset;
                BossPartHitbox pComp = pObj.AddComponent<BossPartHitbox>();
                SerializedObject pSO = new SerializedObject(pComp);
                pSO.FindProperty("partRole").enumValueIndex = (int)role;
                pSO.FindProperty("partRenderer").objectReferenceValue = pSr;
                pSO.ApplyModifiedProperties();
                return pComp;
            }

            BossPartHitbox leftArm = CreatePart("LeftArm", leftArmSprite, new Vector3(-0.35f, 0.18f, 0f), new Vector2(0.55f, 0.85f), new Vector2(-0.18f, -0.30f), 5, BossPartRole.LeftArm);
            BossPartHitbox rightArm = CreatePart("RightArm", rightArmSprite, new Vector3(0.35f, 0.18f, 0f), new Vector2(0.55f, 0.85f), new Vector2(0.18f, -0.30f), 5, BossPartRole.RightArm);
            BossPartHitbox chestCore = CreatePart("ChestCore", coreSprite, new Vector3(-0.02f, 0.14f, 0f), new Vector2(0.45f, 0.55f), Vector2.zero, 6, BossPartRole.ChestCore);

            MechaBossController mecha = go.AddComponent<MechaBossController>();
            SerializedObject mSO = new SerializedObject(mecha);
            mSO.FindProperty("bossData").objectReferenceValue = data;
            mSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            mSO.FindProperty("animator").objectReferenceValue = anim;
            mSO.FindProperty("meleeHitbox").objectReferenceValue = hb;
            mSO.FindProperty("playerLayer").intValue = playerLayer.value;
            mSO.FindProperty("leftArm").objectReferenceValue = leftArm;
            mSO.FindProperty("rightArm").objectReferenceValue = rightArm;
            mSO.FindProperty("chestCore").objectReferenceValue = chestCore;
            mSO.FindProperty("armLaserProjectilePrefab").objectReferenceValue = laserPrefab;
            mSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
        #endregion

        #region Prefab Helper Creators
        private static GameObject CreateCharacterPrefab<T>(
            string name, Sprite sprite, Sprite auraSprite, Color trailColor, RuntimeAnimatorController animatorController,
            CharacterStatsSO statsSO, LayerMask groundLayer, LayerMask enemyLayer,
            System.Action<T, SerializedObject> customBinding, bool useCyanBlockShield = true) where T : BaseCharacter
        {
            string path = $"{PREFABS_ROOT}/Characters/{name}.prefab";
            GameObject go = new GameObject(name);
            go.tag = "Player";
            go.layer = 7; // Player layer

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = 5;

            Animator anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = animatorController;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 4.0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.freezeRotation = true;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.75f, 1.6f);
            col.offset = new Vector2(0f, 0.8f);

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.startWidth = 0.8f;
            trail.endWidth = 0.1f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = trailColor;
            trail.endColor = Color.clear;
            trail.emitting = false;

            go.AddComponent<InputHandler>();
            go.AddComponent<RageManager>();
            go.AddComponent<PlayerItemController>();

            // Con: GroundCheck (bám ngay dưới chân theo Bottom Center pivot)
            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(go.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            // Con: ProjectileSpawnPoint
            GameObject projSpawn = new GameObject("ProjectileSpawnPoint");
            projSpawn.transform.SetParent(go.transform, false);
            projSpawn.transform.localPosition = new Vector3(0.7f, 0.85f, 0f);

            // Con: MeleeHitbox
            GameObject hitboxObj = new GameObject("MeleeHitbox");
            hitboxObj.layer = 0;
            hitboxObj.transform.SetParent(go.transform, false);
            hitboxObj.transform.localPosition = new Vector3(0.7f, 0.8f, 0f);
            BoxCollider2D hitCol = hitboxObj.AddComponent<BoxCollider2D>();
            hitCol.isTrigger = true;
            hitCol.size = new Vector2(1.25f, 1.3f);
            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();

            SerializedObject hbSO = new SerializedObject(hitbox);
            hbSO.FindProperty("targetLayer").intValue = enemyLayer.value;
            hbSO.ApplyModifiedProperties();

            // Con: TransformAura
            GameObject auraObj = new GameObject("TransformAura");
            auraObj.transform.SetParent(go.transform, false);
            auraObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            SpriteRenderer auraSr = auraObj.AddComponent<SpriteRenderer>();
            auraSr.sprite = auraSprite;
            auraSr.color = new Color(1f, 0.92f, 0.2f, 0.3f);
            auraSr.sortingOrder = 4;
            auraObj.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            auraObj.SetActive(false);

            SpriteRenderer blockShieldSr = null;
            if (useCyanBlockShield)
            {
                GameObject shieldObj = new GameObject("BlockShieldVisual");
                shieldObj.transform.SetParent(go.transform, false);
                shieldObj.transform.localPosition = new Vector3(0.32f, 0.9f, 0f);
                shieldObj.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
                blockShieldSr = shieldObj.AddComponent<SpriteRenderer>();
                blockShieldSr.sprite = SpriteHelper.LoadSprite("Assets/Sprites/Combat/FX_BlockShield_Cyan.png", auraSprite);
                blockShieldSr.color = new Color(0.75f, 0.95f, 1f, 0.85f);
                blockShieldSr.sortingOrder = 8;
                shieldObj.SetActive(false);
            }

            T controller = go.AddComponent<T>();
            SerializedObject ctrlSO = new SerializedObject(controller);
            ctrlSO.FindProperty("stats").objectReferenceValue = statsSO;
            ctrlSO.FindProperty("groundCheckPoint").objectReferenceValue = groundCheck.transform;
            ctrlSO.FindProperty("groundCheckRadius").floatValue = 0.25f;
            ctrlSO.FindProperty("groundLayer").intValue = groundLayer.value;
            ctrlSO.FindProperty("meleeHitbox").objectReferenceValue = hitbox;
            ctrlSO.FindProperty("projectileSpawnPoint").objectReferenceValue = projSpawn.transform;
            ctrlSO.FindProperty("enemyLayerMask").intValue = enemyLayer.value;
            ctrlSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            ctrlSO.FindProperty("animator").objectReferenceValue = anim;
            ctrlSO.FindProperty("dashTrail").objectReferenceValue = trail;
            ctrlSO.FindProperty("transformAuraEffect").objectReferenceValue = auraObj;
            ctrlSO.FindProperty("blockShieldVisual").objectReferenceValue = blockShieldSr;
            ctrlSO.FindProperty("blockDuration").floatValue = 0.75f;
            ctrlSO.FindProperty("blockCooldown").floatValue = 0f;
            ctrlSO.FindProperty("mapMaxY").floatValue = 4.8f;

            customBinding?.Invoke(controller, ctrlSO);
            ctrlSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateEnemyPrefab<T>(
            string name, Sprite sprite, Sprite telegraphSprite, Color color, Vector3 scale, Vector2 colliderSize,
            EnemyStatsSO statsSO, LayerMask playerLayer, int layerIndex) where T : BaseEnemy
        {
            string path = $"{PREFABS_ROOT}/Enemies/{name}.prefab";
            GameObject go = new GameObject(name);
            go.layer = layerIndex;
            go.transform.localScale = scale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 4;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = colliderSize;

            // Telegraph Indicator
            GameObject telegraph = CreateTelegraphIndicator(go.transform, telegraphSprite, new Vector3(0, 1.15f, 0), new Vector3(0.35f, 0.35f, 1f));

            // Hitbox cận chiến
            GameObject hitObj = new GameObject("AttackHitbox");
            hitObj.layer = 0;
            hitObj.transform.SetParent(go.transform, false);
            hitObj.transform.localPosition = new Vector3(0.6f, 0, 0);
            BoxCollider2D hbCol = hitObj.AddComponent<BoxCollider2D>();
            hbCol.isTrigger = true;
            hbCol.size = new Vector2(1.0f, 1.1f);
            Hitbox hb = hitObj.AddComponent<Hitbox>();

            SerializedObject hbSO = new SerializedObject(hb);
            hbSO.FindProperty("targetLayer").intValue = playerLayer.value;
            hbSO.ApplyModifiedProperties();

            T enemy = go.AddComponent<T>();
            SerializedObject enemySO = new SerializedObject(enemy);
            enemySO.FindProperty("stats").objectReferenceValue = statsSO;
            enemySO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            enemySO.FindProperty("attackHitbox").objectReferenceValue = hb;
            enemySO.FindProperty("telegraphIndicator").objectReferenceValue = telegraph;
            enemySO.FindProperty("playerLayer").intValue = playerLayer.value;
            enemySO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateRangerEnemyPrefab(string name, Sprite sprite, Sprite telegraphSprite, Color color, EnemyStatsSO statsSO, GameObject bulletPrefab, LayerMask playerLayer, int layerIndex)
        {
            string path = $"{PREFABS_ROOT}/Enemies/{name}.prefab";
            GameObject go = new GameObject(name);
            go.layer = layerIndex;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 4;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.75f, 1.5f);

            GameObject telegraph = CreateTelegraphIndicator(go.transform, telegraphSprite, new Vector3(0, 1.15f, 0), new Vector3(0.35f, 0.35f, 1f));

            GameObject firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(go.transform, false);
            firePoint.transform.localPosition = new Vector3(0.6f, 0.1f, 0);

            RangerEnemy ranger = go.AddComponent<RangerEnemy>();
            SerializedObject rSO = new SerializedObject(ranger);
            rSO.FindProperty("stats").objectReferenceValue = statsSO;
            rSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            rSO.FindProperty("telegraphIndicator").objectReferenceValue = telegraph;
            rSO.FindProperty("playerLayer").intValue = playerLayer.value;
            rSO.FindProperty("bulletPrefab").objectReferenceValue = bulletPrefab;
            rSO.FindProperty("firePoint").objectReferenceValue = firePoint.transform;
            rSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateKamikazeEnemyPrefab(string name, Sprite sprite, Sprite telegraphSprite, Color color, EnemyStatsSO statsSO, GameObject explosionPrefab, LayerMask playerLayer, int layerIndex)
        {
            string path = $"{PREFABS_ROOT}/Enemies/{name}.prefab";
            GameObject go = new GameObject(name);
            go.layer = layerIndex;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 4;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.65f;

            GameObject telegraph = CreateTelegraphIndicator(go.transform, telegraphSprite, new Vector3(0, 1.1f, 0), new Vector3(0.35f, 0.35f, 1f));

            KamikazeEnemy kami = go.AddComponent<KamikazeEnemy>();
            SerializedObject kSO = new SerializedObject(kami);
            kSO.FindProperty("stats").objectReferenceValue = statsSO;
            kSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            kSO.FindProperty("telegraphIndicator").objectReferenceValue = telegraph;
            kSO.FindProperty("playerLayer").intValue = playerLayer.value;
            kSO.FindProperty("explosionPrefab").objectReferenceValue = explosionPrefab;
            kSO.FindProperty("triggerDistance").floatValue = 2.0f;
            kSO.FindProperty("explosionRadius").floatValue = 4.0f;
            kSO.FindProperty("countdownTime").floatValue = 1.5f;
            kSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateEliteEnemyPrefab(string name, Sprite sprite, Sprite telegraphSprite, Color color, EnemyStatsSO statsSO, GameObject shockwavePrefab, LayerMask playerLayer, int layerIndex)
        {
            string path = $"{PREFABS_ROOT}/Enemies/{name}.prefab";
            GameObject go = new GameObject(name);
            go.layer = layerIndex;
            go.transform.localScale = new Vector3(1.4f, 1.4f, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 4;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.85f, 1.6f);

            GameObject telegraph = CreateTelegraphIndicator(go.transform, telegraphSprite, new Vector3(0, 1.2f, 0), new Vector3(0.35f, 0.35f, 1f));

            GameObject slamPoint = new GameObject("SlamImpactPoint");
            slamPoint.transform.SetParent(go.transform, false);
            slamPoint.transform.localPosition = new Vector3(0.8f, -0.6f, 0);

            EliteBruiserEnemy elite = go.AddComponent<EliteBruiserEnemy>();
            SerializedObject eSO = new SerializedObject(elite);
            eSO.FindProperty("stats").objectReferenceValue = statsSO;
            eSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            eSO.FindProperty("telegraphIndicator").objectReferenceValue = telegraph;
            eSO.FindProperty("playerLayer").intValue = playerLayer.value;
            eSO.FindProperty("groundShockwavePrefab").objectReferenceValue = shockwavePrefab;
            eSO.FindProperty("slamImpactPoint").objectReferenceValue = slamPoint.transform;
            eSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateBossPrefab(
            string name, Sprite sprite, Sprite telegraphSprite, Color color, EnemyStatsSO statsSO,
            GameObject laserPrefab, GameObject kamikazePrefab, GameObject carpetBombPrefab, GameObject aoeWarningPrefab,
            LayerMask playerLayer, int layerIndex)
        {
            string path = $"{PREFABS_ROOT}/Enemies/{name}.prefab";
            GameObject go = new GameObject(name);
            go.layer = layerIndex;
            go.transform.localScale = new Vector3(1.35f, 1.35f, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 5;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.95f, 1.8f);

            GameObject telegraph = CreateTelegraphIndicator(go.transform, telegraphSprite, new Vector3(0, 1.4f, 0), new Vector3(0.45f, 0.45f, 1f));

            GameObject minionSpawn = new GameObject("MinionSpawnPoint");
            minionSpawn.transform.SetParent(go.transform, false);
            minionSpawn.transform.localPosition = new Vector3(-2f, 1f, 0);

            GameObject aura = new GameObject("Phase2Aura");
            aura.transform.SetParent(go.transform, false);
            SpriteRenderer auraSr = aura.AddComponent<SpriteRenderer>();
            auraSr.sprite = telegraphSprite;
            auraSr.color = new Color(1f, 0f, 0f, 0.35f);
            aura.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
            aura.SetActive(false);

            // Hitbox cận chiến
            GameObject hbObj = new GameObject("AttackHitbox");
            hbObj.layer = 0;
            hbObj.transform.SetParent(go.transform, false);
            hbObj.transform.localPosition = new Vector3(1.0f, 0, 0);
            BoxCollider2D hbCol = hbObj.AddComponent<BoxCollider2D>();
            hbCol.isTrigger = true;
            hbCol.size = new Vector2(1.5f, 1.6f);
            Hitbox hb = hbObj.AddComponent<Hitbox>();
            SerializedObject hbSO = new SerializedObject(hb);
            hbSO.FindProperty("targetLayer").intValue = playerLayer.value;
            hbSO.ApplyModifiedProperties();

            BossController boss = go.AddComponent<BossController>();
            SerializedObject bSO = new SerializedObject(boss);
            bSO.FindProperty("stats").objectReferenceValue = statsSO;
            bSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            bSO.FindProperty("attackHitbox").objectReferenceValue = hb;
            bSO.FindProperty("telegraphIndicator").objectReferenceValue = telegraph;
            bSO.FindProperty("playerLayer").intValue = playerLayer.value;
            bSO.FindProperty("groundLaserPrefab").objectReferenceValue = laserPrefab;
            bSO.FindProperty("kamikazeMinionPrefab").objectReferenceValue = kamikazePrefab;
            bSO.FindProperty("minionSpawnPoint").objectReferenceValue = minionSpawn.transform;
            bSO.FindProperty("carpetBombExplosionPrefab").objectReferenceValue = carpetBombPrefab;
            bSO.FindProperty("aoeWarningIndicatorPrefab").objectReferenceValue = aoeWarningPrefab;
            bSO.FindProperty("phase2AuraVfx").objectReferenceValue = aura;
            bSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateTelegraphIndicator(Transform parent, Sprite circleSprite, Vector3 localPos, Vector3 localScale)
        {
            GameObject telegraph = new GameObject("TelegraphIndicator");
            telegraph.transform.SetParent(parent, false);
            telegraph.transform.localPosition = localPos;
            SpriteRenderer telSr = telegraph.AddComponent<SpriteRenderer>();
            telSr.sprite = circleSprite;
            telSr.color = Color.red;
            telSr.sortingOrder = 10;
            telegraph.transform.localScale = localScale;
            telegraph.SetActive(false);
            return telegraph;
        }

        private static GameObject CreateProjectilePrefab(string name, Sprite sprite, Color color, float speed, float lifetime, float scale, bool piercing)
        {
            string path = $"{PREFABS_ROOT}/Combat/{name}.prefab";
            GameObject go = new GameObject(name);
            go.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 6;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.45f;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;

            Projectile p = go.AddComponent<Projectile>();
            SerializedObject pSO = new SerializedObject(p);
            pSO.FindProperty("speed").floatValue = speed;
            pSO.FindProperty("lifeTime").floatValue = lifetime;
            pSO.FindProperty("isPiercing").boolValue = piercing;
            pSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateExplosionPrefab(string name, Sprite sprite, Color color, float radius, float visualScale)
        {
            string path = $"{PREFABS_ROOT}/Combat/{name}.prefab";
            GameObject go = new GameObject(name);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 7;
            go.transform.localScale = Vector3.one * visualScale;

            Explosion exp = go.AddComponent<Explosion>();
            SerializedObject expSO = new SerializedObject(exp);
            expSO.FindProperty("radius").floatValue = radius;
            expSO.FindProperty("duration").floatValue = 0.35f;
            expSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateBeamPrefab(string name, Color color, float length, float width)
        {
            string path = $"{PREFABS_ROOT}/Combat/{name}.prefab";
            GameObject go = new GameObject(name);

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(length, width);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;

            BeamAttack beam = go.AddComponent<BeamAttack>();
            SerializedObject bSO = new SerializedObject(beam);
            bSO.FindProperty("beamLength").floatValue = length;
            bSO.FindProperty("beamWidth").floatValue = width;
            bSO.FindProperty("lineRenderer").objectReferenceValue = lr;
            bSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateShockwavePrefab(string name, Sprite sprite, Color color, float speed)
        {
            string path = $"{PREFABS_ROOT}/Combat/{name}.prefab";
            GameObject go = new GameObject(name);
            go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 6;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.9f, 1.0f);

            go.AddComponent<Rigidbody2D>();

            Shockwave sw = go.AddComponent<Shockwave>();
            SerializedObject swSO = new SerializedObject(sw);
            swSO.FindProperty("speed").floatValue = speed;
            swSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateTitanStompPrefab(string name, Sprite sprite)
        {
            string path = $"{PREFABS_ROOT}/Combat/{name}.prefab";
            GameObject go = new GameObject(name);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = 7;
            go.transform.localScale = new Vector3(3.5f, 4.0f, 1f);

            TitanStomp stomp = go.AddComponent<TitanStomp>();
            SerializedObject sSO = new SerializedObject(stomp);
            sSO.FindProperty("stompAreaSize").vector2Value = new Vector2(16f, 6f);
            sSO.FindProperty("delayBeforeHit").floatValue = 0.4f;
            sSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreatePhantomSlashPrefab(string name, Sprite sprite, Color color)
        {
            string path = $"{PREFABS_ROOT}/Combat/{name}.prefab";
            GameObject go = new GameObject(name);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 7;
            go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateSimpleVfxPrefab(string name, Sprite sprite, Color color, Vector3 scale)
        {
            string path = $"{PREFABS_ROOT}/Combat/{name}.prefab";
            GameObject go = new GameObject(name);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 7;
            go.transform.localScale = scale;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateLordKivaPrefab(
            string name, Sprite kivaSprite, RuntimeAnimatorController animatorController,
            GameObject wingFreezeVfxPrefab, LayerMask enemyLayer)
        {
            string path = $"{PREFABS_ROOT}/Combat/{name}.prefab";
            GameObject go = new GameObject(name);
            go.transform.localScale = new Vector3(1.35f, 1.35f, 1.0f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = kivaSprite;
            sr.color = Color.white;
            sr.sortingOrder = 6;

            Animator anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = animatorController;

            go.AddComponent<Rigidbody2D>();

            GameObject claw = new GameObject("ClawHitbox");
            claw.layer = 0;
            claw.transform.SetParent(go.transform, false);
            claw.transform.localPosition = new Vector3(0.8f, 0.85f, 0f);
            BoxCollider2D cCol = claw.AddComponent<BoxCollider2D>();
            cCol.isTrigger = true;
            cCol.size = new Vector2(1.2f, 1.2f);
            Hitbox hb = claw.AddComponent<Hitbox>();
            SerializedObject hbSO = new SerializedObject(hb);
            hbSO.FindProperty("targetLayer").intValue = enemyLayer.value;
            hbSO.ApplyModifiedProperties();

            LordKivaAI kiva = go.AddComponent<LordKivaAI>();
            SerializedObject kSO = new SerializedObject(kiva);
            kSO.FindProperty("clawHitbox").objectReferenceValue = hb;
            kSO.FindProperty("spriteRenderer").objectReferenceValue = sr;
            kSO.FindProperty("animator").objectReferenceValue = anim;
            kSO.FindProperty("wingFreezeVfxPrefab").objectReferenceValue = wingFreezeVfxPrefab;
            kSO.FindProperty("enemyLayer").intValue = enemyLayer.value;
            kSO.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateWarningIndicatorPrefab(string name, Sprite circleSprite, Color color)
        {
            string path = $"{PREFABS_ROOT}/Combat/{name}.prefab";
            GameObject go = new GameObject(name);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = color;
            sr.sortingOrder = 3;
            go.transform.localScale = new Vector3(1.8f, 1.8f, 1f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        #region Consumable Items & Campaign Hazards
        private static void CreateConsumableItemAssets(Sprite fallbackSprite)
        {
            const string uiItems = "Assets/Sprites/UI/Items";
            const string vfxItems = "Assets/Sprites/Combat/Items";

            CreateOrUpdateItemSO(
                "Assets/Resources/Items/SenzuBeanSO.asset",
                ConsumableItemType.SenzuBean,
                "Đậu Thần Senzu",
                "Hồi ngay 50 HP tức thì khi kích hoạt.",
                30,
                SpriteHelper.LoadSprite($"{uiItems}/Icon_SenzuBean.png", fallbackSprite),
                new[]
                {
                    SpriteHelper.LoadSprite($"{vfxItems}/VFX_SenzuHeal_0.png", fallbackSprite),
                    SpriteHelper.LoadSprite($"{vfxItems}/VFX_SenzuHeal_1.png", fallbackSprite),
                    SpriteHelper.LoadSprite($"{vfxItems}/VFX_SenzuHeal_2.png", fallbackSprite)
                },
                10f,
                healAmount: 50f
            );

            CreateOrUpdateItemSO(
                "Assets/Resources/Items/SpinalFluidSO.asset",
                ConsumableItemType.SpinalFluid,
                "Dịch Tủy Titan",
                "Tạo 40 Giáp Ảo trong 10s và phản 10 DMG khi bị đánh.",
                60,
                SpriteHelper.LoadSprite($"{uiItems}/Icon_SpinalFluid.png", fallbackSprite),
                new[]
                {
                    SpriteHelper.LoadSprite($"{vfxItems}/VFX_SpinalRibcage_0.png", fallbackSprite),
                    SpriteHelper.LoadSprite($"{vfxItems}/VFX_SpinalRibcage_1.png", fallbackSprite)
                },
                8f,
                buffDuration: 10f,
                shieldAmount: 40f,
                reflectDamage: 10f
            );

            CreateOrUpdateItemSO(
                "Assets/Resources/Items/UltraDivineWaterSO.asset",
                ConsumableItemType.UltraDivineWater,
                "Nước Thần Siêu Phàm",
                "Trong 8s: +35% Sát thương, +25% Tốc chạy và +5 Nộ/s.",
                50,
                SpriteHelper.LoadSprite($"{uiItems}/Icon_UltraDivineWater.png", fallbackSprite),
                new[]
                {
                    SpriteHelper.LoadSprite($"{vfxItems}/VFX_UltraWaterAura_0.png", fallbackSprite),
                    SpriteHelper.LoadSprite($"{vfxItems}/VFX_UltraWaterAura_1.png", fallbackSprite),
                    SpriteHelper.LoadSprite($"{vfxItems}/VFX_UltraWaterAura_2.png", fallbackSprite)
                },
                10f,
                buffDuration: 8f,
                damageMultiplier: 1.35f,
                speedMultiplier: 1.25f,
                ragePerSecond: 5f
            );
        }

        private static void CreateOrUpdateItemSO(
            string path, ConsumableItemType type, string title, string desc, int price,
            Sprite icon, Sprite[] vfxFrames, float vfxFps,
            float healAmount = 0f, float buffDuration = 0f, float shieldAmount = 0f,
            float reflectDamage = 0f, float damageMultiplier = 1f, float speedMultiplier = 1f, float ragePerSecond = 0f)
        {
            ConsumableItemSO so = CreateOrLoadSO<ConsumableItemSO>(path);
            so.itemType = type;
            so.itemName = title;
            so.description = desc;
            so.price = price;
            so.icon = icon;
            so.vfxFrames = vfxFrames;
            so.vfxFps = vfxFps;
            so.healAmount = healAmount;
            so.buffDuration = buffDuration;
            so.shieldAmount = shieldAmount;
            so.reflectDamage = reflectDamage;
            so.damageMultiplier = damageMultiplier;
            so.speedMultiplier = speedMultiplier;
            so.ragePerSecond = ragePerSecond;
            EditorUtility.SetDirty(so);
        }

        private static void CreateAllHazardPrefabs(Sprite squareSprite, Sprite circleSprite, GameObject explosionPrefab)
        {
            const string hazardSprites = "Assets/Sprites/Combat/Hazards";
            const string combatSprites = "Assets/Sprites/Combat";

            CreateOneWayPlatformHazardPrefab("Hazard_WoodenEaves", SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_WoodenEaves.png", squareSprite), new Vector2(2.6f, 0.35f), false);
            CreateOneWayPlatformHazardPrefab("Hazard_FloatingRock", SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_FloatingRock.png", squareSprite), new Vector2(2.2f, 0.38f), true);
            CreateOneWayPlatformHazardPrefab("Hazard_CyberPlatform", SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_CyberPlatform.png", squareSprite), new Vector2(2.5f, 0.35f), false);

            CreateSimpleHazardPrefab<GunpowderBarrelHazard>(
                "Hazard_GunpowderBarrel",
                SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_GunpowderBarrel.png", squareSprite),
                Color.white, 1, new Vector2(0.9f, 1.15f), Vector2.zero, true,
                so => so.FindProperty("explosionVfxPrefab").objectReferenceValue = explosionPrefab
            );

            CreateSimpleHazardPrefab<RollingCartHazard>(
                "Hazard_RollingCart",
                SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_AmmoCart.png", squareSprite),
                Color.white, 2, new Vector2(1.35f, 1.0f), Vector2.zero, true, null
            );

            CreateSimpleHazardPrefab<WallCannonInteractable>(
                "Hazard_WallCannon",
                SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_WallCannon.png", squareSprite),
                Color.white, 1, new Vector2(1.35f, 1.1f), Vector2.zero, true,
                so => so.FindProperty("cannonballSprite").objectReferenceValue = circleSprite
            );

            CreateSimpleHazardPrefab<KiGeyserHazard>(
                "Hazard_KiGeyser",
                SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_KiGeyser.png", squareSprite),
                Color.white, 1, new Vector2(1.4f, 4.0f), new Vector2(0f, 2.0f), true, null
            );

            CreateSimpleHazardPrefab<GravityZoneHazard>(
                "Hazard_GravityZone",
                SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_GravityZone.png", circleSprite),
                new Color(1f, 1f, 1f, 0.65f), -1, new Vector2(3.4f, 3.0f), Vector2.zero, true,
                so => so.FindProperty("isSlowSmokeOnly").boolValue = false
            );

            CreateSimpleHazardPrefab<GravityZoneHazard>(
                "Hazard_TitanSmoke",
                SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_GravityZone.png", circleSprite),
                new Color(0.55f, 0.45f, 0.38f, 0.55f), 3, new Vector2(3.4f, 2.8f), Vector2.zero, true,
                so => so.FindProperty("isSlowSmokeOnly").boolValue = true
            );

            CreateSimpleHazardPrefab<AncientStonePillar>(
                "Hazard_AncientStonePillar",
                SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_StonePillar.png", squareSprite),
                Color.white, 0, new Vector2(0.95f, 2.2f), Vector2.zero, true, null
            );

            CreateSimpleHazardPrefab<EmpTerminalInteractable>(
                "Hazard_EmpTerminal",
                SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_EmpTerminal.png", squareSprite),
                Color.white, 1, new Vector2(1.15f, 1.35f), Vector2.zero, true, null
            );

            // FallingBoulderSpawner (controller không collider)
            {
                string path = $"{PREFABS_ROOT}/Hazards/Hazard_FallingBoulderSpawner.prefab";
                GameObject go = new GameObject("Hazard_FallingBoulderSpawner");
                FallingBoulderSpawner spawner = go.AddComponent<FallingBoulderSpawner>();
                SerializedObject so = new SerializedObject(spawner);
                so.FindProperty("boulderSprite").objectReferenceValue = SpriteHelper.LoadSprite($"{combatSprites}/FX_RockImpact.png", circleSprite);
                so.FindProperty("warningSprite").objectReferenceValue = SpriteHelper.LoadSprite($"{combatSprites}/FX_WarningCircle.png", circleSprite);
                so.ApplyModifiedProperties();
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }

            // LowLaserTurretHazard
            {
                string path = $"{PREFABS_ROOT}/Hazards/Hazard_LowLaserTurret.prefab";
                GameObject go = new GameObject("Hazard_LowLaserTurret");
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_LaserTurret.png", squareSprite);
                sr.sortingOrder = 1;
                LowLaserTurretHazard turret = go.AddComponent<LowLaserTurretHazard>();
                SerializedObject so = new SerializedObject(turret);
                so.FindProperty("spriteRenderer").objectReferenceValue = sr;
                so.ApplyModifiedProperties();
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }

            // AuroraPortalPair (2 cổng A & B)
            {
                string path = $"{PREFABS_ROOT}/Hazards/Hazard_AuroraPortalPair.prefab";
                GameObject go = new GameObject("Hazard_AuroraPortalPair");
                Sprite portalSprite = SpriteHelper.LoadSprite($"{hazardSprites}/Hazard_AuroraPortal.png", circleSprite);

                GameObject pA = new GameObject("PortalA");
                pA.transform.SetParent(go.transform, false);
                pA.transform.localPosition = new Vector3(-4.5f, 0.85f, 0f);
                SpriteRenderer srA = pA.AddComponent<SpriteRenderer>();
                srA.sprite = portalSprite;
                srA.sortingOrder = 0;

                GameObject pB = new GameObject("PortalB");
                pB.transform.SetParent(go.transform, false);
                pB.transform.localPosition = new Vector3(4.5f, 0.85f, 0f);
                SpriteRenderer srB = pB.AddComponent<SpriteRenderer>();
                srB.sprite = portalSprite;
                srB.sortingOrder = 0;

                AuroraPortalPair pair = go.AddComponent<AuroraPortalPair>();
                SerializedObject so = new SerializedObject(pair);
                so.FindProperty("portalA").objectReferenceValue = pA.transform;
                so.FindProperty("portalB").objectReferenceValue = pB.transform;
                so.ApplyModifiedProperties();
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
        }

        private static GameObject CreateOneWayPlatformHazardPrefab(string prefabName, Sprite sprite, Vector2 colliderSize, bool isFragile)
        {
            string path = $"{PREFABS_ROOT}/Hazards/{prefabName}.prefab";
            GameObject go = new GameObject(prefabName);
            int groundLayerIndex = LayerMask.NameToLayer("Ground");
            go.layer = groundLayerIndex >= 0 ? groundLayerIndex : 6;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -2;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.usedByEffector = true;
            col.size = colliderSize;

            PlatformEffector2D effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;

            OneWayPlatformHazard plat = go.AddComponent<OneWayPlatformHazard>();
            SerializedObject so = new SerializedObject(plat);
            so.FindProperty("isFragile").boolValue = isFragile;
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            so.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateSimpleHazardPrefab<T>(
            string prefabName, Sprite sprite, Color tint, int sortingOrder,
            Vector2 colliderSize, Vector2 colliderOffset, bool isTrigger,
            System.Action<SerializedObject> bindExtra) where T : MonoBehaviour
        {
            string path = $"{PREFABS_ROOT}/Hazards/{prefabName}.prefab";
            GameObject go = new GameObject(prefabName);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = sortingOrder;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = isTrigger;
            col.size = colliderSize;
            col.offset = colliderOffset;

            T comp = go.AddComponent<T>();
            SerializedObject so = new SerializedObject(comp);
            SerializedProperty srProp = so.FindProperty("spriteRenderer");
            if (srProp != null) srProp.objectReferenceValue = sr;
            bindExtra?.Invoke(so);
            so.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
        #endregion

        private static T CreateOrLoadSO<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
        #endregion
    }
}
