using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StickmanBeatEmUp.Core;
using StickmanBeatEmUp.Characters;
using StickmanBeatEmUp.CorePure.Entities.Combat;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Interfaces;
using StickmanBeatEmUp.CorePure.Patterns.Decorators;
using StickmanBeatEmUp.CorePure.Patterns.Items;

namespace StickmanBeatEmUp.Items
{
    /// <summary>
    /// Điều khiển vật phẩm tiêu hao qua phím Q (Slot 0) và E (Slot 1) trên nhân vật Player.
    /// Áp dụng Strategy Pattern (ItemEffectRegistry / IItemEffect) và GoF Decorator (BuffStackResolver -> BaseCharacter.SetStatSource).
    /// </summary>
    public class PlayerItemController : MonoBehaviour
    {
        private const float SENZU_VFX_DURATION = 0.8f;
        public const string BUFF_ID_SPINAL = "SpinalFluid";
        public const string BUFF_ID_ULTRA_WATER = "UltraDivineWater";
        public const string BUFF_ID_RAGE_ELIXIR = "RageElixir";

        [Header("Core References")]
        [SerializeField] private BaseCharacter baseCharacter;
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private SpriteRenderer playerSpriteRenderer;

        [Header("Optional Pre-Wired VFX Animators")]
        [SerializeField] private ItemVfxAnimator senzuVfxAnimator;
        [SerializeField] private ItemVfxAnimator spinalRibcageVfxAnimator;
        [SerializeField] private ItemVfxAnimator ultraWaterVfxAnimator;
        [SerializeField] private ItemVfxAnimator rageElixirVfxAnimator;

        private readonly BuffStackResolver buffResolver = new BuffStackResolver();
        private readonly ItemEffectRegistry effectRegistry = new ItemEffectRegistry();
        private readonly Dictionary<ConsumableItemType, Action<ConsumableItemSO, ItemEffectResult>> vfxHandlers =
            new Dictionary<ConsumableItemType, Action<ConsumableItemSO, ItemEffectResult>>();

        private Coroutine healFlashRoutine;
        private int lastKnownActiveBuffCount;
        private static Sprite cachedHealParticleSprite;

        public BuffStackResolver BuffResolver => buffResolver;
        public ItemEffectRegistry EffectRegistry => effectRegistry;

        public float SpinalBuffRemainingTime => buffResolver.GetRemainingSeconds(BUFF_ID_SPINAL);
        public float UltraWaterBuffRemainingTime => buffResolver.GetRemainingSeconds(BUFF_ID_ULTRA_WATER);
        public float RageElixirBuffRemainingTime => buffResolver.GetRemainingSeconds(BUFF_ID_RAGE_ELIXIR);
        public bool IsSpinalBuffActive => buffResolver.HasBuff(BUFF_ID_SPINAL);
        public bool IsUltraWaterBuffActive => buffResolver.HasBuff(BUFF_ID_ULTRA_WATER);
        public bool IsRageElixirBuffActive => buffResolver.HasBuff(BUFF_ID_RAGE_ELIXIR);

        /// <summary>
        /// Phát ra khi kích hoạt thành công vật phẩm tại slotIndex (0 = Q, 1 = E).
        /// </summary>
        public event Action<int, ConsumableItemType> OnItemActivated;

        /// <summary>
        /// Phát ra khi bấm phím vật phẩm nhưng ô bị khóa hoặc hết số lượng.
        /// </summary>
        public event Action<int> OnItemUseFailed;

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
            RegisterDefaultStrategiesAndVfx();
            EnsureVfxChildren();
            RebuildStatSourceChain();
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
                if (buffResolver.ActiveCount > 0)
                {
                    buffResolver.ClearAll();
                    baseCharacter.ResetStatSource();
                    lastKnownActiveBuffCount = 0;
                }
                StopAllBuffVfx();
                return;
            }

            if (buffResolver.ActiveCount > 0)
            {
                if (buffResolver.HasBuff(BUFF_ID_SPINAL) && baseCharacter != null)
                {
                    buffResolver.UpdateShield(BUFF_ID_SPINAL, baseCharacter.VirtualShieldHP);
                }

                buffResolver.Tick(Time.deltaTime);

                if (buffResolver.ActiveCount != lastKnownActiveBuffCount)
                {
                    RebuildStatSourceChain();
                }

                SyncBuffVfxWithResolver();
            }
        }

        public bool TryUseSlotQ() => TryUseSlot(InventoryManager.SLOT_Q);
        public bool TryUseSlotE() => TryUseSlot(InventoryManager.SLOT_E);

        /// <summary>
        /// Thử tiêu thụ và kích hoạt hiệu ứng vật phẩm ở ô Q (0) hoặc E (1) qua ItemEffectRegistry (OCP).
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

            ConsumableItemType candidateType = inventory.GetEquippedItem(slotIndex);
            if (candidateType == ConsumableItemType.None)
            {
                OnItemUseFailed?.Invoke(slotIndex);
                return false;
            }

            ConsumableItemSO data = inventory.GetItemData(candidateType) ?? ConsumableItemSO.CreateRuntimeDefault(candidateType);
            SyncStrategyFromData(data);

            int effectiveOwned = inventory.IsTrainingFreeUseMode ? 1 : inventory.GetOwnedCount(candidateType);
            ItemUseContext context = BuildItemUseContext(effectiveOwned);
            ItemEffectResult previewResult = effectRegistry.Execute((int)candidateType, context);
            if (!previewResult.Applied)
            {
                OnItemUseFailed?.Invoke(slotIndex);
                return false;
            }

            if (!inventory.TryConsumeSlot(slotIndex, out ConsumableItemType usedType))
            {
                OnItemUseFailed?.Invoke(slotIndex);
                return false;
            }

            ApplyEffectResultToCharacter(usedType, data, previewResult);
            OnItemActivated?.Invoke(slotIndex, usedType);
            return true;
        }

        /// <summary>
        /// Cho phép đăng ký thêm IItemEffect mới tại runtime (Live OCP) mà không sửa PlayerItemController.
        /// </summary>
        public void RegisterCustomEffect(IItemEffect effect, Action<ConsumableItemSO, ItemEffectResult> vfxCallback = null)
        {
            if (effect == null) return;
            effectRegistry.Register(effect);
            if (vfxCallback != null && Enum.IsDefined(typeof(ConsumableItemType), effect.ItemTypeId))
            {
                vfxHandlers[(ConsumableItemType)effect.ItemTypeId] = vfxCallback;
            }
        }

        /// <summary>
        /// Xuất snapshot toàn bộ buff đang chạy phục vụ Save/Load giữa trận (Nhánh 2).
        /// </summary>
        public List<BuffSnapshotDTO> ExportActiveBuffSnapshots()
        {
            return buffResolver.ExportSnapshots();
        }

        /// <summary>
        /// Khôi phục lại chuỗi Decorator và trạng thái buff từ GameStateDTO khi Load trận.
        /// </summary>
        public void RestoreActiveBuffSnapshots(IReadOnlyList<BuffSnapshotDTO> snapshots, float savedVirtualShield)
        {
            if (baseCharacter == null) return;

            buffResolver.RestoreSnapshots(
                snapshots,
                ResolveDecoratorFactoryFromSnapshot,
                buffId => string.Equals(buffId, BUFF_ID_SPINAL, StringComparison.OrdinalIgnoreCase));

            if (buffResolver.HasBuff(BUFF_ID_SPINAL))
            {
                float shield = savedVirtualShield > 0f ? savedVirtualShield : 40f;
                buffResolver.UpdateShield(BUFF_ID_SPINAL, shield);
                // Truyền reflectDamage = 0f để phản đòn chỉ tính duy nhất 1 lần từ SpinalFluidShieldDecorator.
                baseCharacter.ActivateSpinalFluidBuff(buffResolver.GetRemainingSeconds(BUFF_ID_SPINAL), shield, 0f);
            }

            if (buffResolver.HasBuff(BUFF_ID_ULTRA_WATER))
            {
                // Truyền giá trị trung tính (1f, 1f, 0f) để không cộng dồn kép với UltraDivineWaterDecorator.
                baseCharacter.ActivateUltraWaterBuff(buffResolver.GetRemainingSeconds(BUFF_ID_ULTRA_WATER), 1f, 1f, 0f);
            }

            RebuildStatSourceChain();
            SyncBuffVfxWithResolver();
        }

        private void HandleItemQInput()
        {
            TryUseSlot(InventoryManager.SLOT_Q);
        }

        private void HandleItemEInput()
        {
            TryUseSlot(InventoryManager.SLOT_E);
        }

        private void RegisterDefaultStrategiesAndVfx()
        {
            effectRegistry.Register(new SenzuBeanItemEffect());
            effectRegistry.Register(new SpinalFluidItemEffect());
            effectRegistry.Register(new UltraDivineWaterItemEffect());
            effectRegistry.Register(new RageElixirItemEffect());

            vfxHandlers[ConsumableItemType.SenzuBean] = (data, _) => PlaySenzuHealVfx(data);
            vfxHandlers[ConsumableItemType.SpinalFluid] = (data, res) => PlaySpinalRibcageVfx(data, res.BuffDuration);
            vfxHandlers[ConsumableItemType.UltraDivineWater] = (data, res) => PlayUltraWaterVfx(data, res.BuffDuration);
            vfxHandlers[ConsumableItemType.RageElixir] = (data, res) => PlayRageElixirVfx(data, res.BuffDuration);
        }

        private void SyncStrategyFromData(ConsumableItemSO data)
        {
            if (data == null) return;

            if (data.itemType == ConsumableItemType.SenzuBean)
            {
                effectRegistry.Register(new SenzuBeanItemEffect(data.healAmount));
            }
            else if (data.itemType == ConsumableItemType.SpinalFluid)
            {
                effectRegistry.Register(new SpinalFluidItemEffect(data.buffDuration, data.shieldAmount, data.reflectDamage));
            }
            else if (data.itemType == ConsumableItemType.UltraDivineWater)
            {
                effectRegistry.Register(new UltraDivineWaterItemEffect(data.buffDuration, data.damageMultiplier, data.speedMultiplier, data.ragePerSecond));
            }
            else if (data.itemType == ConsumableItemType.RageElixir)
            {
                float instantRage = data.instantRageAmount > 0f ? data.instantRageAmount : 5f;
                effectRegistry.Register(new RageElixirItemEffect(instantRage, data.buffDuration, data.ragePerSecond));
            }
        }

        private ItemUseContext BuildItemUseContext(int quantityOwned)
        {
            return new ItemUseContext
            {
                CurrentHp = baseCharacter != null ? baseCharacter.CurrentHealth : 0f,
                MaxHp = baseCharacter != null ? baseCharacter.MaxHealth : 100f,
                CurrentRage = baseCharacter != null && baseCharacter.RageManager != null
                    ? baseCharacter.RageManager.CurrentRageFloat
                    : 0f,
                MaxRage = RageManager.MAX_RAGE,
                QuantityOwned = quantityOwned,
                IsPlayerDead = baseCharacter == null || baseCharacter.IsDead,
                HasActiveSpinalBuff = IsSpinalBuffActive,
                HasActiveUltraWaterBuff = IsUltraWaterBuffActive
            };
        }

        private void ApplyEffectResultToCharacter(ConsumableItemType itemType, ConsumableItemSO data, ItemEffectResult result)
        {
            EnsureVfxChildren();

            if (result.HealAmount > 0f)
            {
                baseCharacter.Heal(result.HealAmount);
            }

            if (result.ClearNegativeEffects)
            {
                baseCharacter.CleanseDebuffs();
            }

            if (result.RageDelta > 0f && baseCharacter.RageManager != null)
            {
                baseCharacter.RageManager.AddRageFloat(result.RageDelta);
            }

            if (result.BuffDuration > 0f)
            {
                if (result.ShieldAmount > 0f || result.ReflectDamage > 0f)
                {
                    float shieldCap = result.ShieldAmount;
                    float reflectDmg = result.ReflectDamage;
                    buffResolver.ApplyOrRefresh(
                        BUFF_ID_SPINAL,
                        result.BuffDuration,
                        shieldCap,
                        true,
                        inner => new SpinalFluidShieldDecorator(inner, shieldCap, reflectDmg));

                    // QUAN TRỌNG: truyền reflectDamage = 0f để tránh cộng dồn kép với SpinalFluidShieldDecorator!
                    baseCharacter.ActivateSpinalFluidBuff(result.BuffDuration, shieldCap, 0f);
                }
                else if (result.DamageMultiplier > 1.001f || result.SpeedMultiplier > 1.001f)
                {
                    float dmgMult = result.DamageMultiplier;
                    float spdMult = result.SpeedMultiplier;
                    float rps = result.RagePerSecond;
                    buffResolver.ApplyOrRefresh(
                        BUFF_ID_ULTRA_WATER,
                        result.BuffDuration,
                        0f,
                        false,
                        inner => new UltraDivineWaterDecorator(inner, dmgMult, spdMult, rps));

                    // QUAN TRỌNG: truyền giá trị trung tính (1f, 1f, 0f) để không cộng dồn kép với UltraDivineWaterDecorator!
                    baseCharacter.ActivateUltraWaterBuff(result.BuffDuration, 1f, 1f, 0f);
                }
                else if (result.RagePerSecond > 0f)
                {
                    float rps = result.RagePerSecond;
                    buffResolver.ApplyOrRefresh(
                        BUFF_ID_RAGE_ELIXIR,
                        result.BuffDuration,
                        0f,
                        false,
                        inner => new RageRegenDecorator(inner, rps));
                }

                RebuildStatSourceChain();
            }

            if (vfxHandlers.TryGetValue(itemType, out Action<ConsumableItemSO, ItemEffectResult> handler) && handler != null)
            {
                handler(data, result);
            }
        }

        private Func<IStatSource, IStatSource> ResolveDecoratorFactoryFromSnapshot(BuffSnapshotDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.buffId)) return null;

            InventoryManager inv = InventoryManager.HasInstance ? InventoryManager.Instance : null;
            if (string.Equals(dto.buffId, BUFF_ID_SPINAL, StringComparison.OrdinalIgnoreCase))
            {
                ConsumableItemSO so = inv != null ? inv.GetItemData(ConsumableItemType.SpinalFluid) : null;
                float shield = dto.remainingShield > 0f ? dto.remainingShield : (so != null ? so.shieldAmount : 40f);
                float reflect = so != null ? so.reflectDamage : 5f;
                return inner => new SpinalFluidShieldDecorator(inner, shield, reflect);
            }

            if (string.Equals(dto.buffId, BUFF_ID_ULTRA_WATER, StringComparison.OrdinalIgnoreCase))
            {
                ConsumableItemSO so = inv != null ? inv.GetItemData(ConsumableItemType.UltraDivineWater) : null;
                float dmg = so != null ? so.damageMultiplier : 1.5f;
                float spd = so != null ? so.speedMultiplier : 1.3f;
                float rps = so != null ? so.ragePerSecond : 2f;
                return inner => new UltraDivineWaterDecorator(inner, dmg, spd, rps);
            }

            if (string.Equals(dto.buffId, BUFF_ID_RAGE_ELIXIR, StringComparison.OrdinalIgnoreCase))
            {
                ConsumableItemSO so = inv != null ? inv.GetItemData(ConsumableItemType.RageElixir) : null;
                float rps = so != null && so.ragePerSecond > 0f ? so.ragePerSecond : 3f;
                return inner => new RageRegenDecorator(inner, rps);
            }

            return null;
        }

        private void RebuildStatSourceChain()
        {
            if (baseCharacter == null) return;

            IStatSource chain = buffResolver.BuildStatChain(BaseStatSource.Default);
            baseCharacter.SetStatSource(chain);
            lastKnownActiveBuffCount = buffResolver.ActiveCount;
        }

        private void SyncBuffVfxWithResolver()
        {
            if (!buffResolver.HasBuff(BUFF_ID_SPINAL))
            {
                StopSpinalRibcageVfx();
            }
            else if (spinalRibcageVfxAnimator != null && playerSpriteRenderer != null)
            {
                spinalRibcageVfxAnimator.SyncSortingOrder(playerSpriteRenderer, 2);
            }

            if (!buffResolver.HasBuff(BUFF_ID_ULTRA_WATER))
            {
                StopUltraWaterVfx();
            }
            else if (ultraWaterVfxAnimator != null && playerSpriteRenderer != null)
            {
                ultraWaterVfxAnimator.SyncSortingOrder(playerSpriteRenderer, -1);
            }

            if (!buffResolver.HasBuff(BUFF_ID_RAGE_ELIXIR))
            {
                StopRageElixirVfx();
            }
            else if (rageElixirVfxAnimator != null && playerSpriteRenderer != null)
            {
                rageElixirVfxAnimator.SyncSortingOrder(playerSpriteRenderer, -1);
            }
        }

        private void PlaySenzuHealVfx(ConsumableItemSO data)
        {
            if (senzuVfxAnimator != null)
            {
                Sprite[] frames = ResolveVfxFrames(ConsumableItemType.SenzuBean, data);
                float fps = data != null && data.vfxFps > 0f ? data.vfxFps : 8f;
                senzuVfxAnimator.Configure(frames, fps, 3, playerSpriteRenderer, new Color(0.45f, 1f, 0.58f, 0.92f));
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
            if (spinalRibcageVfxAnimator != null)
            {
                Sprite[] frames = ResolveVfxFrames(ConsumableItemType.SpinalFluid, data);
                float fps = data != null && data.vfxFps > 0f ? data.vfxFps : 8f;
                spinalRibcageVfxAnimator.Configure(frames, fps, 2, playerSpriteRenderer, new Color(1f, 0.96f, 0.88f, 0.68f));
                spinalRibcageVfxAnimator.PlayLoop(duration);
            }
        }

        private void PlayUltraWaterVfx(ConsumableItemSO data, float duration)
        {
            if (ultraWaterVfxAnimator != null)
            {
                Sprite[] frames = ResolveVfxFrames(ConsumableItemType.UltraDivineWater, data);
                float fps = data != null && data.vfxFps > 0f ? data.vfxFps : 8f;
                ultraWaterVfxAnimator.Configure(frames, fps, -1, playerSpriteRenderer, new Color(1f, 1f, 1f, 0.72f));
                ultraWaterVfxAnimator.PlayLoop(duration);
            }
        }

        private void PlayRageElixirVfx(ConsumableItemSO data, float duration)
        {
            if (rageElixirVfxAnimator != null)
            {
                Sprite[] frames = ResolveVfxFrames(ConsumableItemType.RageElixir, data);
                float fps = data != null && data.vfxFps > 0f ? data.vfxFps : 8f;
                rageElixirVfxAnimator.Configure(frames, fps, -1, playerSpriteRenderer, new Color(1f, 0.52f, 0.18f, 0.72f));
                rageElixirVfxAnimator.PlayLoop(duration);
            }
        }

        private void StopSpinalRibcageVfx()
        {
            if (spinalRibcageVfxAnimator != null && spinalRibcageVfxAnimator.IsPlaying)
            {
                spinalRibcageVfxAnimator.StopAndHide();
            }
        }

        private void StopUltraWaterVfx()
        {
            if (ultraWaterVfxAnimator != null && ultraWaterVfxAnimator.IsPlaying)
            {
                ultraWaterVfxAnimator.StopAndHide();
            }
        }

        private void StopRageElixirVfx()
        {
            if (rageElixirVfxAnimator != null && rageElixirVfxAnimator.IsPlaying)
            {
                rageElixirVfxAnimator.StopAndHide();
            }
        }

        private void StopAllBuffVfx()
        {
            StopSpinalRibcageVfx();
            StopUltraWaterVfx();
            StopRageElixirVfx();
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

            if (rageElixirVfxAnimator == null)
            {
                ConsumableItemSO rage = inventory != null ? inventory.GetItemData(ConsumableItemType.RageElixir) : null;
                rageElixirVfxAnimator = FindOrCreateChildVfx(
                    "VFX_RageElixirAura",
                    new Vector3(0f, 0.8f, 0f),
                    new Vector3(1.42f, 1.42f, 1f),
                    -1,
                    ResolveVfxFrames(ConsumableItemType.RageElixir, rage),
                    rage != null ? rage.vfxFps : 8f,
                    new Color(1f, 0.52f, 0.18f, 0.72f));
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
