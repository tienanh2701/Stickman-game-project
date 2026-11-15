using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StickmanBeatEmUp.Editor
{
    /// <summary>
    /// Editor utility that trims excess transparent padding / white borders on character and enemy sprites
    /// to an exact 2px transparent safety margin and configures Tight mesh + joint pivots + PPU 160.
    /// </summary>
    public static class SpriteAutoTrimmer
    {
        private const int SafetyPadding = 2;
        private const byte AlphaVisibleThreshold = 13; // > 0.05f
        private const float TargetPixelsPerUnit = 160f;
        private const int TargetMaxTextureSize = 4096;

        private static readonly string[] TargetFolders =
        {
            "Assets/Sprites/Characters",
            "Assets/Sprites/Enemies"
        };

        /// <summary>
        /// Auto-trims and configures all PNG sprites under Assets/Sprites/Characters and Assets/Sprites/Enemies.
        /// </summary>
        [MenuItem("Tools/Sprites/Auto-Trim & Fix All Sprites", false, 20)]
        public static void TrimAndConfigureAllSprites()
        {
            List<string> assetPaths = CollectTargetSpritePaths();
            if (assetPaths.Count == 0)
            {
                Debug.LogWarning("[SpriteAutoTrimmer] No sprite textures found in Characters/ or Enemies/.");
                return;
            }

            int trimmedPngCount = 0;
            int configuredImporterCount = 0;
            bool needRefreshAfterPngWrite = false;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < assetPaths.Count; i++)
                {
                    string assetPath = assetPaths[i];
                    EditorUtility.DisplayProgressBar(
                        "Auto-Trim & Fix All Sprites",
                        Path.GetFileName(assetPath),
                        (float)i / assetPaths.Count);

                    if (TryCleanAndTrimPngOnDisk(assetPath))
                    {
                        trimmedPngCount++;
                        needRefreshAfterPngWrite = true;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            if (needRefreshAfterPngWrite)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < assetPaths.Count; i++)
                {
                    string assetPath = assetPaths[i];
                    EditorUtility.DisplayProgressBar(
                        "Configuring Sprite Importers",
                        Path.GetFileName(assetPath),
                        (float)i / assetPaths.Count);

                    if (ConfigureSpriteImporter(assetPath))
                    {
                        configuredImporterCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[SpriteAutoTrimmer] Processed {assetPaths.Count} sprites: " +
                $"{trimmedPngCount} PNG(s) trimmed/cleaned, {configuredImporterCount} importer(s) updated.");
        }

        private static List<string> CollectTargetSpritePaths()
        {
            List<string> results = new List<string>();
            List<string> existingFolders = new List<string>();
            foreach (string folder in TargetFolders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    existingFolders.Add(folder);
                }
            }

            if (existingFolders.Count == 0)
            {
                return results;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", existingFolders.ToArray());
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path) &&
                    path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(path);
                }
            }

            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }

        private static bool TryCleanAndTrimPngOnDisk(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            byte[] rawBytes = File.ReadAllBytes(fullPath);
            Texture2D srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!srcTex.LoadImage(rawBytes, false))
                {
                    return false;
                }

                int width = srcTex.width;
                int height = srcTex.height;
                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                Color32[] pixels = srcTex.GetPixels32();
                bool hasWhiteEdge = HasSolidWhiteBorderPixel(pixels, width, height);
                bool hasOpaqueOuterBorder = HasNonZeroOuterBorderAlpha(pixels, width, height);

                if (hasWhiteEdge)
                {
                    FloodFillClearWhiteBackground(pixels, width, height);
                }

                if (!FindTightBounds(pixels, width, height, out int minX, out int minY, out int maxX, out int maxY))
                {
                    return false;
                }

                int leftPad = minX;
                int bottomPad = minY;
                int rightPad = width - 1 - maxX;
                int topPad = height - 1 - maxY;

                bool marginMismatch =
                    leftPad != SafetyPadding ||
                    bottomPad != SafetyPadding ||
                    rightPad != SafetyPadding ||
                    topPad != SafetyPadding;

                // Write back only when cleaning/trimming is actually needed so already-trimmed PNGs are untouched.
                if (!hasWhiteEdge && !hasOpaqueOuterBorder && !marginMismatch)
                {
                    return false;
                }

                int cropW = maxX - minX + 1;
                int cropH = maxY - minY + 1;
                int newW = cropW + SafetyPadding * 2;
                int newH = cropH + SafetyPadding * 2;

                Color32[] dstPixels = new Color32[newW * newH];
                for (int cy = 0; cy < cropH; cy++)
                {
                    int srcRowOffset = (minY + cy) * width + minX;
                    int dstRowOffset = (SafetyPadding + cy) * newW + SafetyPadding;
                    for (int cx = 0; cx < cropW; cx++)
                    {
                        dstPixels[dstRowOffset + cx] = pixels[srcRowOffset + cx];
                    }
                }

                Texture2D outTex = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
                try
                {
                    outTex.SetPixels32(dstPixels);
                    outTex.Apply(false, false);
                    byte[] encoded = outTex.EncodeToPNG();
                    if (encoded != null && encoded.Length > 0)
                    {
                        File.WriteAllBytes(fullPath, encoded);
                        return true;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(outTex);
                }

                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(srcTex);
            }
        }

        private static bool HasSolidWhiteBorderPixel(Color32[] pixels, int width, int height)
        {
            for (int x = 0; x < width; x++)
            {
                if (IsOpaqueNearWhite(pixels[x]) || IsOpaqueNearWhite(pixels[(height - 1) * width + x]))
                {
                    return true;
                }
            }

            for (int y = 0; y < height; y++)
            {
                if (IsOpaqueNearWhite(pixels[y * width]) || IsOpaqueNearWhite(pixels[y * width + (width - 1)]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasNonZeroOuterBorderAlpha(Color32[] pixels, int width, int height)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[x].a != 0 || pixels[(height - 1) * width + x].a != 0)
                {
                    return true;
                }
            }

            for (int y = 0; y < height; y++)
            {
                if (pixels[y * width].a != 0 || pixels[y * width + (width - 1)].a != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOpaqueNearWhite(Color32 c)
        {
            return c.a > 12 && c.r > 242 && c.g > 242 && c.b > 242 && (MaxChannelDiff(c) <= 12);
        }

        private static int MaxChannelDiff(Color32 c)
        {
            int mx = c.r > c.g ? (c.r > c.b ? c.r : c.b) : (c.g > c.b ? c.g : c.b);
            int mn = c.r < c.g ? (c.r < c.b ? c.r : c.b) : (c.g < c.b ? c.g : c.b);
            return mx - mn;
        }

        private static void FloodFillClearWhiteBackground(Color32[] pixels, int width, int height)
        {
            int total = width * height;
            bool[] visited = new bool[total];
            Queue<int> queue = new Queue<int>();

            for (int i = 0; i < total; i++)
            {
                int x = i % width;
                int y = i / width;
                bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                if (pixels[i].a <= 12 || (isBorder && IsOpaqueNearWhite(pixels[i])))
                {
                    visited[i] = true;
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                pixels[idx] = new Color32(0, 0, 0, 0);

                int x = idx % width;
                int y = idx / width;

                TryEnqueue(x - 1, y, width, height, pixels, visited, queue);
                TryEnqueue(x + 1, y, width, height, pixels, visited, queue);
                TryEnqueue(x, y - 1, width, height, pixels, visited, queue);
                TryEnqueue(x, y + 1, width, height, pixels, visited, queue);
            }
        }

        private static void TryEnqueue(
            int x,
            int y,
            int width,
            int height,
            Color32[] pixels,
            bool[] visited,
            Queue<int> queue)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int idx = y * width + x;
            if (visited[idx])
            {
                return;
            }

            if (pixels[idx].a <= 12 || IsOpaqueNearWhite(pixels[idx]))
            {
                visited[idx] = true;
                queue.Enqueue(idx);
            }
        }

        private static bool FindTightBounds(
            Color32[] pixels,
            int width,
            int height,
            out int minX,
            out int minY,
            out int maxX,
            out int maxY)
        {
            minX = width;
            minY = height;
            maxX = -1;
            maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[rowOffset + x].a >= AlphaVisibleThreshold)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            return maxX >= minX && maxY >= minY;
        }

        private static void ResolveTargetPivot(string fileName, out SpriteAlignment alignment, out Vector2 pivot)
        {
            if (fileName == "Mecha_LeftArm.png")
            {
                alignment = SpriteAlignment.Custom;
                pivot = new Vector2(0.642105f, 0.805634f);
            }
            else if (fileName == "Mecha_RightArm.png")
            {
                alignment = SpriteAlignment.Custom;
                pivot = new Vector2(0.246914f, 0.650485f);
            }
            else if (fileName == "Mecha_ChestCore.png")
            {
                alignment = SpriteAlignment.Center;
                pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                alignment = SpriteAlignment.BottomCenter;
                pivot = new Vector2(0.5f, 0.0f);
            }
        }

        private static bool ConfigureSpriteImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            string fileName = Path.GetFileName(assetPath);
            ResolveTargetPivot(fileName, out SpriteAlignment targetAlignment, out Vector2 targetPivot);

            TextureImporterSettings texSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(texSettings);

            bool isMultiple = importer.spriteImportMode == SpriteImportMode.Multiple;
            bool metaDirty =
                importer.textureType != TextureImporterType.Sprite ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, TargetPixelsPerUnit) ||
                importer.filterMode != FilterMode.Bilinear ||
                !importer.alphaIsTransparency ||
                importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.maxTextureSize != TargetMaxTextureSize ||
                texSettings.spriteMeshType != SpriteMeshType.Tight;

            bool singlePivotDirty = !isMultiple && (
                texSettings.spriteAlignment != (int)targetAlignment ||
                Vector2.Distance(texSettings.spritePivot, targetPivot) > 0.0005f);

            bool multipleSpritesDirty = isMultiple && AreMultipleSpriteSheetsDirty(importer, targetAlignment, targetPivot);

            if (!metaDirty && !singlePivotDirty && !multipleSpritesDirty)
            {
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = TargetPixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = TargetMaxTextureSize;

            importer.ReadTextureSettings(texSettings);
            texSettings.spriteMeshType = SpriteMeshType.Tight;
            if (!isMultiple)
            {
                texSettings.spriteAlignment = (int)targetAlignment;
                texSettings.spritePivot = targetPivot;
            }
            importer.SetTextureSettings(texSettings);

            if (isMultiple)
            {
                UpdateMultipleSpriteSheetPivots(importer, targetAlignment, targetPivot);
            }

            importer.SaveAndReimport();
            return true;
        }

        private static bool AreMultipleSpriteSheetsDirty(
            TextureImporter importer,
            SpriteAlignment targetAlignment,
            Vector2 targetPivot)
        {
            SerializedObject so = new SerializedObject(importer);
            SerializedProperty spritesProp = so.FindProperty("m_SpriteSheet.m_Sprites");
            if (spritesProp == null || !spritesProp.isArray)
            {
                return false;
            }

            for (int i = 0; i < spritesProp.arraySize; i++)
            {
                SerializedProperty elem = spritesProp.GetArrayElementAtIndex(i);
                SerializedProperty alignProp = elem.FindPropertyRelative("m_Alignment");
                SerializedProperty pivotProp = elem.FindPropertyRelative("m_Pivot");
                if (alignProp != null && alignProp.intValue != (int)targetAlignment)
                {
                    return true;
                }

                if (pivotProp != null && Vector2.Distance(pivotProp.vector2Value, targetPivot) > 0.0005f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void UpdateMultipleSpriteSheetPivots(
            TextureImporter importer,
            SpriteAlignment targetAlignment,
            Vector2 targetPivot)
        {
            SerializedObject so = new SerializedObject(importer);
            SerializedProperty spritesProp = so.FindProperty("m_SpriteSheet.m_Sprites");
            if (spritesProp == null || !spritesProp.isArray)
            {
                return;
            }

            for (int i = 0; i < spritesProp.arraySize; i++)
            {
                SerializedProperty elem = spritesProp.GetArrayElementAtIndex(i);
                SerializedProperty alignProp = elem.FindPropertyRelative("m_Alignment");
                SerializedProperty pivotProp = elem.FindPropertyRelative("m_Pivot");
                if (alignProp != null)
                {
                    alignProp.intValue = (int)targetAlignment;
                }

                if (pivotProp != null)
                {
                    pivotProp.vector2Value = targetPivot;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
