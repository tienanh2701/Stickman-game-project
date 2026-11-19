using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StickmanBeatEmUp.Tests.E2E.PlayMode
{
    public sealed class ScopedPlayerStateBackup : IDisposable
    {
        private enum PrefKind
        {
            Int,
            String
        }

        private struct PrefSnapshot
        {
            public string Key;
            public PrefKind Kind;
            public bool Existed;
            public int IntValue;
            public string StringValue;
        }

        private struct FileSnapshot
        {
            public string Path;
            public bool Existed;
            public byte[] Bytes;
        }

        private static readonly (string Key, PrefKind Kind)[] KnownPrefKeys =
        {
            ("MDS_PlayerCoins", PrefKind.Int),
            ("MDS_ItemOwned_SenzuBean", PrefKind.Int),
            ("MDS_ItemOwned_SpinalFluid", PrefKind.Int),
            ("MDS_ItemOwned_UltraDivineWater", PrefKind.Int),
            ("MDS_EquippedSlot_0", PrefKind.Int),
            ("MDS_EquippedSlot_1", PrefKind.Int),
            ("SelectedCharacter", PrefKind.Int),
            ("SelectedWorld", PrefKind.Int),
            ("SelectedStageIndex", PrefKind.Int),
            ("SelectedGameMode", PrefKind.Int),
            ("Stickman_UnlockedStage", PrefKind.Int),
            ("MDS_ActiveAccountUsername", PrefKind.String),
        };

        private static readonly string[] AccountFileNames =
        {
            "stickman_accounts.json",
            "stickman_accounts.json.tmp",
            "stickman_accounts.json.bak",
            "stickman_accounts.tmp",
            "stickman_accounts.bak",
        };

        private readonly PrefSnapshot[] prefSnapshots;
        private readonly FileSnapshot[] fileSnapshots;
        private bool disposed;

        public string AccountsFilePath => Path.Combine(Application.persistentDataPath, "stickman_accounts.json");

        public ScopedPlayerStateBackup(bool clearForCleanTest = true)
        {
            prefSnapshots = new PrefSnapshot[KnownPrefKeys.Length];
            for (int i = 0; i < KnownPrefKeys.Length; i++)
            {
                string key = KnownPrefKeys[i].Key;
                PrefKind kind = KnownPrefKeys[i].Kind;
                bool existed = PlayerPrefs.HasKey(key);
                prefSnapshots[i] = new PrefSnapshot
                {
                    Key = key,
                    Kind = kind,
                    Existed = existed,
                    IntValue = (existed && kind == PrefKind.Int) ? PlayerPrefs.GetInt(key) : 0,
                    StringValue = (existed && kind == PrefKind.String) ? PlayerPrefs.GetString(key) : string.Empty,
                };
            }

            string dataDir = Application.persistentDataPath;
            fileSnapshots = new FileSnapshot[AccountFileNames.Length];
            for (int i = 0; i < AccountFileNames.Length; i++)
            {
                string fullPath = Path.Combine(dataDir, AccountFileNames[i]);
                bool existed = File.Exists(fullPath);
                fileSnapshots[i] = new FileSnapshot
                {
                    Path = fullPath,
                    Existed = existed,
                    Bytes = existed ? File.ReadAllBytes(fullPath) : null,
                };
            }

            if (clearForCleanTest)
            {
                ClearStateForCleanTest();
            }
        }

        public void ClearStateForCleanTest()
        {
            for (int i = 0; i < KnownPrefKeys.Length; i++)
            {
                PlayerPrefs.DeleteKey(KnownPrefKeys[i].Key);
            }
            PlayerPrefs.Save();

            DeleteAccountFilesOnDisk();
            SyncOrResetRuntimeSingletons(destroyAccountManager: true);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            for (int i = 0; i < prefSnapshots.Length; i++)
            {
                PrefSnapshot s = prefSnapshots[i];
                if (!s.Existed)
                {
                    PlayerPrefs.DeleteKey(s.Key);
                }
                else if (s.Kind == PrefKind.Int)
                {
                    PlayerPrefs.SetInt(s.Key, s.IntValue);
                }
                else
                {
                    PlayerPrefs.SetString(s.Key, s.StringValue ?? string.Empty);
                }
            }
            PlayerPrefs.Save();

            DeleteAccountFilesOnDisk();
            for (int i = 0; i < fileSnapshots.Length; i++)
            {
                FileSnapshot f = fileSnapshots[i];
                if (f.Existed)
                {
                    string dir = Path.GetDirectoryName(f.Path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllBytes(f.Path, f.Bytes);
                }
            }

            SyncOrResetRuntimeSingletons(destroyAccountManager: true);
        }

        private static void DeleteAccountFilesOnDisk()
        {
            string dataDir = Application.persistentDataPath;
            for (int i = 0; i < AccountFileNames.Length; i++)
            {
                string fullPath = Path.Combine(dataDir, AccountFileNames[i]);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
        }

        private static void SyncOrResetRuntimeSingletons(bool destroyAccountManager)
        {
            Time.timeScale = 1f;

            // 1. CurrencyManager
            Type currencyType = E2ETestHelpers.FindRuntimeType("StickmanBeatEmUp.Economy.CurrencyManager");
            if (currencyType != null)
            {
                object cm = GetActiveSingletonInstance(currencyType);
                if (cm != null)
                {
                    PropertyInfo suppressProp = currencyType.GetProperty("SuppressEnemyRewards", BindingFlags.Instance | BindingFlags.Public);
                    if (suppressProp != null && suppressProp.CanWrite)
                    {
                        suppressProp.SetValue(cm, false);
                    }

                    MethodInfo reloadMethod = currencyType.GetMethod("ReloadProfileState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (reloadMethod != null)
                    {
                        reloadMethod.Invoke(cm, null);
                    }
                    else
                    {
                        MethodInfo loadCoins = currencyType.GetMethod("LoadCoins", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (loadCoins != null)
                        {
                            loadCoins.Invoke(cm, null);
                        }
                    }
                }
            }

            // 2. InventoryManager
            Type inventoryType = E2ETestHelpers.FindRuntimeType("StickmanBeatEmUp.Items.InventoryManager");
            if (inventoryType != null)
            {
                object im = GetActiveSingletonInstance(inventoryType);
                if (im != null)
                {
                    PropertyInfo freeUseProp = inventoryType.GetProperty("IsTrainingFreeUseMode", BindingFlags.Instance | BindingFlags.Public);
                    if (freeUseProp != null && freeUseProp.CanWrite)
                    {
                        freeUseProp.SetValue(im, false);
                    }

                    MethodInfo reloadMethod = inventoryType.GetMethod("ReloadProfileState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (reloadMethod != null)
                    {
                        reloadMethod.Invoke(im, null);
                    }
                    else
                    {
                        MethodInfo loadInv = inventoryType.GetMethod("LoadInventoryAndSlots", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (loadInv != null)
                        {
                            loadInv.Invoke(im, null);
                        }
                    }
                }
            }

            // 3. StageManager
            Type stageType = E2ETestHelpers.FindRuntimeType("StickmanBeatEmUp.Level.StageManager");
            if (stageType != null)
            {
                MethodInfo loadMode = stageType.GetMethod("LoadSelectedGameMode", BindingFlags.Static | BindingFlags.Public);
                if (loadMode != null)
                {
                    loadMode.Invoke(null, null);
                }
            }

            // 4. AccountManager (M3)
            if (destroyAccountManager)
            {
                Type accountType = E2ETestHelpers.FindRuntimeType("StickmanBeatEmUp.Auth.AccountManager");
                if (accountType != null)
                {
                    object acct = GetActiveSingletonInstance(accountType);
                    if (acct is Component comp && comp != null)
                    {
                        UnityEngine.Object.DestroyImmediate(comp.gameObject);
                    }
                }
            }
        }

        private static object GetActiveSingletonInstance(Type managerType)
        {
            PropertyInfo hasProp = managerType.GetProperty("HasInstance", BindingFlags.Static | BindingFlags.Public);
            if (hasProp != null)
            {
                object hasObj = hasProp.GetValue(null);
                if (hasObj is bool hasInst && !hasInst)
                {
                    return null;
                }
            }

            FieldInfo instField = managerType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (instField != null)
            {
                return instField.GetValue(null);
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(managerType))
            {
                return UnityEngine.Object.FindAnyObjectByType(managerType);
            }

            return null;
        }
    }

    public sealed class ScopedLogCapture : IDisposable
    {
        public struct CapturedLog
        {
            public LogType Type;
            public string Message;
            public string StackTrace;
        }

        private readonly List<CapturedLog> unexpectedLogs = new List<CapturedLog>();
        private readonly Func<LogType, string, bool> allowFilter;
        private bool attached;

        public ScopedLogCapture(Func<LogType, string, bool> allowFilter = null)
        {
            this.allowFilter = allowFilter;
            Application.logMessageReceived += OnLogReceived;
            attached = true;
        }

        public IReadOnlyList<CapturedLog> UnexpectedLogs => unexpectedLogs;

        public void AssertNoWarningsOrErrors()
        {
            if (unexpectedLogs.Count == 0)
            {
                return;
            }

            var lines = new List<string>(unexpectedLogs.Count);
            for (int i = 0; i < unexpectedLogs.Count; i++)
            {
                CapturedLog item = unexpectedLogs[i];
                lines.Add($"[{item.Type}] {item.Message}");
            }

            Assert.Fail("Unexpected warning/error logs captured:\n" + string.Join("\n", lines));
        }

        public void AssertNoWarningOrErrorLogs()
        {
            AssertNoWarningsOrErrors();
        }

        public void Clear()
        {
            unexpectedLogs.Clear();
        }

        public void Dispose()
        {
            if (attached)
            {
                Application.logMessageReceived -= OnLogReceived;
                attached = false;
            }
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log)
            {
                return;
            }

            if (allowFilter != null && allowFilter(type, condition))
            {
                return;
            }

            unexpectedLogs.Add(new CapturedLog
            {
                Type = type,
                Message = condition,
                StackTrace = stackTrace,
            });
        }
    }

    public static class E2ETestHelpers
    {
        public static Type FindRuntimeType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            Type t = Type.GetType(fullName, throwOnError: false);
            if (t != null)
            {
                return t;
            }

            t = Type.GetType(fullName + ", Assembly-CSharp", throwOnError: false);
            if (t != null)
            {
                return t;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(assemblies[i].GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))
                {
                    t = assemblies[i].GetType(fullName, throwOnError: false);
                    if (t != null)
                    {
                        return t;
                    }
                }
            }

            for (int i = 0; i < assemblies.Length; i++)
            {
                t = assemblies[i].GetType(fullName, throwOnError: false);
                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        public static Type RequireRuntimeType(string fullName)
        {
            Type t = FindRuntimeType(fullName);
            Assert.IsNotNull(t, $"Required runtime type '{fullName}' could not be resolved via reflection.");
            return t;
        }

        public static ScopedLogCapture CaptureLogs(Func<LogType, string, bool> allowFilter = null)
        {
            return new ScopedLogCapture(allowFilter);
        }

        public static IEnumerator LoadSceneAndWait(string sceneName)
        {
            Assert.IsTrue(
                Application.CanStreamedLevelBeLoaded(sceneName),
                $"Scene '{sceneName}' is not registered in Build Settings / cannot be loaded.");

            Time.timeScale = 1f;
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, $"LoadSceneAsync('{sceneName}') returned null.");

            while (!op.isDone)
            {
                yield return null;
            }

            // Allow Awake(), OnEnable(), sceneLoaded callbacks, and Start() to run.
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        public static IEnumerator WaitForCondition(
            Func<bool> predicate,
            float timeoutSeconds = 5f,
            string failureMessage = null)
        {
            Assert.IsNotNull(predicate, "WaitForCondition predicate cannot be null.");
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!predicate())
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Assert.Fail(failureMessage ?? $"Timed out after {timeoutSeconds:0.##}s waiting for condition.");
                }
                yield return null;
            }
        }

        public static GameObject FindUIElement(string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath))
            {
                return null;
            }

            int sceneCount = SceneManager.sceneCount;
            if (nameOrPath.Contains("/"))
            {
                string trimmed = nameOrPath.TrimStart('/');
                string[] parts = trimmed.Split('/');
                if (parts.Length == 0)
                {
                    return null;
                }

                // Pass 1: Exact root-to-descendant path
                for (int s = 0; s < sceneCount; s++)
                {
                    Scene scene = SceneManager.GetSceneAt(s);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int i = 0; i < roots.Length; i++)
                    {
                        if (string.Equals(roots[i].name, parts[0], StringComparison.Ordinal))
                        {
                            if (parts.Length == 1)
                            {
                                return roots[i];
                            }

                            string subPath = string.Join("/", parts, 1, parts.Length - 1);
                            Transform exact = roots[i].transform.Find(subPath);
                            if (exact != null)
                            {
                                return exact.gameObject;
                            }
                        }
                    }
                }

                // Pass 2: Relative suffix path across all descendants
                string leafName = parts[parts.Length - 1];
                for (int s = 0; s < sceneCount; s++)
                {
                    Scene scene = SceneManager.GetSceneAt(s);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int i = 0; i < roots.Length; i++)
                    {
                        Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                        for (int j = 0; j < all.Length; j++)
                        {
                            if (!string.Equals(all[j].name, leafName, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            if (MatchesSuffixChain(all[j], parts))
                            {
                                return all[j].gameObject;
                            }
                        }
                    }
                }

                return null;
            }

            GameObject firstMatch = null;
            GameObject uniqueActiveMatch = null;
            int activeCount = 0;

            for (int s = 0; s < sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                    for (int j = 0; j < all.Length; j++)
                    {
                        if (!string.Equals(all[j].name, nameOrPath, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (firstMatch == null)
                        {
                            firstMatch = all[j].gameObject;
                        }

                        if (all[j].gameObject.activeInHierarchy)
                        {
                            activeCount++;
                            if (activeCount == 1)
                            {
                                uniqueActiveMatch = all[j].gameObject;
                            }
                            else
                            {
                                uniqueActiveMatch = null;
                            }
                        }
                    }
                }
            }

            return uniqueActiveMatch != null ? uniqueActiveMatch : firstMatch;
        }

        public static void ClickButton(string nameOrPath)
        {
            GameObject go = FindUIElement(nameOrPath);
            Assert.IsNotNull(go, $"UI Button GameObject '{nameOrPath}' not found.");
            Button btn = go.GetComponent<Button>();
            Assert.IsNotNull(btn, $"GameObject '{nameOrPath}' does not have a Button component.");
            ClickButton(btn);
        }

        public static void ClickButton(Button button)
        {
            Assert.IsNotNull(button, "Button to click must not be null.");
            Assert.IsTrue(button.gameObject.activeInHierarchy, $"Button '{button.name}' is not activeInHierarchy.");
            Assert.IsTrue(button.IsInteractable(), $"Button '{button.name}' is not interactable.");
            Assert.IsNotNull(EventSystem.current, "EventSystem.current is missing in active scene.");

            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Canvas.ForceUpdateCanvases();
        }

        public static Rect GetRectBoundsIn1920x1080(RectTransform rt, Canvas rootCanvas)
        {
            Assert.IsNotNull(rt, "RectTransform must not be null.");
            Assert.IsNotNull(rootCanvas, "Root Canvas must not be null.");

            Canvas.ForceUpdateCanvases();
            RectTransform canvasRt = rootCanvas.transform as RectTransform;
            Assert.IsNotNull(canvasRt, "Root Canvas transform is not a RectTransform.");

            Rect canvasRect = canvasRt.rect;
            if (Mathf.Abs(canvasRect.width - 1920f) < 0.5f && Mathf.Abs(canvasRect.height - 1080f) < 0.5f)
            {
                Vector3[] worldCorners = new Vector3[4];
                rt.GetWorldCorners(worldCorners);

                float minX = float.MaxValue;
                float minY = float.MaxValue;
                float maxX = float.MinValue;
                float maxY = float.MinValue;

                for (int i = 0; i < 4; i++)
                {
                    Vector3 local = canvasRt.InverseTransformPoint(worldCorners[i]);
                    float x = local.x - canvasRect.xMin;
                    float y = local.y - canvasRect.yMin;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }

                return Rect.MinMaxRect(minX, minY, maxX, maxY);
            }

            List<RectTransform> chain = new List<RectTransform>();
            Transform cursor = rt;
            while (cursor != null && cursor != rootCanvas.transform)
            {
                RectTransform step = cursor as RectTransform;
                if (step == null)
                {
                    Assert.Fail($"Non-RectTransform ancestor '{cursor.name}' found between '{rt.name}' and '{rootCanvas.name}'.");
                }
                chain.Add(step);
                cursor = cursor.parent;
            }

            Assert.AreEqual(rootCanvas.transform, cursor, $"'{rt.name}' is not a descendant of Canvas '{rootCanvas.name}'.");

            Vector2 rootPivot = canvasRt.pivot;
            Rect currentLocalRect = new Rect(-1920f * rootPivot.x, -1080f * rootPivot.y, 1920f, 1080f);
            Matrix4x4 currentTo1920 = Matrix4x4.Translate(new Vector3(1920f * rootPivot.x, 1080f * rootPivot.y, 0f));

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                RectTransform c = chain[i];
                Vector2 anchorMinPos = new Vector2(
                    currentLocalRect.xMin + c.anchorMin.x * currentLocalRect.width,
                    currentLocalRect.yMin + c.anchorMin.y * currentLocalRect.height);
                Vector2 anchorMaxPos = new Vector2(
                    currentLocalRect.xMin + c.anchorMax.x * currentLocalRect.width,
                    currentLocalRect.yMin + c.anchorMax.y * currentLocalRect.height);

                Vector2 minInParent = anchorMinPos + c.offsetMin;
                Vector2 maxInParent = anchorMaxPos + c.offsetMax;
                Vector2 size = maxInParent - minInParent;
                Vector2 pivotInParent = minInParent + Vector2.Scale(size, c.pivot);

                currentLocalRect = new Rect(-size.x * c.pivot.x, -size.y * c.pivot.y, size.x, size.y);
                Matrix4x4 localToParent = Matrix4x4.TRS(
                    new Vector3(pivotInParent.x, pivotInParent.y, 0f),
                    c.localRotation,
                    c.localScale);
                currentTo1920 = currentTo1920 * localToParent;
            }

            Vector3[] localCorners =
            {
                new Vector3(currentLocalRect.xMin, currentLocalRect.yMin, 0f),
                new Vector3(currentLocalRect.xMin, currentLocalRect.yMax, 0f),
                new Vector3(currentLocalRect.xMax, currentLocalRect.yMax, 0f),
                new Vector3(currentLocalRect.xMax, currentLocalRect.yMin, 0f),
            };

            float rMinX = float.MaxValue;
            float rMinY = float.MaxValue;
            float rMaxX = float.MinValue;
            float rMaxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector3 pt = currentTo1920.MultiplyPoint3x4(localCorners[i]);
                if (pt.x < rMinX) rMinX = pt.x;
                if (pt.y < rMinY) rMinY = pt.y;
                if (pt.x > rMaxX) rMaxX = pt.x;
                if (pt.y > rMaxY) rMaxY = pt.y;
            }

            return Rect.MinMaxRect(rMinX, rMinY, rMaxX, rMaxY);
        }

        private static bool MatchesSuffixChain(Transform leaf, string[] parts)
        {
            Transform current = leaf;
            for (int k = parts.Length - 1; k >= 0; k--)
            {
                if (current == null || !string.Equals(current.name, parts[k], StringComparison.Ordinal))
                {
                    return false;
                }
                current = current.parent;
            }
            return true;
        }
    }
}
