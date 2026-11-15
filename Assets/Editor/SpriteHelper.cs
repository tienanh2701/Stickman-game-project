using System.IO;
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

            bool isUI = normalized.StartsWith("Assets/Sprites/UI/");
            bool isMap = normalized.StartsWith("Assets/Sprites/Maps/");
            bool isCharacterOrEnemy = normalized.StartsWith("Assets/Sprites/Characters/") || normalized.StartsWith("Assets/Sprites/Enemies/");
            float targetPpu = (isUI || isMap) ? 100f : 160f;
            int targetMaxSize = isUI ? 2048 : 4096;

            string fileName = Path.GetFileName(normalized);
            bool isKiGeyserHazard = normalized == "Assets/Sprites/Combat/Hazards/Hazard_KiGeyser.png";
            bool hasCustomPivot = isCharacterOrEnemy || isKiGeyserHazard;

            SpriteAlignment targetAlignment = SpriteAlignment.Center;
            Vector2 targetPivot = new Vector2(0.5f, 0.5f);
            if (isCharacterOrEnemy)
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

            bool meshDirty = texSettings.spriteMeshType != SpriteMeshType.Tight;
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
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = targetPpu;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = targetMaxSize;

                importer.ReadTextureSettings(texSettings);
                texSettings.spriteMeshType = SpriteMeshType.Tight;
                if (hasCustomPivot)
                {
                    texSettings.spriteAlignment = (int)targetAlignment;
                    texSettings.spritePivot = targetPivot;
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
    }
}
