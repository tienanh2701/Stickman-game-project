using System;
using System.Collections.Generic;

namespace StickmanBeatEmUp.CorePure.Settings
{
    /// <summary>
    /// Pure C# bijective 1-to-1 keybinding map using Unity KeyCode integer values.
    /// Guarantees two actions can never share the same key code.
    /// </summary>
    [Serializable]
    public sealed class KeyBindingMap
    {
        public const int ActionCount = 12;
        public const int EscapeKeyCode = 27;

        private static readonly int[] DefaultWasdCodes =
        {
            97,  // MoveLeft = A
            100, // MoveRight = D
            32,  // Jump = Space
            119, // AltJump = W
            106, // Attack = J
            117, // Skill1 = U
            105, // Skill2 = I
            111, // Transform = O
            115, // Block = S
            113, // ItemQ = Q
            101, // ItemE = E
            27   // Pause = Escape (locked)
        };

        private static readonly int[] ArrowKeysZxCodes =
        {
            276, // MoveLeft = LeftArrow
            275, // MoveRight = RightArrow
            32,  // Jump = Space
            273, // AltJump = UpArrow
            122, // Attack = Z
            120, // Skill1 = X
            99,  // Skill2 = C
            118, // Transform = V
            274, // Block = DownArrow
            97,  // ItemQ = A
            115, // ItemE = S
            27   // Pause = Escape (locked)
        };

        private static readonly int[] FallbackKeyPool = BuildFallbackPool();

        private readonly int[] bindings = new int[ActionCount];

        public static IReadOnlyList<int> SupportedKeyCodes => FallbackKeyPool;

        public KeyBindingMap()
        {
            ApplyPreset(ControlPresetId.DefaultWasd);
        }

        private KeyBindingMap(int[] sourceCodes)
        {
            if (sourceCodes == null || sourceCodes.Length != ActionCount)
            {
                ApplyPreset(ControlPresetId.DefaultWasd);
                return;
            }

            Array.Copy(sourceCodes, bindings, ActionCount);
            ValidateAndSanitize();
        }

        private static int[] BuildFallbackPool()
        {
            List<int> pool = new List<int>(48);
            for (int c = 97; c <= 122; c++) pool.Add(c); // A..Z
            for (int c = 48; c <= 57; c++) pool.Add(c);  // Alpha0..Alpha9
            pool.Add(32);  // Space
            pool.Add(9);   // Tab
            pool.Add(13);  // Return
            pool.Add(273); // UpArrow
            pool.Add(274); // DownArrow
            pool.Add(275); // RightArrow
            pool.Add(276); // LeftArrow
            pool.Add(304); // LeftShift
            pool.Add(303); // RightShift
            pool.Add(306); // LeftControl
            pool.Add(305); // RightControl
            pool.Add(EscapeKeyCode);
            return pool.ToArray();
        }

        public static bool IsSupportedKeyCode(int code)
        {
            if (code >= 97 && code <= 122) return true; // A..Z
            if (code >= 48 && code <= 57) return true;  // Alpha0..Alpha9
            switch (code)
            {
                case 9:   // Tab
                case 13:  // Return
                case 27:  // Escape
                case 32:  // Space
                case 273: // UpArrow
                case 274: // DownArrow
                case 275: // RightArrow
                case 276: // LeftArrow
                case 303: // RightShift
                case 304: // LeftShift
                case 305: // RightControl
                case 306: // LeftControl
                    return true;
                default:
                    return false;
            }
        }

        public int GetKeyCode(GameInputAction action)
        {
            int idx = (int)action;
            if (idx < 0 || idx >= ActionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }
            return bindings[idx];
        }

        public GameInputAction? FindConflictAction(GameInputAction targetAction, int candidateKeyCode)
        {
            for (int i = 0; i < ActionCount; i++)
            {
                if (i == (int)targetAction) continue;
                if (bindings[i] == candidateKeyCode)
                {
                    return (GameInputAction)i;
                }
            }
            return null;
        }

        public bool HasAnyDuplicate()
        {
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < ActionCount; i++)
            {
                if (!seen.Add(bindings[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public KeyBindChangeResult TrySetBinding(
            GameInputAction action,
            int newKeyCode,
            KeyBindConflictPolicy policy = KeyBindConflictPolicy.Reject)
        {
            int idx = (int)action;
            if (idx < 0 || idx >= ActionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            int previousCode = bindings[idx];

            if (action == GameInputAction.Pause)
            {
                return new KeyBindChangeResult(
                    KeyBindChangeStatus.ReservedActionLocked,
                    action,
                    newKeyCode,
                    previousCode,
                    GameInputAction.Pause);
            }

            if (!IsSupportedKeyCode(newKeyCode))
            {
                return new KeyBindChangeResult(
                    KeyBindChangeStatus.UnsupportedKeyRejected,
                    action,
                    newKeyCode,
                    previousCode);
            }

            if (newKeyCode == EscapeKeyCode)
            {
                return new KeyBindChangeResult(
                    KeyBindChangeStatus.ReservedKeyRejected,
                    action,
                    newKeyCode,
                    previousCode,
                    GameInputAction.Pause);
            }

            if (previousCode == newKeyCode)
            {
                return new KeyBindChangeResult(
                    KeyBindChangeStatus.Unchanged,
                    action,
                    newKeyCode,
                    previousCode);
            }

            GameInputAction? conflict = FindConflictAction(action, newKeyCode);
            if (conflict.HasValue)
            {
                if (conflict.Value == GameInputAction.Pause)
                {
                    return new KeyBindChangeResult(
                        KeyBindChangeStatus.ReservedKeyRejected,
                        action,
                        newKeyCode,
                        previousCode,
                        GameInputAction.Pause);
                }

                if (policy == KeyBindConflictPolicy.Reject)
                {
                    return new KeyBindChangeResult(
                        KeyBindChangeStatus.ConflictRejected,
                        action,
                        newKeyCode,
                        previousCode,
                        conflict.Value);
                }

                bindings[idx] = newKeyCode;
                bindings[(int)conflict.Value] = previousCode;
                return new KeyBindChangeResult(
                    KeyBindChangeStatus.Swapped,
                    action,
                    newKeyCode,
                    previousCode,
                    conflict.Value);
            }

            bindings[idx] = newKeyCode;
            return new KeyBindChangeResult(
                KeyBindChangeStatus.Applied,
                action,
                newKeyCode,
                previousCode);
        }

        public void ApplyPreset(ControlPresetId preset)
        {
            switch (preset)
            {
                case ControlPresetId.ArrowKeysZx:
                    Array.Copy(ArrowKeysZxCodes, bindings, ActionCount);
                    break;
                case ControlPresetId.Custom:
                    ValidateAndSanitize();
                    break;
                default:
                    Array.Copy(DefaultWasdCodes, bindings, ActionCount);
                    break;
            }
        }

        public ControlPresetId DetectPreset()
        {
            if (MatchesCodes(DefaultWasdCodes)) return ControlPresetId.DefaultWasd;
            if (MatchesCodes(ArrowKeysZxCodes)) return ControlPresetId.ArrowKeysZx;
            return ControlPresetId.Custom;
        }

        private bool MatchesCodes(int[] expected)
        {
            for (int i = 0; i < ActionCount; i++)
            {
                if (bindings[i] != expected[i]) return false;
            }
            return true;
        }

        public void ValidateAndSanitize()
        {
            bindings[(int)GameInputAction.Pause] = EscapeKeyCode;

            HashSet<int> used = new HashSet<int> { EscapeKeyCode };
            for (int i = 0; i < ActionCount; i++)
            {
                if (i == (int)GameInputAction.Pause) continue;

                int code = bindings[i];
                if (!IsSupportedKeyCode(code) || code == EscapeKeyCode || !used.Add(code))
                {
                    bindings[i] = -1;
                }
            }

            for (int i = 0; i < ActionCount; i++)
            {
                if (bindings[i] != -1) continue;

                int assigned = FindFirstUnusedKey(used, DefaultWasdCodes[i]);
                bindings[i] = assigned;
                used.Add(assigned);
            }
        }

        private static int FindFirstUnusedKey(HashSet<int> used, int preferredDefault)
        {
            if (preferredDefault != EscapeKeyCode && !used.Contains(preferredDefault))
            {
                return preferredDefault;
            }

            for (int i = 0; i < DefaultWasdCodes.Length; i++)
            {
                int cand = DefaultWasdCodes[i];
                if (cand != EscapeKeyCode && !used.Contains(cand))
                {
                    return cand;
                }
            }

            for (int i = 0; i < FallbackKeyPool.Length; i++)
            {
                int cand = FallbackKeyPool[i];
                if (cand != EscapeKeyCode && !used.Contains(cand))
                {
                    return cand;
                }
            }

            return preferredDefault;
        }

        public string SerializeToCsv()
        {
            string[] parts = new string[ActionCount];
            for (int i = 0; i < ActionCount; i++)
            {
                parts[i] = bindings[i].ToString();
            }
            return string.Join(",", parts);
        }

        public static KeyBindingMap DeserializeFromCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new KeyBindingMap();
            }

            string[] parts = csv.Split(',');
            int[] parsed = new int[ActionCount];
            for (int i = 0; i < ActionCount; i++)
            {
                parsed[i] = -1;
                if (i < parts.Length && int.TryParse(parts[i].Trim(), out int code))
                {
                    parsed[i] = code;
                }
            }
            return new KeyBindingMap(parsed);
        }

        public KeyBindingMap Clone()
        {
            return new KeyBindingMap(bindings);
        }
    }
}
