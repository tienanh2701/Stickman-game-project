using System;
using System.Collections.Generic;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.Tests.CoreUnit.TV2.TestDoubles
{
    /// <summary>
    /// Fake ISaveStorage lưu dữ liệu trong RAM để test Save/Load Round-Trip và Fault-Injection không đụng ổ đĩa.
    /// </summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> store = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public int SaveCallCount { get; private set; }
        public int LoadCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }

        public void SaveText(string key, string content)
        {
            SaveCallCount++;
            store[key] = content ?? string.Empty;
        }

        public string LoadText(string key)
        {
            LoadCallCount++;
            return store.TryGetValue(key, out string value) ? value : string.Empty;
        }

        public bool Exists(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && store.ContainsKey(key);
        }

        public void Delete(string key)
        {
            DeleteCallCount++;
            if (!string.IsNullOrWhiteSpace(key))
            {
                store.Remove(key);
            }
        }

        public void SeedRaw(string key, string rawContent)
        {
            store[key] = rawContent;
        }
    }
}
