using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StickmanBeatEmUp.UI
{
    /// <summary>
    /// Điều khiển hình nền toàn cục cho toàn bộ màn hình Pre-Match (MainMenu, ModeSelect, Shop,
    /// Loadout/CharacterSelect, WorldSelect, StageSelect): ưu tiên nạp MenuGlobalBackground.png
    /// hoặc Background.jfif kèm lớp phủ tối nhẹ (Alpha = 0.20f) và tự ẩn/hủy khi vào Stickman_DemoScene.
    /// </summary>
    [ExecuteAlways]
    public class MenuBackgroundLoader : MonoBehaviour
    {
        private const string GLOBAL_BG_ASSET_PATH = "Assets/Sprites/UI/MenuGlobalBackground.png";
        private const string BATTLE_SCENE_NAME = "Stickman_DemoScene";
        private const string LEGACY_THUMB_NAME = "MainMenu_ThreeHeroes_Thumb";

        [Header("Custom Thumbnail (Drag & Drop in Inspector)")]
        [SerializeField] private Sprite customThumbnail;
        [SerializeField] [Range(0f, 1f)] private float darkOverlayAlpha = 0.20f;

        [Header("UI Targets")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image darkOverlayImage;

        private Texture2D runtimeLoadedTexture;
        private Sprite runtimeLoadedSprite;
        private bool isSceneHooked;

        private void Awake()
        {
            if (CheckAndHideForGameplayScene(SceneManager.GetActiveScene())) return;
            ApplyVisuals();
        }

        private void OnEnable()
        {
            HookSceneLoaded();
            if (CheckAndHideForGameplayScene(SceneManager.GetActiveScene())) return;
            ApplyVisuals();
        }

        private void Start()
        {
            if (CheckAndHideForGameplayScene(SceneManager.GetActiveScene())) return;
            ApplyVisuals();
        }

        private void OnDisable()
        {
            UnhookSceneLoaded();
        }

        private void OnDestroy()
        {
            UnhookSceneLoaded();
            CleanupRuntimeAssets();
        }

        private void OnValidate()
        {
            ApplyVisuals();
        }

        public void ConfigureTargets(Image bgImg, Image overlayImg, Sprite defaultThumb)
        {
            backgroundImage = bgImg;
            darkOverlayImage = overlayImg;
            if (customThumbnail == null && defaultThumb != null)
            {
                customThumbnail = defaultThumb;
            }
            ApplyVisuals();
        }

        public void SetOverlayAlpha(float alpha)
        {
            darkOverlayAlpha = Mathf.Clamp01(alpha);
            ApplyVisuals();
        }

        public void ApplyVisuals()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            bool loadedGlobal = false;
            Sprite targetSprite = ResolveBackgroundSprite(out loadedGlobal);

            if (backgroundImage != null)
            {
                if (targetSprite != null)
                {
                    backgroundImage.sprite = targetSprite;
                }
                backgroundImage.color = Color.white;
                backgroundImage.preserveAspect = false;
                backgroundImage.raycastTarget = false;
            }

            if (darkOverlayImage != null)
            {
                if (Application.isPlaying &&
                    loadedGlobal &&
                    IsLegacyThumbnail(customThumbnail) &&
                    Mathf.Approximately(darkOverlayAlpha, 0.6f))
                {
                    darkOverlayAlpha = 0.20f;
                }

                darkOverlayImage.color = new Color(0.02f, 0.03f, 0.07f, darkOverlayAlpha);
                darkOverlayImage.raycastTarget = false;
            }
        }

        private Sprite ResolveBackgroundSprite(out bool loadedGlobal)
        {
            loadedGlobal = false;

#if UNITY_EDITOR
            Sprite editorGlobal = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(GLOBAL_BG_ASSET_PATH);
            if (editorGlobal != null)
            {
                loadedGlobal = true;
                return editorGlobal;
            }
#endif

            Sprite resGlobal = Resources.Load<Sprite>("UI/MenuGlobalBackground")
                ?? Resources.Load<Sprite>("MenuGlobalBackground");
            if (resGlobal != null)
            {
                loadedGlobal = true;
                return resGlobal;
            }

            if (customThumbnail != null && !IsLegacyThumbnail(customThumbnail))
            {
                return customThumbnail;
            }

            if (Application.isPlaying)
            {
                if (runtimeLoadedSprite != null)
                {
                    loadedGlobal = true;
                    return runtimeLoadedSprite;
                }

                Sprite diskSprite = TryLoadGlobalBackgroundFromDisk();
                if (diskSprite != null)
                {
                    loadedGlobal = true;
                    return diskSprite;
                }
            }

            if (customThumbnail != null)
            {
                return customThumbnail;
            }

            return backgroundImage != null ? backgroundImage.sprite : null;
        }

        private Sprite TryLoadGlobalBackgroundFromDisk()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo dataParent = !string.IsNullOrEmpty(dataPath) ? Directory.GetParent(dataPath) : null;
            DirectoryInfo rootParent = dataParent != null ? dataParent.Parent : null;

            string[] candidates =
            {
                Path.Combine(dataPath, "Sprites", "UI", "MenuGlobalBackground.png"),
                Path.Combine(Application.streamingAssetsPath, "MenuGlobalBackground.png"),
                dataParent != null ? Path.Combine(dataParent.FullName, "MenuGlobalBackground.png") : string.Empty,
                dataParent != null ? Path.Combine(dataParent.FullName, "Background.jfif") : string.Empty,
                rootParent != null ? Path.Combine(rootParent.FullName, "MenuGlobalBackground.png") : string.Empty,
                rootParent != null ? Path.Combine(rootParent.FullName, "Background.jfif") : string.Empty,
                Path.Combine(Application.streamingAssetsPath, "Background.jfif")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate)) continue;

                try
                {
                    byte[] bytes = File.ReadAllBytes(candidate);
                    if (bytes == null || bytes.Length == 0) continue;

                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    if (!tex.LoadImage(bytes, false))
                    {
                        DestroyImmediateSafe(tex);
                        continue;
                    }

                    tex.hideFlags = HideFlags.DontSave;
                    Sprite sprite = Sprite.Create(
                        tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0,
                        SpriteMeshType.FullRect,
                        Vector4.zero
                    );
                    sprite.hideFlags = HideFlags.DontSave;

                    CleanupRuntimeAssets();
                    runtimeLoadedTexture = tex;
                    runtimeLoadedSprite = sprite;
                    return runtimeLoadedSprite;
                }
                catch (IOException ex)
                {
                    Debug.Log($"[MenuBackgroundLoader][BG_IO_ERROR] Unreadable background candidate '{candidate}': {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.Log($"[MenuBackgroundLoader][BG_PERM_ERROR] Access denied for background '{candidate}': {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    Debug.Log($"[MenuBackgroundLoader][BG_ARG_ERROR] Invalid background image '{candidate}': {ex.Message}");
                }
            }

            return null;
        }

        private void HookSceneLoaded()
        {
            if (isSceneHooked || !Application.isPlaying) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            isSceneHooked = true;
        }

        private void UnhookSceneLoaded()
        {
            if (!isSceneHooked) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            isSceneHooked = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            CheckAndHideForGameplayScene(scene);
        }

        private bool CheckAndHideForGameplayScene(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid()) return false;
            if (!string.Equals(scene.name, BATTLE_SCENE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            gameObject.SetActive(false);
            Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponent<Canvas>() : null;
            if (parentCanvas != null && transform.parent.childCount <= 1)
            {
                parentCanvas.gameObject.SetActive(false);
                Destroy(parentCanvas.gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            return true;
        }

        private static bool IsLegacyThumbnail(Sprite sprite)
        {
            return sprite != null && string.Equals(sprite.name, LEGACY_THUMB_NAME, StringComparison.Ordinal);
        }

        private void CleanupRuntimeAssets()
        {
            if (runtimeLoadedSprite != null)
            {
                DestroyImmediateSafe(runtimeLoadedSprite);
                runtimeLoadedSprite = null;
            }
            if (runtimeLoadedTexture != null)
            {
                DestroyImmediateSafe(runtimeLoadedTexture);
                runtimeLoadedTexture = null;
            }
        }

        private static void DestroyImmediateSafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
