using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StickmanBeatEmUp.Tests.E2E.PlayMode
{
    public class TV1_CombatAndBossPlayModeTests
    {
        private static void SetField(object target, string fieldName, object value)
        {
            Assert.IsNotNull(target, $"Target null when setting field '{fieldName}'.");
            Type cursor = target.GetType();
            while (cursor != null)
            {
                FieldInfo fi = cursor.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                {
                    fi.SetValue(target, value);
                    return;
                }
                cursor = cursor.BaseType;
            }

            Assert.Fail($"Field '{fieldName}' not found on type '{target.GetType().Name}'.");
        }

        private static object GetFieldRecursive(object target, string fieldName)
        {
            Assert.IsNotNull(target, $"Target null when reading field '{fieldName}'.");
            Type cursor = target.GetType();
            while (cursor != null)
            {
                FieldInfo fi = cursor.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null)
                {
                    return fi.GetValue(target);
                }
                cursor = cursor.BaseType;
            }

            Assert.Fail($"Field '{fieldName}' not found on type '{target.GetType().Name}'.");
            return null;
        }

        private static object CreateDamageData(float amount, string attackTypeName, bool breaksArmor, GameObject attacker)
        {
            Type dmgType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.DamageData");
            Type atkEnum = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.AttackType");
            object atkVal = Enum.Parse(atkEnum, attackTypeName);
            return Activator.CreateInstance(dmgType, new object[] { amount, Vector2.right * 2f, atkVal, breaksArmor, attacker });
        }

        private static ScriptableObject CreateEnemyData(float maxHp, float attackDmg, float heavyDmg, bool isBoss, bool hasSuperArmor)
        {
            Type dataType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Enemies.Stats.EnemyDataSO");
            ScriptableObject so = ScriptableObject.CreateInstance(dataType);
            SetField(so, "maxHealth", maxHp);
            SetField(so, "attackDamage", attackDmg);
            SetField(so, "heavyAttackDamage", heavyDmg);
            SetField(so, "isBoss", isBoss);
            SetField(so, "hasSuperArmor", hasSuperArmor);
            return so;
        }

        [UnityTest]
        [Category("TV1")]
        [Category("Combat")]
        public IEnumerator TV1_Hitbox_ActivateAndDeactivate_AppliesDamageOncePerSwing_AndResetsOnReactivate()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);

                int enemyLayer = LayerMask.NameToLayer("Enemy");
                if (enemyLayer < 0) enemyLayer = 8;

                Type meleeType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Enemies.MeleeCreep");
                Type hitboxType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Core.Hitbox");
                ScriptableObject enemySo = CreateEnemyData(100f, 5f, 10f, false, true);

                GameObject dummyObj = new GameObject("TV1_HitboxTestTarget");
                GameObject hitboxObj = new GameObject("TV1_TestHitbox");
                try
                {
                    dummyObj.layer = enemyLayer;
                    dummyObj.transform.position = new Vector3(4f, -2.5f, 0f);
                    Rigidbody2D dummyRb = dummyObj.AddComponent<Rigidbody2D>();
                    dummyRb.gravityScale = 0f;
                    BoxCollider2D dummyCol = dummyObj.AddComponent<BoxCollider2D>();
                    dummyCol.size = new Vector2(1.5f, 2f);
                    Component enemyComp = dummyObj.AddComponent(meleeType);
                    PlayModeTierSupport.InvokeMethod(enemyComp, "SetData", enemySo);

                    hitboxObj.transform.position = new Vector3(4f, -2.5f, 0f);
                    BoxCollider2D hbCol = hitboxObj.AddComponent<BoxCollider2D>();
                    hbCol.size = new Vector2(2f, 2f);
                    hbCol.isTrigger = true;
                    Component hitboxComp = hitboxObj.AddComponent(hitboxType);
                    SetField(hitboxComp, "targetLayer", (LayerMask)(1 << enemyLayer));

                    Physics2D.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                    yield return null;

                    float initialHp = (float)PlayModeTierSupport.GetInstanceProperty(enemyComp, "CurrentHealth");
                    Assert.AreEqual(100f, initialHp, 0.01f);

                    Physics2D.SyncTransforms();
                    object swing1 = CreateDamageData(20f, "NormalAttack_J", false, hitboxObj);
                    PlayModeTierSupport.InvokeMethod(hitboxComp, "Activate", swing1);
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();

                    // Phải chỉ trừ 1 lần 20 sát thương dù chồng lấp nhiều FixedUpdate
                    float hpAfterSwing1 = (float)PlayModeTierSupport.GetInstanceProperty(enemyComp, "CurrentHealth");
                    Assert.AreEqual(80f, hpAfterSwing1, 0.01f, "Hitbox must hit each target at most once per Activate.");

                    PlayModeTierSupport.InvokeMethod(hitboxComp, "Deactivate");
                    yield return new WaitForFixedUpdate();
                    Assert.AreEqual(80f, (float)PlayModeTierSupport.GetInstanceProperty(enemyComp, "CurrentHealth"), 0.01f);

                    // Kích hoạt lại đợt 2 phải đánh trúng tiếp
                    Physics2D.SyncTransforms();
                    object swing2 = CreateDamageData(15f, "Skill_U", false, hitboxObj);
                    PlayModeTierSupport.InvokeMethod(hitboxComp, "Activate", swing2);
                    yield return new WaitForFixedUpdate();
                    Assert.AreEqual(65f, (float)PlayModeTierSupport.GetInstanceProperty(enemyComp, "CurrentHealth"), 0.01f);
                }
                finally
                {
                    UnityEngine.Object.Destroy(hitboxObj);
                    UnityEngine.Object.Destroy(dummyObj);
                    UnityEngine.Object.Destroy(enemySo);
                }
            }
        }

        [UnityTest]
        [Category("TV1")]
        [Category("Boss")]
        public IEnumerator TV1_BossBase_BrolyMultiPhase_RageActivatesGreenShield_AndArmorBreakShattersTo2xStagger()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);

                Type brolyType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Enemies.BrolyBossController");
                ScriptableObject brolySo = CreateEnemyData(300f, 15f, 25f, true, true);
                GameObject brolyObj = new GameObject("TV1_TestBrolyBoss");

                try
                {
                    brolyObj.transform.position = new Vector3(12f, -2.5f, 0f);
                    Rigidbody2D rb = brolyObj.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0f;
                    brolyObj.AddComponent<BoxCollider2D>();
                    Component broly = brolyObj.AddComponent(brolyType);
                    SetField(broly, "nextSpecialTime", Time.time + 10f);
                    SetField(broly, "nextAttackTime", Time.time + 10f);
                    PlayModeTierSupport.InvokeMethod(broly, "SetData", brolySo);

                    yield return null;
                    yield return null;

                    // 4 đòn J (+25 rage/đòn = 100 rage) -> kích hoạt Green Ki Shield
                    object lightHit = CreateDamageData(10f, "NormalAttack_J", false, null);
                    for (int i = 0; i < 4; i++)
                    {
                        PlayModeTierSupport.InvokeMethod(broly, "TakeDamage", lightHit);
                    }

                    yield return null;
                    yield return null;

                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(broly, "IsShielded"),
                        "Broly must activate Green Ki Shield after reaching 100 rage.");

                    // Đòn thường không phá giáp khi đang có Green Ki Shield phải gây 0 sát thương
                    float hpBeforeBlocked = (float)PlayModeTierSupport.GetInstanceProperty(broly, "CurrentHealth");
                    PlayModeTierSupport.InvokeMethod(broly, "TakeDamage", lightHit);
                    Assert.AreEqual(hpBeforeBlocked, (float)PlayModeTierSupport.GetInstanceProperty(broly, "CurrentHealth"), 0.01f);

                    // Đòn phá giáp (Skill_I + breaksSuperArmor=true) vỡ khiên -> vào trạng thái Stagger x2 sát thương
                    object breakHit = CreateDamageData(20f, "Skill_I", true, null);
                    PlayModeTierSupport.InvokeMethod(broly, "TakeDamage", breakHit);
                    yield return null;

                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(broly, "IsShielded"));
                    Assert.IsTrue((bool)GetFieldRecursive(broly, "isVulnerableStagger"));
                    Assert.AreEqual(2f, (float)GetFieldRecursive(broly, "damageMultiplier"), 0.01f);

                    float hpBeforeStaggerHit = (float)PlayModeTierSupport.GetInstanceProperty(broly, "CurrentHealth");
                    PlayModeTierSupport.InvokeMethod(broly, "TakeDamage", CreateDamageData(10f, "NormalAttack_J", false, null));
                    float hpAfterStaggerHit = (float)PlayModeTierSupport.GetInstanceProperty(broly, "CurrentHealth");
                    Assert.AreEqual(20f, hpBeforeStaggerHit - hpAfterStaggerHit, 0.01f, "Vulnerable stagger must apply 2x damage.");
                }
                finally
                {
                    UnityEngine.Object.Destroy(brolyObj);
                    UnityEngine.Object.Destroy(brolySo);
                }
            }
        }

        [UnityTest]
        [Category("TV1")]
        [Category("Boss")]
        public IEnumerator TV1_BossBase_TetherWarHammer_DownedBeforeCoreBreak_AndDefeatedWhenCoreShattered()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);

                Type tetherType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Enemies.TetherBossController");
                Type coreType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Enemies.CrystalCore");
                ScriptableObject bossSo = CreateEnemyData(150f, 15f, 20f, true, true);

                GameObject bossObj = new GameObject("TV1_TestWarHammerBoss");
                GameObject coreObj = new GameObject("TV1_TestCrystalCore");
                try
                {
                    bossObj.transform.position = new Vector3(10f, -2.5f, 0f);
                    Rigidbody2D rb = bossObj.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0f;
                    bossObj.AddComponent<BoxCollider2D>();

                    coreObj.transform.SetParent(bossObj.transform, false);
                    coreObj.AddComponent<BoxCollider2D>();
                    Component coreComp = coreObj.AddComponent(coreType);

                    Component tetherBoss = bossObj.AddComponent(tetherType);
                    PlayModeTierSupport.InvokeMethod(tetherBoss, "SetData", bossSo);

                    yield return null;
                    yield return null;

                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(coreComp, "IsShieldProtected"),
                        "CrystalCore must stay shielded while War Hammer main body stands.");

                    // Hạ HP thân chính về 0 -> phải chuyển sang IsDowned (chưa chết) và mở khóa khiên CrystalCore
                    PlayModeTierSupport.InvokeMethod(tetherBoss, "TakeDamage", CreateDamageData(500f, "Skill_I", true, null));
                    yield return null;

                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(tetherBoss, "IsDowned"));
                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(tetherBoss, "IsDead"));
                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(coreComp, "IsShieldProtected"));

                    // Phá CrystalCore 500 HP -> Boss bị đánh bại hoàn toàn
                    PlayModeTierSupport.InvokeMethod(coreComp, "TakeDamage", CreateDamageData(600f, "Skill_I", true, null));
                    yield return null;

                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(coreComp, "IsDead"));
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(tetherBoss, "IsDead"));
                }
                finally
                {
                    if (coreObj != null) UnityEngine.Object.Destroy(coreObj);
                    if (bossObj != null) UnityEngine.Object.Destroy(bossObj);
                    UnityEngine.Object.Destroy(bossSo);
                }
            }
        }

        [UnityTest]
        [Category("TV1")]
        [Category("Boss")]
        public IEnumerator TV1_BossBase_MechaColossus_RequiresBothArmsDestroyedToExposeChestCore()
        {
            using (new ScopedPlayerStateBackup(clearForCleanTest: true))
            {
                PlayModeTierSupport.SetSelectedGameMode("Campaign");
                yield return E2ETestHelpers.LoadSceneAndWait("Stickman_DemoScene");

                Component levelMgr = PlayModeTierSupport.RequireManagerInstance("StickmanBeatEmUp.Level.LevelManager");
                PlayModeTierSupport.InvokeMethod(levelMgr, "SetSandboxMode", true);

                Type mechaType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Enemies.MechaBossController");
                Type partType = E2ETestHelpers.RequireRuntimeType("StickmanBeatEmUp.Enemies.BossPartHitbox");
                ScriptableObject mechaSo = CreateEnemyData(150f, 15f, 20f, true, true);

                GameObject mechaObj = new GameObject("TV1_TestMechaBoss");
                try
                {
                    mechaObj.transform.position = new Vector3(12f, -2.5f, 0f);
                    Rigidbody2D rb = mechaObj.AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0f;
                    mechaObj.AddComponent<BoxCollider2D>();

                    GameObject leftObj = new GameObject("LeftArm");
                    leftObj.transform.SetParent(mechaObj.transform, false);
                    leftObj.AddComponent<BoxCollider2D>();
                    Component leftPart = leftObj.AddComponent(partType);

                    GameObject rightObj = new GameObject("RightArm");
                    rightObj.transform.SetParent(mechaObj.transform, false);
                    rightObj.AddComponent<BoxCollider2D>();
                    Component rightPart = rightObj.AddComponent(partType);

                    GameObject coreObj = new GameObject("ChestCore");
                    coreObj.transform.SetParent(mechaObj.transform, false);
                    coreObj.AddComponent<BoxCollider2D>();
                    Component corePart = coreObj.AddComponent(partType);

                    Component mecha = mechaObj.AddComponent(mechaType);
                    PlayModeTierSupport.InvokeMethod(mecha, "SetData", mechaSo);
                    PlayModeTierSupport.InvokeMethod(mecha, "ConfigureParts", leftPart, rightPart, corePart, null);

                    yield return null;
                    yield return null;

                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(leftPart, "IsVulnerable"));
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(rightPart, "IsVulnerable"));
                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(corePart, "IsVulnerable"),
                        "ChestCore must remain invulnerable before both arms are destroyed.");

                    // Phá tay trái -> ChestCore vẫn bất tử
                    PlayModeTierSupport.InvokeMethod(leftPart, "TakeDamage", CreateDamageData(50f, "Skill_I", true, null));
                    yield return null;
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(leftPart, "IsDead"));
                    Assert.IsFalse((bool)PlayModeTierSupport.GetInstanceProperty(corePart, "IsVulnerable"));

                    // Phá tay phải -> ChestCore mở giáp
                    PlayModeTierSupport.InvokeMethod(rightPart, "TakeDamage", CreateDamageData(50f, "Skill_I", true, null));
                    yield return null;
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(rightPart, "IsDead"));
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(corePart, "IsVulnerable"));

                    // Phá ChestCore (70 HP) -> Mecha Colossus bị tiêu diệt
                    PlayModeTierSupport.InvokeMethod(corePart, "TakeDamage", CreateDamageData(80f, "Skill_I", true, null));
                    yield return null;
                    Assert.IsTrue((bool)PlayModeTierSupport.GetInstanceProperty(mecha, "IsDead"));
                }
                finally
                {
                    if (mechaObj != null) UnityEngine.Object.Destroy(mechaObj);
                    UnityEngine.Object.Destroy(mechaSo);
                }
            }
        }
    }
}
