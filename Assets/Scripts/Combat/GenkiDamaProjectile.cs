using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StickmanBeatEmUp.Audio;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Enemies;
using StickmanBeatEmUp.Level;
using StickmanGame.VFX;

namespace StickmanBeatEmUp.Combat
{
    /// <summary>
    /// Massive Genki Dama (Spirit Bomb) projectile & full-screen detonation handler for SSJ Goku (Dash + I).
    /// Scales cleanly to exactly 1/2 of the camera vertical map height, wraps an orbiting/pulsing cyan-white aura halo,
    /// descends smoothly toward the nearest target or ground, and detonates with a full-screen flash, shockwave,
    /// heavy camera tremor, and map-wide enemy damage.
    /// </summary>
    public class GenkiDamaProjectile : MonoBehaviour
    {
        [Header("Flight & Scale Settings")]
        [SerializeField] private float flightSpeed = 16.5f;
        [SerializeField] private float maxFlightDuration = 2.2f;
        [SerializeField] private float initialChargeScaleRatio = 0.18f;
        [SerializeField] private float haloScaleMultiplier = 1.42f;

        [Header("Visual Tints")]
        [SerializeField] private Color sparkTint = new Color(0.55f, 0.92f, 1f, 1f);
        [SerializeField] private Color flashColor = new Color(0.86f, 0.96f, 1f, 0f);

        private static Texture2D cachedOrbTexture;
        private static Sprite cachedOrbSprite;
        private static Texture2D cachedHaloTexture;
        private static Sprite cachedHaloSprite;
        private static Texture2D cachedRingTexture;
        private static Sprite cachedRingSprite;
        private static Texture2D cachedCoreBurstTexture;
        private static Sprite cachedCoreBurstSprite;

        private GameObject owner;
        private float ultimateDamage;
        private float facingDirection = 1f;
        private LayerMask enemyLayers;
        private float groundLevelY = -2.5f;

        private float mapVerticalHeight = 10f;
        private float targetWorldDiameter = 5f;
        private float spriteLocalDiameter = 1f;
        private float fullScale = 1f;
        private float chargeProgress = 0f;

        private bool isInitialized = false;
        private bool isLaunched = false;
        private bool hasDetonated = false;
        private float flightElapsed = 0f;
        private Vector2 launchVelocity;

        private SpriteRenderer orbRenderer;
        private SpriteRenderer haloRenderer;
        private float haloBaseLocalScale = 1.42f;

        public float TargetWorldDiameter => targetWorldDiameter;
        public float MapVerticalHeight => mapVerticalHeight;
        public float ChargeProgress => chargeProgress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCache()
        {
            cachedOrbSprite = null;
            cachedOrbTexture = null;
            cachedHaloSprite = null;
            cachedHaloTexture = null;
            cachedRingSprite = null;
            cachedRingTexture = null;
            cachedCoreBurstSprite = null;
            cachedCoreBurstTexture = null;
        }

        /// <summary>
        /// Initializes the Spirit Bomb orb to exactly 1/2 vertical map height at full charge,
        /// neutralizes any legacy Explosion/Projectile components, and attaches the outer cyan-white halo.
        /// </summary>
        public void Initialize(GameObject ownerObj, float damage, float facingDir, LayerMask targetLayers, float groundY = -2.5f)
        {
            owner = ownerObj;
            ultimateDamage = damage;
            facingDirection = Mathf.Abs(facingDir) > 0.01f ? Mathf.Sign(facingDir) : 1f;
            groundLevelY = groundY;

            if (targetLayers.value == 0)
            {
                int eLayer = LayerMask.NameToLayer("Enemy");
                enemyLayers = eLayer >= 0 ? (1 << eLayer) : ~0;
            }
            else
            {
                enemyLayers = targetLayers;
            }

            isLaunched = false;
            hasDetonated = false;
            flightElapsed = 0f;
            launchVelocity = Vector2.zero;
            chargeProgress = 0f;
            isInitialized = true;

            NeutralizeLegacyComponents();

            Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            mapVerticalHeight = (cam != null && cam.orthographic) ? cam.orthographicSize * 2f : 10f;
            targetWorldDiameter = 0.5f * mapVerticalHeight;

            transform.rotation = Quaternion.identity;

            EnsureOrbVisual();
            ConfigurePhysicsAndCollider();
            EnsureOuterAuraHalo();

            SetChargeProgress(0f);
        }

        private void NeutralizeLegacyComponents()
        {
            Explosion[] legacyExplosions = GetComponentsInChildren<Explosion>(true);
            for (int i = 0; i < legacyExplosions.Length; i++)
            {
                if (legacyExplosions[i] != null)
                {
                    legacyExplosions[i].enabled = false;
                    Destroy(legacyExplosions[i]);
                }
            }

            Projectile[] legacyProjectiles = GetComponentsInChildren<Projectile>(true);
            for (int i = 0; i < legacyProjectiles.Length; i++)
            {
                if (legacyProjectiles[i] != null)
                {
                    legacyProjectiles[i].enabled = false;
                    Destroy(legacyProjectiles[i]);
                }
            }
        }

        private void EnsureOrbVisual()
        {
            orbRenderer = GetComponent<SpriteRenderer>();
            if (orbRenderer == null)
            {
                SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < childRenderers.Length; i++)
                {
                    if (childRenderers[i] != null && childRenderers[i].gameObject.name != "GenkiAuraHalo")
                    {
                        orbRenderer = childRenderers[i];
                        break;
                    }
                }
            }

            if (orbRenderer == null)
            {
                orbRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (orbRenderer.sprite == null)
            {
                orbRenderer.sprite = GetOrCreateOrbSprite();
            }

            orbRenderer.color = Color.white;
            orbRenderer.sortingOrder = Mathf.Max(orbRenderer.sortingOrder, 18);

            Bounds bounds = orbRenderer.sprite.bounds;
            spriteLocalDiameter = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.y));
            fullScale = targetWorldDiameter / spriteLocalDiameter;
        }

        private void ConfigurePhysicsAndCollider()
        {
            Collider2D orbCollider = GetComponent<Collider2D>();
            if (orbCollider == null)
            {
                orbCollider = gameObject.AddComponent<CircleCollider2D>();
            }

            orbCollider.isTrigger = true;
            if (orbCollider is CircleCollider2D circleCol)
            {
                circleCol.offset = Vector2.zero;
                circleCol.radius = spriteLocalDiameter * 0.42f;
            }
            else if (orbCollider is BoxCollider2D boxCol)
            {
                boxCol.offset = Vector2.zero;
                boxCol.size = new Vector2(spriteLocalDiameter * 0.84f, spriteLocalDiameter * 0.84f);
            }

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        private void EnsureOuterAuraHalo()
        {
            Transform existingHalo = transform.Find("GenkiAuraHalo");
            GameObject haloObj = existingHalo != null ? existingHalo.gameObject : new GameObject("GenkiAuraHalo");
            haloObj.transform.SetParent(transform, false);
            haloObj.transform.localPosition = Vector3.zero;
            haloObj.transform.localRotation = Quaternion.identity;

            haloRenderer = haloObj.GetComponent<SpriteRenderer>();
            if (haloRenderer == null)
            {
                haloRenderer = haloObj.AddComponent<SpriteRenderer>();
            }

            Sprite haloSprite = GetOrCreateHaloSprite();
            haloRenderer.sprite = haloSprite;
            haloRenderer.sortingLayerID = orbRenderer.sortingLayerID;
            haloRenderer.sortingOrder = orbRenderer.sortingOrder - 1;
            haloRenderer.color = new Color(0.58f, 0.91f, 1f, 0.85f);

            float haloSpriteDiam = Mathf.Max(0.01f, Mathf.Max(haloSprite.bounds.size.x, haloSprite.bounds.size.y));
            haloBaseLocalScale = haloScaleMultiplier * (spriteLocalDiameter / haloSpriteDiam);
            haloRenderer.transform.localScale = Vector3.one * haloBaseLocalScale;
        }

        /// <summary>
        /// Scales the Spirit Bomb smoothly during Goku's overhead charge (0..1 -> 0.18*fullScale..1.0*fullScale).
        /// </summary>
        public void SetChargeProgress(float t)
        {
            if (hasDetonated) return;

            chargeProgress = Mathf.Clamp01(t);
            float scaleRatio = Mathf.Lerp(initialChargeScaleRatio, 1f, chargeProgress);
            transform.localScale = Vector3.one * (fullScale * scaleRatio);

            if (haloRenderer != null)
            {
                Color c = haloRenderer.color;
                c.a = Mathf.Lerp(0.55f, 0.92f, chargeProgress);
                haloRenderer.color = c;
            }
        }

        /// <summary>
        /// Computes a clean forward-downward launch velocity aimed toward the nearest live enemy in front of Goku,
        /// or a diagonal downward arc if no enemy is positioned forward.
        /// </summary>
        public Vector2 ComputeTargetAimVelocity(Vector3 originPosition, float customSpeed = -1f)
        {
            float speed = customSpeed > 0f ? customSpeed : flightSpeed;
            float referenceX = owner != null ? owner.transform.position.x : originPosition.x;

            bool foundTarget = false;
            Vector3 bestTargetPos = Vector3.zero;
            float bestDistSq = float.MaxValue;

            ConsiderTargets(FindObjectsByType<BossBase>(), referenceX, originPosition, ref foundTarget, ref bestTargetPos, ref bestDistSq);
            ConsiderTargets(FindObjectsByType<EnemyBase>(), referenceX, originPosition, ref foundTarget, ref bestTargetPos, ref bestDistSq);
            ConsiderTargets(FindObjectsByType<BaseEnemy>(), referenceX, originPosition, ref foundTarget, ref bestTargetPos, ref bestDistSq);
            ConsiderTargets(FindObjectsByType<CrystalCore>(), referenceX, originPosition, ref foundTarget, ref bestTargetPos, ref bestDistSq);

            if (foundTarget)
            {
                float targetX = bestTargetPos.x;
                if (Mathf.Abs(targetX - originPosition.x) < 0.75f)
                {
                    targetX = originPosition.x + facingDirection * 0.75f;
                }

                float targetY = Mathf.Min(bestTargetPos.y + 0.55f, originPosition.y - 0.85f);
                targetY = Mathf.Max(groundLevelY, targetY);

                Vector2 dir = new Vector2(targetX - originPosition.x, targetY - originPosition.y);
                if (dir.sqrMagnitude > 0.001f)
                {
                    return dir.normalized * speed;
                }
            }

            return new Vector2(facingDirection * 0.78f, -0.62f).normalized * speed;
        }

        private void ConsiderTargets<T>(
            T[] candidates,
            float referenceX,
            Vector3 originPosition,
            ref bool foundTarget,
            ref Vector3 bestTargetPos,
            ref float bestDistSq) where T : Component, IDamageable
        {
            if (candidates == null) return;

            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null || candidate.IsDead || !candidate.gameObject.activeInHierarchy) continue;
                if (owner != null && candidate.gameObject == owner) continue;

                Vector3 pos = candidate.Transform != null ? candidate.Transform.position : candidate.transform.position;
                float forwardOffset = (pos.x - referenceX) * facingDirection;
                if (forwardOffset < -0.5f) continue;

                float dSq = ((Vector2)(pos - originPosition)).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestTargetPos = pos;
                    foundTarget = true;
                }
            }
        }

        /// <summary>
        /// Launches the fully charged Spirit Bomb along the given velocity (or computes an automatic downward-forward aim if zero).
        /// </summary>
        public void Launch(Vector2 velocity)
        {
            if (hasDetonated || isLaunched) return;

            if (!isInitialized)
            {
                Initialize(owner, ultimateDamage, facingDirection, enemyLayers, groundLevelY);
            }

            isLaunched = true;
            flightElapsed = 0f;
            SetChargeProgress(1f);

            launchVelocity = velocity.sqrMagnitude > 0.001f
                ? velocity
                : ComputeTargetAimVelocity(transform.position, flightSpeed);
        }

        private void Update()
        {
            if (hasDetonated) return;

            if (haloRenderer != null)
            {
                haloRenderer.transform.Rotate(0f, 0f, -65f * Time.deltaTime);
                float pulse = 1f + 0.055f * Mathf.Sin(Time.time * 10f);
                haloRenderer.transform.localScale = Vector3.one * (haloBaseLocalScale * pulse);
            }

            if (!isInitialized) return;

            if (!isLaunched)
            {
                if (owner == null)
                {
                    Destroy(gameObject);
                }
                return;
            }

            float dt = Time.deltaTime;
            flightElapsed += dt;
            transform.position += (Vector3)(launchVelocity * dt);

            float groundImpactY = groundLevelY + targetWorldDiameter * 0.18f;
            if (transform.position.y <= groundImpactY)
            {
                Detonate();
                return;
            }

            if (HasProximityTargetImpact())
            {
                Detonate();
                return;
            }

            if (flightElapsed >= maxFlightDuration)
            {
                Detonate();
            }
        }

        private bool HasProximityTargetImpact()
        {
            float probeRadius = targetWorldDiameter * 0.36f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, probeRadius);
            if (hits == null) return false;

            for (int i = 0; i < hits.Length; i++)
            {
                if (IsValidDetonationCollider(hits[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isInitialized || !isLaunched || hasDetonated || other == null) return;

            if (IsValidDetonationCollider(other))
            {
                Detonate();
            }
        }

        private bool IsValidDetonationCollider(Collider2D other)
        {
            if (other == null) return false;
            if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
            {
                return false;
            }

            BossBase boss = other.GetComponent<BossBase>() ?? other.GetComponentInParent<BossBase>();
            if (boss != null && !boss.IsDead) return true;

            EnemyBase enemy = other.GetComponent<EnemyBase>() ?? other.GetComponentInParent<EnemyBase>();
            if (enemy != null && !enemy.IsDead) return true;

            BaseEnemy legacyEnemy = other.GetComponent<BaseEnemy>() ?? other.GetComponentInParent<BaseEnemy>();
            if (legacyEnemy != null && !legacyEnemy.IsDead) return true;

            CrystalCore core = other.GetComponent<CrystalCore>() ?? other.GetComponentInParent<CrystalCore>();
            if (core != null && !core.IsDead) return true;

            BossPartHitbox part = other.GetComponent<BossPartHitbox>() ?? other.GetComponentInParent<BossPartHitbox>();
            if (part != null && !part.IsDead) return true;

            return false;
        }

        /// <summary>
        /// Triggers the full-screen white-blue flash, camera shake, massive shockwave burst, and map-wide enemy damage.
        /// Guarded so it executes at most once.
        /// </summary>
        public void Detonate()
        {
            if (hasDetonated) return;
            hasDetonated = true;
            isLaunched = false;

            Vector3 explosionCenter = transform.position;
            explosionCenter.z = 0f;

            SpawnFullScreenFlash();
            if (Application.isPlaying)
            {
                AudioManager.Instance?.PlaySfxByResourceName("Goku/goku_dashI_genkidama_explode", 1.0f);
            }
            CameraFollowBeatEmUp.Instance?.Shake(0.65f, 0.48f);
            SpawnShockwaveBurst(explosionCenter);
            ApplyMapWideDamage(explosionCenter);

            Destroy(gameObject);
        }

        private void SpawnFullScreenFlash()
        {
            GameObject flashRoot = new GameObject("FullScreenFlash");
            Canvas canvas = flashRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            CanvasScaler scaler = flashRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject overlayObj = new GameObject("FlashOverlay");
            overlayObj.transform.SetParent(flashRoot.transform, false);

            RectTransform rect = overlayObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image flashImage = overlayObj.AddComponent<Image>();
            flashImage.color = flashColor;
            flashImage.raycastTarget = false;

            FullScreenFlashRunner runner = flashRoot.AddComponent<FullScreenFlashRunner>();
            runner.Play(flashImage, flashColor);
        }

        private void SpawnShockwaveBurst(Vector3 explosionCenter)
        {
            GameObject shockwaveRoot = new GameObject("GenkiShockwave");
            shockwaveRoot.transform.position = explosionCenter;

            Sprite ringSprite = GetOrCreateShockwaveRingSprite();
            Sprite coreSprite = GetOrCreateCoreBurstSprite();
            int baseSortLayer = orbRenderer != null ? orbRenderer.sortingLayerID : 0;
            int baseSortOrder = orbRenderer != null ? orbRenderer.sortingOrder : 18;

            GameObject coreObj = new GameObject("CoreBurstDome");
            coreObj.transform.SetParent(shockwaveRoot.transform, false);
            SpriteRenderer coreSr = coreObj.AddComponent<SpriteRenderer>();
            coreSr.sprite = coreSprite;
            coreSr.sortingLayerID = baseSortLayer;
            coreSr.sortingOrder = baseSortOrder + 2;

            GameObject outerRingObj = new GameObject("ShockwaveRing");
            outerRingObj.transform.SetParent(shockwaveRoot.transform, false);
            SpriteRenderer outerRingSr = outerRingObj.AddComponent<SpriteRenderer>();
            outerRingSr.sprite = ringSprite;
            outerRingSr.sortingLayerID = baseSortLayer;
            outerRingSr.sortingOrder = baseSortOrder + 3;

            GameObject innerRingObj = new GameObject("ShockwaveInnerRing");
            innerRingObj.transform.SetParent(shockwaveRoot.transform, false);
            SpriteRenderer innerRingSr = innerRingObj.AddComponent<SpriteRenderer>();
            innerRingSr.sprite = ringSprite;
            innerRingSr.sortingLayerID = baseSortLayer;
            innerRingSr.sortingOrder = baseSortOrder + 4;

            GenkiShockwaveRunner runner = shockwaveRoot.AddComponent<GenkiShockwaveRunner>();
            runner.Play(coreSr, outerRingSr, innerRingSr, targetWorldDiameter, mapVerticalHeight * 3.0f);
        }

        private void ApplyMapWideDamage(Vector3 explosionCenter)
        {
            HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
            HashSet<MechaBossController> hitMechaParents = new HashSet<MechaBossController>();

            // 1. BossBase first (so War Hammer Titan enters Downed cleanly if main HP drops to 0)
            BossBase[] bosses = FindObjectsByType<BossBase>();
            for (int i = 0; i < bosses.Length; i++)
            {
                BossBase boss = bosses[i];
                if (boss == null || boss.IsDead || !boss.gameObject.activeInHierarchy) continue;
                if (owner != null && boss.gameObject == owner) continue;
                if (!damagedTargets.Add(boss)) continue;

                if (boss is MechaBossController mecha)
                {
                    hitMechaParents.Add(mecha);
                }

                DeliverUltimateHit(boss, boss, explosionCenter);
            }

            // 2. All active EnemyBase (Creeps & Elites everywhere on screen/map)
            EnemyBase[] enemies = FindObjectsByType<EnemyBase>();
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyBase enemy = enemies[i];
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;
                if (owner != null && enemy.gameObject == owner) continue;
                if (!damagedTargets.Add(enemy)) continue;

                DeliverUltimateHit(enemy, enemy, explosionCenter);
            }

            // 3. All active BaseEnemy
            BaseEnemy[] legacyEnemies = FindObjectsByType<BaseEnemy>();
            for (int i = 0; i < legacyEnemies.Length; i++)
            {
                BaseEnemy legacyEnemy = legacyEnemies[i];
                if (legacyEnemy == null || legacyEnemy.IsDead || !legacyEnemy.gameObject.activeInHierarchy) continue;
                if (owner != null && legacyEnemy.gameObject == owner) continue;
                if (!damagedTargets.Add(legacyEnemy)) continue;

                DeliverUltimateHit(legacyEnemy, legacyEnemy, explosionCenter);
            }

            // 4. All active CrystalCore targets
            CrystalCore[] cores = FindObjectsByType<CrystalCore>();
            for (int i = 0; i < cores.Length; i++)
            {
                CrystalCore core = cores[i];
                if (core == null || core.IsDead || !core.gameObject.activeInHierarchy) continue;
                if (owner != null && core.gameObject == owner) continue;
                if (!damagedTargets.Add(core)) continue;

                DeliverUltimateHit(core, core, explosionCenter);
            }

            // 5. Standalone BossPartHitbox (skip if parent MechaBossController already forwarded damage)
            BossPartHitbox[] parts = FindObjectsByType<BossPartHitbox>();
            for (int i = 0; i < parts.Length; i++)
            {
                BossPartHitbox part = parts[i];
                if (part == null || part.IsDead || !part.gameObject.activeInHierarchy) continue;
                if (owner != null && part.gameObject == owner) continue;

                MechaBossController parentMecha = part.GetComponentInParent<MechaBossController>();
                if (parentMecha != null && hitMechaParents.Contains(parentMecha)) continue;
                if (!damagedTargets.Add(part)) continue;

                DeliverUltimateHit(part, part, explosionCenter);
            }
        }

        private void DeliverUltimateHit(IDamageable target, Component targetComponent, Vector3 explosionCenter)
        {
            if (target == null || target.IsDead || targetComponent == null) return;

            Transform targetTr = target.Transform != null ? target.Transform : targetComponent.transform;
            float dx = targetTr.position.x - explosionCenter.x;
            float outwardDir = Mathf.Abs(dx) > 0.02f ? Mathf.Sign(dx) : facingDirection;

            DamageData dmgData = new DamageData(
                ultimateDamage,
                new Vector2(outwardDir * 15f, 5f),
                AttackType.Combo_DashI,
                true,
                owner
            );

            target.TakeDamage(dmgData);
            BaseCharacter.NotifyAttackerHit(owner, AttackType.Combo_DashI, target);

            Collider2D targetCol = targetComponent.GetComponent<Collider2D>();
            SpriteRenderer targetSr = targetComponent.GetComponentInChildren<SpriteRenderer>();
            Vector3 hitPos = HitSparkManager.ResolveHitPoint(targetTr, targetCol, targetSr, owner);
            HitSparkManager.Spawn(hitPos, sparkTint, 1.6f);
        }

        private static Sprite GetOrCreateOrbSprite()
        {
            if (cachedOrbSprite != null && cachedOrbTexture != null) return cachedOrbSprite;

            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxRadius = size * 0.48f;

            Color whiteCore = new Color(0.96f, 0.995f, 1f, 1f);
            Color cyanMid = new Color(0.32f, 0.86f, 1f, 1f);
            Color royalRim = new Color(0.10f, 0.42f, 0.98f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxRadius;
                    float dy = (y - center) / maxRadius;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float angle = Mathf.Atan2(dy, dx);
                    float swirl = 0.055f * Mathf.Sin(angle * 7f + r * 14f) + 0.035f * Mathf.Cos(angle * 11f - r * 9f);
                    float warpedR = Mathf.Clamp01(r + swirl * Mathf.Clamp01(r * 1.4f));

                    Color col;
                    if (warpedR < 0.45f)
                    {
                        float t = warpedR / 0.45f;
                        col = Color.Lerp(whiteCore, cyanMid, t * t * 0.55f);
                    }
                    else if (warpedR < 0.82f)
                    {
                        float t = (warpedR - 0.45f) / 0.37f;
                        col = Color.Lerp(Color.Lerp(whiteCore, cyanMid, 0.55f), royalRim, t);
                    }
                    else
                    {
                        col = royalRim;
                    }

                    float alpha = r <= 0.89f ? 1f : Mathf.Pow(Mathf.Clamp01(1f - (r - 0.89f) / 0.11f), 1.8f);
                    col.a = alpha;
                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            cachedOrbTexture = tex;
            cachedOrbSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            cachedOrbSprite.name = "GenkiDamaOrb_Procedural";
            cachedOrbSprite.hideFlags = HideFlags.DontSave;
            return cachedOrbSprite;
        }

        private static Sprite GetOrCreateHaloSprite()
        {
            if (cachedHaloSprite != null && cachedHaloTexture != null) return cachedHaloSprite;

            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxRadius = size * 0.49f;

            Color innerGlow = new Color(0.88f, 0.98f, 1f, 1f);
            Color outerGlow = new Color(0.22f, 0.65f, 1f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxRadius;
                    float dy = (y - center) / maxRadius;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float angle = Mathf.Atan2(dy, dx);
                    float corona = 1f + 0.10f * Mathf.Cos(angle * 8f) + 0.06f * Mathf.Sin(angle * 13f);
                    float falloff = Mathf.Pow(1f - r, 1.85f);
                    float alpha = Mathf.Clamp01(falloff * corona * (r < 0.65f ? 0.95f : 0.78f));

                    Color col = Color.Lerp(innerGlow, outerGlow, Mathf.Clamp01((r - 0.35f) / 0.65f));
                    col.a = alpha;
                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            cachedHaloTexture = tex;
            cachedHaloSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            cachedHaloSprite.name = "GenkiAuraHalo_Procedural";
            cachedHaloSprite.hideFlags = HideFlags.DontSave;
            return cachedHaloSprite;
        }

        private static Sprite GetOrCreateShockwaveRingSprite()
        {
            if (cachedRingSprite != null && cachedRingTexture != null) return cachedRingSprite;

            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxRadius = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxRadius;
                    float dy = (y - center) / maxRadius;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float ringPeak = Mathf.Exp(-Mathf.Pow((r - 0.87f) / 0.075f, 2f));
                    float innerTrail = r < 0.87f ? Mathf.Pow(r / 0.87f, 3f) * 0.32f : 0f;
                    float outerEdge = r > 0.94f ? Mathf.Clamp01(1f - (r - 0.94f) / 0.06f) : 1f;
                    float alpha = Mathf.Clamp01(Mathf.Max(ringPeak, innerTrail) * outerEdge);

                    Color col = Color.Lerp(new Color(0.45f, 0.90f, 1f, 1f), Color.white, ringPeak);
                    col.a = alpha;
                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            cachedRingTexture = tex;
            cachedRingSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            cachedRingSprite.name = "GenkiShockwaveRing_Procedural";
            cachedRingSprite.hideFlags = HideFlags.DontSave;
            return cachedRingSprite;
        }

        private static Sprite GetOrCreateCoreBurstSprite()
        {
            if (cachedCoreBurstSprite != null && cachedCoreBurstTexture != null) return cachedCoreBurstSprite;

            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxRadius = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxRadius;
                    float dy = (y - center) / maxRadius;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float alpha = Mathf.Pow(1f - r, 1.75f);
                    Color col = Color.Lerp(Color.white, new Color(0.42f, 0.90f, 1f, 1f), Mathf.Clamp01(r * 1.15f));
                    col.a = alpha;
                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            cachedCoreBurstTexture = tex;
            cachedCoreBurstSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            cachedCoreBurstSprite.name = "GenkiCoreBurst_Procedural";
            cachedCoreBurstSprite.hideFlags = HideFlags.DontSave;
            return cachedCoreBurstSprite;
        }

        private sealed class FullScreenFlashRunner : MonoBehaviour
        {
            public void Play(Image targetImage, Color baseColor)
            {
                StartCoroutine(FlashRoutine(targetImage, baseColor));
            }

            private IEnumerator FlashRoutine(Image img, Color baseColor)
            {
                if (img == null)
                {
                    Destroy(gameObject);
                    yield break;
                }

                const float peakAlpha = 0.85f;
                const float rampUpDuration = 0.10f;
                const float holdDuration = 0.04f;
                const float fadeOutDuration = 0.60f;

                Color c = baseColor;
                c.a = 0f;
                img.color = c;

                float elapsed = 0f;
                while (elapsed < rampUpDuration)
                {
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    float t = Mathf.Clamp01(elapsed / rampUpDuration);
                    c.a = Mathf.Lerp(0f, peakAlpha, t);
                    if (img != null) img.color = c;
                    yield return null;
                }

                c.a = peakAlpha;
                if (img != null) img.color = c;

                float holdElapsed = 0f;
                while (holdElapsed < holdDuration)
                {
                    holdElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    yield return null;
                }

                elapsed = 0f;
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                    float eased = 1f - Mathf.Pow(1f - t, 2f);
                    c.a = Mathf.Lerp(peakAlpha, 0f, eased);
                    if (img != null) img.color = c;
                    yield return null;
                }

                Destroy(gameObject);
            }
        }

        private sealed class GenkiShockwaveRunner : MonoBehaviour
        {
            public void Play(
                SpriteRenderer coreSr,
                SpriteRenderer outerRingSr,
                SpriteRenderer innerRingSr,
                float startDiameter,
                float endDiameter)
            {
                StartCoroutine(ShockwaveRoutine(coreSr, outerRingSr, innerRingSr, startDiameter, endDiameter));
            }

            private IEnumerator ShockwaveRoutine(
                SpriteRenderer coreSr,
                SpriteRenderer outerRingSr,
                SpriteRenderer innerRingSr,
                float startDiameter,
                float endDiameter)
            {
                const float duration = 0.48f;

                float coreSpriteDiam = MeasureSpriteDiameter(coreSr);
                float outerSpriteDiam = MeasureSpriteDiameter(outerRingSr);
                float innerSpriteDiam = MeasureSpriteDiameter(innerRingSr);

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float easeOut = 1f - Mathf.Pow(1f - t, 2.5f);

                    if (coreSr != null)
                    {
                        float coreDiam = Mathf.Lerp(startDiameter * 0.85f, endDiameter * 0.72f, easeOut);
                        coreSr.transform.localScale = Vector3.one * (coreDiam / coreSpriteDiam);
                        float coreAlpha = t < 0.2f ? 0.95f : Mathf.Lerp(0.95f, 0f, (t - 0.2f) / 0.8f);
                        coreSr.color = new Color(0.88f, 0.98f, 1f, coreAlpha);
                    }

                    if (outerRingSr != null)
                    {
                        float outerDiam = Mathf.Lerp(startDiameter * 0.45f, endDiameter, easeOut);
                        outerRingSr.transform.localScale = Vector3.one * (outerDiam / outerSpriteDiam);
                        float outerAlpha = t < 0.35f ? 0.96f : Mathf.Lerp(0.96f, 0f, (t - 0.35f) / 0.65f);
                        outerRingSr.color = new Color(0.75f, 0.95f, 1f, outerAlpha);
                    }

                    if (innerRingSr != null)
                    {
                        float innerEase = Mathf.Pow(t, 0.78f);
                        float innerDiam = Mathf.Lerp(startDiameter * 0.25f, endDiameter * 0.82f, innerEase);
                        innerRingSr.transform.localScale = Vector3.one * (innerDiam / innerSpriteDiam);
                        float innerAlpha = Mathf.Lerp(0.90f, 0f, t);
                        innerRingSr.color = new Color(1f, 1f, 1f, innerAlpha);
                    }

                    yield return null;
                }

                Destroy(gameObject);
            }

            private static float MeasureSpriteDiameter(SpriteRenderer sr)
            {
                if (sr == null || sr.sprite == null) return 1f;
                Bounds b = sr.sprite.bounds;
                return Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y));
            }
        }
    }
}
