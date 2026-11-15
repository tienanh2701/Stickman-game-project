using System;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Mid-cable destructible target connecting War Hammer Titan to CrystalCore.
    /// Player can sever the cable directly or break CrystalCore to stop regeneration.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TetherCableHitbox : MonoBehaviour, IDamageable
    {
        [SerializeField] private float cableHealth = 20f;
        [SerializeField] private SpriteRenderer indicatorRenderer;

        public Transform Transform => transform;
        public bool IsDead { get; private set; }

        public event Action OnCableSevered;

        private float currentHealth;

        private void Start()
        {
            currentHealth = cableHealth;
        }

        public void SetCableHealth(float hp)
        {
            cableHealth = Mathf.Max(1f, hp);
            currentHealth = cableHealth;
        }

        public void TakeDamage(DamageData damageData)
        {
            if (IsDead) return;
            currentHealth = Mathf.Max(0f, currentHealth - damageData.damageAmount);
            if (currentHealth <= 0f)
            {
                IsDead = true;
                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
                if (indicatorRenderer != null) indicatorRenderer.enabled = false;
                OnCableSevered?.Invoke();
            }
        }
    }
}
