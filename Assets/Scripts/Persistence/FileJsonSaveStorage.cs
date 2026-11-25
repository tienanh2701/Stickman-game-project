using System;
using System.IO;
using System.Text;
using UnityEngine;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.Persistence
{
    /// <summary>
    /// Triển khai cụ thể ISaveStorage ghi/đọc tệp JSON UTF-8 an toàn theo kiểu atomic (.tmp -> Replace/Copy).
    /// </summary>
    public sealed class FileJsonSaveStorage : ISaveStorage
    {
        private readonly string rootDirectory;

        public string RootDirectory => rootDirectory;

        public FileJsonSaveStorage(string customRootDirectory = null)
        {
            rootDirectory = string.IsNullOrWhiteSpace(customRootDirectory)
                ? Path.Combine(Application.persistentDataPath, "Saves")
                : customRootDirectory;
        }

        public string GetFilePath(string key)
        {
            string safeKey = SanitizeKey(key);
            return Path.Combine(rootDirectory, safeKey + ".json");
        }

        public void SaveText(string key, string content)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new SaveCorruptedException(key ?? string.Empty, "Storage key cannot be empty.");
            }

            string targetPath = GetFilePath(key);
            string tmpPath = targetPath + ".tmp";
            string bakPath = targetPath + ".bak";

            try
            {
                if (!Directory.Exists(rootDirectory))
                {
                    Directory.CreateDirectory(rootDirectory);
                }

                File.WriteAllText(tmpPath, content ?? string.Empty, new UTF8Encoding(false));

                if (File.Exists(targetPath))
                {
                    try
                    {
                        File.Replace(tmpPath, targetPath, bakPath, ignoreMetadataErrors: true);
                    }
                    catch (IOException)
                    {
                        File.Copy(tmpPath, targetPath, overwrite: true);
                    }
                }
                else
                {
                    File.Move(tmpPath, targetPath);
                }
            }
            catch (IOException ex)
            {
                throw new SaveCorruptedException(key, $"I/O error writing save '{key}': {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new SaveCorruptedException(key, $"Access denied writing save '{key}': {ex.Message}", ex);
            }
            finally
            {
                SafeDeleteTransient(tmpPath);
                SafeDeleteTransient(bakPath);
            }
        }

        public string LoadText(string key)
        {
            string targetPath = GetFilePath(key);
            if (!File.Exists(targetPath))
            {
                throw new SaveCorruptedException(key, $"Save file not found at '{targetPath}'.");
            }

            try
            {
                return File.ReadAllText(targetPath, Encoding.UTF8);
            }
            catch (IOException ex)
            {
                throw new SaveCorruptedException(key, $"I/O error reading save '{key}': {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new SaveCorruptedException(key, $"Access denied reading save '{key}': {ex.Message}", ex);
            }
        }

        public bool Exists(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return File.Exists(GetFilePath(key));
        }

        public void Delete(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            string targetPath = GetFilePath(key);
            SafeDeleteTransient(targetPath);
        }

        private static string SanitizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "default";
            StringBuilder sb = new StringBuilder(key.Length);
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                bool valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                sb.Append(valid ? c : '_');
            }
            return sb.ToString();
        }

        private static void SafeDeleteTransient(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                File.Delete(path);
            }
            catch (IOException ex)
            {
                Debug.Log($"[FileJsonSaveStorage][CLEANUP_IO] Skipped transient delete '{path}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.Log($"[FileJsonSaveStorage][CLEANUP_PERM] Skipped transient delete '{path}': {ex.Message}");
            }
        }
    }
}
