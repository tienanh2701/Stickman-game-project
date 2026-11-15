using System;
using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    public enum BossPartRole
    {
        LeftArm,
        RightArm,
        ChestCore
    }

    /// <summary>
    /// Modular multi-part hitbox for Mecha Colossus Boss (LeftArm, RightArm, ChestCore x2 damage).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BossPartHitbox : MonoBehaviour, IDamageable
    {
        [SerializeField] private BossPartRole partRole = BossPartRole.LeftArm;
        [SerializeField] private float partHealth = 180f;
        [SerializeField] private SpriteRenderer partRenderer;

        public BossPartRole Role => partRole;
        public Transform Transform => transform;
        public bool IsDead { get; private set; }
        public bool IsVulnerable { get; private set; } = true;
        public float CurrentHealth { get; private set; }

        public event Action<BossPartHitbox, float, DamageData> OnPartDamaged;
        public event Action<BossPartHitbox> OnPartDestroyed;

        private Color baseColor = Color.white;

        private void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("Enemy") >= 0 ? LayerMask.NameToLayer("Enemy") : 8;
            if (partRenderer == null) partRenderer = GetComponent<SpriteRenderer>();
            if (partRenderer != null) baseColor = partRenderer.color;
        }

        private void Start()
        {
            CurrentHealth = partHealth;
        }

        public void Initialize(BossPartRole role, float hp, bool vulnerable)
        {
            partRole = role;
            partHealth = Mathf.Max(1f, hp);
            CurrentHealth = partHealth;
            IsDead = false;
            SetVulnerable(vulnerable);
        }

        public void SetVulnerable(bool vulnerable)
        {
            IsVulnerable = vulnerable;
            if (partRenderer != null && !IsDead)
            {
                partRenderer.color = vulnerable ? baseColor : new Color(0.45f, 0.45f, 0.5f, 0.55f);
            }
        }

        public void TakeDamage(DamageData damageData)
        {
            if (IsDead || !IsVulnerable) return;

            float mult = 1f;
            float finalDmg = damageData.damageAmount * mult;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - finalDmg);
            OnPartDamaged?.Invoke(this, finalDmg, damageData);
            StartCoroutine(FlashRoutine());

            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
                if (partRenderer != null) partRenderer.color = new Color(0.25f, 0.25f, 0.25f, 0.45f);
                OnPartDestroyed?.Invoke(this);
            }
        }

        private IEnumerator FlashRoutine()
        {
            if (partRenderer == null) yield break;
            partRenderer.color = partRole == BossPartRole.ChestCore ? Color.yellow : new Color(1f, 0.4f, 0.4f, 1f);
            yield return new WaitForSeconds(0.09f);
            if (!IsDead && partRenderer != null)
            {
                partRenderer.color = IsVulnerable ? baseColor : new Color(0.45f, 0.45f, 0.5f, 0.55f);
            }
        }
    }
}
