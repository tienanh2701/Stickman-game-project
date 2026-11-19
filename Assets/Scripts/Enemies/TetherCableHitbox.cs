using System;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Non-destructible visual anchor on the tether connecting War Hammer Titan to CrystalCore.
    /// Collider is disabled and TakeDamage is a no-op so the tether can only be broken by destroying the 500 HP CrystalCore.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TetherCableHitbox : MonoBehaviour, IDamageable
    {
        [SerializeField] private float cableHealth = 20f;
        [SerializeField] private SpriteRenderer indicatorRenderer;

        public Transform Transform => transform;
        public bool IsDead { get; private set; }
        public float CurrentHealth => currentHealth;

        public event Action OnCableSevered
        {
            add { }
            remove { }
        }

        private float currentHealth;
        private Collider2D cableCollider;

        private void Awake()
        {
            cableCollider = GetComponent<Collider2D>();
            if (cableCollider != null) cableCollider.enabled = false;
            if (indicatorRenderer != null) indicatorRenderer.enabled = false;
            currentHealth = cableHealth;
        }

        private void Start()
        {
            if (cableCollider != null) cableCollider.enabled = false;
            currentHealth = cableHealth;
        }

        public void SetCableHealth(float hp)
        {
            cableHealth = Mathf.Max(1f, hp);
            currentHealth = cableHealth;
        }

        public void TakeDamage(DamageData damageData)
        {
            // No-op: tether cannot be severed directly; player must shatter the 500 HP CrystalCore while Titan is downed.
        }
    }
}
