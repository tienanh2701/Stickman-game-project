using System;
using UnityEditor;
using UnityEngine;

namespace StickmanBeatEmUp.EditorTools
{
    /// <summary>
    /// Automatically applies optimal Unity 6 audio import settings for all clips under Assets/Resources/Audio/.
    /// - BGM tracks: Streaming + Vorbis (0.70) to minimize runtime RAM footprint.
    /// - Combat/State SFX: DecompressOnLoad + Vorbis (0.75) for zero-latency one-shot playback.
    /// </summary>
    public class AudioImportPostprocessor : AssetPostprocessor
    {
        private const string AudioRootPrefix = "Assets/Resources/Audio/";
        private const string BgmRootPrefix = "Assets/Resources/Audio/BGM/";

        private void OnPreprocessAudio()
        {
            string normalizedPath = string.IsNullOrEmpty(assetPath)
                ? string.Empty
                : assetPath.Replace('\\', '/');

            if (!normalizedPath.StartsWith(AudioRootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AudioImporter importer = assetImporter as AudioImporter;
            if (importer == null) return;

            bool isBgm = normalizedPath.StartsWith(BgmRootPrefix, StringComparison.OrdinalIgnoreCase);

            importer.forceToMono = false;
            importer.loadInBackground = false;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = isBgm ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isBgm ? 0.70f : 0.75f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }

        [MenuItem("Tools/Stickman/Reimport Project Audio")]
        public static void ReimportAllProjectAudio()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" });
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
        }
    }
}
