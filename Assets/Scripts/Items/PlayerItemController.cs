using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Characters;

namespace StickmanBeatEmUp.Items
{
    /// <summary>
    /// Điều khiển việc sử dụng vật phẩm qua phím Q (Slot 0) và E (Slot 1) trên nhân vật Player.
    /// Tự động tạo các object VFX con (VFX_SenzuHeal, VFX_SpinalRibcage, VFX_UltraWaterAura) nếu prefab chưa gắn sẵn.
    /// </summary>
    public class PlayerItemController : MonoBehaviour
    {
        private const float SENZU_VFX_DURATION = 0.8f;

        [Header("Core References")]
        [SerializeField] private BaseCharacter baseCharacter;
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private SpriteRenderer playerSpriteRenderer;

        [Header("Optional Pre-Wired VFX Animators")]
        [SerializeField] private ItemVfxAnimator senzuVfxAnimator;
        [SerializeField] private ItemVfxAnimator spinalRibcageVfxAnimator;
        [SerializeField] private ItemVfxAnimator ultraWaterVfxAnimator;

        private bool spinalBuffActive;
        private float spinalBuffRemainingTime;
        private bool ultraWaterBuffActive;
        private float ultraWaterBuffRemainingTime;

        private Coroutine healFlashRoutine;

        // Cached optional reflection members for early ribcage hide when BaseCharacter shield hits 0
        private static bool reflectionInitialized;
        private static PropertyInfo virtualShieldProperty;
        private static FieldInfo virtualShieldField;
        private static PropertyInfo spinalActiveProperty;
        private static FieldInfo spinalActiveField;
        private static Sprite cachedHealParticleSprite;

        public float SpinalBuffRemainingTime => spinalBuffRemainingTime;
        public float UltraWaterBuffRemainingTime => ultraWaterBuffRemainingTime;
        public bool IsSpinalBuffActive => spinalBuffActive;
        public bool IsUltraWaterBuffActive => ultraWaterBuffActive;

        /// <summary>
        /// Phát ra khi kích hoạt thành công vật phẩm tại slotIndex (0 = Q, 1 = E).
        /// </summary>
        public event Action<int, ConsumableItemType> OnItemActivated;

        /// <summary>
        /// Phát ra khi bấm phím vật phẩm nhưng ô bị khóa hoặc hết số lượng.
        /// </summary>
        public event Action<int> OnItemUseFailed;

        /// <summary>
        /// Đảm bảo Player luôn có sẵn component PlayerItemController gắn kèm BaseCharacter.
        /// </summary>
        public static PlayerItemController EnsureAttached(BaseCharacter character)
        {
            if (character == null) return null;

            PlayerItemController existing = character.GetComponent<PlayerItemController>();
            if (existing != null) return existing;

            return character.gameObject.AddComponent<PlayerItemController>();
        }

        private void Awake()
        {
            ResolveCoreReferences();
            InitShieldReflectionCache();
            EnsureVfxChildren();
        }

        private void OnEnable()
        {
            ResolveCoreReferences();
            if (inputHandler != null)
            {
                inputHandler.OnItemQEvent -= HandleItemQInput;
                inputHandler.OnItemEEvent -= HandleItemEInput;
                inputHandler.OnItemQEvent += HandleItemQInput;
                inputHandler.OnItemEEvent += HandleItemEInput;
            }
        }

        private void OnDisable()
        {
            if (inputHandler != null)
            {
                inputHandler.OnItemQEvent -= HandleItemQInput;
                inputHandler.OnItemEEvent -= HandleItemEInput;
            }
        }

        private void Update()
        {
            if (baseCharacter != null && baseCharacter.IsDead)
            {
                StopAllBuffVfx();
                return;
            }

            if (spinalBuffActive)
            {
                spinalBuffRemainingTime -= Time.deltaTime;
                if (spinalBuffRemainingTime <= 0f || HasSpinalShieldDepleted())
                {
                    StopSpinalRibcageVfx();
                }
                else if (spinalRibcageVfxAnimator != null && playerSpriteRenderer != null)
                {
                    spinalRibcageVfxAnimator.SyncSortingOrder(playerSpriteRenderer, 2);
                }
            }

            if (ultraWaterBuffActive)
            {
                ultraWaterBuffRemainingTime -= Time.deltaTime;
                if (ultraWaterBuffRemainingTime <= 0f)
                {
                    StopUltraWaterVfx();
                }
                else if (ultraWaterVfxAnimator != null && playerSpriteRenderer != null)
                {
                    ultraWaterVfxAnimator.SyncSortingOrder(playerSpriteRenderer, -1);
                }
            }
        }

        public bool TryUseSlotQ() => TryUseSlot(InventoryManager.SLOT_Q);
        public bool TryUseSlotE() => TryUseSlot(InventoryManager.SLOT_E);

        /// <summary>
        /// Thử tiêu thụ và kích hoạt hiệu ứng vật phẩm ở ô Q (0) hoặc E (1).
        /// </summary>
        public bool TryUseSlot(int slotIndex)
        {
            if (baseCharacter == null || baseCharacter.IsDead || baseCharacter.IsBlocking || baseCharacter.CurrentState == CharacterState.Block)
            {
                return false;
            }

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                OnItemUseFailed?.Invoke(slotIndex);
                return false;
            }

            if (!inventory.TryConsumeSlot(slotIndex, out ConsumableItemType usedType))
            {
                OnItemUseFailed?.Invoke(slotIndex);
                return false;
            }

            ConsumableItemSO data = inventory.GetItemData(usedType);
            if (data == null)
            {
                data = ConsumableItemSO.CreateRuntimeDefault(usedType);
            }

            ExecuteItemEffect(usedType, data);
            OnItemActivated?.Invoke(slotIndex, usedType);
            return true;
        }

        private void HandleItemQInput()
        {
            TryUseSlot(InventoryManager.SLOT_Q);
        }

        private void HandleItemEInput()
        {
            TryUseSlot(InventoryManager.SLOT_E);
        }

        private void ExecuteItemEffect(ConsumableItemType itemType, ConsumableItemSO data)
        {
            EnsureVfxChildren();

            switch (itemType)
            {
                case ConsumableItemType.SenzuBean:
                    baseCharacter.Heal(data.healAmount);
                    baseCharacter.CleanseDebuffs();
                    PlaySenzuHealVfx(data);
                    break;

                case ConsumableItemType.SpinalFluid:
                    float spinalDuration = data.buffDuration > 0f ? data.buffDuration : 30f;
                    baseCharacter.ActivateSpinalFluidBuff(spinalDuration, data.shieldAmount, data.reflectDamage);
                    PlaySpinalRibcageVfx(data, spinalDuration);
                    break;

                case ConsumableItemType.UltraDivineWater:
                    float waterDuration = data.buffDuration > 0f ? data.buffDuration : 60f;
                    baseCharacter.ActivateUltraWaterBuff(
                        waterDuration,
                        data.damageMultiplier,
                        data.speedMultiplier,
                        data.ragePerSecond);
                    PlayUltraWaterVfx(data, waterDuration);
                    break;
            }
        }

        private void PlaySenzuHealVfx(ConsumableItemSO data)
        {
            if (senzuVfxAnimator != null)
            {
                Sprite[] frames = ResolveVfxFrames(ConsumableItemType.SenzuBean, data);
                float fps = data != null && data.vfxFps > 0f ? data.vfxFps : 8f;
                senzuVfxAnimator.Configure(
                    frames,
                    fps,
                    3,
                    playerSpriteRenderer,
                    new Color(0.45f, 1f, 0.58f, 0.92f));
                senzuVfxAnimator.PlayOneShot(SENZU_VFX_DURATION);
            }

            if (healFlashRoutine != null)
            {
                StopCoroutine(healFlashRoutine);
            }
            healFlashRoutine = StartCoroutine(PlayGreenHealFlashRoutine());
            SpawnSenzuHealParticles();
        }

        private void PlaySpinalRibcageVfx(ConsumableItemSO data, float duration)
        {
            spinalBuffActive = true;
            spinalBuffRemainingTime = duration;

            if (spinalRibcageVfxAnimator != null)
            {
                Sprite[] frames = ResolveVfxFrames(ConsumableItemType.SpinalFluid, data);
                float fps = data != null && data.vfxFps > 0f ? data.vfxFps : 8f;
                spinalRibcageVfxAnimator.Configure(
                    frames,
                    fps,
                    2,
                    playerSpriteRenderer,
                    new Color(1f, 0.96f, 0.88f, 0.68f));
                spinalRibcageVfxAnimator.PlayLoop(duration);
            }
        }

        private void PlayUltraWaterVfx(ConsumableItemSO data, float duration)
        {
            ultraWaterBuffActive = true;
            ultraWaterBuffRemainingTime = duration;

            if (ultraWaterVfxAnimator != null)
            {
                Sprite[] frames = ResolveVfxFrames(ConsumableItemType.UltraDivineWater, data);
                float fps = data != null && data.vfxFps > 0f ? data.vfxFps : 8f;
                ultraWaterVfxAnimator.Configure(
                    frames,
                    fps,
                    -1,
                    playerSpriteRenderer,
                    new Color(1f, 1f, 1f, 0.72f));
                ultraWaterVfxAnimator.PlayLoop(duration);
            }
        }

        private void StopSpinalRibcageVfx()
        {
            spinalBuffActive = false;
            spinalBuffRemainingTime = 0f;
            if (spinalRibcageVfxAnimator != null)
            {
                spinalRibcageVfxAnimator.StopAndHide();
            }
        }

        private void StopUltraWaterVfx()
        {
            ultraWaterBuffActive = false;
            ultraWaterBuffRemainingTime = 0f;
            if (ultraWaterVfxAnimator != null)
            {
                ultraWaterVfxAnimator.StopAndHide();
            }
        }

        private void StopAllBuffVfx()
        {
            StopSpinalRibcageVfx();
            StopUltraWaterVfx();
        }

        private IEnumerator PlayGreenHealFlashRoutine()
        {
            if (playerSpriteRenderer == null) yield break;

            Color emeraldTint = new Color(0.45f, 1f, 0.55f, 1f);
            float elapsed = 0f;
            const float duration = 0.25f;

            while (elapsed < duration)
            {
                if (baseCharacter == null || baseCharacter.IsDead)
                {
                    yield break;
                }

                if (baseCharacter.CurrentState != CharacterState.Hurt)
                {
                    float t = elapsed / duration;
                    playerSpriteRenderer.color = Color.Lerp(emeraldTint, Color.white, t);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (baseCharacter != null && !baseCharacter.IsDead && baseCharacter.CurrentState != CharacterState.Hurt)
            {
                playerSpriteRenderer.color = Color.white;
            }

            healFlashRoutine = null;
        }

        private void SpawnSenzuHealParticles()
        {
            Sprite particleSprite = GetHealParticleSprite();
            if (particleSprite == null) return;

            int baseOrder = playerSpriteRenderer != null ? playerSpriteRenderer.sortingOrder + 4 : 9;
            int layerId = playerSpriteRenderer != null ? playerSpriteRenderer.sortingLayerID : 0;

            for (int i = 0; i < 6; i++)
            {
                GameObject pObj = new GameObject("VFX_HealParticle");
                pObj.transform.position = transform.position + new Vector3(
                    UnityEngine.Random.Range(-0.45f, 0.45f),
                    UnityEngine.Random.Range(0.25f, 0.95f),
                    0f);

                float scale = UnityEngine.Random.Range(0.10f, 0.18f);
                pObj.transform.localScale = new Vector3(scale, scale, 1f);

                SpriteRenderer sr = pObj.AddComponent<SpriteRenderer>();
                sr.sprite = particleSprite;
                sr.sortingLayerID = layerId;
                sr.sortingOrder = baseOrder;
                sr.color = new Color(0.32f, 1f, 0.52f, 0.95f);

                StartCoroutine(AnimateHealParticleRoutine(pObj, sr, UnityEngine.Random.Range(0.45f, 0.72f)));
            }
        }

        private static IEnumerator AnimateHealParticleRoutine(GameObject particleObj, SpriteRenderer sr, float lifetime)
        {
            float elapsed = 0f;
            float speedY = UnityEngine.Random.Range(1.2f, 2.0f);
            float driftX = UnityEngine.Random.Range(-0.25f, 0.25f);
            Color startColor = sr != null ? sr.color : Color.green;

            while (elapsed < lifetime && particleObj != null)
            {
                float dt = Time.deltaTime;
                particleObj.transform.position += new Vector3(driftX * dt, speedY * dt, 0f);
                if (sr != null)
                {
                    Color c = startColor;
                    c.a = Mathf.Lerp(startColor.a, 0f, elapsed / lifetime);
                    sr.color = c;
                }

                elapsed += dt;
                yield return null;
            }

            if (particleObj != null)
            {
                Destroy(particleObj);
            }
        }

        private void EnsureVfxChildren()
        {
            InventoryManager inventory = InventoryManager.Instance;

            if (senzuVfxAnimator == null)
            {
                ConsumableItemSO senzu = inventory != null ? inventory.GetItemData(ConsumableItemType.SenzuBean) : null;
                senzuVfxAnimator = FindOrCreateChildVfx(
                    "VFX_SenzuHeal",
                    new Vector3(0f, 0.85f, 0f),
                    new Vector3(1.3f, 1.3f, 1f),
                    3,
                    ResolveVfxFrames(ConsumableItemType.SenzuBean, senzu),
                    senzu != null ? senzu.vfxFps : 8f,
                    new Color(0.45f, 1f, 0.58f, 0.92f));
            }

            if (spinalRibcageVfxAnimator == null)
            {
                ConsumableItemSO spinal = inventory != null ? inventory.GetItemData(ConsumableItemType.SpinalFluid) : null;
                spinalRibcageVfxAnimator = FindOrCreateChildVfx(
                    "VFX_SpinalRibcage",
                    new Vector3(0f, 0.85f, 0f),
                    new Vector3(1.35f, 1.35f, 1f),
                    2,
                    ResolveVfxFrames(ConsumableItemType.SpinalFluid, spinal),
                    spinal != null ? spinal.vfxFps : 8f,
                    new Color(1f, 0.96f, 0.88f, 0.68f));
            }

            if (ultraWaterVfxAnimator == null)
            {
                ConsumableItemSO water = inventory != null ? inventory.GetItemData(ConsumableItemType.UltraDivineWater) : null;
                ultraWaterVfxAnimator = FindOrCreateChildVfx(
                    "VFX_UltraWaterAura",
                    new Vector3(0f, 0.8f, 0f),
                    new Vector3(1.45f, 1.45f, 1f),
                    -1,
                    ResolveVfxFrames(ConsumableItemType.UltraDivineWater, water),
                    water != null ? water.vfxFps : 8f,
                    new Color(1f, 1f, 1f, 0.72f));
            }
        }

        private ItemVfxAnimator FindOrCreateChildVfx(
            string childName,
            Vector3 localPos,
            Vector3 localScale,
            int sortingOffset,
            Sprite[] frames,
            float fps,
            Color tintColor)
        {
            Transform existing = transform.Find(childName);
            GameObject childObj;
            if (existing != null)
            {
                childObj = existing.gameObject;
            }
            else
            {
                childObj = new GameObject(childName);
                childObj.transform.SetParent(transform, false);
                childObj.transform.localPosition = localPos;
                childObj.transform.localScale = localScale;
            }

            ItemVfxAnimator vfx = childObj.GetComponent<ItemVfxAnimator>();
            if (vfx == null)
            {
                vfx = childObj.AddComponent<ItemVfxAnimator>();
            }

            vfx.Configure(frames, fps, sortingOffset, playerSpriteRenderer, tintColor);
            childObj.SetActive(false);
            return vfx;
        }

        private static Sprite[] ResolveVfxFrames(ConsumableItemType type, ConsumableItemSO data)
        {
            if (data != null && data.vfxFrames != null && data.vfxFrames.Length > 0)
            {
                return data.vfxFrames;
            }

            if (data != null)
            {
                ConsumableItemSO.PopulateDefaultSpritesInEditor(data);
                if (data.vfxFrames != null && data.vfxFrames.Length > 0)
                {
                    return data.vfxFrames;
                }
            }

            ConsumableItemSO fallback = ConsumableItemSO.CreateRuntimeDefault(type);
            return fallback.vfxFrames;
        }

        private void ResolveCoreReferences()
        {
            if (baseCharacter == null)
            {
                baseCharacter = GetComponent<BaseCharacter>();
            }

            if (inputHandler == null)
            {
                inputHandler = GetComponent<InputHandler>();
            }

            if (playerSpriteRenderer == null && baseCharacter != null)
            {
                playerSpriteRenderer = baseCharacter.GetComponentInChildren<SpriteRenderer>();
            }
        }

        private bool HasSpinalShieldDepleted()
        {
            if (baseCharacter == null) return true;

            if (spinalActiveProperty != null)
            {
                object val = spinalActiveProperty.GetValue(baseCharacter);
                if (val is bool active && !active) return true;
            }
            else if (spinalActiveField != null)
            {
                object val = spinalActiveField.GetValue(baseCharacter);
                if (val is bool active && !active) return true;
            }

            if (virtualShieldProperty != null)
            {
                object val = virtualShieldProperty.GetValue(baseCharacter);
                if (val is float shield && shield <= 0f) return true;
            }
            else if (virtualShieldField != null)
            {
                object val = virtualShieldField.GetValue(baseCharacter);
                if (val is float shield && shield <= 0f) return true;
            }

            return false;
        }

        private static void InitShieldReflectionCache()
        {
            if (reflectionInitialized) return;
            reflectionInitialized = true;

            Type charType = typeof(BaseCharacter);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            string[] shieldPropNames = { "VirtualShieldHp", "VirtualShieldHP", "SpinalShieldHp", "CurrentShieldHp", "ShieldHp", "VirtualShield" };
            for (int i = 0; i < shieldPropNames.Length; i++)
            {
                PropertyInfo p = charType.GetProperty(shieldPropNames[i], flags);
                if (p != null && p.PropertyType == typeof(float))
                {
                    virtualShieldProperty = p;
                    break;
                }
            }

            if (virtualShieldProperty == null)
            {
                string[] shieldFieldNames = { "virtualShieldHp", "virtualShieldHP", "spinalShieldHp", "currentShieldHp", "shieldHp" };
                for (int i = 0; i < shieldFieldNames.Length; i++)
                {
                    FieldInfo f = charType.GetField(shieldFieldNames[i], flags);
                    if (f != null && f.FieldType == typeof(float))
                    {
                        virtualShieldField = f;
                        break;
                    }
                }
            }

            string[] activePropNames = { "HasSpinalFluidBuff", "IsSpinalFluidActive", "IsSpinalBuffActive" };
            for (int i = 0; i < activePropNames.Length; i++)
            {
                PropertyInfo p = charType.GetProperty(activePropNames[i], flags);
                if (p != null && p.PropertyType == typeof(bool))
                {
                    spinalActiveProperty = p;
                    break;
                }
            }

            if (spinalActiveProperty == null)
            {
                string[] activeFieldNames = { "hasSpinalFluidBuff", "isSpinalFluidActive", "isSpinalBuffActive" };
                for (int i = 0; i < activeFieldNames.Length; i++)
                {
                    FieldInfo f = charType.GetField(activeFieldNames[i], flags);
                    if (f != null && f.FieldType == typeof(bool))
                    {
                        spinalActiveField = f;
                        break;
                    }
                }
            }
        }

        private static Sprite GetHealParticleSprite()
        {
            if (cachedHealParticleSprite != null) return cachedHealParticleSprite;

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    bool corner = (x == 0 || x == 3) && (y == 0 || y == 3);
                    pixels[y * 4 + x] = corner ? new Color(1f, 1f, 1f, 0.25f) : Color.white;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            cachedHealParticleSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 16f);
            return cachedHealParticleSprite;
        }
    }
}
