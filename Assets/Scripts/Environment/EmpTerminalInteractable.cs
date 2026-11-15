using UnityEngine;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Level;

namespace StickmanBeatEmUp.Environment
{
    /// <summary>
    /// Interactive EMP Terminal in World 3 Stage 4 Boss Arena.
    /// Striking it while off its 15.0s cooldown unleashes an arena-wide cyan EMP ring that paralyzes
    /// MechaBossController (4.5s), freezes nearby EnemyBase units (3.0s), and suppresses LowLaserTurretHazards (5.0s).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EmpTerminalInteractable : MonoBehaviour, IDamageable
    {
        [Header("EMP Config")]
        [SerializeField] private float cooldownDuration = 15.0f;
        [SerializeField] private float mechaParalysisDuration = 4.5f;
        [SerializeField] private float enemyStunDuration = 3.0f;
        [SerializeField] private float turretSuppressDuration = 5.0f;
        [SerializeField] private float enemyRadius = 24f;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Collider2D col;
        private float nextReadyTime;

        public Transform Transform => transform;
        public bool IsDead => Time.time < nextReadyTime || !CampaignHazardSpawner.CanHazardOperate();

        private void Awake()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            gameObject.layer = enemyLayer >= 0 ? enemyLayer : 8;

            col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CampaignHazardSpawner.GetFallbackSquareSprite();
                spriteRenderer.sortingOrder = 4;
            }
        }

        private void Start()
        {
            if (!CampaignHazardSpawner.CanHazardOperate())
            {
                if (col != null) col.enabled = false;
                if (spriteRenderer != null) spriteRenderer.enabled = false;
            }
        }

        private void Update()
        {
            if (spriteRenderer == null || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            if (Time.time >= nextReadyTime)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.5f);
                spriteRenderer.color = Color.Lerp(Color.white, new Color(0.35f, 0.95f, 1f, 1f), pulse * 0.65f);
            }
            else
            {
                spriteRenderer.color = new Color(0.38f, 0.42f, 0.48f, 0.82f);
            }
        }

        public void TakeDamage(DamageData damageData)
        {
            if (Time.time < nextReadyTime || !CampaignHazardSpawner.CanHazardOperate())
            {
                return;
            }

            // Ignore accidental non-player enemy triggers
            if (damageData.attacker != null &&
                damageData.attacker.GetComponentInParent<BaseCharacter>() == null &&
                (damageData.attacker.GetComponentInParent<EnemyBase>() != null ||
                 damageData.attacker.GetComponentInParent<BossBase>() != null))
            {
                return;
            }

            TriggerEmpPulse();
        }

        private void TriggerEmpPulse()
        {
            nextReadyTime = Time.time + cooldownDuration;

            // 1. Paralyze all MechaBossController instances in the arena (4.5s)
            MechaBossController[] mechas = FindObjectsByType<MechaBossController>();
            for (int i = 0; i < mechas.Length; i++)
            {
                if (mechas[i] != null && !mechas[i].IsDead)
                {
                    mechas[i].TriggerEmpParalysis(mechaParalysisDuration);
                }
            }

            // 2. Stun/pause nearby EnemyBase units (3.0s)
            EnemyBase[] enemies = FindObjectsByType<EnemyBase>();
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && !enemies[i].IsDead &&
                    Vector2.Distance(transform.position, enemies[i].transform.position) <= enemyRadius)
                {
                    enemies[i].Freeze(enemyStunDuration);
                }
            }

            // 3. Also freeze any legacy BaseEnemy units nearby if present
            BaseEnemy[] legacyEnemies = FindObjectsByType<BaseEnemy>();
            for (int i = 0; i < legacyEnemies.Length; i++)
            {
                if (legacyEnemies[i] != null && !legacyEnemies[i].IsDead &&
                    Vector2.Distance(transform.position, legacyEnemies[i].transform.position) <= enemyRadius)
                {
                    legacyEnemies[i].Freeze(enemyStunDuration);
                }
            }

            // 4. Suppress active LowLaserTurretHazards (5.0s)
            LowLaserTurretHazard[] turrets = FindObjectsByType<LowLaserTurretHazard>();
            for (int i = 0; i < turrets.Length; i++)
            {
                if (turrets[i] != null)
                {
                    turrets[i].SuppressByEmp(turretSuppressDuration);
                }
            }

            CameraFollowBeatEmUp.Instance?.Shake(0.30f, 0.25f);
            StartCoroutine(CampaignHazardSpawner.AnimateExpandingRingRoutine(
                transform.position,
                new Color(0.25f, 0.94f, 1f, 0.9f),
                0.8f,
                20f,
                0.5f,
                transform
            ));
        }
    }
}
