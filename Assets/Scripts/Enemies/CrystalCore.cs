using System;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.UI;
using StickmanGame.VFX;

namespace StickmanBeatEmUp.Enemies
{
    /// <summary>
    /// Attackable Crystal Core (Lara Tybur) sustaining the War Hammer Titan via umbilical tether.
    /// Destroying this 500 HP core while the Titan is downed permanently defeats the War Hammer Titan.
    /// Enlarged to 2.35x scale and guarded by a 100% damage-immunity barrier while the War Hammer main body stands.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CrystalCore : MonoBehaviour, IDamageable, ICombatTargetProfile
    {
        public const float ENLARGED_SCALE = 2.35f;
        private const float SHIELD_RING_DURATION = 0.22f;

        [SerializeField] private float maxHealth = 500f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TetherBossController mainTitanBody;

        public Transform Transform => transform;
        public bool IsDead { get; private set; }
        public bool IsDestroyed => IsDead;
        public bool IsEliteOrBoss => true;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsShieldProtected => !IsDestroyed && mainTitanBody != null && !mainTitanBody.IsMainBodyDefeated;

        public event Action OnCoreDestroyed;

        private static Sprite sharedShieldAuraSprite;
        private static Sprite sharedShieldRingSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sharedShieldAuraSprite = null;
            sharedShieldRingSprite = null;
        }

        private Color baseColor = Color.white;
        private Collider2D coreCollider;
        private SpriteRenderer shieldOverlayRenderer;
        private SpriteRenderer shieldPulseRingRenderer;
        private float hitFlashTimer;
        private Color hitFlashColor = Color.white;
        private float shieldRingTimer;
        private Color shieldRingColor = new Color(0.15f, 0.96f, 1f, 0.92f);
        private bool wasTitanDefeated;
        private bool hasInitializedTitanState;

        private void Awake()
        {
            if (maxHealth < 500f) maxHealth = 500f;
            CurrentHealth = maxHealth;
            coreCollider = GetComponent<Collider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
            EnsureEnlargedScaleAndBounds();
            EnsureShieldVisuals();
        }

        private void Start()
        {
            if (maxHealth < 500f) maxHealth = 500f;
            CurrentHealth = maxHealth;
            if (mainTitanBody == null)
            {
                mainTitanBody = GetComponentInParent<TetherBossController>();
                if (mainTitanBody == null)
                {
                    mainTitanBody = FindAnyObjectByType<TetherBossController>();
                }
            }
            RefreshProtectionState();
        }

        private void Update()
        {
            if (IsDestroyed) return;
            UpdateProtectionVisuals(Time.deltaTime);
        }

        public void SetMaxHealth(float hp)
        {
            maxHealth = Mathf.Max(500f, hp);
            CurrentHealth = maxHealth;
        }

        public void BindMainTitanBody(TetherBossController titan)
        {
            mainTitanBody = titan;
            RefreshProtectionState();
        }

        public void RefreshProtectionState()
        {
            if (IsDestroyed) return;

            bool isTitanDefeated = mainTitanBody != null && mainTitanBody.IsMainBodyDefeated;
            if (hasInitializedTitanState && isTitanDefeated != wasTitanDefeated)
            {
                if (isTitanDefeated)
                {
                    TriggerShieldRingPulse(new Color(1f, 0.35f, 0.15f, 0.95f));
                }
                else
                {
                    TriggerShieldRingPulse(HitSparkManager.ShieldBlockCyanColor);
                }
            }

            wasTitanDefeated = isTitanDefeated;
            hasInitializedTitanState = true;
            UpdateProtectionVisuals(0f);
        }

        public void EnsureEnlargedScaleAndBounds()
        {
            float signX = transform.localScale.x < 0f ? -1f : 1f;
            float signY = transform.localScale.y < 0f ? -1f : 1f;
            float signZ = transform.localScale.z < 0f ? -1f : 1f;
            transform.localScale = new Vector3(signX * ENLARGED_SCALE, signY * ENLARGED_SCALE, signZ * ENLARGED_SCALE);

            if (coreCollider == null) coreCollider = GetComponent<Collider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (coreCollider is BoxCollider2D box)
            {
                Vector2 targetSize = new Vector2(Mathf.Max(box.size.x, 1.1f), Mathf.Max(box.size.y, 1.25f));
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    Bounds sb = spriteRenderer.sprite.bounds;
                    targetSize.x = Mathf.Max(targetSize.x, sb.size.x);
                    targetSize.y = Mathf.Max(targetSize.y, sb.size.y);
                    box.offset = new Vector2(sb.center.x, sb.center.y);
                }
                box.size = targetSize;
            }
        }

        public float GetGroundPlacementY(float groundY)
        {
            float scaleY = Mathf.Abs(transform.localScale.y) > 0.001f ? Mathf.Abs(transform.localScale.y) : ENLARGED_SCALE;
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Bounds sb = spriteRenderer.sprite.bounds;
                if (sb.min.y < -0.01f)
                {
                    float lift = Mathf.Max(0f, -sb.min.y * scaleY - 0.14f);
                    return groundY + lift;
                }
                return groundY;
            }

            if (coreCollider is BoxCollider2D box)
            {
                float minLocalY = box.offset.y - box.size.y * 0.5f;
                return minLocalY < -0.01f ? groundY + (-minLocalY * scaleY) : groundY;
            }

            return groundY + 1.35f;
        }

        public Vector3 GetTetherAnchorWorldPosition()
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return transform.TransformPoint(spriteRenderer.sprite.bounds.center);
            }
            if (coreCollider != null)
            {
                return coreCollider.bounds.center;
            }
            return transform.position + new Vector3(0f, 1.35f, 0f);
        }

        public float GetWorldHalfWidth()
        {
            float scaleX = Mathf.Abs(transform.localScale.x) > 0.001f ? Mathf.Abs(transform.localScale.x) : ENLARGED_SCALE;
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return spriteRenderer.sprite.bounds.extents.x * scaleX;
            }
            if (coreCollider is BoxCollider2D box)
            {
                return box.size.x * 0.5f * scaleX;
            }
            return 1.35f;
        }

        public void TakeDamage(DamageData damageData)
        {
            if (IsDestroyed) return;

            Vector3 hitPos = HitSparkManager.ResolveHitPoint(transform, coreCollider, spriteRenderer, damageData.attacker);
            if (IsShieldProtected)
            {
                HitSparkManager.Spawn(hitPos, HitSparkManager.ShieldBlockCyanColor, 1.35f);
                DamagePopupManager.SpawnBlocked(hitPos, 0f);
                hitFlashColor = new Color(0.22f, 0.98f, 1f, 1f);
                hitFlashTimer = 0.12f;
                TriggerShieldRingPulse(HitSparkManager.ShieldBlockCyanColor);
                UpdateProtectionVisuals(0f);
                return;
            }

            float finalDamage = Mathf.Max(0f, damageData.damageAmount);
            HitSparkManager.Spawn(hitPos, new Color(1f, 0.35f, 0.15f, 1f), 1.25f);
            if (finalDamage > 0f)
            {
                DamagePopupManager.SpawnDamage(hitPos, finalDamage, damageData.attackType, true);
            }
            hitFlashColor = Color.white;
            hitFlashTimer = 0.09f;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - finalDamage);
            UpdateProtectionVisuals(0f);

            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                hitFlashTimer = 0f;
                shieldRingTimer = 0f;
                if (coreCollider != null) coreCollider.enabled = false;
                if (shieldOverlayRenderer != null) shieldOverlayRenderer.enabled = false;
                if (shieldPulseRingRenderer != null) shieldPulseRingRenderer.enabled = false;
                if (spriteRenderer != null) spriteRenderer.color = new Color(0.35f, 0.35f, 0.4f, 0.55f);
                OnCoreDestroyed?.Invoke();
            }
        }

        private void EnsureShieldVisuals()
        {
            if (!Application.isPlaying) return;

            Vector3 localCenter = GetCrystalLocalCenter();
            int baseOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : 5;
            int sortingLayerId = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;

            if (shieldOverlayRenderer == null)
            {
                Transform existingOverlay = transform.Find("ShieldOverlay");
                GameObject overlayObj = existingOverlay != null ? existingOverlay.gameObject : new GameObject("ShieldOverlay");
                if (existingOverlay == null) overlayObj.transform.SetParent(transform, false);
                overlayObj.transform.localPosition = localCenter;
                if (!overlayObj.TryGetComponent(out shieldOverlayRenderer))
                {
                    shieldOverlayRenderer = overlayObj.AddComponent<SpriteRenderer>();
                }
                shieldOverlayRenderer.sprite = GetOrCreateShieldAuraSprite();
                shieldOverlayRenderer.sortingLayerID = sortingLayerId;
                shieldOverlayRenderer.sortingOrder = baseOrder - 1;
                shieldOverlayRenderer.enabled = false;
            }

            if (shieldPulseRingRenderer == null)
            {
                Transform existingRing = transform.Find("ShieldPulseRing");
                GameObject ringObj = existingRing != null ? existingRing.gameObject : new GameObject("ShieldPulseRing");
                if (existingRing == null) ringObj.transform.SetParent(transform, false);
                ringObj.transform.localPosition = localCenter;
                if (!ringObj.TryGetComponent(out shieldPulseRingRenderer))
                {
                    shieldPulseRingRenderer = ringObj.AddComponent<SpriteRenderer>();
                }
                shieldPulseRingRenderer.sprite = GetOrCreateShieldRingSprite();
                shieldPulseRingRenderer.sortingLayerID = sortingLayerId;
                shieldPulseRingRenderer.sortingOrder = baseOrder + 2;
                shieldPulseRingRenderer.enabled = false;
            }
        }

        private void TriggerShieldRingPulse(Color color)
        {
            EnsureShieldVisuals();
            shieldRingColor = color;
            shieldRingTimer = SHIELD_RING_DURATION;
        }

        private void UpdateProtectionVisuals(float dt)
        {
            if (dt > 0f)
            {
                if (hitFlashTimer > 0f) hitFlashTimer = Mathf.Max(0f, hitFlashTimer - dt);
                if (shieldRingTimer > 0f) shieldRingTimer = Mathf.Max(0f, shieldRingTimer - dt);
            }

            bool hasTitan = mainTitanBody != null;
            bool titanDefeated = hasTitan && mainTitanBody.IsMainBodyDefeated;
            if (hasInitializedTitanState && titanDefeated != wasTitanDefeated)
            {
                wasTitanDefeated = titanDefeated;
                if (titanDefeated)
                {
                    TriggerShieldRingPulse(new Color(1f, 0.35f, 0.15f, 0.95f));
                }
                else
                {
                    TriggerShieldRingPulse(HitSparkManager.ShieldBlockCyanColor);
                }
            }
            else
            {
                wasTitanDefeated = titanDefeated;
                hasInitializedTitanState = true;
            }

            bool isProtectedByTitan = hasTitan && !titanDefeated;
            Vector2 crystalLocalSize = GetCrystalLocalSize();
            Vector3 localCenter = GetCrystalLocalCenter();

            if (shieldOverlayRenderer != null)
            {
                shieldOverlayRenderer.enabled = isProtectedByTitan;
                if (isProtectedByTitan)
                {
                    shieldOverlayRenderer.transform.localPosition = localCenter;
                    float auraPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.2f);
                    float auraScale = 1.42f + 0.08f * auraPulse;
                    shieldOverlayRenderer.transform.localScale = new Vector3(
                        crystalLocalSize.x * auraScale,
                        crystalLocalSize.y * auraScale,
                        1f);
                    shieldOverlayRenderer.color = new Color(0.18f, 0.92f, 1f, 0.28f + 0.14f * auraPulse);
                }
            }

            if (shieldPulseRingRenderer != null)
            {
                if (shieldRingTimer > 0f)
                {
                    shieldPulseRingRenderer.enabled = true;
                    shieldPulseRingRenderer.transform.localPosition = localCenter;
                    float progress = 1f - Mathf.Clamp01(shieldRingTimer / SHIELD_RING_DURATION);
                    float ringMult = Mathf.Lerp(1.05f, 2.05f, progress);
                    shieldPulseRingRenderer.transform.localScale = new Vector3(
                        crystalLocalSize.x * ringMult,
                        crystalLocalSize.y * ringMult,
                        1f);
                    Color c = shieldRingColor;
                    c.a = Mathf.Lerp(shieldRingColor.a, 0f, progress);
                    shieldPulseRingRenderer.color = c;
                }
                else
                {
                    shieldPulseRingRenderer.enabled = false;
                }
            }

            if (spriteRenderer == null) return;

            if (hitFlashTimer > 0f)
            {
                spriteRenderer.color = hitFlashColor;
                return;
            }

            if (isProtectedByTitan)
            {
                float shellPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.8f);
                spriteRenderer.color = Color.Lerp(baseColor, new Color(0.55f, 0.94f, 1f, 1f), 0.35f + 0.15f * shellPulse);
            }
            else if (hasTitan && titanDefeated)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 37f);
                spriteRenderer.color = Color.Lerp(
                    new Color(1f, 0.22f, 0.10f, 1f),
                    new Color(1f, 0.85f, 0.55f, 1f),
                    Mathf.Clamp01(pulse * flicker));
            }
            else
            {
                spriteRenderer.color = baseColor;
            }
        }

        private Vector3 GetCrystalLocalCenter()
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return spriteRenderer.sprite.bounds.center;
            }
            if (coreCollider is BoxCollider2D box)
            {
                return box.offset;
            }
            return Vector3.zero;
        }

        private Vector2 GetCrystalLocalSize()
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Vector3 size = spriteRenderer.sprite.bounds.size;
                return new Vector2(Mathf.Max(size.x, 1.1f), Mathf.Max(size.y, 1.25f));
            }
            if (coreCollider is BoxCollider2D box)
            {
                return new Vector2(Mathf.Max(box.size.x, 1.1f), Mathf.Max(box.size.y, 1.25f));
            }
            return new Vector2(1.1f, 1.25f);
        }

        private static Sprite GetOrCreateShieldAuraSprite()
        {
            if (sharedShieldAuraSprite != null) return sharedShieldAuraSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float glow = Mathf.Pow(1f - r, 1.45f) * 0.65f;
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(r - 0.80f) * 7f) * 0.65f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(glow, rim)));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            sharedShieldAuraSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedShieldAuraSprite;
        }

        private static Sprite GetOrCreateShieldRingSprite()
        {
            if (sharedShieldRingSprite != null) return sharedShieldRingSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = r >= 1f ? 0f : Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 8.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, ring);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            sharedShieldRingSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedShieldRingSprite;
        }
    }
}
