using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace StickmanBeatEmUp.Editor
{
    /// <summary>
    /// Tạo các file ảnh Sprite placeholder cơ bản (Square, Circle) để render nhân vật và quái
    /// </summary>
    public static class SpriteHelper
    {
        private const string SPRITES_FOLDER = "Assets/Sprites/Placeholders";
        private const string VN_FONT_TTF_PATH = "Assets/Fonts/SegoeUIBoldVN.ttf";
        private const string VN_TMP_FONT_ASSET_PATH = "Assets/Resources/Fonts/SegoeUIBoldVN SDF.asset";

        public static TMP_FontAsset GetOrCreateVietnameseTmpFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(VN_TMP_FONT_ASSET_PATH);
            if (existing != null &&
                existing.material != null &&
                existing.atlasTextures != null &&
                existing.atlasTextures.Length > 0 &&
                existing.atlasTextures[0] != null)
            {
                return existing;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(VN_TMP_FONT_ASSET_PATH);
            }

            EnsureFolder("Assets/Resources/Fonts");

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(VN_FONT_TTF_PATH);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[SpriteHelper] Không tìm thấy font nguồn tại '{VN_FONT_TTF_PATH}'.");
                return null;
            }

            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (created == null)
            {
                Debug.LogWarning("[SpriteHelper] TMP_FontAsset.CreateFontAsset trả về null.");
                return null;
            }

            created.name = "SegoeUIBoldVN SDF";
            created.hideFlags = HideFlags.None;

            AssetDatabase.CreateAsset(created, VN_TMP_FONT_ASSET_PATH);

            if (created.atlasTextures != null)
            {
                for (int i = 0; i < created.atlasTextures.Length; i++)
                {
                    Texture2D atlas = created.atlasTextures[i];
                    if (atlas == null) continue;
                    atlas.name = i == 0 ? "SegoeUIBoldVN SDF Atlas" : $"SegoeUIBoldVN SDF Atlas {i}";
                    atlas.hideFlags = HideFlags.None;
                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlas)))
                    {
                        AssetDatabase.AddObjectToAsset(atlas, created);
                    }
                }
            }

            if (created.material != null)
            {
                created.material.name = "SegoeUIBoldVN SDF Material";
                created.material.hideFlags = HideFlags.None;
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(created.material)))
                {
                    AssetDatabase.AddObjectToAsset(created.material, created);
                }
            }

            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(VN_TMP_FONT_ASSET_PATH, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(VN_TMP_FONT_ASSET_PATH);
        }

        public static Sprite GetOrCreateSquareSprite()
        {
            EnsureFolder(SPRITES_FOLDER);
            string path = $"{SPRITES_FOLDER}/Square.png";

            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;

            Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            ConfigureTextureImporter(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Sprite GetOrCreateCircleSprite()
        {
            EnsureFolder(SPRITES_FOLDER);
            string path = $"{SPRITES_FOLDER}/Circle.png";

            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    colors[y * size + x] = dist <= radius ? Color.white : Color.clear;
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            ConfigureTextureImporter(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void ConfigureAllProjectSprites()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
            {
                return;
            }

            string diskSpritesRoot = Path.Combine(Application.dataPath, "Sprites");
            if (Directory.Exists(diskSpritesRoot))
            {
                string normRoot = diskSpritesRoot.Replace('\\', '/').TrimEnd('/') + "/";
                string[] pngFiles = Directory.GetFiles(diskSpritesRoot, "*.png", SearchOption.AllDirectories);
                foreach (string diskPath in pngFiles)
                {
                    string normFile = diskPath.Replace('\\', '/');
                    string relSub = normFile.StartsWith(normRoot) ? normFile.Substring(normRoot.Length) : Path.GetFileName(normFile);
                    string relPath = "Assets/Sprites/" + relSub;
                    if (AssetImporter.GetAtPath(relPath) == null)
                    {
                        AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);
                    }
                    ConfigureImporterForPath(relPath);
                }
            }
        }

        public static Sprite LoadSprite(string assetPath, Sprite fallback = null)
        {
            if (string.IsNullOrEmpty(assetPath)) return fallback;

            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sp != null) return sp;

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ConfigureImporterForPath(assetPath);
                sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }

            return sp != null ? sp : fallback;
        }

        private static void ConfigureImporterForPath(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            if (normalized.StartsWith(SPRITES_FOLDER + "/"))
            {
                return; // Keep Placeholders at PPU 32 Point filter
            }

            TextureImporter importer = AssetImporter.GetAtPath(normalized) as TextureImporter;
            if (importer == null) return;

            bool isSourceSheet = normalized.StartsWith("Assets/Sprites/SourceSheets/");
            if (isSourceSheet)
            {
                bool needsChange = importer.maxTextureSize != 4096 || !importer.alphaIsTransparency;
                if (needsChange)
                {
                    importer.maxTextureSize = 4096;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                return;
            }

            // Preserve Multiple SpriteMode & slices on VFX sprite sheets (e.g., HitSpark_Sheet.png)
            if (normalized.StartsWith("Assets/Sprites/VFX/") &&
                (normalized.EndsWith("_Sheet.png") || importer.spriteImportMode == SpriteImportMode.Multiple))
            {
                bool sheetDirty =
                    importer.textureType != TextureImporterType.Sprite ||
                    importer.filterMode != FilterMode.Bilinear ||
                    !importer.alphaIsTransparency ||
                    importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed;
                if (sheetDirty)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                return;
            }

            bool isUI = normalized.StartsWith("Assets/Sprites/UI/");
            bool isSlicedUI = normalized.StartsWith("Assets/Sprites/UI/SlicedUI/");
            bool isGlassUI = normalized.StartsWith("Assets/Sprites/UI/Glassmorphism/") ||
                             normalized.StartsWith("Assets/Sprites/UI/GlassHUD/") ||
                             normalized.StartsWith("Assets/Sprites/UI/Glass/") ||
                             isSlicedUI;
            bool isVfxSingle = normalized.StartsWith("Assets/Sprites/VFX/");
            bool isCombatSkill = normalized.StartsWith("Assets/Sprites/Combat/Skills/");
            bool isProtectFrame = normalized.StartsWith("Assets/Sprites/ProtectFrames/");
            bool isCharacterOrEnemy = normalized.StartsWith("Assets/Sprites/Characters/") || normalized.StartsWith("Assets/Sprites/Enemies/") || isProtectFrame;
            float targetPpu = (isUI || normalized.StartsWith("Assets/Sprites/Maps/") || isVfxSingle || isCombatSkill) ? 100f : 160f;
            int targetMaxSize = isUI ? 2048 : 4096;
            if (isSlicedUI && TryReadPngDimensions(normalized, out int pngW, out int pngH) && (pngW > 2048 || pngH > 2048))
            {
                targetMaxSize = 4096;
            }

            string fileName = Path.GetFileName(normalized);
            bool isKiGeyserHazard = normalized == "Assets/Sprites/Combat/Hazards/Hazard_KiGeyser.png";
            bool hasCustomPivot = isCharacterOrEnemy || isKiGeyserHazard || isVfxSingle || isCombatSkill;

            SpriteAlignment targetAlignment = SpriteAlignment.Center;
            Vector2 targetPivot = new Vector2(0.5f, 0.5f);
            if (isProtectFrame && (fileName.Contains("_Protect_17x") || fileName.Contains("Shield")))
            {
                targetAlignment = SpriteAlignment.BottomCenter;
                targetPivot = new Vector2(0.5f, 0.0f);
            }
            else if (isCombatSkill)
            {
                targetAlignment = SpriteAlignment.Custom;
                targetPivot = new Vector2(0.045f, 0.5f);
            }
            else if (isVfxSingle)
            {
                targetAlignment = SpriteAlignment.Center;
                targetPivot = new Vector2(0.5f, 0.5f);
            }
            else if (isCharacterOrEnemy)
            {
                if (fileName == "Mecha_LeftArm.png")
                {
                    targetAlignment = SpriteAlignment.Custom;
                    targetPivot = new Vector2(0.642105f, 0.805634f);
                }
                else if (fileName == "Mecha_RightArm.png")
                {
                    targetAlignment = SpriteAlignment.Custom;
                    targetPivot = new Vector2(0.246914f, 0.650485f);
                }
                else if (fileName == "Mecha_ChestCore.png")
                {
                    targetAlignment = SpriteAlignment.Center;
                    targetPivot = new Vector2(0.5f, 0.5f);
                }
                else
                {
                    targetAlignment = SpriteAlignment.BottomCenter;
                    targetPivot = new Vector2(0.5f, 0.0f);
                }
            }
            else if (isKiGeyserHazard)
            {
                targetAlignment = SpriteAlignment.BottomCenter;
                targetPivot = new Vector2(0.5f, 0.0f);
            }

            TextureImporterSettings texSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(texSettings);

            SpriteMeshType desiredMeshType = (isGlassUI || isCombatSkill) ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            bool meshDirty = texSettings.spriteMeshType != desiredMeshType;
            bool pivotDirty = hasCustomPivot && (
                texSettings.spriteAlignment != (int)targetAlignment ||
                Vector2.Distance(texSettings.spritePivot, targetPivot) > 0.005f
            );

            bool dirty =
                meshDirty ||
                pivotDirty ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, targetPpu) ||
                importer.filterMode != FilterMode.Bilinear ||
                !importer.alphaIsTransparency ||
                importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.maxTextureSize != targetMaxSize;

            if (dirty)
            {
                Vector4 preservedBorder = importer.spriteBorder;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = targetPpu;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = targetMaxSize;
                if (isGlassUI && preservedBorder != Vector4.zero)
                {
                    importer.spriteBorder = preservedBorder;
                }

                importer.ReadTextureSettings(texSettings);
                texSettings.spriteMeshType = desiredMeshType;
                if (hasCustomPivot)
                {
                    texSettings.spriteAlignment = (int)targetAlignment;
                    texSettings.spritePivot = targetPivot;
                }
                if (isGlassUI && preservedBorder != Vector4.zero)
                {
                    texSettings.spriteBorder = preservedBorder;
                }
                importer.SetTextureSettings(texSettings);

                importer.SaveAndReimport();
            }
        }

        private static void ConfigureTextureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        public static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static bool TryReadPngDimensions(string assetPath, out int width, out int height)
        {
            width = 0;
            height = 0;
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return false;
            }

            using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < 24) return false;
                byte[] hdr = new byte[24];
                if (fs.Read(hdr, 0, 24) < 24) return false;

                // PNG signature (89 50 4E 47) + IHDR width/height big-endian at offsets 16..23
                if (hdr[0] != 0x89 || hdr[1] != 0x50 || hdr[2] != 0x4E || hdr[3] != 0x47)
                {
                    return false;
                }

                width = (hdr[16] << 24) | (hdr[17] << 16) | (hdr[18] << 8) | hdr[19];
                height = (hdr[20] << 24) | (hdr[21] << 16) | (hdr[22] << 8) | hdr[23];
                return width > 0 && height > 0;
            }
        }
    }
}
