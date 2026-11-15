using System;
using System.Collections;
using UnityEngine;
using StickmanBeatEmUp.Core;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Attackable Crystal Core (Lara Tybur) sustaining the War Hammer Titan via umbilical tether.
    /// Destroying this permanently severs War Hammer's regeneration.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CrystalCore : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 50f;
        [SerializeField] private SpriteRenderer spriteRenderer;

        public Transform Transform => transform;
        public bool IsDead { get; private set; }
        public float CurrentHealth { get; private set; }

        public event Action OnCoreDestroyed;

        private Color baseColor = Color.white;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
        }

        private void Start()
        {
            CurrentHealth = maxHealth;
        }

        public void SetMaxHealth(float hp)
        {
            maxHealth = Mathf.Max(1f, hp);
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(DamageData damageData)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damageData.damageAmount);
            StartCoroutine(PulseRoutine());

            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
                if (spriteRenderer != null) spriteRenderer.color = new Color(0.35f, 0.35f, 0.4f, 0.55f);
                OnCoreDestroyed?.Invoke();
            }
        }

        private IEnumerator PulseRoutine()
        {
            if (spriteRenderer == null) yield break;
            spriteRenderer.color = new Color(0.55f, 0.95f, 1f, 1f);
            yield return new WaitForSeconds(0.1f);
            if (!IsDead && spriteRenderer != null) spriteRenderer.color = baseColor;
        }
    }
}
