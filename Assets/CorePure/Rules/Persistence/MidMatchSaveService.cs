using System;
using StickmanBeatEmUp.CorePure.Entities.DTOs;
using StickmanBeatEmUp.CorePure.Exceptions;
using StickmanBeatEmUp.CorePure.Interfaces;

namespace StickmanBeatEmUp.CorePure.Rules.Persistence
{
    /// <summary>
    /// Dịch vụ Save/Load trạng thái giữa trận thuần C# (DIP qua ISaveStorage và IGameClock).
    /// </summary>
    public sealed class MidMatchSaveService
    {
        public const string DEFAULT_SAVE_KEY = "midmatch_save";

        private readonly ISaveStorage storage;
        private readonly IGameClock clock;

        public MidMatchSaveService(ISaveStorage storage, IGameClock clock = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.clock = clock ?? new SystemGameClock();
        }

        public void SaveState(GameStateDTO state, string storageKey = DEFAULT_SAVE_KEY)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            string key = string.IsNullOrWhiteSpace(storageKey) ? DEFAULT_SAVE_KEY : storageKey;
            if (string.IsNullOrWhiteSpace(state.savedAtUtc))
            {
                state.savedAtUtc = clock.UtcNowIso;
            }
            if (state.elapsedSeconds <= 0f)
            {
                state.elapsedSeconds = clock.ElapsedSeconds;
            }

            state.Validate(key);
            string json = GameStateJsonCodec.Serialize(state);
            storage.SaveText(key, json);
        }

        public GameStateDTO LoadState(string storageKey = DEFAULT_SAVE_KEY)
        {
            string key = string.IsNullOrWhiteSpace(storageKey) ? DEFAULT_SAVE_KEY : storageKey;
            if (!storage.Exists(key))
            {
                throw new SaveCorruptedException(key, $"Save slot '{key}' does not exist.");
            }

            string json = storage.LoadText(key);
            return GameStateJsonCodec.DeserializeOrThrow(json, key);
        }

        public bool HasSave(string storageKey = DEFAULT_SAVE_KEY)
        {
            string key = string.IsNullOrWhiteSpace(storageKey) ? DEFAULT_SAVE_KEY : storageKey;
            return storage.Exists(key);
        }

        public void DeleteSave(string storageKey = DEFAULT_SAVE_KEY)
        {
            string key = string.IsNullOrWhiteSpace(storageKey) ? DEFAULT_SAVE_KEY : storageKey;
            storage.Delete(key);
        }
    }
}
